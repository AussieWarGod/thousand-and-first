using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Proves the one live paid plot root allowed to apply physical labour stages.</summary>
		private static bool TryPlotLabourAuthority(KingdomSystem System, Zone Z,
			GameObject Root, r_KingdomPlotWorks Works, KingdomConstructionJob Job,
			out string Failure)
		{
			Failure = "The plot has no exact current Working receipt at its frozen anchor.";
			if (!GameObject.Validate(Root) || Works == null || Works.ParentObject != Root
				|| Job == null || Job.Phase != KingdomConstructionPhase.Working
				|| !Owns(System, Z, Job) || !IsCurrent(Job)
				|| Root.GetIntProperty(KingdomPlots.PlotWorkSchemaProperty)
					!= KingdomPlots.PlotWorkSchema
				|| (Job.Route != KingdomConstructionRoute.PlotCommission
					&& Job.Route != KingdomConstructionRoute.PlotPlan)
				|| !KingdomConstructionRules.TryReadBuildTruth(Job,
					out bool hasPlot, out bool frontier, out _) || !hasPlot || frontier
				|| Job.TargetKey != Works.DesignKey || !HasReceipt(Root, Job)
				|| string.IsNullOrEmpty(Job.OutputId) || Root.IDIfAssigned != Job.OutputId
				|| Root.IDIfAssigned != Job.SubjectId) return false;
			if (!TryFind(Job.Id, out KingdomConstructionJob current)
				|| current.Revision != Job.Revision || current.Payload != Job.Payload
				|| current.OutputId != Job.OutputId) return false;
			if (FindExactId(Z, Job.OutputId, out GameObject exactId)
				!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exactId, Root)
				|| FindReceipt(Z, Job, out GameObject exactReceipt)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exactReceipt, Root))
			{
				Failure = "The paid plot receipt is duplicated or names a different physical root.";
				return false;
			}
			if (!KingdomPlots.TryDecodePlotPayload(Job.Payload,
				out KingdomPlotRules.PlotRect rect, out _,
				out KingdomArchitectureIntent architecture, out bool legacy, out _)
				|| (!legacy && architecture == null)) return false;
			Cell expected = legacy ? Z.GetCell(rect.CenterX, rect.CenterY)
				: Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			KingdomPlotRules.PlotRect live = Works.Rect();
			if (expected == null || Root.CurrentZone != Z || Root.CurrentCell != expected
				|| Job.X != expected.X || Job.Y != expected.Y
				|| live.X1 != rect.X1 || live.Y1 != rect.Y1
				|| live.X2 != rect.X2 || live.Y2 != rect.Y2
				|| !KingdomPlots.ExpectedArchitectureReceipt(Root, expected,
					Works.DesignKey, architecture, legacy)) return false;
			Failure = null;
			return true;
		}

		/// <summary>Exact loaded yard factor after plot identity and phase authority are proved.</summary>
		private static int PlotInfrastructurePercent(GameObject Root, r_KingdomPlotWorks Works,
			KingdomConstructionJob Job,
			IList<KingdomMaterialRules.KingdomYardStanding> Yards, out string Failure)
		{
			Failure = null;
			if (!KingdomConstructionRules.TryPaidBuildReceipt(Job, null,
				out KingdomPaidBuildReceipt paid)
				|| !KingdomPlots.TryDecodePlotPayload(Job.Payload,
					out KingdomPlotRules.PlotRect rect, out _,
					out KingdomArchitectureIntent architecture, out bool legacy, out _))
			{
				Failure = "The plot's paid material receipt cannot prove its construction yards.";
				return KingdomPlotLabourWindowRules.InfrastructureUnavailable;
			}
			KingdomPlotRules.PlotSize size = legacy
				? KingdomPlotRules.SmallestPlotFor(rect.Width, rect.Height)
				: (KingdomPlotRules.PlotSize)architecture.LotSize;
			if (size < KingdomPlotRules.PlotSize.Small || size > KingdomPlotRules.PlotSize.Huge)
			{
				Failure = "The plot's frozen lot size cannot be priced for construction infrastructure.";
				return KingdomPlotLabourWindowRules.InfrastructureUnavailable;
			}
			return KingdomMaterialRules.AllowsBuild(size, paid.Material.Materials, Yards,
				Works.DisplayName, out Failure)
				? KingdomPlotLabourWindowRules.InfrastructureReady
				: KingdomPlotLabourWindowRules.InfrastructureUnavailable;
		}
	}
}
