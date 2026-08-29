using System;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRemembranceRuntime
	{
		private static bool TryDedicate(KingdomSystem System, CityContext Prior,
			DeathChoice ExpectedSubject, Simulation.City.KingdomResidentRow ExpectedMourner,
			GameObject Carrier, out string Failure)
		{
			Failure = null;
			if (!TryExactOffer(System, Prior, ExpectedSubject, ExpectedMourner,
				out CityContext context, out DeathChoice subject,
				out Simulation.City.KingdomResidentRow mourner, out Failure)) return false;
			if (!GameObject.Validate(Carrier) || !ReferenceEquals(Carrier.CurrentZone, context.Zone)
				|| !IsFixture(Carrier) || !Unlinked(context.Survey, Carrier))
			{
				Failure = "The chosen completed remembrance fixture is no longer unlinked."; return false;
			}
			// Choosing this exact fixture in Open is explicit consent. Only this post-reproof
			// dedication seam may assign identity to an otherwise anonymous fixture.
			string objectId = AssignConfirmedCarrierIdentity(Carrier);
			if (!KingdomExperienceRules.TryPrepareRemembranceProjection(System.Experience,
				System.Experience.Revision, context.SettlementId, context.SettlementName,
				subject.Row.ResidentId, subject.Row.Name, mourner.ResidentId, mourner.Name,
				objectId, context.Zone.ZoneID, Now(), out Failure)) return false;
			KingdomGovernanceScope.Commit("dedicate remembrance");
			if (!KingdomExperienceRules.TryGetRemembrance(System.Experience,
				context.SettlementId, out KingdomRemembranceReceipt receipt, out Failure)) return false;
			if (!EnsureProjection(System, context, receipt, Carrier, subject, out Failure)
				|| !KingdomExperienceRules.TryCompleteRemembranceProjection(System.Experience,
					System.Experience.Revision, receipt.SettlementId, receipt.Generation,
					out Failure)) return false;
			KingdomExperienceRules.TryGetRemembrance(System.Experience, context.SettlementId,
				out receipt, out string _);
			TellProjection(System, receipt);
			KingdomExperienceRuntime.TryRecord(System, KingdomExperienceExperiment.Memorial,
				KingdomExperienceTrialArm.Projected,
				KingdomExperienceObservationKind.Committed, 1);
			MessageQueue.AddPlayerMessage("{{G|" + KingdomPresentation.Rich(mourner.Name)
				+ " dedicates " + FixtureName(Carrier.Blueprint, subject.Row.Name) + ".}}");
			return true;
		}

		private static string AssignConfirmedCarrierIdentity(GameObject Carrier)
		{
			return Carrier.ID;
		}

		private static bool TryDecline(KingdomSystem System, CityContext Prior,
			DeathChoice ExpectedSubject, Simulation.City.KingdomResidentRow ExpectedMourner,
			out string Failure)
		{
			if (!TryExactOffer(System, Prior, ExpectedSubject, ExpectedMourner,
				out CityContext context, out DeathChoice subject,
				out Simulation.City.KingdomResidentRow mourner, out Failure)) return false;
			if (!KingdomExperienceRules.TryDeclineRemembrance(System.Experience,
				System.Experience.Revision, context.SettlementId, context.SettlementName,
				subject.Row.ResidentId, subject.Row.Name, mourner.ResidentId, mourner.Name,
				Now(), out Failure)) return false;
			KingdomGovernanceScope.Commit("decline remembrance");
			KingdomExperienceRuntime.TryRecord(System, KingdomExperienceExperiment.Memorial,
				KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceObservationKind.Closed, 1);
			return true;
		}

		private static bool TryExactOffer(KingdomSystem System, CityContext Prior,
			DeathChoice ExpectedSubject, Simulation.City.KingdomResidentRow ExpectedMourner,
			out CityContext Context, out DeathChoice Subject,
			out Simulation.City.KingdomResidentRow Mourner, out string Failure)
		{
			Context = null; Subject = null; Mourner = default(Simulation.City.KingdomResidentRow);
			if (!TryContext(System, Prior.Zone, Prior.Survey, out Context, out Failure)
				|| Context.SettlementId != Prior.SettlementId
				|| !TryExactDeath(Context, ExpectedSubject.Row.ResidentId, out Subject)
				|| Subject.Row.Name != ExpectedSubject.Row.Name
				|| !TryMourner(Context, out Mourner)
				|| Mourner.ResidentId != ExpectedMourner.ResidentId
				|| Mourner.Name != ExpectedMourner.Name)
			{
				Failure = Failure ?? "The terminal row or named mourner changed; review again.";
				return false;
			}
			return true;
		}

		private static bool Unlinked(KingdomSurvey Survey, GameObject Carrier)
		{
			if (!string.IsNullOrEmpty(Carrier?.GetStringProperty(MemorialForProperty))
				|| Carrier?.GetPart<r_KingdomRemembranceProjection>() != null) return false;
			for (int i = 0; Survey != null && i < Survey.Cairns.Count; i++)
				if (ReferenceEquals(Survey.Cairns[i], Carrier)) return true;
			return false;
		}

		private static long Now()
		{
			return XRL.The.Game == null || XRL.The.Game.TimeTicks < 0L
				? 0L : XRL.The.Game.TimeTicks;
		}
	}
}
