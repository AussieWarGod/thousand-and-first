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
				&& expectedOutput != parent.ID;
			if (hasFrozenFinal)
			{
				KingdomPhysicalLookupState outputState = KingdomConstruction.FindExactId(
					Z, expectedOutput, out building);
				if (outputState != KingdomPhysicalLookupState.Exact || building == null)
				{
					if (construction != null) KingdomConstruction.Quarantine(ref construction,
						"The exact frozen final plot output is absent or duplicated in its loaded owner zone.");
					return false;
				}
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
				return false;
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
			if (created)
			{
				if (construction != null)
				{
					if (!KingdomConstruction.UpdateFinalOutput(ref construction,
						parent.ID, building.ID))
					{
						RemoveCreatedWorks(building, Z);
						return false;
					}
				}
				else parent.SetStringProperty(FinalOutputIdProperty, building.ID);
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
				if (construction != null && !KingdomConstruction.UpdatePhysical(ref construction,
					KingdomPhysicalPhase.FinalOutputPending, construction.PhysicalIndex,
					construction.PhysicalAmount,
					construction.PhysicalSpilled, parent.ID, building.ID,
					construction.PhysicalReceipt))
				{
					RemoveCreatedWorks(building, Z);
					return false;
				}
				GameObject accepted;
				try
				{
					accepted = cell.AddObject(building);
					building.MakeActive();
					KingdomSurvey.ObserveAddResultInActive(Z, building, accepted);
				}
				catch (System.Exception ex)
				{
					bool cleaned = RemoveCreatedWorks(building, Z);
					if (construction != null) KingdomConstruction.Quarantine(ref construction,
						(cleaned ? "Final plot AddObject threw after identity publication: "
							: "Final plot AddObject threw and cleanup failed: ") + ex.Message);
					return false;
				}
				if (!ReferenceEquals(accepted, building))
				{
					if (construction != null) KingdomConstruction.Quarantine(ref construction,
						"Final plot AddObject replaced its exact return identity.");
					return false;
				}
			}
			if (!ExactFinalBuilding(building, Z, cell, entry, receipt, id, Rect,
				Footprint, Roof, architecture, legacyArchitecture, construction))
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
					construction.PhysicalSpilled, parent.ID, building.ID,
					construction.PhysicalReceipt)) return false;
			if (building.CurrentCell != cell || building.ID != (construction == null
					? parent.GetStringProperty(FinalOutputIdProperty) : construction.OutputId)
				|| building.GetIntProperty("KingdomBuilt") != 1
				|| building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != entry.Key
				|| (!string.IsNullOrEmpty(receipt)
					&& building.GetStringProperty(KingdomConstruction.ReceiptProperty) != receipt)
				|| (construction != null && (!KingdomConstruction.IsCurrent(construction)
					|| !KingdomConstruction.PaidBuildMatches(building, construction))))
			{
				if (created) RemoveCreatedWorks(building, Z);
				return false;
			}
			Building = building;
			Created = created;
			return true;
		}
	}
}
