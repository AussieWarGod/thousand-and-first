using System;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Exact citizen-death intake and attended reconciliation for civic office and
	/// remembrance authority. It never appoints an oldest resident or binds a death to a cairn
	/// automatically; both optional projections require an explicit Charter action.</summary>
	public static partial class KingdomOffices
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionMemory") != "No";

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			KingdomMaterials.HeadedProbe = KingdomReach.IsHeaded;
			if (System == null || !System.Founded || Z == null || Survey == null
				|| !System.OwnedZone(Z.ZoneID)) return;
			TagCitizens(System, Survey);
			if (!KingdomOfficeRuntime.TryReconcile(System, Z, Survey, out string officeFailure))
				KingdomLog.Log("office: exact reconciliation waits ("
					+ (officeFailure ?? "unknown failure") + ")");
			if (!KingdomRemembranceRuntime.TryReconcile(System, Z, Survey,
				out string remembranceFailure))
				KingdomLog.Log("remembrance: exact reconciliation waits ("
					+ (remembranceFailure ?? "unknown failure") + ")");
		}

		/// <summary>The engine death callback is the only authority that creates a terminal resident
		/// row. Optional remembrance reads that row later; it never promotes cache absence to death.</summary>
		public static void RecordDeath(GameObject Citizen, GameObject Killer)
		{
			KingdomSystem.Guard("citizen death", delegate
			{
				if (Citizen == null) return;
				KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
				if (!system.Founded) return;
				long tick = The.Game == null || The.Game.TimeTicks < 0L
					? 0L : The.Game.TimeTicks;
				bool witnessed = TryCaptureRemembranceWitness(system, Citizen,
					out RemembranceWitnessContext witness, out string witnessFailure);
				if (!Simulation.City.KingdomExpeditions.TryPrepareResidentDeath(system, Citizen,
					tick, out string expeditionFailure))
					KingdomLog.Log("expedition: dying resident terminal receipt waits ("
						+ (expeditionFailure ?? "unknown failure") + ")");
				KingdomOfficeRules.DeathCause cause = KingdomOfficeRules.ClassifyCause(
					KillerIsPlayer: Killer != null && Killer.IsPlayer(),
					KillerIsRaider: Killer != null
						&& Killer.GetIntProperty("KingdomRaider") == 1,
					KillerKnown: Killer != null);
				Simulation.City.KingdomStandingCause standingCause =
					(Simulation.City.KingdomStandingCause)((int)cause
						+ (int)Simulation.City.KingdomStandingCause.Unwitnessed);
				bool marked = Simulation.City.KingdomResidents.TryMarkDead(system, Citizen,
					standingCause, out Simulation.City.KingdomResidentRow former);
				if (!marked && former.Standing !=
					Simulation.City.KingdomResidentStanding.Dead) return;
				if (!KingdomPolityResidentTransition.TryConclude(system, Citizen,
					former.ResidentId, KingdomPolityResidentTransitionCause.Death,
					out KingdomPolityResidentTransitionPreparation _, out string polityFailure))
					KingdomLog.Log("polity: dead deed-figure conclusion waits ("
						+ (polityFailure ?? "unknown failure") + ")");
				if (!marked) return;
				if (!witnessed || !TryRecordRemembranceEligibility(system, witness, former, tick,
					out witnessFailure)) ReportRemembranceWitnessFailure(witnessFailure);
					if (!KingdomOfficeRuntime.ObserveHolderLoss(system, Citizen,
						KingdomCivicOfficeVacancyCause.Death, out string officeFailure))
						KingdomLog.Log("office: witnessed holder loss waits ("
							+ (officeFailure ?? "unknown failure") + ")");
					if (!KingdomNamedCook.ObserveCookLoss(system, Citizen,
						KingdomNamedCookVacancyCause.Death, out string cookFailure))
						KingdomLog.Log("named cook: witnessed vacancy waits ("
							+ (cookFailure ?? "unknown failure") + ")");
				if (!KingdomCitizenship.TryRemove(system, Citizen,
					KingdomCitizenshipRemovalReason.Death, out string citizenshipFailure))
					KingdomLog.Log("citizenship: death removal remained unresolved ("
						+ (citizenshipFailure ?? "unknown failure") + ")");
				KingdomResidentIdentity.Forget(system, Citizen);
				KingdomCreed.Forget(system, Citizen);
				if (!Enabled)
				{
					KingdomLog.Log("death: living authority retired " + former.Name
						+ " while settlement memory is disabled"); return;
				}
				system.Dead++;
				system.DeadNames.Add(former.Name);
				system.DeadOrigins.Add(former.Origin);
				system.DeadArrived.Add(former.Arrived);
				system.DeadCauses.Add(KingdomOfficeRules.CauseClause(cause));
				bool owned = Simulation.City.KingdomHappenings.OwnDeathTelling(system,
					former.Name, former.Origin, cause, Citizen.CurrentZone,
					The.Game == null ? 0L : The.Game.TimeTicks);
				if (!owned)
				{
					KingdomChronicle.Record(system, KingdomOfficeRules.MourningChronicle(
						KingdomPresentation.Rich(former.Name),
						KingdomPresentation.Rich(former.Origin),
						KingdomPresentation.Rich(system.SeatName), cause));
					MessageQueue.AddPlayerMessage(KingdomVoices.Say(system,
						VoiceOccasion.CitizenLost, "{{r|" + KingdomOfficeRules.MourningMessage(
							KingdomPresentation.Rich(former.Name), cause) + "}}"));
				}
				KingdomLog.Log("death: " + former.Name + " of "
					+ (string.IsNullOrEmpty(former.Origin) ? "-" : former.Origin)
					+ " cause=" + cause + " pop now " + system.Population);
			});
		}

		private static void TagCitizens(KingdomSystem System, KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.CitizenBodies.Count; i++)
			{
				GameObject item = Survey.CitizenBodies[i];
				if (item.GetIntProperty("KingdomBorn") == 1)
					item.RequirePart<r_KingdomCitizenLegacy>();
				if (item.GetPart<r_KingdomCitizenship>() == null)
					KingdomCitizenship.ObserveLegacy(System, item, out string _);
			}
		}
	}
}
