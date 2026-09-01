using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestbook
	{

		/// <summary>
		/// Offers a notable guest the settlement's own housing. Call from
		/// <see cref="XRL.World.Parts.r_KingdomNotableGuest"/>'s inventory action; a no-op if the
		/// guest has already resolved (lodged or departed) or is no longer present.
		/// </summary>
		/// <param name="Guest">The guest object the player targeted.</param>
		public static void TryLodge(GameObject Guest)
		{
			if (Guest == null || Guest.GetIntProperty(NotableGuestProperty) != 1)
			{
				return;
			}
			Zone zone = Guest.CurrentZone;
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused; this guest cannot be lodged yet.");
				return;
			}
			if (zone == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			KingdomGuestRules.HookKind kind = (KingdomGuestRules.HookKind)Guest.GetIntProperty(HookKindProperty);
			string hookText = Guest.GetStringProperty(HookTextProperty) ?? "";
			string shownHook = KingdomPresentation.Rich(hookText);
			bool legendaryTrader = Guest.HasTag(LegendaryTraderTag);
			GameObject fineHouse = null;
			KingdomPlotRules.PlotSize fineHouseTier = KingdomPlotRules.PlotSize.None;
			// A guest is judged by effective physical beds against population, on one live reading,
			// and deliberately NOT by the settlers' own assignment-level gate: brief Addendum 4b
			// binds housing for people who JOIN the settlement, and says guests are unchanged
			// because they never stay without lodging anyway. A visitor is not assigned a home,
			// spends nobody's grace, and never leaves for want of one.
			KingdomSurvey survey = KingdomSurvey.Take(zone);
			KingdomBenefitIndex benefits = null;
			string benefitFailure = null;
			if (survey == null || !survey.TryBenefits(out benefits, out benefitFailure))
			{
				Popup.Show("Lodging evidence is unavailable: "
					+ (benefitFailure ?? "no exact physical-benefit reading") + ".");
				return;
			}
			KingdomGuestRules.LodgingVerdict verdict;
			if (legendaryTrader)
			{
				bool hasFineHouse;
				fineHouse = FindVacantFineHouse(zone, benefits, out hasFineHouse,
					out fineHouseTier);
				int liveShopTier = system.HasShopkeeper ? system.ShopTier : 0;
				verdict = KingdomGuestRules.AssessLegendaryTraderLodging(hasFineHouse,
					fineHouseTier, fineHouse != null, liveShopTier);
			}
			else
			{
				KingdomPlotRules.PlotSize bestTier = BestHousingTier(zone, benefits);
				bool hasRoom = KingdomRules.HasRoomToHouse(system.Population,
					benefits.Total("roof"));
				bool hasTier = bestTier != KingdomPlotRules.PlotSize.None
					&& bestTier >= KingdomGuestRules.RequiredTier(kind);
				verdict = KingdomGuestRules.AssessLodging(hasTier, hasRoom);
			}
			if (verdict != KingdomGuestRules.LodgingVerdict.Lodged)
			{
				Popup.Show(legendaryTrader
					? KingdomGuestRules.LegendaryTraderRefusal(verdict)
					: (verdict == KingdomGuestRules.LodgingVerdict.NoTier
						? KingdomGuestRules.NoTierRefusal(kind)
						: KingdomGuestRules.NoRoomRefusal()));
				return;
			}
			int arrivalCost = KingdomRules.DramsPerArrival;
			if (survey.StoredWater < arrivalCost)
			{
				Popup.Show("Lodging " + KingdomPresentation.Rich(PlainGuestName(Guest))
					+ " requires exactly {{C|"
					+ arrivalCost + " drams}} from the dedicated stores, and they cannot provide it.");
				return;
			}
			string name = PlainGuestName(Guest);
			string shownName = KingdomPresentation.Rich(name);
			bool milestone = !system.FirstNotableGuestLodged;
			string chronicle = KingdomGuestRules.LodgedChronicleLine(shownName,
				KingdomPresentation.Rich(system.SeatName), kind, legendaryTrader);
			string ledger = shownName + " joined the settlement from "
				+ KingdomPresentation.Rich(Guest.GetStringProperty(OriginProperty)
					?? "the road") + ".";
			string message = KingdomGuestRules.LodgedMessage(shownName, kind, legendaryTrader);
			string line = KingdomGuestRules.GuestbookLine(shownName, kind, shownHook,
				Lodged: true, LegendaryTrader: legendaryTrader);
			if (!KingdomGuestLifecycle.PublishLodge(system, Guest, fineHouse,
				The.Game.TimeTicks, KingdomGuestRules.NextDueTick(The.Game.TimeTicks),
				arrivalCost, chronicle, ledger, message, line, milestone))
				Popup.Show("Lodging could not complete. Its exact lifecycle receipt remains open; no second lodging can begin.");
		}

		/// <summary>
		/// The best housing tier standing in the zone, read from an exact physical-provider root
		/// joined to one rectangular designation. Missing, sparse, or irregular geometry is not a
		/// tier: guest requirements fail closed instead of borrowing catalogue dimensions.
		/// </summary>
		private static KingdomPlotRules.PlotSize BestHousingTier(Zone Z,
			KingdomBenefitIndex Benefits)
		{
			KingdomPlotRules.PlotSize best = KingdomPlotRules.PlotSize.None;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (!TryPhysicalHousingTier(item, Benefits, out _,
					out KingdomPlotRules.PlotSize tier)) continue;
				if (tier > best)
				{
					best = tier;
				}
			}
			return best;
		}

		/// <summary>One plain semantic guest name; output callers escape it separately.</summary>
		private static string PlainGuestName(GameObject guest)
		{
			if (!GameObject.Validate(guest)) return "a guest";
			string named = guest.GetStringProperty("KingdomName");
			if (string.IsNullOrEmpty(named)) named = guest.BaseDisplayNameStripped;
			return string.IsNullOrEmpty(named) ? "a guest" : named;
		}

		/// <summary>Finds one exact, sound, wholly vacant fine house. A manor, terrace, or large
		/// generic roof never aliases the named luxury good. The lowest stable LotId wins when
		/// several qualify; the returned tier is that vacant home's actual staked size, or the best
		/// exact fine-house tier seen when every one is occupied so the refusal remains exact.</summary>
		private static GameObject FindVacantFineHouse(Zone Z, KingdomBenefitIndex Benefits,
			out bool HasFineHouse,
			out KingdomPlotRules.PlotSize Tier)
		{
			HasFineHouse = false;
			Tier = KingdomPlotRules.PlotSize.None;
			GameObject chosen = null;
			string chosenPlot = null;
			KingdomPlotRules.PlotSize chosenTier = KingdomPlotRules.PlotSize.None;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (!TryPhysicalHousingTier(item, Benefits,
					out KingdomBenefitReading reading,
					out KingdomPlotRules.PlotSize actual)
					|| !string.Equals(reading.Designation.BuildingKey, "finehouse",
						StringComparison.Ordinal))
					continue;
				string plotId = reading.Designation.LotId;
				HasFineHouse = true;
				if (actual > Tier) Tier = actual;
				if (actual < KingdomGuestRules.LegendaryTraderFineHouseTier
					|| KingdomLodging.ResidentsOf(Z, item).Count != 0)
					continue;
				if (chosen == null || string.CompareOrdinal(plotId, chosenPlot) < 0)
				{
					chosen = item;
					chosenPlot = plotId;
					chosenTier = actual;
				}
			}
			if (chosen != null) Tier = chosenTier;
			return chosen;
		}

		internal static bool TryPhysicalHousingTier(GameObject Root,
			KingdomBenefitIndex Benefits, out KingdomBenefitReading Reading,
			out KingdomPlotRules.PlotSize Tier)
		{
			Reading = null;
			Tier = KingdomPlotRules.PlotSize.None;
			if (!KingdomUpgrade.IsFunctionallyBuilt(Root)
				|| !KingdomLodging.TryHomeReading(Root, Benefits, out Reading, out _)
				|| KingdomLodging.RoofCapacity(Root, Benefits) <= 0
				|| KingdomLodging.IsCondemned(Root)) return false;
			if (!TryExactPlotBounds(Reading, out int width, out int height)) return false;
			Tier = KingdomGuestRules.ClassifyRectTier(width, height);
			return Tier != KingdomPlotRules.PlotSize.None;
		}

		internal static bool TryExactPlotBounds(KingdomBenefitReading Reading,
			out int Width, out int Height)
		{
			return KingdomGuestRules.TryExactPlotBounds(
				Reading?.Designation?.Cells, out Width, out Height);
		}

	}
}
