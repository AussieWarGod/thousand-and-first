using System;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomExternalOwnershipRules
	{
		public const int ContractVersion = 1;
		public const int MaximumEncodedLength = 2048;
		public const int MaximumTextLength = 512;
		private const string Prefix = "taf-external-owner-v1";

		public static bool ValidToken(string Value, int Maximum = MaximumTextLength)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > Maximum) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (char.IsControl(c) || char.IsSurrogate(c)) return false;
			}
			return true;
		}

		public static bool ValidGuid(string Value)
		{
			return Guid.TryParseExact(Value, "D", out Guid parsed)
				&& parsed != Guid.Empty
				&& string.Equals(parsed.ToString("D"), Value, StringComparison.Ordinal);
		}

		public static bool ValidObservation(KingdomExternalOwnershipObservation Value)
		{
			return Value != null
				&& ValidToken(Value.ProviderId, 64)
				&& ValidToken(Value.ProviderVersion, 32)
				&& ValidGuid(Value.OwnerGuid)
				&& (string.IsNullOrEmpty(Value.SectorGuid) || ValidGuid(Value.SectorGuid))
				&& ValidToken(Value.Evidence, 64)
				&& ValidToken(Value.ZoneId)
				&& ValidToken(Value.ParasangId);
		}

		public static bool ValidBinding(KingdomExternalOwnershipBinding Value)
		{
			if (Value == null) return false;
			if (Value.Mode == KingdomExternalOwnershipMode.None)
				return Value.Observation == null;
			return Value.Mode == KingdomExternalOwnershipMode.Bind
				&& ValidObservation(Value.Observation);
		}

		public static KingdomExternalOwnershipBinding None()
		{
			return new KingdomExternalOwnershipBinding
			{
				Mode = KingdomExternalOwnershipMode.None
			};
		}

		public static KingdomExternalOwnershipBinding Bind(
			KingdomExternalOwnershipObservation Observation)
		{
			return new KingdomExternalOwnershipBinding
			{
				Mode = KingdomExternalOwnershipMode.Bind,
				Observation = Observation?.Clone()
			};
		}

		public static bool SameObservation(KingdomExternalOwnershipObservation A,
			KingdomExternalOwnershipObservation B)
		{
			return ValidObservation(A) && ValidObservation(B)
				&& A.ProviderId == B.ProviderId
				&& A.ProviderVersion == B.ProviderVersion
				&& A.OwnerGuid == B.OwnerGuid
				&& (A.SectorGuid ?? "") == (B.SectorGuid ?? "")
				&& A.Evidence == B.Evidence
				&& A.ZoneId == B.ZoneId
				&& A.ParasangId == B.ParasangId;
		}

		/// <summary>CAS law for a two-property receipt. Each present half must be exact;
		/// an interrupted write may retain either one. Rollback can additionally require
		/// at least one staged half so an unrelated permanent row is never erased alone.</summary>
		public static bool PairAbsentOrExact(string CurrentAuthority, string CurrentBinding,
			string ExpectedAuthority, string ExpectedBinding, bool RequireEvidence)
		{
			if (string.IsNullOrEmpty(ExpectedAuthority)
				|| string.IsNullOrEmpty(ExpectedBinding)) return false;
			bool hasAuthority = !string.IsNullOrEmpty(CurrentAuthority);
			bool hasBinding = !string.IsNullOrEmpty(CurrentBinding);
			return (!RequireEvidence || hasAuthority || hasBinding)
				&& (!hasAuthority || CurrentAuthority == ExpectedAuthority)
				&& (!hasBinding || CurrentBinding == ExpectedBinding);
		}

		public static KingdomExternalBindingVerdict Judge(
			KingdomExternalOwnershipBinding Binding,
			KingdomExternalOwnershipReading Current)
		{
			if (!ValidBinding(Binding) || Current == null)
				return KingdomExternalBindingVerdict.Malformed;
			if (Current.State == KingdomExternalOwnershipState.ProviderFailed)
				return KingdomExternalBindingVerdict.ProviderUnavailable;
			if (Current.State == KingdomExternalOwnershipState.Conflicting)
				return KingdomExternalBindingVerdict.Diverged;
			if (Binding.Mode == KingdomExternalOwnershipMode.None)
				return Current.State == KingdomExternalOwnershipState.Unowned
					? KingdomExternalBindingVerdict.Open
					: KingdomExternalBindingVerdict.Diverged;
			if (Current.State == KingdomExternalOwnershipState.Unowned)
				return KingdomExternalBindingVerdict.Diverged;
			return SameObservation(Binding.Observation, Current.Observation)
				? KingdomExternalBindingVerdict.Exact
				: KingdomExternalBindingVerdict.Diverged;
		}
	}
}
