using System.Collections.Generic;
using XRL;
using ThousandAndFirst.Harness;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Dev-only ground preparation for scenario staging. The gallery's own transaction never
	/// clears live ground, and real wilderness always carries brush, so the "empty test zone" the
	/// staging preflight demands does not occur naturally. This verb applies the SAME clearance
	/// law production founding applies when a paid plot is staked - vegetation-tier ground is
	/// cleared, anything the settlement may not take refuses the whole flatten by name - over one
	/// probe rectangle at the operator's feet, BEFORE the single mutating realize call. It runs
	/// only inside a stamped dev-scenario game, so the resulting evidence is already marked
	/// scenario-authority and remains ineligible for green until an ordinary-play anchor exists.
	/// </summary>
	public static class KingdomScenarioFlatten
	{
		private const int FlattenWidth = 12;
		private const int FlattenHeight = 10;

		public static string Flatten()
		{
			bool ok;
			return Flatten(out ok);
		}

		/// <summary>
		/// The same verb, with its outcome as a boolean rather than a string the caller has to
		/// read. The journal and the auto-runner need OK vs REFUSED as a fact, and recovering it by
		/// matching prose would break the first time a message is reworded.
		/// </summary>
		public static string Flatten(out bool Ok)
		{
			Ok = false;
			GameObject player = The.Player;
			Zone zone = player?.CurrentZone;
			Cell foot = player?.CurrentCell;
			if (player == null || zone == null || foot == null)
				return "No player zone; flatten runs only inside a live game.";
			string eligibility;
			if (KingdomScenarioDurableState.OrdinaryAnchorEligible(out eligibility))
				return "Flatten refused: this is an ordinary game. Ground preparation runs only "
					+ "inside a stamped dev-scenario game, where all evidence is already marked "
					+ "scenario-authority.";
			// Search the whole zone for the nearest rectangle that carries nothing the
			// clearance law refuses - no liquid, nothing held, no stairs - rather than centering
			// on the operator, who cannot see a one-cell puddle under marsh grass. The player's
			// own cell is excluded so the staged canvas never has to ask them to move.
			int bestX = -1;
			int bestY = -1;
			int bestDistance = int.MaxValue;
			// The staging canvas also refuses travel-connection cells, open liquid, impassable
			// ground, and cells holding creatures (which are never cleared - they walk off).
			// Mirror every one of those laws here so a flattened rectangle is one the canvas
			// will then accept.
			HashSet<int> connections =
				KingdomArchitectureGalleryWishes.ConnectionCells(zone);
			for (int y1 = 1; y1 + FlattenHeight < zone.Height; y1++)
				for (int x1 = 1; x1 + FlattenWidth < zone.Width; x1++)
				{
					bool lawful = true;
					for (int y = y1 - 1; lawful && y <= y1 + FlattenHeight; y++)
						for (int x = x1 - 1; lawful && x <= x1 + FlattenWidth; x++)
						{
							Cell cell = zone.GetCell(x, y);
							if (cell == null || cell == foot || cell.HasStairs()
								|| connections.Contains(y * zone.Width + x)
								|| cell.HasOpenLiquidVolume() || !cell.IsPassable())
							{
								lawful = false;
								break;
							}
							List<GameObject> standing = cell.GetObjects();
							for (int i = 0; lawful && i < standing.Count; i++)
								if (standing[i] != null && standing[i].IsCreature
									&& !standing[i].IsPlayer())
									lawful = false;
							if (!lawful) break;
							string blocker;
							KingdomPlotRules.GroundKind kind =
								KingdomPlots.ReadGround(cell, out blocker);
							if (KingdomPlotRules.Refuses(kind)) lawful = false;
						}
					if (!lawful) continue;
					int centerX = x1 + FlattenWidth / 2;
					int centerY = y1 + FlattenHeight / 2;
					int distance = (centerX > foot.X ? centerX - foot.X : foot.X - centerX)
						+ (centerY > foot.Y ? centerY - foot.Y : foot.Y - centerY);
					if (distance >= bestDistance) continue;
					bestX = x1;
					bestY = y1;
					bestDistance = distance;
				}
			if (bestX < 0)
				return "Flatten refused: no rectangle in this zone is free of liquid, held "
					+ "objects, and stairs. Move to another zone and try again.";
			int removed = 0;
			for (int y = bestY; y < bestY + FlattenHeight; y++)
				for (int x = bestX; x < bestX + FlattenWidth; x++)
				{
					Cell cell = zone.GetCell(x, y);
					if (cell == null) continue;
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (item == null || item.IsCreature || item.IsPlayer()) continue;
						KingdomPlotRules.GroundKind kind = KingdomPlots.ReadObject(item);
						// Bare includes engine widgets, which must be LEFT IN PLACE: they are the
						// zone's own bookkeeping, not ground, and obliterating one would break
						// the system that owns it.
						if (kind == KingdomPlotRules.GroundKind.Bare) continue;
						bool gone = false;
						try { gone = item.Obliterate(null, Silent: true); }
						catch { }
						if (gone || !GameObject.Validate(item))
						{
							KingdomSurvey.ObserveRemovedFromActive(zone, item);
							removed++;
						}
					}
				}
			// Post-verify with the STAGING canvas's own predicate: clearing is only done when
			// the canvas the realize probe runs would actually accept this ground. Anything that
			// survived - a creature that wandered in, an object Obliterate quietly kept - is
			// named here so the journal says exactly what stood in the way.
			KingdomPlotRules.PlotRect verified;
			string canvasFailure;
			if (!KingdomArchitectureGalleryWishes.TryFindCanvas(zone, FlattenWidth - 2,
					FlattenHeight - 2, out verified, out canvasFailure))
			{
				// Per-predicate census over the flattened region plus one margin ring: name
				// every cell each canvas predicate refuses, so the journal says exactly which
				// law and which cell stand in the way rather than restating the prose.
				List<string> faults = new List<string>();
				for (int y = bestY - 1; y <= bestY + FlattenHeight; y++)
					for (int x = bestX - 1; x <= bestX + FlattenWidth; x++)
					{
						Cell cell = zone.GetCell(x, y);
						if (cell == null) { faults.Add(x + "," + y + ":edge"); continue; }
						if (!cell.IsPassable()) faults.Add(x + "," + y + ":impassable");
						if (cell.HasOpenLiquidVolume()) faults.Add(x + "," + y + ":liquid");
						if (connections.Contains(y * zone.Width + x))
							faults.Add(x + "," + y + ":connection");
						if (cell.HasStairs()) faults.Add(x + "," + y + ":stairs");
						string blocker;
						KingdomPlotRules.GroundKind kind =
							KingdomPlots.ReadGround(cell, out blocker);
						if (kind != KingdomPlotRules.GroundKind.Bare)
							faults.Add(x + "," + y + ":" + kind
								+ (blocker == null ? "" : "(" + blocker + ")"));
						List<GameObject> objects = cell.GetObjects();
						for (int i = 0; i < objects.Count; i++)
							if (objects[i] != null && objects[i].IsCreature
								&& !objects[i].IsPlayer())
								faults.Add(x + "," + y + ":creature("
									+ objects[i].ShortDisplayNameStripped + ")");
						if (faults.Count > 24) break;
					}
				// Ask the REAL canvas predicate about the one candidate whose margin is exactly
				// the flattened rectangle. If it disagrees with the census above, the divergence
				// is in a clause the census does not model, and this line says so.
				KingdomPlotRules.PlotRect exact = new KingdomPlotRules.PlotRect(
					bestX + 1, bestY + 1, bestX + FlattenWidth - 2, bestY + FlattenHeight - 2);
				bool exactSafe = KingdomArchitectureGalleryWishes.SafeCanvas(
					zone, exact, connections, foot);
				faults.Add("exact-candidate(" + (bestX + 1) + "," + (bestY + 1) + ")="
					+ (exactSafe ? "SAFE despite scan refusal" : "refused by SafeCanvas"));
				if (!exactSafe)
				{
					// Clause-by-clause replay of the real predicate over the exact candidate,
					// including the clauses the census does not model and a FRESH connection
					// set (the flatten-time snapshot could differ if anything repopulated the
					// zone connection cache between scan and verify).
					HashSet<int> fresh =
						KingdomArchitectureGalleryWishes.ConnectionCells(zone);
					KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
					for (int y = exact.Y1 - 1; y <= exact.Y2 + 1; y++)
						for (int x = exact.X1 - 1; x <= exact.X2 + 1; x++)
						{
							Cell cell = zone.GetCell(x, y);
							string at = x + "," + y + ":";
							if (cell == null) { faults.Add(at + "null"); continue; }
							if (foot != null && cell == foot) faults.Add(at + "player-cell");
							if (fresh.Contains(y * zone.Width + x)
								&& !connections.Contains(y * zone.Width + x))
								faults.Add(at + "connection-NEW-since-scan");
							if (cell.HasObjectWithPart("StairsUp")
								|| cell.HasObjectWithPart("StairsDown"))
								faults.Add(at + "stairs-part");
							if (exact.Contains(x, y) && system != null
								&& KingdomConstruction.HasActiveAt(system, zone, cell))
								faults.Add(at + "active-construction");
							if (faults.Count > 40) break;
						}
				}
				return "Flatten cleared " + removed + " objects at " + bestX + "," + bestY
					+ " but the staging canvas still refuses: " + canvasFailure
					+ (faults.Count == 0 ? " (no cell in the region fails any predicate;"
						+ " the refusal is elsewhere in the zone scan)"
						: " Failing cells: " + string.Join("; ", faults.ToArray()))
					+ " Run flatten again after resolving.";
			}
			Ok = true;
			return "Flattened a " + FlattenWidth + "x" + FlattenHeight + " rectangle at "
				+ bestX + "," + bestY + " (" + removed + " objects cleared by the production "
				+ "clearance law). Run {{W|kingdom:scenario realize}}.";
		}
	}
}
