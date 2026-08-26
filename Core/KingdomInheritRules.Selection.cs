using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritRules
	{
		private static bool[] Select(KingdomInheritPlan Source, int Percent, int InterregnumRoll, bool PreferHeart)
		{
			bool[] selected = new bool[Source.Count];
			int eligible = 0;
			for (int i = 0; i < Source.Count; i++)
			{
				KingdomInheritWork work = Source.WorkAt(i);
				if (work != null && work.State != KingdomInheritWorkState.Memory)
				{
					eligible++;
				}
			}
			if (eligible == 0)
			{
				return selected;
			}
			int percent = Percent;
			if (percent < 0) percent = 0;
			if (percent > 100) percent = 100;
			int wanted = (eligible * percent + 50) / 100;
			if (wanted < 1) wanted = 1;
			if (wanted > eligible) wanted = eligible;
			int[] order = new int[eligible];
			int at = 0;
			for (int i = 0; i < Source.Count; i++)
			{
				KingdomInheritWork work = Source.WorkAt(i);
				if (work != null && work.State != KingdomInheritWorkState.Memory)
				{
					order[at++] = i;
				}
			}
			for (int i = 1; i < order.Length; i++)
			{
				int value = order[i];
				int write = i;
				while (write > 0 && SelectionBefore(Source.WorkAt(value), Source.WorkAt(order[write - 1]),
					InterregnumRoll, PreferHeart))
				{
					order[write] = order[write - 1];
					write--;
				}
				order[write] = value;
			}
			for (int i = 0; i < wanted; i++)
			{
				selected[order[i]] = true;
			}
			return selected;
		}

		private static bool SelectionBefore(KingdomInheritWork A, KingdomInheritWork B,
			int InterregnumRoll, bool PreferHeart)
		{
			int heartA = HeartRank(A.Key);
			int heartB = HeartRank(B.Key);
			if (heartA != heartB)
			{
				return PreferHeart ? heartA > heartB : heartA < heartB;
			}
			uint scoreA = SelectionScore(A, InterregnumRoll, PreferHeart);
			uint scoreB = SelectionScore(B, InterregnumRoll, PreferHeart);
			if (scoreA != scoreB)
			{
				return scoreA < scoreB;
			}
			return Before(A, B);
		}

		private static uint SelectionScore(KingdomInheritWork Work, int InterregnumRoll, bool PreferHeart)
		{
			uint hash = 2166136261U;
			for (int i = 0; i < Work.Key.Length; i++)
			{
				hash ^= Work.Key[i];
				hash *= 16777619U;
			}
			hash ^= (uint)Work.X;
			hash *= 16777619U;
			hash ^= (uint)Work.Y;
			hash *= 16777619U;
			hash ^= (uint)InterregnumRoll;
			hash *= 16777619U;
			hash ^= PreferHeart ? 0xA17E5EEDU : 0xFAD3D123U;
			hash *= 16777619U;
			return hash;
		}

		private static int Distance(int AX, int AY, int BX, int BY)
		{
			long dx = (long)AX - BX;
			long dy = (long)AY - BY;
			if (dx < 0L) dx = -dx;
			if (dy < 0L) dy = -dy;
			long distance = (dx > dy) ? dx : dy;
			return (distance > int.MaxValue) ? int.MaxValue : (int)distance;
		}

		private static int Min(int A, int B)
		{
			return (A < B) ? A : B;
		}

		private static bool Before(KingdomInheritWork A, KingdomInheritWork B)
		{
			if (A == null) return false;
			if (B == null) return true;
			if (A.Y != B.Y) return A.Y < B.Y;
			if (A.X != B.X) return A.X < B.X;
			return string.CompareOrdinal(A.Key, B.Key) < 0;
		}

		private static void Sort(Candidate[] Candidates)
		{
			for (int i = 1; i < Candidates.Length; i++)
			{
				Candidate value = Candidates[i];
				int at = i;
				while (at > 0 && CandidateBefore(value, Candidates[at - 1]))
				{
					Candidates[at] = Candidates[at - 1];
					at--;
				}
				Candidates[at] = value;
			}
		}

		private static bool CandidateBefore(Candidate A, Candidate B)
		{
			if (A.X != B.X) return A.X < B.X;
			if (A.Y != B.Y) return A.Y < B.Y;
			int key = string.CompareOrdinal(A.Key, B.Key);
			if (key != 0) return key < 0;
			if (A.State != B.State) return A.State > B.State;
			return A.Condition < B.Condition;
		}

		private static KingdomInheritPlan EmptyPlan()
		{
			return new KingdomInheritPlan(new KingdomInheritWork[0], 0, 0);
		}
	}
}
