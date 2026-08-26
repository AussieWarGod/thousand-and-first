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
		private static bool GrowthOutboxShape(KingdomGrowthOperation operation, bool publication)
		{
			if (operation.OutboxEvents == null
				|| operation.OutboxEvents.Count > MaxGrowthOutboxEvents) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			bool haveChronicle = false; int chronicleCount = 0; string chronicleHash = null;
			bool haveOutsider = false; int outsiderCount = 0; string outsiderHash = null;
			bool haveLedger = false; int ledgerCount = 0; string ledgerHash = null;
			for (int i = 0; i < operation.OutboxEvents.Count; i++)
			{
				KingdomGrowthOutboxEvent e = operation.OutboxEvents[i];
				KingdomLifecycleOutbox box = e == null ? null : e.Outbox;
				bool legacyChronicle = e != null && e.LegacySingleRegisterChronicle;
				if (box == null || !ValidName(e.Kind)
					|| !string.Equals(e.EventId, ChildId(operation.Id, "outbox-event", i),
						StringComparison.Ordinal) || !ids.Add(e.EventId)
					|| !string.Equals(box.OperationId, operation.Id, StringComparison.Ordinal)
					|| !string.Equals(box.EventId, e.EventId, StringComparison.Ordinal)
					|| !string.Equals(box.ChronicleReceiptId,
						ChildId(e.EventId, "chronicle", 0), StringComparison.Ordinal)
					|| TooLong(box.Chronicle, MaxTextChars)
					|| TooLong(box.Ledger, MaxTextChars) || TooLong(box.Message, MaxTextChars)
					|| TooLong(box.Deed, MaxTextChars) || TooLong(box.GuestbookLine, MaxTextChars)
					|| !GrowthSinkTextShape(box.Chronicle, box.ChronicleDisposition,
						box.ChronicleState, publication)
					|| !GrowthSinkTextShape(box.Ledger, box.LedgerDisposition,
						box.LedgerState, publication)
					|| !GrowthSinkTextShape(box.Message, box.MessageDisposition,
						box.MessageState, publication)
					|| !GrowthSinkTextShape(box.Deed, box.DeedDisposition,
						box.DeedState, publication)
					|| !GrowthSinkTextShape(box.GuestbookLine, box.GuestbookDisposition,
						box.GuestbookState, publication)
					|| (legacyChronicle && (!operation.LegacyGrowthV1Plan
						|| box.Chronicle == null))
					|| (operation.LegacyGrowthV1Plan && box.Chronicle != null
						&& !legacyChronicle)
					|| (box.Chronicle == null
						? e.ChronicleOfficial != null || e.ChronicleOutsider != null
						: legacyChronicle
							? e.ChronicleOfficial != null || e.ChronicleOutsider != null
							: string.IsNullOrEmpty(e.ChronicleOfficial)
								|| e.ChronicleOfficial.Length >
									KingdomChronicleReceiptRules.MaxEntryChars
								|| string.IsNullOrEmpty(e.ChronicleOutsider)
								|| e.ChronicleOutsider.Length >
									KingdomChronicleReceiptRules.MaxEntryChars)
					|| !GrowthInspectableSinkShape(box.Chronicle, box.ChronicleState,
						e.ChronicleBeforeCount, e.ChronicleBeforeHash,
						e.ChronicleDeclaredAfterCount, e.ChronicleDeclaredAfterHash,
						e.ChronicleObservedCount, e.ChronicleObservedHash, publication,
						legacyChronicle ? -1 : KingdomChronicleReceiptRules.MaxEntries)
					|| (legacyChronicle
						? !GrowthOutsiderReceiptEmpty(e)
						: !GrowthInspectableSinkShape(box.Chronicle, box.ChronicleState,
							e.OutsiderBeforeCount, e.OutsiderBeforeHash,
							e.OutsiderDeclaredAfterCount, e.OutsiderDeclaredAfterHash,
							e.OutsiderObservedCount, e.OutsiderObservedHash, publication,
							KingdomChronicleReceiptRules.MaxEntries))
					|| !GrowthInspectableSinkShape(box.Ledger, box.LedgerState,
						e.LedgerBeforeCount, e.LedgerBeforeHash,
						e.LedgerDeclaredAfterCount, e.LedgerDeclaredAfterHash,
						e.LedgerObservedCount, e.LedgerObservedHash, publication)) return false;
				if (box.Chronicle != null)
				{
					if (haveChronicle && (e.ChronicleBeforeCount != chronicleCount
						|| !string.Equals(e.ChronicleBeforeHash, chronicleHash,
							StringComparison.Ordinal))) return false;
					haveChronicle = true; chronicleCount = e.ChronicleDeclaredAfterCount;
					chronicleHash = e.ChronicleDeclaredAfterHash;
					if (!legacyChronicle)
					{
						if (haveOutsider && (e.OutsiderBeforeCount != outsiderCount
							|| !string.Equals(e.OutsiderBeforeHash, outsiderHash,
								StringComparison.Ordinal))) return false;
						haveOutsider = true;
						outsiderCount = e.OutsiderDeclaredAfterCount;
						outsiderHash = e.OutsiderDeclaredAfterHash;
					}
				}
				if (box.Ledger != null)
				{
					if (haveLedger && (e.LedgerBeforeCount != ledgerCount
						|| !string.Equals(e.LedgerBeforeHash, ledgerHash,
							StringComparison.Ordinal))) return false;
					haveLedger = true; ledgerCount = e.LedgerDeclaredAfterCount;
					ledgerHash = e.LedgerDeclaredAfterHash;
				}
			}
			return true;
		}

		private static bool GrowthInspectableSinkShape(string text,
			KingdomLifecycleSinkState state, int beforeCount, string beforeHash,
			int declaredAfterCount, string declaredAfterHash, int observedCount,
			string observedHash, bool publication, int boundedCount = -1)
		{
			if (!GrowthSinkDeclarationShape(text, beforeCount, beforeHash,
				declaredAfterCount, declaredAfterHash, boundedCount)) return false;
			if (text == null)
				return state == KingdomLifecycleSinkState.Skipped
					&& observedCount == -1 && observedHash == null;
			if (publication || state == KingdomLifecycleSinkState.Pending
				|| state == KingdomLifecycleSinkState.Intent)
				return observedCount == -1 && observedHash == null;
			return state == KingdomLifecycleSinkState.Delivered
				&& observedCount == declaredAfterCount
				&& string.Equals(observedHash, declaredAfterHash, StringComparison.Ordinal);
		}

		private static bool GrowthOutsiderReceiptEmpty(KingdomGrowthOutboxEvent e)
		{
			return e.OutsiderBeforeCount == 0 && e.OutsiderDeclaredAfterCount == 0
				&& e.OutsiderObservedCount == -1 && e.OutsiderBeforeHash == null
				&& e.OutsiderDeclaredAfterHash == null && e.OutsiderObservedHash == null;
		}

		private static bool GrowthSinkTextShape(string text,
			KingdomLifecycleSinkDisposition disposition, KingdomLifecycleSinkState state,
			bool publication)
		{
			return (disposition == KingdomLifecycleSinkDisposition.Skip ? text == null
				: text != null && text.Length > 0)
				&& SinkTextShape(text, disposition, state, publication);
		}

		private static bool GrowthOutboxTerminal(KingdomGrowthOperation operation)
		{
			if (!GrowthOutboxShape(operation, false)) return false;
			for (int i = 0; i < operation.OutboxEvents.Count; i++)
			{
				KingdomLifecycleOutbox box = operation.OutboxEvents[i].Outbox;
				if (!SinkSettled(box.ChronicleState) || !SinkSettled(box.LedgerState)
					|| !SinkSettled(box.MessageState) || !SinkSettled(box.DeedState)
					|| !SinkSettled(box.GuestbookState)
					|| (box.Chronicle == null
						? box.ChronicleState != KingdomLifecycleSinkState.Skipped
						: box.ChronicleState != KingdomLifecycleSinkState.Delivered)
					|| (box.Ledger == null
						? box.LedgerState != KingdomLifecycleSinkState.Skipped
						: box.LedgerState != KingdomLifecycleSinkState.Delivered)) return false;
			}
			return true;
		}

		private static bool GrowthWitnessHash(string value)
		{
			if (value == null || value.Length != 64) return false;
			for (int i = 0; i < value.Length; i++)
				if (!((value[i] >= '0' && value[i] <= '9')
					|| (value[i] >= 'a' && value[i] <= 'f'))) return false;
			return true;
		}

		private static bool GrowthTopologyValid(KingdomLifecycleTopology topology,
			string ownerId, string zoneId, int x, int y)
		{
			return TopologyValid(topology, ownerId, zoneId, x, y)
				&& (topology != KingdomLifecycleTopology.Cell || ownerId == null);
		}

		private static string GrowthWaterReceiptProof(KingdomGrowthOperation operation,
			KingdomGrowthWaterLeg leg, int ordinal)
		{
			return HashId("growth-water-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.SettlementId); CanonicalString(w, operation.Id);
				CanonicalString(w, operation.PlanHash); w.Write(ordinal);
				WriteGrowthWaterPlan(w, leg); CanonicalString(w, leg.ReceiptBeforeOwnerGraphHash);
				CanonicalString(w, leg.ReceiptAfterOwnerGraphHash);
				CanonicalString(w, leg.ReceiptBeforePartGraphHash);
				CanonicalString(w, leg.ReceiptAfterPartGraphHash);
					CanonicalString(w, leg.ReceiptBeforeTopologyHash);
					CanonicalString(w, leg.ReceiptAfterTopologyHash);
					CanonicalString(w, leg.ReceiptCallbackContainerId);
					CanonicalString(w, leg.ReceiptCallbackReferenceHash);
					w.Write(leg.ReceiptSameReference);
				});
		}

		private static string GrowthObjectReceiptProof(KingdomGrowthOperation operation,
			KingdomGrowthObjectLeg leg, int ordinal, bool output)
		{
			return HashId("growth-object-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.SettlementId); CanonicalString(w, operation.Id);
				CanonicalString(w, operation.PlanHash); w.Write(output); w.Write(ordinal);
				WriteGrowthObjectPlan(w, leg); w.Write(leg.ReceiptBeforeIdMatches);
				w.Write(leg.ReceiptBeforeMarkerMatches); w.Write(leg.ReceiptBeforeCount);
				w.Write(leg.ReceiptAfterIdMatches); w.Write(leg.ReceiptAfterMarkerMatches);
				w.Write(leg.ReceiptAfterCount); CanonicalString(w, leg.ReceiptBeforeOwnerGraphHash);
				CanonicalString(w, leg.ReceiptAfterOwnerGraphHash);
				CanonicalString(w, leg.ReceiptBeforeObjectGraphHash);
				CanonicalString(w, leg.ReceiptAfterObjectGraphHash);
				CanonicalString(w, leg.ReceiptBeforeTopologyHash);
				CanonicalString(w, leg.ReceiptAfterTopologyHash);
					CanonicalString(w, leg.ReceiptCallbackObjectId);
					CanonicalString(w, leg.ReceiptCallbackMarker);
					CanonicalString(w, leg.ReceiptCallbackReferenceHash);
					w.Write(leg.ReceiptSameReference);
				});
		}

		private static string GrowthDomainReceiptProof(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep step, int ordinal)
		{
			return HashId("growth-domain-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.SettlementId); CanonicalString(w, operation.Id);
				CanonicalString(w, operation.PlanHash); w.Write(ordinal);
				WriteGrowthDomainPlan(w, step); w.Write(step.ReceiptBeforeValue);
				w.Write(step.ReceiptAfterValue); CanonicalString(w, step.ReceiptBeforeGraphHash);
				CanonicalString(w, step.ReceiptAfterGraphHash);
				CanonicalString(w, step.ReceiptBeforeMapHash);
				CanonicalString(w, step.ReceiptAfterMapHash);
				WriteGrowthCropRowsPlan(w, step.CropRowsAfter);
			});
		}

		private static bool GrowthOperationEvidenceBounded(KingdomGrowthOperation operation)
		{
			if (operation == null) return true;
			if (operation.WaterLegs == null || operation.WaterLegs.Count > MaxWaterLegs
				|| operation.Sources == null || operation.Sources.Count > MaxGrowthSources
				|| operation.Outputs == null || operation.Outputs.Count > MaxGrowthOutputs
				|| operation.DomainSteps == null || operation.DomainSteps.Count > MaxResourceLeases
				|| operation.ClockLease == null || operation.OutboxEvents == null
				|| operation.OutboxEvents.Count > MaxGrowthOutboxEvents
				|| TooLong(operation.Fault, MaxTextChars)
				|| (operation.Phase == KingdomGrowthPhase.Quarantined
					? string.IsNullOrEmpty(operation.Fault) : operation.Fault != null)) return false;
			for (int i = 0; i < operation.WaterLegs.Count; i++) if (operation.WaterLegs[i] == null
				|| operation.WaterLegs[i].Lease == null) return false;
			for (int i = 0; i < operation.Sources.Count; i++) if (operation.Sources[i] == null) return false;
			for (int i = 0; i < operation.Outputs.Count; i++) if (operation.Outputs[i] == null) return false;
			for (int i = 0; i < operation.DomainSteps.Count; i++) if (operation.DomainSteps[i] == null
				|| operation.DomainSteps[i].Lease == null) return false;
			return true;
		}

		private static bool LegacyResourceKindsOnly(KingdomLifecycleBook book)
		{
			if (book == null || book.Resources == null) return false;
			for (int i = 0; i < book.Resources.Count; i++)
				if (book.Resources[i] == null || (byte)book.Resources[i].Kind > 11) return false;
			KingdomLifecycleOperation[] operations = { book.PlainGuest, book.NotableGuest,
				book.Raid, book.Petition };
			for (int i = 0; i < operations.Length; i++)
			{
				KingdomLifecycleOperation operation = operations[i];
				if (operation == null || operation.ResourceLeases == null) continue;
				for (int j = 0; j < operation.ResourceLeases.Count; j++)
					if (operation.ResourceLeases[j] == null ||
						(byte)operation.ResourceLeases[j].Kind > 11) return false;
			}
			return true;
		}

		private static bool HasOpenGrowthOperation(KingdomGrowthBook book)
		{
			if (book == null) return false;
			if (book.HeartbeatOp != null || book.ArrivalOp != null || book.DepartureOp != null
				|| book.DeliveryOp != null || book.FetchOp != null || book.MillOp != null
				|| book.ArrivalCandidate != null) return true;
			if (book.FieldOps != null) for (int i = 0; i < book.FieldOps.Count; i++)
				if (book.FieldOps[i] != null && book.FieldOps[i].Operation != null) return true;
			return false;
		}

	}
}
