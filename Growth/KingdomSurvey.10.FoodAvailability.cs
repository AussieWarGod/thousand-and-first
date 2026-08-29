namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		/// <summary>Food ordinary settlement work may spend now. Protected purpose cargo,
		/// construction leases, and open delivery receipts still occupy physical larder space but
		/// are excluded here. Failure of the durable lease authority is fail-closed.</summary>
		public int FoodAvailable
		{
			get
			{
				int available;
				string failure;
				return KingdomOrdinaryFoodAuthority.TryAvailable(this, out available, out failure)
					? available : 0;
			}
		}

		/// <summary>Pantry tier based on spendable food, never protected physical occupancy.</summary>
		public KingdomRules.PantryTier AvailableFoodAbundance =>
			KingdomRules.ClassifyPantry(FoodAvailable);

		internal void RefreshFoodTopology()
		{
			RefreshPhysicalFoodCount();
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			SynchronizeLarders();
		}
	}
}
