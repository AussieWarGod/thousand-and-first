#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public class KingdomSubsidenceOptionRulesTests
	{
		private const string Prior = "v1|E|100|2";
		private const string Target = "v1|D|101|2";

		private static KingdomDurableKeyObservation Reading(int mask, string wire = Prior)
		{
			return new KingdomDurableKeyObservation
			{
				HasString = (mask & 1) != 0, String = wire,
				HasInt = (mask & 2) != 0, Int = 0,
				HasInt64 = (mask & 4) != 0, HasObject = (mask & 8) != 0,
				HasBoolean = (mask & 16) != 0
			};
		}

		private static KingdomSubsidenceOptionRules.Snapshot DisableSnapshot()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.Observe(Reading(1), false, 2, 101,
				out KingdomSubsidenceOptionRules.Snapshot snapshot).Valid);
			return snapshot;
		}

		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		[TestCase(6)]
		[TestCase(7)]
		[TestCase(8)]
		[TestCase(9)]
		[TestCase(10)]
		[TestCase(11)]
		[TestCase(12)]
		[TestCase(13)]
		[TestCase(14)]
		[TestCase(15)]
		[TestCase(16)]
		[TestCase(17)]
		[TestCase(18)]
		[TestCase(19)]
		[TestCase(20)]
		[TestCase(21)]
		[TestCase(22)]
		[TestCase(23)]
		[TestCase(24)]
		[TestCase(25)]
		[TestCase(26)]
		[TestCase(27)]
		[TestCase(28)]
		[TestCase(29)]
		[TestCase(30)]
		[TestCase(31)]
		public void EveryWrongOrMultipleTableCombinationRefusesReadAndPublication(int mask)
		{
			KingdomDurableKeyObservation observed = Reading(mask);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.TryRead(observed, out KingdomElapsedOptionRecord _,
				out bool present));
			ClassicAssert.IsTrue(present);
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(observed,
				true, 2, 101, out KingdomSubsidenceOptionRules.Snapshot rejected);
			ClassicAssert.IsFalse(decision.Valid);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Invalid, decision.Action);
			ClassicAssert.IsNull(rejected);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(DisableSnapshot(), observed, out string wire));
			ClassicAssert.IsNull(wire);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(DisableSnapshot(), Reading(mask, Target)));
			ClassicAssert.AreEqual(Prior, observed.String);
			ClassicAssert.AreEqual(mask, (observed.HasString ? 1 : 0) | (observed.HasInt ? 2 : 0)
				| (observed.HasInt64 ? 4 : 0) | (observed.HasObject ? 8 : 0) | (observed.HasBoolean ? 16 : 0));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("broken")]
		[TestCase("v2|E|100|2")]
		[TestCase("v1|E|0100|2")]
		[TestCase("v1|E|100|+2")]
		[TestCase("v1|E|-100|2")]
		[TestCase("v1|X|100|2")]
		public void PresentMalformedTextNeverBecomesAbsentOrFresh(string wire)
		{
			KingdomDurableKeyObservation observed = Reading(1, wire);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.TryRead(observed, out KingdomElapsedOptionRecord _,
				out bool present));
			ClassicAssert.IsTrue(present);
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(observed,
				true, 2, 101, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			ClassicAssert.IsFalse(decision.Valid);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Invalid, decision.Action);
			ClassicAssert.IsNull(snapshot);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(DisableSnapshot(), observed, out string _));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(DisableSnapshot(), observed));
			ClassicAssert.AreEqual(wire, observed.String);
		}

		[Test]
		public void OnlyFiveTableAbsenceInitializesAndSameTickPublicationCannotRun()
		{
			KingdomDurableKeyObservation absent = Reading(0);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.TryRead(absent, out KingdomElapsedOptionRecord prior,
				out bool present));
			ClassicAssert.IsFalse(present);
			ClassicAssert.AreEqual(KingdomElapsedOptionState.Unobserved, prior.State);
			KingdomElapsedOptionDecision first = KingdomSubsidenceOptionRules.Observe(absent,
				true, 2, 100, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.AnchorEnabled, first.Action);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.CanPublish(snapshot, absent, out string wire));
			ClassicAssert.AreEqual(Prior, wire);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, Reading(1, wire)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(snapshot, Reading(1, wire), out string _));
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Wait,
				KingdomSubsidenceOptionRules.Observe(Reading(1, wire), true, 2, 100, out snapshot).Action);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Run,
				KingdomSubsidenceOptionRules.Observe(Reading(1, wire), true, 2, 101, out snapshot).Action);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ExactStoredRecordUsesExistingCodecWithoutReset(bool enabled)
		{
			KingdomElapsedOptionRecord record = new KingdomElapsedOptionRecord(enabled
				? KingdomElapsedOptionState.Enabled : KingdomElapsedOptionState.Disabled, 100, 2);
			string wire = KingdomElapsedOptionRules.Encode(record);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.TryRead(Reading(1, wire), out KingdomElapsedOptionRecord prior,
				out bool present));
			ClassicAssert.IsTrue(present);
			ClassicAssert.AreEqual(wire, KingdomElapsedOptionRules.Encode(prior));
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(Reading(1, wire),
				enabled, 2, 101, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			ClassicAssert.AreEqual(enabled ? KingdomElapsedOptionAction.Run : KingdomElapsedOptionAction.Disabled, decision.Action);
			ClassicAssert.AreEqual(100, decision.Record.ObservedTick);
			ClassicAssert.AreEqual(wire, snapshot.PriorWire);
			ClassicAssert.AreEqual(wire, snapshot.NextWire);
		}

		[TestCase(2L, 99L)]
		[TestCase(1L, 101L)]
		[TestCase(-1L, 101L)]
		[TestCase(2L, -1L)]
		public void TokenOrTickRegressionRefusesWithoutResettingDecodedPrior(long token, long now)
		{
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(Reading(1),
				true, token, now, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			ClassicAssert.IsFalse(decision.Valid);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Invalid, decision.Action);
			ClassicAssert.AreEqual(Prior, KingdomElapsedOptionRules.Encode(decision.Record));
			ClassicAssert.IsNull(snapshot);
		}

		[Test]
		public void ModuleTogglesAndMasterRelatchPublishBeforeRepeatsCanRun()
		{
			KingdomSubsidenceOptionRules.Snapshot snapshot = DisableSnapshot();
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.Disabled, snapshot.Decision.Transition);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.CanPublish(snapshot, Reading(1), out string disabled));
			ClassicAssert.AreEqual(Target, disabled);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Disabled,
				KingdomSubsidenceOptionRules.Observe(Reading(1, disabled), false, 2, 101, out snapshot).Action);
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.Enabled,
				KingdomSubsidenceOptionRules.Observe(Reading(1, disabled), true, 2, 102, out snapshot).Transition);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.CanPublish(snapshot, Reading(1, disabled), out string enabled));
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Wait,
				KingdomSubsidenceOptionRules.Observe(Reading(1, enabled), true, 2, 102, out snapshot).Action);
			ClassicAssert.AreEqual(KingdomElapsedOptionTransition.MasterRelatchedEnabled,
				KingdomSubsidenceOptionRules.Observe(Reading(1, enabled), true, 3, 103, out snapshot).Transition);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.CanPublish(snapshot, Reading(1, enabled), out string relatched));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, Reading(1, relatched)));
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Wait,
				KingdomSubsidenceOptionRules.Observe(Reading(1, relatched), true, 3, 103, out snapshot).Action);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Run,
				KingdomSubsidenceOptionRules.Observe(Reading(1, relatched), true, 3, 104, out snapshot).Action);
		}

		[Test]
		public void FrozenSnapshotRequiresExactPriorAndExactPublishedBytes()
		{
			KingdomDurableKeyObservation mutable = Reading(1);
			KingdomSubsidenceOptionRules.Observe(mutable, false, 2, 101,
				out KingdomSubsidenceOptionRules.Snapshot snapshot);
			mutable.String = Target;
			ClassicAssert.AreEqual(Prior, snapshot.PriorWire);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(snapshot, mutable, out string refused));
			ClassicAssert.IsNull(refused);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(snapshot, Reading(0), out refused));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.CanPublish(snapshot, Reading(1), out string wire));
			ClassicAssert.AreEqual(Target, wire);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, Reading(1)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, Reading(0, Target)));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, mutable));
			mutable.HasBoolean = true;
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, mutable));
		}

		[Test]
		public void MissingObservationSnapshotAndOversizedWireCannotAuthorizeAnything()
		{
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.Observe(null, true, 0, 0,
				out KingdomSubsidenceOptionRules.Snapshot snapshot).Valid);
			ClassicAssert.IsNull(snapshot);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(null, Reading(0), out string _));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(DisableSnapshot(), null, out string _));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(null, Reading(1, Target)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(DisableSnapshot(), null));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionRules.Observe(
				Reading(1, new string('x', KingdomElapsedOptionRules.MaxEncodedChars + 1)),
				true, 2, 101, out snapshot).Valid);
			ClassicAssert.IsNull(snapshot);
		}
	}
}
#endif
