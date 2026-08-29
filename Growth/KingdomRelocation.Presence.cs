using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		internal static bool FrameNeedsLabour(GameObject Frame)
		{
			KingdomSystem system = XRL.The.Game?.GetSystem<KingdomSystem>();
			return KingdomUpgrade.Enabled && KingdomMaster.AutomaticWorkAllowed(system)
				&& TryFrameMove(Frame, out KingdomRelocationReceipt receipt,
				out KingdomRelocationMove move) && !receipt.Held
				&& receipt.Phase == KingdomRelocationPhase.Active
				&& (move.Phase == KingdomRelocationMovePhase.Waiting
					|| move.Phase == KingdomRelocationMovePhase.Working)
				&& move.RemainingTicks > 0L;
		}

		internal static long FrameStarted(GameObject Frame)
		{
			return TryFrameMove(Frame, out _, out KingdomRelocationMove move)
				? move.StartedTick : long.MaxValue;
		}

		internal static string FrameDisplay(GameObject Frame)
		{
			return TryFrameMove(Frame, out _, out KingdomRelocationMove move)
				? "moving frame for " + (move.DisplayName ?? move.BuildKey)
				: "quarantined moving frame";
		}

		private static bool TryFrameMove(GameObject Frame,
			out KingdomRelocationReceipt Receipt, out KingdomRelocationMove Move)
		{
			Receipt = null; Move = null;
			Zone zone = Frame?.CurrentZone;
			if (zone == null || Frame.GetIntProperty(FrameKindProperty) != 1
				|| !TryRead(zone, out Receipt, out _, out _)
				|| Receipt.PlanId != Frame.GetStringProperty(FramePlanProperty)
				|| Receipt.CurrentMove != Frame.GetIntProperty(FrameMoveProperty)
				|| Receipt.CurrentMove < 0 || Receipt.CurrentMove >= Receipt.Moves.Count) return false;
			Move = Receipt.Moves[Receipt.CurrentMove];
			return Move.FrameId == Frame.IDIfAssigned;
		}
	}
}
