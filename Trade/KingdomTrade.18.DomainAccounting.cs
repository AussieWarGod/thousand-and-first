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
		private static bool SettleDomain(KingdomSystem System, KingdomTradeBook Book,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			Operation.Phase = KingdomTradePhase.DomainIntent;
			if (!ExactAuthority(Frame, KingdomTradePhase.DomainIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
				return QuarantineFalse(Operation,
					"The domain frame changed before its exact settlement CAS.");
			switch (Operation.Kind)
			{
			case KingdomTradeOperationKind.CharterDelivery:
				if (!SettleStanding(System, Operation, Frame)) return false;
				if (Operation.ProjectionState == KingdomTradePhysicalState.Proved)
				{
					if (!PublishProjectionRow(Book, Operation))
						return QuarantineFalse(Operation,
							"The per-city caravan projection lost its exact before/after CAS.");
					RefreshProjectionRows(Frame);
				}
				break;
			case KingdomTradeOperationKind.ManifestLoad:
				if (Book.Manifest != null || Operation.ProvedWater != Operation.RequestedWater)
				{
					Quarantine(Operation, "Manifest publication lost its exact empty-slot or debit proof.");
					return false;
				}
					Book.Manifest = new KingdomTradeManifestState
					{
						OperationSequence = Operation.Sequence,
						OperationId = Operation.Id,
						Id = Operation.ManifestId,
					OriginId = Operation.OriginId,
					OriginName = Operation.OriginName,
					DestinationId = Operation.DestinationId,
					DestinationName = Operation.DestinationName,
					OriginalDrams = Operation.RequestedWater,
					EscrowDrams = Operation.ProvedWater,
					LoadedTick = Operation.ManifestLoadedTick,
					DeadlineTick = Operation.ManifestDeadlineTick,
					Status = KingdomTradeManifestStatus.InFlight
				};
				break;
			case KingdomTradeOperationKind.ManifestDelivery:
				if (!ExactManifestIdentity(Book.Manifest, Operation)
					|| (Operation.ManifestEscrowState == KingdomTradePhysicalState.Prepared
						&& Book.Manifest.EscrowDrams != Operation.ManifestEscrowBefore))
					return QuarantineFalse(Operation,
					"Manifest delivery no longer owns the exact escrow row.");
				if (!SettleManifestCreditAccounting(Book, Operation)) return false;
				break;
			case KingdomTradeOperationKind.ManifestTurnback:
				if (!ExactManifest(Book.Manifest, Operation) || Book.Manifest.TurnedBack)
					return QuarantineFalse(Operation, "Manifest turnback lost its exact route CAS.");
				string originId = Book.Manifest.OriginId;
				string originName = Book.Manifest.OriginName;
				Book.Manifest.OriginId = Book.Manifest.DestinationId;
				Book.Manifest.OriginName = Book.Manifest.DestinationName;
				Book.Manifest.DestinationId = originId;
				Book.Manifest.DestinationName = originName;
				Book.Manifest.TurnedBack = true;
				Book.Manifest.LoadedTick = Operation.ManifestLoadedTick;
				Book.Manifest.DeadlineTick = Operation.ManifestDeadlineTick;
				break;
			case KingdomTradeOperationKind.ManifestLapse:
				if (!ExactManifest(Book.Manifest, Operation) || !Book.Manifest.TurnedBack)
					return QuarantineFalse(Operation, "Manifest lapse lost its exact escrow CAS.");
				if (!SettleRetainedAccounting(Book, Operation)) return false;
				Book.Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Book.Manifest.Fault = "Both road windows closed; escrow remains retained under its permanent receipt.";
				break;
			case KingdomTradeOperationKind.PolityConsignmentDelivery:
				if (!RequirePolityConsignmentRecipient(System, Operation, Frame.Zone,
					"domain landing")) return false;
				if (Operation.ProvedWater < 1 ||
					Operation.ProvedWater > Operation.RequestedWater)
					return QuarantineFalse(Operation,
						"Polity consignment lacks a bounded exact physical debit proof.");
				break;
			}
			RefreshBookDomain(Frame);
			// Domain state is now externally visible to outbox callbacks. Publish compatibility
			// projection before any callback can read documented legacy API.
			System.SynchronizeLegacyManifestProjection();
			if (!ExactAuthority(Frame, KingdomTradePhase.DomainIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
				return QuarantineFalse(Operation,
					"The domain settlement CAS changed its exact authority or physical frame.");
			if (!RequirePolityConsignmentRecipient(System, Operation, Frame.Zone,
				"DomainSettled publication")) return false;
			Operation.Phase = KingdomTradePhase.DomainSettled;
			return true;
		}

		private static bool SettleManifestCreditAccounting(KingdomTradeBook Book,
			KingdomTradeOperation Operation)
		{
			KingdomTradeManifestState manifest = Book?.Manifest;
			if (manifest == null || Operation == null || Operation.ProvedWater < 0
				|| Operation.ProvedWater > Operation.ManifestEscrowBefore)
				return QuarantineFalse(Operation, "Manifest credit accounting lacks exact escrow evidence.");
			if (Operation.ManifestEscrowState == KingdomTradePhysicalState.Prepared)
			{
				Operation.ManifestEscrowDebit = Operation.ProvedWater;
				Operation.ManifestEscrowAfter = Operation.ManifestEscrowBefore - Operation.ProvedWater;
				Operation.ManifestEscrowState = KingdomTradePhysicalState.Intent;
			}
			if (Operation.ManifestEscrowState == KingdomTradePhysicalState.Intent)
			{
				int after;
				bool apply;
				if (!KingdomTradeRules.TryReconcileEscrow(Operation.ManifestEscrowBefore,
					Operation.ManifestEscrowDebit, manifest.EscrowDrams, out after, out apply)
					|| after != Operation.ManifestEscrowAfter)
				{
					Operation.ManifestEscrowState = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation,
						"Manifest escrow is neither exact before nor exact after; credit remains unresolved.");
				}
				if (apply) manifest.EscrowDrams = after;
				Operation.ManifestEscrowState = KingdomTradePhysicalState.Proved;
			}
			if (Operation.ManifestEscrowState != KingdomTradePhysicalState.Proved
				|| manifest.EscrowDrams != Operation.ManifestEscrowAfter)
			{
				Operation.ManifestEscrowState = KingdomTradePhysicalState.Lost;
				return QuarantineFalse(Operation, "Manifest escrow debit proof was lost.");
			}
			if (manifest.EscrowDrams == 0) manifest.Status = KingdomTradeManifestStatus.Delivered;
			return true;
		}

		private static bool SettleRetainedAccounting(KingdomTradeBook Book,
			KingdomTradeOperation Operation)
		{
			if (Book == null || Operation == null) return false;
			if (Operation.RetainedState == KingdomTradePhysicalState.Prepared)
				Operation.RetainedState = KingdomTradePhysicalState.Intent;
			if (Operation.RetainedState == KingdomTradePhysicalState.Intent)
			{
				long after;
				bool apply;
				if (!KingdomTradeRules.TryReconcileRetained(Operation.RetainedBefore,
					Operation.RetainedDelta, Book.RetainedEscrowDrams, out after, out apply)
					|| after != Operation.RetainedAfter)
				{
					Operation.RetainedState = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation,
						"Retained escrow is neither exact before nor exact after; value remains unresolved.");
				}
				if (apply) Book.RetainedEscrowDrams = after;
				Operation.RetainedState = KingdomTradePhysicalState.Proved;
			}
			if (Operation.RetainedState != KingdomTradePhysicalState.Proved
				|| Book.RetainedEscrowDrams != Operation.RetainedAfter)
			{
				Operation.RetainedState = KingdomTradePhysicalState.Lost;
				return QuarantineFalse(Operation, "Retained escrow proof was lost.");
			}
			return true;
		}

		private static bool PublishProjectionRow(KingdomTradeBook Book,
			KingdomTradeOperation Operation)
		{
			KingdomTradeProjectionRow current;
			if (!TryProjectionRow(Book, Operation.SettlementId, out current)) return false;
			if (string.IsNullOrEmpty(Operation.PriorProjectionId))
			{
				if (current != null || Book.Projections.Count >= KingdomTradeRules.MaxProjectionRows)
					return false;
				Book.Projections.Add(new KingdomTradeProjectionRow
					{
						OperationSequence = Operation.Sequence,
						OperationId = Operation.Id,
					SettlementId = Operation.SettlementId,
					ZoneId = Operation.ZoneId,
					ProjectionId = Operation.ProjectionId,
					ObjectId = Operation.ProjectionObjectId
				});
				return true;
			}
			if (current == null || current.Quarantined
				|| !string.Equals(current.ZoneId, Operation.PriorProjectionZoneId,
					StringComparison.Ordinal)
				|| !string.Equals(current.ProjectionId, Operation.PriorProjectionId,
					StringComparison.Ordinal)
				|| !string.Equals(current.ObjectId, Operation.PriorProjectionObjectId,
					StringComparison.Ordinal)) return false;
			current.ZoneId = Operation.ZoneId;
			current.OperationSequence = Operation.Sequence;
			current.OperationId = Operation.Id;
			current.ProjectionId = Operation.ProjectionId;
			current.ObjectId = Operation.ProjectionObjectId;
			return true;
		}

		private static bool SettleStanding(KingdomSystem System,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			KingdomTradeStandingCas standing = Operation.Standing;
			if (standing == null) return true;
			if (!ExactAuthority(Frame, KingdomTradePhase.DomainIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
				return QuarantineFalse(Operation,
					"The standing frame changed before its exact callback.");
			int current = System.GetRegardForRealm(standing.Faction);
			if (current == standing.After)
			{
				standing.State = KingdomTradePhysicalState.Proved;
				return true;
			}
			if (current != standing.Before)
			{
				return QuarantineFalse(Operation,
					"Standing changed outside the frozen before/delta/after CAS; it was not overwritten.");
			}
			standing.State = KingdomTradePhysicalState.Intent;
			CallbackWitness callback = CaptureCallbackWitness(Frame);
			if (callback == null)
			{
				standing.State = KingdomTradePhysicalState.Lost;
				return QuarantineFalse(Operation, "Standing callback frame could not be frozen.");
			}
			System.SetRegardForRealm(standing.Faction, standing.After);
			if (!ExactCallbackWitness(Frame, callback)
				|| !ReferenceEquals(Frame.System.TradeBook, Frame.Book)
				|| !ReferenceEquals(Frame.Book.OpenOperation, Operation)
				|| Operation.Phase != KingdomTradePhase.DomainIntent)
				return FailDetachedAuthority(Frame,
					"A standing callback detached its official trade authority.");
			if (standing.State != KingdomTradePhysicalState.Intent
				|| System.GetRegardForRealm(standing.Faction) != standing.After
				|| !ExactStandingWithOverride(Frame, standing.Faction, standing.After)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone)
				|| !ExactSettlement(Frame))
				return QuarantineFalse(Operation, "Standing CAS did not leave its exact after value.");
			Frame.StandingRows[standing.Faction] = standing.After;
			standing.State = KingdomTradePhysicalState.Proved;
			return ExactAuthority(Frame, KingdomTradePhase.DomainIntent);
		}

		private static bool ExactManifest(KingdomTradeManifestState Manifest,
			KingdomTradeOperation Operation)
		{
			return ExactManifestIdentity(Manifest, Operation)
				&& string.Equals(Manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
				&& Manifest.EscrowDrams == Operation.RequestedWater;
		}

		private static bool ExactManifestIdentity(KingdomTradeManifestState Manifest,
			KingdomTradeOperation Operation)
		{
			return Manifest != null && Operation != null
				&& (Manifest.Status == KingdomTradeManifestStatus.InFlight
					|| Manifest.Status == KingdomTradeManifestStatus.Delivered)
				&& string.Equals(Manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
				&& string.Equals(Manifest.OriginId, Operation.OriginId, StringComparison.Ordinal)
				&& string.Equals(Manifest.OriginName, Operation.OriginName, StringComparison.Ordinal)
				&& string.Equals(Manifest.DestinationId, Operation.DestinationId, StringComparison.Ordinal)
				&& string.Equals(Manifest.DestinationName, Operation.DestinationName, StringComparison.Ordinal);
		}

	}
}
