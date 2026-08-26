using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One active raising as the crew allocator sees it. Engine-free and immutable:
	/// the runtime freezes these facts before choosing who walks anywhere.</summary>
	public readonly struct KingdomRaisingCandidate
	{
		public readonly string ObjectId;
		public readonly long StartedTick;
		public readonly int X;
		public readonly int Y;

		public KingdomRaisingCandidate(string ObjectId, long StartedTick, int X, int Y)
		{
			this.ObjectId = ObjectId;
			this.StartedTick = StartedTick < 0L ? 0L : StartedTick;
			this.X = X;
			this.Y = Y;
		}
	}

	/// <summary>The bounded result of allocating the settlement's one raising gang.</summary>
	public readonly struct KingdomRaisingPlan
	{
		/// <summary>Index into the candidate list, or -1 when there is no raising.</summary>
		public readonly int SelectedIndex;

		/// <summary>Real unposted bodies assigned, never more than the gang wants.</summary>
		public readonly int AssignedHands;

		public KingdomRaisingPlan(int SelectedIndex, int AssignedHands)
		{
			this.SelectedIndex = SelectedIndex;
			this.AssignedHands = AssignedHands;
		}
	}

	/// <summary>
	/// Pure policy for visible construction presence. A settlement owns one raising gang, and
	/// that gang takes the oldest active raising. Ties are ground order (north, then west), then
	/// stable object identity. Input enumeration order never decides who gets worked.
	/// </summary>
	public static class KingdomConstructionPresenceRules
	{
		public const int Schema = 1;

		public static KingdomRaisingPlan Plan(IList<KingdomRaisingCandidate> Candidates,
			int AvailableBodies, int WantedHands)
		{
			int selected = Oldest(Candidates);
			if (selected < 0)
			{
				return new KingdomRaisingPlan(-1, 0);
			}
			int available = AvailableBodies > 0 ? AvailableBodies : 0;
			int wanted = WantedHands > 0 ? WantedHands : 0;
			return new KingdomRaisingPlan(selected, available < wanted ? available : wanted);
		}

		public static int Oldest(IList<KingdomRaisingCandidate> Candidates)
		{
			if (Candidates == null || Candidates.Count == 0)
			{
				return -1;
			}
			int best = -1;
			for (int i = 0; i < Candidates.Count; i++)
			{
				KingdomRaisingCandidate candidate = Candidates[i];
				if (string.IsNullOrEmpty(candidate.ObjectId))
				{
					continue;
				}
				if (best < 0 || Compare(candidate, Candidates[best]) < 0)
				{
					best = i;
				}
			}
			return best;
		}

		private static int Compare(KingdomRaisingCandidate A, KingdomRaisingCandidate B)
		{
			int result = A.StartedTick.CompareTo(B.StartedTick);
			if (result != 0) return result;
			result = A.Y.CompareTo(B.Y);
			if (result != 0) return result;
			result = A.X.CompareTo(B.X);
			if (result != 0) return result;
			return string.Compare(A.ObjectId, B.ObjectId, StringComparison.Ordinal);
		}

		public static string QueueLine(string WaitingName, string WorkingName)
		{
			string waiting = string.IsNullOrEmpty(WaitingName) ? "The half-raised work" :
				("The " + WaitingName);
			string working = string.IsNullOrEmpty(WorkingName) ? "the older raising" :
				("the " + WorkingName);
			return waiting + " waits. The settlement's raising gang is committed first to "
				+ working + "; the same hands cannot stand at two frames.";
		}
	}
}
