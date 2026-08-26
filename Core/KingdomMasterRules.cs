using System;

namespace ThousandAndFirst
{
	/// <summary>Persisted observation of the realm-wide master switch.</summary>
	public enum KingdomMasterLatchValue : byte
	{
		Unobserved = 0,
		Disabled = 1,
		Enabled = 2
	}

	/// <summary>What one observation changed. Values are diagnostic, not save format.</summary>
	public enum KingdomMasterTransition : byte
	{
		None = 0,
		InitializedDisabled = 1,
		InitializedEnabled = 2,
		Disabled = 3,
		ResumeRequired = 4
	}

	/// <summary>
	/// Immutable result of reading the master setting. A resume token is deliberately issued but
	/// not applied here: every module must first stage its own future clock. Only the coordinator
	/// may publish <see cref="KingdomMasterRules.ApplyResume"/> after all staging succeeds.
	/// </summary>
	public sealed class KingdomMasterDecision
	{
		public readonly bool Valid;
		public readonly KingdomMasterLatchValue State;
		public readonly long ChangedAtTick;
		public readonly long ResumeToken;
		public readonly long AppliedResumeToken;
		public readonly KingdomMasterTransition Transition;

		internal KingdomMasterDecision(bool valid, KingdomMasterLatchValue state,
			long changedAtTick, long resumeToken, long appliedResumeToken,
			KingdomMasterTransition transition)
		{
			Valid = valid;
			State = state;
			ChangedAtTick = changedAtTick;
			ResumeToken = resumeToken;
			AppliedResumeToken = appliedResumeToken;
			Transition = transition;
		}

		public bool ResumePending
		{
			get { return Valid && State == KingdomMasterLatchValue.Enabled
				&& ResumeToken != AppliedResumeToken; }
		}

		public bool AutomaticWorkAllowed
		{
			get { return Valid && State == KingdomMasterLatchValue.Enabled
				&& ResumeToken == AppliedResumeToken; }
		}
	}

	/// <summary>Engine-free realm master-switch algebra.</summary>
	public static class KingdomMasterRules
	{
		public static KingdomMasterDecision Observe(KingdomMasterLatchValue prior,
			long changedAtTick, long resumeToken, long appliedResumeToken,
			bool configuredEnabled, long now)
		{
			if (!WellFormed(prior, changedAtTick, resumeToken, appliedResumeToken)
				|| now < 0L || now < changedAtTick)
				return Invalid(prior, changedAtTick, resumeToken, appliedResumeToken);

			if (prior == KingdomMasterLatchValue.Unobserved)
			{
				return new KingdomMasterDecision(true,
					configuredEnabled ? KingdomMasterLatchValue.Enabled
						: KingdomMasterLatchValue.Disabled,
					now, resumeToken, appliedResumeToken,
					configuredEnabled ? KingdomMasterTransition.InitializedEnabled
						: KingdomMasterTransition.InitializedDisabled);
			}

			KingdomMasterLatchValue observed = configuredEnabled
				? KingdomMasterLatchValue.Enabled : KingdomMasterLatchValue.Disabled;
			if (prior == observed)
				return new KingdomMasterDecision(true, prior, changedAtTick,
					resumeToken, appliedResumeToken, KingdomMasterTransition.None);

			if (!configuredEnabled)
				return new KingdomMasterDecision(true, KingdomMasterLatchValue.Disabled,
					now, resumeToken, appliedResumeToken, KingdomMasterTransition.Disabled);

			if (resumeToken == long.MaxValue)
				return Invalid(prior, changedAtTick, resumeToken, appliedResumeToken);
			return new KingdomMasterDecision(true, KingdomMasterLatchValue.Enabled, now,
				resumeToken + 1L, appliedResumeToken, KingdomMasterTransition.ResumeRequired);
		}

		/// <summary>Publishes exactly the pending resume token; repeats are no-ops.</summary>
		public static KingdomMasterDecision ApplyResume(KingdomMasterDecision staged)
		{
			if (staged == null || !staged.Valid || staged.State != KingdomMasterLatchValue.Enabled
				|| staged.AppliedResumeToken > staged.ResumeToken)
				return staged == null ? Invalid(KingdomMasterLatchValue.Unobserved, 0L, 0L, 0L)
					: Invalid(staged.State, staged.ChangedAtTick, staged.ResumeToken,
						staged.AppliedResumeToken);
			if (!staged.ResumePending) return staged;
			return new KingdomMasterDecision(true, staged.State, staged.ChangedAtTick,
				staged.ResumeToken, staged.ResumeToken, staged.Transition);
		}

		/// <summary>Every newly scheduled producer receives a strict-future full interval.</summary>
		public static bool TryFutureDeadline(long now, long intervalTicks, out long deadline)
		{
			deadline = 0L;
			if (now < 0L || intervalTicks <= 0L || now > long.MaxValue - intervalTicks)
				return false;
			deadline = now + intervalTicks;
			return deadline > now;
		}

		/// <summary>
		/// Freezes an already-committed future deadline for the disabled span. Work which was due
		/// before disable remains due: master pause may delay recovery, never rewrite its history.
		/// </summary>
		public static bool TryResumeCommittedDeadline(long priorDeadline, long disabledAtTick,
			long now, out long deadline)
		{
			deadline = priorDeadline;
			if (priorDeadline < 0L || disabledAtTick < 0L || now < disabledAtTick)
				return false;
			if (priorDeadline <= disabledAtTick) return true;
			long paused = now - disabledAtTick;
			if (priorDeadline > long.MaxValue - paused) return false;
			deadline = priorDeadline + paused;
			return deadline > now;
		}

		public static bool WellFormed(KingdomMasterLatchValue state, long changedAtTick,
			long resumeToken, long appliedResumeToken)
		{
			if (!Enum.IsDefined(typeof(KingdomMasterLatchValue), state)
				|| changedAtTick < 0L || resumeToken < 0L || appliedResumeToken < 0L
				|| appliedResumeToken > resumeToken) return false;
			return state != KingdomMasterLatchValue.Unobserved
				|| (changedAtTick == 0L && resumeToken == 0L && appliedResumeToken == 0L);
		}

		private static KingdomMasterDecision Invalid(KingdomMasterLatchValue state,
			long tick, long token, long applied)
		{
			return new KingdomMasterDecision(false, state, tick, token, applied,
				KingdomMasterTransition.None);
		}
	}
}
