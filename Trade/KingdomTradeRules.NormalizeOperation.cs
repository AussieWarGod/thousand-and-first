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
		private static void NormalizeManifest(KingdomTradeBook Book, KingdomTradeManifestState Manifest,
			bool MalformedRealm)
		{
			if (Manifest == null) return;
			bool oversized = TooLong(Manifest.OperationId, MaxIdChars)
				|| TooLong(Manifest.Id, MaxIdChars)
				|| TooLong(Manifest.OriginId, MaxIdChars)
				|| TooLong(Manifest.DestinationId, MaxIdChars)
				|| TooLong(Manifest.OriginName, MaxNameChars)
				|| TooLong(Manifest.DestinationName, MaxNameChars)
				|| TooLong(Manifest.Fault, MaxTextChars);
			bool malformed = MalformedRealm || oversized || Manifest.OperationSequence <= 0L
				|| !string.Equals(Manifest.OperationId,
					OperationId(Book.RealmId, Manifest.OperationSequence), StringComparison.Ordinal)
				|| !string.Equals(Manifest.Id, ManifestId(Manifest.OperationId), StringComparison.Ordinal)
				|| !IdentityContainsSettlement(Book, Manifest.OriginId)
				|| !IdentityContainsSettlement(Book, Manifest.DestinationId) || !ValidName(Manifest.OriginName)
				|| !ValidName(Manifest.DestinationName) || Manifest.OriginalDrams <= 0
				|| Manifest.OriginalDrams > MaxOperationWater || Manifest.EscrowDrams < 0
				|| Manifest.EscrowDrams > Manifest.OriginalDrams || Manifest.LoadedTick < 0L
				|| Manifest.DeadlineTick < Manifest.LoadedTick
				|| !Enum.IsDefined(typeof(KingdomTradeManifestStatus), Manifest.Status)
				|| (Manifest.Status == KingdomTradeManifestStatus.Delivered
					&& Manifest.EscrowDrams != 0)
				|| (Manifest.Status == KingdomTradeManifestStatus.InFlight
					&& Manifest.EscrowDrams == 0);
			if (malformed)
			{
				Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Manifest.Fault = AppendFault(Manifest.Fault, "malformed manifest authority");
			}
		}

		private static void NormalizeOperation(KingdomTradeBook Book)
		{
			KingdomTradeOperation operation = Book.OpenOperation;
			if (operation == null) return;
			if (operation.Sequence <= Book.RetiredThrough)
			{
				if (Book.PendingRetirement != null) return;
				int matches = 0;
				KingdomTradeProof exact = null;
				for (int i = 0; i < Book.RecentProofs.Count; i++)
					if (Book.RecentProofs[i] != null
						&& (Book.RecentProofs[i].Sequence == operation.Sequence
							|| string.Equals(Book.RecentProofs[i].Id, operation.Id, StringComparison.Ordinal)))
					{
						matches++;
						exact = Book.RecentProofs[i];
					}
				if (matches == 1 && ValidProof(Book, exact, true)
					&& ProofMatchesOperation(Book, exact, operation)) Book.OpenOperation = null;
				else
				{
					operation.Phase = KingdomTradePhase.Quarantined;
					operation.Fault = AppendFault(operation.Fault,
						"retirement barrier lacks an exact completed receipt; open evidence was preserved");
					QuarantineBook(Book, operation.Fault);
				}
				return;
			}
			bool oversized = TooLong(operation.Id, MaxIdChars)
				|| TooLong(operation.ZoneId, MaxNameChars)
				|| TooLong(operation.SettlementId, MaxIdChars)
				|| TooLong(operation.SettlementName, MaxNameChars)
				|| TooLong(operation.CharterId, MaxIdChars)
				|| TooLong(operation.ManifestId, MaxIdChars)
				|| TooLong(operation.DealKey, MaxNameChars)
				|| TooLong(operation.DealDisplayName, MaxNameChars)
				|| TooLong(operation.Faction, MaxNameChars)
				|| TooLong(operation.CaravanBlueprint, MaxNameChars)
				|| TooLong(operation.ProjectionId, MaxIdChars)
				|| TooLong(operation.ProjectionObjectId, MaxIdChars)
				|| TooLong(operation.PriorProjectionId, MaxIdChars)
				|| TooLong(operation.PriorProjectionObjectId, MaxIdChars)
				|| TooLong(operation.PriorProjectionZoneId, MaxNameChars)
				|| TooLong(operation.MaterialClaim, MaxClaimChars)
				|| TooLong(operation.OriginId, MaxIdChars)
				|| TooLong(operation.DestinationId, MaxIdChars)
				|| TooLong(operation.OriginName, MaxNameChars)
				|| TooLong(operation.DestinationName, MaxNameChars)
				|| TooLong(operation.Fault, MaxTextChars);
			if (operation.WaterLegs == null || operation.MaterialOutputs == null)
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault, "missing operation evidence list");
				return;
			}
			bool malformed = oversized || operation.Sequence <= 0L
				|| !string.Equals(operation.Id, OperationId(Book.RealmId, operation.Sequence), StringComparison.Ordinal)
				|| !ValidName(operation.ZoneId) || !IdentityContainsSettlement(Book, operation.SettlementId)
				|| !ValidName(operation.SettlementName)
				|| operation.Kind == KingdomTradeOperationKind.None
				|| !Enum.IsDefined(typeof(KingdomTradeOperationKind), operation.Kind)
				|| !Enum.IsDefined(typeof(KingdomTradePhase), operation.Phase)
				|| operation.Phase == KingdomTradePhase.Invalid
				|| operation.CreatedTick < 0L || operation.UpdatedTick < 0L
				|| operation.RequestedWater < 0 || operation.RequestedWater > MaxOperationWater
				|| operation.ProvedWater < 0 || operation.ProvedWater > operation.RequestedWater
				|| operation.AmbiguousWater < 0 || operation.MaterialRequested < 0
				|| operation.MaterialProved < 0 || operation.MaterialProved > operation.MaterialRequested
				|| operation.ManifestEscrowBefore < 0 || operation.ManifestEscrowDebit < 0
				|| operation.ManifestEscrowDebit > operation.ManifestEscrowBefore
				|| operation.ManifestEscrowAfter != operation.ManifestEscrowBefore - operation.ManifestEscrowDebit
				|| operation.RetainedBefore < 0L || operation.RetainedDelta < 0L
				|| operation.RetainedDelta > long.MaxValue - operation.RetainedBefore
				|| operation.RetainedAfter != operation.RetainedBefore + operation.RetainedDelta
				|| !Enum.IsDefined(typeof(KingdomTradeWaterDirection), operation.WaterDirection)
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), operation.ProjectionState)
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), operation.PriorCleanupState)
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), operation.ManifestEscrowState)
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), operation.RetainedState)
				|| operation.WaterLegs.Count > MaxWaterLegs
				|| operation.MaterialOutputs.Count > MaxMaterialOutputs;
			if (!ValidAccountingEvidence(operation)) malformed = true;
			if (!ValidPolityConsignmentOperation(operation)) malformed = true;
			if (!string.IsNullOrEmpty(operation.ProjectionId)
				&& !string.Equals(operation.ProjectionId, ProjectionId(operation.Id), StringComparison.Ordinal)) malformed = true;
			if (operation.Outbox != null && !string.Equals(operation.Outbox.EventId,
				operation.Id, StringComparison.Ordinal)) malformed = true;
			int provedWater = 0;
			int plannedWater = 0;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
			{
				KingdomTradeWaterLeg leg = operation.WaterLegs[i];
				if (leg == null || !NormalizeWaterLeg(leg, operation.WaterDirection)) malformed = true;
				if (leg != null)
				{
					plannedWater = SaturatingAdd(plannedWater, leg.Delta);
					if (leg.State == KingdomTradePhysicalState.Proved)
						provedWater = SaturatingAdd(provedWater, leg.Delta);
					for (int j = 0; j < i; j++)
						if (operation.WaterLegs[j] != null && string.Equals(
							operation.WaterLegs[j].OwnerId, leg.OwnerId,
							StringComparison.Ordinal)) malformed = true;
				}
				if (leg != null && leg.State == KingdomTradePhysicalState.Intent &&
					operation.Kind != KingdomTradeOperationKind.PolityConsignmentDelivery)
				{
					operation.AmbiguousWater = Math.Max(operation.AmbiguousWater,
						operation.RequestedWater - operation.ProvedWater);
					operation.Phase = KingdomTradePhase.Quarantined;
					operation.Fault = AppendFault(operation.Fault,
						"reloaded water intent lacks live part witnesses");
				}
				else if (leg != null && leg.State == KingdomTradePhysicalState.Intent &&
					operation.Phase != KingdomTradePhase.ResourceIntent &&
					operation.Phase != KingdomTradePhase.Quarantined) malformed = true;
			}
			if (plannedWater > operation.RequestedWater || provedWater != operation.ProvedWater)
				malformed = true;
			int provedMaterial = 0;
			int plannedMaterial = 0;
			for (int i = 0; i < operation.MaterialOutputs.Count; i++)
			{
				KingdomTradeMaterialOutput output = operation.MaterialOutputs[i];
				bool createIntent = output != null
					&& output.State == KingdomTradePhysicalState.CreateIntent;
				bool cleanupIntent = output != null
					&& output.CleanupState == KingdomTradePhysicalState.CleanupIntent;
				if (output == null || !NormalizeMaterial(output)) malformed = true;
				if (output != null)
				{
					if (!ValidMaterialMarker(operation.Id, output.Marker)) malformed = true;
					plannedMaterial = SaturatingAdd(plannedMaterial, output.Count);
					if (output.State == KingdomTradePhysicalState.Proved)
						provedMaterial = SaturatingAdd(provedMaterial, output.Count);
					for (int j = 0; j < i; j++)
					{
						KingdomTradeMaterialOutput prior = operation.MaterialOutputs[j];
						if (prior != null && (string.Equals(prior.OutputId, output.OutputId,
							StringComparison.Ordinal) || string.Equals(prior.Marker, output.Marker,
							StringComparison.Ordinal))) malformed = true;
					}
				}
				if (createIntent || cleanupIntent)
				{
					operation.Phase = KingdomTradePhase.Quarantined;
					operation.Fault = AppendFault(operation.Fault,
						"reloaded material creation or cleanup intent is uninspectable and was not replayed");
				}
			}
			if (operation.ProjectionState == KingdomTradePhysicalState.CreateIntent)
			{
				operation.ProjectionState = KingdomTradePhysicalState.Lost;
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"reloaded projection creation intent is uninspectable and was not replayed");
			}
			if (operation.PriorCleanupState == KingdomTradePhysicalState.Intent
				|| operation.PriorCleanupState == KingdomTradePhysicalState.CleanupIntent)
			{
				operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"reloaded projection cleanup intent is uninspectable and was not replayed");
			}
			if (operation.ManifestEscrowState == KingdomTradePhysicalState.Lost
				|| operation.RetainedState == KingdomTradePhysicalState.Lost)
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"unresolved manifest accounting evidence remains open");
			}
			if (plannedMaterial > operation.MaterialRequested
				|| provedMaterial != operation.MaterialProved) malformed = true;
			if (operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& (!ValidId(operation.CharterId) || !ValidName(operation.DealKey)
					|| !ValidName(operation.Faction) || operation.Cycles <= 0
					|| operation.IncomePerCycle < 0
					|| operation.RequestedWater != SaturatingMultiply(operation.IncomePerCycle, operation.Cycles)
					|| operation.IntervalTicks <= 0L || operation.DueBefore < 0L
					|| operation.DueAfter != SaturatingAdd(operation.CreatedTick,
						operation.IntervalTicks))) malformed = true;
			if (operation.Kind != KingdomTradeOperationKind.CharterDelivery
				&& (!ValidId(operation.ManifestId) || !IdentityContainsSettlement(Book, operation.OriginId)
					|| !IdentityContainsSettlement(Book, operation.DestinationId) || !ValidName(operation.OriginName)
					|| !ValidName(operation.DestinationName))) malformed = true;
			if (operation.Kind == KingdomTradeOperationKind.ManifestLoad
				&& !string.Equals(operation.ManifestId, ManifestId(operation.Id), StringComparison.Ordinal)) malformed = true;
			if (InvalidWaterDirection(operation)) malformed = true;
			NormalizeStanding(operation.Standing, ref malformed);
			if (operation.Standing != null
				&& operation.Standing.State == KingdomTradePhysicalState.Intent)
			{
				operation.Standing.State = KingdomTradePhysicalState.Lost;
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"reloaded standing callback intent is uninspectable and was not replayed");
			}
			NormalizeOutbox(operation.Outbox, ref malformed);
			if (operation.Kind == KingdomTradeOperationKind.CharterDelivery)
			{
				if (operation.Pattern == null
					|| !KingdomTradePatternRules.Normalize(operation.Pattern)) malformed = true;
				bool terminalLane = operation.Phase == KingdomTradePhase.ScheduleIntent
					|| operation.Phase == KingdomTradePhase.RetirementReady
					|| operation.Phase == KingdomTradePhase.Terminal;
				if (operation.Phase == KingdomTradePhase.Quarantined)
				{
					if (operation.Outbox != null && !CharterOutboxLaneShape(operation)
						&& !QuarantineCharterOutboxLaneShape(operation)) malformed = true;
				}
				else
				{
					if ((operation.Outbox != null && !CharterOutboxLaneShape(operation))
						|| ((operation.Phase == KingdomTradePhase.Sinks || terminalLane)
							&& operation.Outbox == null)
						|| (terminalLane && !TerminalCharterOutboxExact(operation))) malformed = true;
				}
			}
			else if (operation.Pattern != null) malformed = true;
			if (operation.Outbox != null && (operation.Outbox.ChronicleState == KingdomTradeSinkState.Lost
				|| operation.Outbox.LedgerState == KingdomTradeSinkState.Lost
				|| operation.Outbox.MessageState == KingdomTradeSinkState.Lost
				|| operation.Outbox.DeedState == KingdomTradeSinkState.Lost))
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"reloaded outbox intent has an unresolved external effect");
			}
			bool exactPendingIdentity = Book.PendingRetirement != null
				&& Book.PendingRetirement.Sequence == operation.Sequence
				&& string.Equals(Book.PendingRetirement.Id, operation.Id, StringComparison.Ordinal);
			if (operation.Phase == KingdomTradePhase.Terminal && !exactPendingIdentity)
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"terminal operation remained open past retirement");
			}
			if (malformed)
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault, "malformed open trade operation");
			}
		}

	}
}
