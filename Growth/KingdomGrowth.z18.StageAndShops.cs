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
			if (System.HasShopkeeper && tier > System.ShopTier)
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
		/// Raises the settlement's shops to a new stock tier: the trader's stock table and the
		/// per-creature InventoryTier both climb, and the shelves are restocked at once so the
		/// change is visible the moment the player next trades.
		/// </summary>
		public static void RestockShops(KingdomSystem System, Zone Z, int Tier)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return;
			int raised = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty("VillageMerchant") != 1
					|| !KingdomCitizenship.BelongsTo(System, item))
				{
					continue;
				}
				GenericInventoryRestocker restocker = item.GetPart<GenericInventoryRestocker>();
				if (restocker == null)
				{
					continue;
				}
				restocker.Clear();
				restocker.AddTable("Tier" + Tier + "Wares");
				restocker.Chance = 100;
				item.SetIntProperty("InventoryTier", Tier);
				restocker.PerformRestock(Silent: true);
				KingdomSurvey.ObserveChangedInActive(Z, item);
				raised++;
			}
			if (raised > 0)
			{
				System.ShopTier = Tier;
				string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
				KingdomChronicle.Record(System, "the stalls of " + realm + " began carrying finer goods");
				MessageQueue.AddPlayerMessage("{{G|The traders of " + realm + " have better wares to show you.}}");
				if (KingdomLog.Enabled) KingdomLog.Log("shops raised to tier " + Tier + " (" + raised + " traders)");
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
			restocker.Clear();
			restocker.AddTable("Tier1Wares");
			restocker.Chance = 100;
			restocker.PerformRestock(Silent: true);
			citizen.SetIntProperty("VillageMerchant", 1);
			KingdomSurvey.ObserveChangedInActive(Z, citizen);
			TakeOnRoleEvent.Send(citizen, "Merchant");
			System.HasShopkeeper = true;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			KingdomChronicle.Record(System, "a settler took up the trade, and the first stall opened at " + realm);
			MessageQueue.AddPlayerMessage("{{G|A settler has taken up the trade. The first stall of " + realm + " is open.}}");
		}
	}
}
