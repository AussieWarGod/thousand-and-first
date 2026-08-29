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
		private static void SettlePriorProjection(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			if (string.IsNullOrEmpty(Operation.PriorProjectionId))
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				return;
			}
			GameObject old;
			LoadedTopologyWitness oldTopology;
			LoadedObjectResolution oldResolution = ResolveLoadedObject(
				Operation.PriorProjectionObjectId, Z, out old, out oldTopology);
			if (Operation.PriorCleanupState == KingdomTradePhysicalState.Intent
				|| Operation.PriorCleanupState == KingdomTradePhysicalState.CleanupIntent)
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"Old projection cleanup resumed without its live list witness and was not repeated.");
				return;
			}
			if (oldResolution == LoadedObjectResolution.Missing)
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				return;
			}
			if (oldResolution != LoadedObjectResolution.ExactUnique)
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection object identity was ambiguous or topology-incomplete.");
				return;
			}
			if (!GameObject.Validate(old) || old.CurrentZone != Z
				|| !string.Equals(old.GetStringProperty(ProjectionProperty),
					Operation.PriorProjectionId, StringComparison.Ordinal))
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection exact object did not match its persisted marker.");
				return;
			}
			if (CountProjection(Z, Operation.PriorProjectionId) != 1)
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection identity was not unique on active settlement ground.");
				return;
			}
			CellWitness oldCell;
			if (!TryCaptureCell(old.CurrentCell, Z, out oldCell)
				|| !ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Z))
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection cleanup lost its exact live frame.");
				return;
			}
			Operation.PriorCleanupState = KingdomTradePhysicalState.CleanupIntent;
			CallbackWitness callback = CaptureCallbackWitness(Frame);
			if (callback == null || oldTopology == null || !ExactLoadedTopology(oldTopology))
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection cleanup frame could not be frozen.");
				return;
			}
			try
			{
				old.Obliterate();
			}
			finally
			{
				BoundTradeSurvey(Z)?.ObserveCurrentTopology(old);
			}
			if (!ExactCallbackWitness(Frame, callback)
				|| !ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactLoadedTopologyWithDelta(oldTopology, null, old, null, true))
			{
				FailDetachedAuthority(Frame,
					"An old-projection cleanup callback detached official trade authority.");
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				return;
			}
			RefreshPhysicalTopologies(Frame.Physical);
			Operation.PriorCleanupState = !GameObject.Validate(old)
				&& ExactCellAfterRemoval(oldCell, old, Z)
				&& CountProjection(Z, Operation.PriorProjectionId) == 0
				&& ExactPhysicalFrame(Frame, Operation, Z)
				? KingdomTradePhysicalState.Proved : KingdomTradePhysicalState.Lost;
			if (Operation.PriorCleanupState == KingdomTradePhysicalState.Lost)
				Quarantine(Operation,
					"Old caravan destruction was vetoed or changed topology; it was not attempted twice.");
		}

		private static bool TryBindProjectionFrame(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z)
		{
			if (Operation == null || Operation.Kind != KingdomTradeOperationKind.CharterDelivery
				|| Operation.ProjectionState != KingdomTradePhysicalState.Proved) return true;
			if (Frame.ProjectionObject != null || Frame.ProjectionCell != null)
				return ExactProjectionWitness(Frame, Operation, Z);
			GameObject body;
			LoadedTopologyWitness topology;
			if (ResolveLoadedObject(Operation.ProjectionObjectId, Z, out body, out topology)
				!= LoadedObjectResolution.ExactUnique) return false;
			CellWitness cell;
			if (!GameObject.Validate(body) || !TryCaptureCell(body.CurrentCell, Z, out cell))
				return false;
			Frame.ProjectionObject = body;
			Frame.ProjectionCell = cell;
			return ExactLoadedTopology(topology) && ExactProjectionWitness(Frame, Operation, Z);
		}

		private static bool ExactProjectionWitness(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z)
		{
			return Frame != null && ExactCell(Frame.ProjectionCell, Z)
				&& ExactProjection(Frame.ProjectionObject, Frame.ProjectionCell.Cell,
					Operation, Z)
				&& CountProjection(Z, Operation.ProjectionId) == 1;
		}

		private static bool ExactProjection(GameObject Body, Cell Cell,
			KingdomTradeOperation Operation, Zone Z)
		{
			return GameObject.Validate(Body) && Cell != null && Cell.ParentZone == Z
				&& Body.CurrentZone == Z && Body.CurrentCell == Cell
				&& Cell.X == Operation.ProjectionX && Cell.Y == Operation.ProjectionY
				&& string.Equals(Body.IDIfAssigned, Operation.ProjectionObjectId,
					StringComparison.Ordinal)
				&& string.Equals(Body.Blueprint, Operation.CaravanBlueprint,
					StringComparison.Ordinal)
				&& string.Equals(Body.GetStringProperty(ProjectionProperty),
					Operation.ProjectionId, StringComparison.Ordinal)
				&& Body.GetIntProperty("KingdomCaravan") == 1;
		}

		private static bool TryCaptureCell(Cell Cell, Zone Z, out CellWitness Witness)
		{
			Witness = null;
			if (Cell == null || Cell.ParentZone != Z || Cell.Objects == null) return false;
			Witness = new CellWitness
			{
				Cell = Cell, Objects = Cell.Objects, Rows = Cell.Objects.ToArray()
			};
			return ExactCell(Witness, Z);
		}

		private static bool ExactCell(CellWitness Witness, Zone Z)
		{
			if (Witness == null || Witness.Cell == null || Witness.Cell.ParentZone != Z
				|| !ReferenceEquals(Witness.Cell.Objects, Witness.Objects)
				|| Witness.Objects == null || Witness.Rows == null
				|| Witness.Objects.Count != Witness.Rows.Length) return false;
			for (int i = 0; i < Witness.Rows.Length; i++)
				if (!ReferenceEquals(Witness.Objects[i], Witness.Rows[i])) return false;
			return true;
		}

		private static bool ExactCellAfterAppend(CellWitness Witness, GameObject Added, Zone Z)
		{
			if (Witness == null || Witness.Cell == null || Witness.Cell.ParentZone != Z
				|| !ReferenceEquals(Witness.Cell.Objects, Witness.Objects)
				|| Witness.Objects == null || Witness.Rows == null
				|| Witness.Objects.Count != Witness.Rows.Length + 1) return false;
			for (int i = 0; i < Witness.Rows.Length; i++)
				if (!ReferenceEquals(Witness.Objects[i], Witness.Rows[i])) return false;
			return ReferenceEquals(Witness.Objects[Witness.Rows.Length], Added);
		}

		private static bool ExactCellAfterRemoval(CellWitness Witness, GameObject Removed, Zone Z)
		{
			if (Witness == null || Witness.Cell == null || Witness.Cell.ParentZone != Z
				|| !ReferenceEquals(Witness.Cell.Objects, Witness.Objects)
				|| Witness.Objects == null || Witness.Rows == null
				|| Witness.Objects.Count != Witness.Rows.Length - 1) return false;
			int at = 0;
			bool found = false;
			for (int i = 0; i < Witness.Rows.Length; i++)
			{
				if (ReferenceEquals(Witness.Rows[i], Removed))
				{
					if (found) return false;
					found = true;
					continue;
				}
				if (at >= Witness.Objects.Count
					|| !ReferenceEquals(Witness.Objects[at++], Witness.Rows[i])) return false;
			}
			return found && at == Witness.Objects.Count;
		}

		private static int CountProjection(Zone Z, string ProjectionId)
		{
			if (Z == null || string.IsNullOrEmpty(ProjectionId)) return 0;
			KingdomSurvey survey = BoundTradeSurvey(Z);
			IList<GameObject> objects;
			if (survey == null || !survey.TryLoaded(out objects) || objects == null)
				return int.MaxValue;
			int count = 0;
			for (int i = 0; i < objects.Count; i++)
				if (GameObject.Validate(objects[i]) && string.Equals(
					objects[i].GetStringProperty(ProjectionProperty), ProjectionId,
					StringComparison.Ordinal)) count++;
			return count;
		}

	}
}
