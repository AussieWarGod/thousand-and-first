namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Pure route-mutation policy at the porter rendering boundary.</summary>
	internal static class KingdomPorterRouteRules
	{
		/// <summary>Construction input is bound to its parent's frozen route evidence. Existing
		/// scalar and CarryBook deliveries retain the original ground-wins reprojection rule.</summary>
		internal static bool ReprojectsOnMove(KingdomDeliveryCargoAuthority Authority)
		{
			return Authority != KingdomDeliveryCargoAuthority.ConstructionInput;
		}
	}
}
