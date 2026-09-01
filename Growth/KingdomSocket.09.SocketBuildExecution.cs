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
				|| Marker.IDIfAssigned != Prepared.MarkerId || Marker.CurrentZone != Z
				|| Marker.GetPart<r_KingdomSocket>() == null || HasBlockingReceipt(Marker)
				|| !KingdomPlots.TryReadRect(Marker, out KingdomPlotRules.PlotRect liveRect)
				|| liveRect.X1 != Prepared.Rect.X1 || liveRect.Y1 != Prepared.Rect.Y1
				|| liveRect.X2 != Prepared.Rect.X2 || liveRect.Y2 != Prepared.Rect.Y2)
			{
				Failure = "The previewed cleared lot changed before consent.";
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
			if (!TrySocketBuildLabour(System, Z, Prepared.Rect, entry, architecture,
					out long liveLabour, out Failure)
				|| liveLabour != Prepared.LabourTicks)
			{
				if (Failure == null)
					Failure = "The cleared lot's labour changed after its preview.";
				return false;
			}
			Cell mainCell = Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			if (mainCell == null || KingdomConstruction.HasActiveAt(System, Z, mainCell))
			{
				Failure = "The authored building's main ground already has paid construction in hand.";
				return false;
			}
			if (!GameObject.Validate(Marker) || Marker.IDIfAssigned != Prepared.MarkerId
				|| Marker.CurrentZone != Z
				|| Marker.CurrentCell != Z.GetCell(Prepared.Rect.CenterX, Prepared.Rect.CenterY)
				|| Marker.GetPart<r_KingdomSocket>() == null || HasBlockingReceipt(Marker)
				|| !KingdomPlots.TryReadRect(Marker, out liveRect)
				|| liveRect.X1 != Prepared.Rect.X1 || liveRect.Y1 != Prepared.Rect.Y1
				|| liveRect.X2 != Prepared.Rect.X2 || liveRect.Y2 != Prepared.Rect.Y2)
			{
				Failure = "The cleared lot's identity or rectangle changed before its exact debit.";
				return false;
			}
			if (!KingdomPlots.TryDecodePlotPayload(payload, out var promisedRect,
				out string promisedSkin, out KingdomArchitectureIntent promisedArchitecture,
				out bool legacyPromise, out Failure) || legacyPromise
				|| promisedRect.X1 != Prepared.Rect.X1 || promisedRect.Y1 != Prepared.Rect.Y1
				|| promisedRect.X2 != Prepared.Rect.X2 || promisedRect.Y2 != Prepared.Rect.Y2
				|| promisedArchitecture.BuildKey != entry.Key
				|| promisedArchitecture.EncodedSnapshot != architecture.EncodedSnapshot
				|| promisedArchitecture.SnapshotHash != architecture.SnapshotHash
				|| promisedArchitecture.MainWorldX != architecture.MainWorldX
				|| promisedArchitecture.MainWorldY != architecture.MainWorldY
				|| promisedSkin != (string.IsNullOrEmpty(Prepared.SkinKey) ? null : Prepared.SkinKey))
			{
				if (Failure == null)
					Failure = "The prepared architecture promise changed before its exact debit.";
				return false;
			}
			if (!SocketAcceptsArchitecture(Marker, promisedArchitecture, out Failure)) return false;
			architecture = promisedArchitecture;
			// A cleared local lot may be supplied by exact realm routes. The funding receipt,
			// not a settlement-local preview check, owns affordability and custody.
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
				Failure = "The cleared lot's exact build effects could not be frozen.";
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
				System.Ledger.Note("{{r|The cleared lot's construction receipt remains outstanding. It will retry without charging any paid claim twice.}}");
				return true;
			}
			ContinueSocketBuild(System, Z, job, true);
			KingdomGovernanceScope.Commit("build on cleared plot");
			if (KingdomConstruction.TryFind(job.Id, out var observed)
				&& observed.Phase == KingdomConstructionPhase.InspectionRequired)
				System.Ledger.Note("{{r|The cleared lot's exact removal or output receipt needs inspection; it will not retry either callback.}}");
			KingdomLog.Log("socket: ordered " + entry.Key + " on cleared ground at "
				+ Prepared.Rect.X1 + "," + Prepared.Rect.Y1);
			return true;
		}
	}
}
