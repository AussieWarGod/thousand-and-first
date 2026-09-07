#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomElapsedOptionRulesTests
	{
		private static KingdomElapsedOptionDecision Observe(
			KingdomElapsedOptionRecord prior, bool enabled, long token, long now)
		{
			KingdomElapsedOptionDecision result = KingdomElapsedOptionRules.Observe(
				prior, enabled, token, now);
			ClassicAssert.IsTrue(result.Valid);
			return result;
		}

		[Test]
		public void DisableAtDueWinsAndResumeStartsAWholeFutureInterval()
		{
			long start = 100L;
			long interval = KingdomRules.TicksPerDay;
			KingdomElapsedOptionDecision initialized = Observe(
				KingdomElapsedOptionRecord.Unobserved, true, 0L, start);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.AnchorEnabled, initialized.Action);
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.InitializedEnabled,
				initialized.Transition);

			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Wait,
				Observe(initialized.Record, true, 0L, start).Action,
				"a retry on the transition tick ran due work");
			KingdomElapsedOptionDecision dueMinusOne = Observe(initialized.Record, true, 0L,
				start + interval - 1L);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Run, dueMinusOne.Action);
			ClassicAssert.AreEqual(0, KingdomRules.ElapsedDays(
				start + interval - 1L - start));

			KingdomElapsedOptionDecision disabled = Observe(initialized.Record, false, 0L,
				start + interval);
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.Disabled, disabled.Transition);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.AnchorDisabled, disabled.Action,
				"disable at due allowed one last event");
			long muchLater = start + interval * 10000L;
			KingdomElapsedOptionDecision stillDisabled = Observe(disabled.Record, false, 0L,
				muchLater);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Disabled, stillDisabled.Action);
			ClassicAssert.AreEqual(disabled.Record.ObservedTick, stillDisabled.Record.ObservedTick,
				"repeated disabled wake rewrote the transition");

			KingdomElapsedOptionDecision resumed = Observe(stillDisabled.Record, true, 0L,
				muchLater);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.AnchorEnabled, resumed.Action);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Wait,
				Observe(resumed.Record, true, 0L, muchLater).Action);
			ClassicAssert.AreEqual(0, KingdomRules.ElapsedDays(
				(muchLater + interval - 1L) - resumed.Record.ObservedTick), "due-1");
			ClassicAssert.AreEqual(1, KingdomRules.ElapsedDays(
				(muchLater + interval) - resumed.Record.ObservedTick), "due");
			ClassicAssert.AreEqual(1, KingdomRules.ElapsedDays(
				(muchLater + interval + 1L) - resumed.Record.ObservedTick), "due+1");
		}

		[Test]
		public void RoadSubsidenceAndFaithIntervalsAllBeginStrictlyAfterResume()
		{
			long resumed = 50000L;
			long day = KingdomRules.TicksPerDay;
			ClassicAssert.AreEqual(0, KingdomRules.ElapsedDays(resumed + day - 1L - resumed),
				"road due-1");
			ClassicAssert.AreEqual(1, KingdomRules.ElapsedDays(resumed + day - resumed),
				"road due");
			ClassicAssert.AreEqual(1, KingdomRules.ElapsedDays(resumed + day + 1L - resumed),
				"road due+1");

			long slide = (long)KingdomSubsidenceRules.StepDays * day;
			ClassicAssert.AreEqual(0, KingdomRules.ElapsedDays(resumed + slide - 1L - resumed)
				/ KingdomSubsidenceRules.StepDays, "subsidence due-1");
			ClassicAssert.AreEqual(1, KingdomRules.ElapsedDays(resumed + slide - resumed)
				/ KingdomSubsidenceRules.StepDays, "subsidence due");
			ClassicAssert.AreEqual(1, KingdomRules.ElapsedDays(resumed + slide + 1L - resumed)
				/ KingdomSubsidenceRules.StepDays, "subsidence due+1");

			long pull = (long)KingdomFaithRules.ConversionPullThreshold * day;
			ClassicAssert.IsFalse(KingdomFaithRules.ConversionReady(KingdomFaithRules.PullAfterDays(0,
				KingdomRules.ElapsedDays(resumed + pull - 1L - resumed))), "faith due-1");
			ClassicAssert.IsTrue(KingdomFaithRules.ConversionReady(KingdomFaithRules.PullAfterDays(0,
				KingdomRules.ElapsedDays(resumed + pull - resumed))), "faith due");
			ClassicAssert.IsTrue(KingdomFaithRules.ConversionReady(KingdomFaithRules.PullAfterDays(0,
				KingdomRules.ElapsedDays(resumed + pull + 1L - resumed))), "faith due+1");
		}

		[Test]
		public void MasterResumePreservesModuleStateButForcesAClockAnchor()
		{
			KingdomElapsedOptionDecision initialized = Observe(
				KingdomElapsedOptionRecord.Unobserved, true, 0L, 50L);
			KingdomElapsedOptionDecision resumed = Observe(initialized.Record, true, 1L,
				500000L);
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.MasterRelatchedEnabled,
				resumed.Transition);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.AnchorEnabled, resumed.Action);
			ClassicAssert.AreEqual(500000L, resumed.Record.ObservedTick);
			ClassicAssert.AreEqual(1L, resumed.Record.MasterResumeToken);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Wait,
				Observe(resumed.Record, true, 1L, 500000L).Action);
		}

		[Test]
		public void ModuleChangeWhileMasterWasOffOwnsPolicyBeforeRelatch()
		{
			KingdomElapsedOptionRecord prior = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 100L, 3L);
			KingdomElapsedOptionDecision decision = Observe(prior, false, 4L, 900L);
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.Disabled, decision.Transition);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.AnchorDisabled, decision.Action);
		}

		[Test]
		public void LateZoneModuleChangeOutranksRealmMasterRelatch()
		{
			KingdomElapsedOptionRecord local = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 100L, 3L);
			KingdomElapsedOptionRecord realm = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Disabled, 900L, 4L);
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.Disabled,
				KingdomElapsedOptionRules.LocalTransition(false, false, true, local, realm));
		}

		[Test]
		public void LateZoneDistinguishesMasterRelatchMigrationAndMissedModuleCycle()
		{
			KingdomElapsedOptionRecord local = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 100L, 3L);
			KingdomElapsedOptionRecord master = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 900L, 4L);
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.MasterRelatchedEnabled,
				KingdomElapsedOptionRules.LocalTransition(true, false, true, local, master));

			KingdomElapsedOptionRecord cycle = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 900L, 3L);
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.Enabled,
				KingdomElapsedOptionRules.LocalTransition(true, false, true, local, cycle));
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.InitializedEnabled,
				KingdomElapsedOptionRules.LocalTransition(true, false, false,
					KingdomElapsedOptionRecord.Unobserved, cycle));
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.Disabled,
				KingdomElapsedOptionRules.LocalTransition(false, true, false,
					KingdomElapsedOptionRecord.Unobserved,
					new KingdomElapsedOptionRecord(KingdomElapsedOptionState.Disabled,
						900L, 3L)));
		}

		[Test]
		public void CanonicalWireRoundTripsAcrossRepeatedReloads()
		{
			KingdomElapsedOptionRecord original = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, long.MaxValue, long.MaxValue);
			string encoded = KingdomElapsedOptionRules.Encode(original);
			ClassicAssert.LessOrEqual(encoded.Length, KingdomElapsedOptionRules.MaxEncodedChars);
			for (int i = 0; i < 50; i++)
			{
				KingdomElapsedOptionRecord decoded;
				ClassicAssert.IsTrue(KingdomElapsedOptionRules.TryDecode(encoded, out decoded));
				ClassicAssert.AreEqual(original.State, decoded.State);
				ClassicAssert.AreEqual(original.ObservedTick, decoded.ObservedTick);
				ClassicAssert.AreEqual(original.MasterResumeToken, decoded.MasterResumeToken);
				encoded = KingdomElapsedOptionRules.Encode(decoded);
			}
		}

		[Test]
		public void RepeatedDisabledReloadCannotMoveEpochOrBankAReward()
		{
			KingdomElapsedOptionDecision disabled = Observe(
				KingdomElapsedOptionRecord.Unobserved, false, 2L, 100L);
			string encoded = KingdomElapsedOptionRules.Encode(disabled.Record);
			for (int i = 1; i <= 50; i++)
			{
				KingdomElapsedOptionRecord reloaded;
				ClassicAssert.IsTrue(KingdomElapsedOptionRules.TryDecode(encoded, out reloaded));
				KingdomElapsedOptionDecision wake = Observe(reloaded, false, 2L,
					100L + i * 100000L);
				ClassicAssert.AreEqual(KingdomElapsedOptionAction.Disabled, wake.Action);
				ClassicAssert.AreEqual(100L, wake.Record.ObservedTick);
				encoded = KingdomElapsedOptionRules.Encode(wake.Record);
			}
			KingdomElapsedOptionRecord last;
			ClassicAssert.IsTrue(KingdomElapsedOptionRules.TryDecode(encoded, out last));
			KingdomElapsedOptionDecision resumed = Observe(last, true, 2L, 6000000L);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.AnchorEnabled, resumed.Action);
			ClassicAssert.AreEqual(6000000L, resumed.Record.ObservedTick);
		}

		[TestCase("v1|E|01|0")]
		[TestCase("v1|E|-1|0")]
		[TestCase("v1|E|0|-1")]
		[TestCase("v2|E|0|0")]
		[TestCase("v1|X|0|0")]
		[TestCase("v1|E|0|0|0")]
		public void NonCanonicalOrUnknownWireIsRefused(string encoded)
		{
			KingdomElapsedOptionRecord ignored;
			ClassicAssert.IsFalse(KingdomElapsedOptionRules.TryDecode(encoded, out ignored));
		}

		[Test]
		public void ClockOrMasterRegressionCannotLicenseWork()
		{
			KingdomElapsedOptionRecord prior = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 100L, 4L);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Invalid,
				KingdomElapsedOptionRules.Observe(prior, true, 4L, 99L).Action);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Invalid,
				KingdomElapsedOptionRules.Observe(prior, true, 3L, 100L).Action);
		}

		[Test]
		public void MasterPauseRestartsAFullPreservedShrineWindow()
		{
			long warned = 100L;
			long resumed = 100000L;
			long interval = (long)KingdomBrinkRules.CreedBrinkWindowDays
				* KingdomRules.TicksPerDay;
			ClassicAssert.AreEqual(resumed, KingdomFaithRules.EffectiveWindowStart(
				warned, resumed, resumed));
			ClassicAssert.IsFalse(KingdomBrinkRules.WindowSpent(BrinkKind.Creed, resumed,
				resumed + interval - 1L));
			ClassicAssert.IsTrue(KingdomBrinkRules.WindowSpent(BrinkKind.Creed, resumed,
				resumed + interval));
			ClassicAssert.IsTrue(KingdomBrinkRules.WindowSpent(BrinkKind.Creed, resumed,
				resumed + interval + 1L));
		}
	}
}
#endif
