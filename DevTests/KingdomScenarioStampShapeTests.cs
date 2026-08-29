#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The provenance half of the durable-key presence law: scenario stamp, presence marker, and
	/// every other harness-owned authority key that carries text.
	/// <para>
	/// Split from the transaction half only to hold the house line cap. The law is one law: absence
	/// under every durable type table is the only ordinary state, and every other present shape
	/// refuses rather than resolving in some direction.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioStampShapeTests
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

		private static KingdomScenarioStampShape Stamp(KingdomDurableKeyObservation Provenance,
			KingdomDurableKeyObservation Marker)
		{
			string detail;
			return KingdomScenarioStateShape.Stamp(Provenance, Marker, out detail);
		}


		[Test]
		public void OnlyTotalAbsenceIsOrdinaryPlay()
		{
			Assert.AreEqual(KingdomScenarioStampShape.Absent, Stamp(Absent(), Absent()));
		}

		[Test]
		public void ExactStringProvenanceWithTheExactMarkerIsReadable()
		{
			Assert.AreEqual(KingdomScenarioStampShape.Readable, Stamp(Text("sc1|k"), Int(1)));
		}

		/// <summary>
		/// An empty stamp is the laundering case: it reads as absent under a default getter and
		/// would let a scenario-built save found ordinary-play anchor evidence.
		/// </summary>
		[Test]
		public void AnExplicitlyEmptyStampIsUnreadableNotAbsent()
		{
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Text(""), Int(1)));
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Text(""), Absent()));
		}

		[Test]
		public void EitherHalfAloneIsUnreadable()
		{
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), Absent()));
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Absent(), Int(1)));
		}

		[Test]
		public void AZeroOrWrongTypedMarkerIsUnreadable()
		{
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), Int(0)));
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), Int(2)));
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), Text("1")));
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), new KingdomDurableKeyObservation { HasBoolean = true }));
		}

		[Test]
		public void ProvenanceUnderTheIntTableIsUnreadable()
		{
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Int(0), Int(1)));
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Int(1), Absent()));
		}

		[Test]
		public void ADualTypedStampPairIsUnreadable()
		{
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(
				new KingdomDurableKeyObservation
				{
					HasString = true,
					String = "sc1|k",
					HasInt = true,
					Int = 1
				}, Int(1)));
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Text("sc1|k"),
				new KingdomDurableKeyObservation
				{
					HasInt = true,
					Int = 1,
					HasObject = true
				}));
		}

		[Test]
		public void AnUnobservedStampPairIsUnreadable()
		{
			Assert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(null, null));
		}

		// ----- other harness-owned authority keys -----------------------------------------------

		[Test]
		public void AnAbsentAuthorityTextKeyIsOrdinary()
		{
			string value;
			bool present;
			string detail;
			Assert.IsTrue(KingdomScenarioStateShape.TryAuthorityText(Absent(), out value,
				out present, out detail));
			Assert.IsFalse(present);
			Assert.IsNull(value);
			Assert.IsNull(detail);
		}

		[Test]
		public void AnExactNonEmptyAuthorityTextKeyIsReadable()
		{
			string value;
			bool present;
			string detail;
			Assert.IsTrue(KingdomScenarioStateShape.TryAuthorityText(Text("arch;facing=north"),
				out value, out present, out detail));
			Assert.IsTrue(present);
			Assert.AreEqual("arch;facing=north", value);
		}

		[Test]
		public void AnEmptyWrongTypedOrDualAuthorityTextKeyRefuses()
		{
			string value;
			bool present;
			string detail;
			Assert.IsFalse(KingdomScenarioStateShape.TryAuthorityText(Text(""), out value,
				out present, out detail), "an explicitly stored empty string is not absence");
			Assert.IsTrue(present);
			Assert.IsNotNull(detail);
			Assert.IsFalse(KingdomScenarioStateShape.TryAuthorityText(Int(0), out value,
				out present, out detail));
			Assert.IsFalse(KingdomScenarioStateShape.TryAuthorityText(
				new KingdomDurableKeyObservation
				{
					HasString = true,
					String = "x",
					HasInt64 = true
				}, out value, out present, out detail));
			Assert.IsFalse(KingdomScenarioStateShape.TryAuthorityText(null, out value,
				out present, out detail));
		}
	}
}
#endif
