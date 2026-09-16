using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal const int SavedBrushUnits = MintedBrushUnits - 2;
		private sealed partial class Frame { internal string TentJobId; }

		private static string PaidTent(Frame Frame, List<KingdomConstructionJob> Jobs)
		{
			KingdomConstructionJob found = null;
			foreach (var job in Jobs)
			{
				if (job?.TargetKey != ClaimKey || job.OwnerKey != KingdomConstruction.OwnerOf(Frame.System)
					|| job.ZoneId != Frame.Zone.ZoneID) continue;
				Require(found == null, "camp has more than one tent job");
				found = job;
			}
			var brush = new KingdomMaterialTally();
			brush.Add(KingdomMaterial.Brush, MintedBrushUnits - SavedBrushUnits);
			Require(found != null && found.Id != Frame.JobId && found.Route == KingdomConstructionRoute.PlotCommission
				&& (found.Phase == KingdomConstructionPhase.Working || found.Phase == KingdomConstructionPhase.Complete)
				&& KingdomQuickstartBuildClaims.CleanFirstPayment(found.Claims, 3, new KingdomMaterialDebitCost(brush)),
				"camp tent has no distinct clean receipt for three drams and two brush");
			Require(KingdomConstruction.FindExactId(Frame.Zone, found.OutputId, out GameObject output) == KingdomPhysicalLookupState.Exact,
				"paid camp tent output is absent or ambiguous");
			ExactGround(Frame.Zone, output);
			Require(KingdomConstruction.HasReceipt(output, found) && output.CurrentCell.X == found.X
				&& output.CurrentCell.Y == found.Y, "paid camp tent output has lost its receipt or anchor");
			Require(found.Phase == KingdomConstructionPhase.Working
				? output.GetPart<XRL.World.Parts.r_KingdomPlotWorks>()?.DesignKey == ClaimKey
				: found.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
					&& KingdomUpgrade.IsFunctionallyBuilt(output) && KingdomUpgrade.DesignKeyOf(output) == ClaimKey,
				"paid camp tent output does not match its working or completed receipt");
			return found.Id;
		}
	}
}
