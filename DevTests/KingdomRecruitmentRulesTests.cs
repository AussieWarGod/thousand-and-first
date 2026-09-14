using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomRecruitmentRulesTests
	{
		[TestCase(0, 0, 10000UL)]
		[TestCase(500, 0, 20000UL)]
		[TestCase(0, 500, 20000UL)]
		[TestCase(500, 500, 40000UL)]
		[TestCase(1000, 1000, 90000UL)]
		[TestCase(int.MaxValue, int.MaxValue, 90000UL)]
		[TestCase(-249, 0, 5100UL)]
		[TestCase(int.MinValue, int.MinValue, 2601UL)]
		public void BothRegardChannelsHaveBoundedWeights(int Personal, int Civic, ulong Expected)
		{
			ClassicAssert.IsTrue(KingdomRecruitmentRules.TryWeight(1UL, Personal, Civic, false, out ulong weight));
			ClassicAssert.AreEqual(Expected, weight);
		}

		[Test]
		public void HostilityExcludesEvenTheLargestPositiveCounterweight()
		{
			ClassicAssert.IsTrue(KingdomRecruitmentRules.TryWeight(1000000UL,
				int.MaxValue, int.MaxValue, true, out ulong weight));
			ClassicAssert.AreEqual(0UL, weight);
		}

		[TestCase(0UL)]
		[TestCase(1000001UL)]
		[TestCase(ulong.MaxValue)]
		public void InvalidAuthoredWeightsNeverOverflowOrBecomeFallbacks(ulong Base)
		{
			ClassicAssert.IsFalse(KingdomRecruitmentRules.TryWeight(Base, int.MaxValue,
				int.MaxValue, false, out ulong weight));
			ClassicAssert.AreEqual(0UL, weight);
		}

		[Test]
		public void IncreasingEitherRegardNeverReducesVoluntaryWeight()
		{
			ulong before = 0UL;
			for (int regard = -500; regard <= 2000; regard++)
			{
				ClassicAssert.IsTrue(KingdomRecruitmentRules.TryWeight(17UL, regard, 73, false, out ulong personal));
				ClassicAssert.IsTrue(KingdomRecruitmentRules.TryWeight(17UL, 73, regard, false, out ulong civic));
				ClassicAssert.AreEqual(personal, civic);
				ClassicAssert.GreaterOrEqual(personal, before);
				before = personal;
			}
		}

		[Test]
		public void FixedSeedWeightedDrawsAreOrderIndependentWithoutDiversityQuotas()
		{
			var seed = new KernelSeed128(123UL, 456UL);
			var ordinary = new List<KingdomSemanticWeightedEntry>
			{
				Entry("workers", 60UL, 0, 0), Entry("mechanimists", 15UL, 500, 500),
				Entry("dromad", 3UL, 0, 0)
			};
			var reverse = new List<KingdomSemanticWeightedEntry>(ordinary); reverse.Reverse();
			int workers = 0, mechanimists = 0, dromad = 0;
			for (ulong ordinal = 1UL; ordinal <= 4096UL; ordinal++)
			{
				ClassicAssert.IsTrue(KingdomSemanticSelectionRules.TryChoose(seed, 1,
					"taf:settlement:recruit-test", "taf:semantic:recruit-test:v1", 1U, ordinal,
					0U, ordinary, out string first, out _));
				ClassicAssert.IsTrue(KingdomSemanticSelectionRules.TryChoose(seed, 1,
					"taf:settlement:recruit-test", "taf:semantic:recruit-test:v1", 1U, ordinal,
					0U, reverse, out string repeated, out _));
				ClassicAssert.AreEqual(first, repeated);
				if (first == "workers") workers++;
				else if (first == "mechanimists") mechanimists++;
				else dromad++;
			}
			ClassicAssert.That(workers, Is.InRange(1750, 2250));
			ClassicAssert.That(mechanimists, Is.InRange(1750, 2250));
			ClassicAssert.That(dromad, Is.InRange(50, 160));
		}

		private static KingdomSemanticWeightedEntry Entry(string Id, ulong Base, int Personal, int Civic)
		{
			ClassicAssert.IsTrue(KingdomRecruitmentRules.TryWeight(Base, Personal, Civic, false, out ulong weight));
			return new KingdomSemanticWeightedEntry(Id, weight);
		}
	}
}
