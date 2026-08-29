using System;

namespace ThousandAndFirst
{
	/// <summary>One privacy-bounded session observation. It contains no realm, save, person, object,
	/// path, wall-clock, free-text, or game-tick identity.</summary>
	internal readonly struct KingdomExperienceTelemetryReceipt
	{
		internal readonly long Sequence;
		internal readonly KingdomExperienceExperiment Experiment;
		internal readonly KingdomExperienceTrialArm Arm;
		internal readonly KingdomExperienceFixture Fixture;
		internal readonly KingdomExperienceObservationKind Observation;
		internal readonly int Measure;

		internal KingdomExperienceTelemetryReceipt(long Sequence,
			KingdomExperienceExperiment Experiment, KingdomExperienceTrialArm Arm,
			KingdomExperienceFixture Fixture, KingdomExperienceObservationKind Observation,
			int Measure)
		{
			this.Sequence = Sequence; this.Experiment = Experiment; this.Arm = Arm;
			this.Fixture = Fixture; this.Observation = Observation; this.Measure = Measure;
		}
	}

	/// <summary>Fixed session ring. Overwrite is explicit through <see cref="Dropped"/> and can
	/// never affect gameplay authority.</summary>
	internal sealed class KingdomExperienceTelemetryBuffer
	{
		internal const int Capacity = 128;
		internal const int MaxMeasure = 1000000;
		private readonly KingdomExperienceTelemetryReceipt[] Rows =
			new KingdomExperienceTelemetryReceipt[Capacity];
		private int CountValue;
		private int Cursor;
		private long NextSequence = 1L;
		private long DroppedValue;

		internal int Count { get { return CountValue; } }
		internal long Dropped { get { return DroppedValue; } }

		internal bool TryRecord(KingdomExperienceExperiment Experiment,
			KingdomExperienceTrialArm Arm, KingdomExperienceFixture Fixture,
			KingdomExperienceObservationKind Observation, int Measure)
		{
			if (!KingdomExperienceTelemetryRules.Valid(Experiment, Arm, Fixture, Observation,
				Measure) || NextSequence == long.MaxValue) return false;
			Rows[Cursor] = new KingdomExperienceTelemetryReceipt(NextSequence++, Experiment,
				Arm, Fixture, Observation, Measure);
			Cursor = (Cursor + 1) % Capacity;
			if (CountValue < Capacity) CountValue++;
			else if (DroppedValue < long.MaxValue) DroppedValue++;
			return true;
		}

		internal bool TryGet(int OrdinalFromOldest,
			out KingdomExperienceTelemetryReceipt Receipt)
		{
			Receipt = default(KingdomExperienceTelemetryReceipt);
			if (OrdinalFromOldest < 0 || OrdinalFromOldest >= CountValue) return false;
			int oldest = CountValue < Capacity ? 0 : Cursor;
			Receipt = Rows[(oldest + OrdinalFromOldest) % Capacity]; return true;
		}
	}

	internal static class KingdomExperienceTelemetryRules
	{
		internal static bool Valid(KingdomExperienceExperiment Experiment,
			KingdomExperienceTrialArm Arm, KingdomExperienceFixture Fixture,
			KingdomExperienceObservationKind Observation, int Measure)
		{
			if (Experiment < KingdomExperienceExperiment.CivicVoices
				|| Experiment > KingdomExperienceExperiment.GuestsFeast
				|| Arm < KingdomExperienceTrialArm.FactsOnly
				|| Arm > KingdomExperienceTrialArm.Integrated
				|| Observation < KingdomExperienceObservationKind.Exposed
				|| Observation > KingdomExperienceObservationKind.QuietCompletion
				|| Measure < 0 || Measure > KingdomExperienceTelemetryBuffer.MaxMeasure)
				return false;
			return Fixture == FixtureFor(Experiment) && ArmAllowed(Experiment, Arm);
		}

		internal static KingdomExperienceFixture FixtureFor(
			KingdomExperienceExperiment Experiment)
		{
			if (Experiment == KingdomExperienceExperiment.CivicVoices)
				return KingdomExperienceFixture.Choice;
			if (Experiment == KingdomExperienceExperiment.Memorial)
				return KingdomExperienceFixture.DeathRow;
			if (Experiment == KingdomExperienceExperiment.SocialLocus)
				return KingdomExperienceFixture.LocusVisit;
			if (Experiment == KingdomExperienceExperiment.FirstFeastPractice)
				return KingdomExperienceFixture.PracticeProposal;
			if (Experiment == KingdomExperienceExperiment.FirstGuestCorrespondence)
				return KingdomExperienceFixture.ArrivalOpportunity;
			if (Experiment == KingdomExperienceExperiment.Curator)
				return KingdomExperienceFixture.KnownDestination;
			if (Experiment == KingdomExperienceExperiment.GuestsFeast)
				return KingdomExperienceFixture.WholeArc;
			return KingdomExperienceFixture.None;
		}

		private static bool ArmAllowed(KingdomExperienceExperiment Experiment,
			KingdomExperienceTrialArm Arm)
		{
			if (Experiment == KingdomExperienceExperiment.CivicVoices
				|| Experiment == KingdomExperienceExperiment.Curator)
				return Arm == KingdomExperienceTrialArm.FactsOnly
					|| Arm == KingdomExperienceTrialArm.SemanticOnly;
			if (Experiment == KingdomExperienceExperiment.Memorial
				|| Experiment == KingdomExperienceExperiment.SocialLocus)
				return Arm == KingdomExperienceTrialArm.FactsOnly
					|| Arm == KingdomExperienceTrialArm.Projected;
			if (Experiment == KingdomExperienceExperiment.FirstFeastPractice
				|| Experiment == KingdomExperienceExperiment.FirstGuestCorrespondence)
				return Arm == KingdomExperienceTrialArm.FactsOnly
					|| Arm == KingdomExperienceTrialArm.SemanticOnly
					|| Arm == KingdomExperienceTrialArm.Projected;
			return Experiment == KingdomExperienceExperiment.GuestsFeast;
		}
	}
}
