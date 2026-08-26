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
			KingdomWaterDebit water = Survey.ReserveExactWater(A.CostDrams);
			KingdomMaterialTally transitionMaterials = A.Transition == null
				? null : A.Transition.Materials;
			KingdomMaterialDebit materials;
			if (A.Transition == null)
				materials = KingdomMaterials.ReserveUpgradePayment(Z, A.Key);
			else
				materials = KingdomMaterials.ReserveTransitionPayment(Z, transitionMaterials);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				A.Transition == null ? KingdomMaterials.UpgradeCostFor(A.Key)
					: transitionMaterials);
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
			if (A.Transition != null)
			{
				KingdomArchitectureIntent before;
				KingdomArchitectureIntent after;
				KingdomPlotRules.PlotRect transitionRect;
				string transitionSkin;
				bool legacy;
				string transitionFailure = null;
				if (!KingdomArchitectureRuntime.TryRead(Work, out before, out _)
					|| !KingdomPlots.TryDecodePlotPayload(payload, out transitionRect,
						out transitionSkin, out after, out legacy, out _)
					|| legacy || after == null
					|| !KingdomSocketTransitions.BindReceipt(Work, job, before, after,
						A.Transition, out transitionFailure))
				{
					KingdomSocketTransitions.ClearReceipt(Work);
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
				if (A.Transition != null) KingdomSocketTransitions.ClearReceipt(Work);
				KingdomLog.Log("improvement refused cleanly: "
					+ (fundingFailure ?? A.SuccessorKey));
				return false;
			}
			KingdomConstruction.Bind(Work, job);
			if (A.Transition != null)
			{
				KingdomArchitectureIntent before;
				KingdomArchitectureIntent after;
				KingdomPlotRules.PlotRect transitionRect;
				string transitionSkin;
				bool legacy;
				if (!KingdomArchitectureRuntime.TryRead(Work, out before, out _)
					|| !KingdomPlots.TryDecodePlotPayload(payload, out transitionRect,
						out transitionSkin, out after, out legacy, out _)
					|| legacy || !KingdomSocketTransitions.Authorizes(Work, before, after))
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
