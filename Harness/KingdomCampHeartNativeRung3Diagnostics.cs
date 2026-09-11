using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// What the rung-3 boundary READS before it asserts anything (native runs 7 and 33, issue
	/// #159). Run 33 refused with the bare design key and nothing else, because the rung-3 leg
	/// had no parity with the rung-2 leg's blocked-path diagnostics: the settlement's own verdict
	/// on the standing heart, its stage and population, and the moot yard's job looked up by its
	/// TARGET rather than by the retained rung-2 id. All of it is journaled here first, so the
	/// next refusal names the production gate that held rather than the symptom.
	/// <para>Read-only: <c>KingdomUpgrade.Assess</c> is the settlement pass's own listing read
	/// and changes nothing; the free-hands and other-work inputs are derived exactly as
	/// <c>KingdomUpgrade.Resolve</c> derives them from the same survey.</para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			/// <summary>The settlement's verdict on raising the standing heart, plus the stage,
			/// population, craft and announced reason it was reached from. Never refuses.</summary>
			internal void RecordRung3Standing(GameObject Standing)
			{
				try
				{
					KingdomSurvey survey = Census();
					int freeHands = System.Population - System.AssignedCrew;
					if (freeHands < 0) freeHands = 0;
					bool otherWorkUnderway = false;
					for (int i = 0; i < survey.Improvements.Count; i++)
					{
						r_KingdomImprovement working =
							survey.Improvements[i]?.GetPart<r_KingdomImprovement>();
						if (working != null && working.Working) otherWorkUnderway = true;
					}
					KingdomUpgrade.Assessment assessment = KingdomUpgrade.Assess(System, Zone,
						Standing, survey, freeHands, otherWorkUnderway);
					r_KingdomImprovement improvement = Standing.GetPart<r_KingdomImprovement>();
					Evidence.Append("\nrung3-standing tick=").Append(Game.TimeTicks)
						.Append("; key=").Append(KingdomUpgrade.DesignKeyOf(Standing))
						.Append("; stage=").Append(System.Stage)
						.Append("; population=").Append(System.Population)
						.Append("; assigned-crew=").Append(System.AssignedCrew)
						.Append("; free-hands=").Append(freeHands)
						.Append("; craft=").Append(KingdomZoning.Tech(System))
						.Append("; other-work-underway=").Append(otherWorkUnderway)
						.Append("; stored water=").Append(survey.StoredWater)
						.Append("; assess valid=").Append(assessment.Valid)
						.Append("; verdict=").Append(assessment.Verdict)
						.Append("; successor=").Append(assessment.SuccessorKey ?? "(none)")
						.Append("; stage-needed=").Append(assessment.StageNeeded)
						.Append("; crew-needed=").Append(assessment.CrewNeeded)
						.Append("; cost=").Append(assessment.CostDrams)
						.Append("; reserve=").Append(assessment.Reserve)
						.Append("; shortfall=").Append(assessment.Shortfall)
						.Append("; reason=").Append(KingdomScenarioRules.Bounded(assessment.Reason))
						.Append("; announced-verdict=").Append(improvement == null ? "absent"
							: ((KingdomUpgradeRules.UpgradeVerdict)improvement.AnnouncedReason)
								.ToString())
						.Append("; improvement-working=").Append(improvement != null
							&& improvement.Working);
				}
				catch (Exception error)
				{
					// Diagnostics never replace the production answer asserted after them.
					Evidence.Append("\nrung3-standing-read-error=").Append(
						KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message));
				}
			}

			/// <summary>The one improvement job that targets the moot yard, found by its target,
			/// or null when the settlement never began one. Two such jobs refuse: the settlement
			/// betters one work at a time.</summary>
			internal KingdomConstructionJob FindRung3Job()
			{
				List<KingdomConstructionJob> jobs;
				string failure;
				// The whole durable registry, not the active slice: a finished job is terminal and
				// the active reads deliberately exclude it.
				Require(KingdomConstruction.TryRead(out jobs, out failure) && jobs != null,
					"taf-camp-rung3-jobs-unreadable: " + KingdomScenarioRules.Bounded(failure));
				KingdomConstructionJob found = null;
				for (int i = 0; i < jobs.Count; i++)
				{
					KingdomConstructionJob job = jobs[i];
					if (job == null || job.Route != KingdomConstructionRoute.Improvement
						|| job.TargetKey != ThirdRungKey) continue;
					Require(found == null,
						"taf-camp-rung3-job-ambiguous: two improvement jobs target the moot yard");
					found = job;
				}
				return found;
			}

			/// <summary>Parity with the rung-2 leg's blocked path: when no moot-yard job exists at
			/// the boundary, the road census, the founder's cell and the last messages are
			/// journaled exactly as Phase1 journals them for a rung-2 climb that never began.
			/// </summary>
			internal void RecordRung3Blocked(KingdomConstructionJob Job)
			{
				if (Job != null)
				{
					Evidence.Append("\nrung3-job=").Append(Job.Id).Append("; phase=")
						.Append(Job.Phase).Append("; physical=").Append(Job.PhysicalPhase)
						.Append("; failure=").Append(KingdomScenarioRules.Bounded(Job.Failure));
					return;
				}
				Evidence.Append("\nrung3-job=absent");
				RecordBlockedMessages();
			}

			/// <summary>The gate run 33 fell through: the moot yard asks for a Town, and the
			/// settlement must still BE one when the pass prices its bill. Production is free to
			/// shed people the works cannot carry; a fixture that lost them proves nothing about
			/// the second climb and says so here, with the numbers, instead of at the design key.
			/// </summary>
			internal void RequireTownHeld(string When)
			{
				Require(System.Stage >= GrowthStage.Town
					&& System.Population >= TownResidentCount,
					"taf-camp-rung3-town-held: the settlement did not hold the Town the moot yard "
						+ "is gated on " + When + "; stage=" + System.Stage + "; population="
						+ System.Population + " of " + TownResidentCount);
				KingdomCatalogueRules.SupportTally tally =
					KingdomSubsidence.ScopedSupports(System, Zone, Census());
				Evidence.Append("\nrung3-town-held ").Append(When).Append(" tick=")
					.Append(Game.TimeTicks).Append("; stage=").Append(System.Stage)
					.Append("; population=").Append(System.Population)
					.Append("; supports water=").Append(tally.Water).Append(" roof=")
					.Append(tally.Roof).Append(" lift=").Append(tally.Lift)
					.Append("; supported level=").Append(KingdomSubsidenceRules.SupportedLevel(
						tally, System.Stage, System.Shade));
			}
		}
	}
}
