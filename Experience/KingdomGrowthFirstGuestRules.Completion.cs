using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		internal static bool GrowthFirstGuestBodyReleaseReady(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate)
		{
			KingdomGrowthFirstGuestOpportunity x = Candidate?.FirstGuest;
			return x != null && ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				&& x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
				&& x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Reserved
				&& GrowthFirstGuestBodyReleasePhaseShape(Book, Candidate, Candidate.Phase);
		}

		internal static bool GrowthFirstGuestBodyLeaseRecoveryRequired(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate)
		{
			return Candidate?.FirstGuest?.BodyLeaseState ==
				KingdomGrowthFirstGuestBodyLeaseState.Reserved
				&& !GrowthFirstGuestBodyReleaseReady(Book, Candidate);
		}

		public static bool TryBindDeclinedFirstGuestOperation(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			KingdomGrowthOperation op = Book?.ArrivalOp;
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Declined
				|| Candidate.FirstGuest?.ChoiceState != KingdomGrowthFirstGuestChoiceState.Declined
				|| op == null || op.Phase != KingdomGrowthPhase.Prepared
				|| op.ArrivalDisposition != KingdomGrowthArrivalDisposition.Declined
				|| op.ArrivalCandidateId != Candidate.Id || Tick < Candidate.UpdatedTick
				|| !ExactGrowthOperationAuthority(Book, op)) return false;
			KingdomLifecycleResourceLease[] leases = { Candidate.CandidateLease,
				Candidate.LodgingLease, Candidate.EscrowLease };
			KingdomLifecycleResourceRevision[] rows = new KingdomLifecycleResourceRevision[3];
			for (int i = 0; i < leases.Length; i++)
			{
				rows[i] = FindGrowthResource(Book, leases[i].Key);
				if (!GrowthResourceMatches(rows[i], leases[i])
					|| rows[i].Revision != leases[i].BeforeRevision
					|| rows[i].ActiveOperationId != Candidate.Id
					|| leases[i].State != KingdomLifecycleLeaseState.Prepared) return false;
			}
			long[] revisions = new long[3]; string[] last = new string[3];
			for (int i = 0; i < rows.Length; i++)
			{
				revisions[i] = rows[i].Revision; last[i] = rows[i].LastOperationId;
				rows[i].Revision = leases[i].AfterRevision;
				rows[i].LastOperationId = Candidate.Id;
				leases[i].State = KingdomLifecycleLeaseState.Proved;
			}
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			string oldOp = Candidate.ConsumingOperationId; long oldSequence =
				Candidate.ConsumingOperationSequence; long oldTick = Candidate.UpdatedTick;
			Candidate.ConsumingOperationId = op.Id;
			Candidate.ConsumingOperationSequence = op.Sequence;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Settled;
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				&& ExactGrowthOperationAuthority(Book, op)) return true;
			Candidate.Phase = oldPhase; Candidate.ConsumingOperationId = oldOp;
			Candidate.ConsumingOperationSequence = oldSequence; Candidate.UpdatedTick = oldTick;
			for (int i = rows.Length - 1; i >= 0; i--)
			{
				leases[i].State = KingdomLifecycleLeaseState.Prepared;
				rows[i].Revision = revisions[i]; rows[i].LastOperationId = last[i];
			}
			return false;
		}

		public static bool TryBindDepartedFirstGuestOperation(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			KingdomGrowthOperation op = Book?.ArrivalOp;
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.GuestTerminal
				|| Candidate.FirstGuest?.GuestPhase != KingdomGrowthFirstGuestGuestPhase.Terminal
				|| Candidate.Disposition != KingdomGrowthArrivalDisposition.Departed
				|| op == null || op.Phase != KingdomGrowthPhase.Prepared
				|| op.ArrivalDisposition != KingdomGrowthArrivalDisposition.Departed
				|| op.ArrivalCandidateId != Candidate.Id || Tick < Candidate.UpdatedTick
				|| !ExactGrowthOperationAuthority(Book, op)) return false;
			KingdomLifecycleResourceRevision candidateRow = FindGrowthResource(Book,
				Candidate.CandidateLease.Key);
			if (!GrowthLeaseProvedByCandidateRow(candidateRow, Candidate.CandidateLease,
				Candidate.Id)) return false;
			KingdomLifecycleResourceLease[] leases = { Candidate.LodgingLease,
				Candidate.EscrowLease };
			KingdomLifecycleResourceRevision[] rows = new KingdomLifecycleResourceRevision[2];
			for (int i = 0; i < rows.Length; i++)
			{
				rows[i] = FindGrowthResource(Book, leases[i].Key);
				if (!GrowthResourceMatches(rows[i], leases[i])
					|| rows[i].Revision != leases[i].BeforeRevision
					|| rows[i].ActiveOperationId != Candidate.Id
					|| leases[i].State != KingdomLifecycleLeaseState.Prepared) return false;
			}
			string[] oldLast = new string[2]; long oldTick = Candidate.UpdatedTick;
			for (int i = 0; i < rows.Length; i++)
			{
				oldLast[i] = rows[i].LastOperationId;
				rows[i].Revision = leases[i].AfterRevision;
				rows[i].LastOperationId = Candidate.Id;
				leases[i].State = KingdomLifecycleLeaseState.Proved;
			}
			Candidate.ConsumingOperationId = op.Id;
			Candidate.ConsumingOperationSequence = op.Sequence;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Settled;
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				&& ExactGrowthOperationAuthority(Book, op)) return true;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.GuestTerminal;
			Candidate.ConsumingOperationId = null;
			Candidate.ConsumingOperationSequence = 0L;
			Candidate.UpdatedTick = oldTick;
			for (int i = rows.Length - 1; i >= 0; i--)
			{
				leases[i].State = KingdomLifecycleLeaseState.Prepared;
				rows[i].Revision = leases[i].BeforeRevision;
				rows[i].LastOperationId = oldLast[i];
			}
			return false;
		}

		public static bool TryMarkGrowthFirstGuestBodyReleased(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ReservationId, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.FirstGuest?.ChoiceState != KingdomGrowthFirstGuestChoiceState.Admitted
				|| Candidate.FirstGuest.BodyReservationId != ReservationId
				|| Tick < Candidate.UpdatedTick) return false;
			KingdomGrowthFirstGuestOpportunity x = Candidate.FirstGuest;
			if (x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Released) return true;
			if (x.BodyLeaseState != KingdomGrowthFirstGuestBodyLeaseState.Reserved) return false;
			long oldTick = Candidate.UpdatedTick;
			x.BodyLeaseState = KingdomGrowthFirstGuestBodyLeaseState.Released;
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			x.BodyLeaseState = KingdomGrowthFirstGuestBodyLeaseState.Reserved;
			Candidate.UpdatedTick = oldTick; return false;
		}

		public static bool TryPublishGrowthFirstGuestTerminal(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, KingdomGrowthOperation Operation,
			int ResidentId, long Tick)
		{
			KingdomGrowthFirstGuestOpportunity x = Candidate?.FirstGuest;
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate) || x == null
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Settled
				|| Operation == null || Operation.Phase != KingdomGrowthPhase.Terminal
				|| !ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.ArrivalCandidateId != Candidate.Id
				|| Operation.ArrivalDisposition != Candidate.Disposition
				|| !GrowthOutboxTerminal(Operation) || Operation.OutboxEvents.Count == 0)
				return false;
			if (Book.FirstGuestTerminal != null)
				return SameTerminalSource(Book.FirstGuestTerminal, Candidate, Operation,
					ResidentId);
			bool joined = Candidate.Disposition == KingdomGrowthArrivalDisposition.Joined;
			if (joined ? ResidentId <= 0 || string.IsNullOrEmpty(Candidate.ObjectId)
				: ResidentId != 0) return false;
			KingdomGrowthOutboxEvent tail =
				Operation.OutboxEvents[Operation.OutboxEvents.Count - 1];
			string receiptId = KingdomGrowthFirstGuestIdentityRules.TerminalReceiptId(
				Candidate.Id, x.DecisionReceiptId, Operation.Id,
				Candidate.Disposition, Tick);
			if (receiptId == null) return false;
			KingdomGrowthFirstGuestOpportunity terminalOpportunity = CopyFirstGuest(x);
			if (!CompleteFirstGuestTerminalOpportunity(terminalOpportunity,
				Candidate.Disposition, Tick)) return false;
			KingdomGrowthFirstGuestTerminalReceipt terminal =
				new KingdomGrowthFirstGuestTerminalReceipt
				{
					ReceiptId = receiptId, SettlementId = Candidate.SettlementId,
					CandidateId = Candidate.Id, CandidateObjectId = Candidate.ObjectId,
					Blueprint = Candidate.Blueprint, PersonName = Candidate.PlannedName,
					PersonOrigin = Candidate.PlannedOrigin, PersonCreed = Candidate.PlannedCreed,
					ResidentId = ResidentId, Result = Candidate.Disposition,
					ArrivalOperationId = Operation.Id,
					ArrivalOutboxEventId = tail.EventId, TerminalTick = Tick,
					Opportunity = terminalOpportunity
				};
			Book.FirstGuestTerminal = terminal;
			if (ValidGrowthFirstGuestTerminal(Book, terminal)
				&& CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.FirstGuestTerminal = null; return false;
		}

		public static bool ValidGrowthFirstGuestTerminal(KingdomGrowthBook Book,
			KingdomGrowthFirstGuestTerminalReceipt x)
		{
			if (x == null) return true;
			KingdomGrowthFirstGuestOpportunity o = x.Opportunity;
			if (Book == null || x.Version != KingdomGrowthFirstGuestTerminalReceipt.CurrentVersion
				&& x.Version != KingdomGrowthFirstGuestTerminalReceipt.LegacyVersion
				|| x.Version == KingdomGrowthFirstGuestTerminalReceipt.LegacyVersion
					&& o?.RulesVersion != 1
				|| x.SettlementId != Book.SettlementId || !ValidGeneratedId(x.CandidateId)
				|| !ValidGeneratedId(x.ArrivalOperationId)
				|| !ValidGeneratedId(x.ArrivalOutboxEventId)
				|| !ValidName(x.Blueprint) || !ValidName(x.PersonName)
				|| !ValidName(x.PersonOrigin) || !ValidName(x.PersonCreed)
				|| x.TerminalTick < 0L || !TerminalOpportunity(x.SettlementId, o, x.Result)) return false;
			bool joined = x.Result == KingdomGrowthArrivalDisposition.Joined;
			if (x.Result != KingdomGrowthArrivalDisposition.Joined
				&& x.Result != KingdomGrowthArrivalDisposition.NoAcceptableHome
				&& x.Result != KingdomGrowthArrivalDisposition.Declined
				&& x.Result != KingdomGrowthArrivalDisposition.Departed) return false;
			if (joined)
			{
				if (x.ResidentId <= 0 || !ValidRootId(x.CandidateObjectId)) return false;
			}
			else if (x.ResidentId != 0 || (x.Result == KingdomGrowthArrivalDisposition.Declined
				? x.CandidateObjectId != null : !ValidRootId(x.CandidateObjectId))) return false;
			return x.TerminalTick >= o.DecisionTick
				&& x.ReceiptId == KingdomGrowthFirstGuestIdentityRules.TerminalReceiptId(
					x.CandidateId, o.DecisionReceiptId, x.ArrivalOperationId,
					x.Result, x.TerminalTick);
		}

		internal static KingdomGrowthFirstGuestOpportunity CopyFirstGuest(
			KingdomGrowthFirstGuestOpportunity x)
		{
			if (x == null) return null;
			return new KingdomGrowthFirstGuestOpportunity
			{
				RulesVersion = x.RulesVersion, OpportunityId = x.OpportunityId,
				CauseId = x.CauseId, CauseTick = x.CauseTick, OfferedTick = x.OfferedTick,
				CadenceTicks = x.CadenceTicks, FactsState = x.FactsState,
				CohortSize = x.CohortSize, PopulationBefore = x.PopulationBefore,
				PopulationCap = x.PopulationCap, SupportedLevel = x.SupportedLevel,
				SupportCap = x.SupportCap, WaterAvailable = x.WaterAvailable,
				WaterRequired = x.WaterRequired, ChoiceState = x.ChoiceState,
				DeferredTick = x.DeferredTick, DeferredReceiptId = x.DeferredReceiptId,
				DecisionTick = x.DecisionTick, DecisionReceiptId = x.DecisionReceiptId,
				BodyReservationId = x.BodyReservationId, BodyRealmId = x.BodyRealmId,
				BodyOptionKind = x.BodyOptionKind, BodyEnableEpoch = x.BodyEnableEpoch,
				BodyReservedTick = x.BodyReservedTick, BodyLeaseState = x.BodyLeaseState,
				GuestPhase = x.GuestPhase, GuestTerminalState = x.GuestTerminalState,
				GuestActionTick = x.GuestActionTick,
				GuestActionReceiptId = x.GuestActionReceiptId,
				GuestTerminalTick = x.GuestTerminalTick,
				GuestTerminalReceiptId = x.GuestTerminalReceiptId
			};
		}

		private static bool TerminalOpportunity(string settlement,
			KingdomGrowthFirstGuestOpportunity x, KingdomGrowthArrivalDisposition result)
		{
			if (x == null || x.RulesVersion != 1 && x.RulesVersion != 2 || x.CohortSize != 1
				|| x.OpportunityId != GrowthFirstGuestOpportunityId(settlement, 1L)
				|| x.CauseId != GrowthFirstGuestCauseId(settlement, 1L,
					x.CauseTick, x.CadenceTicks) || x.CauseTick < 0L
				|| x.OfferedTick < x.CauseTick || x.CadenceTicks <= 0L
				|| x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Admitted
					&& x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Declined
				|| x.DecisionTick < x.OfferedTick
				|| x.DecisionReceiptId != GrowthFirstGuestReceiptId(x.OpportunityId,
					x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
						? "admit" : "decline", x.DecisionTick)) return false;
			if (x.RulesVersion == 1 && !GrowthFirstGuestPhysicalDefaults(x) || x.RulesVersion == 2
				&& !GrowthFirstGuestTerminalResultShape(x, result)) return false;
			return x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.None
				? GrowthFirstGuestNoBodyProof(x)
				: x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Released
					&& x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
					&& !string.IsNullOrEmpty(x.BodyReservationId)
					&& ValidRootId(x.BodyRealmId) && x.BodyEnableEpoch > 0L;
		}

		private static bool SameTerminalSource(KingdomGrowthFirstGuestTerminalReceipt t,
			KingdomGrowthArrivalCandidate c, KingdomGrowthOperation o, int residentId)
		{
			return t != null && t.SettlementId == c.SettlementId && t.CandidateId == c.Id
				&& t.CandidateObjectId == c.ObjectId && t.Blueprint == c.Blueprint
				&& t.PersonName == c.PlannedName && t.PersonOrigin == c.PlannedOrigin
				&& t.PersonCreed == c.PlannedCreed && t.ResidentId == residentId
				&& t.Result == c.Disposition && t.ArrivalOperationId == o.Id
				&& o.OutboxEvents.Count > 0 && t.ArrivalOutboxEventId ==
					o.OutboxEvents[o.OutboxEvents.Count - 1].EventId;
		}
	}
}
