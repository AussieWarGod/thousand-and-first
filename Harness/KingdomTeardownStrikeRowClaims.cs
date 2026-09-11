namespace ThousandAndFirst
{
	/// <summary>Engine-free predicate for the exact registry row a successful OrderStrike must
	/// have minted AND fully stamped (Growth/KingdomMaterials.08.StrikeOrdering.cs:257-261
	/// NewJob, route Strike; OrderStrikeDurable does not return true until
	/// ResumeStrikeStamp succeeds, :274). ResumeStrikeStamp itself sets PhysicalPhase to
	/// StrikeWorking (Growth/KingdomMaterials.09.StrikeStampAndCancellation.cs:51) and then
	/// transitions Phase to Working via FinishProjection (:55-56) -- Phase is NEVER left at
	/// NewJob's initial Published once OrderStrike has actually returned true, so a real
	/// post-call row must read Phase==Working and PhysicalPhase==StrikeWorking, not Published.
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
			if (Row.Phase != KingdomConstructionPhase.Working)
				return Fail(out Failure,
					"the registry row has not been stamped to Working by ResumeStrikeStamp/"
					+ "FinishProjection");
			if (Row.PhysicalPhase != KingdomPhysicalPhase.StrikeWorking)
				return Fail(out Failure,
					"the registry row's physical phase is not StrikeWorking");
			return true;
		}

		private static bool Fail(out string Failure, string Reason)
		{
			Failure = Reason;
			return false;
		}
	}
}
