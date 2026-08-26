using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomSocket
	{
		private static bool ExecuteSocketBuild(KingdomSystem System, Zone Z, GameObject Marker,
			PreparedSocketBuild Prepared, out string Failure)
		{
			Failure = null;
			if (Prepared == null || !GameObject.Validate(Marker)
				|| Marker.ID != Prepared.MarkerId || Marker.CurrentZone != Z
				|| Marker.GetPart<r_KingdomSocket>() == null || HasBlockingReceipt(Marker)
				|| !KingdomPlots.TryReadRect(Marker, out KingdomPlotRules.PlotRect liveRect)
				|| liveRect.X1 != Prepared.Rect.X1 || liveRect.Y1 != Prepared.Rect.Y1
				|| liveRect.X2 != Prepared.Rect.X2 || liveRect.Y2 != Prepared.Rect.Y2)
			{
				Failure = "The previewed cleared plot changed before consent.";
				return false;
			}
			KingdomRules.BuildEntry entry = Prepared.Entry;
			KingdomArchitectureIntent architecture = Prepared.Architecture;
			string payload = Prepared.Payload;
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(entry.Key), KingdomMaterials.BitCostFor(entry.Key),
				KingdomMaterials.ExoticCostFor(entry.Key));
			if (!KingdomArchitectureStamper.TryPreflight(System, Z, architecture, claim,
				out Failure)) return false;
			if (!KingdomPlots.TryGetSpec(entry.Key, out KingdomPlotRules.PlotSpec liveSpec)
				|| !TrySocketBuildLabour(System, Z, Prepared.Rect, entry, liveSpec,
					out long liveLabour, out Failure)
				|| liveLabour != Prepared.LabourTicks)
			{
				if (Failure == null)
					Failure = "The cleared plot's labour changed after its preview.";
				return false;
			}
			Cell mainCell = Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			if (mainCell == null || KingdomConstruction.HasActiveAt(System, Z, mainCell))
			{
				Failure = "The authored building's main ground already has paid construction in hand.";
				return false;
			}
			if (KingdomGrowth.CountStoredWater(Z) < entry.CostDrams)
			{
				Failure = "The work would cost {{C|" + entry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			if (!KingdomMaterials.CanPay(Z, entry.Key, out string materialFailure))
			{
				Failure = materialFailure;
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomWaterDebit water = survey.ReserveExactWater(entry.CostDrams);
			KingdomMaterialDebit materials = KingdomMaterials.ReservePayment(Z, entry.Key);
			long start = The.Game.TimeTicks;
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.SocketBuild, mainCell, Marker,
				entry.Key, payload, entry.CostDrams, claim, start,
				start + Prepared.LabourTicks);
			if (!KingdomConstruction.FreezeBuildTruth(job, System, entry.Defence, true))
			{
				water.Rollback();
				materials.Cancel();
				Failure = "The cleared plot's exact build effects could not be frozen.";
				return false;
			}
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stores could not cover the work after all.";
				return false;
			}
			KingdomConstruction.Bind(Marker, job);
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				KingdomGovernanceScope.Commit("build on cleared plot");
				System.Ledger.Note("{{r|The cleared plot's construction receipt remains outstanding. It will retry without charging any paid claim twice.}}");
				return true;
			}
			ContinueSocketBuild(System, Z, job, true);
			KingdomGovernanceScope.Commit("build on cleared plot");
			if (KingdomConstruction.TryFind(job.Id, out var observed)
				&& observed.Phase == KingdomConstructionPhase.InspectionRequired)
				System.Ledger.Note("{{r|The cleared plot's exact removal or output receipt needs inspection; it will not retry either callback.}}");
			KingdomLog.Log("socket: ordered " + entry.Key + " on cleared ground at "
				+ Prepared.Rect.X1 + "," + Prepared.Rect.Y1);
			return true;
		}
	}
}
