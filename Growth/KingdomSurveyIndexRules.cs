using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free laws for maintaining one ordered zone index after physical commits.</summary>
	public static class KingdomSurveyIndexRules
	{
		public enum Mutation
		{
			Refuse,
			Add,
			Refresh,
			Remove
		}

		/// <summary>A known object leaving or becoming invalid is removed; an unknown valid object
		/// on the surveyed ground is appended; only a known valid object may refresh in place.</summary>
		public static Mutation Classify(bool Known, bool Valid, bool OnSurveyedGround)
		{
			if (Known)
			{
				return Valid && OnSurveyedGround ? Mutation.Refresh : Mutation.Remove;
			}
			return Valid && OnSurveyedGround ? Mutation.Add : Mutation.Refuse;
		}

		/// <summary>Stable insertion point for a monotone object-order token. Equal tokens remain in
		/// their original order, so refreshing one category cannot reshuffle an earlier decision.</summary>
		public static int StableInsertionIndex(IList<long> Existing, long Order)
		{
			if (Existing == null) throw new ArgumentNullException(nameof(Existing));
			int low = 0;
			int high = Existing.Count;
			while (low < high)
			{
				int middle = low + ((high - low) / 2);
				if (Existing[middle] <= Order) low = middle + 1;
				else high = middle;
			}
			return low;
		}

		public static bool ComesBeforeOrEqual(long ExistingOrder, long IncomingOrder)
		{
			return ExistingOrder <= IncomingOrder;
		}
	}
}
