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

		private static bool BeginCore(KingdomSystem System, Zone Z, GameObject Work,
			Assessment A, KingdomSurvey Survey, PreparedImprovement Prepared)
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
			if (Prepared == null
				? !TryPrepareImprovementPayload(System, Z, Work, A, out payload,
					out _, out _, out _, out architectureFailure)
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
				System.Ledger.Note("{{r|The improvement waits. Its exact build effects could not be frozen.}}");
				return false;
			}
			if (hostedAuthority && !KingdomHostedArcology.TryReserve(System, Z, Work,
				job.Id, out string hostedFailure))
			{
				water.Rollback();
				materials.Cancel();
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
				KingdomLog.Log("improvement refused cleanly: "
					+ (fundingFailure ?? A.SuccessorKey));
				return false;
			}
			KingdomConstruction.Bind(Work, job);
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
