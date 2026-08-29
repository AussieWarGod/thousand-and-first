using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementRules
	{
		public static bool TryPlan(string ReceiptId, string RealmId, string FactionId,
			string GameId, long RealmIncarnation, long Tick, string AuthorityDigest,
			IList<KingdomRemovalLocator> Locators,
			out KingdomRealmRetirementState Planned, out string Failure)
		{
			Planned = null;
			Failure = null;
			if (!LowerHex(ReceiptId, 32) || !Text(RealmId, false) || !Text(FactionId, false)
				|| !Text(GameId, false) || RealmIncarnation < 0L || Tick < 0L
				|| !Digest(AuthorityDigest) || Locators == null || Locators.Count == 0
				|| Locators.Count > KingdomRealmRetirementState.MaxLocators)
				return Fail("retirement plan inputs are malformed or unbounded", out Failure);
			List<KingdomRemovalLocator> rows = new List<KingdomRemovalLocator>();
			for (int i = 0; i < Locators.Count; i++)
			{
				KingdomRemovalLocator source = Locators[i];
				if (source == null || !Text(source.ZoneId, false)
					|| !Text(source.SettlementId, true))
					return Fail("retirement plan contains an invalid locator", out Failure);
				rows.Add(new KingdomRemovalLocator
				{
					ZoneId = source.ZoneId,
					SettlementId = source.SettlementId,
					State = KingdomRemovalLocatorState.OutstandingVisit
				});
			}
			rows.Sort((a, b) => string.CompareOrdinal(a.ZoneId, b.ZoneId));
			for (int i = 1; i < rows.Count; i++)
				if (rows[i - 1].ZoneId == rows[i].ZoneId)
					return Fail("retirement plan contains duplicate ground", out Failure);
			Planned = new KingdomRealmRetirementState
			{
				Phase = KingdomRealmRetirementPhase.Planning,
				Revision = 1,
				ReceiptId = ReceiptId,
				RealmId = RealmId,
				FactionId = FactionId,
				GameId = GameId,
				RealmIncarnation = RealmIncarnation,
				StartedTick = Tick,
				UpdatedTick = Tick,
				AuthorityDigest = AuthorityDigest,
				Fault = "",
				Locators = rows
			};
			return Valid(Planned, out Failure);
		}

		public static bool TrySetPhase(KingdomRealmRetirementState Current,
			int ExpectedRevision, KingdomRealmRetirementPhase Expected,
			KingdomRealmRetirementPhase Next, long Tick,
			out KingdomRealmRetirementState Updated, out string Failure)
		{
			Updated = null;
			if (!Valid(Current, out Failure) || Current.Revision != ExpectedRevision
				|| Current.Phase != Expected || Tick < Current.UpdatedTick
				|| !Allowed(Expected, Next))
				return Fail(Failure ?? "retirement phase CAS or transition refused", out Failure);
			if ((Next == KingdomRealmRetirementPhase.ReadyForFence
				|| Next == KingdomRealmRetirementPhase.FenceCommitted
				|| Next == KingdomRealmRetirementPhase.PreparedForRemoval)
				&& !CanCommitFence(Current, out Failure)) return false;
			if ((Next == KingdomRealmRetirementPhase.FenceCommitted
				|| Next == KingdomRealmRetirementPhase.PreparedForRemoval)
				&& !HasFenceCommitRecord(Current))
				return Fail("base identity fence commit is not receipted", out Failure);
			Updated = Current.Clone();
			Updated.Phase = Next;
			Updated.Revision++;
			Updated.UpdatedTick = Tick;
			return Valid(Updated, out Failure);
		}

		public static bool TryRecord(KingdomRealmRetirementState Current,
			int ExpectedRevision, KingdomRemovalRecord Record, long Tick,
			out KingdomRealmRetirementState Updated, out string Failure)
		{
			Updated = null;
			if (!Valid(Current, out Failure) || Record == null || !Text(Record.Id, false)
				|| !Enum.IsDefined(typeof(KingdomRemovalProjectionKind), Record.Kind)
				|| !Enum.IsDefined(typeof(KingdomRemovalDisposition), Record.Disposition)
				|| !OptionalDigest(Record.BeforeDigest) || !OptionalDigest(Record.AfterDigest)
				|| !Detail(Record.Detail ?? "") || Tick < Current.UpdatedTick)
				return Fail(Failure ?? "retirement projection record is invalid", out Failure);
			string key = RecordKey(Record);
			for (int i = 0; i < Current.Records.Count; i++)
			{
				if (RecordKey(Current.Records[i]) != key) continue;
				if (Exact(Current.Records[i], Record))
				{
					Updated = Current.Clone();
					return true;
				}
				return Fail("retirement projection identity reached a third state", out Failure);
			}
			bool fenceCommit = Current.Phase == KingdomRealmRetirementPhase.ReadyForFence
				&& Record.Kind == KingdomRemovalProjectionKind.GlobalState
				&& Record.Id == FenceRecordId
				&& Record.Disposition == KingdomRemovalDisposition.Preserved
				&& Digest(Record.BeforeDigest) && Digest(Record.AfterDigest)
				&& Record.BeforeDigest != Record.AfterDigest;
			if ((!MutableRecordPhase(Current.Phase) && !fenceCommit)
				|| Current.Revision != ExpectedRevision
				|| Current.Records.Count >= KingdomRealmRetirementState.MaxRecords)
				return Fail("retirement projection CAS or capacity refused", out Failure);
			Updated = Current.Clone();
			Updated.Records.Add(Record.Clone());
			Updated.Records.Sort((a, b) => string.CompareOrdinal(RecordKey(a), RecordKey(b)));
			Updated.Revision++;
			Updated.UpdatedTick = Tick;
			return Valid(Updated, out Failure);
		}

		public static bool TryMarkGround(KingdomRealmRetirementState Current,
			int ExpectedRevision, string ZoneId, KingdomRemovalLocatorState State,
			long Tick, int ObjectCount, string EvidenceDigest,
			out KingdomRealmRetirementState Updated, out string Failure)
		{
			Updated = null;
			if (!Valid(Current, out Failure) || !Text(ZoneId, false) || Tick < Current.UpdatedTick
				|| !Enum.IsDefined(typeof(KingdomRemovalLocatorState), State)
				|| ObjectCount < 0 || (State == KingdomRemovalLocatorState.Cleaned
					? !Digest(EvidenceDigest) : EvidenceDigest != null))
				return Fail(Failure ?? "retirement ground record is invalid", out Failure);
			int found = Current.Locators.FindIndex(row => row.ZoneId == ZoneId);
			if (found < 0) return Fail("ground is outside the frozen locator set", out Failure);
			KingdomRemovalLocator old = Current.Locators[found];
			if (old.State == State && old.ObjectCount == ObjectCount
				&& old.EvidenceDigest == EvidenceDigest)
			{
				Updated = Current.Clone();
				return true;
			}
			if (Current.Phase != KingdomRealmRetirementPhase.CleaningGround
				|| Current.Revision != ExpectedRevision
				|| !AllowedGround(old.State, State))
				return Fail("retirement ground CAS refused a rewrite", out Failure);
			Updated = Current.Clone();
			KingdomRemovalLocator next = Updated.Locators[found];
			next.State = State;
			next.ObjectCount = ObjectCount;
			next.EvidenceDigest = EvidenceDigest;
			next.Revision++;
			next.CleanedTick = State == KingdomRemovalLocatorState.Cleaned ? Tick : 0L;
			Updated.Revision++;
			Updated.UpdatedTick = Tick;
			return Valid(Updated, out Failure);
		}

		private static bool Allowed(KingdomRealmRetirementPhase From,
			KingdomRealmRetirementPhase To)
		{
			if (To == KingdomRealmRetirementPhase.Quarantined)
				return From == KingdomRealmRetirementPhase.Planning
					|| From == KingdomRealmRetirementPhase.Paused
					|| From == KingdomRealmRetirementPhase.CleaningGround
					|| From == KingdomRealmRetirementPhase.ReadyForFence;
			return (From == KingdomRealmRetirementPhase.Planning && To == KingdomRealmRetirementPhase.Paused)
				|| (From == KingdomRealmRetirementPhase.Paused && To == KingdomRealmRetirementPhase.CleaningGround)
				|| (From == KingdomRealmRetirementPhase.CleaningGround && To == KingdomRealmRetirementPhase.ReadyForFence)
				|| (From == KingdomRealmRetirementPhase.ReadyForFence && To == KingdomRealmRetirementPhase.FenceCommitted)
				|| (From == KingdomRealmRetirementPhase.FenceCommitted && To == KingdomRealmRetirementPhase.PreparedForRemoval);
		}

		private static bool MutableRecordPhase(KingdomRealmRetirementPhase Phase)
		{
			return Phase == KingdomRealmRetirementPhase.Planning
				|| Phase == KingdomRealmRetirementPhase.Paused
				|| Phase == KingdomRealmRetirementPhase.CleaningGround;
		}

		private static bool AllowedGround(KingdomRemovalLocatorState From,
			KingdomRemovalLocatorState To)
		{
			if (From == KingdomRemovalLocatorState.OutstandingVisit)
				return To == KingdomRemovalLocatorState.Cleaning
					|| To == KingdomRemovalLocatorState.Contested
					|| To == KingdomRemovalLocatorState.Diverged;
			if (From == KingdomRemovalLocatorState.Cleaning)
				return To == KingdomRemovalLocatorState.Cleaned
					|| To == KingdomRemovalLocatorState.Contested
					|| To == KingdomRemovalLocatorState.Diverged;
			if (From == KingdomRemovalLocatorState.Cleaned)
				return To == KingdomRemovalLocatorState.Cleaning;
			return (From == KingdomRemovalLocatorState.Contested
				|| From == KingdomRemovalLocatorState.Diverged)
				&& To == KingdomRemovalLocatorState.Cleaning;
		}

		private static bool Exact(KingdomRemovalRecord A, KingdomRemovalRecord B)
		{
			return A.Kind == B.Kind && A.Id == B.Id && A.Disposition == B.Disposition
				&& A.BeforeDigest == B.BeforeDigest && A.AfterDigest == B.AfterDigest
				&& A.Amount == B.Amount && (A.Detail ?? "") == (B.Detail ?? "");
		}
	}
}
