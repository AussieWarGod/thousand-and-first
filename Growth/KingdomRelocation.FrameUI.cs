using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		public static string FrameDescription(GameObject Frame)
		{
			if (!TryFrameMove(Frame, out KingdomRelocationReceipt receipt,
				out KingdomRelocationMove move)) return "Its ring-call slate is absent or divergent.";
			long completed = move.RequiredTicks - move.RemainingTicks;
			int done = move.RequiredTicks <= 0L ? 0 : (int)System.Math.Max(0.0,
				System.Math.Min(100.0, (double)completed * 100.0 / move.RequiredTicks));
			return (receipt.Held ? "The ring call is held. " : "The moving crew has the call. ")
				+ (move.DisplayName ?? move.BuildKey) + ": " + done + "% of the receiving frame; "
				+ Corners(move.Source) + " → " + Corners(move.Destination)
				+ ". No water or material is owed.";
		}

		public static void OpenFrame(GameObject Frame)
		{
			if (!TryFrameMove(Frame, out KingdomRelocationReceipt receipt,
				out KingdomRelocationMove move))
			{ Popup.Show("This moving frame has no exact active ring-call slate."); return; }
			Zone zone = Frame.CurrentZone;
			int picked = Popup.PickOption(Title: "The heart's ring call",
				Intro: Preview(receipt), Options: new List<string>
				{
					receipt.Held ? "{{W|Resume the moving crew}}" : "{{K|Hold the moving crew}}",
					"Leave the slate as it is"
				}, AllowEscape: true);
			if (picked != 0) return;
			if (!TryRead(zone, out receipt, out string expected, out string failure)
				|| receipt.CurrentMove != Frame.GetIntProperty(FrameMoveProperty))
			{ Popup.Show(failure ?? "The ring-call slate changed."); return; }
			receipt.Held = !receipt.Held;
			long now = The.Game == null ? move.LastTick : The.Game.TimeTicks;
			if (receipt.Moves[receipt.CurrentMove].LastTick < now)
				receipt.Moves[receipt.CurrentMove].LastTick = now;
			if (!TryPublish(zone, expected, receipt, out _, out failure)) Popup.Show(failure);
			else KingdomGovernanceScope.Commit(receipt.Held ? "hold heart relocation" : "resume heart relocation");
		}
	}
}
