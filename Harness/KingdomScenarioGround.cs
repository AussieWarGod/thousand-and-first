using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Dev-only ground scout for scenario staging. The gallery deliberately refuses to clear live
	/// terrain, so a populated arrival zone can never stage a case; this verb walks nearby surface
	/// zones, asks the gallery's own canvas preflight whether a case-plus-margin rectangle fits,
	/// and teleports the operator to the first zone that answers yes. It reuses the exact preflight
	/// the staging path runs, so a zone it approves is a zone staging will accept, and it mutates
	/// nothing: movement only, no terrain edits, no object clears.
	/// </summary>
	public static class KingdomScenarioGround
	{
		/// <summary>
		/// Probe footprint: the phase-1 staged case is a small tent, and real wilderness never
		/// offers a barren XL rectangle (grass, dunes, and pools all lawfully refuse), so the
		/// probe covers the small case plus generous margin rather than the widest authored map.
		/// </summary>
		private const int ProbeWidth = 8;
		private const int ProbeHeight = 6;
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
						if (!KingdomArchitectureGalleryWishes.TryFindCanvas(zone, ProbeWidth,
							ProbeHeight, out rect, out failure))
						{
							tried.Add(candidateId + " (no canvas)");
							continue;
						}
						if (ring == 0)
						{
							Ok = true;
							return "This zone already fits a staged case; no move was needed.";
						}
						Cell target = zone.GetCell(rect.CenterX, rect.CenterY);
						if (target == null) { tried.Add(candidateId + " (no cell)"); continue; }
						player.CurrentCell.RemoveObject(player);
						target.AddObject(player);
						The.ZoneManager.SetActiveZone(zone);
						The.ZoneManager.ProcessGoToPartyLeader();
						Ok = true;
						return "Moved to " + candidateId + "; a case-plus-margin canvas fits here. "
							+ "Run {{W|kingdom:scenario realize}}.";
					}
			return "No stageable ground within " + SearchRadius + " parasangs. Tried: "
				+ string.Join(", ", tried.ToArray());
		}
	}
}
