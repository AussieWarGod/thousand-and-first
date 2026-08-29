using System;

namespace ThousandAndFirst
{
	public static partial class KingdomStasisVaultRules
	{
		public static KingdomStasisVaultVerdict JudgeEntry(bool Founded,
			bool OwnedGround, bool ExactVault, bool DominatedSubject, bool ExactDominator,
			bool ExactCradle, bool EmptyBay, bool BodyHeldElsewhere, bool BodyInStasis,
			bool BodyInPhase, bool CradleClear, bool ForeignProjection,
			bool BoundedIdentity)
		{
			if (!Founded) return KingdomStasisVaultVerdict.Unfounded;
			if (!OwnedGround) return KingdomStasisVaultVerdict.WrongGround;
			if (!ExactVault) return KingdomStasisVaultVerdict.WrongVault;
			if (!DominatedSubject) return KingdomStasisVaultVerdict.NotDominating;
			if (!ExactDominator) return KingdomStasisVaultVerdict.DominatorMissing;
			if (!ExactCradle) return KingdomStasisVaultVerdict.WrongCradle;
			if (!EmptyBay) return KingdomStasisVaultVerdict.NoEmptyBay;
			if (BodyHeldElsewhere) return KingdomStasisVaultVerdict.BodyAlreadyHeld;
			if (BodyInStasis) return KingdomStasisVaultVerdict.BodyAlreadyStilled;
			if (!BodyInPhase) return KingdomStasisVaultVerdict.BodyOutOfPhase;
			if (!CradleClear) return KingdomStasisVaultVerdict.CradleOccupied;
			if (ForeignProjection) return KingdomStasisVaultVerdict.ForeignProjection;
			if (!BoundedIdentity) return KingdomStasisVaultVerdict.MalformedIdentity;
			return KingdomStasisVaultVerdict.Allowed;
		}

		public static KingdomStasisCustodyReceipt FieldProjected(
			KingdomStasisCustodyReceipt Receipt)
		{
			return Move(Receipt, KingdomStasisCustodyPhase.Prepared,
				KingdomStasisCustodyPhase.FieldProjected, 0L, "");
		}

		public static KingdomStasisCustodyReceipt Activated(
			KingdomStasisCustodyReceipt Receipt)
		{
			return Move(Receipt, KingdomStasisCustodyPhase.FieldProjected,
				KingdomStasisCustodyPhase.Active, 0L, "");
		}

		public static KingdomStasisCustodyReceipt BeginRelease(
			KingdomStasisCustodyReceipt Receipt)
		{
			if (Receipt == null || (Receipt.Phase != KingdomStasisCustodyPhase.Prepared
				&& Receipt.Phase != KingdomStasisCustodyPhase.FieldProjected
				&& Receipt.Phase != KingdomStasisCustodyPhase.Active)) return null;
			KingdomStasisCustodyReceipt copy = Receipt.Copy();
			copy.Phase = KingdomStasisCustodyPhase.ReleasePrepared;
			string failure;
			return Validate(copy, out failure) ? copy : null;
		}

		public static KingdomStasisCustodyReceipt Released(
			KingdomStasisCustodyReceipt Receipt, long Tick)
		{
			return Released(Receipt, Tick, "");
		}

		/// <summary>Closes a physically complete release while retaining a bounded
		/// custody warning. A warning is evidence, not an open bay: the body has already
		/// been made live and no vault projection remains.</summary>
		public static KingdomStasisCustodyReceipt Released(
			KingdomStasisCustodyReceipt Receipt, long Tick, string Warning)
		{
			return Move(Receipt, KingdomStasisCustodyPhase.ReleasePrepared,
				KingdomStasisCustodyPhase.Released, Tick,
				Clean(Warning, MaxFaultChars));
		}

		public static KingdomStasisCustodyReceipt Quarantined(
			KingdomStasisCustodyReceipt Receipt, string Fault)
		{
			if (Receipt == null) return null;
			int slot = Receipt.Slot >= 0 && Receipt.Slot < MaxSlots ? Receipt.Slot : 0;
			return QuarantineMalformed(Receipt, slot, Fault);
		}

		public static KingdomStasisCustodyReceipt QuarantineMalformed(
			KingdomStasisCustodyReceipt Receipt, int Slot, string Fault)
		{
			if (Receipt == null || Slot < 0 || Slot >= MaxSlots) return null;
			KingdomStasisCustodyReceipt copy = Receipt.Copy();
			copy.Version = CurrentReceiptVersion;
			copy.Phase = KingdomStasisCustodyPhase.Quarantined;
			copy.Slot = Slot;
			copy.Generation = Math.Max(0, copy.Generation);
			copy.CustodyId = Clean(copy.CustodyId, MaxIdentityChars);
			copy.RealmId = Clean(copy.RealmId, MaxIdentityChars);
			copy.SettlementId = Clean(copy.SettlementId, MaxIdentityChars);
			copy.ZoneId = Clean(copy.ZoneId, MaxIdentityChars);
			copy.VaultObjectId = Clean(copy.VaultObjectId, MaxIdentityChars);
			copy.LotId = Clean(copy.LotId, MaxIdentityChars);
			copy.CradleObjectId = Clean(copy.CradleObjectId, MaxIdentityChars);
			copy.FieldObjectId = Clean(copy.FieldObjectId, MaxIdentityChars);
			copy.BodyObjectId = Clean(copy.BodyObjectId, MaxIdentityChars);
			copy.SubjectObjectId = Clean(copy.SubjectObjectId, MaxIdentityChars);
			copy.BodyBlueprint = Clean(copy.BodyBlueprint, MaxIdentityChars);
			copy.BodyName = Clean(copy.BodyName, MaxNameChars);
			if (!DigestShape(copy.InventoryFingerprint)) copy.InventoryFingerprint = "";
			if (!DigestShape(copy.EquipmentFingerprint)) copy.EquipmentFingerprint = "";
			if (!DigestShape(copy.EffectFingerprint)) copy.EffectFingerprint = "";
			copy.EnteredTick = Math.Max(0L, copy.EnteredTick);
			copy.ReleasedTick = Math.Max(0L, copy.ReleasedTick);
			copy.Fault = Clean(Fault, MaxFaultChars);
			if (string.IsNullOrEmpty(copy.Fault)) copy.Fault = "stasis evidence diverged";
			string failure;
			return Validate(copy, out failure) ? copy : null;
		}

		public static KingdomStasisCustodyReceipt RetireQuarantine(
			KingdomStasisCustodyReceipt Receipt, long Tick)
		{
			if (Receipt == null || Receipt.Phase != KingdomStasisCustodyPhase.Quarantined
				|| Tick < Receipt.EnteredTick) return null;
			KingdomStasisCustodyReceipt copy = Receipt.Copy();
			copy.Phase = KingdomStasisCustodyPhase.Released;
			copy.ReleasedTick = Tick;
			copy.Fault = "";
			string failure;
			return Validate(copy, out failure) ? copy : null;
		}

		public static KingdomStasisRecoveryVerdict JudgeRecovery(bool AuthorityValid,
			bool OwnedGround, bool ExactVault, bool ExactCradle, bool ExactBody,
			bool ExactSubjectDomination, bool MarkerMatches, bool FieldMatches,
			bool BodyInStasis, bool BodyOutOfPhase)
		{
			if (!AuthorityValid || !ExactVault || !ExactCradle || !ExactBody
				|| !MarkerMatches || !FieldMatches)
				return KingdomStasisRecoveryVerdict.QuarantineAndRelease;
			if (!OwnedGround || !ExactSubjectDomination)
				return KingdomStasisRecoveryVerdict.Release;
			if (BodyInStasis && BodyOutOfPhase)
				return KingdomStasisRecoveryVerdict.KeepActive;
			return KingdomStasisRecoveryVerdict.ContinueForward;
		}

		private static KingdomStasisCustodyReceipt Move(
			KingdomStasisCustodyReceipt Receipt, KingdomStasisCustodyPhase From,
			KingdomStasisCustodyPhase To, long Tick, string Fault)
		{
			if (Receipt == null || Receipt.Phase != From) return null;
			KingdomStasisCustodyReceipt copy = Receipt.Copy();
			copy.Phase = To;
			copy.ReleasedTick = Tick;
			copy.Fault = Fault ?? "";
			string failure;
			return Validate(copy, out failure) ? copy : null;
		}
	}
}
