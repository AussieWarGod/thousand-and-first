using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal struct KingdomDesignationMatch
	{
		internal KingdomBenefitDesignation Designation;
		internal KingdomBenefitCellUse Use;
		internal KingdomBenefitCover Cover;
		internal string NetworkKey;
		internal int X;
		internal int Y;
	}

	public sealed partial class KingdomDesignationIndex
	{
		internal List<KingdomDesignationMatch> MatchingExact(int X, int Y,
			KingdomBenefitScope Scope, bool InContainer, string NetworkKey)
		{
			List<KingdomDesignationMatch> result = new List<KingdomDesignationMatch>();
			if (!ByCell.TryGetValue(KingdomDesignationRules.Pack(X, Y), out var rows))
				return result;
			for (int i = 0; i < rows.Count; i++)
				if (Accepts(rows[i], Scope, InContainer, NetworkKey))
					result.Add(new KingdomDesignationMatch {
						Designation = rows[i].Designation, Use = rows[i].Use,
						Cover = rows[i].Cover, NetworkKey = rows[i].NetworkKey, X = X, Y = Y
					});
			return result;
		}

		internal KingdomBenefitDesignation FindRootExact(string RootId)
		{
			if (string.IsNullOrEmpty(RootId)) return null;
			for (int i = 0; i < Rows.Count; i++)
				if (Rows[i].RootId == RootId) return Rows[i];
			return null;
		}

		internal static string[] StructuralTags(KingdomBenefitCover Cover, bool Underground)
		{
			if (Underground || Cover == KingdomBenefitCover.Walled
				|| Cover == KingdomBenefitCover.Natural
				|| Cover == KingdomBenefitCover.ObservedEnclosure)
				return new[] { KingdomQolRules.TagDark };
			return new[] { KingdomQolRules.TagSky };
		}
	}
}
