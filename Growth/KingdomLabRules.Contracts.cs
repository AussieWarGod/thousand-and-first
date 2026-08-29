using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLabRules
	{
		/// <summary>Stable effect identity. Registry display data is never included.</summary>
		internal static string EffectFingerprint(int Version, string Key, string Grants,
			int Source, int Attach, string Manager, string Detail = "")
		{
			ulong hash = 14695981039346656037UL;
			Fold(ref hash, Version.ToString(CultureInfo.InvariantCulture));
			Fold(ref hash, (Key ?? "").Trim().ToLowerInvariant());
			Fold(ref hash, Grants ?? "");
			Fold(ref hash, Source.ToString(CultureInfo.InvariantCulture));
			Fold(ref hash, Attach.ToString(CultureInfo.InvariantCulture));
			Fold(ref hash, Manager ?? "");
			Fold(ref hash, Detail ?? "");
			return hash.ToString("x16", CultureInfo.InvariantCulture);
		}

		internal static bool ValidEffectContract(int Version, string Key, string Grants,
			int Source, int Attach, string Manager, string Fingerprint, string Detail = "")
		{
			return Version == EffectContractVersion && Bounded(Key, 128) && Bounded(Grants, 256)
				&& Bounded(Manager, 256) && Bounded(Fingerprint, 32)
				&& Detail != null && Detail.Length <= MaxRegistryFieldChars
				&& Enum.IsDefined(typeof(LabSource), (LabSource)Source)
				&& Enum.IsDefined(typeof(LabAttach), (LabAttach)Attach)
				&& string.Equals(Fingerprint, EffectFingerprint(Version, Key, Grants, Source,
					Attach, Manager, Detail), StringComparison.Ordinal);
		}

		internal static string ExecutionStampFingerprint(string Stamp)
		{
			ulong hash = 14695981039346656037UL;
			Fold(ref hash, Stamp ?? "");
			return hash.ToString("x16", CultureInfo.InvariantCulture);
		}

		internal static KingdomVatOutputDecision VatOutputIdentity(bool FrozenId,
			bool Resolved, bool FingerprintMatches)
		{
			if (!FrozenId)
			{
				return KingdomVatOutputDecision.CreateAndFreeze;
			}
			if (!Resolved)
			{
				return KingdomVatOutputDecision.QuarantineMissing;
			}
			return FingerprintMatches ? KingdomVatOutputDecision.UseExact
				: KingdomVatOutputDecision.QuarantineMismatch;
		}

		internal static string VatOutputFingerprint(string JobId, string Blueprint, int Yield,
			string Stamp, string Source)
		{
			ulong hash = 14695981039346656037UL;
			Fold(ref hash, JobId ?? "");
			Fold(ref hash, Blueprint ?? "");
			Fold(ref hash, Yield.ToString(CultureInfo.InvariantCulture));
			Fold(ref hash, Stamp ?? "");
			Fold(ref hash, Source ?? "");
			return hash.ToString("x16", CultureInfo.InvariantCulture);
		}

		internal static string VatRawFingerprint(string JobId, string RawId, string Blueprint,
			int Count, string Stamp, string Source)
		{
			ulong hash = 14695981039346656037UL;
			Fold(ref hash, JobId ?? "");
			Fold(ref hash, RawId ?? "");
			Fold(ref hash, Blueprint ?? "");
			Fold(ref hash, Count.ToString(CultureInfo.InvariantCulture));
			Fold(ref hash, Stamp ?? "");
			Fold(ref hash, Source ?? "");
			return hash.ToString("x16", CultureInfo.InvariantCulture);
		}

		/// <summary>An interrupted external intent is evidence of uncertainty, never permission
		/// to invoke that callback a second time.</summary>
		internal static KingdomVatOutputPhase ResumeVatOutput(KingdomVatOutputPhase Phase,
			bool ExactOutputInVat)
		{
			if (Phase != KingdomVatOutputPhase.AddIntent) return Phase;
			return ExactOutputInVat ? KingdomVatOutputPhase.Added
				: KingdomVatOutputPhase.Quarantined;
		}

		internal static KingdomVatRawPhase ResumeVatRaw(KingdomVatRawPhase Phase,
			bool ExactRawPresent, bool ExactOutputInVat)
		{
			if (Phase != KingdomVatRawPhase.DestroyIntent) return Phase;
			return !ExactRawPresent && ExactOutputInVat ? KingdomVatRawPhase.Destroyed
				: KingdomVatRawPhase.Quarantined;
		}

		internal static int StandingAfter(int Before, int Delta)
		{
			long value = (long)Before + Delta;
			return value > int.MaxValue ? int.MaxValue
				: value < int.MinValue ? int.MinValue : (int)value;
		}

		internal static KingdomLabStandingPhase ObserveStanding(
			KingdomLabStandingPhase Phase, int Current, int Before, int After)
		{
			if (Phase == KingdomLabStandingPhase.Bound)
				return Current == Before ? Phase : KingdomLabStandingPhase.Quarantined;
			if (Phase == KingdomLabStandingPhase.Intent)
				return Current == After ? KingdomLabStandingPhase.Applied
					: KingdomLabStandingPhase.Quarantined;
			return Phase;
		}

		internal static KingdomLabMessagePhase ResumeMessage(KingdomLabMessagePhase Phase)
		{
			return Phase == KingdomLabMessagePhase.Intent
				? KingdomLabMessagePhase.Lost : Phase;
		}

		internal static bool MessageSettled(KingdomLabMessagePhase Phase)
		{
			return Phase == KingdomLabMessagePhase.Delivered
				|| Phase == KingdomLabMessagePhase.Skipped
				|| Phase == KingdomLabMessagePhase.Lost;
		}

		internal static bool RegistryAuthority(KingdomLabRegistryEntry Entry, string JobId,
			string BuildingId, string PatientId, string GameId, string RealmId,
			long RealmFoundedTick, string Fingerprint, bool RequireActive)
		{
			return ValidRegistryEntry(Entry)
				&& string.Equals(Entry.JobId, JobId, StringComparison.Ordinal)
				&& string.Equals(Entry.BuildingId, BuildingId, StringComparison.Ordinal)
				&& string.Equals(Entry.PatientId, PatientId, StringComparison.Ordinal)
				&& string.Equals(Entry.GameId, GameId, StringComparison.Ordinal)
				&& string.Equals(Entry.RealmId, RealmId, StringComparison.Ordinal)
				&& Entry.RealmFoundedTick == RealmFoundedTick
				&& string.Equals(Entry.Fingerprint, Fingerprint, StringComparison.Ordinal)
				&& (!RequireActive || Entry.Status == KingdomLabRegistryStatus.Active);
		}

		internal static bool RegistryAuthority(KingdomLabRegistryEntry Entry,
			KingdomLabRegistryEntry Expected, bool RequireActive)
		{
			return Expected != null && RegistryAuthority(Entry, Expected.JobId,
				Expected.BuildingId, Expected.PatientId, Expected.GameId, Expected.RealmId,
				Expected.RealmFoundedTick, Expected.Fingerprint, RequireActive)
				&& Entry.ContractVersion == Expected.ContractVersion
				&& Entry.RulerSuccessionOrdinal == Expected.RulerSuccessionOrdinal
				&& string.Equals(Entry.RulerLifeId, Expected.RulerLifeId,
					StringComparison.Ordinal)
				&& string.Equals(Entry.ProcedureKey, Expected.ProcedureKey, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(Entry.Grants, Expected.Grants, StringComparison.Ordinal)
				&& Entry.Source == Expected.Source && Entry.Attach == Expected.Attach
				&& string.Equals(Entry.Manager, Expected.Manager, StringComparison.Ordinal)
				&& string.Equals(Entry.Detail, Expected.Detail, StringComparison.Ordinal);
		}

	}
}
