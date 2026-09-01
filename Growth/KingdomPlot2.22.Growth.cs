using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static bool HeartGrowRefused(GameObject Work, Zone Z, string SuccessorKey, string SuccessorName, out string Refusal)
		{
			Refusal = null;
			int rung = KingdomPlotRules.HeartRungOf(SuccessorKey);
			if (!TryHeartRectFor(Z, rung, out var grown))
			{
				Refusal = KingdomPlotRules.RefuseHeartRoom(SuccessorName);
				return true;
			}
			string id = Work.GetStringProperty(PlotIdProperty);
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			for (int i = 0; i < survey.PlotRoots.Count; i++)
			{
				GameObject item = survey.PlotRoots[i];
				if (item == null || item == Work || !TryReadRect(item, out var laid))
				{
					continue;
				}
				// The heart's own earlier rungs are the ground it is growing out of, never an
				// obstruction in it.
				if (!string.IsNullOrEmpty(id) && item.GetStringProperty(PlotIdProperty) == id)
				{
					continue;
				}
				if (KingdomPlotRules.Overlaps(grown, KingdomPlotRules.Reserved(laid)))
				{
					string what = KingdomDesign.ReferenceFor(item, item.ShortDisplayNameStripped);
					Refusal = IsYielding(item)
						? KingdomPlotRules.RefuseHeartYielding(SuccessorName, what)
						: KingdomPlotRules.RefuseHeartGround(SuccessorName, what);
					return true;
				}
			}
			// Walked by hand rather than through GroundGrid, because the grid reads the heart's
			// own standing rung -- its building, its walls, its floor -- as ground that refuses a
			// plot, which is correct for every other plot and exactly wrong for the one growing
			// out of it.
			for (int y = grown.Y1; y <= grown.Y2; y++)
			{
				for (int x = grown.X1; x <= grown.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						Refusal = KingdomPlotRules.RefuseHeartRoom(SuccessorName);
						return true;
					}
					foreach (GameObject item in cell.GetObjects())
					{
						if (item == null || item == Work || item.IsCreature || item.IsPlayer())
						{
							continue;
						}
						if (!string.IsNullOrEmpty(id) && item.GetStringProperty(PlotIdProperty) == id)
						{
							continue;
						}
						if (!KingdomPlotRules.Refuses(ReadObject(item)))
						{
							continue;
						}
						Refusal = KingdomPlotRules.RefuseHeartGround(SuccessorName, KingdomDesign.ReferenceFor(item, item.ShortDisplayNameStripped));
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Carries a plot across an improvement and stamps the new tier's footprint inside it. The
		/// plot itself never moves: the ground the founder staked is the ground the building keeps,
		/// and the yard is whatever the grown footprint leaves.
		/// <para>
		/// Called with the predecessor still standing, so everything the ground was recorded as is
		/// still readable. A single-cell design carries nothing and this does nothing.
		/// </para>
		/// </summary>
		public static bool GrowInPlace(GameObject Predecessor, GameObject Successor, string SuccessorKey)
		{
			if (!GameObject.Validate(Predecessor) || !GameObject.Validate(Successor))
			{
				return false;
			}
			KingdomArchitectureIntent authored;
			if (KingdomArchitectureRuntime.TryRead(Predecessor, out authored, out _)
				&& KingdomArchitectureRules.IsManagedSnapshotEncoding(authored.EncodedSnapshot))
			{
				// Current authored plots may only change through the exact frozen layout delta.
				return false;
			}
			if (!TryReadRect(Predecessor, out var plot)) return true;
			string id = Predecessor.GetStringProperty(PlotIdProperty);
			Zone zone = Predecessor.CurrentZone ?? Successor.CurrentZone;
			Cell predecessorCell = Predecessor.CurrentCell;
			if (zone == null || predecessorCell == null || Successor.CurrentZone != zone
				|| Successor.CurrentCell != predecessorCell || string.IsNullOrEmpty(id)
				|| string.IsNullOrEmpty(SuccessorKey)) return false;
			// The heart's plot GROWS with its rung: the next rung takes the next ring of the
			// ground surveyed at the founding rite, and the rung below it stays underfoot -- the
			// kerb becomes the hall's floor, and the basin stands in the middle of all of it.
			// Every other plot keeps exactly the envelope the founder staked.
			bool heart = IsHeartPlot(Predecessor) && KingdomPlotRules.HeartRungOf(SuccessorKey) > 0;
			if (heart)
			{
				Successor.SetIntProperty(HeartPlotProperty, 1);
				if (TryHeartRectFor(zone, KingdomPlotRules.HeartRungOf(SuccessorKey), out var climbed))
				{
					plot = climbed;
				}
			}
			KingdomPlotRules.PlotRect old = TryReadFootprint(Predecessor, out var read) ? read : plot;
			KingdomPlotRules.RoofState roof = RoofOf(Predecessor);
			if (zone == null || !TryGetSpec(SuccessorKey, out var spec))
			{
				// Nothing known about what it became: carry forward only what was actually
				// recorded. A building raised before footprints existed has no roof stamped on it
				// and gets none invented for it -- it filled its plot, and it still does.
				if (Predecessor.HasIntProperty(PlotRoofProperty))
				{
					StampFootprint(Successor, old, roof);
				}
				StampRect(Successor, plot);
				if (!string.IsNullOrEmpty(id)) Successor.SetStringProperty(PlotIdProperty, id);
				return ExactGrowthEndpoints(Predecessor, Successor, predecessorCell, null);
			}
			HeartFor(zone, plot, out var heartX, out var heartY);
			KingdomPlotRules.RoofState grownRoof = KingdomPlotRules.RoofOnGround(spec.Roof, KingdomPlotRules.IsUnderground(zone.Z));
			KingdomPlotRules.PlotRect grown = heart
				? HeartFootprintFor(zone, plot, spec)
				: FootprintFor(plot, spec, heartX, heartY);
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			// The settlement's wall material is derived and not stored, exactly as it is when a
			// plot is first raised, so a grown building is walled in the same stone as its
			// neighbours whether or not anybody wrote that stone down.
			string wall = (system == null) ? null : KingdomPlotRules.WallBlueprintFor(system.Style, system.FoundingRegionName);
			// The heart alone keeps the rung below it standing. Every other improvement takes down
			// the walls that end up inside the bigger building, because one building has one
			// enclosure; the heart is not one building growing but four layers accreting, and the
			// moot hall is meant to stand inside the great court, beams, door and all.
			GrowthPlan plan;
			string frozen = Predecessor.GetStringProperty(GrowthReceiptProperty);
			if (string.IsNullOrEmpty(frozen))
			{
				if (!TryBuildGrowthPlan(zone, Predecessor, Successor, SuccessorKey, id,
					old, grown, grownRoof, heartX, heartY, heart, wall, out plan)) return false;
				frozen = EncodeGrowthPlan(plan);
				if (frozen == null) return false;
				Predecessor.SetStringProperty(GrowthReceiptProperty, frozen);
				if (Predecessor.GetStringProperty(GrowthReceiptProperty) != frozen) return false;
			}
			else if (!TryDecodeGrowthPlan(frozen, out plan)
				|| !GrowthPlanMatches(plan, Predecessor, Successor, SuccessorKey, id,
					old, grown, grownRoof, heartX, heartY, heart, wall)) return false;

			if (!string.IsNullOrEmpty(id)) Successor.SetStringProperty(PlotIdProperty, id);
			if (Predecessor.GetIntProperty(YieldingProperty) == 1)
			{
				Successor.SetIntProperty(YieldingProperty, 1);
				try { Successor.RequirePart<r_KingdomYielding>(); }
				catch { return false; }
				if (!ExactGrowthEndpoints(Predecessor, Successor, predecessorCell, plan)) return false;
			}
			if (heart) Successor.SetIntProperty(HeartPlotProperty, 1);
			StampRect(Successor, plot);
			StampFootprint(Successor, grown, grownRoof);
			if (!ExactGrowthEndpoints(Predecessor, Successor, predecessorCell, plan)) return false;
			if (!ApplyGrowthPlan(zone, Predecessor, Successor, plan)) return false;
			if (!ValidateGrowthWorld(zone, Predecessor, Successor, plan, false)) return false;
			if (!plan.Done)
			{
				plan.Done = true;
				if (!PublishGrowthPlan(Predecessor, plan)) return false;
			}
			return ExactGrowthEndpoints(Predecessor, Successor, predecessorCell, plan)
				&& ValidateGrowthWorld(zone, Predecessor, Successor, plan, false);
		}

	}
}
