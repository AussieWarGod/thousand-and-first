using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One possible world envelope for a rectangular authored lot.</summary>
	public struct KingdomPlotPoseCandidate
	{
		public readonly KingdomPlotRules.PlotRect Rect;
		public readonly bool Transposed;

		public KingdomPlotPoseCandidate(KingdomPlotRules.PlotRect Rect, bool Transposed)
		{
			this.Rect = Rect;
			this.Transposed = Transposed;
		}
	}

	/// <summary>
	/// Engine-free enumeration of every exact rectangular lot pose inside surveyed ground.
	/// Canonical precedes transposed at the same low corner; square lots are emitted once.
	/// The authored runtime remains responsible for choosing North/South or East/West from
	/// frontage evidence.
	/// </summary>
	public static class KingdomPlotPoseSitingRules
	{
		public static List<KingdomPlotPoseCandidate> Enumerate(
			KingdomPlotRules.PlotRect Interior, int CanonicalWidth, int CanonicalHeight)
		{
			List<KingdomPlotPoseCandidate> candidates =
				new List<KingdomPlotPoseCandidate>();
			if (Interior.Width < 1 || Interior.Height < 1
				|| CanonicalWidth < 1 || CanonicalHeight < 1) return candidates;

			bool distinctTranspose = CanonicalWidth != CanonicalHeight;
			for (int y = Interior.Y1; y <= Interior.Y2; y++)
			{
				for (int x = Interior.X1; x <= Interior.X2; x++)
				{
					TryAdd(candidates, Interior, x, y,
						CanonicalWidth, CanonicalHeight, false);
					if (distinctTranspose)
						TryAdd(candidates, Interior, x, y,
							CanonicalHeight, CanonicalWidth, true);
				}
			}
			return candidates;
		}

		/// <summary>
		/// Enumerates exact target envelopes which contain an already-standing lot. This is
		/// deliberately a filtered form of <see cref="Enumerate"/>: growth may use either authored
		/// pose, but it may never translate the predecessor outside the successor reservation.
		/// Candidate order therefore remains the ordinary siting order and is stable across retries.
		/// </summary>
		public static List<KingdomPlotPoseCandidate> EnumerateContaining(
			KingdomPlotRules.PlotRect OldRect, KingdomPlotRules.PlotRect Interior,
			int TargetCanonicalWidth, int TargetCanonicalHeight)
		{
			List<KingdomPlotPoseCandidate> containing =
				new List<KingdomPlotPoseCandidate>();
			if (OldRect.Width < 1 || OldRect.Height < 1) return containing;

			List<KingdomPlotPoseCandidate> candidates = Enumerate(Interior,
				TargetCanonicalWidth, TargetCanonicalHeight);
			for (int i = 0; i < candidates.Count; i++)
				if (Contains(candidates[i].Rect, OldRect)) containing.Add(candidates[i]);
			return containing;
		}

		/// <summary>Whether a successor reservation wholly contains and adds cells to its source.</summary>
		public static bool IsStrictContainingEnvelope(KingdomPlotRules.PlotRect OldRect,
			KingdomPlotRules.PlotRect TargetRect)
		{
			return OldRect.Width > 0 && OldRect.Height > 0
				&& TargetRect.Width > 0 && TargetRect.Height > 0
				&& Contains(TargetRect, OldRect) && TargetRect.Area > OldRect.Area;
		}

		private static bool Contains(KingdomPlotRules.PlotRect Outer,
			KingdomPlotRules.PlotRect Inner)
		{
			return Inner.X1 >= Outer.X1 && Inner.X2 <= Outer.X2
				&& Inner.Y1 >= Outer.Y1 && Inner.Y2 <= Outer.Y2;
		}

		private static void TryAdd(List<KingdomPlotPoseCandidate> Candidates,
			KingdomPlotRules.PlotRect Interior, int X, int Y, int Width, int Height,
			bool Transposed)
		{
			long x2 = (long)X + Width - 1L;
			long y2 = (long)Y + Height - 1L;
			if (x2 > Interior.X2 || y2 > Interior.Y2) return;
			Candidates.Add(new KingdomPlotPoseCandidate(
				new KingdomPlotRules.PlotRect(X, Y, (int)x2, (int)y2), Transposed));
		}
	}
}
