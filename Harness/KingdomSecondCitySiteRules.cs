using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Pure arithmetic for choosing a second-city site: which surface parasangs to offer, in
	/// what order, and which cells of one of them a rite could seat a heart on. Engine-free on
	/// purpose so it carries real value tests instead of a source pin; every world fact the
	/// candidates still need (does the zone build, does it answer to a foreign faction, what do
	/// KingdomFounding.JudgeSite and the founding-heart preflight say, is the ground dry) is
	/// proved by the caller against the live world, never guessed here.
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
		/// TryDraftFoundingHeart calls them), offered only when the rite stands inside it. The
		/// immutable basin is a placement inside that rect, and production binds a pose only when
		/// the basin lands exactly on the poured rite (Growth/KingdomArchitectureRuntime.FoundingHeart.cs),
		/// so a rite the survey slid the rect away from - every cell of a map's outer two rows and
		/// columns - can never seat a city. Whether a pose really binds is production's own
		/// preflight, asked by the caller; this is only the arithmetic that rules a cell out.
		/// </summary>
		internal static bool TryRiteHeartRect(int RiteX, int RiteY, int Width, int Height,
			out KingdomPlotRules.PlotRect Rect)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			KingdomPlotRules.PlotRect survey;
			KingdomPlotRules.PlotRect rect;
			if (!KingdomPlotRules.TrySurveyedHeart(RiteX, RiteY, Width, Height, out survey)
				|| !KingdomPlotRules.TryHeartRect(survey, RiteX, RiteY,
					KingdomPlotRules.HeartSizeForRung(FoundingRung), out rect)
				|| !rect.Contains(RiteX, RiteY))
				return false;
			Rect = rect;
			return true;
		}

		/// <summary>
		/// Whether a drafted rect is exactly the rect centred on the rite, with no slide. City
		/// one's rite has this geometry; reported so a run can say which kind of ground it used.
		/// </summary>
		internal static bool IsCentred(KingdomPlotRules.PlotRect Rect, int RiteX, int RiteY)
		{
			KingdomPlotRules.PlotRect centred;
			// Bounds wide enough that TryCentred cannot slide: its answer is the pure centring.
			return KingdomPlotRules.TryCentred(new KingdomPlotRules.PlotRect(RiteX - Rect.Width,
					RiteY - Rect.Height, RiteX + Rect.Width, RiteY + Rect.Height), RiteX, RiteY,
					Rect.Width, Rect.Height, out centred)
				&& centred.X1 == Rect.X1 && centred.Y1 == Rect.Y1
				&& centred.X2 == Rect.X2 && centred.Y2 == Rect.Y2;
		}

		/// <summary>
		/// Rite cells to try on one candidate map, packed as Y * Width + X: nearest the preferred
		/// cell first (Chebyshev rings, row-major inside a ring), keeping only cells
		/// TryRiteHeartRect admits. The caller passes city one's rite, so the first offer
		/// reproduces the geometry production already accepted for the first founding. Empty
		/// means the map is too small to survey a heart at all.
		/// </summary>
		internal static IList<int> RiteOrder(int PreferredX, int PreferredY, int Width, int Height)
		{
			List<int> order = new List<int>();
			if (Width <= 0 || Height <= 0) return order;
			int reach = Math.Max(Math.Max(Math.Abs(PreferredX), Math.Abs(Width - 1 - PreferredX)),
				Math.Max(Math.Abs(PreferredY), Math.Abs(Height - 1 - PreferredY)));
			for (int ring = 0; ring <= reach; ring++)
				for (int y = PreferredY - ring; y <= PreferredY + ring; y++)
				{
					// Only the ring's own cells: its whole top and bottom rows, and the two ends
					// of every row between them, still row-major.
					bool edge = y == PreferredY - ring || y == PreferredY + ring;
					int step = edge || ring == 0 ? 1 : 2 * ring;
					for (int x = PreferredX - ring; x <= PreferredX + ring; x += step)
						Offer(order, x, y, Width, Height);
				}
			return order;
		}

		private static void Offer(List<int> Order, int X, int Y, int Width, int Height)
		{
			if (X < 0 || Y < 0 || X >= Width || Y >= Height) return;
			KingdomPlotRules.PlotRect rect;
			if (TryRiteHeartRect(X, Y, Width, Height, out rect)) Order.Add(Y * Width + X);
		}
	}
}
