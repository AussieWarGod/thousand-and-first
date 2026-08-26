#if TAF_TESTS
using NUnit.Framework;
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
			Assert.IsTrue(result.Valid);
			return result;
		}

		[Test]
		public void DisableAtDueWinsAndResumeStartsAWholeFutureInterval()
		{
			long start = 100L;
			long interval = KingdomRules.TicksPerDay;
			KingdomElapsedOptionDecision initialized = Observe(
				KingdomElapsedOptionRecord.Unobserved, true, 0L, start);
			Assert.AreEqual(KingdomElapsedOptionAction.AnchorEnabled, initialized.Action);
			Assert.AreEqual(KingdomElapsedOptionTransition.InitializedEnabled,
				initialized.Transition);

			Assert.AreEqual(KingdomElapsedOptionAction.Wait,
				Observe(initialized.Record, true, 0L, start).Action,
				"a retry on the transition tick ran due work");
			KingdomElapsedOptionDecision dueMinusOne = Observe(initialized.Record, true, 0L,
				start + interval - 1L);
			Assert.AreEqual(KingdomElapsedOptionAction.Run, dueMinusOne.Action);
			Assert.AreEqual(0, KingdomRules.ElapsedDays(
				start + interval - 1L - start));

			KingdomElapsedOptionDecision disabled = Observe(initialized.Record, false, 0L,
				start + interval);
			Assert.AreEqual(KingdomElapsedOptionTransition.Disabled, disabled.Transition);
			Assert.AreEqual(KingdomElapsedOptionAction.AnchorDisabled, disabled.Action,
				"disable at due allowed one last event");
			long muchLater = start + interval * 10000L;
			KingdomElapsedOptionDecision stillDisabled = Observe(disabled.Record, false, 0L,
				muchLater);
			Assert.AreEqual(KingdomElapsedOptionAction.Disabled, stillDisabled.Action);
			Assert.AreEqual(disabled.Record.ObservedTick, stillDisabled.Record.ObservedTick,
				"repeated disabled wake rewrote the transition");

			KingdomElapsedOptionDecision resumed = Observe(stillDisabled.Record, true, 0L,
				muchLater);
			Assert.AreEqual(KingdomElapsedOptionAction.AnchorEnabled, resumed.Action);
			Assert.AreEqual(KingdomElapsedOptionAction.Wait,
				Observe(resumed.Record, true, 0L, muchLater).Action);
			Assert.AreEqual(0, KingdomRules.ElapsedDays(
				(muchLater + interval - 1L) - resumed.Record.ObservedTick), "due-1");
			Assert.AreEqual(1, KingdomRules.ElapsedDays(
				(muchLater + interval) - resumed.Record.ObservedTick), "due");
			Assert.AreEqual(1, KingdomRules.ElapsedDays(
				(muchLater + interval + 1L) - resumed.Record.ObservedTick), "due+1");
		}

		[Test]
		public void RoadSubsidenceAndFaithIntervalsAllBeginStrictlyAfterResume()
		{
			long resumed = 50000L;
			long day = KingdomRules.TicksPerDay;
			Assert.AreEqual(0, KingdomRules.ElapsedDays(resumed + day - 1L - resumed),
				"road due-1");
			Assert.AreEqual(1, KingdomRules.ElapsedDays(resumed + day - resumed),
				"road due");
			Assert.AreEqual(1, KingdomRules.ElapsedDays(resumed + day + 1L - resumed),
				"road due+1");

			long slide = (long)KingdomSubsidenceRules.StepDays * day;
			Assert.AreEqual(0, KingdomRules.ElapsedDays(resumed + slide - 1L - resumed)
				/ KingdomSubsidenceRules.StepDays, "subsidence due-1");
			Assert.AreEqual(1, KingdomRules.ElapsedDays(resumed + slide - resumed)
				/ KingdomSubsidenceRules.StepDays, "subsidence due");
			Assert.AreEqual(1, KingdomRules.ElapsedDays(resumed + slide + 1L - resumed)
				/ KingdomSubsidenceRules.StepDays, "subsidence due+1");

			long pull = (long)KingdomFaithRules.ConversionPullThreshold * day;
			Assert.IsFalse(KingdomFaithRules.ConversionReady(KingdomFaithRules.PullAfterDays(0,
				KingdomRules.ElapsedDays(resumed + pull - 1L - resumed))), "faith due-1");
			Assert.IsTrue(KingdomFaithRules.ConversionReady(KingdomFaithRules.PullAfterDays(0,
				KingdomRules.ElapsedDays(resumed + pull - resumed))), "faith due");
			Assert.IsTrue(KingdomFaithRules.ConversionReady(KingdomFaithRules.PullAfterDays(0,
				KingdomRules.ElapsedDays(resumed + pull + 1L - resumed))), "faith due+1");
		}

		[Test]
		public void MasterResumePreservesModuleStateButForcesAClockAnchor()
		{
			KingdomElapsedOptionDecision initialized = Observe(
				KingdomElapsedOptionRecord.Unobserved, true, 0L, 50L);
			KingdomElapsedOptionDecision resumed = Observe(initialized.Record, true, 1L,
				500000L);
			Assert.AreEqual(KingdomElapsedOptionTransition.MasterRelatchedEnabled,
				resumed.Transition);
			Assert.AreEqual(KingdomElapsedOptionAction.AnchorEnabled, resumed.Action);
			Assert.AreEqual(500000L, resumed.Record.ObservedTick);
			Assert.AreEqual(1L, resumed.Record.MasterResumeToken);
			Assert.AreEqual(KingdomElapsedOptionAction.Wait,
				Observe(resumed.Record, true, 1L, 500000L).Action);
		}

		[Test]
		public void ModuleChangeWhileMasterWasOffOwnsPolicyBeforeRelatch()
		{
			KingdomElapsedOptionRecord prior = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 100L, 3L);
			KingdomElapsedOptionDecision decision = Observe(prior, false, 4L, 900L);
			Assert.AreEqual(KingdomElapsedOptionTransition.Disabled, decision.Transition);
			Assert.AreEqual(KingdomElapsedOptionAction.AnchorDisabled, decision.Action);
		}

		[Test]
		public void LateZoneModuleChangeOutranksRealmMasterRelatch()
		{
			KingdomElapsedOptionRecord local = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 100L, 3L);
			KingdomElapsedOptionRecord realm = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Disabled, 900L, 4L);
			Assert.AreEqual(KingdomElapsedOptionTransition.Disabled,
				KingdomElapsedOptionRules.LocalTransition(false, false, true, local, realm));
		}

		[Test]
		public void LateZoneDistinguishesMasterRelatchMigrationAndMissedModuleCycle()
		{
			KingdomElapsedOptionRecord local = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 100L, 3L);
			KingdomElapsedOptionRecord master = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 900L, 4L);
			Assert.AreEqual(KingdomElapsedOptionTransition.MasterRelatchedEnabled,
				KingdomElapsedOptionRules.LocalTransition(true, false, true, local, master));

			KingdomElapsedOptionRecord cycle = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 900L, 3L);
			Assert.AreEqual(KingdomElapsedOptionTransition.Enabled,
				KingdomElapsedOptionRules.LocalTransition(true, false, true, local, cycle));
			Assert.AreEqual(KingdomElapsedOptionTransition.InitializedEnabled,
				KingdomElapsedOptionRules.LocalTransition(true, false, false,
					KingdomElapsedOptionRecord.Unobserved, cycle));
			Assert.AreEqual(KingdomElapsedOptionTransition.Disabled,
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
			Assert.LessOrEqual(encoded.Length, KingdomElapsedOptionRules.MaxEncodedChars);
			for (int i = 0; i < 50; i++)
			{
				KingdomElapsedOptionRecord decoded;
				Assert.IsTrue(KingdomElapsedOptionRules.TryDecode(encoded, out decoded));
				Assert.AreEqual(original.State, decoded.State);
				Assert.AreEqual(original.ObservedTick, decoded.ObservedTick);
				Assert.AreEqual(original.MasterResumeToken, decoded.MasterResumeToken);
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
				Assert.IsTrue(KingdomElapsedOptionRules.TryDecode(encoded, out reloaded));
				KingdomElapsedOptionDecision wake = Observe(reloaded, false, 2L,
					100L + i * 100000L);
				Assert.AreEqual(KingdomElapsedOptionAction.Disabled, wake.Action);
				Assert.AreEqual(100L, wake.Record.ObservedTick);
				encoded = KingdomElapsedOptionRules.Encode(wake.Record);
			}
			KingdomElapsedOptionRecord last;
			Assert.IsTrue(KingdomElapsedOptionRules.TryDecode(encoded, out last));
			KingdomElapsedOptionDecision resumed = Observe(last, true, 2L, 6000000L);
			Assert.AreEqual(KingdomElapsedOptionAction.AnchorEnabled, resumed.Action);
			Assert.AreEqual(6000000L, resumed.Record.ObservedTick);
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
			Assert.IsFalse(KingdomElapsedOptionRules.TryDecode(encoded, out ignored));
		}

		[Test]
		public void ClockOrMasterRegressionCannotLicenseWork()
		{
			KingdomElapsedOptionRecord prior = new KingdomElapsedOptionRecord(
				KingdomElapsedOptionState.Enabled, 100L, 4L);
			Assert.AreEqual(KingdomElapsedOptionAction.Invalid,
				KingdomElapsedOptionRules.Observe(prior, true, 4L, 99L).Action);
			Assert.AreEqual(KingdomElapsedOptionAction.Invalid,
				KingdomElapsedOptionRules.Observe(prior, true, 3L, 100L).Action);
		}

		[Test]
		public void MasterPauseRestartsAFullPreservedShrineWindow()
		{
			long warned = 100L;
			long resumed = 100000L;
			long interval = (long)KingdomBrinkRules.CreedBrinkWindowDays
				* KingdomRules.TicksPerDay;
			Assert.AreEqual(resumed, KingdomFaithRules.EffectiveWindowStart(
				warned, resumed, resumed));
			Assert.IsFalse(KingdomBrinkRules.WindowSpent(BrinkKind.Creed, resumed,
				resumed + interval - 1L));
			Assert.IsTrue(KingdomBrinkRules.WindowSpent(BrinkKind.Creed, resumed,
				resumed + interval));
			Assert.IsTrue(KingdomBrinkRules.WindowSpent(BrinkKind.Creed, resumed,
				resumed + interval + 1L));
		}
	}
}
#endif
