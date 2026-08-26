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
		private static bool ConfirmCarryOutputCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleProjection Output)
		{
			int index = IndexOfOutput(Operation, Output);
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.ProjectionIntent
				|| index < 0 || index != Operation.OutputIndex || Operation.LostOnRoad
				|| Output.State != KingdomLifecyclePhysicalState.Intent
				|| Output.ReceiptState != KingdomLifecyclePhysicalState.Intent
				|| Output.ReceiptBeforeIdMatches != 0
				|| Output.ReceiptBeforeMarkerMatches != 0 || Output.ReceiptBeforeCount != 0
				|| Output.ReceiptAfterIdMatches != -1 || Output.ReceiptAfterMarkerMatches != -1
				|| Output.ReceiptAfterCount != -1 || Output.ReceiptSameReference
				|| !string.IsNullOrEmpty(Output.ReceiptProofId)) return false;
			Output.State = KingdomLifecyclePhysicalState.Proved;
			return true;
		}

		private static bool SkipCarryOutputOnRoadCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleProjection Output)
		{
			int index = IndexOfOutput(Operation, Output);
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.ProjectionIntent
				|| index < 0 || index != Operation.OutputIndex || !Operation.LostOnRoad
				|| !CarryOutputShape(Output, Operation.Id, index, false)
				|| Output.State != KingdomLifecyclePhysicalState.Prepared
				|| Output.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| Output.ReceiptBeforeIdMatches != -1
				|| Output.ReceiptBeforeMarkerMatches != -1
				|| Output.ReceiptBeforeCount != -1) return false;
			Output.ReceiptBeforeIdMatches = 0;
			Output.ReceiptBeforeMarkerMatches = 0;
			Output.ReceiptBeforeCount = 0;
			Output.ReceiptAfterIdMatches = 0;
			Output.ReceiptAfterMarkerMatches = 0;
			Output.ReceiptAfterCount = 0;
			Output.ReceiptProofId = CarryOutputReceiptProof(Operation, Output, true);
			Output.ReceiptState = KingdomLifecyclePhysicalState.Skipped;
			Output.State = KingdomLifecyclePhysicalState.Skipped;
			return CarryOutputShape(Output, Operation.Id, index, false);
		}

		public static bool MoveCarryEscrow(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleProjection Output, bool Lost)
		{
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.ProjectionIntent
				|| Output == null || Operation.OutputIndex < 0
				|| Operation.OutputIndex >= Operation.Outputs.Count
				|| !ReferenceEquals(Operation.Outputs[Operation.OutputIndex], Output)
				|| Lost != Operation.LostOnRoad
				|| !CarryOutputShape(Output, Operation.Id, Operation.OutputIndex, false)
				|| Output.State != (Lost ? KingdomLifecyclePhysicalState.Skipped
					: KingdomLifecyclePhysicalState.Proved)
				|| Output.ReceiptState != Output.State
				|| !ExactCarryOutputReceipt(Operation, Output, Lost)
				|| !CarryConserved(Operation)) return false;
			int Material = Output.Material;
			int Count = Output.Count;
			int escrow = MaterialValue(Operation, Material, 1);
			if (escrow < Count) return false;
			int delivered = MaterialValue(Operation, Material, 2);
			int roadLost = MaterialValue(Operation, Material, 3);
			if (!AddMaterial(Operation, Material, -Count, Lost ? 0 : Count, Lost ? Count : 0))
				return false;
			Operation.OutputIndex++;
			if (CarryConserved(Operation)) return true;
			Operation.OutputIndex--;
			SetMaterial(Operation, Material, 1, escrow);
			SetMaterial(Operation, Material, 2, delivered);
			SetMaterial(Operation, Material, 3, roadLost);
			return false;
		}

		public static bool CanTransitionCarry(KingdomLifecyclePhase From,
			KingdomLifecyclePhase To)
		{
			if (To == KingdomLifecyclePhase.Quarantined)
				return CarryPhaseAllowed(From) && From != KingdomLifecyclePhase.Terminal
					&& From != KingdomLifecyclePhase.Quarantined;
			switch (From)
			{
			case KingdomLifecyclePhase.Prepared: return To == KingdomLifecyclePhase.RemovalIntent;
			case KingdomLifecyclePhase.RemovalIntent: return To == KingdomLifecyclePhase.Removed;
			case KingdomLifecyclePhase.Removed: return To == KingdomLifecyclePhase.ScheduleIntent;
			case KingdomLifecyclePhase.ScheduleIntent: return To == KingdomLifecyclePhase.ProjectionIntent;
			case KingdomLifecyclePhase.ProjectionIntent: return To == KingdomLifecyclePhase.Projected;
			case KingdomLifecyclePhase.Projected: return To == KingdomLifecyclePhase.Sinks;
			case KingdomLifecyclePhase.Sinks: return To == KingdomLifecyclePhase.Terminal;
			default: return false;
			}
		}

		public static bool AdvanceCarryPhase(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecyclePhase To, long Tick)
		{
			if (!ExactCarryAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| !CanTransitionCarry(Operation.Phase, To)) return false;
			bool exact = Operation.AuthorityKind == KingdomCarryAuthorityKind.ExactManifest;
			if (To == KingdomLifecyclePhase.RemovalIntent && exact
				&& Operation.SignReceiptState != KingdomLifecyclePhysicalState.Proved) return false;
			if (To == KingdomLifecyclePhase.Removed
				&& !(exact ? AllExactSourcesLoaded(Operation) : AllSourcesProved(Operation))) return false;
			if (To == KingdomLifecyclePhase.ProjectionIntent
				&& !CarryScheduleProved(Book, Operation)) return false;
			if (To == KingdomLifecyclePhase.Projected
				&& (!(exact ? ExactOutputsSettled(Operation) : OutputsSettledForRoad(Operation))
					|| CarryEscrow(Operation) != 0
					|| !CarryConserved(Operation))) return false;
			if (To == KingdomLifecyclePhase.Terminal && !CarryTerminalComponentsSettled(Operation))
				return false;
			Operation.Phase = To;
			Operation.UpdatedTick = Tick;
			return true;
		}

		public static bool Quarantine(KingdomCarryOperation Operation, string Fault)
		{
			if (Operation == null || Operation.Phase == KingdomLifecyclePhase.Quarantined) return false;
			Operation.Phase = KingdomLifecyclePhase.Quarantined;
			Operation.Fault = SafeFault(Fault);
			return true;
		}

		public static bool RetireCarry(KingdomCarryBook Book,
			KingdomCarryOperation Operation, long Tick)
		{
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Terminal
				|| !IsExactSuccessor(Operation.Sequence, Book.RetiredThrough)
				|| Tick < Operation.UpdatedTick
				|| !CarryTerminalComponentsSettled(Operation)
				|| !CarryScheduleProved(Book, Operation)
				|| !CarryProofListValid(Book)) return false;
			KingdomLifecycleResourceRevision schedule = FindResource(Book,
				Operation.ScheduleLease.Key);
			if (schedule == null || !string.Equals(schedule.ActiveOperationId,
				Operation.Id, StringComparison.Ordinal)) return false;
			schedule.ActiveOperationId = null;
			Operation.UpdatedTick = Tick;
			Book.RetiredThrough = Operation.Sequence;
			AppendProof(Book.RecentProofs, new KingdomLifecycleProof
			{
				Sequence = Operation.Sequence,
				Id = Operation.Id,
				PlanHash = Operation.PlanHash,
				Lane = KingdomLifecycleLane.None,
				Action = KingdomLifecycleAction.None,
				Tick = Tick
			});
			Book.Open = null;
			return true;
		}

		public static int CarryEscrow(KingdomCarryOperation Operation)
		{
			if (Operation != null
				&& Operation.AuthorityKind == KingdomCarryAuthorityKind.ExactManifest)
			{
				long exact = 0L;
				for (int i = 0; Operation.Sources != null && i < Operation.Sources.Count; i++)
				{
					KingdomCarrySource source = Operation.Sources[i];
					if (source == null) return -1;
					exact += (long)source.LoadedCount - source.DeliveredCount - source.LostCount;
					if (exact < 0L || exact > int.MaxValue) return -1;
				}
				return (int)exact;
			}
			long value = Operation == null ? -1L : SumSix(Operation.EscrowMud,
				Operation.EscrowBrush, Operation.EscrowTimber, Operation.EscrowStone,
				Operation.EscrowMarble, Operation.EscrowScrap);
			return value < 0L || value > int.MaxValue ? -1 : (int)value;
		}

		public static bool CarryConserved(KingdomCarryOperation Operation)
		{
			if (Operation != null
				&& Operation.AuthorityKind == KingdomCarryAuthorityKind.ExactManifest)
				return ExactCarryConserved(Operation);
			if (Operation == null || !CarryCountsValid(Operation) || Operation.Sources == null) return false;
			long[] planned = new long[6];
			long[] removed = new long[6];
			for (int i = 0; i < Operation.Sources.Count; i++)
			{
				KingdomCarrySource source = Operation.Sources[i];
				if (!CarrySourceShape(source, Operation, i, false)) return false;
				if (!CheckedAccumulate(planned, source.Material, source.PlannedCount)
					|| !CheckedAccumulate(removed, source.Material, source.Removed)) return false;
				long physical = (long)source.OriginalCount - source.Removed;
				if (physical < 0L || (long)source.OriginalCount != physical + source.Removed) return false;
			}
			long[] provedOutput = new long[6];
			long[] skippedOutput = new long[6];
			if (Operation.Outputs == null || Operation.Outputs.Count > MaxCarryOutputs) return false;
			for (int i = 0; i < Operation.Outputs.Count; i++)
			{
				KingdomLifecycleProjection output = Operation.Outputs[i];
				if (!CarryOutputShape(output, Operation.Id, i, false)
					|| output.Material < 0 || output.Material >= 6) return false;
				if (output.State == KingdomLifecyclePhysicalState.Proved)
				{
					if (!CheckedAccumulate(provedOutput, output.Material, output.Count)) return false;
				}
				else if (output.State == KingdomLifecyclePhysicalState.Skipped)
				{
					if (!CheckedAccumulate(skippedOutput, output.Material, output.Count)) return false;
				}
			}
			for (int material = 0; material < 6; material++)
			{
				long frozen = MaterialValue(Operation, material, 0);
				long escrow = MaterialValue(Operation, material, 1);
				long delivered = MaterialValue(Operation, material, 2);
				long lost = MaterialValue(Operation, material, 3);
				if (planned[material] != frozen || removed[material] != escrow + delivered + lost
					|| delivered > provedOutput[material]
					|| (Operation.LostOnRoad ? lost > skippedOutput[material] : lost != 0L))
					return false;
			}
			return true;
		}

		private static bool ExactCarryConserved(KingdomCarryOperation operation)
		{
			if (operation == null || operation.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
				|| !CarryCountsValid(operation) || operation.Sources == null
				|| operation.Outputs == null || operation.Sources.Count != operation.Outputs.Count)
				return false;
			int firstSource = operation.Sources.Count;
			int firstOutput = operation.Outputs.Count;
			for (int i = 0; i < operation.Sources.Count; i++)
			{
				KingdomCarrySource source = operation.Sources[i];
				if (source == null || source.Material != -1
					|| source.PlannedCount != source.OriginalCount
					|| (source.LoadedCount != 0 && source.LoadedCount != source.PlannedCount)
					|| (source.DeliveredCount != 0
						&& source.DeliveredCount != source.PlannedCount)
					|| (source.LostCount != 0 && source.LostCount != source.PlannedCount)
					|| source.DeliveredCount + source.LostCount > source.LoadedCount) return false;
				if (firstSource == operation.Sources.Count && source.LoadedCount == 0) firstSource = i;
				if (firstOutput == operation.Outputs.Count
					&& source.DeliveredCount == 0 && source.LostCount == 0) firstOutput = i;
			}
			if (operation.SourceIndex != firstSource || operation.OutputIndex != firstOutput) return false;
			for (int material = 0; material < 6; material++)
				if (MaterialValue(operation, material, 0) != 0
					|| MaterialValue(operation, material, 1) != 0
					|| MaterialValue(operation, material, 2) != 0
					|| MaterialValue(operation, material, 3) != 0) return false;
			return true;
		}

	}
}
