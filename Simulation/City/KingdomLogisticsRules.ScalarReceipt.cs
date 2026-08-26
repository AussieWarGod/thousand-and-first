namespace ThousandAndFirst.Simulation.City
{
	internal enum KingdomScalarReceiptAction : byte
	{
		Refuse = 0,
		Apply = 1,
		AlreadyApplied = 2,
		ContinueFood = 3,
		Interference = 4
	}

	/// <summary>Pure recovery verdict for scalar target callbacks. Amount equality alone is never
	/// authority: the exact target must still carry this job's marker, and food additionally proves
	/// how many exact marked objects the callback created. Any unrelated target change cuts the
	/// transaction and quarantines instead of being mistaken for our receipt.</summary>
	internal static class KingdomScalarReceiptRules
	{
		internal static bool TryRecover(KingdomStockKind kind, long before, int amount,
			long observed, bool markerMatches, int markedFoodObjects,
			out KingdomScalarReceiptAction action)
		{
			action = KingdomScalarReceiptAction.Refuse;
			if ((kind != KingdomStockKind.Water && kind != KingdomStockKind.Food)
				|| before < 0L || amount <= 0 || observed < 0L || markedFoodObjects < 0
				|| markedFoodObjects > amount) return false;
			if (!markerMatches)
			{
				action = KingdomScalarReceiptAction.Interference;
				return true;
			}
			if (kind == KingdomStockKind.Water)
			{
				if (markedFoodObjects != 0)
				{
					action = KingdomScalarReceiptAction.Interference;
					return true;
				}
				action = observed == before ? KingdomScalarReceiptAction.Apply
					: (observed == before + amount ? KingdomScalarReceiptAction.AlreadyApplied
						: KingdomScalarReceiptAction.Interference);
				return true;
			}
			if (observed != before + markedFoodObjects)
			{
				action = KingdomScalarReceiptAction.Interference;
				return true;
			}
			action = markedFoodObjects == 0 ? KingdomScalarReceiptAction.Apply
				: (markedFoodObjects == amount ? KingdomScalarReceiptAction.AlreadyApplied
					: KingdomScalarReceiptAction.ContinueFood);
			return true;
		}
	}
}
