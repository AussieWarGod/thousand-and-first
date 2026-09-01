namespace ThousandAndFirst
{
	/// <summary>Pure crash-cut grammar shared by authored-layout receipt writers.</summary>
	public enum ArchitectureOutputPrefix
	{
		Malformed = 0,
		Empty = 1,
		IdOnly = 2,
		StateOnly = 3,
		Published = 4,
		Settled = 5
	}

	public enum ArchitectureUpgradeFaultEvidence
	{
		None = 0,
		Message = 1,
		EmptyString = 2,
		Integer = 3,
		Collision = 4
	}

	public static class KingdomArchitectureReceiptPrefixRules
	{
		public static bool ExactInt(bool HasInt, int Observed,
			bool HasString, int Expected)
		{
			return HasInt && !HasString && Observed == Expected;
		}

		public static bool ExactString(bool HasString, string Observed,
			bool HasInt, string Expected)
		{
			return HasString && !HasInt && Observed == Expected;
		}

		public static bool ExactOptionalInt(bool HasInt, int Observed,
			bool HasString, int Expected)
		{
			return !HasString && (!HasInt || Observed == Expected);
		}

		public static bool ExactOptionalString(bool HasString, string Observed,
			bool HasInt, string Expected)
		{
			return !HasInt && (Expected == null
				? !HasString : HasString && Observed == Expected);
		}

		public static ArchitectureUpgradeFaultEvidence ClassifyUpgradeFault(
			bool HasString, string Observed, bool HasInt)
		{
			if (HasString && HasInt) return ArchitectureUpgradeFaultEvidence.Collision;
			if (HasInt) return ArchitectureUpgradeFaultEvidence.Integer;
			if (!HasString) return ArchitectureUpgradeFaultEvidence.None;
			return string.IsNullOrWhiteSpace(Observed)
				? ArchitectureUpgradeFaultEvidence.EmptyString
				: ArchitectureUpgradeFaultEvidence.Message;
		}

		public static bool ExactOrAbsentInt(bool HasInt, int Observed,
			bool HasString, int Expected)
		{
			return !HasString && (!HasInt || Observed == Expected);
		}

		public static bool ExactOrAbsentString(bool HasString, string Observed,
			bool HasInt, string Expected)
		{
			return !HasInt && (!HasString || Observed == Expected);
		}

		public static bool OldOrNewInt(bool HasInt, int Observed, bool HasString,
			int Old, int Next)
		{
			return HasInt && !HasString && (Observed == Old || Observed == Next);
		}

		public static bool OldOrNewString(bool HasString, string Observed, bool HasInt,
			string Old, string Next)
		{
			if (HasInt) return false;
			string value = HasString ? Observed : null;
			return (value ?? "") == (Old ?? "") || (value ?? "") == (Next ?? "");
		}

		/// <summary>
		/// Classifies one ID/state pair. ExpectedId may be null when any non-empty bounded ID is
		/// admissible at this layer; the engine boundary performs that length check separately.
		/// </summary>
		public static ArchitectureOutputPrefix ClassifyOutput(bool HasState, int State,
			bool HasStringState, bool HasId, string Id, bool HasIntId, string ExpectedId)
		{
			if (HasStringState || HasIntId || (HasId && string.IsNullOrEmpty(Id)))
				return ArchitectureOutputPrefix.Malformed;
			int state = HasState ? State : 0;
			if (state < 0 || state > 2
				|| (HasId && ExpectedId != null && Id != ExpectedId))
				return ArchitectureOutputPrefix.Malformed;
			if (state == 0)
				return HasId ? ArchitectureOutputPrefix.IdOnly
					: ArchitectureOutputPrefix.Empty;
			if (state == 1)
				return HasId ? ArchitectureOutputPrefix.Published
					: ArchitectureOutputPrefix.StateOnly;
			return HasId ? ArchitectureOutputPrefix.Settled
				: ArchitectureOutputPrefix.Malformed;
		}

		/// <summary>Legal successor publication prefixes at each predecessor retain phase.</summary>
		public static bool LegalRetainedTarget(int OwnerState,
			ArchitectureOutputPrefix Target)
		{
			if (OwnerState == 0)
				return Target == ArchitectureOutputPrefix.Empty
					|| Target == ArchitectureOutputPrefix.StateOnly
					|| Target == ArchitectureOutputPrefix.Published;
			if (OwnerState == 1)
				return Target == ArchitectureOutputPrefix.Published
					|| Target == ArchitectureOutputPrefix.Settled;
			return OwnerState == 2 && Target == ArchitectureOutputPrefix.Settled;
		}
	}
}
