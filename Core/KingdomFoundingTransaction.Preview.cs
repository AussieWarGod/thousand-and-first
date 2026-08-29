namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		/// <summary>Exact covenant delta shown before commit and copied by civic voices.</summary>
		public static string VillageCharterPreview(string VillageDisplayName,
			int CurrentStanding)
		{
			string village = string.IsNullOrEmpty(VillageDisplayName) ? "the village"
				: ("{{C|" + VillageDisplayName + "}}");
			int after = CurrentStanding < KingdomRules.VillageCharterSealedStanding
				? KingdomRules.VillageCharterSealedStanding : CurrentStanding;
			return "Seal a covenant with " + village + "?\n\nFacts: exactly "
				+ KingdomRules.FoundingCostDrams + " drams of fresh water are spent. Realm standing"
				+ " changes from " + CurrentStanding + " to " + after
				+ ". Their ground, faction, and citizenship remain theirs; no city is founded.";
		}
	}
}
