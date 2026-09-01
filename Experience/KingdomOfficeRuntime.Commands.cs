using System;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomOfficeRuntime
	{
		private static bool TryAppoint(KingdomSystem System, CityContext Context,
			KingdomOfficeCandidate Candidate, out string Failure)
		{
			Failure = null;
			if (!ExactCandidate(System, Context, Candidate, out GameObject body, out Failure))
				return false;
			if (body.GetPart<r_KingdomOfficeProjection>() != null
				|| MarketOfficeCandidateBlocked(body, Context.Survey))
			{
				Failure = "An office or protected market projection already marks that resident.";
				return false;
			}
			string objectId = body.ID;
			string role = KingdomOfficeRules.ChooseTitle(Context.SettlementName)
				+ " of " + Context.SettlementName;
			bool ownsRole = !HasRole(body.GetPart<SocialRoles>(), role);
			if (!KingdomExperienceRules.TryPrepareOfficeAppointment(System.Experience,
				System.Experience.Revision, Context.SettlementId, Context.SettlementName,
				Context.WorkId, Candidate.ResidentId, Candidate.Name, objectId, ownsRole, Now(),
				out Failure)) return false;
			KingdomGovernanceScope.Commit("appoint civic office");
			if (!KingdomExperienceRules.TryGetOffice(System.Experience, Context.SettlementId,
				out KingdomCivicOfficeReceipt receipt, out Failure)) return false;
			if (!EnsureProjection(System, receipt, body, out Failure)
				|| !KingdomExperienceRules.TryCompleteOfficeAppointment(System.Experience,
					System.Experience.Revision, receipt.SettlementId, receipt.Generation,
					out Failure)) return false;
			KingdomExperienceRules.TryGetOffice(System.Experience, Context.SettlementId,
				out receipt, out string _);
			ProjectCompatibility(System, receipt); TellHeld(System, receipt);
			MessageQueue.AddPlayerMessage("{{G|" + KingdomPresentation.Rich(Candidate.Name)
				+ " now holds the office of " + role + ".}}");
			return true;
		}

		private static bool TryRelease(KingdomSystem System, CityContext Context,
			KingdomCivicOfficeReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (Receipt == null || Receipt.Phase != KingdomCivicOfficePhase.Held)
			{
				Failure = "No exact held civic office can be released."; return false;
			}
			if (!KingdomExperienceRules.TryPrepareOfficeVacancy(System.Experience,
				System.Experience.Revision, Receipt.SettlementId, Receipt.HolderResidentId,
				KingdomCivicOfficeVacancyCause.Released, Now(), out Failure)) return false;
			KingdomGovernanceScope.Commit("release civic office");
			KingdomExperienceRules.TryGetOffice(System.Experience, Receipt.SettlementId,
				out KingdomCivicOfficeReceipt prepared, out string _);
			GameObject body = FindExact(Context.Survey, Receipt.HolderObjectId);
			if (body == null)
			{
				Failure = "The exact former holder is not loaded. The office is vacant; its owned "
					+ "title will be removed when that body is next present."; return false;
			}
			if (!CleanupProjection(System, prepared, body, out Failure)
				|| !KingdomExperienceRules.TryCompleteOfficeVacancy(System.Experience,
					System.Experience.Revision, prepared.SettlementId, prepared.Generation,
					out Failure)) return false;
			KingdomExperienceRules.TryGetOffice(System.Experience, Receipt.SettlementId,
				out KingdomCivicOfficeReceipt vacant, out string _);
			ProjectCompatibility(System, vacant); TellVacant(System, vacant);
			MessageQueue.AddPlayerMessage("{{K|The office of " + RoleFor(vacant)
				+ " stands vacant.}}");
			return true;
		}

		public static bool ObserveHolderLoss(KingdomSystem System, GameObject Body,
			KingdomCivicOfficeVacancyCause Cause, out string Failure)
		{
			Failure = null;
			if (System?.Experience == null || Body == null
				|| Cause == KingdomCivicOfficeVacancyCause.None) return true;
			if (!KingdomExperienceRules.TryValidate(System.Experience, out Failure)) return false;
			int residentId = Simulation.City.KingdomResidents.IdOf(Body);
			string objectId = Body.IDIfAssigned;
			KingdomCivicOfficeReceipt receipt = null;
			for (int i = 0; i < System.Experience.Offices.Count; i++)
			{
				KingdomCivicOfficeReceipt candidate = System.Experience.Offices[i];
				if ((candidate.Phase == KingdomCivicOfficePhase.Held
					|| candidate.Phase == KingdomCivicOfficePhase.AppointmentPrepared
					|| candidate.Phase == KingdomCivicOfficePhase.VacancyPrepared
						&& candidate.VacancyCause == Cause)
					&& candidate.HolderResidentId == residentId
					&& candidate.HolderObjectId == objectId) receipt = candidate;
			}
			if (receipt == null) return true;
			if (!KingdomExperienceRules.TryPrepareOfficeVacancy(System.Experience,
				System.Experience.Revision, receipt.SettlementId, residentId, Cause, Now(),
				out Failure)) return false;
			KingdomExperienceRules.TryGetOffice(System.Experience, receipt.SettlementId,
				out KingdomCivicOfficeReceipt prepared, out string _);
			if (Cause == KingdomCivicOfficeVacancyCause.Death)
			{
				if (!TryOfficeCityState(System, prepared.SettlementId,
					out Simulation.City.KingdomCityState state, out Failure)
					|| !KingdomExperienceRules.CanCompleteOfficeDeathVacancy(System.Experience,
						System.Experience.Revision, prepared.SettlementId, prepared.Generation,
						state, out Failure)) return false;
				bool cleaned = TryCleanupDeathProjection(System, prepared, Body, out Failure);
				string residueFailure = cleaned ? null : Failure;
				if (!cleaned) MarkDeathResidue(System, prepared, Body);
				if (!KingdomExperienceRules.TryCompleteOfficeDeathVacancy(System.Experience,
						System.Experience.Revision, prepared.SettlementId, prepared.Generation,
						state, out Failure)) return false;
				if (!cleaned) KingdomLog.Log("office: dead-holder projection quarantined for "
					+ "exact later cleanup (" + (residueFailure ?? "body absent") + ")");
			}
			else if (!CleanupProjection(System, prepared, Body, out Failure)
				|| !KingdomExperienceRules.TryCompleteOfficeVacancy(System.Experience,
					System.Experience.Revision, prepared.SettlementId, prepared.Generation,
					out Failure)) return false;
			KingdomExperienceRules.TryGetOffice(System.Experience, receipt.SettlementId,
				out KingdomCivicOfficeReceipt vacant, out string _);
			ProjectCompatibility(System, vacant);
			TellVacant(System, vacant); return true;
		}

		private static void TellHeld(KingdomSystem System, KingdomCivicOfficeReceipt Receipt)
		{
			if (Receipt == null || Receipt.Phase != KingdomCivicOfficePhase.Held) return;
			KingdomChronicle.RecordOnce(System, OfficeEvent(Receipt, "held"), Receipt.HolderName
				+ " took the office of " + RoleFor(Receipt) + " at " + Receipt.SettlementName);
		}

		private static void TellVacant(KingdomSystem System, KingdomCivicOfficeReceipt Receipt)
		{
			if (Receipt == null || Receipt.Phase != KingdomCivicOfficePhase.Vacant) return;
			KingdomChronicle.RecordOnce(System, OfficeEvent(Receipt, "vacant"), "the office of "
				+ RoleFor(Receipt) + " at " + Receipt.SettlementName + " fell vacant after "
				+ Receipt.PredecessorName);
		}

		private static string OfficeEvent(KingdomCivicOfficeReceipt R, string Kind)
		{
			return "taf:experience:office:" + R.SettlementId + ":" + R.Generation + ":" + Kind;
		}
	}
}
