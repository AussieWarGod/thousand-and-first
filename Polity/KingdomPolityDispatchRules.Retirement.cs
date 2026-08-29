using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDispatchRules
	{
		/// <summary>Terminalizes open intents and retains an exact receipt/digest seal.</summary>
		internal static bool TryRetire(KingdomPolityDispatchState State, long ExpectedRevision,
			string RealmId, string RetirementReceiptId, out string Failure)
		{
			Failure = null;
			if (!ValidState(State, out Failure) || State.RealmId != null
				&& State.RealmId != RealmId
				|| string.IsNullOrEmpty(RetirementReceiptId))
				return Fail(Failure ?? "polity dispatch retirement authority is invalid", out Failure);
			KingdomPolityDirectRecord existing = RetirementRecord(State);
			if (existing != null)
			{
				string digest = DirectAuthorityDigest(State);
				KingdomPolityDirectRecord expected = BuildRetirement(State,
					RetirementReceiptId, digest);
				return SameStoredRecord(existing, expected, true)
					|| Fail("polity dispatch was retired by another receipt", out Failure);
			}
			if (State.Revision == long.MaxValue)
				return Fail("polity dispatch revision is exhausted", out Failure);
			KingdomPolityDispatchState candidate = CloneState(State);
			candidate.RealmId = RealmId;
			if (!TerminalizeOpenIntents(candidate, out Failure)) return false;
			candidate.CompletedMask = candidate.HasWindow
				? (1 << candidate.EndpointCount) - 1 : 0;
			candidate.FutureCauseFloorTick = long.MaxValue;
			string authority = DirectAuthorityDigest(candidate);
			candidate.DirectRecords.Add(BuildRetirement(candidate, RetirementReceiptId, authority));
			SortRecords(candidate.DirectRecords); candidate.Revision++;
			return TryCommitState(State, candidate, ExpectedRevision, out Failure);
		}

		internal static bool ExactRetirementReceipt(KingdomPolityDispatchState State,
			string RealmId, string RetirementReceiptId, out string Failure)
		{
			Failure = null;
			if (!ValidState(State, out Failure) || State.RealmId != RealmId
				|| string.IsNullOrEmpty(RetirementReceiptId)) return false;
			KingdomPolityDirectRecord existing = RetirementRecord(State);
			if (existing == null) return Fail("polity dispatch has no retirement receipt", out Failure);
			return SameStoredRecord(existing, BuildRetirement(State, RetirementReceiptId,
				DirectAuthorityDigest(State)), true)
				|| Fail("polity dispatch was retired by another receipt", out Failure);
		}

		internal static KingdomPolityDirectRecord RetirementRecord(
			KingdomPolityDispatchState State)
		{
			for (int i = 0; i < (State?.DirectRecords?.Count ?? 0); i++)
				if (IsKind(State.DirectRecords[i], RetirementPrefix)) return State.DirectRecords[i];
			return null;
		}

		private static bool ExactRetirementSeal(KingdomPolityDispatchState State,
			KingdomPolityDirectRecord Seal)
		{
			const string start = "retirement receipt=";
			const string middle = "; authority=";
			if (State == null || Seal == null || State.RealmId == null
				|| State.FutureCauseFloorTick != long.MaxValue
				|| State.HasWindow && State.CompletedMask != (1 << State.EndpointCount) - 1
				|| Seal.EndpointVerb == null || !Seal.EndpointVerb.StartsWith(start,
					System.StringComparison.Ordinal)) return false;
			int boundary = Seal.EndpointVerb.IndexOf(middle, start.Length,
				System.StringComparison.Ordinal);
			if (boundary <= start.Length) return false;
			string receipt = Seal.EndpointVerb.Substring(start.Length, boundary - start.Length);
			return SameStoredRecord(Seal, BuildRetirement(State, receipt,
				DirectAuthorityDigest(State)), true);
		}

		private static KingdomPolityDirectRecord BuildRetirement(KingdomPolityDispatchState State,
			string ReceiptId, string AuthorityDigest)
		{
			string source = KingdomPolityRules.ActivationId(
				"taf:event:polity-dispatch-retirement:v1:", "polity-dispatch-retirement-v1",
				State.RealmId, ReceiptId, AuthorityDigest);
			KingdomPolityDirectRecord row = new KingdomPolityDirectRecord
			{
				SourceRef = source, Purpose = KingdomPolityCohortPurpose.Guard,
				WindowOrdinal = State.HasWindow ? State.LastWindowOrdinal : 0UL,
				CauseTick = State.HasWindow ? State.WindowCauseTick : 0L,
				EndpointVerb = "retirement receipt=" + ReceiptId + "; authority="
					+ AuthorityDigest + "; detailed=" + DetailedAuthorityCount(State).ToString(
						CultureInfo.InvariantCulture)
			};
			row.RecordId = StoredId(RetirementPrefix,
				"polity-direct-retirement-v1", row); return row;
		}

		private static int DetailedAuthorityCount(KingdomPolityDispatchState State)
		{
			int count = 0;
			for (int i = 0; i < (State?.DirectRecords?.Count ?? 0); i++)
				if (IsKind(State.DirectRecords[i], DirectPrefix)) count++;
			return count;
		}
	}
}
