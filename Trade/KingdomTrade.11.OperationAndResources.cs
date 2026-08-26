using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		private static void ContinueOperation(KingdomSystem System, KingdomTradeBook Book,
			Zone Z, KingdomSurvey Survey, long Now)
		{
			KingdomTradeOperation operation = Book.OpenOperation;
			if (operation == null) return;
			if (!TryBindTopologyGround(System, Z, Survey))
			{
				KingdomLog.Log("trade: open receipt " + (operation.Id ?? "?")
					+ " deferred; exact active settlement ground is unavailable");
				return;
			}
			TradeLiveFrame frame;
			if (!TryBindFrame(System, Book, operation, Z, out frame))
			{
				if (!ReferenceEquals(System.TradeBook, Book)
					|| !ReferenceEquals(Book.OpenOperation, operation))
				{
					FailDetachedAuthority(new TradeLiveFrame
					{
						System = System, Book = Book, Operation = operation,
						Charters = Book.Charters, RealmId = Book.RealmId, Zone = Z
					}, "A trade callback detached the official operation before resume.");
				}
				else
				{
					KingdomLog.Log("trade: open receipt " + (operation.Id ?? "?")
						+ " remains bound to " + (operation.SettlementName ?? "?")
						+ "/" + (operation.ZoneId ?? "?") + "; refused resume here");
				}
				return;
			}
			if (operation.Phase == KingdomTradePhase.Quarantined)
			{
				FinalizeQuarantine(System, Book, operation, Now, frame);
				return;
			}
			if (operation.Phase >= KingdomTradePhase.ResourceSettled
				&& (!TryBindPersistedPhysicalFrame(frame, operation, Z, Survey)
					|| !TryBindProjectionFrame(frame, operation, Z)))
			{
				ReconcilePhysicalFailure(frame, operation, Z,
					"A resumed trade receipt could not bind its exact physical frame.");
				FinalizeQuarantine(System, Book, operation, Now, frame);
				return;
			}
			if (operation.Phase == KingdomTradePhase.Prepared
				|| operation.Phase == KingdomTradePhase.ResourceIntent)
			{
				if (!SettleResources(operation, Z, Survey, frame))
				{
					if (operation.Phase == KingdomTradePhase.Quarantined)
						FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
			}
			if (operation.Phase == KingdomTradePhase.ResourceSettled
				|| operation.Phase == KingdomTradePhase.ProjectionIntent)
			{
				SettleProjection(operation, Z, frame);
				if (operation.Phase == KingdomTradePhase.Quarantined)
				{
					FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				if (operation.Phase == KingdomTradePhase.ProjectionIntent) return;
			}
			if (operation.Phase == KingdomTradePhase.ProjectionSettled
				|| operation.Phase == KingdomTradePhase.DomainIntent)
			{
				if (!SettleDomain(System, Book, operation, frame))
				{
					FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
			}
			if (operation.Phase == KingdomTradePhase.DomainSettled)
			{
				BuildOutbox(System, operation);
				if (operation.Kind == KingdomTradeOperationKind.CharterDelivery
					&& !KingdomTradeRules.CharterOutboxReadyForDispatch(operation))
				{
					Quarantine(operation,
						"The mandatory Charter outbox was malformed before external dispatch.");
					return;
				}
				operation.Phase = KingdomTradePhase.Sinks;
			}
			if (operation.Phase == KingdomTradePhase.Sinks)
			{
				if (operation.Kind == KingdomTradeOperationKind.CharterDelivery
					&& !KingdomTradeRules.CharterOutboxReadyForDispatch(operation))
				{
					Quarantine(operation,
						"The mandatory Charter outbox changed before external dispatch.");
					return;
				}
				if (!DispatchOutbox(System, operation, frame))
				{
					if (operation.Phase == KingdomTradePhase.Quarantined)
						FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				if (!OutboxSettled(operation.Outbox)) return;
				operation.Phase = KingdomTradePhase.ScheduleIntent;
			}
			if (operation.Phase == KingdomTradePhase.ScheduleIntent)
			{
				if (operation.Kind == KingdomTradeOperationKind.CharterDelivery
					&& !ContinuePatternBook(System, operation, frame))
				{
					if (operation.Phase == KingdomTradePhase.Quarantined)
						FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				if (!ExactPhysicalFrame(frame, operation, Z))
				{
					ReconcilePhysicalFailure(frame, operation, Z,
						"The final physical checkpoint no longer matched its exact witnesses.");
					FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				RefreshSurveyWater(frame.Physical);
				if (!SettleSchedule(Book, operation, frame))
				{
					FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				KingdomTradePhase disposition = string.IsNullOrEmpty(operation.Fault)
					? KingdomTradePhase.Terminal : KingdomTradePhase.Quarantined;
				operation.Phase = KingdomTradePhase.RetirementReady;
				KingdomTradeRules.Retire(Book, operation, disposition, Now, operation.Fault);
				System.SynchronizeLegacyManifestProjection();
			}
		}

		private static bool SettleResources(KingdomTradeOperation Operation,
			Zone Z, KingdomSurvey Survey, TradeLiveFrame Frame)
		{
			if (Operation.Kind == KingdomTradeOperationKind.ManifestTurnback
				|| Operation.Kind == KingdomTradeOperationKind.ManifestLapse)
			{
				Operation.Phase = KingdomTradePhase.ResourceSettled;
				return true;
			}
			if (Operation.Phase == KingdomTradePhase.ResourceIntent)
			{
				if (!TryBindPersistedPhysicalFrame(Frame, Operation, Z, Survey))
				{
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"The persisted resource frame could not bind its exact live owners and parts.");
					return false;
				}
				if (!ResumePreparedWater(Operation, Z, Frame)) return false;
				if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery
					&& Operation.MaterialRequested > 0)
				{
					if (Operation.MaterialOutputs.Count == 0)
					{
						// Water was proved before the material sub-lane published an Add intent.
						// Starting that still-unstarted lane is safe; no output ID existed to replay.
						if (!ApplyMaterials(Operation, Z, Frame)) return false;
					}
					else if (!ReconcileMaterialOutputs(Operation, Z, Frame)) return false;
				}
				Operation.Phase = KingdomTradePhase.ResourceSettled;
				return true;
			}
			if (Survey == null || Z == null || !string.Equals(Operation.ZoneId,
				Z.ZoneID, StringComparison.Ordinal))
			{
				Quarantine(Operation, "The prepared resource zone is not loaded exactly.");
				return false;
			}
			if (!ApplyWater(Operation, Z, Survey, Frame)) return false;
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& Operation.MaterialRequested > 0)
			{
				if (!ApplyMaterials(Operation, Z, Frame)) return false;
			}
			Operation.Phase = KingdomTradePhase.ResourceSettled;
			return true;
		}

	}
}
