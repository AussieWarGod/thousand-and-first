#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The durable-key presence law, executed rather than asserted in prose.
	/// <para>
	/// The shapes that matter are the corrupt ones, and a corrupt shape cannot be reached by
	/// playing: it has to be constructed. Because the classifier is pure, every zero, empty,
	/// wrong-type, dual-table, and cross-key combination runs here without a live game, and the
	/// runtime reader is pinned to it by a separate source contract.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioStateShapeTests
	{
		private static KingdomDurableKeyObservation Absent()
		{
			return new KingdomDurableKeyObservation();
		}

		private static KingdomDurableKeyObservation Int(int Value)
		{
			return new KingdomDurableKeyObservation { HasInt = true, Int = Value };
		}

		private static KingdomDurableKeyObservation Text(string Value)
		{
			return new KingdomDurableKeyObservation { HasString = true, String = Value };
		}

		private static KingdomDurableKeyShape Shape(KingdomDurableKeyObservation Observed)
		{
			string detail;
			return KingdomScenarioStateShape.Classify(Observed, out detail);
		}

		private static KingdomScenarioTransactionShape Transaction(
			KingdomDurableKeyObservation Attempt, KingdomDurableKeyObservation Committed)
		{
			string detail;
			return KingdomScenarioStateShape.Transaction(Attempt, Committed, out detail);
		}

		// ----- raw table presence ---------------------------------------------------------------

		[Test]
		public void NoKeyUnderAnyTableIsTheOnlyAbsentShape()
		{
			Assert.AreEqual(KingdomDurableKeyShape.Absent, Shape(Absent()));
		}

		/// <summary>An explicitly stored zero or empty is PRESENT. That is the whole defect.</summary>
		[Test]
		public void AStoredZeroOrEmptyIsPresentNotAbsent()
		{
			Assert.AreEqual(KingdomDurableKeyShape.ExactInt, Shape(Int(0)));
			Assert.AreEqual(KingdomDurableKeyShape.ExactString, Shape(Text("")));
		}

		[Test]
		public void AKeyUnderAWrongDurableTableIsTorn()
		{
			Assert.AreEqual(KingdomDurableKeyShape.Torn,
				Shape(new KingdomDurableKeyObservation { HasInt64 = true }));
			Assert.AreEqual(KingdomDurableKeyShape.Torn,
				Shape(new KingdomDurableKeyObservation { HasObject = true }));
			Assert.AreEqual(KingdomDurableKeyShape.Torn,
				Shape(new KingdomDurableKeyObservation { HasBoolean = true }));
		}

		[Test]
		public void AKeyUnderTwoTablesIsTornRatherThanResolved()
		{
			Assert.AreEqual(KingdomDurableKeyShape.Torn, Shape(
				new KingdomDurableKeyObservation
				{
					HasInt = true,
					Int = 1,
					HasString = true,
					String = "1"
				}));
			Assert.AreEqual(KingdomDurableKeyShape.Torn, Shape(
				new KingdomDurableKeyObservation { HasInt = true, Int = 1, HasBoolean = true }));
		}

		[Test]
		public void AnUnobservedKeyIsTornRatherThanAbsent()
		{
			Assert.AreEqual(KingdomDurableKeyShape.Torn, Shape(null));
		}

		// ----- the transaction pair -------------------------------------------------------------

		[Test]
		public void OnlyTwoTotallyAbsentKeysAreFresh()
		{
			Assert.AreEqual(KingdomScenarioTransactionShape.None, Transaction(Absent(), Absent()));
		}

		[Test]
		public void AttemptedIsExactlyOneIntKeyOfOneWithNoCommittedKey()
		{
			Assert.AreEqual(KingdomScenarioTransactionShape.Attempted,
				Transaction(Int(1), Absent()));
		}

		[Test]
		public void CommittedIsExactlyTheTwoIntKeys()
		{
			Assert.AreEqual(KingdomScenarioTransactionShape.Committed, Transaction(Int(2), Int(1)));
		}

		/// <summary>
		/// A stored zero is the case the old reader called fresh. It is torn: something wrote that
		/// key, and a profile whose ground may have moved may never be replayed.
		/// </summary>
		[Test]
		public void AStoredZeroAttemptKeyIsTornNotFresh()
		{
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Int(0), Absent()));
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Absent(), Int(0)));
		}

		[Test]
		public void EveryOtherCrossKeyCombinationIsTorn()
		{
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Int(1), Int(1)),
				"attempted may not carry a committed key");
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Int(2), Absent()),
				"committed without its cross-check is half a commit");
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Absent(), Int(1)),
				"a committed key with no transaction key is corruption");
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Int(2), Int(2)));
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Int(3), Absent()));
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Int(-1), Absent()));
		}

		[Test]
		public void AWrongTypedTransactionKeyIsTorn()
		{
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Text("1"), Absent()));
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Text(""), Absent()));
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(Int(2), Text("1")));
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(
				new KingdomDurableKeyObservation { HasInt64 = true }, Absent()));
		}

		[Test]
		public void ADualTypedTransactionKeyIsTorn()
		{
			Assert.AreEqual(KingdomScenarioTransactionShape.Torn, Transaction(
				new KingdomDurableKeyObservation
				{
					HasInt = true,
					Int = 1,
					HasString = true,
					String = ""
				}, Absent()));
		}

		[Test]
		public void ATornTransactionNamesWhyRatherThanSayingUnknown()
		{
			string detail;
			KingdomScenarioStateShape.Transaction(Int(0), Absent(), out detail);
			Assert.IsNotNull(detail);
			Assert.IsNotEmpty(detail);
		}
	}
}
#endif
