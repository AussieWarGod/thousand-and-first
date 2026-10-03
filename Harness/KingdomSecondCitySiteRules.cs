using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Pure arithmetic for choosing a second-city site: which surface parasangs to offer, in
	/// what order, and where on one of them the rite may be poured. Engine-free on purpose so it
	/// carries real value tests instead of a source pin; every world fact the candidates still
	/// need (does the zone build, does it answer to a foreign faction, what does
	/// KingdomFounding.JudgeSite say, is the ground dry and walkable) is proved by the caller
	/// against the live world, never guessed here.
	/// <para>
	/// The ring starts at MinRing, not 1, because ring 1 is exactly the
	/// SecondFoundingVerdict.GroundIsTooClose band: a bordering parasang is claimed, not
	/// founded. Candidates off the Joppa world's 80x25 parasang grid are skipped, never moved
	/// onto it, for the reason Harness/KingdomScenarioGround.cs gives - the engine's biome
	/// arrays are sized to exactly that grid, so an off-map candidate crashes zone build rather
	/// than refusing.
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

		/// <summary>The heart rung a founding lays (Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs).</summary>
		internal const int FoundingRung = 1;

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
		/// world parasangs, nearest ring first and stable within a ring, with every position off
		/// the world map skipped (never adjusted onto it). An empty list means this home id cannot
		/// seat a second city on this world map at all, which is a refusal the caller must report,
		/// never a silent pass.
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

		/// <summary>
		/// The rung-1 heart rect production's founding draft lays around a poured rite
		/// (KingdomPlotRules.TrySurveyedHeart, then TryHeartRect at HeartSizeForRung(1), as
		/// TryDraftFoundingHeart calls them), accepted only when it is exactly the rect centred
		/// on the rite. When the survey has to slide that rect, the rite is no longer where any
		/// authored pose puts the immutable basin, and production refuses with "no authored
		/// founding-heart pose binds its basin to the poured rite"
		/// (Growth/KingdomArchitectureRuntime.FoundingHeart.cs): a rite poured near a map edge
		/// can never seat a city. City one's rite has exactly this unslid geometry.
		/// </summary>
		internal static bool TryRiteHeartRect(int RiteX, int RiteY, int Width, int Height,
			out KingdomPlotRules.PlotRect Rect)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			KingdomPlotRules.PlotRect survey;
			KingdomPlotRules.PlotRect rect;
			KingdomPlotRules.PlotRect centred;
			if (!KingdomPlotRules.TrySurveyedHeart(RiteX, RiteY, Width, Height, out survey)
				|| !KingdomPlotRules.TryHeartRect(survey, RiteX, RiteY,
					KingdomPlotRules.HeartSizeForRung(FoundingRung), out rect))
				return false;
			// Bounds wide enough that TryCentred cannot slide: its answer is the pure centring.
			if (!KingdomPlotRules.TryCentred(new KingdomPlotRules.PlotRect(RiteX - rect.Width,
					RiteY - rect.Height, RiteX + rect.Width, RiteY + rect.Height), RiteX, RiteY,
					rect.Width, rect.Height, out centred)
				|| centred.X1 != rect.X1 || centred.Y1 != rect.Y1
				|| centred.X2 != rect.X2 || centred.Y2 != rect.Y2)
				return false;
			Rect = rect;
			return true;
		}

		/// <summary>
		/// Every cell an authored public ingress route of a heart on this rect can cross: the
		/// rect, its KingdomPlotRules.RoadMargin, and the lane endpoint one cell beyond
		/// (Growth/KingdomRoadRules.Entrance.cs TryAuthoredLane). Deliberately a superset: which
		/// variant and pose production picks for the new seat is not known until it is founded,
		/// so the caller requires every cell any of them could need to be walkable now.
		/// </summary>
		internal static KingdomPlotRules.PlotRect IngressEnvelope(KingdomPlotRules.PlotRect Rect)
		{
			int reach = KingdomPlotRules.RoadMargin + 1;
			return new KingdomPlotRules.PlotRect(Rect.X1 - reach, Rect.Y1 - reach,
				Rect.X2 + reach, Rect.Y2 + reach);
		}

		/// <summary>
		/// Rite cells to try on one candidate map, packed as Y * Width + X: nearest the preferred
		/// cell first (Chebyshev rings, row-major inside a ring), keeping only cells whose rung-1
		/// heart rect is unslid (TryRiteHeartRect) and whose whole ingress envelope lies on the
		/// map. The caller passes city one's rite, so the first offer reproduces the geometry
		/// production already accepted for the first founding. Empty means the map is too small
		/// to seat a heart at all.
		/// </summary>
		internal static IList<int> RiteOrder(int PreferredX, int PreferredY, int Width, int Height)
		{
			List<int> order = new List<int>();
			if (Width <= 0 || Height <= 0) return order;
			int reach = Math.Max(Math.Max(Math.Abs(PreferredX), Math.Abs(Width - 1 - PreferredX)),
				Math.Max(Math.Abs(PreferredY), Math.Abs(Height - 1 - PreferredY)));
			for (int ring = 0; ring <= reach; ring++)
				for (int y = PreferredY - ring; y <= PreferredY + ring; y++)
					for (int x = PreferredX - ring; x <= PreferredX + ring; x++)
					{
						if (Math.Max(Math.Abs(x - PreferredX), Math.Abs(y - PreferredY)) != ring)
							continue;
						if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
						KingdomPlotRules.PlotRect rect;
						if (!TryRiteHeartRect(x, y, Width, Height, out rect)) continue;
						KingdomPlotRules.PlotRect envelope = IngressEnvelope(rect);
						if (envelope.X1 < 0 || envelope.Y1 < 0 || envelope.X2 >= Width
							|| envelope.Y2 >= Height) continue;
						order.Add(y * Width + x);
					}
			return order;
		}
	}
}
