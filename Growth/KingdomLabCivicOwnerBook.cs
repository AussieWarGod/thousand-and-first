using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	[Serializable]
	internal sealed class KingdomLabCivicOwnerRow
	{
		public string RealmId;
		public string SettlementId;
		public string ZoneId;
		public string OwnerObjectId;

		public KingdomLabCivicOwnerRow Copy()
		{
			return (KingdomLabCivicOwnerRow)MemberwiseClone();
		}
	}

	[Serializable]
	internal sealed class KingdomLabCivicOwnerBook
	{
		public int Version = KingdomLabCivicOwnerRules.CurrentVersion;
		public List<KingdomLabCivicOwnerRow> Rows = new List<KingdomLabCivicOwnerRow>();

		public KingdomLabCivicOwnerBook Copy()
		{
			KingdomLabCivicOwnerBook copy = new KingdomLabCivicOwnerBook { Version = Version };
			for (int i = 0; Rows != null && i < Rows.Count; i++)
				copy.Rows.Add(Rows[i]?.Copy());
			return copy;
		}
	}
}
