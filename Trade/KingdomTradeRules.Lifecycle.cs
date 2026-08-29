using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		public static KingdomTradeOperation NewOperation(KingdomTradeBook Book,
			KingdomTradeOperationKind Kind, long Tick)
		{
			if (!BookUsable(Book) || !ValidId(Book.RealmId) || Book.OpenOperation != null
				|| Book.NextOperationSequence <= Book.RetiredThrough
				|| Book.NextOperationSequence == long.MaxValue || Tick < 0L
				|| Kind == KingdomTradeOperationKind.None
				|| !Enum.IsDefined(typeof(KingdomTradeOperationKind), Kind)) return null;
			// Reserve a durable retirement slot before publishing any operation authority.
			if (!EnsureRetirementCapacity(Book)) return null;
			long sequence = Book.NextOperationSequence;
			string id = OperationId(Book.RealmId, sequence);
			if (!ValidId(id)) return null;
			Book.NextOperationSequence++;
			KingdomTradeOperation operation = new KingdomTradeOperation
			{
				Sequence = sequence,
				Id = id,
				Kind = Kind,
				Phase = KingdomTradePhase.Prepared,
				CreatedTick = Tick,
				UpdatedTick = Tick,
					ProjectionState = KingdomTradePhysicalState.None,
					PriorCleanupState = KingdomTradePhysicalState.None,
					WaterLegs = new List<KingdomTradeWaterLeg>(),
					MaterialOutputs = new List<KingdomTradeMaterialOutput>(),
					Pattern = Kind == KingdomTradeOperationKind.CharterDelivery
						? KingdomTradePatternRules.PriorWireDefault() : null
			};
			Book.OpenOperation = operation;
			return operation;
		}

		/// <summary>Reserves one exact proof slot before an operation can publish effects.</summary>
		public static bool EnsureRetirementCapacity(KingdomTradeBook Book)
		{
			if (Book == null || Book.RecentProofs == null || Book.CompactedProofs == null
				|| Book.RecentProofs.Count > MaxRecentProofs
				|| Book.CompactedProofs.Count > MaxCompactedProofs) return false;
			if (Book.RecentProofs.Count < MaxRecentProofs) return true;
			const int compactCount = MaxRecentProofs / 2;
			// D2 proofs are the only durable source from which Polity can publish its reply.
			// Keep them exact until the matching durable Polity conclusion acknowledges them.
			List<KingdomTradeProof> batch = new List<KingdomTradeProof>();
			List<int> indexes = new List<int>();
			for (int i = 0; i < Book.RecentProofs.Count && batch.Count < compactCount; i++)
				if (Book.RecentProofs[i]?.Kind !=
					KingdomTradeOperationKind.PolityConsignmentDelivery)
				{
					batch.Add(Book.RecentProofs[i]); indexes.Add(i);
				}
			if (batch.Count == 0) return false;
			return TryCompactProofRows(Book, batch, indexes, out string _);
		}

		public static bool Retire(KingdomTradeBook Book, KingdomTradeOperation Operation,
			KingdomTradePhase Disposition, long Tick, string Fault)
		{
			if (Book == null || Operation == null || Book.OpenOperation != Operation
				|| Operation.Sequence <= Book.RetiredThrough
				|| (Disposition != KingdomTradePhase.Terminal
					&& Disposition != KingdomTradePhase.Quarantined)
				|| (Disposition == KingdomTradePhase.Terminal
					&& Operation.Phase != KingdomTradePhase.RetirementReady)
				|| (Operation.Phase != KingdomTradePhase.RetirementReady
					&& Operation.Phase != KingdomTradePhase.Quarantined)) return false;
			if (HasUnresolvedEffects(Operation) || !DurableDomainSettled(Book, Operation))
			{
				Operation.Phase = KingdomTradePhase.Quarantined;
				Operation.Fault = AppendFault(Operation.Fault,
					"unresolved trade value or effects remain under this open receipt");
				return false;
			}
			if (Book.PendingRetirement != null || Book.RecentProofs == null
				|| Book.RecentProofs.Count >= MaxRecentProofs) return false;
			Operation.Phase = Disposition;
			Operation.UpdatedTick = Tick;
			Operation.Fault = Bound(Fault, MaxTextChars);
			Book.PendingRetirement = ProofFor(Book, Operation, Disposition, Tick, Fault);
			return CompletePendingRetirement(Book);
		}

		public static bool HasUnresolvedEffects(KingdomTradeOperation Operation)
		{
			if (Operation == null || Operation.AmbiguousWater > 0
				|| Operation.MaterialProved != Operation.MaterialRequested
				|| !ValidAccountingEvidence(Operation)
				|| Operation.ManifestEscrowState == KingdomTradePhysicalState.Lost
				|| Operation.RetainedState == KingdomTradePhysicalState.Lost) return true;
			if (Operation.Kind == KingdomTradeOperationKind.ManifestDelivery
				&& Operation.ManifestEscrowState != KingdomTradePhysicalState.Proved) return true;
			if (Operation.Kind == KingdomTradeOperationKind.ManifestLapse
				&& Operation.RetainedState != KingdomTradePhysicalState.Proved) return true;
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& (Operation.ProvedWater != Operation.RequestedWater
					|| !KingdomTradePatternRules.Terminal(Operation.Pattern))) return true;
			if (Operation.Kind == KingdomTradeOperationKind.ManifestLoad
				&& Operation.ProvedWater != Operation.RequestedWater) return true;
			if (Operation.Kind == KingdomTradeOperationKind.PolityConsignmentDelivery &&
				(Operation.Phase == KingdomTradePhase.Quarantined
					? Operation.ProvedWater == 0 ? Operation.RetainedBefore != 0L ||
						Operation.RetainedDelta != 0L || Operation.RetainedAfter != 0L ||
						Operation.RetainedState != KingdomTradePhysicalState.None :
						Operation.RetainedDelta != Operation.ProvedWater ||
						Operation.RetainedState != KingdomTradePhysicalState.Proved
					: Operation.ProvedWater < 1 ||
						Operation.ProvedWater > Operation.RequestedWater)) return true;
			if (Operation.WaterLegs == null) return true;
			long provedWater = 0L;
			for (int i = 0; i < Operation.WaterLegs.Count; i++)
			{
				KingdomTradeWaterLeg leg = Operation.WaterLegs[i];
					if (leg == null || (leg.State != KingdomTradePhysicalState.Proved
						&& leg.State != KingdomTradePhysicalState.Skipped)) return true;
				if (leg.State == KingdomTradePhysicalState.Proved) provedWater += leg.Delta;
			}
			if (provedWater != Operation.ProvedWater) return true;
			if (Operation.MaterialOutputs == null) return true;
			long provedMaterial = 0L;
			for (int i = 0; i < Operation.MaterialOutputs.Count; i++)
			{
				KingdomTradeMaterialOutput output = Operation.MaterialOutputs[i];
					if (output == null || output.State != KingdomTradePhysicalState.Proved
						|| (output.CleanupState != KingdomTradePhysicalState.None
							&& output.CleanupState != KingdomTradePhysicalState.Skipped
							&& output.CleanupState != KingdomTradePhysicalState.Proved)) return true;
				if (output.State == KingdomTradePhysicalState.Proved) provedMaterial += output.Count;
			}
			if (provedMaterial != Operation.MaterialProved) return true;
			if (!TerminalPhysical(Operation.ProjectionState)
				|| !TerminalPhysical(Operation.PriorCleanupState)
				|| (Operation.Standing != null
					&& !TerminalPhysical(Operation.Standing.State))) return true;
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery)
			{
				if ((Operation.Phase == KingdomTradePhase.RetirementReady
						|| Operation.Phase == KingdomTradePhase.Terminal)
					&& !TerminalCharterOutboxExact(Operation)) return true;
				if (Operation.Phase == KingdomTradePhase.Quarantined
					&& !TerminalCharterOutboxExact(Operation)
					&& !QuarantineCharterOutboxExact(Operation)) return true;
			}
			KingdomTradeOutbox box = Operation.Outbox;
			return box == null || !string.Equals(box.EventId, Operation.Id,
				StringComparison.Ordinal) || box.ChronicleState == KingdomTradeSinkState.Lost
				|| box.LedgerState == KingdomTradeSinkState.Lost
				|| box.MessageState == KingdomTradeSinkState.Lost
				|| box.DeedState == KingdomTradeSinkState.Lost
				|| !SinkSettled(box.ChronicleState) || !SinkSettled(box.LedgerState)
				|| !SinkSettled(box.MessageState) || !SinkSettled(box.DeedState);
		}

		/// <summary>Only a complete bounded Charter payload may reach external callbacks.</summary>
	}
}
