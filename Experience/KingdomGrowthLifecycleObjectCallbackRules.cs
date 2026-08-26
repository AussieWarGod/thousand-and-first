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
		internal static bool CommitGrowthObjectCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, bool Output, int LegOrdinal,
			string CallbackObjectId, string CallbackMarker, string CallbackReferenceHash,
			bool SameReference, string ObservedAfterOwnerGraphHash,
			string ObservedAfterObjectGraphHash, string ObservedAfterTopologyHash)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation) || !ValidRootId(CallbackObjectId)
				|| !ValidRootId(CallbackMarker) || !GrowthWitnessHash(CallbackReferenceHash)
				|| !SameReference || !GrowthWitnessHash(ObservedAfterOwnerGraphHash)
				|| !GrowthWitnessHash(ObservedAfterObjectGraphHash)
				|| !GrowthWitnessHash(ObservedAfterTopologyHash)) return false;
			List<KingdomGrowthObjectLeg> list = Output ? Operation.Outputs : Operation.Sources;
			int cursor = Output ? Operation.OutputCursor : Operation.SourceCursor;
			if (LegOrdinal != cursor || LegOrdinal < 0 || LegOrdinal >= list.Count) return false;
			KingdomGrowthObjectLeg leg = list[LegOrdinal];
			if (leg.State != KingdomLifecyclePhysicalState.Intent || leg.CallbackCursor < 0
				|| leg.CallbackCursor >= leg.Callbacks.Count) return false;
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[leg.CallbackCursor];
			bool create = step.Kind == KingdomGrowthObjectMutationKind.Create;
			if (step.State != KingdomLifecyclePhysicalState.Intent
				|| !string.Equals(CallbackMarker, leg.Marker, StringComparison.Ordinal)
				|| (!create && !string.Equals(CallbackObjectId, leg.ObjectId,
					StringComparison.Ordinal))
				|| (!create && (!string.Equals(ObservedAfterOwnerGraphHash,
					step.AfterOwnerGraphHash, StringComparison.Ordinal)
					|| !string.Equals(ObservedAfterObjectGraphHash, step.AfterObjectGraphHash,
						StringComparison.Ordinal)
					|| !string.Equals(ObservedAfterTopologyHash, step.AfterTopologyHash,
						StringComparison.Ordinal)))) return false;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, leg.Lease.Key);
			if (!GrowthResourceMatches(row, leg.Lease)
				|| row.Revision != leg.Lease.BeforeRevision
				|| !string.Equals(row.ActiveOperationId, Operation.Id,
					StringComparison.Ordinal)) return false;
			string oldObjectId = leg.ObjectId;
			string oldStepAfterOwner = step.AfterOwnerGraphHash;
			string oldStepAfterObject = step.AfterObjectGraphHash;
			string oldStepAfterTopology = step.AfterTopologyHash;
			KingdomLifecyclePhysicalState oldStepState = step.State;
			KingdomLifecyclePhysicalState oldStepReceiptState = step.ReceiptState;
			int oldStepAfterMatches = step.ReceiptAfterMatches;
			int oldStepAfterCount = step.ReceiptAfterCount;
			string oldStepCallbackId = step.ReceiptCallbackObjectId;
			string oldStepCallbackMarker = step.ReceiptCallbackMarker;
			string oldStepCallbackReference = step.ReceiptCallbackReferenceHash;
			bool oldStepSameReference = step.ReceiptSameReference;
			string oldStepReceiptAfterOwner = step.ReceiptAfterOwnerGraphHash;
			string oldStepReceiptAfterObject = step.ReceiptAfterObjectGraphHash;
			string oldStepReceiptAfterTopology = step.ReceiptAfterTopologyHash;
			string oldStepProof = step.ReceiptProofId;
			int oldCallbackCursor = leg.CallbackCursor;
			KingdomGrowthObjectCallbackStep nextStep = oldCallbackCursor + 1 < leg.Callbacks.Count
				? leg.Callbacks[oldCallbackCursor + 1] : null;
			string oldNextBeforeOwner = nextStep == null ? null : nextStep.BeforeOwnerGraphHash;
			string oldNextBeforeObject = nextStep == null ? null : nextStep.BeforeObjectGraphHash;
			string oldNextBeforeTopology = nextStep == null ? null : nextStep.BeforeTopologyHash;
			KingdomLifecyclePhysicalState oldLegState = leg.State;
			KingdomLifecycleLeaseState oldLeaseState = leg.Lease.State;
			string oldLegAfterOwner = leg.AfterOwnerGraphHash;
			string oldLegAfterObject = leg.AfterObjectGraphHash;
			string oldLegAfterTopology = leg.AfterTopologyHash;
			KingdomLifecyclePhysicalState oldLegReceiptState = leg.ReceiptState;
			int oldLegAfterIdMatches = leg.ReceiptAfterIdMatches;
			int oldLegAfterMarkerMatches = leg.ReceiptAfterMarkerMatches;
			int oldLegAfterCount = leg.ReceiptAfterCount;
			string oldLegReceiptAfterOwner = leg.ReceiptAfterOwnerGraphHash;
			string oldLegReceiptAfterObject = leg.ReceiptAfterObjectGraphHash;
			string oldLegReceiptAfterTopology = leg.ReceiptAfterTopologyHash;
			string oldLegCallbackId = leg.ReceiptCallbackObjectId;
			string oldLegCallbackMarker = leg.ReceiptCallbackMarker;
			string oldLegCallbackReference = leg.ReceiptCallbackReferenceHash;
			bool oldLegSameReference = leg.ReceiptSameReference;
			string oldLegProof = leg.ReceiptProofId;
			long oldRowRevision = row.Revision;
			string oldRowLastOperation = row.LastOperationId;
			int oldOperationCursor = cursor;
			if (create)
			{
				leg.ObjectId = CallbackObjectId;
				step.AfterOwnerGraphHash = ObservedAfterOwnerGraphHash;
				step.AfterObjectGraphHash = ObservedAfterObjectGraphHash;
				step.AfterTopologyHash = ObservedAfterTopologyHash;
			}
			step.State = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptAfterMatches = step.AfterCount == 0 ? 0 : 1;
			step.ReceiptAfterCount = step.AfterCount;
			step.ReceiptCallbackObjectId = CallbackObjectId;
			step.ReceiptCallbackMarker = CallbackMarker;
			step.ReceiptCallbackReferenceHash = CallbackReferenceHash;
			step.ReceiptSameReference = true;
			step.ReceiptAfterOwnerGraphHash = ObservedAfterOwnerGraphHash;
			step.ReceiptAfterObjectGraphHash = ObservedAfterObjectGraphHash;
			step.ReceiptAfterTopologyHash = ObservedAfterTopologyHash;
			step.ReceiptProofId = GrowthObjectCallbackProof(Operation, leg,
				LegOrdinal, Output, leg.CallbackCursor);
			leg.CallbackCursor++;
			if (leg.CallbackCursor < leg.Callbacks.Count)
			{
				KingdomGrowthObjectCallbackStep next = leg.Callbacks[leg.CallbackCursor];
				if (next.BeforeOwnerGraphHash == null)
				{
					next.BeforeOwnerGraphHash = ObservedAfterOwnerGraphHash;
					next.BeforeObjectGraphHash = ObservedAfterObjectGraphHash;
					next.BeforeTopologyHash = ObservedAfterTopologyHash;
				}
			}
			else
			{
				leg.State = KingdomLifecyclePhysicalState.Proved;
				leg.Lease.State = KingdomLifecycleLeaseState.Proved;
				leg.AfterOwnerGraphHash = ObservedAfterOwnerGraphHash;
				leg.AfterObjectGraphHash = ObservedAfterObjectGraphHash;
				leg.AfterTopologyHash = ObservedAfterTopologyHash;
				leg.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				leg.ReceiptAfterIdMatches = step.AfterCount == 0 ? 0 : 1;
				leg.ReceiptAfterMarkerMatches = leg.ReceiptAfterIdMatches;
				leg.ReceiptAfterCount = leg.AfterCount;
				leg.ReceiptAfterOwnerGraphHash = ObservedAfterOwnerGraphHash;
				leg.ReceiptAfterObjectGraphHash = ObservedAfterObjectGraphHash;
				leg.ReceiptAfterTopologyHash = ObservedAfterTopologyHash;
				leg.ReceiptCallbackObjectId = CallbackObjectId;
				leg.ReceiptCallbackMarker = CallbackMarker;
				leg.ReceiptCallbackReferenceHash = CallbackReferenceHash;
				leg.ReceiptSameReference = true;
				leg.ReceiptProofId = GrowthObjectReceiptProof(Operation, leg, LegOrdinal, Output);
				row.Revision = leg.Lease.AfterRevision;
				row.LastOperationId = Operation.Id;
				if (Output) Operation.OutputCursor++; else Operation.SourceCursor++;
			}
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			leg.ObjectId = oldObjectId;
			step.AfterOwnerGraphHash = oldStepAfterOwner;
			step.AfterObjectGraphHash = oldStepAfterObject;
			step.AfterTopologyHash = oldStepAfterTopology;
			step.State = oldStepState; step.ReceiptState = oldStepReceiptState;
			step.ReceiptAfterMatches = oldStepAfterMatches;
			step.ReceiptAfterCount = oldStepAfterCount;
			step.ReceiptCallbackObjectId = oldStepCallbackId;
			step.ReceiptCallbackMarker = oldStepCallbackMarker;
			step.ReceiptCallbackReferenceHash = oldStepCallbackReference;
			step.ReceiptSameReference = oldStepSameReference;
			step.ReceiptAfterOwnerGraphHash = oldStepReceiptAfterOwner;
			step.ReceiptAfterObjectGraphHash = oldStepReceiptAfterObject;
			step.ReceiptAfterTopologyHash = oldStepReceiptAfterTopology;
			step.ReceiptProofId = oldStepProof;
			leg.CallbackCursor = oldCallbackCursor;
			if (nextStep != null)
			{
				nextStep.BeforeOwnerGraphHash = oldNextBeforeOwner;
				nextStep.BeforeObjectGraphHash = oldNextBeforeObject;
				nextStep.BeforeTopologyHash = oldNextBeforeTopology;
			}
			leg.State = oldLegState; leg.Lease.State = oldLeaseState;
			leg.AfterOwnerGraphHash = oldLegAfterOwner;
			leg.AfterObjectGraphHash = oldLegAfterObject;
			leg.AfterTopologyHash = oldLegAfterTopology;
			leg.ReceiptState = oldLegReceiptState;
			leg.ReceiptAfterIdMatches = oldLegAfterIdMatches;
			leg.ReceiptAfterMarkerMatches = oldLegAfterMarkerMatches;
			leg.ReceiptAfterCount = oldLegAfterCount;
			leg.ReceiptAfterOwnerGraphHash = oldLegReceiptAfterOwner;
			leg.ReceiptAfterObjectGraphHash = oldLegReceiptAfterObject;
			leg.ReceiptAfterTopologyHash = oldLegReceiptAfterTopology;
			leg.ReceiptCallbackObjectId = oldLegCallbackId;
			leg.ReceiptCallbackMarker = oldLegCallbackMarker;
			leg.ReceiptCallbackReferenceHash = oldLegCallbackReference;
			leg.ReceiptSameReference = oldLegSameReference;
			leg.ReceiptProofId = oldLegProof;
			row.Revision = oldRowRevision; row.LastOperationId = oldRowLastOperation;
			if (Output) Operation.OutputCursor = oldOperationCursor;
			else Operation.SourceCursor = oldOperationCursor;
			return false;
		}

		private static string GrowthObjectCallbackProof(KingdomGrowthOperation operation,
			KingdomGrowthObjectLeg leg, int legOrdinal, bool output, int callbackOrdinal)
		{
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[callbackOrdinal];
			return HashId("growth-object-callback-proof", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.Id); CanonicalString(w, operation.PlanHash);
				w.Write(output); w.Write(legOrdinal); w.Write(callbackOrdinal);
				CanonicalString(w, leg.ObjectId); CanonicalString(w, leg.Marker);
				CanonicalString(w, step.ReceiptCallbackReferenceHash);
				CanonicalString(w, step.ReceiptAfterOwnerGraphHash);
				CanonicalString(w, step.ReceiptAfterObjectGraphHash);
				CanonicalString(w, step.ReceiptAfterTopologyHash);
			});
		}

		public static KingdomGrowthDomainStep PrepareGrowthDomainStep(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, KingdomGrowthDomainStepKind Kind,
			KingdomGrowthDomainCallbackKind CallbackKind, string ActorId, string SubjectId,
			long Before, long After, string CallbackBodyHash, string BeforeGraphHash,
			string AfterGraphHash, string BeforeMapHash, string AfterMapHash)
		{
			return PrepareGrowthDomainStep(Book, Operation, Kind, CallbackKind, ActorId,
				SubjectId, Before, After, CallbackBodyHash, BeforeGraphHash, AfterGraphHash,
				BeforeMapHash, AfterMapHash, null, null, null, null);
		}

		public static KingdomGrowthDomainStep PrepareGrowthDomainStep(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, KingdomGrowthDomainStepKind Kind,
			KingdomGrowthDomainCallbackKind CallbackKind, string ActorId, string SubjectId,
			long Before, long After, string CallbackBodyHash, string BeforeGraphHash,
			string AfterGraphHash, string BeforeMapHash, string AfterMapHash,
			KingdomGrowthScarcitySnapshot ScarcityBefore,
			KingdomGrowthScarcitySnapshot ScarcityAfter,
			KingdomGrowthAccountingSnapshot AccountingBefore,
			KingdomGrowthAccountingSnapshot AccountingAfter,
			KingdomGrowthFieldState FieldBefore = null,
			KingdomGrowthFieldState FieldAfter = null,
			List<KingdomGrowthCropRow> CropRowsBefore = null,
			List<KingdomGrowthCropRow> CropRowsDeclaredAfter = null)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Operation == null || Operation.Phase != KingdomGrowthPhase.Prepared
				|| Operation.PlanHash != null || Operation.DomainSteps == null
				|| Operation.DomainSteps.Count >= MaxResourceLeases) return null;
			KingdomLifecycleResourceKind resourceKind;
			if (!TryGrowthDomainKind(Kind, CallbackKind, out resourceKind)) return null;
			long delta;
			if (!CheckedAdd(After, -Before, out delta) || delta == 0L) return null;
			string key = ResourceKey(resourceKind, Operation.SettlementId, SubjectId);
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, key);
			if (key == null || (row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Revision == long.MaxValue))) return null;
			long revision = row == null ? 0L : row.Revision;
			int ordinal = Operation.DomainSteps.Count;
			KingdomGrowthDomainStep step = new KingdomGrowthDomainStep
			{
				Kind = Kind, CallbackKind = CallbackKind, CallbackBodyHash = CallbackBodyHash,
				EventId = ChildId(Operation.Id, "domain", ordinal), ActorId = ActorId,
				SubjectId = SubjectId, BeforeValue = Before, AfterValue = After,
				BeforeGraphHash = BeforeGraphHash, AfterGraphHash = AfterGraphHash,
				BeforeMapHash = BeforeMapHash, AfterMapHash = AfterMapHash,
				ScarcityBefore = CloneGrowthScarcity(ScarcityBefore),
				ScarcityAfter = CloneGrowthScarcity(ScarcityAfter),
				AccountingBefore = CloneGrowthAccounting(AccountingBefore),
				AccountingAfter = CloneGrowthAccounting(AccountingAfter),
				FieldBefore = CloneGrowthFieldState(FieldBefore),
				FieldAfter = CloneGrowthFieldState(FieldAfter),
				CropRowsBefore = CloneGrowthCropRows(CropRowsBefore),
				CropRowsDeclaredAfter = CloneGrowthCropRows(CropRowsDeclaredAfter),
				CropRowsAfter = null,
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, "domain-receipt", ordinal),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared,
				Lease = new KingdomLifecycleResourceLease
				{
					OperationId = Operation.Id, Kind = resourceKind, ScopeId = Operation.SettlementId,
					SubjectId = SubjectId, Key = key, Before = Before, Delta = delta, After = After,
					BeforeRevision = revision, AfterRevision = revision + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				}
			};
			return GrowthDomainShape(Operation, step, ordinal, true) ? step : null;
		}

	}
}
