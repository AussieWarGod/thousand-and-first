#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFoundingHeartRecordedRemovalTests
	{
		[TestCase(0, false)]
		[TestCase(1, false)]
		[TestCase(2, false)]
		[TestCase(3, false)]
		[TestCase(4, true)]
		[TestCase(5, true)]
		[TestCase(6, true)]
		[TestCase(7, false)]
		[TestCase(255, false)]
		public void EveryPhaseRequiresBothWitnessesAndOnlyAbsentTombstones(
			int phase, bool eligiblePhase)
		{
			KingdomFoundingHeartTerminalPlan plan = Plan(phase);
			foreach (bool final in new[] { false, true })
			foreach (bool absent in new[] { false, true })
			foreach (KingdomPhysicalLookupState tombstone in LookupStates())
				Decision(plan, final, absent, tombstone,
					eligiblePhase && final && absent
						&& tombstone == KingdomPhysicalLookupState.Absent);
		}

		[TestCase(4)]
		[TestCase(5)]
		[TestCase(6)]
		public void SinkShapesMustRemainValidForTheirRecordedPhase(int phase)
		{
			foreach (int raising in new[] { 0, 1, 2, 3, 4, 255 })
			foreach (int heart in new[] { 0, 1, 2, 3, 4, 255 })
			{
				KingdomFoundingHeartTerminalPlan plan = Plan(phase);
				plan.Raising = (KingdomFoundingHeartSinkDisposition)raising;
				plan.Heart = (KingdomFoundingHeartSinkDisposition)heart;
				bool expected = phase == 4 ? raising == 0 && heart == 0
					: phase == 5 ? raising <= 3 && heart <= 3
					: (raising == 2 || raising == 3) && (heart == 2 || heart == 3);
				Decision(plan, true, true, KingdomPhysicalLookupState.Absent, expected);
			}
		}

		[TestCase("TransactionId")]
		[TestCase("CompletionSeal")]
		[TestCase("ZoneId")]
		[TestCase("PredecessorId")]
		[TestCase("FinalId")]
		[TestCase("Blueprint")]
		[TestCase("BuildKey")]
		[TestCase("PlotId")]
		public void MissingEmptyOrOverBoundBindingsCannotAuthorizeRemoval(string field)
		{
			foreach (string malformed in new[] { null, "", new string('x', 1025) })
			{
				KingdomFoundingHeartTerminalPlan plan = Plan(4);
				typeof(KingdomFoundingHeartTerminalPlan).GetField(field).SetValue(plan, malformed);
				Decision(plan, true, true, KingdomPhysicalLookupState.Absent, false);
			}
		}

		[TestCase("0123456789ABCDEF0123456789ABCDEF")]
		[TestCase("0123456789abcdef0123456789abcdeg")]
		[TestCase("0123456789abcdef0123456789abcde")]
		[TestCase("0123456789abcdef0123456789abcdef0")]
		public void TransactionMustRetainCanonicalLowerHexIdentity(string transaction)
		{
			KingdomFoundingHeartTerminalPlan plan = Plan(4);
			plan.TransactionId = transaction;
			Decision(plan, true, true, KingdomPhysicalLookupState.Absent, false);
		}

		[TestCase(-1, 12)]
		[TestCase(4097, 12)]
		[TestCase(40, -1)]
		[TestCase(40, 4097)]
		public void OutOfDomainFrozenCoordinatesRefuseWithoutNormalization(int x, int y)
		{
			KingdomFoundingHeartTerminalPlan plan = Plan(4);
			plan.X = x;
			plan.Y = y;
			Decision(plan, true, true, KingdomPhysicalLookupState.Absent, false);
		}

		[Test]
		public void NullAndDefaultPlansAlwaysRefuse()
		{
			foreach (bool final in new[] { false, true })
			foreach (bool absent in new[] { false, true })
			foreach (KingdomPhysicalLookupState tombstone in LookupStates())
			{
				Decision(null, final, absent, tombstone, false);
				Decision(new KingdomFoundingHeartTerminalPlan(), final, absent, tombstone, false);
			}
		}

		[TestCase(4)]
		[TestCase(5)]
		[TestCase(6)]
		public void CanonicalRoundtripRetainsRecordedRemovalAndExactWire(int phase)
		{
			KingdomFoundingHeartTerminalPlan plan = Plan(phase);
			string wire = KingdomFoundingHeartTerminalRules.Encode(plan);
			ClassicAssert.IsNotNull(wire);
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryDecode(wire, out var loaded));
			Decision(loaded, true, true, KingdomPhysicalLookupState.Absent, true);
			ClassicAssert.AreEqual(wire, KingdomFoundingHeartTerminalRules.Encode(loaded));
		}

		private static KingdomFoundingHeartTerminalPlan Plan(int phase)
		{
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryCreate(
				"0123456789abcdef0123456789abcdef", "hs1-" + new string('a', 64),
				"JoppaWorld.2.2.1.1.10", "predecessor", "final", "r_KingdomFirstBasin",
				"heartbasin", "plot", 40, 12, out var plan));
			plan.Phase = (KingdomFoundingHeartTerminalPhase)phase;
			if (phase == 6)
			{
				plan.Raising = KingdomFoundingHeartSinkDisposition.Settled;
				plan.Heart = KingdomFoundingHeartSinkDisposition.Lost;
			}
			return plan;
		}

		private static KingdomPhysicalLookupState[] LookupStates()
		{
			return new[] { KingdomPhysicalLookupState.Absent, KingdomPhysicalLookupState.Exact,
				KingdomPhysicalLookupState.Ambiguous, (KingdomPhysicalLookupState)3,
				(KingdomPhysicalLookupState)255 };
		}

		private static void Decision(KingdomFoundingHeartTerminalPlan plan, bool final,
			bool absent, KingdomPhysicalLookupState tombstone, bool expected)
		{
			object[] before = Snapshot(plan);
			ClassicAssert.AreEqual(expected, KingdomFoundingHeartTerminalRules.CanUseRecordedRemoval(
				plan, final, absent, tombstone));
			if (plan != null) CollectionAssert.AreEqual(before, Snapshot(plan),
				"Recorded-removal observation must not change any plan field.");
		}

		private static object[] Snapshot(KingdomFoundingHeartTerminalPlan plan)
		{
			return plan == null ? null : new object[] { plan.TransactionId, plan.CompletionSeal,
				plan.ZoneId, plan.PredecessorId, plan.FinalId, plan.Blueprint, plan.BuildKey,
				plan.PlotId, plan.X, plan.Y, plan.Phase, plan.Raising, plan.Heart };
		}
	}
}
#endif
