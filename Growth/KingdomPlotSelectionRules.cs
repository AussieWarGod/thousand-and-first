using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// The architecture-acceptance selection loop, lifted engine-free from
	/// Growth/KingdomPlot2.08.Siting.cs's TryFindRect so it is value-testable with a fake
	/// candidate/occupancy source instead of a real Zone. Pure: it only decides, it never writes
	/// -- <c>Resolve</c> is the caller's own read-only probe (architecture acceptance, and now
	/// occupancy against the resolved snapshot's managed cells), and this loop's only job is to
	/// collect every accepted candidate and, on total rejection, name the nearest-to-founder
	/// rejection's own failure text -- exactly the production behaviour this replaces.
	/// </summary>
	public static class KingdomPlotSelectionRules
	{
		/// <summary>One candidate's resolution: accepted, or rejected with a named reason.</summary>
		public readonly struct Resolution
		{
			public readonly bool Accepted;
			public readonly string Failure;
			public Resolution(bool Accepted, string Failure)
			{ this.Accepted = Accepted; this.Failure = Failure; }
		}

		/// <summary>
		/// Resolves every candidate once, in order. Returns true with every accepted rect in
		/// <paramref name="Accepted"/> when at least one exists; otherwise returns false with
		/// <paramref name="Refusal"/> naming the rejection nearest the founder (or the generic
		/// "no authored architecture" line when no founder cell narrows it) -- never a mutation,
		/// never a second call to <paramref name="Resolve"/> per candidate.
		/// </summary>
		public static bool TrySelect(IList<KingdomPlotRules.PlotRect> Candidates,
			Func<KingdomPlotRules.PlotRect, Resolution> Resolve,
			bool HasFounder, int FounderX, int FounderY,
			out List<KingdomPlotRules.PlotRect> Accepted, out string Refusal)
		{
			Accepted = new List<KingdomPlotRules.PlotRect>();
			Refusal = null;
			List<KingdomPlotRules.PlotRect> rejected = new List<KingdomPlotRules.PlotRect>();
			List<string> rejectedFailures = new List<string>();
			for (int i = 0; i < Candidates.Count; i++)
			{
				Resolution resolution = Resolve(Candidates[i]);
				if (resolution.Accepted) Accepted.Add(Candidates[i]);
				else
				{
					rejected.Add(Candidates[i]);
					rejectedFailures.Add(resolution.Failure);
				}
			}
			if (Accepted.Count > 0) return true;
			int nearest = NearestIndex(rejected, HasFounder, FounderX, FounderY);
			string chosen = nearest >= 0 ? rejectedFailures[nearest] : null;
			Refusal = string.IsNullOrEmpty(chosen)
				? "No authored architecture fits any clear pose of that exact plot."
				: chosen;
			return false;
		}

		/// <summary>The candidate nearest the founder, or the lowest-positioned one when the
		/// founder is elsewhere. Deterministic either way -- the same rule Siting.cs's own
		/// NearestIndex applies, kept as a separate copy here (not a call into that method) so
		/// this shard has zero dependency on the file it was lifted out of.</summary>
		internal static int NearestIndex(IList<KingdomPlotRules.PlotRect> Candidates,
			bool HasFounder, int FounderX, int FounderY)
		{
			int best = -1;
			int bestReach = 0;
			for (int i = 0; i < Candidates.Count; i++)
			{
				int reach = HasFounder ? KingdomPlotRules.Reach(Candidates[i], FounderX, FounderY) : 0;
				if (best < 0 || KingdomPlotRules.Beats(0, reach, Candidates[i],
					0, bestReach, Candidates[best]))
				{
					best = i;
					bestReach = reach;
				}
			}
			return best;
		}
	}
}
