namespace ThousandAndFirst
{
	public enum KingdomPolitySchemaState : byte
	{
		Compatible = 0,
		Unknown = 1,
		Quarantined = 2
	}

	public enum KingdomPolitySource : byte
	{
		None = 0,
		CurrentRealm = 1,
		ImportedLegacy = 2,
		AuthoredRival = 3,
		VanillaCounterparty = 4
	}

	public enum KingdomPolityLifecycle : byte
	{
		Latent = 0,
		Active = 1,
		Dormant = 2,
		Ended = 3
	}

	public enum KingdomPolityRelationBand : byte
	{
		Unspecified = 0,
		Contact = 1,
		Neutral = 2,
		Pact = 3,
		Rival = 4,
		Hostile = 5,
		Truce = 6
	}

	public enum KingdomPolityRoutePurpose : byte
	{
		None = 0,
		Trade = 1,
		Delegation = 2,
		Patrol = 3,
		Migration = 4,
		Courier = 5
	}

	public enum KingdomPolityRouteMode : byte
	{
		None = 0,
		Foot = 1,
		Caravan = 2,
		Water = 3,
		Mechanical = 4
	}

	public enum KingdomPolityRoutePhase : byte
	{
		Preparing = 0,
		Traveling = 1,
		AvailableToWitness = 2,
		Blocked = 3,
		ConfrontationAvailable = 4,
		Arrived = 5,
		Returned = 6,
		Cancelled = 7
	}

	public enum KingdomPolityGrievanceCause : byte
	{
		None = 0,
		Claim = 1,
		BrokenPact = 2,
		RouteObstruction = 3,
		WitnessedHarm = 4,
		Trespass = 5,
		DesignatedTheft = 6,
		RefusedTerms = 7,
		ResourceRefusal = 8
	}

	public enum KingdomPolityGrievancePhase : byte
	{
		Open = 0,
		Consumed = 1,
		Resolved = 2,
		Withdrawn = 3
	}

	public enum KingdomPolityFrontTarget : byte
	{
		None = 0,
		Settlement = 1,
		Route = 2,
		Site = 3,
		Cohort = 4
	}

	public enum KingdomPolityFrontPhase : byte
	{
		Quiet = 0,
		Friction = 1,
		Contested = 2,
		ConfrontationAvailable = 3,
		Truce = 4,
		Ended = 5
	}

	public enum KingdomPolityCohortPurpose : byte
	{
		None = 0,
		Guard = 1,
		Patrol = 2,
		Trader = 3,
		Envoy = 4,
		Courier = 5,
		Warband = 6,
		Migrant = 7
	}

	public enum KingdomPolityCohortPhase : byte
	{
		Planned = 0,
		Materialized = 1,
		Concluded = 2,
		Cleaned = 3,
		Archived = 4,
		Cancelled = 5,
		/// <summary>Exact physical loss proved; no semantic death, return, or reward was claimed.</summary>
		Abandoned = 6
	}

	public enum KingdomPolityLoadoutPolicyKind : byte
	{
		None = 0,
		StockPreserve = 1,
		OwnedReplace = 2,
		BoundedAdd = 3
	}

	public enum KingdomPolityFigureOrigin : byte
	{
		None = 0,
		Officeholder = 1,
		PromotedByDeed = 2,
		PlayerNamed = 3,
		Successor = 4,
		Namesake = 5,
		Claimant = 6,
		LegacyEnvoy = 7
	}

	public enum KingdomPolityFigurePhase : byte
	{
		Active = 0,
		Retired = 1,
		Departed = 2,
		Dead = 3,
		Missing = 4,
		Transferred = 5
	}

	public enum KingdomPolityResolutionKind : byte
	{
		None = 0,
		LiveScene = 1,
		ConsentedEscrow = 2
	}

	public enum KingdomPolitySystemicDeltaKind : byte
	{
		None = 0,
		Relation = 1,
		RoutePosture = 2,
		ClaimPosture = 3,
		Standing = 4,
		ReservedStake = 5,
		ReversibleWound = 6
	}

	public enum KingdomPolityProjectionKind : byte
	{
		None = 0,
		Faction = 1,
		Relation = 2,
		CohortManifestation = 3,
		RoutePrompt = 4,
		IncidentView = 5,
		Aftermath = 6,
		FactionTombstone = 7,
		ConsentedEscrow = 8
	}

	public enum KingdomPolityProjectionPhase : byte
	{
		Prepared = 0,
		Committed = 1,
		Cleaned = 2,
		Archived = 3,
		Cancelled = 4
	}

	public enum KingdomPolityImportPolicy : byte
	{
		Off = 0,
		LatestEligible = 1
	}

	public enum KingdomPolityPresentationState : byte
	{
		Unobserved = 0,
		Enabled = 1,
		Disabled = 2
	}

	/// <summary>Durable current-realm exile/refounding transaction phase.</summary>
	public enum KingdomPolityRealmTransitionPhase : byte
	{
		None = 0,
		Prepared = 1,
		Tombstoned = 2,
		Detached = 3,
		Rebound = 4,
		Restored = 5,
		Quarantined = 6
	}
}
