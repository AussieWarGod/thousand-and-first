using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free geometry and journal-witness helpers for
	/// <see cref="KingdomForageNativeChecks"/>. No <c>XRL</c> type appears here so this shard can
	/// be unit-tested with fakes, independent of any GameObject/Zone/Cell instance.
	/// </summary>
	internal static class KingdomForageNativeGeometry
	{
		/// <summary>An axis-aligned inclusive rect, structurally identical to
		/// <c>KingdomPlotRules.PlotRect</c> but carried as plain ints so this shard needs no
		/// production type.</summary>
		internal readonly struct Rect
		{
			internal readonly int X1, Y1, X2, Y2;
			internal Rect(int X1, int Y1, int X2, int Y2)
			{
				this.X1 = X1; this.Y1 = Y1; this.X2 = X2; this.Y2 = Y2;
			}
		}

		/// <summary>Whether (X,Y) lies inside any of the given inclusive rects.</summary>
		internal static bool InsideAnyRect(int X, int Y, IReadOnlyList<Rect> Rects)
		{
			if (Rects == null) return false;
			for (int i = 0; i < Rects.Count; i++)
			{
				Rect rect = Rects[i];
				if (X >= rect.X1 && X <= rect.X2 && Y >= rect.Y1 && Y <= rect.Y2) return true;
			}
			return false;
		}

		/// <summary>
		/// The Chebyshev ring offsets at exactly Radius from the origin, in a fixed deterministic
		/// order (row-major), so two runs over the same ground pick the same cell first.
		/// </summary>
		internal static IEnumerable<(int Dx, int Dy)> Ring(int Radius)
		{
			if (Radius == 0) { yield return (0, 0); yield break; }
			for (int dy = -Radius; dy <= Radius; dy++)
				for (int dx = -Radius; dx <= Radius; dx++)
					if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) == Radius)
						yield return (dx, dy);
		}

		/// <summary>How many of Notes contain Marker verbatim. Used to witness a once-only
		/// ledger notice without trusting a single boolean flag.</summary>
		internal static int CountContaining(IReadOnlyList<string> Notes, string Marker)
		{
			if (Notes == null || string.IsNullOrEmpty(Marker)) return 0;
			int found = 0;
			for (int i = 0; i < Notes.Count; i++)
				if (Notes[i] != null && Notes[i].Contains(Marker)) found++;
			return found;
		}
	}
}
