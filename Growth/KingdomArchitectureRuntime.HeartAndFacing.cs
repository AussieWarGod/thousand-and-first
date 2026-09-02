using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		private static bool TryHeartBasinInvariant(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, Zone Z, out string Failure)
		{
			Failure = null;
			int riteX;
			int riteY;
			if (Snapshot == null || Z == null || !KingdomPlots.TryRiteGround(Z, out riteX, out riteY))
				return Fail("founding-heart architecture has no recorded rite ground", out Failure);
			return TryHeartBasinAt(Snapshot, Rect, riteX, riteY, out Failure);
		}

		internal static bool TryFoundingHeartBasinInvariant(KingdomArchitectureIntent Intent,
			int RiteX, int RiteY, out string Failure)
		{
			Failure = null;
			if (!TryDecode(Intent, out ArchitectureLayoutSnapshot snapshot, out Failure)) return false;
			return TryHeartBasinAt(snapshot, Intent.Rect, RiteX, RiteY, out Failure);
		}

		private static bool TryHeartBasinAt(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, int RiteX, int RiteY, out string Failure)
		{
			if (!TryHeartBasinCoordinate(Snapshot, Rect, out int basinX, out int basinY,
				out Failure)) return false;
			if (basinX != RiteX || basinY != RiteY)
				return Fail("founding-heart immutable basin moves away from the recorded rite",
					out Failure);
			return true;
		}

		private static bool TryHeartBasinCoordinate(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, out int BasinX, out int BasinY,
			out string Failure)
		{
			BasinX = 0;
			BasinY = 0;
			Failure = null;
			if (Snapshot == null) return Fail("founding-heart architecture is absent", out Failure);
			ArchitecturePlacement basin = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (!placement.ExistingAuthority) continue;
				// The compiler binds a stateful anchor to its cell (role@x,y); the basin is
				// named by its role, whatever cell the pose put it on.
				if (basin != null || placement.Blueprint != "r_KingdomFirstBasin"
					|| KingdomArchitectureRules.AnchorRole(placement.StatefulAnchor)
						!= "fixture:first-basin")
					return Fail("founding-heart architecture must bind exactly one immutable first basin",
						out Failure);
				basin = placement;
			}
			if (basin == null || !TryWorldPlacement(Snapshot, Rect, basin,
				out BasinX, out BasinY, out Failure))
				return Failure != null ? false : Fail(
					"founding-heart architecture has no immutable first basin", out Failure);
			return true;
		}

		private static bool SameRect(KingdomPlotRules.PlotRect A,
			KingdomPlotRules.PlotRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}

		private static bool TryHeartFacing(KingdomArchitectureMapping Mapping, Zone Z,
			KingdomPlotRules.PlotRect Rect, out ArchitectureFacing Facing, out string Failure)
		{
			Facing = ArchitectureFacing.North;
			Failure = null;
			int canonicalWidth;
			int canonicalHeight;
			if (!KingdomArchitectureRules.TryCanonicalDimensions(Mapping.LotSize,
				out canonicalWidth, out canonicalHeight))
				return Fail("the frozen mapping has an unknown lot size", out Failure);
			bool northSouth = Rect.Width == canonicalWidth && Rect.Height == canonicalHeight;
			bool eastWest = Rect.Width == canonicalHeight && Rect.Height == canonicalWidth;
			if (!northSouth && !eastWest)
				return Fail("the staked rectangle does not exactly fit the frozen lot size in any pose",
					out Failure);
			if (Mapping.Frontage != ArchitectureFrontage.Heart)
				return Fail("building " + Mapping.BuildKey + " has an unknown frontage", out Failure);
			int heartX;
			int heartY;
			KingdomPlots.HeartFor(Z, Rect, out heartX, out heartY);
			if (northSouth && !eastWest)
				Facing = heartY <= Rect.CenterY ? ArchitectureFacing.North : ArchitectureFacing.South;
			else if (eastWest && !northSouth)
				Facing = heartX >= Rect.CenterX ? ArchitectureFacing.East : ArchitectureFacing.West;
			else
			{
				// No shipped lot is square, but the tie law is fixed for additive sizes.
				int dx = heartX - Rect.CenterX;
				int dy = heartY - Rect.CenterY;
				if (Math.Abs(dx) > Math.Abs(dy))
					Facing = dx >= 0 ? ArchitectureFacing.East : ArchitectureFacing.West;
				else
					Facing = dy <= 0 ? ArchitectureFacing.North : ArchitectureFacing.South;
			}
			int posedWidth;
			int posedHeight;
			if (!KingdomArchitectureRules.TryDimensions(Mapping.LotSize, Facing,
				out posedWidth, out posedHeight)
				|| posedWidth != Rect.Width || posedHeight != Rect.Height)
				return Fail("the selected cardinal pose does not exactly fit the staked rectangle",
					out Failure);
			return true;
		}

		private static bool TryRoadFacing(string BuildKey, KingdomArchitectureMapping Mapping,
			Zone Z, KingdomPlotRules.PlotRect Rect, ArchitectureSelectionContext Context,
			out ArchitectureFacing Facing, out ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Facing = ArchitectureFacing.North;
			Snapshot = null;
			Failure = null;
			if (Mapping.Frontage != ArchitectureFrontage.Road)
				return Fail("road-facing resolution needs a Road frontage mapping", out Failure);
			ArchitectureFacing[] candidates = new ArchitectureFacing[]
			{
				ArchitectureFacing.North, ArchitectureFacing.East,
				ArchitectureFacing.South, ArchitectureFacing.West
			};
			int bestScore = -1;
			ArchitectureLayoutSnapshot best = null;
			for (int i = 0; i < candidates.Length; i++)
			{
				ArchitectureFacing candidate = candidates[i];
				int width;
				int height;
				if (!KingdomArchitectureRules.TryDimensions(Mapping.LotSize, candidate,
					out width, out height) || width != Rect.Width || height != Rect.Height) continue;
				ArchitectureLayoutSnapshot resolved;
				if (!KingdomArchitecture.TryResolve(BuildKey, Mapping.TypeKey,
					Mapping.LotSize, Context, candidate,
					out resolved, out Failure)) return false;
				if (!MatchesMapping(resolved, Mapping))
					return Fail("road-facing candidate disagrees with its frozen mapping", out Failure);
				int score;
				if (!TryPhysicalRoadIngressScore(Z, Rect, resolved, out score, out Failure))
					return false;
				// Candidate order is the fixed N/E/S/W tie law; equal scores keep the earlier pose.
				if (score > bestScore)
				{
					bestScore = score;
					Facing = candidate;
					best = resolved;
				}
			}
			if (best == null || bestScore <= 0)
				return Fail("building " + Mapping.BuildKey
					+ " has no authored public entrance connected to existing road evidence", out Failure);
			Snapshot = best;
			return true;
		}

		private static bool TryRectLotSize(KingdomPlotRules.PlotRect Rect,
			out ArchitectureLotSize Size)
		{
			Size = ArchitectureLotSize.Small;
			for (int i = (int)ArchitectureLotSize.Small; i <= (int)ArchitectureLotSize.Huge; i++)
			{
				ArchitectureLotSize candidate = (ArchitectureLotSize)i;
				int width;
				int height;
				if (!KingdomArchitectureRules.TryCanonicalDimensions(candidate, out width, out height))
					continue;
				if ((Rect.Width == width && Rect.Height == height)
					|| (Rect.Width == height && Rect.Height == width))
				{
					Size = candidate;
					return true;
				}
			}
			return false;
		}

	}
}
