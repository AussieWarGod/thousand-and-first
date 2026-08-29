using System;
using ThousandAndFirst.Simulation.City;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomOfficeRuntime
	{
		public static bool TryReconcile(KingdomSystem System, Zone Zone, KingdomSurvey Survey,
			out string Failure)
		{
			Failure = null;
			if (!TryContext(System, Zone, Survey, out CityContext context, out Failure)) return false;
			if (System.Experience == null
				|| !KingdomExperienceRules.TryValidate(System.Experience, out Failure)) return false;
			if (!CleanOrphanMarkers(System, Survey, out Failure)) return false;
			if (!KingdomExperienceRules.TryGetOffice(System.Experience, context.SettlementId,
				out KingdomCivicOfficeReceipt receipt, out Failure)) return false;
			if (receipt == null && !TryAdoptLegacy(System, context, out receipt, out Failure))
				return false;
			if (receipt == null) return true;

			if (receipt.Phase == KingdomCivicOfficePhase.AppointmentPrepared)
			{
				GameObject body = FindExact(Survey, receipt.HolderObjectId);
				if (body != null && EnsureProjection(System, receipt, body, out Failure)
					&& KingdomExperienceRules.TryCompleteOfficeAppointment(System.Experience,
						System.Experience.Revision, receipt.SettlementId, receipt.Generation,
						out Failure))
					KingdomExperienceRules.TryGetOffice(System.Experience, receipt.SettlementId,
						out receipt, out Failure);
			}
			else if (receipt.Phase == KingdomCivicOfficePhase.Held)
			{
				bool residentFound = TryResident(context.State, receipt.HolderResidentId,
					out KingdomResidentRow row);
				if (!residentFound
					|| row.Standing == KingdomResidentStanding.Dead
					|| row.Standing == KingdomResidentStanding.Abroad
					|| !HasWork(context.State, receipt.WorkId))
				{
					KingdomCivicOfficeVacancyCause cause = !HasWork(context.State, receipt.WorkId)
						? KingdomCivicOfficeVacancyCause.AuthorityLost
						: residentFound && row.Standing == KingdomResidentStanding.Dead
							? KingdomCivicOfficeVacancyCause.Death
							: KingdomCivicOfficeVacancyCause.Departure;
					if (!KingdomExperienceRules.TryPrepareOfficeVacancy(System.Experience,
						System.Experience.Revision, receipt.SettlementId,
						receipt.HolderResidentId, cause, Now(), out Failure)) return false;
					if (!KingdomExperienceRules.TryGetOffice(System.Experience,
						receipt.SettlementId, out receipt, out Failure)) return false;
				}
				else
				{
					GameObject body = FindExact(Survey, receipt.HolderObjectId);
					if (body != null && !EnsureProjection(System, receipt, body, out Failure))
						return false;
				}
			}

			if (receipt.Phase == KingdomCivicOfficePhase.VacancyPrepared)
			{
				GameObject body = FindExact(Survey, receipt.HolderObjectId);
				if (body != null && CleanupProjection(System, receipt, body, out Failure)
					&& KingdomExperienceRules.TryCompleteOfficeVacancy(System.Experience,
						System.Experience.Revision, receipt.SettlementId, receipt.Generation,
						out Failure))
					KingdomExperienceRules.TryGetOffice(System.Experience, receipt.SettlementId,
						out receipt, out Failure);
			}
			ProjectCompatibility(System, receipt);
			if (receipt.Phase == KingdomCivicOfficePhase.Held) TellHeld(System, receipt);
			else if (receipt.Phase == KingdomCivicOfficePhase.Vacant) TellVacant(System, receipt);
			return Failure == null;
		}

		private static bool TryAdoptLegacy(KingdomSystem System, CityContext Context,
			out KingdomCivicOfficeReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			int legacyId = Context.Seated ? System.OfficeHolderResidentId
				: Context.Settlement.OfficeHolderResidentId;
			string legacyName = Context.Seated ? System.OfficeHolderName
				: Context.Settlement.OfficeHolderName;
			if (legacyId <= 0 && string.IsNullOrEmpty(legacyName))
				return true;
			int id = legacyId;
			KingdomResidentRow row = default(KingdomResidentRow);
			if (id > 0) TryResident(Context.State, id, out row);
			else if (!TryUniqueResidentByName(Context.State, legacyName, out row))
				return true;
			if (row.ResidentId <= 0 || row.Standing != KingdomResidentStanding.Resident)
				return true;
			GameObject body = Context.Survey.FindCitizen(row.ResidentId);
			if (!GameObject.Validate(body) || !body.IsAlive) return true;
			string bodyId = body.IDIfAssigned;
			if (string.IsNullOrEmpty(bodyId))
			{
				Failure = "The appointed resident lacks assigned physical identity.";
				return false;
			}
			string role = KingdomOfficeRules.ChooseTitle(Context.SettlementName)
				+ " of " + Context.SettlementName;
			bool ownsRole = !HasRole(body.GetPart<SocialRoles>(), role);
			if (!KingdomExperienceRules.TryPrepareOfficeAppointment(System.Experience,
				System.Experience.Revision, Context.SettlementId, Context.SettlementName,
				Context.WorkId, row.ResidentId, row.Name, bodyId, ownsRole, Now(), out Failure))
				return false;
			KingdomExperienceRules.TryGetOffice(System.Experience, Context.SettlementId,
				out Receipt, out Failure);
			if (!EnsureProjection(System, Receipt, body, out Failure)
				|| !KingdomExperienceRules.TryCompleteOfficeAppointment(System.Experience,
					System.Experience.Revision, Context.SettlementId, Receipt.Generation,
					out Failure)) return false;
			return KingdomExperienceRules.TryGetOffice(System.Experience, Context.SettlementId,
				out Receipt, out Failure);
		}

		private static bool TryResident(KingdomCityState State, int ResidentId,
			out KingdomResidentRow Row)
		{
			Row = default(KingdomResidentRow);
			for (int i = 0; State != null && i < State.ResidentCount; i++)
				if (State.TryResident(i, out KingdomResidentRow found)
					&& found.ResidentId == ResidentId) { Row = found; return true; }
			return false;
		}

		private static bool TryUniqueResidentByName(KingdomCityState State, string Name,
			out KingdomResidentRow Row)
		{
			Row = default(KingdomResidentRow); int count = 0;
			for (int i = 0; State != null && i < State.ResidentCount; i++)
				if (State.TryResident(i, out KingdomResidentRow found)
					&& found.Name == Name) { Row = found; count++; }
			return count == 1;
		}

		private static GameObject FindExact(KingdomSurvey Survey, string ObjectId)
		{
			GameObject found = null;
			for (int i = 0; Survey != null && i < Survey.Objects.Count; i++)
				if (Survey.Objects[i]?.IDIfAssigned == ObjectId)
				{
					if (found != null) return null; found = Survey.Objects[i];
				}
			return found;
		}

		private static void ProjectCompatibility(KingdomSystem System,
			KingdomCivicOfficeReceipt Receipt)
		{
			if (System == null || Receipt == null
				|| !System.TryFindSettlement(Receipt.SettlementId,
					out bool seated, out KingdomSettlement settlement)) return;
			bool held = Receipt != null && Receipt.Phase == KingdomCivicOfficePhase.Held;
			if (seated)
			{
				System.OfficeHolderResidentId = held ? Receipt.HolderResidentId : 0;
				System.OfficeHolderName = held ? Receipt.HolderName : null;
				System.NotableShade = 0;
			}
			else
			{
				settlement.OfficeHolderResidentId = held ? Receipt.HolderResidentId : 0;
				settlement.OfficeHolderName = held ? Receipt.HolderName : null;
				settlement.NotableShade = 0;
			}
		}

		private static long Now()
		{
			return XRL.The.Game == null || XRL.The.Game.TimeTicks < 0L
				? 0L : XRL.The.Game.TimeTicks;
		}
	}
}
