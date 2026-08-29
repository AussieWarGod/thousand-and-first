namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>Explicit canonical C2 receipt. Empty means no current-realm retirement began.</summary>
		public string RealmRetirementWire = "";

		/// <summary>Monotonic incarnation reserved from the base StringGameState fence.</summary>
		public long RealmIncarnation;

		/// <summary>Exact first-founding transaction holding a pre-debit fence reservation.</summary>
		public string PendingRealmIncarnationTransaction;

		/// <summary>Reserved high-water value paired with PendingRealmIncarnationTransaction.</summary>
		public long PendingRealmIncarnation;

		/// <summary>Blank systems recreated over an operational fence cannot bootstrap authority.</summary>
		public string RealmIdentityFenceFault;

		public bool RealmRetirementBlocksWork
		{
			get
			{
				if (!string.IsNullOrEmpty(RealmIdentityFenceFault)) return true;
				if (string.IsNullOrEmpty(RealmRetirementWire)) return false;
				return true;
			}
		}

		public bool TryReadRealmRetirement(out KingdomRealmRetirementState State,
			out string Failure)
		{
			State = null;
			Failure = null;
			if (string.IsNullOrEmpty(RealmRetirementWire)) return true;
			return KingdomRealmRetirementCodec.TryDecode(RealmRetirementWire,
				out State, out Failure);
		}
	}
}
