#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(off.Valid);
			ClassicAssert.AreEqual(KingdomMasterLatchValue.Disabled, off.State);
			ClassicAssert.AreEqual(KingdomMasterTransition.InitializedDisabled, off.Transition);
			ClassicAssert.IsFalse(off.AutomaticWorkAllowed);
			ClassicAssert.AreEqual("1|40|0|0", Scalars(off));

			KingdomMasterDecision on = KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Unobserved, 0L, 0L, 0L, true, 40L);
			ClassicAssert.IsTrue(on.Valid);
			ClassicAssert.AreEqual(KingdomMasterLatchValue.Enabled, on.State);
			ClassicAssert.AreEqual(KingdomMasterTransition.InitializedEnabled, on.Transition);
			ClassicAssert.AreEqual("2|40|0|0", Scalars(on));
		}

		[Test]
		public void SteadyDisabledObservationIsByteEquivalentAndAllowsNoAutomaticWork()
		{
			KingdomMasterDecision observed = KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Disabled, 123L, 8L, 8L, false, 999L);
			ClassicAssert.IsTrue(observed.Valid);
			ClassicAssert.AreEqual(KingdomMasterTransition.None, observed.Transition);
			ClassicAssert.AreEqual("1|123|8|8", Scalars(observed));
			ClassicAssert.IsFalse(observed.AutomaticWorkAllowed);
		}

		[TestCase(99L)]
		[TestCase(100L)]
		[TestCase(101L)]
		public void DisableAndResumeTransitionsWinAtDueMinusOneDueAndDuePlusOne(long boundary)
		{
			const long due = 100L;
			KingdomMasterDecision disabled = KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Enabled, 5L, 2L, 2L, false, boundary);
			ClassicAssert.IsTrue(disabled.Valid);
			ClassicAssert.AreEqual(KingdomMasterTransition.Disabled, disabled.Transition);
			ClassicAssert.IsFalse(disabled.AutomaticWorkAllowed,
				"due work must not run on the disabling observation");

			long resumeAt = boundary + 7L;
			KingdomMasterDecision staged = KingdomMasterRules.Observe(disabled.State,
				disabled.ChangedAtTick, disabled.ResumeToken, disabled.AppliedResumeToken,
				true, resumeAt);
			ClassicAssert.AreEqual(KingdomMasterTransition.ResumeRequired, staged.Transition);
			ClassicAssert.IsTrue(staged.ResumePending);
			ClassicAssert.IsFalse(staged.AutomaticWorkAllowed,
				"due work must not run before every module publishes its resume latch");

			ClassicAssert.IsTrue(KingdomMasterRules.TryFutureDeadline(resumeAt, 10L,
				out long newDeadline));
			ClassicAssert.AreEqual(resumeAt + 10L, newDeadline);
			ClassicAssert.Greater(newDeadline, resumeAt);

			ClassicAssert.IsTrue(KingdomMasterRules.TryResumeCommittedDeadline(due, boundary,
				resumeAt, out long committedDeadline));
			if (due <= boundary) ClassicAssert.AreEqual(due, committedDeadline);
			else ClassicAssert.AreEqual(due + (resumeAt - boundary), committedDeadline);

			KingdomMasterDecision applied = KingdomMasterRules.ApplyResume(staged);
			ClassicAssert.IsTrue(applied.AutomaticWorkAllowed);
			ClassicAssert.AreEqual(applied.ResumeToken, applied.AppliedResumeToken);
			ClassicAssert.AreEqual(Scalars(applied), Scalars(KingdomMasterRules.ApplyResume(applied)),
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
			ClassicAssert.AreEqual(persistedDisabled, Scalars(stillDisabled));

			KingdomMasterDecision pending = KingdomMasterRules.Observe(stillDisabled.State,
				stillDisabled.ChangedAtTick, stillDisabled.ResumeToken,
				stillDisabled.AppliedResumeToken, true, 80L);
			ClassicAssert.IsTrue(pending.ResumePending);
			ClassicAssert.AreEqual("2|80|1|0", Scalars(pending));
			KingdomMasterDecision applied = KingdomMasterRules.ApplyResume(pending);
			ClassicAssert.AreEqual("2|80|1|1", Scalars(applied));

			KingdomMasterDecision reloaded = KingdomMasterRules.Observe(applied.State,
				applied.ChangedAtTick, applied.ResumeToken, applied.AppliedResumeToken,
				true, 81L);
			ClassicAssert.AreEqual(KingdomMasterTransition.None, reloaded.Transition);
			ClassicAssert.AreEqual(Scalars(applied), Scalars(reloaded));
			ClassicAssert.IsTrue(reloaded.AutomaticWorkAllowed);
		}

		[Test]
		public void MalformedAndOverflowEvidenceFailsClosed()
		{
			ClassicAssert.IsFalse(KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Unobserved, 1L, 0L, 0L, true, 1L).Valid);
			ClassicAssert.IsFalse(KingdomMasterRules.Observe(
				(KingdomMasterLatchValue)99, 0L, 0L, 0L, true, 1L).Valid);
			ClassicAssert.IsFalse(KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Enabled, 0L, 1L, 2L, true, 1L).Valid);
			ClassicAssert.IsFalse(KingdomMasterRules.Observe(
				KingdomMasterLatchValue.Disabled, 1L, long.MaxValue, long.MaxValue,
				true, 2L).Valid);
			ClassicAssert.IsFalse(KingdomMasterRules.TryFutureDeadline(long.MaxValue, 1L, out _));
			ClassicAssert.IsFalse(KingdomMasterRules.TryResumeCommittedDeadline(long.MaxValue,
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
				KingdomCharterAction.CivicCommitments,
				KingdomCharterAction.InspectBuildingBenefits,
				KingdomCharterAction.TrafficRecords
			};
			foreach (KingdomCharterAction action in Enum.GetValues(typeof(KingdomCharterAction)))
				ClassicAssert.AreEqual(expected.Contains(action),
					KingdomCharterMenuRules.AvailableWhileSimulationPaused(action), action.ToString());
		}
	}
}
#endif
