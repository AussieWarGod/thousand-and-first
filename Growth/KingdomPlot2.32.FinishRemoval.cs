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
			bool foundingHeart = FoundingHeartWorkIdentityEvidence(parent);
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
			string predecessorId = parent.IDIfAssigned;
			string expectedFinalId = construction == null
				? parent.GetStringProperty(FinalOutputIdProperty) : construction.OutputId;
			bool freshRemovalAttempt = construction == null;
			if (construction != null)
			{
				if (construction.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending)
				{
					KingdomSystem recoverySystem = The.Game == null
						? null : The.Game.RequireSystem<KingdomSystem>();
					RecoverPendingPlotRemoval(recoverySystem, Z, building, ref construction);
					return construction.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved
						|| construction.PhysicalPhase == KingdomPhysicalPhase.EffectsPending
						|| construction.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled;
				}
				else if (construction.PhysicalPhase != KingdomPhysicalPhase.FurnishingSettled
					|| !KingdomConstruction.UpdatePhysical(ref construction,
						KingdomPhysicalPhase.FinalRemovalPending, construction.PhysicalIndex,
						construction.PhysicalAmount,
						construction.PhysicalSpilled, predecessorId, building.IDIfAssigned,
						construction.PhysicalReceipt)) return false;
				else freshRemovalAttempt = true;
			}
			if (!freshRemovalAttempt) return false;
			bool returned = false;
			bool removed = false;
			try { removed = parent.Destroy(null, Silent: true); returned = true; }
			catch { }
			finally { KingdomSurvey.ObserveCurrentTopologyInActive(Z, parent); }
			KingdomPhysicalLookupState predecessorState = KingdomConstruction.FindExactId(
				Z, predecessorId, out _);
			KingdomSystem ownerSystem = construction == null || The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			if (!ExactPlotFinalRootCustody(expectedFinalId, building)
				|| !KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(returned, removed,
				GameObject.Validate(parent), predecessorState == KingdomPhysicalLookupState.Absent,
				ExactPlotRemovalTombstone(predecessorId, parent, construction))
				|| (construction != null && !KingdomConstruction.Owns(ownerSystem, Z, construction)))
			{
				if (construction != null)
					KingdomConstruction.Quarantine(ref construction,
						"Plot predecessor removal was vetoed, moved, or partially changed.");
				return false;
			}
			KingdomSurvey.ObserveRemovedFromActive(Z, parent);
			if (foundingHeart && (!ExactFoundingHeartRetiredAuthority(Z, predecessorId,
				out FoundingHeartContext foundingAfter)
				|| !ExactFoundingHeartFinalTruth(building, foundingAfter.Stake))) return false;
			if (building.CurrentCell != cell || building.IDIfAssigned != expectedFinalId
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
			building.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, predecessorId);
			if (!r_KingdomScaffold.HasRemovalProof(building, predecessorId))
			{
				if (construction != null) KingdomConstruction.Quarantine(ref construction,
					"The completed plot did not retain exact works-removal proof.");
				return false;
			}
			if (construction != null && !KingdomConstruction.UpdatePhysical(ref construction,
				KingdomPhysicalPhase.FinalRemoved, construction.PhysicalIndex,
				construction.PhysicalAmount, construction.PhysicalSpilled, predecessorId,
				building.IDIfAssigned, construction.PhysicalReceipt)) return false;
			return true;
		}
	}
}
