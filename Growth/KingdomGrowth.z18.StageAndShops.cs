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
			bool priorShopClaim = System.HasShopkeeper;
			KingdomSurvey marketSurvey = Survey ?? KingdomSurvey.Take(Z, System);
			// Standing comes from one live accepted market fixture, current craft, and its
			// designated ground. Population is a ceiling only and never creates wares.
			if (!TryMarketServiceStanding(System, marketSurvey, out int tier,
				out bool liveMarketCapability, out string standingFailure))
			{
				System.HasShopkeeper = false;
				System.ShopTier = 0;
				KingdomLog.Log("market standing: exact reading waits ("
					+ (standingFailure ?? "unknown failure") + ")"); return;
			}
			if (!ReconcileMarketOffice(System, Z, marketSurvey, tier,
				liveMarketCapability, priorShopClaim,
				out string marketFailure))
			{
				System.HasShopkeeper = false;
				System.ShopTier = 0;
				KingdomLog.Log("market office: exact reconciliation waits ("
					+ (marketFailure ?? "unknown failure") + ")");
				return;
			}
			if (System.HasShopkeeper)
			{
				if (!TryAcknowledgeMarketStanding(System, Z, tier))
				{
					System.HasShopkeeper = false;
					System.ShopTier = 0;
				}
			}
			else
			{
				if (!KingdomMarketStockDetachment.TryRetireServiceStock(System,
					marketSurvey, System.CurrentSettlementId, out marketFailure))
				{
					System.HasShopkeeper = false;
					System.ShopTier = 0;
					KingdomLog.Log("market retirement: exact reconciliation waits ("
						+ (marketFailure ?? "unknown failure") + ")");
					return;
				}
				System.ShopTier = 0;
			}
		}

		/// <summary>Acknowledges one newly reached local-market service tier without touching stock.
		/// Qud's native trade screen moves exact physical items and water between the two loaded bodies;
		/// this transition only records how capable the settlement's market has become. It never rolls a
		/// population table, searches remote stores, or creates, replaces, stamps, deletes, or restocks an
		/// item. The historical method name remains public for source compatibility.</summary>
		public static void RestockShops(KingdomSystem System, Zone Z, int Tier)
		{
			if (TryAcknowledgeMarketStanding(System, Z, Tier)) return;
			if (System != null) { System.HasShopkeeper = false; System.ShopTier = 0; }
		}

		private static bool TryAcknowledgeMarketStanding(KingdomSystem System,
			Zone Z, int Tier)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
			string settlementId = System?.SettlementIdForOwnedZone(Z?.ZoneID);
			if (string.IsNullOrEmpty(settlementId)
				|| settlementId != System.CurrentSettlementId) return false;
			GameObject merchant = null;
			int merchants = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty("VillageMerchant") != 1
					|| !TryAuthorizedMarketBody(System, Z, item, Tier,
						out bool _))
				{
					continue;
				}
				merchants++;
				merchant = item;
			}
			if (merchant == null || merchants != 1
				|| !RetireLegacyMarketOutputIntent(System, merchant, settlementId)) return false;
			if (!NoAutomaticMarketStockAuthority(merchant) || merchant.Inventory == null
				|| Tier < KingdomShopStockRules.FirstPhysicalMarketTier
				|| Tier > KingdomShopStockRules.MaximumTier) return false;
			List<GameObject> before = new List<GameObject>(merchant.Inventory.Objects);
			if (!KingdomShopStockRules.SamePhysicalSet(before, merchant.Inventory.Objects)) return false;
			merchant.SetIntProperty("InventoryTier", Tier);
			r_KingdomOfficeProjection office = merchant.GetPart<r_KingdomOfficeProjection>();
			if (office != null && office.MarketServicePhase == 2) office.MarketTier = Tier;
			if (!KingdomShopStockRules.SamePhysicalSet(before, merchant.Inventory.Objects)) return false;
			System.ShopTier = Tier;
			if (!PublishMarketTierAcknowledgement(System, settlementId, Tier))
				KingdomLog.Log("market standing: historical acknowledgement waits");
			return KingdomShopStockRules.SamePhysicalSet(before, merchant.Inventory.Objects);
		}

		private static bool PublishMarketTierAcknowledgement(KingdomSystem System,
			string SettlementId, int Tier)
		{
			string receipt = KingdomShopStockRules.TierReceiptId(System?.RealmId,
				SettlementId, Tier);
			return receipt != null && KingdomChronicle.RecordOnce(System, receipt,
				"the local market of " + System.KingdomDisplayName + " was acknowledged at service tier "
				+ Tier + "; its wares remain only the physical goods brought to its keeper");
		}

		private static bool NoAutomaticMarketStockAuthority(GameObject Merchant)
		{
			GenericInventoryRestocker restocker = Merchant?.GetPart<GenericInventoryRestocker>();
			return restocker == null || SealedFiniteRestocker(restocker);
		}

		/// <summary>Old code could save an exact intent after committing ShopTier. Seal only that
		/// authority and close its marker. Existing objects are deliberately left untouched because an
		/// interrupted callback cannot prove which unstamped object was old, bought, or generated.</summary>
		private static bool RetireLegacyMarketOutputIntent(KingdomSystem System,
			GameObject Merchant, string SettlementId)
		{
			string held = Merchant?.GetStringProperty(KingdomShopStockRules.IssueIntentProperty);
			if (string.IsNullOrEmpty(held)) return true;
			if (System == null || System.ShopTier < 1 || held != KingdomShopStockRules.IntentReceipt(
				KingdomShopStockRules.SourceId(System.RealmId, SettlementId, System.ShopTier))) return false;
			GenericInventoryRestocker restocker = Merchant.GetPart<GenericInventoryRestocker>();
			if (restocker != null)
			{
				restocker.Clear(); restocker.Chance = 0;
				restocker.RestockFrequency = long.MaxValue;
				restocker.LastRestockTick = Math.Max(1L, The.Game.TimeTicks);
			}
			Merchant.SetStringProperty(KingdomShopStockRules.IssueIntentProperty, null,
				RemoveIfNull: true);
			return NoAutomaticMarketStockAuthority(Merchant);
		}
	}
}
