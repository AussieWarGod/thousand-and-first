using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free laws for the founding transaction. The live transaction reports through this
	/// exact four-field contract; UI energy and inventory exit are derived only from it.
	/// </summary>
	public static partial class KingdomFoundingTransactionRules
	{
		public const int MaximumAuthorityLength = 2048;
		public const int MaximumComponentEncodingLength = 4096;
		public const int MaximumComponentCount = 32;
		public const int MaximumComponentNameLength = 64;
		public const int VillageStandingEffectPrepared = 1;
		public const int VillageStandingEffectApplied = 2;

		private const string AuthorityVersion = "taf-founding-v1";

		public static bool IsKnownKind(KingdomFoundingKind Kind)
		{
			return Kind == KingdomFoundingKind.None ||
				Kind == KingdomFoundingKind.FirstCity ||
				Kind == KingdomFoundingKind.SecondCity ||
				Kind == KingdomFoundingKind.VillageCharter;
		}

		public static bool IsKnownPhase(KingdomFoundingPhase Phase)
		{
			return Phase == KingdomFoundingPhase.None ||
				Phase == KingdomFoundingPhase.WaterCommitted ||
				Phase == KingdomFoundingPhase.PublicationCommitted ||
				Phase == KingdomFoundingPhase.RecoveryRequired ||
				Phase == KingdomFoundingPhase.Complete;
		}

		public static bool IsKnownOwnerKind(KingdomFoundingOwnerKind Kind)
		{
			return Kind == KingdomFoundingOwnerKind.None ||
				Kind == KingdomFoundingOwnerKind.Basin ||
				Kind == KingdomFoundingOwnerKind.Direct;
		}

		public static bool IsKnownChronicleDisposition(KingdomChronicleDisposition Disposition)
		{
			return Disposition == KingdomChronicleDisposition.None ||
				Disposition == KingdomChronicleDisposition.Required ||
				Disposition == KingdomChronicleDisposition.Inserted ||
				Disposition == KingdomChronicleDisposition.Skipped;
		}

		/// <summary>Strict outbox state law. A terminal row remembers whether insertion happened;
		/// current options are deliberately absent from this decision.</summary>
		public static bool ChronicleDispositionValid(int Stage,
			KingdomChronicleDisposition Disposition, int AccomplishmentCount)
		{
			if (Stage < 0 || Stage > 2 || !IsKnownChronicleDisposition(Disposition) ||
				AccomplishmentCount < 0 || AccomplishmentCount > 1)
			{
				return false;
			}
			if (Stage == 0)
			{
				return Disposition == KingdomChronicleDisposition.None &&
					AccomplishmentCount == 0;
			}
			if (Stage == 1)
			{
				return (Disposition == KingdomChronicleDisposition.None &&
						AccomplishmentCount == 0) ||
					(Disposition == KingdomChronicleDisposition.Required) ||
					(Disposition == KingdomChronicleDisposition.Inserted &&
						AccomplishmentCount == 1) ||
					(Disposition == KingdomChronicleDisposition.Skipped &&
						AccomplishmentCount == 0);
			}
			return (Disposition == KingdomChronicleDisposition.Inserted &&
					AccomplishmentCount == 1) ||
				(Disposition == KingdomChronicleDisposition.Skipped &&
					AccomplishmentCount == 0);
		}

		/// <summary>Conservative migration for receipts written before disposition existed. A
		/// retained journal row proves Inserted. A missing row proves old option-No only while that
		/// option is still No; after an option change it is intentionally left unresolved.</summary>
		public static bool TryMigrateChronicleDisposition(int Stage, bool RawPresent, int Raw,
			int AccomplishmentCount, bool ChronicleOptionIsNo,
			out KingdomChronicleDisposition Disposition, out bool NeedsWrite)
		{
			Disposition = KingdomChronicleDisposition.None;
			NeedsWrite = false;
			if (Stage < 0 || Stage > 2 || AccomplishmentCount < 0 ||
				AccomplishmentCount > 1)
			{
				return false;
			}
			if (RawPresent)
			{
				Disposition = (KingdomChronicleDisposition)Raw;
				return ChronicleDispositionValid(Stage, Disposition,
					AccomplishmentCount);
			}
			NeedsWrite = true;
			if (Stage < 2)
			{
				Disposition = Stage == 0
					? KingdomChronicleDisposition.None
					: (AccomplishmentCount == 1
						? KingdomChronicleDisposition.Inserted
						: KingdomChronicleDisposition.None);
				return ChronicleDispositionValid(Stage, Disposition,
					AccomplishmentCount);
			}
			if (AccomplishmentCount == 1)
			{
				Disposition = KingdomChronicleDisposition.Inserted;
				return true;
			}
			if (ChronicleOptionIsNo)
			{
				Disposition = KingdomChronicleDisposition.Skipped;
				return true;
			}
			return false;
		}

		public static bool TryParseKind(int Raw, out KingdomFoundingKind Kind)
		{
			Kind = (KingdomFoundingKind)Raw;
			return IsKnownKind(Kind);
		}

		public static bool TryParsePhase(int Raw, out KingdomFoundingPhase Phase)
		{
			Phase = (KingdomFoundingPhase)Raw;
			return IsKnownPhase(Phase);
		}
		/// <summary>
		/// Classifies receipt headers without trusting enum casts from an object's property map.
		/// A non-empty kind with phase None was staged before any debit and is safe to clear;
		/// Complete is also terminal. Any other malformed pair may describe paid water and must stay
		/// quarantined rather than being guessed clean.
		/// </summary>
		public static KingdomFoundingReceiptNormalization Normalize(
			KingdomFoundingKind Kind, KingdomFoundingPhase Phase)
		{
			bool knownKind = IsKnownKind(Kind);
			bool knownPhase = IsKnownPhase(Phase);
			if (knownKind && Kind == KingdomFoundingKind.None &&
				knownPhase && Phase == KingdomFoundingPhase.None)
			{
				return KingdomFoundingReceiptNormalization.Clean;
			}
			if (knownKind && knownPhase && IsPending(Kind, Phase))
			{
				return KingdomFoundingReceiptNormalization.Pending;
			}
			if (knownKind && Kind != KingdomFoundingKind.None && knownPhase &&
				Phase == KingdomFoundingPhase.None)
			{
				return KingdomFoundingReceiptNormalization.ClearStaged;
			}
			// Complete is paid state until every live projection and seal is observed again. A
			// forged Complete header must never erase the only receipt for spent water.
			if (knownKind && Kind != KingdomFoundingKind.None && knownPhase &&
				Phase == KingdomFoundingPhase.Complete)
			{
				return KingdomFoundingReceiptNormalization.Pending;
			}
			return KingdomFoundingReceiptNormalization.Quarantine;
		}

		/// <summary>Strict raw header classification, including missing-key provenance.</summary>
		public static KingdomFoundingReceiptNormalization NormalizeRaw(bool KindPresent,
			int RawKind, bool PhasePresent, int RawPhase, bool AnyPayloadPresent)
		{
			if (!KindPresent && !PhasePresent)
			{
				return AnyPayloadPresent
					? KingdomFoundingReceiptNormalization.Quarantine
					: KingdomFoundingReceiptNormalization.Clean;
			}
			if (!KindPresent || !PhasePresent ||
				!TryParseKind(RawKind, out var kind) ||
				!TryParsePhase(RawPhase, out var phase))
			{
				return KingdomFoundingReceiptNormalization.Quarantine;
			}
			if (kind == KingdomFoundingKind.None && phase == KingdomFoundingPhase.None)
			{
				return AnyPayloadPresent
					? KingdomFoundingReceiptNormalization.Quarantine
					: KingdomFoundingReceiptNormalization.Clean;
			}
			return Normalize(kind, phase);
		}

		public static bool IsPending(KingdomFoundingKind Kind, KingdomFoundingPhase Phase)
		{
			return IsKnownKind(Kind) && Kind != KingdomFoundingKind.None &&
				(Phase == KingdomFoundingPhase.WaterCommitted ||
				 Phase == KingdomFoundingPhase.PublicationCommitted ||
				 Phase == KingdomFoundingPhase.RecoveryRequired ||
				 Phase == KingdomFoundingPhase.Complete);
		}

		public static bool IsValidPair(KingdomFoundingKind Kind, KingdomFoundingPhase Phase)
		{
			if (!IsKnownKind(Kind) || !IsKnownPhase(Phase))
			{
				return false;
			}
			if (Kind == KingdomFoundingKind.None)
			{
				return Phase == KingdomFoundingPhase.None;
			}
			return Phase == KingdomFoundingPhase.None ||
				Phase == KingdomFoundingPhase.WaterCommitted ||
				Phase == KingdomFoundingPhase.PublicationCommitted ||
				Phase == KingdomFoundingPhase.RecoveryRequired ||
				Phase == KingdomFoundingPhase.Complete;
		}
	}
}
