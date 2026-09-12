using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		public static bool Begin(KingdomSystem System, Zone Z, GameObject Work, Assessment A, KingdomSurvey Survey)
		{
			return BeginCore(System, Z, Work, A, Survey, null);
		}

		/// <summary>Commits the exact successor already shown to the founder.</summary>
		public static bool BeginPrepared(KingdomSystem System, Zone Z, GameObject Work,
			Assessment A, KingdomSurvey Survey, PreparedImprovement Prepared)
		{
			return BeginCore(System, Z, Work, A, Survey, Prepared);
		}

		/// <summary>
		/// Stands movable bodies off the ground this improvement is about to annex, through the
		/// plot clearance so there is one set of rules for both routes. Never fatal: a clearance
		/// that refuses leaves the ground exactly as it was, and the envelope proof that follows is
		/// the thing that decides whether the improvement may take it.
		/// </summary>
		private static void ClearImprovementGround(KingdomSystem System, Zone Z, GameObject Work,
			KingdomArchitectureIntent Successor, ImprovementGroundClearance Cleared)
		{
			if (Successor == null || !KingdomPlots.TryReadRect(Work,
				out KingdomPlotRules.PlotRect before)) return;
			if (!KingdomPlots.TryClearEnvelopeOccupants(System, Z, Work, Successor, before,
				out int moved, out int beasts, out Cell post, out string refusal))
			{
				KingdomLog.Log("architecture: improvement ground clearance refused: " + refusal);
				Cleared.Moved = moved;
				Cleared.Beasts = beasts;
				return;
			}
			Cleared.Moved = moved;
			Cleared.Beasts = beasts;
			Cleared.Post = post;
			if (moved <= 0 && post == null) return;
			KingdomLog.Log("architecture: improvement ground cleared: " + moved + " stood off ("
				+ beasts + " driven) for " + Work.ShortDisplayName);
		}

		/// <summary>What the crew did to the annexed ground, held until the improvement's own
		/// outcome is known so the founder is never told the work goes on when it did not.</summary>
		private sealed class ImprovementGroundClearance
		{
			internal int Moved;
			internal int Beasts;
			internal Cell Post;
			internal string Fault;
		}

		private static bool BeginCore(KingdomSystem System, Zone Z, GameObject Work,
			Assessment A, KingdomSurvey Survey, PreparedImprovement Prepared)
		{
			ImprovementGroundClearance cleared = new ImprovementGroundClearance();
			bool begun = BeginCommit(System, Z, Work, A, Survey, Prepared, cleared);
			// Said last, with the real outcome: the transition block, the reserve and the funding
			// can all still refuse AFTER bodies have been stood off this ground.
			if (cleared.Moved > 0 || cleared.Post != null)
			{
				KingdomPlots.SayEnvelopeCleared(System, Work, Work.ShortDisplayName,
					cleared.Moved, cleared.Beasts, cleared.Post, begun, cleared.Fault);
			}
			return begun;
		}

		private static bool BeginCommit(KingdomSystem System, Zone Z, GameObject Work,
			Assessment A, KingdomSurvey Survey, PreparedImprovement Prepared,
			ImprovementGroundClearance Cleared)
		{
			if (!A.Valid || !KingdomUpgradeRules.IsReady(A.Verdict) || A.Successor == null)
			{
				return false;
			}
			Cell cell = Work?.CurrentCell;
			if (cell == null || HasActiveConstruction(Work)
				|| KingdomConstruction.HasActiveSubject(System, Z,
					KingdomConstructionRoute.Improvement, Work))
			{
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, A.Successor,
				out string zoningFailure))
			{
				System.Ledger.Note("{{r|The improvement waits. " + zoningFailure + "}}");
				return false;
			}
			string payload;
			string architectureFailure;
			KingdomArchitectureIntent successorLayout = Prepared?.Architecture;
			if (Prepared == null
				? !TryPrepareImprovementPayload(System, Z, Work, A, out payload,
					out successorLayout, out _, out _, out architectureFailure)
				: !TryReprovePreparedImprovement(System, Z, Work, A, Prepared,
					out payload, out architectureFailure))
			{
				System.Ledger.Note("{{r|The improvement waits. " + architectureFailure + "}}");
				KingdomLog.Log("architecture: improvement refused before debit: "
					+ architectureFailure);
				return false;
			}
			KingdomSocketTransition transition = null;
			KingdomArchitectureIntent transitionBefore = null;
			KingdomArchitectureIntent transitionAfter = null;
			if (A.Transition != null)
			{
				KingdomPlotRules.PlotRect transitionRect;
				string transitionSkin;
				bool transitionLegacy;
				if (!KingdomArchitectureRuntime.TryRead(Work, out transitionBefore,
					out architectureFailure)
					|| !KingdomPlots.TryDecodePlotPayload(payload, out transitionRect,
						out transitionSkin, out transitionAfter, out transitionLegacy,
						out architectureFailure)
					|| transitionLegacy || transitionAfter == null
					|| !TryCurrentTransition(transitionBefore, A, out transition,
						out architectureFailure))
				{
					Cleared.Fault = architectureFailure
						?? "its exact declaration changed before debit";
					System.Ledger.Note("{{r|The plan change waits. "
						+ (architectureFailure ?? "Its exact declaration changed before debit.")
						+ "}}");
					return false;
				}
				A.Key = transition.FromBuildKey;
				A.SuccessorKey = transition.ToBuildKey;
				A.CostDrams = transition.WaterDrams;
				A.BuildTicks = transition.WorkTicks;
				A.Transition = transition;
			}
			if (!ContentsWouldFit(Work, A.Successor.Blueprint))
			{
				Cleared.Fault = "its exact contents are no longer safe to hand over";
				System.Ledger.Note("{{r|The improvement waits. Its exact contents are no longer safe to hand over.}}");
				return false;
			}
			KingdomWaterDebit water = Survey.ReserveExactWater(A.CostDrams);
			bool hostedAuthority = A.SuccessorKey == KingdomHostedArcology.ArcologyKey;
			KingdomMaterialTally transitionMaterials = transition == null
				? null : transition.Materials;
			KingdomMaterialDebit materials;
			if (transition == null)
				materials = hostedAuthority
					? KingdomMaterials.ReserveComposite(Z, new KingdomMaterialDebitCost(
						KingdomMaterials.UpgradeCostFor(A.Key),
						KingdomMaterials.BitCostFor(A.SuccessorKey),
						KingdomMaterials.ExoticCostFor(A.SuccessorKey)))
					: KingdomMaterials.ReserveUpgradePayment(Z, A.Key);
			else
				materials = KingdomMaterials.ReserveTransitionPayment(Z, transitionMaterials);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				transition == null ? KingdomMaterials.UpgradeCostFor(A.Key)
					: transitionMaterials,
				hostedAuthority ? KingdomMaterials.BitCostFor(A.SuccessorKey) : null,
				hostedAuthority ? KingdomMaterials.ExoticCostFor(A.SuccessorKey) : null);
			long now = The.Game.TimeTicks;
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.Improvement, cell, Work, A.SuccessorKey, payload,
				A.CostDrams, claim, now, now + A.BuildTicks);
			bool hasPlot = KingdomPlots.IsPlotDesign(A.SuccessorKey);
			if (!KingdomConstruction.FreezeBuildTruth(job, System,
				A.Successor.Defence, hasPlot))
			{
				water.Rollback();
				materials.Cancel();
				Cleared.Fault = "its exact build effects could not be frozen";
				System.Ledger.Note("{{r|The improvement waits. Its exact build effects could not be frozen.}}");
				return false;
			}
			if (hostedAuthority && !KingdomHostedArcology.TryReserve(System, Z, Work,
				job.Id, out string hostedFailure))
			{
				water.Rollback();
				materials.Cancel();
				Cleared.Fault = hostedFailure;
				System.Ledger.Note("{{r|The improvement waits. " + hostedFailure + "}}");
				return false;
			}
			if (transition != null)
			{
				string transitionFailure = null;
				if (!KingdomSocketTransitions.BindReceipt(Work, job, transitionBefore,
					transitionAfter, transition, out transitionFailure))
				{
					if (hostedAuthority)
						KingdomHostedArcology.ReleaseCleanReservation(System, Z, Work, job.Id);
					water.Rollback();
					materials.Cancel();
					Cleared.Fault = transitionFailure
						?? "its exact transition receipt could not be frozen";
					System.Ledger.Note("{{r|The plan change waits. "
						+ (transitionFailure ?? "Its exact transition receipt could not be frozen.")
						+ "}}");
					return false;
				}
			}
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				if (hostedAuthority)
				{
					System.Ledger.Note("{{r|The hosted arcology waits. "
						+ (fundingFailure ?? "Its exact stores are not ready.") + "}}");
					KingdomHostedArcology.ReleaseCleanReservation(System, Z, Work, job.Id);
				}
				if (transition != null) KingdomSocketTransitions.ClearReceipt(Work, job,
					transitionBefore, transitionAfter, transition);
				Cleared.Fault = fundingFailure ?? "its exact stores are not ready";
				KingdomLog.Log("improvement refused cleanly: "
					+ (fundingFailure ?? A.SuccessorKey));
				return false;
			}
			KingdomConstruction.Bind(Work, job);
			// The ground is cleared HERE and nowhere earlier: the transition block, the contents
			// check and both reserves can all refuse for reasons that have nothing to do with who
			// is standing where, and nobody is moved for a raising a missing dram then refuses.
			// This is the mutating path, so it is also not Assess. The strict envelope proof reads
			// this ground later, when the funded job applies (issues #176, #165).
			ClearImprovementGround(System, Z, Work, successorLayout, Cleared);
			if (transition != null)
			{
				if (!KingdomSocketTransitions.Authorizes(Work, transitionBefore,
					transitionAfter))
				{
					KingdomConstruction.Quarantine(ref job,
						"The funded same-set transition lost its frozen endpoint receipt.");
					return true;
				}
			}
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				System.Ledger.Note("{{r|The improvement receipt remains outstanding. The old work stands while its exact claim retries.}}");
				return true;
			}
			if (!ProjectImprovement(System, Work, A.Successor, job, out job,
				out string projectionFailure))
			{
				System.Ledger.Note("{{r|The paid improvement could not yet raise its scaffold. Its receipt remains queued.}}");
				KingdomLog.Log("construction: improvement projection waits: " + projectionFailure);
				return true;
			}
			string standing = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string line = KingdomUpgradeRules.BegunLine(standing, A.Successor.Name, A.CostDrams);
			MessageQueue.AddPlayerMessage("{{G|" + line + "}}");
			System.Ledger.Note("{{G|" + line + "}}");
			KingdomChronicle.Record(System, "the " + standing + " at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " was set to be raised into " + KingdomUpgradeRules.Article(A.Successor.Name));
			KingdomLog.Log("improvement begun: " + A.Key + " -> " + A.SuccessorKey + " cost=" + A.CostDrams + " ticks=" + A.BuildTicks + " at " + cell.X + "," + cell.Y);
			return true;
		}

		/// <summary>Runs production successor resolution and protection preflight without mutation.</summary>
	}
}
