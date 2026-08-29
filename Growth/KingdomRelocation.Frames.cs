using System;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool EnsureFrames(Zone Zone, KingdomRelocationReceipt Receipt,
			out GameObject Frame, out string Failure)
		{
			Frame = null; Failure = null;
			if (Zone == null || Receipt == null || Receipt.CurrentMove < 0
				|| Receipt.CurrentMove >= Receipt.Moves.Count)
			{
				Failure = "The current relocation frame has no move authority."; return false;
			}
			KingdomRelocationMove move = Receipt.Moves[Receipt.CurrentMove];
			int[,] points = { { move.Destination.CenterX, move.Destination.CenterY },
				{ move.Destination.X1, move.Destination.Y1 },
				{ move.Destination.X2, move.Destination.Y1 },
				{ move.Destination.X1, move.Destination.Y2 },
				{ move.Destination.X2, move.Destination.Y2 } };
			for (int i = 0; i < 5; i++)
			{
				string id = i == 0 ? move.FrameId : move.StakeIds[i - 1];
				string blueprint = i == 0 ? FrameBlueprint : StakeBlueprint;
				KingdomPhysicalLookupState found = KingdomConstruction.FindExactId(Zone, id,
					out GameObject exact);
				if (found == KingdomPhysicalLookupState.Ambiguous)
				{
					Failure = "Relocation frame identity " + id + " is duplicated."; return false;
				}
				Cell cell = Zone.GetCell(points[i, 0], points[i, 1]);
				if (cell == null) { Failure = "Relocation frame ground is absent."; return false; }
				if (found == KingdomPhysicalLookupState.Exact)
				{
					if (exact.Blueprint != blueprint || exact.CurrentCell != cell
						|| exact.GetStringProperty(FramePlanProperty) != Receipt.PlanId
						|| exact.GetIntProperty(FrameMoveProperty) != Receipt.CurrentMove
						|| exact.GetIntProperty(FrameKindProperty) != (i == 0 ? 1 : 2))
					{
						Failure = "Relocation frame identity points to divergent physical work.";
						return false;
					}
					if (i == 0) Frame = exact;
					continue;
				}
				GameObject placed = GameObject.Create(blueprint);
				if (!GameObject.Validate(placed)) { Failure = "Relocation frame blueprint is absent."; return false; }
				placed.ID = id; placed.SetStringProperty(FramePlanProperty, Receipt.PlanId);
				placed.SetIntProperty(FrameMoveProperty, Receipt.CurrentMove);
				placed.SetIntProperty(FrameKindProperty, i == 0 ? 1 : 2);
				GameObject accepted = null;
				try { accepted = cell.AddObject(placed, NoStack: true, Silent: true); }
				finally { KingdomSurvey.ObserveAddResultInActive(Zone, placed, accepted); }
				if (!ReferenceEquals(accepted, placed))
				{
					Failure = "The engine replaced an exact relocation frame."; return false;
				}
				if (i == 0) Frame = placed;
			}
			return GameObject.Validate(Frame);
		}

		private static void RemoveFrames(Zone Zone, KingdomRelocationMove Move)
		{
			if (Zone == null || Move == null) return;
			RemoveFrame(Zone, Move.FrameId);
			for (int i = 0; i < Move.StakeIds.Length; i++) RemoveFrame(Zone, Move.StakeIds[i]);
		}

		/// <summary>Idempotent post-publication cleanup after interruption between the
		/// authoritative move commit and removal of its temporary physical work.</summary>
		private static void CleanCompletedArtifacts(Zone Zone,
			KingdomRelocationReceipt Receipt)
		{
			if (Zone == null || Receipt?.Moves == null) return;
			int count = Receipt.CurrentMove < Receipt.Moves.Count
				? Receipt.CurrentMove : Receipt.Moves.Count;
			for (int i = 0; i < count; i++)
			{
				KingdomRelocationMove move = Receipt.Moves[i];
				if (move == null || move.Phase != KingdomRelocationMovePhase.Complete) continue;
				RemoveFrames(Zone, move); ReleaseClearanceEscrow(Receipt, move);
			}
			if (count > 0) Simulation.City.KingdomNetworks.MarkTopologyChanged();
		}

		private static void RemoveFrame(Zone Zone, string Id)
		{
			if (KingdomConstruction.FindExactId(Zone, Id, out GameObject item)
				!= KingdomPhysicalLookupState.Exact || !GameObject.Validate(item)) return;
			Cell cell = item.CurrentCell;
			try
			{
				if (cell != null) cell.RemoveObject(item, Forced: true, System: true,
					IgnoreGravity: true, Silent: true, NoStack: true);
			}
			finally { KingdomSurvey.ObserveRemovedFromActive(Zone, item); }
		}

		public static void FrameDestroyed(GameObject Frame)
		{
			Zone zone = Frame?.CurrentZone;
			if (zone != null && HasActive(zone))
				MessageQueue.AddPlayerMessage("{{K|A relocation frame fell. The ring call keeps its exact slate and will restake it.}}");
		}
	}
}
