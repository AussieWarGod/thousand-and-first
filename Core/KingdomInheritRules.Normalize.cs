using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritRules
	{
		internal static bool TryNormalize(IList<string> Keys, IList<int> X, IList<int> Y,
			IList<int> Conditions, out KingdomInheritPlan Plan, out KingdomInheritFault Fault)
		{
			Plan = EmptyPlan();
			Fault = KingdomInheritFault.None;
			try
			{
				return TryNormalizeCore(Keys, X, Y, Conditions, out Plan, out Fault);
			}
			catch
			{
				Plan = EmptyPlan();
				Fault = KingdomInheritFault.Malformed;
				return false;
			}
		}

		private static bool TryNormalizeCore(IList<string> Keys, IList<int> X, IList<int> Y,
			IList<int> Conditions, out KingdomInheritPlan Plan, out KingdomInheritFault Fault)
		{
			Plan = EmptyPlan();
			Fault = KingdomInheritFault.None;
			if (Keys == null || X == null || Y == null || Conditions == null)
			{
				Fault = KingdomInheritFault.NullInput;
				return false;
			}
			int count = Keys.Count;
			if (X.Count != count || Y.Count != count || Conditions.Count != count)
			{
				Fault = KingdomInheritFault.RowCountMismatch;
				return false;
			}
			if (count > MaxWorks)
			{
				Fault = KingdomInheritFault.TooManyWorks;
				return false;
			}
			Candidate[] candidates = new Candidate[count];
			for (int i = 0; i < count; i++)
			{
				string key = Keys[i];
				if (!IsStableSemanticKey(key))
				{
					Fault = KingdomInheritFault.InvalidKey;
					return false;
				}
				int condition = Conditions[i];
				if (condition < 0 || condition > 100)
				{
					Fault = KingdomInheritFault.ConditionOutOfRange;
					return false;
				}
				int x = X[i];
				int y = Y[i];
				if (!SourceCoordinate(x) || !SourceCoordinate(y))
				{
					Fault = KingdomInheritFault.CoordinateOutOfRange;
					return false;
				}
				bool known = IsInheritableKey(key) && key != RubbleKey
					&& key != MemoryKey && key != FounderCairnKey;
				candidates[i] = new Candidate
				{
					Key = known ? key : MemoryKey,
					X = x,
					Y = y,
					Condition = known ? condition : 0,
					State = known ? KingdomInheritWorkState.Standing : KingdomInheritWorkState.Memory
				};
			}
			Sort(candidates);
			int unique = Deduplicate(candidates);
			DegradeAmbiguousFootprints(candidates, unique);
			return TryBuildPlan(candidates, unique, out Plan, out Fault);
		}

		/// <summary>
		/// Old city books prove anchors, not the footprint version under which a work
		/// was built. Adjacent single-cell migrated works can therefore overlap only
		/// when interpreted through today's catalogue dimensions. Every member of such
		/// an ambiguous overlap becomes a one-cell memory at its exact old anchor; one
		/// local uncertainty must never invalidate the whole legacy.
		/// </summary>
		private static void DegradeAmbiguousFootprints(Candidate[] Candidates, int Count)
		{
			bool[] ambiguous = new bool[Count];
			for (int i = 0; i < Count; i++)
			{
				Rect current;
				if (!TryRect(Candidates[i].Key, Candidates[i].X, Candidates[i].Y, out current))
				{
					ambiguous[i] = true;
					continue;
				}
				for (int j = 0; j < i; j++)
				{
					Rect earlier;
					if (!TryRect(Candidates[j].Key, Candidates[j].X, Candidates[j].Y, out earlier)
						|| Overlaps(current, earlier))
					{
						ambiguous[i] = true;
						ambiguous[j] = true;
					}
				}
			}
			for (int i = 0; i < Count; i++)
			{
				if (ambiguous[i])
				{
					Candidates[i].Key = MemoryKey;
					Candidates[i].Condition = 0;
					Candidates[i].State = KingdomInheritWorkState.Memory;
				}
			}
		}

	}
}
