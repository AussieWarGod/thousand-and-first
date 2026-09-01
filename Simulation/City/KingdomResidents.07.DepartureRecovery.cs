using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		/// <summary>Forward-completes only the carriers named by the realm's exact departure
		/// journal. Missing one side is a retry cut; a foreign or duplicate side is refusal.</summary>
		internal static bool TryCompleteDepartureCarriers(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation,
			KingdomResidentDestructionAuthorization Authorization,
			out KingdomResidentRow FormerRow, out string Failure)
		{
			FormerRow = default(KingdomResidentRow); Failure = null;
			if (!KingdomResidentDepartureRules.Valid(Operation) || System?.Bindings == null
				|| !GameObject.Validate(Body) || Body.IDIfAssigned != Operation.BodyObjectId
				|| Body.GetIntProperty(ResidentIdProperty) != Operation.ResidentId
				|| Body.CurrentZone?.ZoneID != Operation.ZoneId
				|| System.CurrentRealmId != Operation.RealmId
				|| System.SettlementIdForOwnedZone(Operation.ZoneId) != Operation.SettlementId)
			{
				Failure = "departure recovery identity is divergent"; return false;
			}
			if (!TryFindSettlementBook(System, Operation.SettlementId,
				out KingdomCityBook intended)) return false;
			KingdomCityState intendedState = null;
			int rowMatches = 0;
			List<KingdomCityBook> books = System.OwnedCityBooks();
			for (int i = 0; books != null && i < books.Count; i++)
			{
				KingdomCityBook book = books[i];
				if (book == null || !book.TryRead(out KingdomCityState state,
					out KingdomCityFault _)) return false;
				if (ReferenceEquals(book, intended)) intendedState = state;
				if (!state.TryResidentIndex(Operation.ResidentId, out int at)) continue;
				rowMatches++;
				if (!ReferenceEquals(book, intended)
					|| !state.TryResident(at, out KingdomResidentRow row)
					|| !ExactDepartureRow(row, Operation)) return false;
				FormerRow = row;
			}
			if (rowMatches > 1 || intendedState == null) return false;

			if (!System.Bindings.TryRead(out KingdomBindingTable bindings,
				out KingdomCityFault _) || !bindings.TryAudit(out KingdomCityFault _)) return false;
			bool hasBinding = bindings.TryGet(Operation.ResidentId,
				KingdomBindingKind.Resident, out KingdomBinding held);
			if (hasBinding && (held.ObjectId != Operation.BodyObjectId
				|| held.ZoneId != Operation.ZoneId)) return false;
			// A cut may leave only one carrier. Re-project every non-carrier claim before
			// removing that survivor; a role acquired after selection must cause zero mutation.
			if (!KingdomResidentTransitionAuthority.CanContinueJournaledCarrierRemoval(
				System, Body, Operation, Authorization))
			{
				Failure = "departure acquired a claim before carrier completion"; return false;
			}

			if (rowMatches == 1 && hasBinding)
			{
				if (!TryDepart(System, Body, Authorization, Operation,
					out FormerRow)) return false;
				return DepartureCarriersAbsent(System, intended, Operation.ResidentId);
			}
			if (rowMatches == 1)
			{
				if (!KingdomResidentRules.TryRemove(intendedState, Operation.ResidentId,
					out KingdomCityState next, out FormerRow, out KingdomCityFault _)
					|| !SafePublish(intended, next, "departure recovery city")) return false;
			}
			if (hasBinding)
			{
				if (!KingdomResidentTransitionAuthority.CanContinueJournaledCarrierRemoval(
					System, Body, Operation, Authorization))
				{
					Failure = "departure acquired a claim before binding completion";
					return false;
				}
				if (!bindings.TryUnbind(Operation.ResidentId, KingdomBindingKind.Resident,
					KingdomUnbindCause.Abroad, out KingdomBindingTable next,
					out KingdomBinding _, out KingdomCityFault _)
					|| !SafePublish(System.Bindings, next,
						"departure recovery registry")) return false;
			}
			ProjectCompatibility(System);
			return DepartureCarriersAbsent(System, intended, Operation.ResidentId);
		}

		private static bool TryFindSettlementBook(KingdomSystem System, string SettlementId,
			out KingdomCityBook Book)
		{
			Book = null;
			if (!System.TryFindSettlement(SettlementId, out bool seated,
				out KingdomSettlement settlement)) return false;
			Book = seated ? System.City : settlement?.City;
			return Book != null && Book.SettlementId == SettlementId;
		}

		private static bool ExactDepartureRow(KingdomResidentRow Row,
			KingdomResidentDepartureOperation Operation)
		{
			return Row.ResidentId == Operation.ResidentId
				&& Row.Standing == KingdomResidentStanding.Resident
				&& Row.Name == Operation.ResidentName
				&& (Row.Origin ?? "") == Operation.Origin
				&& Row.BoundZoneId == Operation.ZoneId;
		}

		internal static bool DepartureCarriersAbsent(KingdomSystem System,
			KingdomCityBook Book, int ResidentId)
		{
			if (Book == null || System?.Bindings == null
				|| !Book.TryRead(out KingdomCityState state, out KingdomCityFault _)
				|| !System.Bindings.TryRead(out KingdomBindingTable bindings,
					out KingdomCityFault _)) return false;
			return !state.TryResidentIndex(ResidentId, out int _)
				&& !bindings.TryGet(ResidentId, KingdomBindingKind.Resident,
					out KingdomBinding _);
		}
	}
}
