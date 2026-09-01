using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using ThousandAndFirst.Harness;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Dev-only ground scout for scenario staging. The gallery deliberately refuses to clear live
	/// terrain, so a populated arrival zone can never stage a case; this verb walks nearby surface
	/// zones, asks the gallery's own canvas preflight whether the stamped case's exact posed rectangle
	/// fits, and teleports the operator to a proved landing outside that rectangle and its complete
	/// review clearance. It mutates nothing but movement: no terrain edits and no object clears.
	/// </summary>
	public static class KingdomScenarioGround
	{
		private const int SearchRadius = 3;

		public static string Scout()
		{
			bool ok;
			return Scout(out ok);
		}

		/// <summary>
		/// The same verb, with its outcome as a boolean rather than a string the caller has to
		/// read. The journal and the auto-runner need OK vs REFUSED as a fact, and recovering it by
		/// matching prose would break the first time a message is reworded.
		/// </summary>
		public static string Scout(out bool Ok)
		{
			Ok = false;
			GameObject player = The.Player;
			Zone here = player?.CurrentZone;
			if (player == null || here == null)
				return "No player zone; the ground scout runs only inside a live game.";
			int probeWidth;
			int probeHeight;
			string exactFailure;
			if (!TryExactDimensions(out probeWidth, out probeHeight, out exactFailure))
				return "Ground scout refused: " + KingdomScenarioRules.Bounded(exactFailure);
			string zoneId = here.ZoneID;
			string[] parts = zoneId.Split('.');
			int wx;
			int wy;
			int z;
			if (parts.Length != 6
				|| !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out wx)
				|| !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out wy)
				|| !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out z))
				return "This zone id does not parse as a surface world zone: " + zoneId;
			if (z != 10) return "The ground scout only walks surface zones (z=10); here z=" + z + ".";
			List<string> tried = new List<string>();
			for (int ring = 0; ring <= SearchRadius; ring++)
				for (int dy = -ring; dy <= ring; dy++)
					for (int dx = -ring; dx <= ring; dx++)
					{
						if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
						int cx = wx + dx;
						int cy = wy + dy;
						// The Joppa world map is 80x25 parasangs; the engine's biome arrays are
						// sized to exactly that, so a candidate off the map crashes zone build
						// rather than refusing. Clamp the search to the world.
						if (cx < 0 || cx > 79 || cy < 0 || cy > 24) continue;
						string candidateId = parts[0] + "." + cx + "." + cy
							+ "." + parts[3] + "." + parts[4] + ".10";
						Zone zone;
						try { zone = The.ZoneManager.GetZone(candidateId); }
						catch (Exception) { tried.Add(candidateId + " (unbuildable)"); continue; }
						if (zone == null) { tried.Add(candidateId + " (null)"); continue; }
						KingdomPlotRules.PlotRect rect;
						string failure;
						if (!KingdomArchitectureGalleryWishes.TryFindCanvas(zone, probeWidth,
							probeHeight, out rect, out failure))
						{
							tried.Add(candidateId + " (no canvas)");
							continue;
						}
						if (ring == 0)
						{
							Ok = true;
							return "This zone already fits a staged case; no move was needed.";
						}
						HashSet<int> connections =
							KingdomArchitectureGalleryWishes.ConnectionCells(zone);
						Cell target = FindParkingCell(zone, rect, connections);
						if (target == null
							|| !KingdomArchitectureGalleryWishes.SafeCanvas(zone, rect, connections,
								target))
						{
							tried.Add(candidateId + " (no safe exterior landing)");
							continue;
						}
						player.CurrentCell.RemoveObject(player);
						target.AddObject(player);
						The.ZoneManager.SetActiveZone(zone);
						The.ZoneManager.ProcessGoToPartyLeader();
						if (!ReferenceEquals(player.CurrentZone, zone)
							|| !ReferenceEquals(player.CurrentCell, target))
							return "Ground scout moved the tester but could not prove its exact landing.";
						Ok = true;
						return "Moved to " + candidateId + "; the exact " + probeWidth + "x"
							+ probeHeight + " posed case and an exterior landing fit here. "
							+ "Run {{W|kingdom:scenario realize}}.";
					}
			return "No stageable ground within " + SearchRadius + " parasangs. Tried: "
				+ string.Join(", ", tried.ToArray());
		}

		/// <summary>The exact world dimensions frozen by this stamped gallery scenario.</summary>
		internal static bool TryExactDimensions(out int Width, out int Height, out string Failure)
		{
			Width = 0;
			Height = 0;
			KingdomScenarioPlan plan;
			KingdomScenarioProvenance stamp;
			KingdomScenarioGallerySlice.Case expected;
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out Failure)
				|| !KingdomScenarioRun.TryExpectedGalleryCase(plan, out expected, out Failure)
				|| !KingdomArchitecture.TryResolveVariant(expected.BuildKey, expected.TypeKey,
					expected.LotSize, expected.VariantKey, expected.Facing, out snapshot, out Failure))
				return false;
			if (!KingdomArchitectureRules.TryWorldDimensions(snapshot.Width, snapshot.Height,
				snapshot.Facing, out Width, out Height))
			{
				Failure = "the exact requested pose has impossible world dimensions";
				return false;
			}
			return true;
		}

		/// <summary>Nearest deterministic walkable cell outside an exact canvas's full clearance.</summary>
		internal static Cell FindParkingCell(Zone Zone, KingdomPlotRules.PlotRect Canvas,
			HashSet<int> Connections)
		{
			if (Zone == null || Connections == null) return null;
			int reach = KingdomArchitectureGalleryWishes.ReviewClearance;
			KingdomPlotRules.PlotRect clearance = new KingdomPlotRules.PlotRect(
				Canvas.X1 - reach, Canvas.Y1 - reach, Canvas.X2 + reach, Canvas.Y2 + reach);
			Cell best = null;
			int bestDistance = int.MaxValue;
			for (int y = 0; y < Zone.Height; y++)
				for (int x = 0; x < Zone.Width; x++)
				{
					if (clearance.Contains(x, y)) continue;
					Cell cell = Zone.GetCell(x, y);
					if (!SafeParking(cell, Zone, Connections)) continue;
					int distance = Math.Abs(x - Canvas.CenterX) + Math.Abs(y - Canvas.CenterY);
					if (distance >= bestDistance) continue;
					best = cell;
					bestDistance = distance;
				}
			return best;
		}

		private static bool SafeParking(Cell Cell, Zone Zone, HashSet<int> Connections)
		{
			if (Cell == null || Connections.Contains(Cell.Y * Zone.Width + Cell.X)
				|| Cell.HasStairs() || Cell.HasObjectWithPart("StairsUp")
				|| Cell.HasObjectWithPart("StairsDown") || !KingdomRoads.Walkable(Cell)
				|| !Cell.IsEmptyOfSolid()) return false;
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
				if (GameObject.Validate(objects[i]) && objects[i].IsCreature) return false;
			return true;
		}
	}
}
