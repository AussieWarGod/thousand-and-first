using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Whole immutable realm/city authority captured beside a seal. Display names and
	/// seat role are absent: the exact full topology and both provenance chains are the proof.</summary>
	internal sealed class KingdomSealIdentity
	{
		public string RealmId;
		public string SettlementId;
		public List<string> SettlementIds = new List<string>();
		/// <summary>One canonical row per sorted SettlementIds entry. Each row binds the id to
		/// its complete immutable city provenance; a topology list without these rows is inert.</summary>
		public List<string> SettlementProvenanceRows = new List<string>();
		public int RealmIdentityVersion;
		public KingdomIdentityOrigin RealmIdentityOrigin;
		public string RealmIdentityTransactionId = "";
		public string RealmIdentityLegacyFaction = "";
		public long RealmIdentityFoundedTick;
		public ulong RealmIdentitySeedHigh = 0UL;
		public ulong RealmIdentitySeedLow = 0UL;
		public string RealmIdentityFirstClaimedZone = "";
		public int SettlementIdentityVersion;
		public KingdomIdentityOrigin SettlementIdentityOrigin;
		public string SettlementIdentityTransactionId = "";
		public long SettlementIdentityFoundedTick;
		public string SettlementIdentityFirstClaimedZone = "";
		public string SettlementIdentityLegacyId = "";
	}
}
