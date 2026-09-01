namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		private long BenefitEpoch;
		private long BenefitSnapshotEpoch = -1L;
		private bool BenefitSnapshotAttempted;
		private KingdomBenefitIndex BenefitSnapshot;
		private string BenefitSnapshotFailure;

		/// <summary>Begins a named semantic step. Every unchanged read inside that step shares one
		/// immutable observation; explicit physical mutation starts another epoch.</summary>
		internal static void BeginBenefitEpochInActive()
		{
			if (BoundSurvey != null) BoundSurvey.InvalidateBenefits();
		}

		/// <summary>Marks non-topological operation state (staffing, charge, or a custom predicate)
		/// changed after its caller committed that state.</summary>
		internal void InvalidateBenefits()
		{
			BenefitEpoch = BenefitEpoch == long.MaxValue ? 1L : BenefitEpoch + 1L;
			BenefitSnapshotEpoch = -1L;
			BenefitSnapshotAttempted = false;
			BenefitSnapshot = null;
			BenefitSnapshotFailure = null;
		}

		/// <summary>Observes physical benefits from this exact maintained survey. A bound pass caches
		/// both success and failure for its current mutation epoch. Unbound inspection remains a fresh
		/// observation because no transaction boundary can prove unseen third-party state stable.</summary>
		public bool TryBenefits(out KingdomBenefitIndex Benefits, out string Failure)
		{
			Benefits = null; Failure = null;
			if (Ground == null) { Failure = "benefit reading has no ground"; return false; }
			bool cache = ReferenceEquals(BoundSurvey, this);
			if (cache && BenefitSnapshotAttempted && BenefitSnapshotEpoch == BenefitEpoch)
			{
				Benefits = BenefitSnapshot; Failure = BenefitSnapshotFailure;
				return Benefits != null;
			}
			bool success = KingdomDesignationIndex.TryActiveZone(Ground, this,
				out KingdomDesignationIndex designations, out Failure)
				&& KingdomBenefitIndex.TryBuild(Ground, this, designations,
					out Benefits, out Failure);
			if (cache)
			{
				BenefitSnapshotAttempted = true; BenefitSnapshotEpoch = BenefitEpoch;
				BenefitSnapshot = Benefits; BenefitSnapshotFailure = Failure;
			}
			return success;
		}
	}
}
