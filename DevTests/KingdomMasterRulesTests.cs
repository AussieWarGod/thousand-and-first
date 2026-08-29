#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomMasterRulesTests
	{
		private static string Scalars(KingdomMasterDecision value)
		{
			return ((byte)value.State) + "|" + value.ChangedAtTick + "|"
				+ value.ResumeToken + "|" + value.AppliedResumeToken;
		}

		[Test]
		public void FirstObservationPublishesOneExplicitLatchAndNeverInventsAToken()
		{
			KingdomMasterDecision off = KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Unobserved, 0L, 0L, 0L, false, 40L);
			Assert.IsTrue(off.Valid);
			Assert.AreEqual(KingdomMasterLatchValue.Disabled, off.State);
			Assert.AreEqual(KingdomMasterTransition.InitializedDisabled, off.Transition);
			Assert.IsFalse(off.AutomaticWorkAllowed);
			Assert.AreEqual("1|40|0|0", Scalars(off));

			KingdomMasterDecision on = KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Unobserved, 0L, 0L, 0L, true, 40L);
			Assert.IsTrue(on.Valid);
			Assert.AreEqual(KingdomMasterLatchValue.Enabled, on.State);
			Assert.AreEqual(KingdomMasterTransition.InitializedEnabled, on.Transition);
			Assert.AreEqual("2|40|0|0", Scalars(on));
		}

		[Test]
		public void SteadyDisabledObservationIsByteEquivalentAndAllowsNoAutomaticWork()
		{
			KingdomMasterDecision observed = KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Disabled, 123L, 8L, 8L, false, 999L);
			Assert.IsTrue(observed.Valid);
			Assert.AreEqual(KingdomMasterTransition.None, observed.Transition);
			Assert.AreEqual("1|123|8|8", Scalars(observed));
			Assert.IsFalse(observed.AutomaticWorkAllowed);
		}

		[TestCase(99L)]
		[TestCase(100L)]
		[TestCase(101L)]
		public void DisableAndResumeTransitionsWinAtDueMinusOneDueAndDuePlusOne(long boundary)
		{
			const long due = 100L;
			KingdomMasterDecision disabled = KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Enabled, 5L, 2L, 2L, false, boundary);
			Assert.IsTrue(disabled.Valid);
			Assert.AreEqual(KingdomMasterTransition.Disabled, disabled.Transition);
			Assert.IsFalse(disabled.AutomaticWorkAllowed,
				"due work must not run on the disabling observation");

			long resumeAt = boundary + 7L;
			KingdomMasterDecision staged = KingdomMasterRules.Observe(disabled.State,
				disabled.ChangedAtTick, disabled.ResumeToken, disabled.AppliedResumeToken,
				true, resumeAt);
			Assert.AreEqual(KingdomMasterTransition.ResumeRequired, staged.Transition);
			Assert.IsTrue(staged.ResumePending);
			Assert.IsFalse(staged.AutomaticWorkAllowed,
				"due work must not run before every module publishes its resume latch");

			Assert.IsTrue(KingdomMasterRules.TryFutureDeadline(resumeAt, 10L,
				out long newDeadline));
			Assert.AreEqual(resumeAt + 10L, newDeadline);
			Assert.Greater(newDeadline, resumeAt);

			Assert.IsTrue(KingdomMasterRules.TryResumeCommittedDeadline(due, boundary,
				resumeAt, out long committedDeadline));
			if (due <= boundary) Assert.AreEqual(due, committedDeadline);
			else Assert.AreEqual(due + (resumeAt - boundary), committedDeadline);

			KingdomMasterDecision applied = KingdomMasterRules.ApplyResume(staged);
			Assert.IsTrue(applied.AutomaticWorkAllowed);
			Assert.AreEqual(applied.ResumeToken, applied.AppliedResumeToken);
			Assert.AreEqual(Scalars(applied), Scalars(KingdomMasterRules.ApplyResume(applied)),
				"replaying the apply step must be an exact no-op");
		}

		[Test]
		public void ReloadReconstructsDisabledPendingAndAppliedTransitionsExactly()
		{
			KingdomMasterDecision disabled = KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Unobserved, 0L, 0L, 0L, false, 45L);
			string persistedDisabled = Scalars(disabled);
			KingdomMasterDecision stillDisabled = KingdomMasterRules.Observe(disabled.State,
				disabled.ChangedAtTick, disabled.ResumeToken, disabled.AppliedResumeToken,
				false, 80L);
			Assert.AreEqual(persistedDisabled, Scalars(stillDisabled));

			KingdomMasterDecision pending = KingdomMasterRules.Observe(stillDisabled.State,
				stillDisabled.ChangedAtTick, stillDisabled.ResumeToken,
				stillDisabled.AppliedResumeToken, true, 80L);
			Assert.IsTrue(pending.ResumePending);
			Assert.AreEqual("2|80|1|0", Scalars(pending));
			KingdomMasterDecision applied = KingdomMasterRules.ApplyResume(pending);
			Assert.AreEqual("2|80|1|1", Scalars(applied));

			KingdomMasterDecision reloaded = KingdomMasterRules.Observe(applied.State,
				applied.ChangedAtTick, applied.ResumeToken, applied.AppliedResumeToken,
				true, 81L);
			Assert.AreEqual(KingdomMasterTransition.None, reloaded.Transition);
			Assert.AreEqual(Scalars(applied), Scalars(reloaded));
			Assert.IsTrue(reloaded.AutomaticWorkAllowed);
		}

		[Test]
		public void MalformedAndOverflowEvidenceFailsClosed()
		{
			Assert.IsFalse(KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Unobserved, 1L, 0L, 0L, true, 1L).Valid);
			Assert.IsFalse(KingdomMasterRules.Observe(
				(KingdomMasterLatchValue)99, 0L, 0L, 0L, true, 1L).Valid);
			Assert.IsFalse(KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Enabled, 0L, 1L, 2L, true, 1L).Valid);
			Assert.IsFalse(KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Disabled, 1L, long.MaxValue, long.MaxValue,
				true, 2L).Valid);
			Assert.IsFalse(KingdomMasterRules.TryFutureDeadline(long.MaxValue, 1L, out _));
			Assert.IsFalse(KingdomMasterRules.TryResumeCommittedDeadline(long.MaxValue,
				0L, 1L, out _));
		}

		[Test]
		public void PausedCharterSurfaceIsReportsAndCommittedRecoveryOnly()
		{
			HashSet<KingdomCharterAction> expected = new HashSet<KingdomCharterAction>
			{
				KingdomCharterAction.HearPetition,
				KingdomCharterAction.Status,
				KingdomCharterAction.Homecoming,
				KingdomCharterAction.ChronicleAndDynasty,
				KingdomCharterAction.OutsiderChronicle,
				KingdomCharterAction.Standings,
				KingdomCharterAction.SettlerRoll,
				KingdomCharterAction.AnswerThreat,
				KingdomCharterAction.CityBook,
				KingdomCharterAction.TechMap,
				KingdomCharterAction.CityAsks,
				KingdomCharterAction.FirstGuestCorrespondence,
				KingdomCharterAction.FirstFeastPractice,
				KingdomCharterAction.PracticeAndVocation,
				KingdomCharterAction.CivicKnowledge,
				KingdomCharterAction.BodyHistory,
				KingdomCharterAction.GuestFeastRecord,
				KingdomCharterAction.CivicCommitments
			};
			foreach (KingdomCharterAction action in Enum.GetValues(typeof(KingdomCharterAction)))
				Assert.AreEqual(expected.Contains(action),
					KingdomCharterMenuRules.AvailableWhileSimulationPaused(action), action.ToString());
		}
	}
}
#endif
