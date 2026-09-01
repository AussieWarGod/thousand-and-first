namespace ThousandAndFirst
{
	public static partial class KingdomArchitecturePreview
	{
		/// <summary>
		/// Re-proves the current semantic rule for the exact binding being previewed. The canonical
		/// snapshot already freezes that binding and the cardinal pose it selected; repeating a
		/// frontage byte in the receipt would create a second authority that could disagree.
		/// </summary>
		private static bool TryFrontage(ArchitectureLayoutSnapshot Snapshot,
			out string Frontage, out string Failure)
		{
			Frontage = null;
			Failure = null;
			KingdomArchitectureMapping mapping;
			if (Snapshot == null
				|| !KingdomArchitecture.TryGetMapping(Snapshot.BuildKey, Snapshot.LotType,
					Snapshot.LotSize, out mapping)
				|| mapping.PlanKey != Snapshot.PlanKey || mapping.BindingKey != Snapshot.BindingKey
				|| mapping.TierKey != Snapshot.TierKey)
			{
				Failure = "The building preview cannot re-prove its semantic frontage binding.";
				return false;
			}
			Frontage = mapping.Frontage == ArchitectureFrontage.Heart ? "heart-facing"
				: (mapping.Frontage == ArchitectureFrontage.Road ? "road-facing" : null);
			if (Frontage != null) return true;
			Failure = "The building preview has an unknown semantic frontage.";
			return false;
		}
	}
}
