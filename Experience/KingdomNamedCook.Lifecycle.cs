using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Witnessed resident-loss, predecessor history, and active-ground recovery seams.</summary>
	public static partial class KingdomNamedCook
	{
		/// <summary>Stages a departure cause before the resident row/binding transaction. No body
		/// part changes here; a refused resident publication can therefore restore Prior exactly.</summary>
		internal static bool PrepareCookLoss(KingdomSystem System, GameObject Body,
			KingdomNamedCookVacancyCause Cause, out KingdomNamedCookReceipt Prior,
			out string Failure)
		{
			Prior = null; Failure = null;
			if (Cause != KingdomNamedCookVacancyCause.Departure || Body == null) return true;
			if (!TryFindCookBook(System, Body.IDIfAssigned, out KingdomCityBook book, out Failure))
				return false;
			if (book == null) return true;
			KingdomNamedCookReceipt current = book.NamedCook;
			if (current != null && KingdomNamedCookRules.IsVacant(current.Phase)) return true;
			if (!KingdomNamedCookRules.Validate(current, out Failure)
				|| current.Phase != KingdomNamedCookPhase.Applied)
			{
				if (string.IsNullOrEmpty(Failure))
					Failure = "The exact cook appointment is not departure-preparable.";
				return false;
			}
			KingdomNamedCookReceipt prepared = KingdomNamedCookRules.BeginVacancy(current, Cause);
			if (prepared == null)
			{
				Failure = "The exact cook departure could not be prepared."; return false;
			}
			Prior = current.Copy(); book.NamedCook = prepared; return true;
		}

		internal static bool CancelPreparedCookLoss(KingdomSystem System, GameObject Body,
			KingdomNamedCookReceipt Prior, KingdomNamedCookVacancyCause Cause,
			out string Failure)
		{
			Failure = null;
			if (Prior == null) return true;
			if (Cause != KingdomNamedCookVacancyCause.Departure || Body == null
				|| !KingdomNamedCookRules.Validate(Prior, out Failure)
				|| Prior.Phase != KingdomNamedCookPhase.Applied
				|| !TryFindCookBook(System, Body.IDIfAssigned, out KingdomCityBook book, out Failure)
				|| book == null) return false;
			KingdomNamedCookReceipt current = book.NamedCook;
			if (!KingdomNamedCookRules.Validate(current, out Failure)
				|| !KingdomNamedCookRules.IsVacancyPrepared(current.Phase)
				|| KingdomNamedCookRules.VacancyCause(current.Phase) != Cause
				|| !SameAppointment(current, Prior))
			{
				if (string.IsNullOrEmpty(Failure))
					Failure = "The prepared cook departure lost its rollback CAS.";
				return false;
			}
			book.NamedCook = Prior.Copy(); return true;
		}

		internal static bool ObserveCookLoss(KingdomSystem System, GameObject Body,
			KingdomNamedCookVacancyCause Cause, out string Failure)
		{
			Failure = null;
			if (System == null || Body == null || (Cause != KingdomNamedCookVacancyCause.Death
				&& Cause != KingdomNamedCookVacancyCause.Departure)) return true;
			if (!TryFindCookBook(System, Body.IDIfAssigned, out KingdomCityBook match,
				out Failure)) return false;
			if (match == null) return true;
			KingdomNamedCookReceipt authority = match.NamedCook;
			if (!KingdomNamedCookRules.Validate(authority, out Failure)
				|| authority.Phase == KingdomNamedCookPhase.Quarantined) return false;
			if (KingdomNamedCookRules.IsVacant(authority.Phase))
				return TellVacancy(System, authority, out Failure);
			if (!KingdomNamedCookRules.IsVacancyPrepared(authority.Phase))
			{
				KingdomNamedCookReceipt prepared = KingdomNamedCookRules.BeginVacancy(authority,
					Cause);
				if (prepared == null)
				{
					Failure = "The witnessed cook vacancy could not be prepared."; return false;
				}
				match.NamedCook = authority = prepared;
			}
			else if (KingdomNamedCookRules.VacancyCause(authority.Phase) != Cause)
			{
				Failure = "The cook receipt already carries a different witnessed vacancy.";
				return false;
			}
			if (!RemoveProjection(match, Body, authority, out Failure)) return false;
			return TellVacancy(System, match.NamedCook, out Failure);
		}

		private static bool RepairWitnessedLossOnActiveGround(KingdomSystem System,
			KingdomCityBook Book, Zone Zone, out string Failure)
		{
			Failure = null;
			KingdomNamedCookReceipt row = Book?.NamedCook;
			if (row == null || row.Phase == KingdomNamedCookPhase.None
				|| row.Phase == KingdomNamedCookPhase.Quarantined
				|| KingdomNamedCookRules.IsVacant(row.Phase)) return true;
			GameObject body = FindExactOnGround(Zone, row.BodyObjectId);
			if (body == null) return true;
			if (KingdomNamedCookRules.IsVacancyPrepared(row.Phase))
			{
				if (KingdomNamedCookRules.VacancyCause(row.Phase)
					== KingdomNamedCookVacancyCause.Departure
					&& StandingResident(Book, row.ResidentId))
				{
					r_KingdomNamedCook marker = body.GetPart<r_KingdomNamedCook>();
					if (marker == null || !marker.Matches(row, body)
						|| !ExactTeaching(body.GetPart<XRL.World.Parts.TeachesDish>(), row))
					{
						Failure = "A staged cook departure has neither exact rollback nor published loss.";
						return false;
					}
					KingdomNamedCookReceipt restored = KingdomNamedCookRules.CancelVacancy(row,
						KingdomNamedCookVacancyCause.Departure);
					if (restored == null)
					{
						Failure = "The unpublished cook departure could not restore its service.";
						return false;
					}
					marker.Stamp(restored); Book.NamedCook = restored; return true;
				}
				if (!RemoveProjection(Book, body, row, out Failure)) return false;
				return TellVacancy(System, Book.NamedCook, out Failure);
			}
			if (!WitnessedDead(Book, row.ResidentId)) return true;
			return ObserveCookLoss(System, body, KingdomNamedCookVacancyCause.Death, out Failure);
		}

		private static bool TryFindCookBook(KingdomSystem System, string ObjectId,
			out KingdomCityBook Match, out string Failure)
		{
			Match = null; Failure = null;
			if (System == null || string.IsNullOrEmpty(ObjectId)) return true;
			List<KingdomCityBook> books = System.OwnedCityBooks();
			for (int i = 0; i < books.Count; i++)
			{
				KingdomNamedCookReceipt row = books[i]?.NamedCook;
				if (row == null || row.BodyObjectId != ObjectId
					|| row.Phase == KingdomNamedCookPhase.None) continue;
				if (Match != null)
				{
					Failure = "One exact body is claimed by two named-cook receipts."; return false;
				}
				Match = books[i];
			}
			return true;
		}

		private static bool SameAppointment(KingdomNamedCookReceipt A,
			KingdomNamedCookReceipt B)
		{
			return A != null && B != null && A.Version == B.Version
				&& A.Generation == B.Generation && A.RealmId == B.RealmId
				&& A.SettlementId == B.SettlementId && A.ResidentId == B.ResidentId
				&& A.BodyObjectId == B.BodyObjectId && A.RecipeId == B.RecipeId
				&& A.GraphFingerprint == B.GraphFingerprint
				&& A.DesignatedTick == B.DesignatedTick;
		}

		private static bool WitnessedDead(KingdomCityBook Book, int ResidentId)
		{
			if (Book == null || ResidentId <= 0 || !Book.TryRead(out KingdomCityState state,
				out KingdomCityFault _)) return false;
			for (int i = 0; i < state.ResidentCount; i++)
				if (state.TryResident(i, out KingdomResidentRow row)
					&& row.ResidentId == ResidentId)
					return row.Standing == KingdomResidentStanding.Dead;
			return false;
		}

		private static bool StandingResident(KingdomCityBook Book, int ResidentId)
		{
			if (Book == null || ResidentId <= 0 || !Book.TryRead(out KingdomCityState state,
				out KingdomCityFault _)) return false;
			return state.TryResidentIndex(ResidentId, out int at)
				&& state.TryResident(at, out KingdomResidentRow row)
				&& row.Standing == KingdomResidentStanding.Resident;
		}

		private static GameObject FindExactOnGround(Zone Zone, string ObjectId)
		{
			GameObject found = null;
			List<GameObject> objects = Zone?.GetObjects();
			for (int i = 0; objects != null && i < objects.Count; i++)
				if (objects[i]?.IDIfAssigned == ObjectId)
				{
					if (found != null) return null;
					found = objects[i];
				}
			return found;
		}

		private static bool TellVacancy(KingdomSystem System, KingdomNamedCookReceipt Receipt,
			out string Failure)
		{
			Failure = null;
			if (Receipt == null || !KingdomNamedCookRules.IsVacant(Receipt.Phase)) return true;
			string text = Receipt.ResidentName + " " + KingdomNamedCookRules.VacancyClause(Receipt)
				+ " at " + Receipt.SettlementName
				+ "; the named hearth remains vacant until a deliberate appointment.";
			if (KingdomChronicle.RecordOnce(System,
				"taf:named-cook:vacancy:" + Receipt.RecipeId, text)) return true;
			Failure = "Named-cook predecessor history remains pending."; return false;
		}

		private static bool TellAppointment(KingdomSystem System,
			KingdomNamedCookReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (Receipt == null || Receipt.Phase != KingdomNamedCookPhase.Applied) return true;
			if (KingdomChronicle.RecordOnce(System,
				"taf:named-cook:appointment:" + Receipt.RecipeId, Receipt.ResidentName
				+ " took up the named hearth of " + Receipt.SettlementName + ".")) return true;
			Failure = "Named-cook appointment history remains pending."; return false;
		}
	}
}
