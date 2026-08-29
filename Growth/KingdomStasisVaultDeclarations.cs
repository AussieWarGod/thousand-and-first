using System;

#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public enum KingdomStasisCustodyPhase
	{
		Prepared = 1,
		FieldProjected = 2,
		Active = 3,
		ReleasePrepared = 4,
		Released = 5,
		Quarantined = 6
	}

	public enum KingdomStasisVaultVerdict
	{
		Allowed = 0,
		Unfounded = 1,
		WrongGround = 2,
		WrongVault = 3,
		NotDominating = 4,
		DominatorMissing = 5,
		WrongCradle = 6,
		NoEmptyBay = 7,
		BodyAlreadyHeld = 8,
		BodyAlreadyStilled = 9,
		BodyOutOfPhase = 10,
		CradleOccupied = 11,
		ForeignProjection = 12,
		MalformedIdentity = 13
	}

	public enum KingdomStasisRecoveryVerdict
	{
		ContinueForward = 0,
		KeepActive = 1,
		Release = 2,
		QuarantineAndRelease = 3
	}

	/// <summary>One exact body, one bay, and whole-body gear/effect custody evidence.</summary>
	[Serializable]
	public sealed class KingdomStasisCustodyReceipt
	{
		public int Version;
		public KingdomStasisCustodyPhase Phase;
		public int Slot;
		public int Generation;
		public string CustodyId = "";
		public string RealmId = "";
		public string SettlementId = "";
		public string ZoneId = "";
		public string VaultObjectId = "";
		public string LotId = "";
		public string CradleObjectId = "";
		public string FieldObjectId = "";
		public string BodyObjectId = "";
		public string SubjectObjectId = "";
		public string BodyBlueprint = "";
		public string BodyName = "";
		public string InventoryFingerprint = "";
		public string EquipmentFingerprint = "";
		public string EffectFingerprint = "";
		public long EnteredTick;
		public long ReleasedTick;
		public string Fault = "";

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomStasisCustodyReceipt));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomStasisCustodyReceipt));
			Normalize();
		}
#endif

		public void Normalize()
		{
			CustodyId = CustodyId ?? "";
			RealmId = RealmId ?? "";
			SettlementId = SettlementId ?? "";
			ZoneId = ZoneId ?? "";
			VaultObjectId = VaultObjectId ?? "";
			LotId = LotId ?? "";
			CradleObjectId = CradleObjectId ?? "";
			FieldObjectId = FieldObjectId ?? "";
			BodyObjectId = BodyObjectId ?? "";
			SubjectObjectId = SubjectObjectId ?? "";
			BodyBlueprint = BodyBlueprint ?? "";
			BodyName = BodyName ?? "";
			InventoryFingerprint = InventoryFingerprint ?? "";
			EquipmentFingerprint = EquipmentFingerprint ?? "";
			EffectFingerprint = EffectFingerprint ?? "";
			Fault = Fault ?? "";
		}

		public KingdomStasisCustodyReceipt Copy()
		{
			return (KingdomStasisCustodyReceipt)MemberwiseClone();
		}
	}
}
