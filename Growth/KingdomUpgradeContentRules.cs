namespace ThousandAndFirst
{
	public enum KingdomHandoverManifestSlot : byte
	{
		Invalid = 0,
		Source = 1,
		Pending = 2,
		Destination = 3
	}

	/// <summary>Pure admission and placement rules for durable improvement contents.</summary>
	public static class KingdomUpgradeContentRules
	{
		public const int MaxManifestItems = 4096;

		public static bool ManifestCardinalityValid(int Count)
		{
			return Count >= 0 && Count <= MaxManifestItems;
		}

		public static KingdomHandoverManifestSlot ExpectedSlot(int Index, int Count,
			int Moved, int PendingIndex, int PendingPhase)
		{
			if (!ManifestCardinalityValid(Count) || Index < 0 || Index >= Count
				|| Moved < 0 || Moved > Count || PendingPhase < 0 || PendingPhase > 4)
				return KingdomHandoverManifestSlot.Invalid;
			if (PendingPhase == 0)
				return Index < Moved ? KingdomHandoverManifestSlot.Destination
					: KingdomHandoverManifestSlot.Source;
			if (PendingIndex < 0 || PendingIndex >= Count
				|| (Moved != PendingIndex && Moved != PendingIndex + 1))
				return KingdomHandoverManifestSlot.Invalid;
			if (Index == PendingIndex) return KingdomHandoverManifestSlot.Pending;
			return Index < PendingIndex ? KingdomHandoverManifestSlot.Destination
				: KingdomHandoverManifestSlot.Source;
		}

		public static bool LiquidEndpointSafe(int MaxVolume, bool HasContextCallback,
			bool HasBehaviourOverride)
		{
			return MaxVolume != -1 && !HasContextCallback && !HasBehaviourOverride;
		}
	}
}
