#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Deterministic attended selection among buildings that all match the frozen case.
	/// <para>
	/// A real settlement holds several buildings, so requiring exactly one stamped owner in the zone
	/// is not a workflow at all. But taking the first of several would make a curated anchor depend
	/// on enumeration order, which is the one thing a differential may never depend on. Ambiguity
	/// therefore refuses with a stable list.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioSelectorTests
	{
		private static KingdomScenarioOwnerRow Row(int x, int y, string id)
		{
			return new KingdomScenarioOwnerRow { X = x, Y = y, Id = id };
		}

		private static List<KingdomScenarioOwnerRow> Two()
		{
			return new List<KingdomScenarioOwnerRow>
			{
				Row(11, 7, "object-b"),
				Row(4, 3, "object-a")
			};
		}

		private static int Resolve(IList<KingdomScenarioOwnerRow> rows, string selector,
			out string failure)
		{
			bool hasCoordinate;
			int x;
			int y;
			string id;
			if (!KingdomScenarioSelectorRules.TryParse(selector, out hasCoordinate, out x, out y,
				out id, out failure)) return -1;
			return KingdomScenarioSelectorRules.Resolve(rows, hasCoordinate, x, y, id, out failure);
		}

		// ----- ordering ---------------------------------------------------------------------------

		/// <summary>Row order is the answer's order, so it cannot depend on how the zone enumerates.</summary>
		[Test]
		public void OrderingIsByPositionThenStableIdentity()
		{
			List<KingdomScenarioOwnerRow> rows = Two();
			KingdomScenarioSelectorRules.Sort(rows);
			Assert.AreEqual("object-a", rows[0].Id);
			Assert.AreEqual("object-b", rows[1].Id);
			List<KingdomScenarioOwnerRow> tied = new List<KingdomScenarioOwnerRow>
			{
				Row(2, 2, "zeta"),
				Row(2, 2, "alpha")
			};
			KingdomScenarioSelectorRules.Sort(tied);
			Assert.AreEqual("alpha", tied[0].Id);
		}

		// ----- arity ------------------------------------------------------------------------------

		/// <summary>Zero matches is a refusal, never an empty measurement.</summary>
		[Test]
		public void ZeroCandidatesRefuses()
		{
			string failure;
			Assert.AreEqual(-1, Resolve(new List<KingdomScenarioOwnerRow>(), "", out failure));
			StringAssert.Contains("no building", failure);
			Assert.AreEqual(-1, Resolve(null, "", out failure));
		}

		[Test]
		public void OneExactCandidateNeedsNoSelector()
		{
			string failure;
			List<KingdomScenarioOwnerRow> one = new List<KingdomScenarioOwnerRow> { Row(4, 3, "a") };
			Assert.AreEqual(0, Resolve(one, "", out failure));
			Assert.IsNull(failure);
		}

		/// <summary>
		/// The case a settlement actually produces. Two identical buildings must not silently
		/// resolve to whichever the zone happened to list first.
		/// </summary>
		[Test]
		public void TwoIdenticalCandidatesRefuseWithoutASelector()
		{
			string failure;
			List<KingdomScenarioOwnerRow> rows = Two();
			KingdomScenarioSelectorRules.Sort(rows);
			Assert.AreEqual(-1, Resolve(rows, "", out failure));
			StringAssert.Contains("2 buildings", failure);
			StringAssert.Contains("at=4,3", failure);
			StringAssert.Contains("at=11,7", failure);
			StringAssert.Contains("id=object-a", failure);
		}

		// ----- selectors --------------------------------------------------------------------------

		[Test]
		public void ACoordinateSelectorNamesOneCandidate()
		{
			string failure;
			List<KingdomScenarioOwnerRow> rows = Two();
			KingdomScenarioSelectorRules.Sort(rows);
			Assert.AreEqual(1, Resolve(rows, "at=11,7", out failure));
			Assert.AreEqual(0, Resolve(rows, "at=4,3", out failure));
		}

		[Test]
		public void AnIdentitySelectorNamesOneCandidate()
		{
			string failure;
			List<KingdomScenarioOwnerRow> rows = Two();
			KingdomScenarioSelectorRules.Sort(rows);
			Assert.AreEqual(1, Resolve(rows, "id=object-b", out failure));
		}

		[Test]
		public void ASelectorNamingNothingRefuses()
		{
			string failure;
			List<KingdomScenarioOwnerRow> rows = Two();
			KingdomScenarioSelectorRules.Sort(rows);
			Assert.AreEqual(-1, Resolve(rows, "at=99,99", out failure));
			StringAssert.Contains("names no building", failure);
			Assert.AreEqual(-1, Resolve(rows, "id=object-z", out failure));
		}

		/// <summary>Two rows on one cell cannot both be "the" selection; identity settles it.</summary>
		[Test]
		public void AnAmbiguousCoordinateSelectorRefuses()
		{
			string failure;
			List<KingdomScenarioOwnerRow> tied = new List<KingdomScenarioOwnerRow>
			{
				Row(2, 2, "alpha"),
				Row(2, 2, "zeta")
			};
			Assert.AreEqual(-1, Resolve(tied, "at=2,2", out failure));
			StringAssert.Contains("more than one", failure);
			Assert.AreEqual(1, Resolve(tied, "id=zeta", out failure));
		}

		[TestCase("north")]
		[TestCase("at=")]
		[TestCase("at=4")]
		[TestCase("at=4,")]
		[TestCase("at=-4,3")]
		[TestCase("at=4,3,5")]
		[TestCase("at=x,3")]
		[TestCase("at= 4,3")]
		[TestCase("id=")]
		public void AMalformedSelectorRefusesRatherThanBeingIgnored(string selector)
		{
			string failure;
			bool hasCoordinate;
			int x;
			int y;
			string id;
			Assert.IsFalse(KingdomScenarioSelectorRules.TryParse(selector, out hasCoordinate,
				out x, out y, out id, out failure), selector);
			Assert.IsNotEmpty(failure);
		}

		[Test]
		public void AnEmptySelectorIsLawfulAndSelectsNothing()
		{
			string failure;
			bool hasCoordinate;
			int x;
			int y;
			string id;
			Assert.IsTrue(KingdomScenarioSelectorRules.TryParse("", out hasCoordinate, out x,
				out y, out id, out failure));
			Assert.IsFalse(hasCoordinate);
			Assert.IsNull(id);
			Assert.IsNull(failure);
		}
	}
}
#endif
