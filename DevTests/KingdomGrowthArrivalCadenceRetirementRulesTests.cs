#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGrowthArrivalCadenceRetirementRulesTests
	{
		[Test]
		public void DelayedRefusalChangesRateAtDurableTerminalNotNextObservation()
		{
			KingdomGrowthBook book = ActiveCadence();
			Advance(book, 10L, 10L, 0);
			KingdomGrowthArrivalOpportunity opportunity = Freeze(book);
			Advance(book, 10L, 20L, 1);

			ClassicAssert.IsTrue(KingdomLifecycleRules.
				TryTransitionGrowthArrivalCadenceForRetirement(book, opportunity,
					25L, 45L, 10L, 0, 3, out string failure), failure);
			ClassicAssert.AreEqual(25L, book.ArrivalRateEpochStartedTick);
			ClassicAssert.AreEqual(25L, book.ArrivalProcessedThroughTick);
			ClassicAssert.AreEqual(35L, book.ArrivalCadenceNextDueTick);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryRetireGrowthArrivalOpportunity(book,
				opportunity));
			Advance(book, 45L, 10L, 0);

			ClassicAssert.AreEqual(1, book.ArrivalDebtRanges.Count);
			ClassicAssert.AreEqual(2UL, book.ArrivalDebtRanges[0].FirstOrdinal);
			ClassicAssert.AreEqual(1UL, book.ArrivalDebtRanges[0].Count);
			ClassicAssert.AreEqual(35L, book.ArrivalDebtRanges[0].FirstDueTick);
			ClassicAssert.AreEqual(0, book.ArrivalDebtRanges[0].Cohort);
			ClassicAssert.AreEqual(3L, book.ArrivalDebtRanges[0].RateEpoch);
			ClassicAssert.AreEqual(35L, book.ArrivalProcessedThroughTick);
			ClassicAssert.AreEqual(45L, book.ArrivalCadenceNextDueTick);
		}

		[Test]
		public void SaveCutBeforeRetirementReplaysByteExactly()
		{
			KingdomGrowthBook book = ActiveCadence();
			Advance(book, 10L, 10L, 0);
			KingdomGrowthArrivalOpportunity opportunity = Freeze(book);
			Advance(book, 10L, 20L, 1);
			ClassicAssert.IsTrue(KingdomLifecycleRules.
				TryTransitionGrowthArrivalCadenceForRetirement(book, opportunity,
					25L, 25L, 10L, 0, 3, out string failure), failure);
			book = RoundTripCadence(book);
			opportunity = book.ArrivalOpportunity;
			byte[] before = WriteCadence(book);

			ClassicAssert.IsTrue(KingdomLifecycleRules.
				TryTransitionGrowthArrivalCadenceForRetirement(book, opportunity,
					25L, 40L, 10L, 0, 3, out failure), failure);
			CollectionAssert.AreEqual(before, WriteCadence(book));
		}

		[Test]
		public void RuntimeTransitionsBeforeOperationAndOpportunityRetirement()
		{
			string completion = TestMain.ReadRepositoryText(
				"Growth/KingdomGrowth.z07.ArrivalCompletion.cs");
			int transition = completion.IndexOf("TransitionArrivalCadenceForRetirement",
				StringComparison.Ordinal);
			int operation = completion.IndexOf("RetireGrowth(growth, operation",
				StringComparison.Ordinal);
			int opportunity = completion.IndexOf("TryRetireGrowthArrivalOpportunity",
				StringComparison.Ordinal);
			Assert.That(transition, Is.GreaterThanOrEqualTo(0));
			Assert.That(operation, Is.GreaterThan(transition));
			Assert.That(opportunity, Is.GreaterThan(operation));
		}

		private static KingdomGrowthBook ActiveCadence()
		{
			return new KingdomGrowthBook
			{
				SettlementId = "city-cadence-retirement",
				OptionState = KingdomLifecycleOptionState.Enabled,
				HealthState = KingdomGrowthHealthState.Healthy,
				ArrivalCadenceMigrationPending = false, ArrivalRulesVersion = 3,
				ArrivalRateEpoch = 1L, ArrivalRateEpochStartedTick = 0L,
				ArrivalProcessedThroughTick = 0L, ArrivalCadenceNextDueTick = 10L,
				ArrivalRateCohort = 0, ArrivalIntervalTicks = 10L, NextArrivalTick = 10L
			};
		}

		private static void Advance(KingdomGrowthBook book, long now, long interval,
			int cohort)
		{
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(book, now,
				interval, cohort, 3, out string failure), failure);
		}

		private static KingdomGrowthArrivalOpportunity Freeze(KingdomGrowthBook book)
		{
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeGrowthArrivalOpportunity(book, 3,
				KingdomLifecycleRules.GrowthArrivalEventStreamId,
				KingdomLifecycleRules.GrowthArrivalEventKindCode, false, "Settler", "Joppa",
				"water", "Ari", "1 of Nivvun, 1000 AR", out var opportunity));
			return opportunity;
		}

		private static byte[] WriteCadence(KingdomGrowthBook book)
		{
			MethodInfo method = typeof(KingdomLifecycleWireCodec).GetMethod(
				"WriteGrowthArrivalCadence", BindingFlags.NonPublic | BindingFlags.Static);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				method.Invoke(null, new object[] { writer, book }); writer.Flush();
				return stream.ToArray();
			}
		}

		private static KingdomGrowthBook RoundTripCadence(KingdomGrowthBook source)
		{
			byte[] payload = WriteCadence(source);
			MethodInfo method = typeof(KingdomLifecycleWireCodec).GetMethod(
				"ReadGrowthArrivalCadence", BindingFlags.NonPublic | BindingFlags.Static);
			KingdomGrowthBook book = new KingdomGrowthBook
			{
				SettlementId = "city-cadence-retirement",
				OptionState = KingdomLifecycleOptionState.Enabled,
				HealthState = KingdomGrowthHealthState.Healthy,
				ArrivalIntervalTicks = source.ArrivalIntervalTicks,
				NextArrivalTick = source.NextArrivalTick
			};
			using (MemoryStream stream = new MemoryStream(payload, false))
			using (BinaryReader reader = new BinaryReader(stream))
				method.Invoke(null, new object[] { reader, book });
			return book;
		}
	}
}
#endif
