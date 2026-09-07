#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.AreEqual(KingdomScenarioStampShape.Absent, Stamp(Absent(), Absent()));
		}

		[Test]
		public void ExactStringProvenanceWithTheExactMarkerIsReadable()
		{
			ClassicAssert.AreEqual(KingdomScenarioStampShape.Readable, Stamp(Text("sc1|k"), Int(1)));
		}

		/// <summary>
		/// An empty stamp is the laundering case: it reads as absent under a default getter and
		/// would let a scenario-built save found ordinary-play anchor evidence.
		/// </summary>
		[Test]
		public void AnExplicitlyEmptyStampIsUnreadableNotAbsent()
		{
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Text(""), Int(1)));
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Text(""), Absent()));
		}

		[Test]
		public void EitherHalfAloneIsUnreadable()
		{
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), Absent()));
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Absent(), Int(1)));
		}

		[Test]
		public void AZeroOrWrongTypedMarkerIsUnreadable()
		{
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), Int(0)));
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), Int(2)));
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), Text("1")));
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable,
				Stamp(Text("sc1|k"), new KingdomDurableKeyObservation { HasBoolean = true }));
		}

		[Test]
		public void ProvenanceUnderTheIntTableIsUnreadable()
		{
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Int(0), Int(1)));
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Int(1), Absent()));
		}

		[Test]
		public void ADualTypedStampPairIsUnreadable()
		{
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(
				new KingdomDurableKeyObservation
				{
					HasString = true,
					String = "sc1|k",
					HasInt = true,
					Int = 1
				}, Int(1)));
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(Text("sc1|k"),
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
			ClassicAssert.AreEqual(KingdomScenarioStampShape.PresentUnreadable, Stamp(null, null));
		}

		// ----- other harness-owned authority keys -----------------------------------------------

		[Test]
		public void AnAbsentAuthorityTextKeyIsOrdinary()
		{
			string value;
			bool present;
			string detail;
			ClassicAssert.IsTrue(KingdomScenarioStateShape.TryAuthorityText(Absent(), out value,
				out present, out detail));
			ClassicAssert.IsFalse(present);
			ClassicAssert.IsNull(value);
			ClassicAssert.IsNull(detail);
		}

		[Test]
		public void AnExactNonEmptyAuthorityTextKeyIsReadable()
		{
			string value;
			bool present;
			string detail;
			ClassicAssert.IsTrue(KingdomScenarioStateShape.TryAuthorityText(Text("arch;facing=north"),
				out value, out present, out detail));
			ClassicAssert.IsTrue(present);
			ClassicAssert.AreEqual("arch;facing=north", value);
		}

		[Test]
		public void AnEmptyWrongTypedOrDualAuthorityTextKeyRefuses()
		{
			string value;
			bool present;
			string detail;
			ClassicAssert.IsFalse(KingdomScenarioStateShape.TryAuthorityText(Text(""), out value,
				out present, out detail), "an explicitly stored empty string is not absence");
			ClassicAssert.IsTrue(present);
			ClassicAssert.IsNotNull(detail);
			ClassicAssert.IsFalse(KingdomScenarioStateShape.TryAuthorityText(Int(0), out value,
				out present, out detail));
			ClassicAssert.IsFalse(KingdomScenarioStateShape.TryAuthorityText(
				new KingdomDurableKeyObservation
				{
					HasString = true,
					String = "x",
					HasInt64 = true
				}, out value, out present, out detail));
			ClassicAssert.IsFalse(KingdomScenarioStateShape.TryAuthorityText(null, out value,
				out present, out detail));
		}
	}
}
#endif
