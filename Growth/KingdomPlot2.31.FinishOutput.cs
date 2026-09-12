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
		private static bool TryFinishOutput(r_KingdomPlotWorks Works, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomPlotRules.PlotRect Footprint,
			KingdomPlotRules.RoofState Roof, GameObject parent, Cell cell,
			KingdomRules.BuildEntry entry, KingdomArchitectureIntent architecture,
			bool legacyArchitecture, bool currentAuthored, string id,
			string skinColorString, string skinDetailColor, string skinRenderString,
			string skinTile, string displayName, long completeTick, string planQuote,
			bool heart, bool yielding, int defence, int staff, bool threshold,
			string receipt, ref KingdomConstructionJob construction,
			out GameObject Building, out bool Created)
		{
			Building = null;
			Created = false;
			GameObject building = null;
			string expectedOutput = construction == null
				? parent.GetStringProperty(FinalOutputIdProperty) : construction.OutputId;
			bool hasFrozenFinal = !string.IsNullOrEmpty(expectedOutput)
				&& expectedOutput != parent.IDIfAssigned;
			if (hasFrozenFinal)
			{
				if (!TryPlotFinalRoot(expectedOutput, out building))
				{
					if (construction != null) KingdomConstruction.Quarantine(ref construction,
						"The frozen final plot output lost its canonical custody root.");
					return false;
				}
			}
			else
			{
				KingdomPhysicalLookupState rootState = FindPlotFinalRootForPredecessor(
					parent.IDIfAssigned, out building);
				if (rootState == KingdomPhysicalLookupState.Ambiguous)
				{
					if (construction != null) KingdomConstruction.Quarantine(ref construction,
						"The plot predecessor has ambiguous canonical final custody.");
					return false;
				}
				if (rootState == KingdomPhysicalLookupState.Exact)
				{
					expectedOutput = building.IDIfAssigned;
					if (!PreparedPlotFinalOutput(building, parent, entry, receipt, id, Rect,
						Footprint, Roof, expectedOutput, construction))
						return FinishOutputFault(parent, "the rooted final output is not prepared");
					if (construction != null)
					{
						if (!KingdomConstruction.UpdateFinalOutput(ref construction,
							parent.IDIfAssigned, expectedOutput))
							return FinishOutputFault(parent,
								"the receipt would not take the rooted final output id");
					}
					else parent.SetStringProperty(FinalOutputIdProperty, expectedOutput);
						hasFrozenFinal = true;
					}
				}
				if (building != null && (building.IDIfAssigned != expectedOutput
					|| building.GetStringProperty(PlotFinalPredecessorProperty) != parent.IDIfAssigned
					|| (!string.IsNullOrEmpty(receipt)
						&& building.GetStringProperty(KingdomConstruction.ReceiptProperty) != receipt)
					|| (construction != null && (!KingdomConstruction.HasReceipt(building, construction)
						|| !KingdomConstruction.PaidBuildMatches(building, construction)))
					|| !PlotPlanMarkerRemovalProofMatches(parent, building)))
				{
					if (construction != null) KingdomConstruction.Quarantine(ref construction,
						"The rooted final plot lacks authenticated predecessor provenance.");
					return false;
				}
				bool created = building == null;
			if (created)
			{
				try { building = GameObject.Create(entry.Blueprint); }
				catch (System.Exception ex)
				{
					if (construction != null) KingdomConstruction.Quarantine(ref construction,
						"Final plot creation threw: " + ex.Message);
					return false;
				}
			}
			if (building == null)
			{
				return FinishOutputFault(parent, "no final building to publish");
			}
			if (created && !legacyArchitecture)
			{
				string copyFailure;
				bool copied = currentAuthored
					? KingdomArchitectureStamper.TryCopyFrozenOwner(parent, building,
						out copyFailure)
					: KingdomArchitectureRuntime.TryCopyFrozen(parent, building,
						out copyFailure);
				if (!copied)
				{
					RemoveCreatedWorks(building, Z);
					if (construction != null) KingdomConstruction.Quarantine(ref construction,
						"The final plot could not inherit its frozen authored receipt: "
							+ copyFailure);
					return false;
				}
			}
			if (created && !KingdomPurpose.CopyCommit(parent, building))
			{
				RemoveCreatedWorks(building, Z);
				if (construction != null) KingdomConstruction.Quarantine(ref construction,
					"The final building could not inherit its frozen city-purpose commitment.");
				return false;
			}
			if (created && !TryCopyPlotPlanMarkerRemovalProof(parent, building))
			{
				RemoveCreatedWorks(building, Z);
				if (construction != null) KingdomConstruction.Quarantine(ref construction,
					"The final plot could not retain its plan-marker removal proof.");
				return false;
			}
			if (created)
			{
				PrepareFinalBuilding(building, entry, receipt, id, Rect, Footprint, Roof,
					skinColorString, skinDetailColor, skinRenderString, skinTile, displayName,
					completeTick, planQuote, heart, yielding, defence, staff, threshold);
				if (construction != null
					&& !KingdomConstruction.FreezePaidBuild(building, construction))
				{
					RemoveCreatedWorks(building, Z);
					KingdomConstruction.Quarantine(ref construction,
						"The exact paid plot receipt could not be frozen on its final output.");
					return false;
				}
				building.SetStringProperty(PlotFinalPredecessorProperty, parent.IDIfAssigned);
				// The engine assigns object ids lazily: a freshly Created output has none yet, so
				// IDIfAssigned here refused every finish silently (issue #172) -- the same class
				// fixed on the staging side on 2026-08-30 (RootStagingOutput). The custody root
				// needs a durable id; asking for ID assigns one, which is exactly what publishing
				// a brand-new final output means.
				expectedOutput = building.ID;
				if (!PreparedPlotFinalOutput(building, parent, entry, receipt, id, Rect,
					Footprint, Roof, expectedOutput, construction))
					return FinishOutputFault(parent, "the new final output is not prepared");
				if (!RootPlotFinalOutput(expectedOutput, building))
					return FinishOutputFault(parent,
						"the new final output could not be rooted under id "
							+ (string.IsNullOrEmpty(expectedOutput) ? "<none>" : expectedOutput));
				if (construction != null)
				{
					if (!KingdomConstruction.UpdateFinalOutput(ref construction,
						parent.IDIfAssigned, building.ID))
					{
						return FinishOutputFault(parent,
							"the receipt would not take the new final output id");
					}
				}
				else parent.SetStringProperty(FinalOutputIdProperty, building.ID);
				if ((construction == null
						? parent.GetStringProperty(FinalOutputIdProperty) : construction.OutputId)
					!= expectedOutput)
					return FinishOutputFault(parent, "the published final output id did not stick");
			}
			if (construction != null
				&& construction.PhysicalPhase != KingdomPhysicalPhase.FinalOutputPending
				&& construction.PhysicalPhase != KingdomPhysicalPhase.FinalOutputSettled
				&& !KingdomConstruction.UpdatePhysical(ref construction,
					KingdomPhysicalPhase.FinalOutputPending, construction.PhysicalIndex,
					construction.PhysicalAmount, construction.PhysicalSpilled,
					parent.IDIfAssigned, building.ID, construction.PhysicalReceipt))
				return FinishOutputFault(parent, "the physical phase would not reach pending");
			if (construction != null
				&& (construction.PhysicalItemId != parent.IDIfAssigned
					|| construction.PhysicalDestinationId != expectedOutput))
				return FinishOutputFault(parent, "the pending physical endpoints disagree");
			GameObject accepted = null;
			bool callbackReturned = false;
			if (building.CurrentCell == null && building.InInventory == null)
			{
				if (!PreparedPlotFinalOutput(building, parent, entry, receipt, id, Rect,
					Footprint, Roof, expectedOutput, construction))
					return FinishOutputFault(parent, "the final output is not prepared for placing");
				try { accepted = cell.AddObject(building); callbackReturned = true; building.MakeActive(); }
				catch { }
				finally { KingdomSurvey.ObserveAddResultInActive(Z, building, accepted); }
			}
			bool exactEndpoint = ExactFinalBuilding(building, Z, cell, entry, receipt, id, Rect,
				Footprint, Roof, architecture, legacyArchitecture, construction);
			bool exactCustody = ExactPlotFinalRootCustody(expectedOutput, building);
			if (!KingdomFoundingHeartTerminalRules.ExactAddCut(callbackReturned,
				object.ReferenceEquals(accepted, building), exactEndpoint, exactCustody)
				|| building.IDIfAssigned != expectedOutput
				|| (construction == null
					? parent.GetStringProperty(FinalOutputIdProperty) : construction.OutputId)
					!= expectedOutput
				|| !PlotPlanMarkerRemovalProofMatches(parent, building))
			{
				if (construction != null) KingdomConstruction.Quarantine(ref construction,
					"The exact final plot output changed across AddObject.");
				return false;
			}
			if (construction != null
				&& construction.PhysicalPhase == KingdomPhysicalPhase.FinalOutputPending
				&& !KingdomConstruction.UpdatePhysical(ref construction,
					KingdomPhysicalPhase.FinalOutputSettled, construction.PhysicalIndex,
					construction.PhysicalAmount,
					construction.PhysicalSpilled, parent.IDIfAssigned, building.ID,
					construction.PhysicalReceipt))
				return FinishOutputFault(parent, "the physical phase would not settle");
			if (building.CurrentCell != cell || building.IDIfAssigned != expectedOutput
				|| building.GetIntProperty("KingdomBuilt") != 1
				|| building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != entry.Key
				|| (!string.IsNullOrEmpty(receipt)
					&& building.GetStringProperty(KingdomConstruction.ReceiptProperty) != receipt)
				|| (construction != null && (!KingdomConstruction.IsCurrent(construction)
					|| !KingdomConstruction.PaidBuildMatches(building, construction))))
			{
				return FinishOutputFault(parent, "the placed final building is not exact");
			}
			Building = building;
			Created = created;
			return true;
		}

		/// <summary>
		/// Names a finish refusal. Every step here used to return false in silence, which is how a
		/// fully paid commission cycled ProjectionPending -> Working forever with nothing in the
		/// log to read (issue #172). Once per call, and TryFinishOutput returns at the first fault,
		/// so a stalled plot costs one line a pass.
		/// </summary>
		private static bool FinishOutputFault(GameObject Parent, string Step)
		{
			KingdomLog.Log("plot finish refused: " + Step + " (lot "
				+ (Parent == null ? "unknown" : Parent.GetStringProperty(PlotIdProperty)) + ")");
			return false;
		}
	}
}
