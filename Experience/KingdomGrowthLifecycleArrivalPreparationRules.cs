using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		public static KingdomGrowthArrivalCandidate PrepareGrowthArrivalCandidate(
			KingdomGrowthBook Book, string Marker, string Blueprint, string EscrowKey, string ZoneId,
			long Tick, string BeforeOwnerGraphHash, string BeforeObjectGraphHash,
			string BeforeTopologyHash, int SemanticPlanVersion, string SemanticStreamId,
			uint SemanticEventKind, string PlannedOrigin, string PlannedCreed,
			string PlannedName, string PlannedArrived, int ArrivalX, int ArrivalY,
			bool LegacySemanticPlan = false)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Book.ArrivalCandidate != null || Book.ArrivalCandidateNextSequence == long.MaxValue
				|| Book.OptionState != KingdomLifecycleOptionState.Enabled
				|| Book.HealthState != KingdomGrowthHealthState.Healthy || Book.WorkPaused
				|| Tick < Book.OptionTick || Tick < Book.HealthTick || Tick < Book.ScarcityOptionTick
				|| !ValidRootId(Marker) || !ValidName(Blueprint) || !ValidRootId(EscrowKey)
				|| !ValidName(ZoneId)
				|| !GrowthWitnessHash(BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(BeforeObjectGraphHash)
				|| !GrowthWitnessHash(BeforeTopologyHash)) return null;
			long sequence = Book.ArrivalCandidateNextSequence;
			if (!IsExactSuccessor(sequence, Book.ArrivalCandidateRetiredThrough)) return null;
			string id = GrowthArrivalCandidateId(Book.SettlementId, sequence);
			string candidateKey = ResourceKey(KingdomLifecycleResourceKind.GrowthArrivalCandidate,
				Book.SettlementId, id);
			string lodgingSubject = ChildId(id, "lodging-lease", 0);
			string lodgingKey = ResourceKey(KingdomLifecycleResourceKind.GrowthArrivalCandidate,
				Book.SettlementId, lodgingSubject);
			string escrowLeaseKey = ResourceKey(KingdomLifecycleResourceKind.GrowthEscrowRelease,
				Book.SettlementId, EscrowKey);
			KingdomLifecycleResourceRevision candidateRow = FindGrowthResource(Book, candidateKey);
			KingdomLifecycleResourceRevision lodgingRow = FindGrowthResource(Book, lodgingKey);
			KingdomLifecycleResourceRevision escrowRow = FindGrowthResource(Book, escrowLeaseKey);
			if (id == null || candidateKey == null || lodgingKey == null || escrowLeaseKey == null
				|| candidateRow != null && (!string.IsNullOrEmpty(candidateRow.ActiveOperationId)
					|| candidateRow.Revision == long.MaxValue)
				|| lodgingRow != null && (!string.IsNullOrEmpty(lodgingRow.ActiveOperationId)
					|| lodgingRow.Revision == long.MaxValue)
				|| escrowRow != null && (!string.IsNullOrEmpty(escrowRow.ActiveOperationId)
					|| escrowRow.Revision == long.MaxValue)) return null;
			KingdomGrowthArrivalCandidate candidate = new KingdomGrowthArrivalCandidate
			{
				Sequence = sequence, Id = id, SettlementId = Book.SettlementId,
				CreatedTick = Tick, UpdatedTick = Tick,
				Phase = KingdomGrowthArrivalCandidatePhase.Prepared,
				ArrivalOpportunityOrdinal = Book.ArrivalOpportunity?.Ordinal ?? 0UL,
				ArrivalOpportunityDueTick = Book.ArrivalOpportunity?.DueTick ?? 0L,
				ArrivalOpportunityRateEpoch = Book.ArrivalOpportunity?.RateEpoch ?? 0L,
				ArrivalOpportunityPayloadHash = Book.ArrivalOpportunity?.PayloadHash,
				Marker = Marker, Blueprint = Blueprint, EscrowKey = EscrowKey,
				LodgingZoneId = ZoneId,
				LegacySemanticPlan = LegacySemanticPlan,
				SemanticPlanVersion = SemanticPlanVersion,
				SemanticStreamId = SemanticStreamId,
				SemanticEventKind = SemanticEventKind,
				PlannedOrigin = PlannedOrigin,
				PlannedCreed = PlannedCreed,
				PlannedName = PlannedName,
				PlannedArrived = PlannedArrived,
				ArrivalX = ArrivalX,
				ArrivalY = ArrivalY,
				CandidateLease = new KingdomLifecycleResourceLease
				{
					OperationId = id, Kind = KingdomLifecycleResourceKind.GrowthArrivalCandidate,
					ScopeId = Book.SettlementId, SubjectId = id, Key = candidateKey,
					Before = Book.ArrivalCandidateRetiredThrough, Delta = 1L, After = sequence,
					BeforeRevision = candidateRow == null ? 0L : candidateRow.Revision,
					AfterRevision = (candidateRow == null ? 0L : candidateRow.Revision) + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				},
				LodgingLease = new KingdomLifecycleResourceLease
				{
					OperationId = id, Kind = KingdomLifecycleResourceKind.GrowthArrivalCandidate,
					ScopeId = Book.SettlementId, SubjectId = lodgingSubject, Key = lodgingKey,
					Before = 0L, Delta = 1L, After = 1L,
					BeforeRevision = lodgingRow == null ? 0L : lodgingRow.Revision,
					AfterRevision = (lodgingRow == null ? 0L : lodgingRow.Revision) + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				},
				EscrowLease = new KingdomLifecycleResourceLease
				{
					OperationId = id, Kind = KingdomLifecycleResourceKind.GrowthEscrowRelease,
					ScopeId = Book.SettlementId, SubjectId = EscrowKey, Key = escrowLeaseKey,
					Before = 0L, Delta = 1L, After = 1L,
					BeforeRevision = escrowRow == null ? 0L : escrowRow.Revision,
					AfterRevision = (escrowRow == null ? 0L : escrowRow.Revision) + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				},
				CreateStep = new KingdomGrowthObjectCallbackStep
				{
					EventId = ChildId(id, "object-callback", 0),
					Kind = KingdomGrowthObjectMutationKind.Create,
					FromLocation = KingdomGrowthLocationKind.Absent,
					ToLocation = KingdomGrowthLocationKind.Escrow, EscrowKey = EscrowKey,
					BeforeX = -1, BeforeY = -1, AfterX = -1, AfterY = -1,
					BeforeCount = 0, AfterCount = 1, NoStack = true,
					BeforeOwnerGraphHash = BeforeOwnerGraphHash,
					BeforeObjectGraphHash = BeforeObjectGraphHash,
					BeforeTopologyHash = BeforeTopologyHash,
					State = KingdomLifecyclePhysicalState.Prepared,
					ReceiptId = ChildId(id, "object-callback-receipt", 0),
					ReceiptState = KingdomLifecyclePhysicalState.Prepared
				}
			};
			return GrowthArrivalCandidateShape(Book, candidate, true) ? candidate : null;
		}

		public static bool TryPublishGrowthArrivalCandidate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Candidate == null || Book.ArrivalCandidate != null
				|| Book.OptionState != KingdomLifecycleOptionState.Enabled
				|| Book.HealthState != KingdomGrowthHealthState.Healthy || Book.WorkPaused
				|| Candidate.CreatedTick < Book.OptionTick
				|| Candidate.CreatedTick < Book.HealthTick
				|| !Book.ArrivalCadenceMigrationPending
					&& (Book.ArrivalOpportunity == null
						|| Book.ArrivalOpportunity.FirstGuest != (Candidate.FirstGuest != null))
				|| !GrowthArrivalCandidateShape(Book, Candidate, true)
				|| !ClaimGrowthArrivalCandidateAgainstBook(Book, Candidate)) return false;
			string hash;
			if (!TryGrowthArrivalCandidatePlanHash(Candidate, out hash)) return false;
			KingdomLifecycleResourceLease[] leases =
				{ Candidate.CandidateLease, Candidate.LodgingLease, Candidate.EscrowLease };
			KingdomLifecycleResourceRevision[] rows = new KingdomLifecycleResourceRevision[3];
			bool[] created = new bool[3];
			for (int i = 0; i < leases.Length; i++)
			{
				KingdomLifecycleResourceLease lease = leases[i];
				rows[i] = FindGrowthResource(Book, lease.Key);
				created[i] = rows[i] == null;
				if (rows[i] == null) rows[i] = new KingdomLifecycleResourceRevision
				{
					Kind = lease.Kind, ScopeId = lease.ScopeId, SubjectId = lease.SubjectId,
					Key = lease.Key, Revision = lease.BeforeRevision
				};
				if (!GrowthResourceMatches(rows[i], lease)
					|| rows[i].Revision != lease.BeforeRevision
					|| !string.IsNullOrEmpty(rows[i].ActiveOperationId)) return false;
			}
			int additions = (created[0] ? 1 : 0) + (created[1] ? 1 : 0)
				+ (created[2] ? 1 : 0);
			if (Book.Resources.Count + additions > MaxResourceRows) return false;
			string oldHash = Candidate.PlanHash;
			ulong oldOrdinalHighWater = Book.ArrivalOrdinalHighWater;
			Candidate.PlanHash = hash;
			for (int i = 0; i < rows.Length; i++)
			{
				if (created[i]) Book.Resources.Add(rows[i]);
				rows[i].ActiveOperationId = Candidate.Id;
			}
			Book.ArrivalCandidate = Candidate;
			Book.ArrivalCandidateNextSequence = Candidate.Sequence + 1L;
			if (Book.ArrivalCadenceMigrationPending)
				Book.ArrivalOrdinalHighWater = (ulong)Candidate.Sequence;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.ArrivalCandidate = null; Book.ArrivalCandidateNextSequence = Candidate.Sequence;
			Book.ArrivalOrdinalHighWater = oldOrdinalHighWater;
			for (int i = rows.Length - 1; i >= 0; i--)
			{
				if (created[i]) Book.Resources.Remove(rows[i]);
				else rows[i].ActiveOperationId = null;
			}
			Candidate.PlanHash = oldHash;
			return false;
		}

		private static bool ClaimGrowthArrivalCandidateAgainstBook(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate)
		{
			Dictionary<string, string> claims = new Dictionary<string, string>(StringComparer.Ordinal);
			if (!ClaimGrowthOperationIdentities(claims, book.HeartbeatOp)
				|| !ClaimGrowthOperationIdentities(claims, book.ArrivalOp)
				|| !ClaimGrowthOperationIdentities(claims, book.DepartureOp)
				|| !ClaimGrowthOperationIdentities(claims, book.DeliveryOp)
				|| !ClaimGrowthOperationIdentities(claims, book.FetchOp)
				|| !ClaimGrowthOperationIdentities(claims, book.MillOp)) return false;
			for (int i = 0; i < book.FieldOps.Count; i++)
				if (book.FieldOps[i] != null && book.FieldOps[i].Operation != null
					&& !ClaimGrowthOperationIdentities(claims, book.FieldOps[i].Operation)) return false;
			return ClaimGrowthArrivalCandidateIdentities(claims, candidate, null);
		}

		internal static bool BeginGrowthArrivalCandidateCreate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Prepared
				|| Tick < Candidate.UpdatedTick) return false;
			KingdomGrowthObjectCallbackStep step = Candidate.CreateStep;
			KingdomLifecyclePhysicalState oldStepState = step.State;
			KingdomLifecyclePhysicalState oldReceiptState = step.ReceiptState;
			int oldBeforeMatches = step.ReceiptBeforeMatches;
			int oldBeforeCount = step.ReceiptBeforeCount;
			string oldBeforeOwner = step.ReceiptBeforeOwnerGraphHash;
			string oldBeforeObject = step.ReceiptBeforeObjectGraphHash;
			string oldBeforeTopology = step.ReceiptBeforeTopologyHash;
			KingdomLifecycleLeaseState oldLeaseState = Candidate.CandidateLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			step.State = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptBeforeMatches = 0; step.ReceiptBeforeCount = 0;
			step.ReceiptBeforeOwnerGraphHash = step.BeforeOwnerGraphHash;
			step.ReceiptBeforeObjectGraphHash = step.BeforeObjectGraphHash;
			step.ReceiptBeforeTopologyHash = step.BeforeTopologyHash;
			Candidate.CandidateLease.State = KingdomLifecycleLeaseState.Intent;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.CreateIntent;
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			step.State = oldStepState; step.ReceiptState = oldReceiptState;
			step.ReceiptBeforeMatches = oldBeforeMatches;
			step.ReceiptBeforeCount = oldBeforeCount;
			step.ReceiptBeforeOwnerGraphHash = oldBeforeOwner;
			step.ReceiptBeforeObjectGraphHash = oldBeforeObject;
			step.ReceiptBeforeTopologyHash = oldBeforeTopology;
			Candidate.CandidateLease.State = oldLeaseState;
			Candidate.Phase = oldPhase; Candidate.UpdatedTick = oldTick;
			return false;
		}

	}
}
