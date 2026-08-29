using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static void RollbackAndQuarantine(Zone Zone, string Expected,
			KingdomRelocationReceipt Receipt, string Reason)
		{
			if (Receipt == null || Receipt.CurrentMove < 0
				|| Receipt.CurrentMove >= Receipt.Moves.Count)
			{ Quarantine(Zone, Expected, Receipt, Reason); return; }
			KingdomRelocationMove move = Receipt.Moves[Receipt.CurrentMove];
			if (move.Phase == KingdomRelocationMovePhase.RolledBack)
			{ Quarantine(Zone, Expected, Receipt, Receipt.Failure ?? Reason); return; }
			string failure = null;
			if (move.Phase != KingdomRelocationMovePhase.RollingBack)
			{
				Receipt.Failure = Bounded(Reason);
				move.Phase = KingdomRelocationMovePhase.RollingBack;
				if (!TryPublish(Zone, Expected, Receipt, out Expected, out failure)) return;
			}
			bool restored = true;
			for (int i = 0; i < move.Rows.Count; i++)
				if (!RestorePlotRow(Zone, Receipt, move, move.Rows[i], ref Expected,
					out failure)) { restored = false; break; }
			if (restored)
				for (int i = 0; i < move.Clearance.Count; i++)
					if (!RestoreClearance(Zone, Receipt, move, move.Clearance[i], ref Expected,
						out failure)) { restored = false; break; }
			if (restored && KingdomConstruction.FindExactId(Zone, move.RootId,
				out GameObject root) == KingdomPhysicalLookupState.Exact)
			{
				KingdomPlots.StampRect(root, Runtime(move.Source));
				KingdomPlots.StampFootprint(root, Runtime(move.Footprint),
					(KingdomPlotRules.RoofState)move.Roof);
				if (move.Architecture != null && !KingdomArchitectureRuntime.TryFreeze(root,
					ArchitectureIntent(move, false), out failure)) restored = false;
				KingdomSurvey.ObserveChangedInActive(Zone, root);
			}
			else if (restored) { failure = "The behavior root could not be restored."; restored = false; }
			if (restored)
			{
				move.Phase = KingdomRelocationMovePhase.RolledBack;
				if (TryPublish(Zone, Expected, Receipt, out Expected, out _)) RemoveFrames(Zone, move);
			}
			Quarantine(Zone, Expected, Receipt, restored ? Reason
				: Reason + " Rollback also stopped: " + (failure ?? "unknown divergence"));
		}

		private static bool RestorePlotRow(Zone Zone, KingdomRelocationReceipt Receipt,
			KingdomRelocationMove Move, KingdomRelocationRow Row, ref string Expected,
			out string Failure)
		{
			Failure = null;
			if (Row.State == KingdomRelocationRowState.Source) return true;
			GameObject item = Escrow(Receipt, Row.ObjectId, false);
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Zone,
				Row.ObjectId, out GameObject physical);
			if (state == KingdomPhysicalLookupState.Ambiguous
				|| (item != null && physical != null && !ReferenceEquals(item, physical)))
			{ Failure = "Rollback found a duplicated lot identity."; return false; }
			bool rooted = item != null;
			if (item == null) item = physical;
			if (!GameObject.Validate(item) || item.Blueprint != Row.Blueprint)
			{ Failure = "Rollback lost a lot object."; return false; }
			bool atSource = state == KingdomPhysicalLookupState.Exact
				&& ExactAt(physical, Zone, Row.Blueprint,
				Move.Source.X1 + Row.OffsetX, Move.Source.Y1 + Row.OffsetY);
			bool atDestination = state == KingdomPhysicalLookupState.Exact
				&& ExactAt(physical, Zone, Row.Blueprint,
				Move.Destination.X1 + Row.OffsetX, Move.Destination.Y1 + Row.OffsetY);
			if (state == KingdomPhysicalLookupState.Exact && !atSource && !atDestination)
			{ Failure = "Rollback found a lot object outside both receipt cells."; return false; }
			if (state == KingdomPhysicalLookupState.Absent && !rooted)
			{ Failure = "Rollback lost a rooted lot object."; return false; }
			if (state == KingdomPhysicalLookupState.Absent && item.CurrentCell != null)
			{ Failure = "Rollback escrow points to a lot object on foreign ground."; return false; }
			if (atSource) { Row.State = KingdomRelocationRowState.Source; }
			else
			{
				Cell cell = Zone.GetCell(Move.Source.X1 + Row.OffsetX,
					Move.Source.Y1 + Row.OffsetY);
				if (!RestoreCellReady(cell, Move, out Failure)) return false;
				if (!RootEscrow(Receipt, item, false, out Failure)) return false;
				if (atDestination && !RemoveForHandover(Zone, item, out Failure)) return false;
				GameObject accepted = null;
				try { accepted = cell?.AddObject(item, NoStack: true, Silent: true); }
				catch (System.Exception exception) { Failure = exception.Message; return false; }
				KingdomSurvey.ObserveAddResultInActive(Zone, item, accepted);
				if (!ReferenceEquals(accepted, item)) { Failure = "Rollback placement was replaced."; return false; }
				Row.State = KingdomRelocationRowState.Source;
			}
			if (!TryPublish(Zone, Expected, Receipt, out Expected, out Failure)) return false;
			if (!ClearEscrow(Receipt, Row.ObjectId, false, item))
			{ Failure = "Rollback lot escrow would not retire."; return false; }
			return true;
		}

		private static bool RestoreClearance(Zone Zone, KingdomRelocationReceipt Receipt,
			KingdomRelocationMove Move, KingdomRelocationClearRow Row, ref string Expected,
			out string Failure)
		{
			Failure = null;
			if (Row.State == KingdomRelocationClearState.Standing) return true;
			GameObject item = Escrow(Receipt, Row.ObjectId, true);
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Zone,
				Row.ObjectId, out GameObject physical);
			if (state == KingdomPhysicalLookupState.Ambiguous
				|| (item != null && physical != null && !ReferenceEquals(item, physical)))
			{ Failure = "Rollback found duplicated frozen natural ground."; return false; }
			bool standing = state == KingdomPhysicalLookupState.Exact
				&& ExactAt(physical, Zone, Row.Blueprint, Row.X, Row.Y);
			if (state == KingdomPhysicalLookupState.Exact && !standing)
			{ Failure = "Frozen natural ground moved outside its receipt cell."; return false; }
			if (item == null) item = physical;
			if (!GameObject.Validate(item) || item.Blueprint != Row.Blueprint)
			{ Failure = "Rollback lost frozen natural ground."; return false; }
			if (state == KingdomPhysicalLookupState.Absent && item.CurrentCell != null)
			{ Failure = "Natural rollback escrow points to foreign ground."; return false; }
			if (!standing)
			{
				Cell cell = Zone.GetCell(Row.X, Row.Y);
				if (!RestoreCellReady(cell, Move, out Failure)) return false;
				GameObject accepted = cell?.AddObject(item,
					NoStack: true, Silent: true);
				KingdomSurvey.ObserveAddResultInActive(Zone, item, accepted);
				if (!ReferenceEquals(accepted, item)) { Failure = "Natural rollback was replaced."; return false; }
			}
			Row.State = KingdomRelocationClearState.Standing;
			if (!TryPublish(Zone, Expected, Receipt, out Expected, out Failure)) return false;
			if (!ClearEscrow(Receipt, Row.ObjectId, true, item))
			{ Failure = "Natural rollback escrow would not retire."; return false; }
			return true;
		}

		private static bool RestoreCellReady(Cell Cell, KingdomRelocationMove Move,
			out string Failure)
		{
			Failure = null;
			if (Cell == null) { Failure = "Rollback ground is absent."; return false; }
			foreach (GameObject other in Cell.GetObjects())
			{
				if (!GameObject.Validate(other)) continue;
				string otherId = other.IDIfAssigned;
				if (!string.IsNullOrEmpty(otherId) && Move != null
					&& Move.Rows.Exists(r => r.ObjectId == otherId)) continue;
				if (!string.IsNullOrEmpty(otherId) && Move != null
					&& Move.Clearance.Exists(r => r.ObjectId == otherId)) continue;
				if (!string.IsNullOrEmpty(otherId) && Move != null && (otherId == Move.FrameId
					|| System.Array.IndexOf(Move.StakeIds, otherId) >= 0)) continue;
				if (KingdomPlots.ReadObject(other) == KingdomPlotRules.GroundKind.Bare) continue;
				Failure = (other.IsPlayer() ? "The founder" : other.ShortDisplayNameStripped)
					+ " now protects rollback ground.";
				return false;
			}
			return true;
		}
	}
}
