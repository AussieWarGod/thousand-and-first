using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>The three promises the founder's basin can publish.</summary>
	public enum KingdomFoundingKind : byte
	{
		None = 0,
		FirstCity = 1,
		SecondCity = 2,
		VillageCharter = 3
	}

	/// <summary>
	/// Durable phase stored on the exact basin that paid for a rite. A non-empty phase is an
	/// interrupted rite and is resumed, never mistaken for a new pour.
	/// </summary>
	public enum KingdomFoundingPhase : byte
	{
		None = 0,
		WaterCommitted = 1,
		PublicationCommitted = 2,
		RecoveryRequired = 3,
		Complete = 4
	}

	/// <summary>Safe disposition for a decoded kind/phase pair at an entry point.</summary>
	public enum KingdomFoundingReceiptNormalization : byte
	{
		Clean = 0,
		Pending = 1,
		ClearStaged = 2,
		Quarantine = 3
	}

	/// <summary>Provenance of authority to publish a founding.</summary>
	public enum KingdomFoundingOwnerKind : byte
	{
		None = 0,
		Basin = 1,
		Direct = 2
	}

	/// <summary>
	/// Canonical authority reserved globally and on the exact site before publication. Names and
	/// water details stay in the receipt; their digest is the last member of this tuple.
	/// </summary>
	public struct KingdomFoundingAuthority
	{
		public KingdomFoundingKind Kind;
		public string TransactionID;
		public KingdomFoundingOwnerKind OwnerKind;
		public string OwnerNonce;
		public string RealmFaction;
		public string ZoneID;
		public int RiteX;
		public int RiteY;
		public string PayloadDigest;
	}

	/// <summary>Exact caller contract for one attempt or resumption.</summary>
	public enum KingdomFoundingOutcome : byte
	{
		Refused = 0,
		CompensatedFailure = 1,
		RecoverableFailure = 2,
		Committed = 3
	}

	/// <summary>What happened to the basin's measured water.</summary>
	public enum KingdomFoundingWaterDisposition : byte
	{
		Untouched = 0,
		RestoredExactly = 1,
		HeldForRecovery = 2,
		Spent = 3,
		RestorationFailed = 4
	}

	/// <summary>Ordered live projections. Tests inject a failure after every boundary.</summary>
	public enum KingdomFoundingProjection : byte
	{
		None = 0,
		Water = 1,
		Identity = 2,
		Claim = 3,
		Seat = 4,
		Ability = 5,
		Placement = 6,
		Seal = 7
	}

	/// <summary>Durable decision made by a chronicle outbox about its optional journal row.
	/// Required is written before the external Journal callback; Inserted and Skipped are terminal
	/// observations and therefore do not change when the live option changes later.</summary>
	public enum KingdomChronicleDisposition : byte
	{
		None = 0,
		Required = 1,
		Inserted = 2,
		Skipped = 3
	}

	/// <summary>
	/// Engine-free laws for the founding transaction. The live transaction reports through this
	/// exact four-field contract; UI energy and inventory exit are derived only from it.
	/// </summary>
	public static class KingdomFoundingTransactionRules
	{
		public const int MaximumAuthorityLength = 2048;
		public const int MaximumComponentEncodingLength = 4096;
		public const int MaximumComponentCount = 32;
		public const int MaximumComponentNameLength = 64;

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

		/// <summary>
		/// Exact durable identity binding. First founding binds its intended realm before the system
		/// can name that realm; every later kind requires the live system realm immediately.
		/// </summary>
		public static bool ReceiptBindingMatches(string BoundOwnerNonce, string CurrentOwnerNonce,
			KingdomFoundingOwnerKind OwnerKind, string TransactionID,
			string BoundRealmFaction, string PendingName, string CurrentRealmFaction,
			KingdomFoundingKind Kind)
		{
			if (OwnerKind != KingdomFoundingOwnerKind.Basin ||
				!IsNonce(BoundOwnerNonce) ||
				!string.Equals(BoundOwnerNonce, CurrentOwnerNonce, StringComparison.Ordinal) ||
				string.IsNullOrEmpty(TransactionID) || string.IsNullOrEmpty(BoundRealmFaction))
			{
				return false;
			}
			if (Kind == KingdomFoundingKind.FirstCity)
			{
				return string.Equals(BoundRealmFaction, PendingName, StringComparison.Ordinal) &&
					(string.IsNullOrEmpty(CurrentRealmFaction) ||
					 string.Equals(BoundRealmFaction, CurrentRealmFaction,
						 StringComparison.Ordinal));
			}
			return (Kind == KingdomFoundingKind.SecondCity ||
				Kind == KingdomFoundingKind.VillageCharter) &&
				string.Equals(BoundRealmFaction, CurrentRealmFaction,
					StringComparison.Ordinal);
		}

		/// <summary>Legacy overload retained for old callers/tests; object IDs are not copy-safe.</summary>
		public static bool ReceiptBindingMatches(string BoundBasinID, string CurrentBasinID,
			string TransactionID, string BoundRealmFaction, string PendingName,
			string CurrentRealmFaction, KingdomFoundingKind Kind)
		{
			return !string.IsNullOrEmpty(BoundBasinID) &&
				string.Equals(BoundBasinID, CurrentBasinID, StringComparison.Ordinal) &&
				!string.IsNullOrEmpty(TransactionID) &&
				!string.IsNullOrEmpty(BoundRealmFaction) &&
				(Kind == KingdomFoundingKind.FirstCity
					? string.Equals(BoundRealmFaction, PendingName, StringComparison.Ordinal) &&
					  (string.IsNullOrEmpty(CurrentRealmFaction) ||
					   string.Equals(BoundRealmFaction, CurrentRealmFaction,
						   StringComparison.Ordinal))
					: (Kind == KingdomFoundingKind.SecondCity ||
					   Kind == KingdomFoundingKind.VillageCharter) &&
					  string.Equals(BoundRealmFaction, CurrentRealmFaction,
						  StringComparison.Ordinal));
		}

		/// <summary>
		/// A second projection may start only from one exact city and no Away seat. Once published,
		/// only its own exact seated city may resume. This prevents a stale receipt from replacing an
		/// unrelated second city.
		/// </summary>
		public static bool SecondRecoveryCanProject(int SettlementCount, int MaximumSettlements,
			bool AwayIsNull, bool TargetIsExactSeat, bool TargetIsExactAway,
			bool AlreadyPublished)
		{
			if (MaximumSettlements < 2 || SettlementCount < 0 ||
				SettlementCount > MaximumSettlements)
			{
				return false;
			}
			if (AlreadyPublished)
			{
				return !AwayIsNull && SettlementCount == MaximumSettlements &&
					(TargetIsExactSeat ^ TargetIsExactAway);
			}
			return !TargetIsExactSeat && !TargetIsExactAway && AwayIsNull &&
				SettlementCount == MaximumSettlements - 1;
		}

		public static bool SecondRecoveryCanProject(int SettlementCount, int MaximumSettlements,
			bool AwayIsNull, bool TargetIsExactSeat, bool AlreadyPublished)
		{
			return SecondRecoveryCanProject(SettlementCount, MaximumSettlements, AwayIsNull,
				TargetIsExactSeat, TargetIsExactAway: false, AlreadyPublished);
		}

		/// <summary>Exact liquid algebra for a basin receipt, independent of current vessel.</summary>
		public static bool WaterAlgebraValid(int OriginalVolume, int OriginalMaxVolume,
			int CommittedVolume, int CommittedMaxVolume, int Cost,
			bool OriginalIsPureWater, bool CommittedComponentsValid)
		{
			return OriginalVolume >= Cost && Cost > 0 && OriginalMaxVolume >= OriginalVolume &&
				OriginalMaxVolume >= 0 && CommittedMaxVolume == OriginalMaxVolume &&
				CommittedVolume == OriginalVolume - Cost && CommittedVolume >= 0 &&
				OriginalIsPureWater && CommittedComponentsValid;
		}

		/// <summary>Phase-specific vessel proof. None is clearable only at original; paid phases
		/// require committed. RecoveryRequired is always quarantine. Complete remains paid until
		/// live completion is observed.</summary>
		public static KingdomFoundingReceiptNormalization ValidatePhaseState(
			KingdomFoundingPhase Phase, bool PayloadValid, bool CurrentMatchesOriginal,
			bool CurrentMatchesCommitted, bool CompletionObserved)
		{
			if (!IsKnownPhase(Phase) || !PayloadValid)
			{
				return KingdomFoundingReceiptNormalization.Quarantine;
			}
			switch (Phase)
			{
			case KingdomFoundingPhase.None:
				return CurrentMatchesOriginal
					? KingdomFoundingReceiptNormalization.ClearStaged
					: KingdomFoundingReceiptNormalization.Quarantine;
			case KingdomFoundingPhase.WaterCommitted:
			case KingdomFoundingPhase.PublicationCommitted:
				return CurrentMatchesCommitted
					? KingdomFoundingReceiptNormalization.Pending
					: KingdomFoundingReceiptNormalization.Quarantine;
			case KingdomFoundingPhase.RecoveryRequired:
				return KingdomFoundingReceiptNormalization.Quarantine;
			case KingdomFoundingPhase.Complete:
				return CurrentMatchesCommitted && CompletionObserved
					? KingdomFoundingReceiptNormalization.ClearStaged
					: KingdomFoundingReceiptNormalization.Quarantine;
			default:
				return KingdomFoundingReceiptNormalization.Quarantine;
			}
		}

		public static string FormatAuthority(KingdomFoundingAuthority Authority)
		{
			if (!AuthorityValid(Authority))
			{
				return null;
			}
			return string.Join("|", new string[]
			{
				AuthorityVersion,
				((int)Authority.Kind).ToString(),
				((int)Authority.OwnerKind).ToString(),
				EncodeField(Authority.TransactionID),
				EncodeField(Authority.OwnerNonce),
				EncodeField(Authority.RealmFaction),
				EncodeField(Authority.ZoneID),
				Authority.RiteX.ToString(),
				Authority.RiteY.ToString(),
				Authority.PayloadDigest
			});
		}

		public static bool TryParseAuthority(string Encoded,
			out KingdomFoundingAuthority Authority)
		{
			Authority = default(KingdomFoundingAuthority);
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaximumAuthorityLength)
			{
				return false;
			}
			string[] fields = Encoded.Split('|');
			if (fields.Length != 10 || fields[0] != AuthorityVersion ||
				!int.TryParse(fields[1], out var rawKind) ||
				!TryParseKind(rawKind, out Authority.Kind) ||
				!int.TryParse(fields[2], out var rawOwner))
			{
				return false;
			}
			Authority.OwnerKind = (KingdomFoundingOwnerKind)rawOwner;
			if (!TryDecodeField(fields[3], 64, out Authority.TransactionID) ||
				!TryDecodeField(fields[4], 64, out Authority.OwnerNonce) ||
				!TryDecodeField(fields[5], 256, out Authority.RealmFaction) ||
				!TryDecodeField(fields[6], 512, out Authority.ZoneID) ||
				!int.TryParse(fields[7], out Authority.RiteX) ||
				!int.TryParse(fields[8], out Authority.RiteY))
			{
				return false;
			}
			Authority.PayloadDigest = fields[9];
			return AuthorityValid(Authority) && FormatAuthority(Authority) == Encoded;
		}

		public static bool AuthorityMatches(string Encoded,
			KingdomFoundingAuthority Expected)
		{
			return TryParseAuthority(Encoded, out var parsed) &&
				FormatAuthority(parsed) == FormatAuthority(Expected);
		}

		public static bool AuthorityValid(KingdomFoundingAuthority Authority)
		{
			return IsKnownKind(Authority.Kind) && Authority.Kind != KingdomFoundingKind.None &&
				IsKnownOwnerKind(Authority.OwnerKind) &&
				Authority.OwnerKind != KingdomFoundingOwnerKind.None &&
				IsNonce(Authority.TransactionID) && IsNonce(Authority.OwnerNonce) &&
				Bounded(Authority.RealmFaction, 256) && Bounded(Authority.ZoneID, 512) &&
				Authority.RiteX >= 0 && Authority.RiteX <= 255 &&
				Authority.RiteY >= 0 && Authority.RiteY <= 255 &&
				IsLowerHex(Authority.PayloadDigest, 64);
		}

		public static string PayloadDigest(KingdomFoundingKind Kind, string Name,
			string Vocation, string VillageFaction, string VillageDisplay,
			int OriginalVolume, int OriginalMax, int CommittedVolume, int CommittedMax,
			string OriginalComponents, string CommittedComponents)
		{
			if (!IsKnownKind(Kind) || Kind == KingdomFoundingKind.None)
			{
				return null;
			}
			StringBuilder payload = new StringBuilder();
			AppendDigestField(payload, ((int)Kind).ToString());
			AppendDigestField(payload, Name);
			AppendDigestField(payload, Vocation);
			AppendDigestField(payload, VillageFaction);
			AppendDigestField(payload, VillageDisplay);
			AppendDigestField(payload, OriginalVolume.ToString());
			AppendDigestField(payload, OriginalMax.ToString());
			AppendDigestField(payload, CommittedVolume.ToString());
			AppendDigestField(payload, CommittedMax.ToString());
			AppendDigestField(payload, OriginalComponents);
			AppendDigestField(payload, CommittedComponents);
			try
			{
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()));
					StringBuilder hex = new StringBuilder(64);
					for (int i = 0; i < digest.Length; i++)
					{
						hex.Append(digest[i].ToString("x2"));
					}
					return hex.ToString();
				}
			}
			catch
			{
				return null;
			}
		}

		public static bool TryDecodeComponents(string Encoded,
			out Dictionary<string, int> Components)
		{
			Components = new Dictionary<string, int>(StringComparer.Ordinal);
			if (Encoded == null || Encoded.Length > MaximumComponentEncodingLength)
			{
				return false;
			}
			if (Encoded.Length == 0)
			{
				return true;
			}
			string[] rows = Encoded.Split(';');
			if (rows.Length > MaximumComponentCount)
			{
				return false;
			}
			string previous = null;
			foreach (string row in rows)
			{
				int split = row.LastIndexOf(':');
				string amountText = split >= 0 && split < row.Length - 1
					? row.Substring(split + 1) : null;
				if (split <= 0 || split == row.Length - 1 ||
					!int.TryParse(amountText, out var amount) ||
					amount.ToString() != amountText ||
					amount <= 0 || amount > 1000 ||
					!TryDecodeField(row.Substring(0, split), MaximumComponentNameLength,
						out var key) || string.IsNullOrEmpty(key) ||
					Components.ContainsKey(key) ||
					(previous != null && string.CompareOrdinal(previous, key) >= 0))
				{
					Components.Clear();
					return false;
				}
				Components.Add(key, amount);
				previous = key;
			}
			return true;
		}

		public static bool ComponentsDescribePureWater(Dictionary<string, int> Components,
			int Volume)
		{
			if (Components == null || Volume < 0)
			{
				return false;
			}
			if (Volume == 0)
			{
				return Components.Count == 0;
			}
			return Components.Count == 1 && Components.TryGetValue("water", out var water) &&
				water == 1000;
		}

		public static bool IsNonce(string Value)
		{
			return IsLowerHex(Value, 32);
		}

		public static bool IsLowerHex(string Value, int Length)
		{
			if (Value == null || Value.Length != Length)
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
				{
					return false;
				}
			}
			return true;
		}

		private static bool Bounded(string Value, int Maximum)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= Maximum;
		}

		private static string EncodeField(string Value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static bool TryDecodeField(string Encoded, int Maximum,
			out string Value)
		{
			Value = null;
			if (Encoded == null || Encoded.Length > Maximum * 4 + 8)
			{
				return false;
			}
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded);
				Value = new UTF8Encoding(false, true).GetString(bytes);
				return Value.Length <= Maximum && EncodeField(Value) == Encoded;
			}
			catch
			{
				Value = null;
				return false;
			}
		}

		private static void AppendDigestField(StringBuilder Builder, string Value)
		{
			string value = Value ?? "";
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			Builder.Append(bytes.Length).Append(':').Append(value).Append(';');
		}

		/// <summary>
		/// Before publication, every changed fact is compensable and the same-vessel snapshot must
		/// be restored. Once publication crossed an irreversible engine boundary, retaining the
		/// exact paid receipt for an idempotent retry is the only honest failure result.
		/// </summary>
		public static KingdomFoundingOutcome FailureOutcome(bool PublicationCommitted,
			bool WaterChanged, bool RestorationExact)
		{
			if (PublicationCommitted)
			{
				return KingdomFoundingOutcome.RecoverableFailure;
			}
			if (!WaterChanged)
			{
				return KingdomFoundingOutcome.Refused;
			}
			return RestorationExact
				? KingdomFoundingOutcome.CompensatedFailure
				: KingdomFoundingOutcome.RecoverableFailure;
		}

		public static KingdomFoundingWaterDisposition WaterDisposition(
			KingdomFoundingOutcome Outcome, bool RestorationExact)
		{
			switch (Outcome)
			{
			case KingdomFoundingOutcome.Refused:
				return KingdomFoundingWaterDisposition.Untouched;
			case KingdomFoundingOutcome.CompensatedFailure:
				return RestorationExact
					? KingdomFoundingWaterDisposition.RestoredExactly
					: KingdomFoundingWaterDisposition.RestorationFailed;
			case KingdomFoundingOutcome.RecoverableFailure:
				return RestorationExact
					? KingdomFoundingWaterDisposition.HeldForRecovery
					: KingdomFoundingWaterDisposition.RestorationFailed;
			case KingdomFoundingOutcome.Committed:
				return KingdomFoundingWaterDisposition.Spent;
			default:
				return KingdomFoundingWaterDisposition.RestorationFailed;
			}
		}

		public static bool ChargesEnergy(KingdomFoundingOutcome Outcome)
		{
			return Outcome == KingdomFoundingOutcome.Committed;
		}

		public static bool RequestsInventoryExit(KingdomFoundingOutcome Outcome)
		{
			return Outcome == KingdomFoundingOutcome.Committed;
		}

		/// <summary>Every projection through <paramref name="Through"/> must have succeeded.</summary>
		public static bool ProjectionSequenceComplete(bool[] Succeeded,
			KingdomFoundingProjection Through)
		{
			int last = (int)Through;
			if (Succeeded == null || last < (int)KingdomFoundingProjection.Water ||
				Succeeded.Length <= last)
			{
				return false;
			}
			for (int i = (int)KingdomFoundingProjection.Water; i <= last; i++)
			{
				if (!Succeeded[i])
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Checked integer subtraction used by the live same-vessel receipt.</summary>
		public static bool TryCommittedVolume(int OriginalVolume, int Cost,
			out int CommittedVolume)
		{
			CommittedVolume = OriginalVolume;
			if (OriginalVolume < 0 || Cost <= 0 || OriginalVolume < Cost)
			{
				return false;
			}
			CommittedVolume = OriginalVolume - Cost;
			return true;
		}
	}

	/// <summary>Value returned by every live founding operation.</summary>
	public struct KingdomFoundingResult
	{
		public KingdomFoundingOutcome Outcome;
		public KingdomFoundingWaterDisposition Water;
		public KingdomFoundingProjection Projection;
		public string Failure;

		public bool Committed => Outcome == KingdomFoundingOutcome.Committed;

		public bool ChargesEnergy => KingdomFoundingTransactionRules.ChargesEnergy(Outcome);

		public bool RequestsInventoryExit =>
			KingdomFoundingTransactionRules.RequestsInventoryExit(Outcome);

		public static KingdomFoundingResult From(KingdomFoundingOutcome Outcome,
			KingdomFoundingWaterDisposition Water, KingdomFoundingProjection Projection,
			string Failure = null)
		{
			return new KingdomFoundingResult
			{
				Outcome = Outcome,
				Water = Water,
				Projection = Projection,
				Failure = Failure ?? ""
			};
		}
	}
}
