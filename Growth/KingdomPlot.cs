using System;
using XRL;
using XRL.Messages;
using XRL.World;

using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the part moves; the
// settlement-side resolver below stays where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// A worked patch of ground the settlement commissioned: cycles
	/// <see cref="KingdomCropRules.PlotStage.Dormant"/> to
	/// <see cref="KingdomCropRules.PlotStage.Growing"/> to
	/// <see cref="KingdomCropRules.PlotStage.Ripe"/> on the settlement's own clock, resolved by
	/// <see cref="KingdomPlot.OnSettlementPass"/> the same way every other subsystem here
	/// resolves absence &mdash; from a stored tick stamp compared at the next
	/// <c>ZoneActivatedEvent</c>, never from a per-turn tick. Vanilla's own
	/// <c>Harvestable.RegenTimer</c> is dead code in every shipped blueprint precisely because
	/// its clock is turn-delivered and gated on zone-cache residency instead; this part is not a
	/// <c>Harvestable</c> and carries no <c>WantTurnTick</c> for the same reason.
	/// </summary>
	[Serializable]
	public class r_KingdomPlot : IPart
	{
		public KingdomCropRules.PlotStage Stage;

		/// <summary>
		/// The tick a <see cref="KingdomCropRules.PlotStage.Growing"/> crop ripens at. Stale and
		/// unread while <see cref="Stage"/> is Dormant or Ripe &mdash; it is only ever compared
		/// while Growing, and is overwritten the next time planting happens.
		/// </summary>
		public long NextStageTick;

		/// <summary>
		/// What this plot grows, resolved once from the settlement's founding style and cached
		/// so it cannot silently change crop mid-cycle. Null until the first resolution.
		/// </summary>
		public string CropBlueprint;

		/// <summary>Set once a ripe plot has already told the founder it has nowhere to put its
		/// harvest, so the wait is announced once rather than on every visit.</summary>
		public bool NoLarderAnnounced;

		/// <summary>
		/// Recolors the plot for its new stage. Presentation only: the blueprint declares one
		/// tile throughout (vanilla Watervine's own, already reachable), and only the accent
		/// colors move &mdash; exactly the scheme Watervine's <c>Harvestable</c> declaration
		/// uses for its own ripe/unripe swap, borrowed here without borrowing the part itself.
		/// </summary>
		public void ApplyStage(KingdomCropRules.PlotStage NewStage)
		{
			Stage = NewStage;
			XRL.World.Parts.Render render = ParentObject?.Render;
			if (render == null)
			{
				return;
			}
			switch (NewStage)
			{
			case KingdomCropRules.PlotStage.Growing:
				render.ColorString = "&g";
				render.TileColor = "&w";
				render.DetailColor = "K";
				break;
			case KingdomCropRules.PlotStage.Ripe:
				render.ColorString = "&G";
				render.TileColor = "&w";
				render.DetailColor = "g";
				break;
			default:
				render.ColorString = "&K";
				render.TileColor = "&w";
				render.DetailColor = "K";
				break;
			}
		}
	}

}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Resolves every commissioned plot in a zone on the settlement's own clock. Called from
	/// <see cref="KingdomGrowth.OnZoneActivated"/> after every other water-consuming step in the
	/// pass, so a plot only ever spends what the day's upkeep and arrivals left behind &mdash;
	/// it can never be the reason the thirst ladder fires.
	/// </summary>
	public static class KingdomPlot
	{
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomGrowth.Enabled || !System.Founded || Z == null || Survey == null)
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") != 1)
				{
					continue;
				}
				r_KingdomPlot plot = item.GetPart<r_KingdomPlot>();
				if (plot == null)
				{
					continue;
				}
				Resolve(System, plot, Survey, timeTicks);
			}
		}

		/// <summary>
		/// Walks one plot forward through as many stage transitions as
		/// <see cref="KingdomCropRules.MaxCyclesPerVisit"/> allows, then stops &mdash; on a
		/// closed gate (no water, no larder) or on the cap, whichever comes first. Either way the
		/// plot's own stored state is exactly what the next visit needs to pick up from.
		/// </summary>
		private static void Resolve(KingdomSystem System, r_KingdomPlot Plot, KingdomSurvey Survey, long TimeTicks)
		{
			if (string.IsNullOrEmpty(Plot.CropBlueprint))
			{
				Plot.CropBlueprint = KingdomCropRules.CropBlueprintForStyle(System.Style);
			}
			int cycles = 0;
			while (cycles < KingdomCropRules.MaxCyclesPerVisit)
			{
				switch (Plot.Stage)
				{
				case KingdomCropRules.PlotStage.Dormant:
				{
					if (!KingdomCropRules.CanAffordPlanting(Survey.StoredWater, System.Population))
					{
						return;
					}
					// Nowhere to put a harvest means nothing to plant for. Checked before the
					// water is drawn rather than after: spending the settlement's water on a crop
					// with no larder to receive it is a small penalty for not having engaged with
					// the larder yet, and this mod does not levy those.
					if (Survey.Larders.Count == 0)
					{
						return;
					}
					// Measure the delta rather than trusting the request: Consume can draw less
					// than asked if a container the aggregate StoredWater counted turns out
					// unable to give it up. Half a planting cost spent for no crop is worse than
					// waiting one more pass.
					int drawn = Survey.Consume(KingdomCropRules.PlantWaterCostDrams);
					if (drawn < KingdomCropRules.PlantWaterCostDrams)
					{
						return;
					}
					Plot.NextStageTick = KingdomCropRules.RipenTick(TimeTicks);
					Plot.ApplyStage(KingdomCropRules.PlotStage.Growing);
					System.Ledger.Note("{{c|" + drawn + " drams poured for the plot at " + System.KingdomDisplayName + "; it is growing.}}");
					cycles++;
					continue;
				}
				case KingdomCropRules.PlotStage.Growing:
					if (!KingdomCropRules.HasRipened(Plot.NextStageTick, TimeTicks))
					{
						return;
					}
					Plot.ApplyStage(KingdomCropRules.PlotStage.Ripe);
					cycles++;
					continue;
				case KingdomCropRules.PlotStage.Ripe:
					if (Survey.Larders.Count == 0)
					{
						if (!Plot.NoLarderAnnounced)
						{
							Plot.NoLarderAnnounced = true;
							System.Ledger.Note("{{r|The plot at " + System.KingdomDisplayName + " stands ripe with nowhere to put it. Dedicate a larder, and it will be gathered in.}}");
						}
						return;
					}
					Plot.NoLarderAnnounced = false;
					Deposit(Survey, Plot.CropBlueprint, KingdomCropRules.YieldPerHarvest);
					System.RecordDeed("the harvest gathered at " + System.KingdomDisplayName);
					KingdomChronicle.Record(System, "the plot at " + System.KingdomDisplayName + " was gathered in");
					System.Ledger.Note("{{G|The plot yields " + KingdomCropRules.YieldPerHarvest + " food for the larders.}}");
					MessageQueue.AddPlayerMessage("{{G|The plot at " + System.KingdomDisplayName + " has ripened and been gathered in.}}");
					Plot.ApplyStage(KingdomCropRules.PlotStage.Dormant);
					cycles++;
					continue;
				default:
					// Unreached by any stage this build writes; a stray value (a future stage
					// added by a later build, or corrupted save data) heals to Dormant rather
					// than spinning the loop with nothing to advance it.
					Plot.ApplyStage(KingdomCropRules.PlotStage.Dormant);
					return;
				}
			}
		}

		/// <summary>
		/// Spawns the harvest directly into the first dedicated larder found, keeping the
		/// survey's food counters in step so the rest of the same pass reads a pantry that
		/// already includes it. Never touches a container the founder has not dedicated: the
		/// destination comes from <see cref="KingdomSurvey.Larders"/>, the same list
		/// <c>KingdomSurvey.ConsumeFood</c> spends from.
		/// </summary>
		private static void Deposit(KingdomSurvey Survey, string CropBlueprint, int Amount)
		{
			GameObject larder = Survey.Larders[0];
			if (larder?.Inventory == null)
			{
				return;
			}
			int delivered = 0;
			for (int i = 0; i < Amount; i++)
			{
				GameObject crop = GameObject.Create(CropBlueprint);
				if (crop == null)
				{
					continue;
				}
				larder.Inventory.AddObject(crop, Silent: true);
				delivered++;
			}
			Survey.FoodStored += delivered;
			Survey.FoodAbundance = KingdomRules.ClassifyPantry(Survey.FoodStored);
		}
	}
}
