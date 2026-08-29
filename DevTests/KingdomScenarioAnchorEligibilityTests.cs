#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Whether a save may found ORDINARY-PLAY anchor evidence.
	/// <para>
	/// Absence of a stamp is not innocence. A scenario profile whose stamp was deleted, torn, or
	/// never published still carries the transaction marker and the request key it was prepared
	/// with, and either one means the world was arranged rather than played. The cross-product below
	/// is the whole point: every combination of a missing stamp with surviving scenario authority
	/// must refuse.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioAnchorEligibilityTests
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

		private static bool Eligible(KingdomDurableKeyObservation provenance,
			KingdomDurableKeyObservation marker, KingdomDurableKeyObservation attempt,
			KingdomDurableKeyObservation committed, KingdomDurableKeyObservation request,
			out string refusal)
		{
			return KingdomScenarioStateShape.OrdinaryAnchorEligible(provenance, marker, attempt,
				committed, request, out refusal);
		}

		private static bool Eligible(KingdomDurableKeyObservation attempt,
			KingdomDurableKeyObservation committed, KingdomDurableKeyObservation request)
		{
			string refusal;
			return Eligible(Absent(), Absent(), attempt, committed, request, out refusal);
		}

		[Test]
		public void ATotallyCleanGameIsEligible()
		{
			string refusal;
			Assert.IsTrue(Eligible(Absent(), Absent(), Absent(), Absent(), Absent(), out refusal));
			Assert.IsNull(refusal);
		}

		// ----- a stamp of any kind refuses --------------------------------------------------------

		[Test]
		public void AStampedGameIsNotEligible()
		{
			string refusal;
			Assert.IsFalse(Eligible(Text("sc1|k"), Int(1), Absent(), Absent(), Absent(),
				out refusal));
			StringAssert.Contains("scenario stamp", refusal);
		}

		[Test]
		public void ATornStampIsNotEligible()
		{
			string refusal;
			Assert.IsFalse(Eligible(Text(""), Int(1), Absent(), Absent(), Absent(), out refusal));
			StringAssert.Contains("unreadable", refusal);
			Assert.IsFalse(Eligible(Absent(), Int(1), Absent(), Absent(), Absent(), out refusal));
			Assert.IsFalse(Eligible(Text("sc1|k"), Absent(), Absent(), Absent(), Absent(),
				out refusal));
		}

		// ----- the cross-product: absent stamp, surviving scenario authority ----------------------

		/// <summary>
		/// The laundering case. Deleting or tearing the stamp leaves a profile that ordinary play
		/// never produced, and the transaction marker is what still says so.
		/// </summary>
		[TestCase(1, 0, TestName = "attempted transaction with no stamp")]
		[TestCase(2, 1, TestName = "committed transaction with no stamp")]
		[TestCase(0, 0, TestName = "torn zero transaction with no stamp")]
		[TestCase(3, 0, TestName = "torn unknown transaction with no stamp")]
		public void ASurvivingTransactionMarkerRefusesEvenWithNoStamp(int attempt, int committed)
		{
			string refusal;
			KingdomDurableKeyObservation second = committed == 0 ? Absent() : Int(committed);
			Assert.IsFalse(Eligible(Absent(), Absent(), Int(attempt), second, Absent(),
				out refusal));
			StringAssert.Contains("transaction marker", refusal);
		}

		[Test]
		public void ATornCrossCheckMarkerRefusesEvenWithNoStamp()
		{
			Assert.IsFalse(Eligible(Absent(), Int(1), Absent()),
				"a committed cross-check with no transaction key is corruption");
			Assert.IsFalse(Eligible(Int(2), Absent(), Absent()));
			Assert.IsFalse(Eligible(Text("1"), Absent(), Absent()),
				"a wrong-typed transaction key is torn, not absent");
		}

		/// <summary>
		/// A prepared scenario profile carries its request key whether or not the gate ever ran, so
		/// the key alone disqualifies the save.
		/// </summary>
		[Test]
		public void ASurvivingRequestKeyRefusesEvenWithNoStamp()
		{
			string refusal;
			Assert.IsFalse(Eligible(Absent(), Absent(), Absent(), Absent(),
				Text("arch-gallery-slice;facing=north;seed=#0"), out refusal));
			StringAssert.Contains("request key", refusal);
		}

		[Test]
		public void AnEmptyWrongTypedOrDualRequestKeyRefuses()
		{
			Assert.IsFalse(Eligible(Absent(), Absent(), Text("")),
				"an explicitly stored empty request is present, not absent");
			Assert.IsFalse(Eligible(Absent(), Absent(), Int(0)),
				"a request key under the int table is present, not absent");
			Assert.IsFalse(Eligible(Absent(), Absent(),
				new KingdomDurableKeyObservation
				{
					HasString = true,
					String = "arch",
					HasInt = true,
					Int = 1
				}), "a dual-typed request key is present twice over");
			Assert.IsFalse(Eligible(Absent(), Absent(),
				new KingdomDurableKeyObservation { HasBoolean = true }));
		}

		[Test]
		public void AnUnobservedGameIsNotEligible()
		{
			string refusal;
			Assert.IsFalse(Eligible(null, null, null, null, null, out refusal));
			Assert.IsNotNull(refusal);
			Assert.IsFalse(Eligible(null, null, Absent()));
		}

		/// <summary>Every refusal names which authority it saw, so an operator can act on it.</summary>
		[Test]
		public void EveryRefusalNamesTheAuthorityItSaw()
		{
			string refusal;
			Eligible(Text("sc1|k"), Int(1), Absent(), Absent(), Absent(), out refusal);
			Assert.IsNotEmpty(refusal);
			Eligible(Absent(), Absent(), Int(1), Absent(), Absent(), out refusal);
			Assert.IsNotEmpty(refusal);
			Eligible(Absent(), Absent(), Absent(), Absent(), Text("x"), out refusal);
			Assert.IsNotEmpty(refusal);
		}
	}
}
#endif
