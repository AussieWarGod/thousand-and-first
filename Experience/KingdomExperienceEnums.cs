using System;

namespace ThousandAndFirst
{
	public enum KingdomExperienceSchemaState : byte
	{
		Compatible = 0,
		Quarantined = 1,
		Unknown = 2
	}

	public enum KingdomExperienceOptionKind : byte
	{
		None = 0,
		CivicStory = 1,
		CivicKnowledge = 2,
		AmbientUse = 3
	}

	public enum KingdomExperienceOptionState : byte
	{
		Unobserved = 0,
		Disabled = 1,
		Enabled = 2
	}

	/// <summary>Typed optional lanes only. This is capacity vocabulary, not a story selector.</summary>
	public enum KingdomExperienceLane : byte
	{
		None = 0,
		CivicVoices = 1,
		Memorial = 2,
		Office = 3,
		SocialLocus = 4,
		WitnessWork = 5,
		FirstGuest = 6,
		FirstFeast = 7,
		Curator = 8,
		CommunalRite = 9,
		RouteCorrespondence = 10,
		BodyHistory = 11,
		ArtifactRecognition = 12,
		PolityCohort = 13
	}

	public enum KingdomExperienceCapacityFault : byte
	{
		None = 0,
		InvalidLedger = 1,
		InvalidRequest = 2,
		RevisionConflict = 3,
		WrongRealm = 4,
		OptionDisabled = 5,
		CauseBeforeEnable = 6,
		AudienceCapacityFull = 7,
		LiveBodyCapacityFull = 8,
		ReservationCapacityFull = 9,
		DuplicateMismatch = 10,
		OwnershipMismatch = 11,
		RevisionExhausted = 12
	}

	/// <summary>Read-only classification of one durable capacity row.</summary>
	public enum KingdomExperienceLeaseState : byte
	{
		Missing = 0,
		Active = 1,
		Retirement = 2
	}

	public enum KingdomExperienceExperiment : byte
	{
		None = 0,
		CivicVoices = 1,
		Memorial = 2,
		SocialLocus = 3,
		FirstFeastPractice = 4,
		FirstGuestCorrespondence = 5,
		Curator = 6,
		GuestsFeast = 7
	}

	public enum KingdomExperienceTrialArm : byte
	{
		None = 0,
		FactsOnly = 1,
		SemanticOnly = 2,
		Projected = 3,
		Integrated = 4
	}

	public enum KingdomExperienceFixture : byte
	{
		None = 0,
		Choice = 1,
		DeathRow = 2,
		LocusVisit = 3,
		PracticeProposal = 4,
		ArrivalOpportunity = 5,
		KnownDestination = 6,
		WholeArc = 7
	}

	public enum KingdomExperienceObservationKind : byte
	{
		None = 0,
		Exposed = 1,
		Viewed = 2,
		Committed = 3,
		Closed = 4,
		RecallSucceeded = 5,
		RecallFailed = 6,
		DestinationVisited = 7,
		QuietCompletion = 8
	}
}
