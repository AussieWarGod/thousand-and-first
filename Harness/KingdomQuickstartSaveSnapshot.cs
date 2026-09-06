namespace ThousandAndFirst.Harness
{
	// External save witness, not authority to run or repair Quickstart.
	internal sealed class KingdomQuickstartSaveSnapshot
	{
		internal readonly string GameId, Seed, ProfileKey;
		internal readonly bool Advisor;
		internal readonly int FounderBaseId;
		internal readonly string FounderId;
		internal readonly long Turns, TimeTicks, ActionTicks, PlayerActionTicks;
		internal readonly string ReceiptWire, HeartReceipt, HeartSeal, HeartTerminal, ReservationsWire;

		internal KingdomQuickstartSaveSnapshot(string gameId, string seed, string profileKey,
			bool advisor, int founderBaseId, string founderId, long turns, long timeTicks,
			long actionTicks, long playerActionTicks, string receiptWire, string heartReceipt,
			string heartSeal, string heartTerminal, string reservationsWire)
		{
			GameId = gameId; Seed = seed; ProfileKey = profileKey; Advisor = advisor;
			FounderBaseId = founderBaseId; FounderId = founderId;
			Turns = turns; TimeTicks = timeTicks; ActionTicks = actionTicks;
			PlayerActionTicks = playerActionTicks; ReceiptWire = receiptWire;
			HeartReceipt = heartReceipt; HeartSeal = heartSeal;
			HeartTerminal = heartTerminal; ReservationsWire = reservationsWire;
		}
	}
}
