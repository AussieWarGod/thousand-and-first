using System;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityAmbientTransactionRules
	{
		public static bool TryRecordTerminal(KingdomPolityLedger Ledger, long ExpectedRevision,
			string CohortId, KingdomPolityAmbientTerminalChoice Choice, long Tick,
			KingdomPolityAdmissionHandoff Handoff, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L)
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			if (cohort?.AmbientTransaction == null || !Valid(cohort.AmbientTransaction,
				CohortId, out Failure)) return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "ambient cohort has no resolved transaction", out Failure);
			if (cohort.AmbientTransaction.TerminalChoice !=
				KingdomPolityAmbientTerminalChoice.None)
			{
				if (cohort.AmbientTransaction.TerminalChoice == Choice &&
					cohort.AmbientTransaction.TerminalTick == Tick)
				{
					Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
					Result.CommittedRevision = Ledger.Revision; return true;
				}
				return KingdomPolityAuthority.Refuse(Result,
					"ambient transaction already has another terminal receipt", out Failure);
			}
			if (cohort.Phase != KingdomPolityCohortPhase.Materialized ||
				string.IsNullOrEmpty(cohort.ManifestationReceiptId))
				return KingdomPolityAuthority.Refuse(Result,
					"only an exact materialized visit can be answered", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityCohortPlan changed = KingdomPolityAuthority.Cohort(candidate, CohortId);
			KingdomPolityAmbientTransaction terminal;
			if (!TryTerminalCopy(changed.AmbientTransaction, CohortId, Choice, Tick, Handoff,
				out terminal, out Failure)) return KingdomPolityAuthority.Refuse(Result, Failure,
					out Failure);
			changed.AmbientTransaction = terminal;
			changed.Phase = KingdomPolityCohortPhase.Concluded;
			changed.RewardEventId = terminal.TerminalReceiptId;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		internal static bool TryTerminalCopy(KingdomPolityAmbientTransaction Source,
			string CohortId, KingdomPolityAmbientTerminalChoice Choice, long Tick,
			KingdomPolityAdmissionHandoff Handoff,
			out KingdomPolityAmbientTransaction Terminal, out string Failure)
		{
			Terminal = null; Failure = null;
			if (Source == null || Source.TerminalChoice !=
				KingdomPolityAmbientTerminalChoice.None || Tick < Source.PreparedTick)
				return Fail("ambient transaction is not open at this tick", out Failure);
			KingdomPolityAmbientTransaction t = Copy(Source);
			t.TerminalChoice = Choice; t.TerminalTick = Tick;
			if (Handoff != null)
			{
				t.AdmissionHandoff = Copy(Handoff);
				t.AdmissionHandoff.Decision = Choice ==
					KingdomPolityAmbientTerminalChoice.PetitionAccepted
						? KingdomPolityAdmissionDecision.Accepted
						: KingdomPolityAdmissionDecision.Rejected;
				t.AdmissionHandoff.DecidedTick = Tick;
			}
			t.TerminalReceiptId = KingdomPolityRules.ActivationId(
				"taf:ambient-receipt:v1:", "polity-ambient-terminal-v1", t.TransactionId,
				t.FrozenDigest, ((byte)t.TerminalChoice).ToString(CultureInfo.InvariantCulture),
				t.TerminalTick.ToString(CultureInfo.InvariantCulture),
				t.AdmissionHandoff?.HandoffId ?? "");
			if (!Valid(t, CohortId, out Failure))
				return false;
			Terminal = t; return true;
		}

		internal static KingdomPolityAmbientTransaction Copy(
			KingdomPolityAmbientTransaction S)
		{
			if (S == null) return null;
			return new KingdomPolityAmbientTransaction
			{
				Version = S.Version, TransactionId = S.TransactionId, Purpose = S.Purpose,
				SourcePolityId = S.SourcePolityId, SourceSettlementId = S.SourceSettlementId,
				SourceSettlementName = S.SourceSettlementName, SourceZoneId = S.SourceZoneId,
				DestinationSettlementId = S.DestinationSettlementId,
				DestinationSettlementName = S.DestinationSettlementName,
				DestinationZoneId = S.DestinationZoneId, LocalLocusRef = S.LocalLocusRef,
				FactRefs = new System.Collections.Generic.List<string>(S.FactRefs),
				SafeDetail = S.SafeDetail,
				ManifestRefs = new System.Collections.Generic.List<string>(S.ManifestRefs),
				PhysicalStockObjectIds = new System.Collections.Generic.List<string>(
					S.PhysicalStockObjectIds), NewsRef = S.NewsRef, PreparedTick = S.PreparedTick,
				FrozenDigest = S.FrozenDigest, TerminalChoice = S.TerminalChoice,
				TerminalTick = S.TerminalTick, TerminalReceiptId = S.TerminalReceiptId,
				AdmissionHandoff = Copy(S.AdmissionHandoff), Fault = S.Fault
			};
		}

		internal static bool Same(KingdomPolityAmbientTransaction A,
			KingdomPolityAmbientTransaction B)
		{
			if (A == null || B == null) return A == B;
			return A.Version == B.Version && A.TransactionId == B.TransactionId &&
				A.FrozenDigest == B.FrozenDigest && A.TerminalChoice == B.TerminalChoice &&
				A.TerminalTick == B.TerminalTick && A.TerminalReceiptId == B.TerminalReceiptId &&
				A.Fault == B.Fault && Same(A.AdmissionHandoff, B.AdmissionHandoff);
		}

		private static bool Same(KingdomPolityAdmissionHandoff A,
			KingdomPolityAdmissionHandoff B)
		{
			if (A == null || B == null) return A == B;
			return A.Version == B.Version && A.HandoffId == B.HandoffId &&
				A.RealmId == B.RealmId && A.PolityId == B.PolityId &&
				A.CohortId == B.CohortId && A.MemberId == B.MemberId &&
				A.TargetSettlementId == B.TargetSettlementId &&
				A.SourceObjectId == B.SourceObjectId && A.SourceZoneId == B.SourceZoneId &&
				A.ProposedResidentName == B.ProposedResidentName &&
				A.Decision == B.Decision && A.PreparedTick == B.PreparedTick &&
				A.DecidedTick == B.DecidedTick && A.CauseDigest == B.CauseDigest &&
				A.Fault == B.Fault && KingdomPolityAdmissionReceiptRules.Same(
					A.AdmissionReceipt, B.AdmissionReceipt);
		}

		private static KingdomPolityAdmissionHandoff Copy(KingdomPolityAdmissionHandoff S)
		{
			if (S == null) return null;
			return new KingdomPolityAdmissionHandoff
			{
				Version = S.Version, HandoffId = S.HandoffId, RealmId = S.RealmId,
				PolityId = S.PolityId, CohortId = S.CohortId, MemberId = S.MemberId,
				TargetSettlementId = S.TargetSettlementId, SourceObjectId = S.SourceObjectId,
				SourceZoneId = S.SourceZoneId, ProposedResidentName = S.ProposedResidentName,
				Decision = S.Decision, PreparedTick = S.PreparedTick, DecidedTick = S.DecidedTick,
				CauseDigest = S.CauseDigest, AdmissionReceipt = S.AdmissionReceipt?.Copy(),
				Fault = S.Fault
			};
		}
	}
}
