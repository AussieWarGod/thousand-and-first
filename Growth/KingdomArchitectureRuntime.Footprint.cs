using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		/// <summary>
		/// Transforms the exact canonical building footprint frozen by a latest architecture
		/// receipt into world coordinates. Legacy snapshots deliberately cannot enter this helper:
		/// their building/yard distinction was never serialized, so a rectangle would be invented.
		/// </summary>
		public static bool TryWorldFootprint(KingdomArchitectureIntent Intent,
			out KingdomPlotRules.PlotRect Footprint, out string Failure)
		{
			Footprint = default(KingdomPlotRules.PlotRect);
			ArchitectureLayoutSnapshot snapshot;
			if (!TryValidateIntent(Intent, out snapshot, out Failure)
				|| !KingdomArchitectureRules.IsLatestSnapshotEncoding(Intent.EncodedSnapshot))
				return Failure != null ? false : Fail(
					"legacy architecture has no exact frozen building footprint", out Failure);
			return TryWorldFootprint(snapshot, Intent.Rect, out Footprint, out Failure);
		}

		/// <summary>Pure pose transform for an already-validated snapshot footprint.</summary>
		public static bool TryWorldFootprint(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, out KingdomPlotRules.PlotRect Footprint,
			out string Failure)
		{
			Footprint = default(KingdomPlotRules.PlotRect);
			Failure = null;
			if (Snapshot == null || !ValidRect(Rect)
				|| Snapshot.FootprintWidth < 1 || Snapshot.FootprintHeight < 1
				|| Snapshot.FootprintX < 0 || Snapshot.FootprintY < 0
				|| Snapshot.FootprintX + Snapshot.FootprintWidth > Snapshot.Width
				|| Snapshot.FootprintY + Snapshot.FootprintHeight > Snapshot.Height)
				return Fail("the frozen canonical building footprint is malformed", out Failure);

			int canonicalX2 = Snapshot.FootprintX + Snapshot.FootprintWidth - 1;
			int canonicalY2 = Snapshot.FootprintY + Snapshot.FootprintHeight - 1;
			int[] canonicalX = new int[4]
			{
				Snapshot.FootprintX, canonicalX2, Snapshot.FootprintX, canonicalX2
			};
			int[] canonicalY = new int[4]
			{
				Snapshot.FootprintY, Snapshot.FootprintY, canonicalY2, canonicalY2
			};
			int x1 = int.MaxValue;
			int y1 = int.MaxValue;
			int x2 = int.MinValue;
			int y2 = int.MinValue;
			for (int i = 0; i < canonicalX.Length; i++)
			{
				int x;
				int y;
				if (!KingdomArchitectureRules.TryToWorld(Rect.X1, Rect.Y1,
					Snapshot.Width, Snapshot.Height, Snapshot.Facing,
					canonicalX[i], canonicalY[i], out x, out y) || !Rect.Contains(x, y))
					return Fail("the frozen building footprint does not fit its exact pose",
						out Failure);
				if (x < x1) x1 = x;
				if (x > x2) x2 = x;
				if (y < y1) y1 = y;
				if (y > y2) y2 = y;
			}
			Footprint = new KingdomPlotRules.PlotRect(x1, y1, x2, y2);
			return true;
		}

		/// <summary>Reads the tier roof frozen with a latest intent and applies ground strata.</summary>
		public static bool TryRoofOnGround(KingdomArchitectureIntent Intent, bool Underground,
			out KingdomPlotRules.RoofState Roof, out string Failure)
		{
			Roof = KingdomPlotRules.RoofState.Open;
			ArchitectureLayoutSnapshot snapshot;
			if (!TryValidateIntent(Intent, out snapshot, out Failure)
				|| !KingdomArchitectureRules.IsLatestSnapshotEncoding(Intent.EncodedSnapshot))
				return Failure != null ? false : Fail(
					"legacy architecture has no exact frozen roof", out Failure);
			if (snapshot.BaseRoof < KingdomPlotRules.RoofState.Open
				|| snapshot.BaseRoof > KingdomPlotRules.RoofState.Carved)
				return Fail("the frozen building roof is malformed", out Failure);
			Roof = KingdomPlotRules.RoofOnGround(snapshot.BaseRoof, Underground);
			return true;
		}
	}
}
