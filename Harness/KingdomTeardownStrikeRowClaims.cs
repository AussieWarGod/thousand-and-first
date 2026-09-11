namespace ThousandAndFirst
{
	/// <summary>Engine-free predicate for the exact registry row a successful OrderStrike must
	/// have minted (Growth/KingdomMaterials.08.StrikeOrdering.cs:257-261 NewJob, route Strike).
	/// Mirrors the KingdomQuickstartBuildClaims pattern: pure fields in, pure bool out, no
	/// GameObject/Zone dependency, so it is value-testable without the game.</summary>
	internal static class KingdomTeardownStrikeRowClaims
	{
		internal static bool IsExpectedStrikeRow(KingdomConstructionJob Row,
			string ExpectedWorksId, string ExpectedOwnerKey, string ExpectedZoneId,
			out string Failure)
		{
			Failure = null;
			if (Row == null)
				return Fail(out Failure, "no registry row was found for the captured strike receipt");
			if (Row.Route != KingdomConstructionRoute.Strike)
				return Fail(out Failure, "the registry row's route is not the strike route");
			if (Row.SubjectId != ExpectedWorksId || Row.SourceId != ExpectedWorksId)
				return Fail(out Failure,
					"the registry row's subject/source id does not name the struck object");
			if (Row.OwnerKey != ExpectedOwnerKey)
				return Fail(out Failure, "the registry row's owner key is not this settlement's");
			if (Row.ZoneId != ExpectedZoneId)
				return Fail(out Failure, "the registry row's zone id does not match the struck zone");
			if (Row.Phase != KingdomConstructionPhase.Published)
				return Fail(out Failure,
					"the registry row is not in its expected freshly-minted strike phase");
			return true;
		}

		private static bool Fail(out string Failure, string Reason)
		{
			Failure = Reason;
			return false;
		}
	}
}
