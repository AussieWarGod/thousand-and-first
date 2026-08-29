using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterials
	{
		private static void RemoveStrikePlotPart(Zone Z, KingdomStrikeIntent Intent,
			GameObject Part, ref KingdomConstructionJob Job)
		{
			KingdomStrikeTarget frozen = Intent != null && Intent.Targets != null
				&& Job.PhysicalIndex >= 0 && Job.PhysicalIndex < Intent.Targets.Count
				? Intent.Targets[Job.PhysicalIndex] : null;
			bool networkStrike = KingdomGatehouseRules.IsNetworkStrike(Intent.BuildKey,
				Intent.HasPlot, Intent.X1, Intent.Y1, Intent.X2, Intent.Y2,
				Intent.PlotId, Intent.Targets.Count);
			GameObject resolved = null;
			bool owned = networkStrike
				? frozen != null && KingdomGatehouse.TryResolveStrikeSatellite(Z,
					Intent.PlotId, Job.PhysicalIndex, frozen.Id, frozen.Blueprint,
					frozen.X, frozen.Y, out resolved) && ReferenceEquals(resolved, Part)
				: GameObject.Validate(Part)
					&& Part.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
					&& Part.GetStringProperty(KingdomPlots.PlotIdProperty) == Intent.PlotId;
			if (frozen == null || !GameObject.Validate(Part) || Part.IDIfAssigned != frozen.Id
				|| Part.Blueprint != frozen.Blueprint
				|| Part.CurrentCell != Z.GetCell(frozen.X, frozen.Y)
				|| Part.CurrentZone != Z || Part.IDIfAssigned == Job.SourceId || !owned
				|| (!networkStrike && !ReferenceEquals(ExactObject(Part.IDIfAssigned), Part))
				|| !StrikeObjectUnencumbered(Part, out _))
			{
				QuarantineStrike(Job, "A plot part changed before exact removal intent published.");
				return;
			}
			string id = Part.IDIfAssigned;
			if (!KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.PlotPartRemovalPending, Job.PhysicalIndex, 0,
				Job.PhysicalSpilled, id, null, Job.PhysicalReceipt)) return;
			if (!StrikeObjectUnencumbered(Part, out string obstruction))
			{
				QuarantineStrike(Job, obstruction);
				return;
			}
			if (networkStrike && (!KingdomGatehouse.TryResolveStrikeSatellite(Z,
				Intent.PlotId, Job.PhysicalIndex, frozen.Id, frozen.Blueprint,
				frozen.X, frozen.Y, out resolved) || !ReferenceEquals(resolved, Part)))
			{
				QuarantineStrike(Job, "A gatehouse satellite changed at its removal boundary.");
				return;
			}
			bool removed;
			try { removed = Part.Obliterate(null, Silent: true); }
			catch (Exception ex)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, Part);
				QuarantineStrike(Job, "Plot-part removal threw: " + ex.Message);
				return;
			}
			if (removed || !GameObject.Validate(Part))
				KingdomSurvey.ObserveRemovedFromActive(Z, Part);
			bool exactAbsent = networkStrike
				? KingdomGatehouse.LoadedIdentityAbsent(Z, id) : ExactObject(id) == null;
			if (!removed || GameObject.Validate(Part) || !exactAbsent)
			{
				QuarantineStrike(Job, "Plot-part removal was vetoed, moved, or replaced.");
				return;
			}
			KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.StrikeWorkComplete,
				Job.PhysicalIndex + 1, 0, Job.PhysicalSpilled, null, null, Job.PhysicalReceipt);
		}
	}
}
