using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static bool TryFinishRemoval(Zone Z, Cell cell,
			KingdomPlotRules.PlotRect Footprint, KingdomRules.BuildEntry entry,
			GameObject parent, GameObject building,
			bool created, bool currentAuthored, string contents, string id,
			string receipt, ref KingdomConstructionJob construction)
		{
			if (construction != null)
			{
				if (currentAuthored)
				{
					if (construction.PhysicalPhase == KingdomPhysicalPhase.FinalOutputSettled
						&& !KingdomConstruction.UpdatePhysical(ref construction,
							KingdomPhysicalPhase.FurnishingSettled,
							construction.PhysicalIndex, construction.PhysicalAmount,
							construction.PhysicalSpilled, construction.PhysicalItemId,
							construction.PhysicalDestinationId,
							construction.PhysicalReceipt)) return false;
					if (construction.PhysicalPhase != KingdomPhysicalPhase.FurnishingSettled
						&& construction.PhysicalPhase != KingdomPhysicalPhase.FinalRemovalPending
						&& construction.PhysicalPhase != KingdomPhysicalPhase.FinalRemoved
						&& construction.PhysicalPhase != KingdomPhysicalPhase.EffectsPending
						&& construction.PhysicalPhase != KingdomPhysicalPhase.EffectsSettled)
					{
						KingdomConstruction.Quarantine(ref construction,
							"The authored plot finalization carries an impossible layout phase.");
						return false;
					}
				}
				else if (!FurnishDurable(Z, Footprint, contents, id, entry.Key,
					ref construction)) return false;
			}
			else if (!currentAuthored && !FurnishLegacyDurable(building, Z, Footprint,
				contents, id, entry.Key)) return false;
			// Final projection proved before predecessor removal. Keep DesignKey intact until the
			// vetoable callback has actually invalidated the exact predecessor, so a retry remains live.
			string predecessorId = parent.ID;
			if (construction != null)
			{
				if (construction.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending)
				{
					KingdomConstruction.Quarantine(ref construction,
						"Plot predecessor removal was interrupted before callback-success proof.");
					return false;
				}
				if (construction.PhysicalPhase != KingdomPhysicalPhase.FurnishingSettled
					|| !KingdomConstruction.UpdatePhysical(ref construction,
						KingdomPhysicalPhase.FinalRemovalPending, construction.PhysicalIndex,
						construction.PhysicalAmount,
						construction.PhysicalSpilled, predecessorId, building.ID,
						construction.PhysicalReceipt)) return false;
			}
			bool removed;
			try { removed = parent.Destroy(null, Silent: true); }
			catch (System.Exception ex)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, parent);
				if (construction != null) KingdomConstruction.Quarantine(ref construction,
					"Plot predecessor removal threw: " + ex.Message);
				return false;
			}
			if (removed && !GameObject.Validate(parent))
				KingdomSurvey.ObserveRemovedFromActive(Z, parent);
			KingdomPhysicalLookupState predecessorState = construction == null
				? (GameObject.Validate(parent) ? KingdomPhysicalLookupState.Exact
					: KingdomPhysicalLookupState.Absent)
				: KingdomConstruction.FindExactId(Z, predecessorId, out _);
			KingdomSystem ownerSystem = construction == null || The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			if (!removed || GameObject.Validate(parent)
				|| predecessorState != KingdomPhysicalLookupState.Absent
				|| (construction != null && !KingdomConstruction.Owns(ownerSystem, Z, construction)))
			{
				if (construction != null)
					KingdomConstruction.Quarantine(ref construction,
						"Plot predecessor removal was vetoed, moved, or partially changed.");
				return false;
			}
			if (building.CurrentCell != cell || building.ID != (construction == null
					? parent.GetStringProperty(FinalOutputIdProperty) : construction.OutputId)
				|| building.GetIntProperty("KingdomBuilt") != 1
				|| building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != entry.Key
				|| (!string.IsNullOrEmpty(receipt)
					&& building.GetStringProperty(KingdomConstruction.ReceiptProperty) != receipt)
				|| (construction != null
					&& !KingdomConstruction.PaidBuildMatches(building, construction)))
			{
				if (construction != null)
					KingdomConstruction.Quarantine(ref construction,
						"The completed plot changed during predecessor removal.");
				return false;
			}
			if (construction != null && !KingdomConstruction.UpdatePhysical(ref construction,
				KingdomPhysicalPhase.FinalRemoved, construction.PhysicalIndex,
				construction.PhysicalAmount,
				construction.PhysicalSpilled, predecessorId, building.ID,
				construction.PhysicalReceipt)) return false;
			building.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, predecessorId);
			if (!r_KingdomScaffold.HasRemovalProof(building, predecessorId))
			{
				if (construction != null) KingdomConstruction.Quarantine(ref construction,
					"The completed plot did not retain exact works-removal proof.");
				return false;
			}
			return true;
		}
	}
}
