using System;
using System.Collections.Generic;

using XRL;
using ThousandAndFirst.Harness;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Dev-only ground preparation for one stamped architecture scenario. It resolves the exact
	/// authored pose, plans that canvas together with a safe tester landing outside the complete
	/// review/ingress clearance, re-proves the plan after movement, and clears only ground the
	/// production plot law classifies as takeable. Gallery realization remains the sole production
	/// transaction; this helper is available only in a sealed synthetic profile.
	/// </summary>
	public static class KingdomScenarioFlatten
	{
		public static string Flatten()
		{
			bool ok;
			return Flatten(out ok);
		}

		public static string Flatten(out bool Ok)
		{
			Ok = false;
			GameObject player = The.Player;
			Zone zone = player?.CurrentZone;
			Cell foot = player?.CurrentCell;
			if (!GameObject.Validate(player) || zone == null || foot == null)
				return "No player zone; flatten runs only inside a live game.";
			string eligibility;
			if (KingdomScenarioDurableState.OrdinaryAnchorEligible(out eligibility))
				return "Flatten refused: this is ordinary play. Ground preparation is synthetic "
					+ "scenario authority and cannot run here.";

			int width;
			int height;
			string failure;
			if (!KingdomScenarioGround.TryExactDimensions(out width, out height, out failure))
				return "Flatten refused: " + KingdomScenarioRules.Bounded(failure);
			int reach = KingdomArchitectureGalleryWishes.ReviewClearance;
			KingdomPlotRules.PlotRect origins;
			if (!KingdomPlotRules.TryInsetOriginBounds(zone.Width, zone.Height, width, height,
				reach, out origins))
				return "Flatten refused: the exact " + width + "x" + height
					+ " pose and its complete clearance cannot fit this zone.";

			HashSet<int> connections =
				KingdomArchitectureGalleryWishes.ConnectionCells(zone);
			KingdomPlotRules.PlotRect chosen = default(KingdomPlotRules.PlotRect);
			Cell parking = null;
			int bestDistance = int.MaxValue;
			string lastFailure = null;
			for (int y = origins.Y1; y <= origins.Y2; y++)
				for (int x = origins.X1; x <= origins.X2; x++)
				{
					KingdomPlotRules.PlotRect candidate = new KingdomPlotRules.PlotRect(
						x, y, x + width - 1, y + height - 1);
					if (!TryProveClearable(zone, candidate, connections, player, out failure))
					{
						lastFailure = failure;
						continue;
					}
					Cell candidateParking = KingdomScenarioGround.FindParkingCell(
						zone, candidate, connections);
					if (candidateParking == null)
					{
						lastFailure = "no safe tester landing stands outside the candidate clearance";
						continue;
					}
					int distance = Math.Abs(candidate.CenterX - foot.X)
						+ Math.Abs(candidate.CenterY - foot.Y);
					if (distance >= bestDistance) continue;
					chosen = candidate;
					parking = candidateParking;
					bestDistance = distance;
				}
			if (parking == null)
				return "Flatten refused before movement: no exact canvas-and-landing pair is "
					+ "clearable in this zone" + (lastFailure == null ? "." : " ("
					+ KingdomScenarioRules.Bounded(lastFailure) + ").");

			if (!ReferenceEquals(player.CurrentCell, parking))
			{
				try
				{
					if (!player.SystemLongDistanceMoveTo(parking, 0, forced: true,
						ignoreCombat: true) || !ReferenceEquals(player.CurrentZone, zone)
						|| !ReferenceEquals(player.CurrentCell, parking))
						return "Flatten refused: the engine did not park the tester at the proved "
							+ "exterior cell; ground remains untouched.";
				}
				catch (Exception exception)
				{
					return "Flatten refused before clearing: tester movement failed ("
						+ KingdomScenarioRules.Bounded(exception.Message) + ").";
				}
			}

			// Movement can fire engine events. Re-read connections and every cell before the first
			// destructive call; a changed candidate refuses with its ground still untouched.
			connections = KingdomArchitectureGalleryWishes.ConnectionCells(zone);
			if (!TryProveClearable(zone, chosen, connections, player, out failure))
				return "Flatten refused after parking but before clearing: "
					+ KingdomScenarioRules.Bounded(failure) + ".";
			KingdomPlotRules.PlotRect clearance = new KingdomPlotRules.PlotRect(
				chosen.X1 - reach, chosen.Y1 - reach, chosen.X2 + reach, chosen.Y2 + reach);
			int removed;
			if (!TryClear(zone, clearance, player, out removed, out failure))
				return "Flatten stopped while clearing synthetic test ground: "
					+ KingdomScenarioRules.Bounded(failure) + ". Prepare a fresh profile.";

			connections = KingdomArchitectureGalleryWishes.ConnectionCells(zone);
			if (!KingdomArchitectureGalleryWishes.SafeCanvas(zone, chosen, connections,
					player.CurrentCell))
				return "Flatten cleared " + removed + " objects, but its exact selected canvas "
					+ "failed the production predicate. Prepare a fresh profile.";
			KingdomPlotRules.PlotRect productionChoice;
			if (!KingdomArchitectureGalleryWishes.TryFindCanvas(zone, width, height,
					out productionChoice, out failure))
				return "Flatten proved its exact canvas but the production scan refused: "
					+ KingdomScenarioRules.Bounded(failure) + ".";
			Ok = true;
			return "Flattened exact " + width + "x" + height + " pose at " + chosen.X1 + ","
				+ chosen.Y1 + " with " + reach + "-cell review clearance; removed " + removed
				+ " clearable objects and parked the tester at " + parking.X + "," + parking.Y
				+ ". Run {{W|kingdom:scenario realize}}.";
		}

		/// <summary>Projected post-clear proof. Only the exact current player may move away.</summary>
		private static bool TryProveClearable(Zone Zone, KingdomPlotRules.PlotRect Canvas,
			HashSet<int> Connections, GameObject Player, out string Failure)
		{
			Failure = null;
			int reach = KingdomArchitectureGalleryWishes.ReviewClearance;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			for (int y = Canvas.Y1 - reach; y <= Canvas.Y2 + reach; y++)
				for (int x = Canvas.X1 - reach; x <= Canvas.X2 + reach; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					string at = x + "," + y;
					if (cell == null || Connections.Contains(y * Zone.Width + x)
						|| cell.HasStairs() || cell.HasObjectWithPart("StairsUp")
						|| cell.HasObjectWithPart("StairsDown"))
						return Fail("protected edge, connection, or stair at " + at, out Failure);
					if (Canvas.Contains(x, y) && system != null
						&& KingdomConstruction.HasActiveAt(system, Zone, cell))
						return Fail("active construction at " + at, out Failure);
					string blocker;
					KingdomPlotRules.GroundKind ground = KingdomPlots.ReadGround(cell, out blocker);
					if (KingdomPlotRules.Refuses(ground))
						return Fail((blocker ?? ground.ToString()) + " may not be taken at " + at,
							out Failure);
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (!GameObject.Validate(item) || ReferenceEquals(item, Player)) continue;
						if (item.IsCreature || item.IsPlayer())
							return Fail("another creature stands at " + at, out Failure);
						KingdomPlotRules.GroundKind kind = KingdomPlots.ReadObject(item);
						if (KingdomPlotRules.Refuses(kind))
							return Fail(item.ShortDisplayNameStripped + " may not be taken at " + at,
								out Failure);
						if (kind == KingdomPlotRules.GroundKind.Bare && item.Physics != null
							&& item.Physics.Solid)
							return Fail("a surviving solid object blocks " + at, out Failure);
					}
					if (ground == KingdomPlotRules.GroundKind.Bare
						&& !KingdomRoads.Walkable(cell))
						return Fail("unclearable impassable ground at " + at, out Failure);
				}
			return true;
		}

		/// <summary>Collects every target before mutation, then clears only that proved set.</summary>
		private static bool TryClear(Zone Zone, KingdomPlotRules.PlotRect Clearance,
			GameObject Player, out int Removed, out string Failure)
		{
			Removed = 0;
			Failure = null;
			List<GameObject> targets = new List<GameObject>();
			for (int y = Clearance.Y1; y <= Clearance.Y2; y++)
				for (int x = Clearance.X1; x <= Clearance.X2; x++)
				{
					List<GameObject> objects = Zone.GetCell(x, y).GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (!GameObject.Validate(item) || ReferenceEquals(item, Player)) continue;
						if (item.IsCreature || item.IsPlayer())
							return Fail("a creature entered the proved clearance", out Failure);
						KingdomPlotRules.GroundKind kind = KingdomPlots.ReadObject(item);
						if (kind == KingdomPlotRules.GroundKind.Bare) continue;
						if (KingdomPlotRules.Refuses(kind))
							return Fail(item.ShortDisplayNameStripped + " became protected", out Failure);
						targets.Add(item);
					}
				}
			for (int i = 0; i < targets.Count; i++)
			{
				GameObject item = targets[i];
				bool gone;
				try { gone = item.Obliterate(null, Silent: true); }
				catch (Exception exception)
				{
					return Fail("clear threw: " + exception.Message, out Failure);
				}
				if (!gone && GameObject.Validate(item))
					return Fail(item.ShortDisplayNameStripped + " refused synthetic clearing",
						out Failure);
				KingdomSurvey.ObserveRemovedFromActive(Zone, item);
				Removed++;
			}
			return true;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
