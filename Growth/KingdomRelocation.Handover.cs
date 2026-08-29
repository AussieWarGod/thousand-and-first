using System.Collections.Generic;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool TryHandOver(KingdomSystem System, Zone Zone, ref string Expected,
			KingdomRelocationReceipt Receipt, out string Failure)
		{
			Failure = null;
			KingdomRelocationMove move = Receipt.Moves[Receipt.CurrentMove];
			if (move.Phase != KingdomRelocationMovePhase.Handover)
			{ Failure = "The receiving frame is not complete."; return false; }
			if (!DestinationReady(Zone, Receipt, move, out string blocker))
			{
				Failure = blocker;
				if (!Receipt.ObstructionAnnounced)
				{
					Receipt.ObstructionAnnounced = true;
					if (TryPublish(Zone, Expected, Receipt, out Expected, out _))
						MessageQueue.AddPlayerMessage("{{K|The whole-lot handover waits: "
							+ blocker + " Nothing is displaced.}}");
				}
				return false;
			}
			if (Receipt.ObstructionAnnounced)
			{
				Receipt.ObstructionAnnounced = false;
				if (!TryPublish(Zone, Expected, Receipt, out Expected, out Failure)) return false;
			}
			for (int i = 0; i < move.Clearance.Count; i++)
				if (!MoveClearance(Zone, Receipt, move.Clearance[i], ref Expected,
					out Failure)) return false;
			for (int i = 0; i < move.Rows.Count; i++)
				if (!MovePlotRow(Zone, Receipt, move, move.Rows[i], ref Expected,
					out Failure)) return false;
			if (!FinalizeMove(Zone, move, out Failure))
			{
				RollbackAndQuarantine(Zone, Expected, Receipt, Failure); return false;
			}
			move.Phase = KingdomRelocationMovePhase.Complete;
			Receipt.CurrentMove++;
			if (Receipt.CurrentMove == Receipt.Moves.Count)
				Receipt.Phase = KingdomRelocationPhase.Complete;
			if (!TryPublish(Zone, Expected, Receipt, out Expected, out Failure)) return false;
			RemoveFrames(Zone, move); ReleaseClearanceEscrow(Receipt, move);
			Simulation.City.KingdomNetworks.MarkTopologyChanged();
			string name = move.DisplayName ?? move.BuildKey;
			System.Ledger.Note("{{G|The " + name + " crossed whole from "
				+ Corners(move.Source) + " to " + Corners(move.Destination)
				+ ". Its lot, contents, household, work, history, and wear are unchanged. "
				+ "Its declared networks rejoin from the new ground.}}");
			KingdomChronicle.Record(System, "the " + name + " yielded to the heart and was moved whole");
			if (Receipt.Phase == KingdomRelocationPhase.Complete)
			{
				System.Ledger.Note("{{W|Every promised lot has yielded. The exact ground is free, and the heart may climb.}}");
				return TryRetire(Zone, Expected, Receipt, out Failure);
			}
			return EnsureFrames(Zone, Receipt, out _, out Failure);
		}

		private static bool FinalizeMove(Zone Zone, KingdomRelocationMove Move,
			out string Failure)
		{
			Failure = null;
			KingdomRelocationRow rootRow = RootRow(Move);
			if (rootRow == null)
			{ Failure = "The exact behavior root row is absent."; return false; }
			if (KingdomConstruction.FindExactId(Zone, Move.RootId, out GameObject root)
				!= KingdomPhysicalLookupState.Exact || !ExactAt(root, Zone, root.Blueprint,
					Move.Destination.X1 + rootRow.OffsetX,
					Move.Destination.Y1 + rootRow.OffsetY))
			{ Failure = "The exact behavior root did not reach the destination."; return false; }
			int dx = Move.Destination.X1 - Move.Source.X1;
			int dy = Move.Destination.Y1 - Move.Source.Y1;
			KingdomPlots.StampRect(root, Runtime(Move.Destination));
			KingdomPlots.StampFootprint(root, Runtime(KingdomRelocationRules.Shift(
				Move.Footprint, dx, dy)), (KingdomPlotRules.RoofState)Move.Roof);
			if (Move.Architecture != null && !KingdomArchitectureRuntime.TryFreeze(root,
				ArchitectureIntent(Move, true), out Failure)) return false;
			for (int i = 0; i < Move.Rows.Count; i++)
			{
				KingdomRelocationRow row = Move.Rows[i];
				if (row.State != KingdomRelocationRowState.Destination
					|| KingdomConstruction.FindExactId(Zone, row.ObjectId, out GameObject exact)
						!= KingdomPhysicalLookupState.Exact
					|| !ExactAt(exact, Zone, row.Blueprint,
						Move.Destination.X1 + row.OffsetX,
						Move.Destination.Y1 + row.OffsetY)
					|| exact.GetStringProperty(KingdomPlots.PlotIdProperty) != Move.PlotId)
				{ Failure = "The destination does not contain the exact whole lot."; return false; }
			}
			KingdomSurvey.ObserveChangedInActive(Zone, root);
			return true;
		}

		private static KingdomRelocationRow RootRow(KingdomRelocationMove Move)
		{
			for (int i = 0; i < Move.Rows.Count; i++) if (Move.Rows[i].Root) return Move.Rows[i];
			return null;
		}
	}
}
