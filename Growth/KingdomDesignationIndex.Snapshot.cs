using System;

namespace ThousandAndFirst
{
	public sealed partial class KingdomDesignationIndex
	{
		internal bool SameSnapshot(KingdomDesignationIndex Other)
		{
			if (Other == null || Rows.Count != Other.Rows.Count
				|| SourceFaultRows.Count != Other.SourceFaultRows.Count) return false;
			for (int i = 0; i < SourceFaultRows.Count; i++)
				if (SourceFaultRows[i] != Other.SourceFaultRows[i]) return false;
			for (int i = 0; i < Rows.Count; i++)
				if (!SameExactDesignation(Rows[i], Other.Rows[i])) return false;
			return true;
		}

		internal static bool SameExactDesignation(KingdomBenefitDesignation A,
			KingdomBenefitDesignation B)
		{
			if (A == null || B == null || A.ProviderId != B.ProviderId
				|| A.ProviderVersion != B.ProviderVersion || A.Identity != B.Identity
				|| A.Revision != B.Revision || A.ZoneId != B.ZoneId
				|| A.RootId != B.RootId || A.BuildingKey != B.BuildingKey
				|| A.LotId != B.LotId || A.Caps.Count != B.Caps.Count
				|| A.AcceptedTags.Count != B.AcceptedTags.Count
				|| A.Cells.Count != B.Cells.Count) return false;
			for (int i = 0; i < A.Caps.Count; i++)
				if (A.Caps[i].Kind != B.Caps[i].Kind
					|| A.Caps[i].Amount != B.Caps[i].Amount) return false;
			for (int i = 0; i < A.AcceptedTags.Count; i++)
				if (A.AcceptedTags[i] != B.AcceptedTags[i]) return false;
			for (int i = 0; i < A.Cells.Count; i++)
			{
				KingdomBenefitCell a = A.Cells[i]; KingdomBenefitCell b = B.Cells[i];
				if (a.X != b.X || a.Y != b.Y || a.Use != b.Use
					|| a.Cover != b.Cover || a.NetworkKey != b.NetworkKey) return false;
			}
			return true;
		}
	}
}
