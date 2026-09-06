namespace ThousandAndFirst
{
	internal static partial class KingdomArchivedSettlementCodec
	{
		private static bool HistoricalPhysicalFirstGuestOpportunity(
			KingdomGrowthFirstGuestOpportunity Value, int SchemaVersion)
		{
			return SchemaVersion >= PhysicalFirstGuestVersion || Value == null
				|| Value.RulesVersion == 1
					&& Value.GuestPhase == KingdomGrowthFirstGuestGuestPhase.None
					&& Value.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.None
					&& Value.GuestActionTick == -1L && Value.GuestActionReceiptId == null
					&& Value.GuestTerminalTick == -1L && Value.GuestTerminalReceiptId == null;
		}

		/// <summary>Versions v9-v12 retain the historical delivery enum domains.</summary>
		private static bool ValidDeliveryDomain(
			Simulation.City.KingdomJobRegistry Value, int SchemaVersion)
		{
			if (Value == null || Value.JobIds == null || Value.DeliveryPhases == null
				|| Value.DeliveryCargoAuthorityKinds == null
				|| Value.DeliveryPhases.Count != Value.JobIds.Count
				|| Value.DeliveryCargoAuthorityKinds.Count != Value.JobIds.Count) return false;
			int maximumAuthority = SchemaVersion < DeliveryDomainVersion
				? (int)Simulation.City.KingdomDeliveryCargoAuthority.CarryBookManifest
				: (int)Simulation.City.KingdomDeliveryCargoAuthority.ConstructionInput;
			int maximumPhase = SchemaVersion < DeliveryDomainVersion
				? (int)Simulation.City.KingdomDeliveryPhase.Quarantined
				: (int)Simulation.City.KingdomDeliveryPhase.LandedAwaitingOwner;
			for (int i = 0; i < Value.JobIds.Count; i++)
				if (Value.DeliveryCargoAuthorityKinds[i] < 0
					|| Value.DeliveryCargoAuthorityKinds[i] > maximumAuthority
					|| Value.DeliveryPhases[i] < 0
					|| Value.DeliveryPhases[i] > maximumPhase) return false;
			return true;
		}

		private static bool ValidCivicAuthority(Simulation.City.KingdomCityBook Value)
		{
			return Value != null && Value.NamedCook != null && Value.AssentingMoot != null
				&& KingdomNamedCookRules.Validate(Value.NamedCook, out string _)
				&& KingdomAssentingMootRules.Validate(Value.AssentingMoot, out string _);
		}

#if TAF_TESTS
		internal static bool ValidDeliveryDomainForTests(
			Simulation.City.KingdomJobRegistry Value, int SchemaVersion)
		{
			return ValidDeliveryDomain(Value, SchemaVersion);
		}
#endif
	}
}
