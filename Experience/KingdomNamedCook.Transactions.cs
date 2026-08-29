using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomNamedCook
	{
		internal static bool TryDesignate(KingdomSystem System, KingdomCityBook Book,
			int ResidentId, out string Failure)
		{
			return TryDesignate(System, Book, ResidentId, out bool _, out Failure);
		}

		internal static bool TryDesignate(KingdomSystem System, KingdomCityBook Book,
			int ResidentId, out bool MutationCommitted, out string Failure)
		{
			MutationCommitted = false;
			Failure = null;
			string realm;
			string seatedId;
			bool seated;
			KingdomSettlement settlement;
			if (System == null || Book == null
				|| !System.TryGetCurrentIdentity(out realm, out seatedId)
				|| !System.TryFindSettlement(Book, out seated, out settlement))
			{
				Failure = "Current realm and city identity cannot be proved.";
				return false;
			}
			Book.Normalize();
			KingdomNamedCookReceipt prior = Book.NamedCook;
			bool open = prior.Phase != KingdomNamedCookPhase.None
				&& !KingdomNamedCookRules.IsVacant(prior.Phase);
			KingdomResidentRow resident;
			bool standing = KingdomResidents.TryResident(Book, ResidentId, out resident)
				&& resident.Standing == KingdomResidentStanding.Resident;
			GameObject body = null;
			string zoneId = null;
			bool exact = standing && KingdomResidents.TryResolveBoundBody(System, ResidentId,
				true, out body, out zoneId);
			string localId = Book.SettlementId;
			string localName = seated ? System.SeatName : settlement?.SettlementName;
			string residentName = standing ? resident.Name : "";
			string objectId = exact ? body.IDIfAssigned : "";
			bool shares = exact && !string.IsNullOrEmpty(body.GetPropertyOrTag("SharesRecipe"));
			bool teaches = exact && body.GetPart<TeachesDish>() != null;
			bool marker = exact && body.GetPart<r_KingdomNamedCook>() != null;
			KingdomNamedCookVerdict verdict = KingdomNamedCookRules.JudgeCandidate(
				System.Founded, System.TryFindSettlement(localId, out seated, out settlement),
				standing, exact, exact && (body.IsPlayer() || body.IsPlayerLed()), shares,
				teaches, marker, open, realm, localId, ResidentId, residentName, objectId);
			if (verdict != KingdomNamedCookVerdict.Allowed)
			{
				Failure = Refusal(verdict);
				return false;
			}
			int generation = prior == null || prior.Generation < 1 ? 1 : prior.Generation + 1;
			if (generation <= 0)
			{
				Failure = "The bounded appointment generation is exhausted.";
				return false;
			}
			long now = The.Game == null || The.Game.TimeTicks < 0L ? 0L : The.Game.TimeTicks;
			if (KingdomNamedCookRules.IsVacant(prior.Phase)
				&& !TellVacancy(System, prior, out Failure)) return false;
			KingdomNamedCookReceipt prepared;
			if (!KingdomNamedCookRules.TryPrepare(realm, localId, localName, ResidentId,
				residentName, objectId, generation, now, out prepared, out Failure)) return false;
			Book.NamedCook = prepared; MutationCommitted = true;
			if (!EnsureProjection(Book, body, prepared, CompletePrepared: true, out Failure))
				return false;
			return TellAppointment(System, Book.NamedCook, out Failure);
		}

		internal static bool TryRelease(KingdomSystem System, KingdomCityBook Book,
			out string Failure)
		{
			return TryRelease(System, Book, KingdomNamedCookVacancyCause.Released,
				out bool _, out Failure);
		}

		internal static bool TryRelease(KingdomSystem System, KingdomCityBook Book,
			KingdomNamedCookVacancyCause Cause, out bool MutationCommitted, out string Failure)
		{
			MutationCommitted = false;
			Failure = null;
			Book?.Normalize();
			KingdomNamedCookReceipt authority = Book?.NamedCook;
			string invalid = null;
			if (System == null || Book == null || authority == null
				|| !KingdomNamedCookRules.Validate(authority, out invalid)
				|| (authority.Phase != KingdomNamedCookPhase.Applied
					&& !KingdomNamedCookRules.IsVacancyPrepared(authority.Phase)))
			{
				Failure = invalid ?? "No exact active named-cook appointment exists.";
				return false;
			}
			GameObject body;
			string zoneId;
			if (!KingdomResidents.TryResolveBoundBody(System, authority.ResidentId, true,
				out body, out zoneId)
				|| !string.Equals(body.IDIfAssigned, authority.BodyObjectId,
					StringComparison.Ordinal))
			{
				Failure = "The exact appointed resident cannot presently be resolved; no other "
					+ "body was substituted.";
				return false;
			}
			if (authority.Phase == KingdomNamedCookPhase.Applied)
			{
				KingdomNamedCookReceipt releasing = KingdomNamedCookRules.BeginVacancy(authority,
					Cause);
				if (releasing == null)
				{
					Failure = "The release boundary could not be prepared.";
					return false;
				}
				Book.NamedCook = authority = releasing; MutationCommitted = true;
			}
			else if (KingdomNamedCookRules.VacancyCause(authority.Phase) != Cause)
			{
				Failure = "A different named-cook vacancy cause is already prepared."; return false;
			}
			if (!RemoveProjection(Book, body, authority, out Failure)) return false;
			return TellVacancy(System, Book.NamedCook, out Failure);
		}

		internal static bool Reconcile(KingdomSystem System, KingdomCityBook Book,
			bool LoadZone, out string Failure)
		{
			Failure = null;
			if (System == null || Book == null) return false;
			Book.Normalize();
			KingdomNamedCookReceipt authority = Book.NamedCook;
			string invalid = null;
			if (!KingdomNamedCookRules.Validate(authority, out invalid))
			{
				Book.NamedCook = KingdomNamedCookRules.Quarantined(authority, invalid)
					?? new KingdomNamedCookReceipt();
				Failure = invalid;
				return false;
			}
			if (authority.Phase == KingdomNamedCookPhase.None) return true;
			if (KingdomNamedCookRules.IsVacant(authority.Phase))
				return TellVacancy(System, authority, out Failure);
			if (authority.Phase == KingdomNamedCookPhase.Quarantined)
			{
				Failure = authority.Fault;
				return false;
			}
			GameObject body;
			string zoneId;
			if (!KingdomResidents.TryResolveBoundBody(System, authority.ResidentId, LoadZone,
				out body, out zoneId))
			{
				Failure = "The exact appointed resident is not loaded and standing on the roll.";
				return false;
			}
			if (KingdomNamedCookRules.IsVacancyPrepared(authority.Phase))
			{
				if (!RemoveProjection(Book, body, authority, out Failure)) return false;
				return TellVacancy(System, Book.NamedCook, out Failure);
			}
			if (!EnsureProjection(Book, body, authority,
				CompletePrepared: authority.Phase == KingdomNamedCookPhase.Prepared, out Failure))
				return false;
			return TellAppointment(System, Book.NamedCook, out Failure);
		}

		private static bool EnsureProjection(KingdomCityBook Book, GameObject Body,
			KingdomNamedCookReceipt Authority, bool CompletePrepared, out string Failure)
		{
			Failure = null;
			r_KingdomNamedCook marker = Body?.GetPart<r_KingdomNamedCook>();
			TeachesDish teaching = Body?.GetPart<TeachesDish>();
			if (marker != null && !marker.Matches(Authority, Body))
				return Quarantine(Book, Authority, "A different cook marker occupies the exact body.",
					out Failure);
			if (teaching != null && !ExactTeaching(teaching, Authority))
				return Quarantine(Book, Authority, "A different native recipe occupies the exact body.",
					out Failure);
			try
			{
				if (marker == null)
				{
					marker = new r_KingdomNamedCook();
					marker.Stamp(Authority);
					Body.AddPart(marker);
				}
				if (teaching == null)
				{
					XRL.World.Skills.Cooking.CookingRecipe recipe = BuildRecipe(Authority);
					if (recipe == null)
					{
						Failure = "The exact native recipe graph could not be built.";
						return false;
					}
					teaching = new TeachesDish(recipe,
						KingdomNamedCookRules.TeachingText(Authority));
					Body.AddPart(teaching);
				}
			}
			catch (Exception ex)
			{
				Failure = "Native cook projection threw " + ex.GetType().Name + ".";
				return false;
			}
			if (!marker.Matches(Authority, Body) || !ExactTeaching(teaching, Authority))
				return Quarantine(Book, Authority, "Native cook projection did not match its receipt.",
					out Failure);
			if (CompletePrepared)
			{
				KingdomNamedCookReceipt applied = KingdomNamedCookRules.Applied(Authority);
				if (applied == null)
				{
					Failure = "The prepared cook receipt could not commit.";
					return false;
				}
				marker.Stamp(applied);
				Book.NamedCook = applied;
			}
			else marker.Stamp(Authority);
			return true;
		}
	}
}
