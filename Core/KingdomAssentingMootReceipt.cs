using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// City authority for one exact moot, its six assenting voices, and its bounded exemptions.
	/// Native zone/body parts are reversible projections of this receipt.
	/// </summary>
	[Serializable]
	public sealed class KingdomAssentingMootReceipt
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int Version = KingdomAssentingMootRules.CurrentReceiptVersion;
		public KingdomAssentingMootPhase Phase;
		public int Generation;
		public string RealmId = "";
		public string SettlementId = "";
		public string SettlementName = "";
		public string ZoneId = "";
		public string BuildingObjectId = "";
		public string LotId = "";
		public string AuthorityId = "";
		public string MembershipFingerprint = "";
		public int BaselineHitpoints;
		public int Strength;
		public List<int> AssentResidentIds = new List<int>();
		public List<string> AssentResidentNames = new List<string>();
		public List<string> AssentBodyObjectIds = new List<string>();
		public List<int> ExemptResidentIds = new List<int>();
		public List<string> ExemptResidentNames = new List<string>();
		public List<string> ExemptBodyObjectIds = new List<string>();
		public long PreparedTick;
		public long AppliedTick;
		public long SuspendedTick;
		public string SuspendedReason = "";
		public string Fault = "";

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomAssentingMootReceipt));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomAssentingMootReceipt));
			Normalize();
		}
#endif

		public void Normalize()
		{
			RealmId = RealmId ?? "";
			SettlementId = SettlementId ?? "";
			SettlementName = SettlementName ?? "";
			ZoneId = ZoneId ?? "";
			BuildingObjectId = BuildingObjectId ?? "";
			LotId = LotId ?? "";
			AuthorityId = AuthorityId ?? "";
			MembershipFingerprint = MembershipFingerprint ?? "";
			AssentResidentIds = AssentResidentIds ?? new List<int>();
			AssentResidentNames = AssentResidentNames ?? new List<string>();
			AssentBodyObjectIds = AssentBodyObjectIds ?? new List<string>();
			ExemptResidentIds = ExemptResidentIds ?? new List<int>();
			ExemptResidentNames = ExemptResidentNames ?? new List<string>();
			ExemptBodyObjectIds = ExemptBodyObjectIds ?? new List<string>();
			SuspendedReason = SuspendedReason ?? "";
			Fault = Fault ?? "";
			if (Phase == KingdomAssentingMootPhase.None) Clear();
		}

		public KingdomAssentingMootReceipt Copy()
		{
			KingdomAssentingMootReceipt copy =
				(KingdomAssentingMootReceipt)MemberwiseClone();
			copy.AssentResidentIds = new List<int>(AssentResidentIds ?? new List<int>());
			copy.AssentResidentNames = new List<string>(AssentResidentNames ?? new List<string>());
			copy.AssentBodyObjectIds = new List<string>(AssentBodyObjectIds ?? new List<string>());
			copy.ExemptResidentIds = new List<int>(ExemptResidentIds ?? new List<int>());
			copy.ExemptResidentNames = new List<string>(ExemptResidentNames ?? new List<string>());
			copy.ExemptBodyObjectIds = new List<string>(ExemptBodyObjectIds ?? new List<string>());
			return copy;
		}

		private void Clear()
		{
			Generation = BaselineHitpoints = Strength = 0;
			PreparedTick = AppliedTick = SuspendedTick = 0L;
			RealmId = SettlementId = SettlementName = ZoneId = BuildingObjectId = LotId = "";
			AuthorityId = MembershipFingerprint = SuspendedReason = Fault = "";
			AssentResidentIds.Clear();
			AssentResidentNames.Clear();
			AssentBodyObjectIds.Clear();
			ExemptResidentIds.Clear();
			ExemptResidentNames.Clear();
			ExemptBodyObjectIds.Clear();
		}
	}
}
