using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{

		private static bool ResumeStrikeStamp(Zone Z, GameObject Building,
			KingdomStrikeIntent Intent, ref KingdomConstructionJob Job)
		{
			if (Intent == null || Intent.Effort <= 0 || Intent.Targets == null
				|| !GameObject.Validate(Building) || Building.IDIfAssigned != Job.SourceId
				|| Building.CurrentZone != Z || Building.CurrentCell != Z.GetCell(Job.X, Job.Y)
				|| Building.GetIntProperty("KingdomBuilt") != 1)
			{
				KingdomConstruction.Quarantine(ref Job,
					"The strike predecessor changed before its order could be stamped.");
				return false;
			}
			if (Job.PhysicalPhase == KingdomPhysicalPhase.StrikeOrdered
				&& !KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.StrikeStampPending, 0, 0, 0, null, null,
					Job.PhysicalReceipt)) return false;
			if (Job.PhysicalPhase != KingdomPhysicalPhase.StrikeStampPending
				&& Job.PhysicalPhase != KingdomPhysicalPhase.StrikeWorking) return false;
			if (Job.PhysicalPhase == KingdomPhysicalPhase.StrikeStampPending)
			{
				KingdomConstruction.Bind(Building, Job);
				Building.SetIntProperty(StrikeEffortProperty, Intent.Effort);
				Building.SetIntProperty(StrikeTotalProperty, Intent.Effort);
				Building.SetIntProperty(StrikeAnnouncedProperty, 0);
				WriteTick(Building, StrikeWorkedProperty, The.Game.TimeTicks);
				if (!GameObject.Validate(Building) || Building.IDIfAssigned != Job.SourceId
					|| Building.CurrentZone != Z || !KingdomConstruction.HasReceipt(Building, Job)
					|| Building.GetIntProperty(StrikeEffortProperty) != Intent.Effort
					|| Building.GetIntProperty(StrikeTotalProperty) != Intent.Effort)
				{
					KingdomConstruction.Quarantine(ref Job,
						"The strike predecessor changed while its order was stamped.");
					return false;
				}
				if (!KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.StrikeWorking, 0, 0, 0, null, null,
					Job.PhysicalReceipt)) return false;
			}
			return Job.Route != KingdomConstructionRoute.Strike
				|| Job.Phase == KingdomConstructionPhase.Working
				|| KingdomConstruction.FinishProjection(ref Job, true, true);
		}

		private static bool FinishStrikeCancellation(Zone Z, GameObject Building,
			ref KingdomConstructionJob Job)
		{
			if (Job == null || Job.PhysicalPhase != KingdomPhysicalPhase.StrikeCancellationPending
				|| !GameObject.Validate(Building) || Building.IDIfAssigned != Job.SourceId
				|| Building.CurrentZone != Z || Building.CurrentCell != Z.GetCell(Job.X, Job.Y)
				|| !KingdomConstruction.HasReceipt(Building, Job))
			{
				KingdomConstruction.Quarantine(ref Job,
					"Strike cancellation lost its exact live predecessor.");
				return false;
			}
			// Cleanup is published and proved before the terminal receipt. A save in this
			// phase only repeats idempotent property removal; it can never resume strike work.
			Building.SetIntProperty(StrikeEffortProperty, 0);
			Building.SetIntProperty(StrikeTotalProperty, 0);
			Building.SetIntProperty(StrikeAnnouncedProperty, 0);
			Building.RemoveStringProperty(StrikeWorkedProperty);
			if (!GameObject.Validate(Building) || Building.IDIfAssigned != Job.SourceId
				|| Building.CurrentZone != Z || Building.GetIntProperty(StrikeEffortProperty) != 0
				|| Building.GetIntProperty(StrikeTotalProperty) != 0
				|| !string.IsNullOrEmpty(Building.GetStringProperty(StrikeWorkedProperty)))
			{
				KingdomConstruction.Quarantine(ref Job,
					"Strike cancellation cleanup could not be proved exact.");
				return false;
			}
			return KingdomConstruction.Cancel(ref Job, "The strike order was called off.");
		}
	}
}
