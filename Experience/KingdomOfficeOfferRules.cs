using System;

namespace ThousandAndFirst
{
	public sealed class KingdomOfficeCandidate
	{
		public int ResidentId;
		public string Name;
		public string Origin;
		public long ArrivedTick;
		public bool Eligible;
	}

	/// <summary>Deterministic two-name offer law. Fewer than two exact eligible residents means an
	/// indefinite vacancy; it never degrades into automatic appointment or a one-name non-choice.</summary>
	public static class KingdomOfficeOfferRules
	{
		public static bool TryOffer(KingdomOfficeCandidate[] Candidates,
			out KingdomOfficeCandidate First, out KingdomOfficeCandidate Second)
		{
			First = null; Second = null;
			if (Candidates == null) return false;
			for (int i = 0; i < Candidates.Length; i++)
			{
				KingdomOfficeCandidate row = Candidates[i];
				if (!Valid(row)) continue;
				if (First == null || Before(row, First))
				{
					Second = First; First = Copy(row);
				}
				else if (Second == null || Before(row, Second)) Second = Copy(row);
			}
			if (First != null && Second != null) return true;
			First = null; Second = null; return false;
		}

		private static bool Valid(KingdomOfficeCandidate Row)
		{
			return Row != null && Row.Eligible && Row.ResidentId > 0
				&& KingdomExperienceRules.CivicText(Row.Name, true)
				&& KingdomExperienceRules.CivicText(Row.Origin, false)
				&& Row.ArrivedTick >= 0L;
		}

		private static bool Before(KingdomOfficeCandidate A, KingdomOfficeCandidate B)
		{
			return A.ArrivedTick < B.ArrivedTick || A.ArrivedTick == B.ArrivedTick
				&& A.ResidentId < B.ResidentId;
		}

		private static KingdomOfficeCandidate Copy(KingdomOfficeCandidate R)
		{
			return new KingdomOfficeCandidate { ResidentId = R.ResidentId, Name = R.Name,
				Origin = R.Origin, ArrivedTick = R.ArrivedTick, Eligible = R.Eligible };
		}
	}
}
