using System;

namespace ThousandAndFirst
{
	/// <summary>Staged no-backlog re-anchor for one observed master-switch resume.</summary>
	public sealed class KingdomExperienceMasterResumePlan
	{
		public readonly string RealmId;
		public readonly long SourceRevision;
		public readonly long TargetRevision;
		public readonly long DisabledAtTick;
		public readonly long ResumeTick;
		internal readonly byte[] SourceEnvelope;
		internal readonly KingdomExperienceLedger Target;

		internal KingdomExperienceMasterResumePlan(string realmId, long sourceRevision,
			long disabledAt, long resumeTick, byte[] sourceEnvelope,
			KingdomExperienceLedger target)
		{
			RealmId = realmId; SourceRevision = sourceRevision;
			TargetRevision = target.Revision; DisabledAtTick = disabledAt;
			ResumeTick = resumeTick; SourceEnvelope = sourceEnvelope; Target = target;
		}
	}

	public static partial class KingdomExperienceRules
	{
		public static bool TryPrepareMasterResume(KingdomExperienceLedger Ledger,
			string RealmId, long DisabledAtTick, long ResumeTick, bool StoryEnabled,
			bool KnowledgeEnabled, bool AmbientEnabled,
			out KingdomExperienceMasterResumePlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (!TypedId(RealmId, "taf:realm:") || DisabledAtTick < 0L
				|| ResumeTick < DisabledAtTick)
				return Fail("experience master-resume context is invalid", out Failure);
			KingdomExperienceLedger source = Ledger ?? new KingdomExperienceLedger();
			if (!TryValidate(source, out Failure)) return false;
			long sourceRevision = source.Revision;
			byte[] sourceEnvelope = KingdomExperienceCodec.EncodeEnvelope(source);
			KingdomExperienceLedger candidate = Clone(source);
			if (!candidate.IdentityBound)
			{
				if (!TryBindEmptyIdentity(candidate, RealmId, out Failure)) return false;
			}
			else if (!string.Equals(candidate.RealmId, RealmId, StringComparison.Ordinal))
				return Fail("experience master-resume belongs to another realm", out Failure);
			if (!RowsPredatePause(candidate, DisabledAtTick))
				return Fail("experience lease was reserved during the master pause", out Failure);
			if (candidate.Revision == long.MaxValue
				|| (StoryEnabled && candidate.Story.EnableEpoch == long.MaxValue)
				|| (KnowledgeEnabled && candidate.Knowledge.EnableEpoch == long.MaxValue)
				|| (AmbientEnabled && candidate.Ambient.EnableEpoch == long.MaxValue))
				return Fail("experience master-resume authority is exhausted", out Failure);
			Reanchor(candidate.Story, StoryEnabled, ResumeTick);
			Reanchor(candidate.Knowledge, KnowledgeEnabled, ResumeTick);
			Reanchor(candidate.Ambient, AmbientEnabled, ResumeTick);
			candidate.Revision++;
			if (!TryValidate(candidate, out Failure)) return false;
			Plan = new KingdomExperienceMasterResumePlan(RealmId, sourceRevision,
				DisabledAtTick, ResumeTick, sourceEnvelope, candidate); return true;
		}

		public static bool TryPublishMasterResume(KingdomExperienceLedger Ledger,
			KingdomExperienceMasterResumePlan Plan, out string Failure)
		{
			Failure = null;
			if (Ledger == null || Plan == null || Plan.Target == null)
				return Fail("experience master-resume plan is absent", out Failure);
			if (!TryValidate(Ledger, out Failure) || !TryValidate(Plan.Target, out Failure))
				return false;
			if (Exact(Ledger, Plan.Target)) return true;
			if (!CanPublishMasterResume(Ledger, Plan, out Failure)) return false;
			PublishMasterResumePrevalidated(Ledger, Plan); return true;
		}

		internal static bool CanPublishMasterResume(KingdomExperienceLedger Ledger,
			KingdomExperienceMasterResumePlan Plan, out string Failure)
		{
			Failure = null;
			if (Ledger == null || Plan == null || Plan.Target == null
				|| !TryValidate(Ledger, out Failure) || !TryValidate(Plan.Target, out Failure))
				return false;
			return Exact(Ledger, Plan.SourceEnvelope) || Fail(
				"experience master-resume publication lost its staged CAS", out Failure);
		}

		internal static void PublishMasterResumePrevalidated(KingdomExperienceLedger Ledger,
			KingdomExperienceMasterResumePlan Plan)
		{
			Ledger.CopyFrom(Clone(Plan.Target));
		}

		private static void Reanchor(KingdomExperienceOptionReceipt Option, bool Enabled,
			long Tick)
		{
			Option.State = Enabled ? KingdomExperienceOptionState.Enabled
				: KingdomExperienceOptionState.Disabled;
			Option.ObservedTick = Tick;
			Option.FutureCauseFloorTick = Enabled ? Tick : long.MaxValue;
			if (Enabled) Option.EnableEpoch++;
		}

		private static bool RowsPredatePause(KingdomExperienceLedger Ledger, long DisabledAt)
		{
			// Equal-tick authority may have committed before the master transition. The transition
			// consumes its wake, so no later same-tick automatic work can create another row.
			for (int i = 0; i < Ledger.Audiences.Count; i++)
				if (Ledger.Audiences[i].ReservedTick > DisabledAt) return false;
			for (int i = 0; i < Ledger.BodyReservations.Count; i++)
				if (Ledger.BodyReservations[i].ReservedTick > DisabledAt) return false;
			for (int i = 0; i < Ledger.Voices.Count; i++)
				if (Ledger.Voices[i].CauseTick > DisabledAt
					|| Ledger.Voices[i].CallbackConsumed
					&& Ledger.Voices[i].CallbackTick > DisabledAt) return false;
			for (int i = 0; i < Ledger.FirstFeasts.Count; i++)
				if (Ledger.FirstFeasts[i].OfferedTick > DisabledAt
					|| Ledger.FirstFeasts[i].DecidedTick > DisabledAt) return false;
			return true;
		}

		private static bool Exact(KingdomExperienceLedger A, KingdomExperienceLedger B)
		{
			byte[] left = KingdomExperienceCodec.EncodeEnvelope(A);
			byte[] right = KingdomExperienceCodec.EncodeEnvelope(B);
			if (left.Length != right.Length) return false;
			for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
			return true;
		}

		private static bool Exact(KingdomExperienceLedger Ledger, byte[] Envelope)
		{
			if (Envelope == null) return false;
			byte[] current = KingdomExperienceCodec.EncodeEnvelope(Ledger);
			if (current.Length != Envelope.Length) return false;
			for (int i = 0; i < current.Length; i++) if (current[i] != Envelope[i]) return false;
			return true;
		}
	}
}
