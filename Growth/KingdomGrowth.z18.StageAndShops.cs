using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		/// <summary>
		/// What the settlement has become, both ways, and the reckoning that can move it.
		/// <para>
		/// The ratchet this replaced only ever climbed (<c>if (stage &gt; System.Stage)</c>), so a
		/// City could hold four people and <c>StageFor</c>'s own answer for a collapsed settlement
		/// was computed and thrown away. It now runs in both directions, with a band on the way
		/// down (<c>KingdomSubsidenceRules.StageWithHysteresis</c>) so a rung cannot flap on a
		/// single arrival, and the way DOWN is driven by subsidence rather than by this line: the
		/// reckoning below moves the people, and the stage follows them.
		/// </para>
		/// <para>
		/// Order is load-bearing. The reckoning runs first, because it is what may change the
		/// population and the stage; the rise is then asked of the figures that reckoning left,
		/// so a settlement cannot be promoted on people who have already gone. Raising is
		/// deliberately NOT gated on the supported level: hauling may still carry a settlement to
		/// City, because the pillar promises that a city held up by your own hauling settles
		/// back, not that it could never be raised at all.
		/// </para>
		/// <para>
		/// And the whole of this runs after <see cref="AssignWork"/>, which is what makes the
		/// summation honest: a crewed work carries what the staffing pass says it is running at,
		/// so an unmanned field feeds nobody. The cost of that order is that a departure here
		/// leaves the pass's <c>Survey.Settlers</c> holding an obliterated object &mdash; the same
		/// bargain <see cref="ResolveHeartbeat"/>'s own <see cref="Emigrate"/> already makes, and
		/// safe for the same reason: the only reader of that list is the staffing pass, which has
		/// already run.
		/// </para>
		/// </summary>
		public static void UpdateStage(KingdomSystem System, Zone Z, KingdomSurvey Survey = null)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return;
			int zoneCapacity = (Survey != null) ? Survey.StorageCapacity : CountStorageCapacity(Z);
			KingdomSubsidence.Reckon(System, Z, Survey, The.Game.TimeTicks);
			// Read AFTER Reckon, which writes this zone's own sighting. The ladder measures the
			// city's casks, not the casks of whichever zone the founder walked in through.
			int capacity = KingdomSubsidence.CityStorageCapacity(System, Z, zoneCapacity);
			GrowthStage stage = KingdomSubsidenceRules.StageWithHysteresis(System.Stage, System.Population, capacity);
			if (stage > System.Stage)
			{
				System.Stage = stage;
				string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
				string text = realm + " has grown into a " + stage.ToString().ToLower();
				System.RecordDeed("the growth of " + realm);
				KingdomChronicle.Record(System, text, Accomplishment: true);
				Popup.Show(KingdomVoices.Say(System, VoiceOccasion.StageUp, "{{C|" + text + ".}}"));
			}
			else if (stage < System.Stage)
			{
				// The rung a settlement loses without a slide: its people were taken by the
				// drought, or its casks were undedicated, and the place is honestly smaller than
				// the ladder says. Said plainly and never popped up - a stage-up is an
				// achievement and interrupts; a stage-down is news, and the ledger is where news
				// belongs.
				GrowthStage lost = System.Stage;
				System.Stage = stage;
				string text = KingdomPresentation.Rich(System.KingdomDisplayName) + " is a "
					+ stage.ToString().ToLower() + " again, and no longer a "
					+ lost.ToString().ToLower();
				KingdomChronicle.Record(System, text);
				System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(text) + ".}}");
			}
			if (System.HasShopkeeper)
			{
				// The survey already answered this in its single pass; only a call site with
				// no survey (a direct wish, say) needs the fallback scan.
				bool stillTrading = (Survey != null) ? Survey.HasTradePost : StillHasTradePost(System, Z);
				if (!stillTrading)
				{
					System.HasShopkeeper = false;
					KingdomLog.Log("shopkeeper lost; the post reopens");
				}
			}
			if (System.Stage >= GrowthStage.Steading && !System.HasShopkeeper)
			{
				PromoteShopkeeper(System, Z);
			}
			// A market district stocks the stalls a rung above what the settlement's raw size
			// would otherwise carry.
			int tier = KingdomRules.ShopTierForStage(System.Stage) + KingdomRules.DistrictsShopTierBonus(System.ZoneDistricts.Values);
			if (System.HasShopkeeper)
			{
				RestockShops(System, Z, tier);
			}
		}

		private static bool StillHasTradePost(KingdomSystem System, Zone Z)
		{
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty("VillageMerchant") == 1
					&& KingdomCitizenship.BelongsTo(System, item))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Issues one explicitly named local market-output batch for a newly reached tier. ShopTier commits
		/// before the opaque population-table callback, making the issue at-most-once. Vanilla's
		/// periodic restock is disabled: merchant replacement never manufactures another batch.
		/// </summary>
		public static void RestockShops(KingdomSystem System, Zone Z, int Tier)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return;
			Tier = KingdomShopStockRules.NextIssueTier(System.ShopTier, Tier);
			GameObject merchant = null;
			int merchants = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty("VillageMerchant") != 1
					|| !KingdomCitizenship.BelongsTo(System, item))
				{
					continue;
				}
				merchants++;
				merchant = item;
			}
			KingdomShopStockVerdict verdict = KingdomShopStockRules.Classify(
				System.ShopTier, Tier, merchants);
			if (merchant != null && merchants == 1 && RecoverMarketOutputCut(System, merchant)) return;
			if (verdict != KingdomShopStockVerdict.Issue || merchant == null) return;
			string sourceId = KingdomShopStockRules.SourceId(System.RealmId,
				System.CurrentSettlementId, Tier);
			if (sourceId == null) return;
			GenericInventoryRestocker restocker = merchant.GetPart<GenericInventoryRestocker>();
			if (restocker == null) return;
			List<GameObject> before = new List<GameObject>(merchant.Inventory.Objects);
			ProtectPriorMarketStock(before);
			ConfigureFiniteStock(restocker, Tier);
			merchant.SetIntProperty("InventoryTier", Tier);
			merchant.SetStringProperty(KingdomShopStockRules.IssueIntentProperty,
				KingdomShopStockRules.IntentReceipt(sourceId));
			System.ShopTier = Tier;
			Exception callbackFailure = null;
			int stamped = 0;
			try
			{
				restocker.PerformRestock(Silent: true);
			}
			catch (Exception ex) { callbackFailure = ex; }
			finally
			{
				try { stamped = StampMarketOutput(merchant, before, sourceId,
					System.CurrentSettlementId, Tier); }
				catch (Exception ex) { KingdomLog.Log("local market-output stamping failed: " + ex.Message); }
				try { DisableAutomaticStock(restocker); }
				catch (Exception ex) { KingdomLog.Log("shop restocker sealing failed: " + ex.Message); }
				merchant.SetStringProperty(KingdomShopStockRules.IssueIntentProperty, null,
					RemoveIfNull: true);
			}
			KingdomSurvey.ObserveChangedInActive(Z, merchant);
			if (callbackFailure == null && stamped > 0)
			{
				string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
				KingdomChronicle.Record(System, "the stalls of " + realm
					+ " produced one new tier of local market wares");
				MessageQueue.AddPlayerMessage("{{G|The traders of " + realm
					+ " have produced one new batch of local wares.}}");
				if (KingdomLog.Enabled) KingdomLog.Log("local market output issued at tier " +
					Tier + " (" + sourceId + ", " + stamped + " stamped items)");
			}
			else
			{
				KingdomLog.Log(callbackFailure == null
					? "local market output produced no physical items after at-most-once receipt"
					: "local market output callback failed after at-most-once receipt: " +
						callbackFailure.Message);
				MessageQueue.AddPlayerMessage(stamped > 0
					? "{{y|Only part of the new local market batch survived handling. Its stamped goods remain, but its tier receipt is closed.}}"
					: "{{r|The new local market batch was lost in handling. Its tier receipt remains closed, so it will not be duplicated.}}");
			}
		}

		public static void PromoteShopkeeper(KingdomSystem System, Zone Z)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return;
			GameObject citizen = null;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (KingdomCitizenship.BelongsTo(System, item)
					&& item.GetIntProperty("VillageMerchant") == 0 && !item.IsPlayer())
				{
					citizen = item;
					break;
				}
			}
			if (citizen == null)
			{
				return;
			}
			GenericInventoryRestocker restocker = citizen.RequirePart<GenericInventoryRestocker>();
			int tier = Math.Max(1, System.ShopTier);
			ProtectPriorMarketStock(new List<GameObject>(citizen.Inventory.Objects));
			DisableAutomaticStock(restocker);
			citizen.SetIntProperty("VillageMerchant", 1);
			citizen.SetIntProperty("Merchant", 1);
			citizen.SetIntProperty("InventoryTier", tier);
			KingdomSurvey.ObserveChangedInActive(Z, citizen);
			TakeOnRoleEvent.Send(citizen, "Merchant");
			System.HasShopkeeper = true;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			KingdomChronicle.Record(System, "a settler took up the trade, and the first stall opened at " + realm);
			MessageQueue.AddPlayerMessage("{{G|A settler has taken up the trade. The first stall of " +
				realm + " is open. Each newly reached tier can produce one local batch of wares.}}");
		}

		private static void ConfigureFiniteStock(GenericInventoryRestocker Restocker, int Tier)
		{
			Restocker.Clear();
			Restocker.AddTable("Tier" + Tier + "Wares");
			Restocker.Chance = 0;
			Restocker.RestockFrequency = long.MaxValue;
			Restocker.LastRestockTick = Math.Max(1L, The.Game.TimeTicks);
		}

		private static void DisableAutomaticStock(GenericInventoryRestocker Restocker)
		{
			Restocker.Clear();
			Restocker.Chance = 0;
			Restocker.RestockFrequency = long.MaxValue;
			Restocker.LastRestockTick = Math.Max(1L, The.Game.TimeTicks);
		}

		private static void ProtectPriorMarketStock(List<GameObject> Before)
		{
			for (int i = 0; i < Before.Count; i++)
				if (Before[i].HasProperty("_stock")) Before[i].SetIntProperty("norestock", 1);
		}

		private static int StampMarketOutput(GameObject Merchant, List<GameObject> Before,
			string SourceId, string SettlementId, int Tier)
		{
			int count = 0;
			foreach (GameObject item in Merchant.Inventory.Objects)
			{
				if (Before.Contains(item)) continue;
				item.SetStringProperty(KingdomShopStockRules.ItemSourceProperty, SourceId);
				item.SetStringProperty(KingdomShopStockRules.ItemSettlementProperty, SettlementId);
				item.SetIntProperty(KingdomShopStockRules.ItemTierProperty, Tier);
				item.SetIntProperty("norestock", 1);
				count++;
			}
			return count;
		}

		private static bool RecoverMarketOutputCut(KingdomSystem System, GameObject Merchant)
		{
			if (System.ShopTier < 1) return false;
			string source = KingdomShopStockRules.SourceId(System.RealmId,
				System.CurrentSettlementId, System.ShopTier);
			if (Merchant.GetStringProperty(KingdomShopStockRules.IssueIntentProperty)
				!= KingdomShopStockRules.IntentReceipt(source)) return false;
			int stamped = 0;
			foreach (GameObject item in Merchant.Inventory.Objects)
			{
				if (item.GetStringProperty(KingdomShopStockRules.ItemSourceProperty) == source)
				{ stamped++; continue; }
				if (!item.HasProperty("_stock") || item.HasPropertyOrTag("norestock")) continue;
				item.SetStringProperty(KingdomShopStockRules.ItemSourceProperty, source);
				item.SetStringProperty(KingdomShopStockRules.ItemSettlementProperty,
					System.CurrentSettlementId);
				item.SetIntProperty(KingdomShopStockRules.ItemTierProperty, System.ShopTier);
				item.SetIntProperty("norestock", 1); stamped++;
			}
			GenericInventoryRestocker restocker = Merchant.GetPart<GenericInventoryRestocker>();
			if (restocker != null) DisableAutomaticStock(restocker);
			Merchant.SetStringProperty(KingdomShopStockRules.IssueIntentProperty, null,
				RemoveIfNull: true);
			MessageQueue.AddPlayerMessage(stamped > 0
				? "{{y|The market recovered a partly handled local batch; only its stamped goods remain.}}"
				: "{{r|A tier's local market batch was lost before completion and will not be duplicated.}}");
			return true;
		}
	}
}
