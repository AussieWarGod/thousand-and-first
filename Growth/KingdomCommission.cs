using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static class KingdomCommission
	{
		public static bool Commission(KingdomSystem System, string Key, out string Failure)
		{
			return Commission(System, Key, null, out Failure);
		}

		/// <summary>
		/// Issues one commission on the ground the founder is standing on.
		/// </summary>
		/// <param name="System">The realm; must be founded and must hold this ground.</param>
		/// <param name="Key">Registry key of the design.</param>
		/// <param name="SkinKey">Key of the design's own skin the founder chose, or null for the
		/// design's unmodified look. A key the design does not carry is ignored.</param>
		/// <param name="Failure">A founder-facing sentence when this returns false; null otherwise.
		/// Every refusal names what would lift it.</param>
		/// <returns>True once scaffolding is standing and the water is spent.</returns>
		public static bool Commission(KingdomSystem System, string Key, string SkinKey, out string Failure)
		{
			return Commission(System, Key, SkinKey, KingdomPlotRules.PlotSize.None, out Failure);
		}

		/// <summary>
		/// Issues one commission on the ground the founder is standing on, staking the tier of plot
		/// they chose. Identical in every check to the overload above: the tier only ever widens the
		/// envelope, never the building, and a design that is not a plot ignores it entirely.
		/// </summary>
		/// <param name="Stake">The tier to lay, from <c>KingdomPlots.StakeableSizes</c>.
		/// <c>PlotSize.None</c> stakes the design's own.</param>
		public static bool Commission(KingdomSystem System, string Key, string SkinKey, KingdomPlotRules.PlotSize Stake, out string Failure)
		{
			Failure = null;
			Zone zone = The.Player?.CurrentZone;
			if (!System.Founded || zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Failure = "Commissions are issued on the kingdom's own ground.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out var entry) || !KingdomRules.StyleAllows(entry.Styles, System.Style))
			{
				Failure = "No such design.";
				return false;
			}
			// Before the room and the stores are counted, so a founder is never told what a design
			// would cost them on ground that was never going to take it.
			if (!KingdomZoning.Permits(System, zone.ZoneID, entry, out string refusal))
			{
				Failure = refusal;
				return false;
			}
			// A plot-sized design is raised over a rect in stages, not as one object in one cell.
			// Every design that declares no Plot size - which is all of them until one does - falls
			// through to the single-cell path below, untouched.
			if (KingdomPlots.IsPlotDesign(entry.Key))
			{
				return KingdomPlots.Commission(System, zone, entry, SkinKey, Stake, out Failure);
			}
			// Walls do not count against the plan. A palisade is a LINE, and charging a slot per
			// segment is what made enclosing a settlement impossible - the ring would have eaten
			// the whole allowance before anything civic was built.
			int built = 0;
			foreach (GameObject item in zone.GetObjects())
			{
				// A plot's walls, floor, and furnishings carry KingdomPlotPart and never count: the
				// cap counts plots, not the hundred objects one plot is made of.
				if (item.GetIntProperty("KingdomDefence") > 0 || item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1)
				{
					continue;
				}
				if (item.GetIntProperty("KingdomBuilt") == 1 || item.HasPart("r_KingdomScaffold") || item.HasPart("r_KingdomPlotWorks"))
				{
					built++;
				}
			}
			if (entry.Defence <= 0 && built >= KingdomRules.MaxBuildingsForStage(System.Stage))
			{
				Failure = "There is no more room in the plan. " + System.SeatName + " is as built-up as this ground allows, until it grows into something larger.";
				return false;
			}
			if (KingdomGrowth.CountStoredWater(zone) < entry.CostDrams)
			{
				Failure = "The work would cost {{C|" + entry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			// After the water and before the ground is chosen: a founder is told the whole price
			// before anything is committed, and a design with no material cost is always affordable,
			// which is every design the catalogue carried before materials existed.
			if (!KingdomMaterials.CanPay(zone, entry.Key, out var materialRefusal))
			{
				Failure = materialRefusal;
				return false;
			}
			Cell cell = FindBuildCell(zone, System, entry, out var outcome);
			if (cell == null)
			{
				Failure = "There is no clear ground for it here.";
				return false;
			}
			// Raised before it is paid for, deliberately: if the scaffold cannot be created the
			// settlement must not have already spent the water on nothing. There is no refund
			// path, because there is nothing to refund.
			GameObject gameObject = GameObject.Create("r_KingdomScaffold");
			if (gameObject == null)
			{
				Failure = "The scaffold could not be raised.";
				return false;
			}
			if (!KingdomMaterials.Pay(zone, entry.Key))
			{
				// Never trust the affordability check over the actual draw. The scaffold goes and
				// the water stays where it was.
				gameObject.Obliterate();
				Failure = "The stockpiles could not cover the work after all.";
				return false;
			}
			KingdomGrowth.ConsumeStoredWater(zone, entry.CostDrams);
			gameObject.SetStringProperty(KingdomUpgrade.BuildKeyProperty, entry.Key);
			KingdomDesign.StageSkin(gameObject, entry, SkinKey);
			r_KingdomScaffold part = gameObject.GetPart<r_KingdomScaffold>();
			if (part != null)
			{
				part.TargetBlueprint = entry.Blueprint;
				part.TargetDisplayName = entry.Name;
				part.CompleteTick = The.Game.TimeTicks + CraftBuildTicks(entry.BuildTicks, System.ZoneDistricts.Values);
				part.StaffNeeded = entry.Staff;
				part.ThresholdManning = KingdomRules.IsThresholdManning(entry.Manning);
				if (entry.Defence > 0)
				{
					// The founder standing at the commission is who the ground and the skill
					// check both belong to; a wall is built by hands that are here, not by
					// whoever last passed through.
					bool hasTinkering = The.Player != null && The.Player.HasSkill("Tinkering");
					bool hasAdvancedTinkering = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
					int defence = KingdomRules.WallDefence(entry.Defence, System.FoundingTerrainBlueprint, System.FoundingRegionName, hasTinkering, hasAdvancedTinkering);
					gameObject.SetIntProperty("KingdomDefencePending", defence);
				}
			}
			cell.AddObject(gameObject);
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(entry.Name) + " was commissioned at " + System.KingdomDisplayName);
			string clause = KingdomLayoutRules.PlacementClause(KingdomLayout.PurposeOfEntry(entry), outcome);
			MessageQueue.AddPlayerMessage("{{G|The " + entry.Name + " is commissioned. Scaffolding rises"
				+ ((clause == null) ? "" : (" " + clause)) + ".}}");
			return true;
		}

		/// <summary>
		/// Build ticks after a craft district's discount. Floors at one tick and falls back to
		/// the undiscounted time on a hostile or missing percent rather than ever completing a
		/// build instantly or in the past.
		/// </summary>
		public static long CraftBuildTicks(long BaseTicks, IEnumerable<string> Districts)
		{
			int percent = KingdomRules.DistrictsBuildPercent(Districts);
			if (percent <= 0)
			{
				percent = 100;
			}
			long ticks = BaseTicks * percent / 100;
			return (ticks < 1) ? 1 : ticks;
		}

		/// <summary>
		/// Where a commissioned work is raised, by what it is for, read against everything the
		/// settlement already has standing here.
		/// <para>
		/// The settlement's own plan (<c>KingdomLayout</c>) is asked first: casks gather by the
		/// water, houses gather by the houses and stand back from the wall, the civic ground
		/// thickens where the settlement already lives, fields lie out past the last roof, and a
		/// wall extends the wall. It grows with the city because every one of those answers is a
		/// function of what is already built, so the same commission lands somewhere different
		/// in a camp and in a town.
		/// </para>
		/// <para>
		/// The plan is allowed to have nothing to say &mdash; on empty ground it always does,
		/// because there are no neighbours to reason from &mdash; and it never fights the
		/// founder over ground it does not care about. Either way the fallback below is the
		/// placement this mod always had.
		/// </para>
		/// <para>
		/// Failure here degrades to that fallback rather than escaping into the engine: a
		/// commission the plan cannot site is still a commission.
		/// </para>
		/// </summary>
		/// <param name="Z">Zone to build in.</param>
		/// <param name="System">The realm, for its claim.</param>
		/// <param name="Entry">The design being raised.</param>
		/// <param name="Outcome">What the plan did, for the message the founder reads. Reports
		/// <c>Defer</c> whenever the fallback placed the work, whatever the reason.</param>
		public static Cell FindBuildCell(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry, out KingdomLayoutRules.LayoutOutcome Outcome)
		{
			KingdomLayoutRules.LayoutOutcome planned = KingdomLayoutRules.LayoutOutcome.Defer;
			Cell cell = null;
			// Asked before the plan, because the gatehouse is the one design whose ground is a
			// rule rather than a preference: it belongs where the wall meets the road, and the
			// plan has no way to know that. Everything else, the plan answers for.
			KingdomSystem.Guard("gatehouse siting", delegate
			{
				cell = FindGateCell(Z, System, Entry);
			});
			if (cell != null)
			{
				// Grammar, not Founder: the settlement's own shape chose this ground, and the
				// founder's standing on it had nothing to do with it. The clause the founder
				// reads for a defensive work chosen by the plan is "on the line", which is
				// exactly where a gatehouse went.
				Outcome = KingdomLayoutRules.LayoutOutcome.Grammar;
				return cell;
			}
			KingdomSystem.Guard("settlement layout", delegate
			{
				cell = KingdomLayout.ChooseCell(Z, System, Entry, out planned);
			});
			if (cell != null)
			{
				Outcome = planned;
				return cell;
			}
			Outcome = KingdomLayoutRules.LayoutOutcome.Defer;
			return FindBuildCell(Z, System, Entry != null && Entry.Defence > 0);
		}

		/// <summary>
		/// Where a commissioned work is raised when the settlement's plan has no opinion.
		/// <para>
		/// Defensive works go on the frontier: the edges of this zone that face ground the realm
		/// does not hold. That is what makes a wall a wall rather than a post in a field, and it
		/// is why walls must be sited against the WHOLE claim rather than one zone - a camp
		/// becomes a city across several zones, and the edge that needed a wall yesterday is
		/// interior once the neighbour is claimed. Nothing is moved or torn down when that
		/// happens; the old line simply becomes an inner wall.
		/// </para>
		/// <para>
		/// Everything else is raised where the founder is standing, which is the closest thing to
		/// intent the mod can read without a placement UI.
		/// </para>
		/// </summary>
		/// <param name="Z">Zone to build in.</param>
		/// <param name="System">The realm, for its claim. Null falls back to founder-adjacent.</param>
		/// <param name="Defensive">True to site this on the frontier.</param>
		public static Cell FindBuildCell(Zone Z, KingdomSystem System, bool Defensive)
		{
			if (Z == null)
			{
				return null;
			}
			if (Defensive && System != null)
			{
				KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones);
				if (edges != KingdomRules.Frontier.None)
				{
					List<Cell> line = new List<Cell>();
					foreach (Cell candidate in Z.GetEmptyCells())
					{
						if (candidate.IsPassable() && !candidate.HasObjectWithPart("LiquidVolume")
							&& KingdomRules.IsOnFrontier(candidate.X, candidate.Y, Z.Width, Z.Height, edges))
						{
							line.Add(candidate);
						}
					}
					if (line.Count > 0)
					{
						return line.GetRandomElement();
					}
				}
			}
			return FindBuildCell(Z);
		}

		/// <summary>
		/// The gatehouse's own ground: the buildable frontier cell nearest the way out
		/// (<c>KingdomRoadRules.TryGate</c>), which is the cell the settlement's own
		/// <c>HeartToGate</c> route is already walked to. The brief's whole ruling on it &mdash;
		/// "a placement rule, not a size: on the frontier wall, astride a road".
		/// <para>
		/// Null for every other design, for a zone with no frontier left to have a way out of,
		/// and for a settlement with no heart yet to aim from. Every one of those falls through
		/// to the ordinary plan, which is what sited a gatehouse before this existed.
		/// </para>
		/// </summary>
		public static Cell FindGateCell(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			if (Z == null || System == null || Entry == null || !KingdomRoadRules.SitesAtGate(Entry.Key))
			{
				return null;
			}
			KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones);
			if (edges == KingdomRules.Frontier.None)
			{
				return null;
			}
			bool hasRite = KingdomPlots.TryRiteGround(Z, out var riteX, out var riteY);
			if (!KingdomPlotRules.TryHeart(KingdomLayout.ReadMarks(Z), hasRite, riteX, riteY, out var heartX, out var heartY))
			{
				return null;
			}
			if (!KingdomRoadRules.TryGate(Z.Width, Z.Height, edges, heartX, heartY, out var gateX, out var gateY))
			{
				return null;
			}
			List<Cell> candidates = new List<Cell>();
			List<int> xs = new List<int>();
			List<int> ys = new List<int>();
			foreach (Cell candidate in Z.GetEmptyCells())
			{
				if (!candidate.IsPassable() || candidate.HasObjectWithPart("LiquidVolume")
					|| !KingdomRules.IsOnFrontier(candidate.X, candidate.Y, Z.Width, Z.Height, edges))
				{
					continue;
				}
				candidates.Add(candidate);
				xs.Add(candidate.X);
				ys.Add(candidate.Y);
			}
			int index = KingdomRoadRules.NearestToGate(xs, ys, gateX, gateY);
			if (index < 0 || index >= candidates.Count)
			{
				return null;
			}
			KingdomLog.Log("gatehouse: gate=" + gateX + "," + gateY + " sited=" + candidates[index].X + "," + candidates[index].Y
				+ " from " + candidates.Count + " frontier cells");
			return candidates[index];
		}

		public static Cell FindBuildCell(Zone Z)
		{
			Cell playerCell = The.Player?.CurrentCell;
			if (playerCell != null)
			{
				List<Cell> adjacent = playerCell.GetLocalAdjacentCells();
				for (int i = 0; i < adjacent.Count; i++)
				{
					if (adjacent[i].IsEmpty() && adjacent[i].IsPassable() && !adjacent[i].HasObjectWithPart("LiquidVolume"))
					{
						return adjacent[i];
					}
				}
			}
			List<Cell> emptyCells = Z.GetEmptyCells();
			if (emptyCells != null && emptyCells.Count > 0)
			{
				return emptyCells.GetRandomElement();
			}
			return null;
		}
	}
}
