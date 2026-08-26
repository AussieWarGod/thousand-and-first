using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		private static void SettleProjection(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			if (Operation.Kind != KingdomTradeOperationKind.CharterDelivery)
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Skipped;
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
				return;
			}
			if (!string.IsNullOrEmpty(Operation.PriorProjectionId)
				&& !string.Equals(Operation.PriorProjectionZoneId, Z?.ZoneID,
					StringComparison.Ordinal))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Skipped;
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				Operation.Fault = AppendFault(Operation.Fault,
					"This city's existing caravan is bound to another loaded zone; no second projection was created.");
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
				return;
			}
			if (Operation.Phase == KingdomTradePhase.ProjectionIntent)
			{
				ReconcileProjection(Operation, Z, Frame);
				return;
			}
			Cell cell;
			if (!TryChooseProjectionCell(Z, out cell)
				|| string.IsNullOrEmpty(Operation.CaravanBlueprint))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Skipped;
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				Operation.Fault = AppendFault(Operation.Fault,
					"The caravan projection had no exact cell; delivery authority was not replayed.");
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
				return;
			}
			Operation.ProjectionState = KingdomTradePhysicalState.CreateIntent;
			Operation.Phase = KingdomTradePhase.ProjectionIntent;
			if (!ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Z))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"The caravan frame changed before its creation callback.");
				return;
			}
			CallbackWitness callback = CaptureCallbackWitness(Frame);
			LoadedTopologyWitness createTopology = CaptureLoadedTopology();
			if (callback == null || createTopology == null
				|| !ExactLoadedTopology(createTopology))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Caravan creation frame could not be frozen.");
				return;
			}
			GameObject caravan = GameObject.Create(Operation.CaravanBlueprint);
			if (!ExactCallbackWitness(Frame, callback)
				|| !ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactLoadedTopology(createTopology))
			{
				FailDetachedAuthority(Frame,
					"A caravan creation callback detached its official trade authority.");
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				return;
			}
			if (Operation.ProjectionState != KingdomTradePhysicalState.CreateIntent
				|| !ExactPhysicalFrame(Frame, Operation, Z)
				|| !GameObject.Validate(caravan) || string.IsNullOrEmpty(caravan.ID)
				|| !string.Equals(caravan.Blueprint, Operation.CaravanBlueprint,
					StringComparison.Ordinal))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				Quarantine(Operation,
					"The frozen caravan blueprint did not create an exact projection.");
				return;
			}
			CellWitness cellWitness;
			if (!TryCaptureCell(cell, Z, out cellWitness)
				|| cellWitness.Rows.Length != 0)
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"The chosen caravan cell changed before placement.");
				return;
			}
			Operation.ProjectionObjectId = caravan.ID;
			Operation.ProjectionX = cell.X;
			Operation.ProjectionY = cell.Y;
			caravan.SetStringProperty(ProjectionProperty, Operation.ProjectionId);
			caravan.SetIntProperty("KingdomCaravan", 1);
			if (caravan.Brain != null) caravan.Brain.Allegiance.Calm = true;
			Operation.ProjectionState = KingdomTradePhysicalState.Intent;
			if (!ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Z)
				|| !ExactCell(cellWitness, Z))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "The caravan frame changed before AddObject.");
				return;
			}
			callback = CaptureCallbackWitness(Frame);
			LoadedTopologyWitness addTopology = CaptureLoadedTopology();
			if (callback == null || addTopology == null
				|| !ExactLoadedTopology(addTopology))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Caravan AddObject frame could not be frozen.");
				return;
			}
			GameObject added = null;
			try
			{
				added = cell.AddObject(caravan);
			}
			finally
			{
				KingdomSurvey.ObserveAddResultInActive(Z, caravan, added);
			}
			if (!ExactCallbackWitness(Frame, callback)
				|| !ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactLoadedTopologyWithDelta(addTopology, caravan, null, null, true))
			{
				FailDetachedAuthority(Frame,
					"A caravan AddObject callback detached its official trade authority.");
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				return;
			}
			RefreshPhysicalTopologies(Frame.Physical);
			if (!ReferenceEquals(added, caravan)
				|| Operation.ProjectionState != KingdomTradePhysicalState.Intent
				|| !ExactPhysicalFrame(Frame, Operation, Z)
				|| !ExactCellAfterAppend(cellWitness, caravan, Z)
				|| !ExactProjection(caravan, cell, Operation, Z)
				|| CountProjection(Z, Operation.ProjectionId) != 1)
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"The caravan AddObject callback did not leave one exact projection.");
				return;
			}
			cellWitness.Rows = AppendRow(cellWitness.Rows, caravan);
			Frame.ProjectionObject = caravan;
			Frame.ProjectionCell = cellWitness;
			Operation.ProjectionState = KingdomTradePhysicalState.Proved;
			SettlePriorProjection(Operation, Z, Frame);
			if (Operation.Phase != KingdomTradePhase.Quarantined)
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
		}

		/// <summary>Finds one exact object-rack-empty caravan berth without allocating or
		/// traversing Qud's full empty-cell list. Boundary cells retain first priority; a bounded
		/// row-major interior probe is deterministic across save/resume and fails closed when the
		/// settlement is too crowded.</summary>
		private static bool TryChooseProjectionCell(Zone Z, out Cell Cell)
		{
			Cell = null;
			if (Z == null || Z.Width <= 0 || Z.Height <= 0) return false;
			int probes = 0;
			for (int y = 0; y < Z.Height && probes < MaxProjectionCellProbes; y++)
			{
				for (int x = 0; x < Z.Width && probes < MaxProjectionCellProbes; x++)
				{
					if (x != 0 && x != Z.Width - 1 && y != 0 && y != Z.Height - 1)
						continue;
					probes++;
					Cell candidate = Z.GetCell(x, y);
					if (ExactEmptyProjectionCell(candidate, Z))
					{
						Cell = candidate;
						return true;
					}
				}
			}
			for (int y = 1; y < Z.Height - 1 && probes < MaxProjectionCellProbes; y++)
			{
				for (int x = 1; x < Z.Width - 1 && probes < MaxProjectionCellProbes; x++)
				{
					probes++;
					Cell candidate = Z.GetCell(x, y);
					if (ExactEmptyProjectionCell(candidate, Z))
					{
						Cell = candidate;
						return true;
					}
				}
			}
			return false;
		}

		private static bool ExactEmptyProjectionCell(Cell Cell, Zone Z)
		{
			return Cell != null && ReferenceEquals(Cell.ParentZone, Z)
				&& Cell.Objects != null && Cell.Objects.Count == 0 && Cell.IsEmpty();
		}

		private static void ReconcileProjection(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			if (Operation.ProjectionState == KingdomTradePhysicalState.Intent
				|| Operation.ProjectionState == KingdomTradePhysicalState.CreateIntent)
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"A reloaded caravan Create/Add intent lacked its live list witness and was not replayed.");
				return;
			}
			SettlePriorProjection(Operation, Z, Frame);
			if (Operation.Phase != KingdomTradePhase.Quarantined)
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
		}

	}
}
