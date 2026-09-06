using System;
using XRL;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDepartureRuntime
	{
		private static bool TrySettleChronicle(KingdomSystem system, KingdomResidentDepartureOperation operation)
		{
			var game = The.Game;
			KingdomLedger ledger = system?.Ledger;
			var notes = ledger?.Notes;
			if (game == null || ledger == null || notes == null || operation == null) return false;
			string archive = system.ResidentDepartureCapacityWarnings;
			KingdomResidentDepartureOperation frozen = operation.Copy();
			Func<bool> exact = () => ReferenceEquals(The.Game, game)
				&& ReferenceEquals(game.GetSystem<KingdomSystem>(), system)
				&& ReferenceEquals(system.ResidentDeparture, operation)
				&& KingdomResidentDepartureRules.Valid(operation)
				&& operation.Phase == (int)KingdomResidentDeparturePhase.RolesClosed
				&& operation.Version == frozen.Version && operation.Phase == frozen.Phase
				&& operation.Revision == frozen.Revision && operation.OperationId == frozen.OperationId
				&& operation.RealmId == frozen.RealmId && operation.SettlementId == frozen.SettlementId
				&& operation.ResidentId == frozen.ResidentId && operation.BodyObjectId == frozen.BodyObjectId
				&& operation.ZoneId == frozen.ZoneId && operation.ResidentName == frozen.ResidentName
				&& operation.PreparedTick == frozen.PreparedTick && operation.DeparturesBefore == frozen.DeparturesBefore
				&& operation.Chronicled == frozen.Chronicled && operation.ChronicleLine == frozen.ChronicleLine
				&& operation.LedgerLine == frozen.LedgerLine
				&& operation.Origin == frozen.Origin && operation.Cause == frozen.Cause
				&& operation.PolityConclusionRef == frozen.PolityConclusionRef
				&& operation.AuthorizationKind == frozen.AuthorizationKind
				&& operation.AuthorizationEventId == frozen.AuthorizationEventId
				&& operation.AuthorizationOwnerObjectId == frozen.AuthorizationOwnerObjectId
				&& operation.AuthorizationCauseDigest == frozen.AuthorizationCauseDigest
				&& SameCook(operation.PriorCook, frozen.PriorCook)
				&& (operation.PriorCook == null
					|| operation.PriorCook.SettlementName == frozen.PriorCook.SettlementName
					&& operation.PriorCook.ResidentName == frozen.PriorCook.ResidentName
					&& operation.PriorCook.RecipeDisplayName == frozen.PriorCook.RecipeDisplayName
					&& operation.PriorCook.EffectId == frozen.PriorCook.EffectId
					&& operation.PriorCook.ReleasedTick == frozen.PriorCook.ReleasedTick
					&& operation.PriorCook.Fault == frozen.PriorCook.Fault)
				&& SameOffice(operation.PriorOffice, frozen.PriorOffice)
				&& (operation.PriorOffice == null
					|| operation.PriorOffice.VacancyCause == frozen.PriorOffice.VacancyCause
					&& operation.PriorOffice.SettlementName == frozen.PriorOffice.SettlementName
					&& operation.PriorOffice.PredecessorResidentId == frozen.PriorOffice.PredecessorResidentId
					&& operation.PriorOffice.PredecessorName == frozen.PriorOffice.PredecessorName
					&& operation.PriorOffice.Fault == frozen.PriorOffice.Fault)
				&& SamePolity(operation.PriorPolity, frozen.PriorPolity)
				&& system.TryGetCurrentIdentity(out string realm, out string settlement)
				&& realm == frozen.RealmId && settlement == frozen.SettlementId
				&& ReferenceEquals(system.Ledger, ledger) && ReferenceEquals(ledger.Notes, notes)
				&& ledger.Departures == operation.DeparturesBefore + 1
				&& string.Equals(system.ResidentDepartureCapacityWarnings, archive, StringComparison.Ordinal);
			KingdomChronicle.CapacityObservation capacity = null;
			return KingdomResidentDepartureStoryRules.TrySettle(operation, archive, exact,
				() => KingdomChronicle.TryObserveCapacityRefusal(system,
					KingdomResidentDepartureCapacityArchive.EventId(operation), operation.ChronicleLine, exact, out capacity)
					? capacity.Witness : null,
				() => exact() && KingdomChronicle.ReproveCapacityRefusal(capacity, exact),
				next =>
				{
					if (!exact()) return false;
					system.ResidentDepartureCapacityWarnings = next;
					archive = next;
					return exact();
				},
				() => KingdomChronicle.RecordOnce(system, operation.OperationId + ":chronicle", operation.ChronicleLine));
		}
	}
}
