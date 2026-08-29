namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		/// <summary>Moves proved-but-undelivered consignment water into durable Trade custody.</summary>
		private static bool SettlePolityConsignmentRetention(KingdomTradeBook Book,
			KingdomTradeOperation Operation)
		{
			if (Book == null || Operation == null || Operation.ProvedWater < 0) return false;
			if (Operation.ProvedWater == 0)
				return Operation.RetainedBefore == 0L && Operation.RetainedDelta == 0L &&
					Operation.RetainedAfter == 0L &&
					Operation.RetainedState == KingdomTradePhysicalState.None;
			if (Operation.RetainedState == KingdomTradePhysicalState.None)
			{
				if (Book.RetainedEscrowDrams < 0L || Operation.ProvedWater >
					long.MaxValue - Book.RetainedEscrowDrams) return false;
				Operation.RetainedBefore = Book.RetainedEscrowDrams;
				Operation.RetainedDelta = Operation.ProvedWater;
				Operation.RetainedAfter = Operation.RetainedBefore + Operation.RetainedDelta;
				Operation.RetainedState = KingdomTradePhysicalState.Prepared;
			}
			return SettleRetainedAccounting(Book, Operation);
		}
	}
}
