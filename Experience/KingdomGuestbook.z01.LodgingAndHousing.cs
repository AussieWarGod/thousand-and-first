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
			// A guest is judged by the raw bed count against population, on the same live survey,
			// and deliberately NOT by the settlers' own assignment-level gate: brief Addendum 4b
			// binds housing for people who JOIN the settlement, and says guests are unchanged
			// because they never stay without lodging anyway. A visitor is not assigned a home,
			// spends nobody's grace, and never leaves for want of one.
			KingdomSurvey survey = KingdomSurvey.Take(zone);
			KingdomGuestRules.LodgingVerdict verdict;
			if (legendaryTrader)
			{
				bool hasFineHouse;
				fineHouse = FindVacantFineHouse(zone, out hasFineHouse, out fineHouseTier);
				int liveShopTier = system.HasShopkeeper ? system.ShopTier : 0;
				verdict = KingdomGuestRules.AssessLegendaryTraderLodging(hasFineHouse,
					fineHouseTier, fineHouse != null, liveShopTier);
			}
			else
			{
				KingdomPlotRules.PlotSize bestTier = BestHousingTier(zone);
				bool hasRoom = KingdomRules.HasRoomToHouse(system.Population, survey.Beds);
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
		/// The best housing tier standing in the zone, read off the plot rects stamped on its
		/// beds (<c>KingdomPlots.TryReadRect</c>). A bed with no readable rect is legacy
		/// single-cell furniture and reads as <see cref="KingdomPlotRules.PlotSize.Small"/>, never
		/// <c>None</c> — S plots never obsolete, and neither does the furniture that predates
		/// plots entirely.
		/// </summary>
		private static KingdomPlotRules.PlotSize BestHousingTier(Zone Z)
		{
			KingdomPlotRules.PlotSize best = KingdomPlotRules.PlotSize.None;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty("KingdomBuilt") != 1 || !item.HasPart("Bed"))
				{
					continue;
				}
				KingdomPlotRules.PlotSize tier = KingdomPlotRules.PlotSize.Small;
				if (KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect rect))
				{
					tier = KingdomGuestRules.ClassifyRectTier(rect.Width, rect.Height);
				}
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
		private static GameObject FindVacantFineHouse(Zone Z, out bool HasFineHouse,
			out KingdomPlotRules.PlotSize Tier)
		{
			HasFineHouse = false;
			Tier = KingdomPlotRules.PlotSize.None;
			GameObject chosen = null;
			string chosenPlot = null;
			KingdomPlotRules.PlotSize chosenTier = KingdomPlotRules.PlotSize.None;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
					|| !string.Equals(KingdomUpgrade.DesignKeyOf(item), "finehouse",
						StringComparison.Ordinal)
					|| KingdomLodging.IsCondemned(item))
					continue;
				string plotId = item.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (string.IsNullOrEmpty(plotId)) continue;
				HasFineHouse = true;
				KingdomPlotRules.PlotSize actual = KingdomPlotRules.PlotSize.Medium;
				if (KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect rect))
					actual = KingdomGuestRules.ClassifyRectTier(rect.Width, rect.Height);
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

	}
}
