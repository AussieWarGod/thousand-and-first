using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Pure zone-id arithmetic for choosing a second-city site: which surface parasangs to
	/// offer, in what order, and which ones the world map cannot hold. Engine-free on purpose
	/// so it carries real value tests instead of a source pin; every world fact the candidates
	/// still need (does the zone build, does it answer to a foreign faction, is there a landing
	/// cell, what does KingdomFounding.JudgeSite say) is proved by the caller against the live
	/// world, never guessed here.
	/// <para>
	/// The ring starts at MinRing, not 1, because ring 1 is exactly the
	/// SecondFoundingVerdict.GroundIsTooClose band: a bordering parasang is claimed, not
	/// founded. Candidates are clamped to the Joppa world's 80x25 parasang grid for the reason
	/// Harness/KingdomScenarioGround.cs gives - the engine's biome arrays are sized to exactly
	/// that, so an off-map candidate crashes zone build rather than refusing.
	/// </para>
	/// </summary>
	internal static class KingdomSecondCitySiteRules
	{
		/// <summary>Ring 1 is the bordering band; a site must start outside it.</summary>
		internal const int MinRing = 2;
		internal const int MaxRing = 4;
		internal const int WorldMaxX = 79;
		internal const int WorldMaxY = 24;
		internal const int SurfaceDepth = 10;
		internal const int Parts = 6;

		/// <summary>
		/// Splits a surface zone id of the shape world.wx.wy.sx.sy.depth. The sub-cell columns
		/// are kept as their original text so a rebuilt id is byte-comparable with the one the
		/// engine handed us.
		/// </summary>
		internal static bool TrySplit(string ZoneId, out string World, out int Wx, out int Wy,
			out string SubX, out string SubY, out int Depth)
		{
			World = null;
			SubX = null;
			SubY = null;
			Wx = 0;
			Wy = 0;
			Depth = 0;
			if (string.IsNullOrEmpty(ZoneId)) return false;
			string[] parts = ZoneId.Split('.');
			if (parts.Length != Parts) return false;
			if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out Wx)
				|| !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out Wy)
				|| !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out Depth))
				return false;
			if (string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[3])
				|| string.IsNullOrEmpty(parts[4])) return false;
			World = parts[0];
			SubX = parts[3];
			SubY = parts[4];
			return true;
		}

		/// <summary>Rebuilds a surface zone id on the home sub-cell at another parasang.</summary>
		internal static string Compose(string World, int Wx, int Wy, string SubX, string SubY)
		{
			return World + "." + Wx.ToString(CultureInfo.InvariantCulture)
				+ "." + Wy.ToString(CultureInfo.InvariantCulture)
				+ "." + SubX + "." + SubY + "."
				+ SurfaceDepth.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// The ordered, distinct candidate ids for a home surface zone: rings MinRing..MaxRing of
		/// world parasangs, clamped to the world, nearest ring first and stable within a ring.
		/// An empty list means this home id cannot seat a second city on this world map at all,
		/// which is a refusal the caller must report, never a silent pass.
		/// </summary>
		internal static IList<string> Candidates(string HomeZoneId)
		{
			List<string> candidates = new List<string>();
			string world;
			string subX;
			string subY;
			int wx;
			int wy;
			int depth;
			if (!TrySplit(HomeZoneId, out world, out wx, out wy, out subX, out subY, out depth))
				return candidates;
			if (depth != SurfaceDepth) return candidates;
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int ring = MinRing; ring <= MaxRing; ring++)
				for (int dy = -ring; dy <= ring; dy++)
					for (int dx = -ring; dx <= ring; dx++)
					{
						if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
						int cx = wx + dx;
						int cy = wy + dy;
						if (cx < 0 || cx > WorldMaxX || cy < 0 || cy > WorldMaxY) continue;
						string id = Compose(world, cx, cy, subX, subY);
						if (id == HomeZoneId || !seen.Add(id)) continue;
						candidates.Add(id);
					}
			return candidates;
		}
	}
}
