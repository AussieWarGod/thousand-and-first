#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGrowthArrivalCadenceRulesTests
	{
		[Test]
		public void WakePartitionsPublishSameOrdinalsDeadlinesEpochsAndPayload()
		{
			KingdomGrowthBook direct = ActiveCadence();
			AssertAdvance(direct, 35L, 10L, 0);
			Freeze(direct);
			AssertAdvance(direct, 10L, 20L, 1);
			AssertAdvance(direct, 35L, 20L, 1);

			KingdomGrowthBook partitioned = ActiveCadence();
			AssertAdvance(partitioned, 5L, 10L, 0);
			partitioned = RoundTripCadence(partitioned);
			AssertAdvance(partitioned, 10L, 10L, 0);
			Freeze(partitioned);
			partitioned = RoundTripCadence(partitioned);
			AssertAdvance(partitioned, 10L, 20L, 1);
			AssertAdvance(partitioned, 20L, 20L, 1);
			AssertAdvance(partitioned, 35L, 20L, 1);

			CollectionAssert.AreEqual(WriteCadence(direct), WriteCadence(partitioned));
			Assert.AreEqual(2UL, direct.ArrivalOrdinalHighWater);
			Assert.AreEqual(1UL, direct.ArrivalOpportunity.Ordinal);
			StringAssert.StartsWith("taf:growth-arrival-opportunity:",
				direct.ArrivalOpportunity.EventId);
			Assert.AreEqual(30L, direct.ArrivalDebtRanges[0].FirstDueTick);
			Assert.AreEqual(2UL, direct.ArrivalDebtRanges[0].FirstOrdinal);
		}

		[Test]
		public void FixedPeriodFoldRetainsLargeDebtInOneBoundedRange()
		{
			KingdomGrowthBook book = ActiveCadence();
			AssertAdvance(book, 10L, 10L, 0); Freeze(book);
			AssertAdvance(book, 10L, 10L, 1);
			AssertAdvance(book, 1_000_000L, 10L, 1);
			Assert.AreEqual(1, book.ArrivalDebtRanges.Count);
			Assert.AreEqual(99_999UL, book.ArrivalDebtRanges[0].Count);
			Assert.AreEqual(100_000UL, book.ArrivalOrdinalHighWater);
			Assert.AreEqual(0UL, book.ArrivalOrdinalRetiredThrough);
		}

		[Test]
		public void PhysicalRetirementMovesOnlyOneSemanticHeadAndKeepsDebt()
		{
			KingdomGrowthBook book = ActiveCadence();
			AssertAdvance(book, 30L, 10L, 0); Freeze(book);
			AssertAdvance(book, 10L, 10L, 1);
			AssertAdvance(book, 30L, 10L, 1);
			KingdomGrowthArrivalOpportunity opportunity = book.ArrivalOpportunity;
			ulong debt = KingdomLifecycleRules.ArrivalDebtCount(book);
			Assert.IsTrue(KingdomLifecycleRules.TryRetireGrowthArrivalOpportunity(book,
				opportunity));
			Assert.AreEqual(1UL, book.ArrivalOrdinalRetiredThrough);
			Assert.AreEqual(debt - 1UL, KingdomLifecycleRules.ArrivalDebtCount(book));
			Assert.AreEqual(book.ArrivalDebtRanges[0].FirstDueTick, book.NextArrivalTick);
		}

		[Test]
		public void V6MigrationKeepsLegacyCandidateAndInventsNoElapsedOpportunity()
		{
			KingdomGrowthArrivalCandidate legacy = new KingdomGrowthArrivalCandidate
			{
				Sequence = 8L, Id = "legacy-candidate", Blueprint = "LegacySettler"
			};
			KingdomGrowthBook withCandidate = new KingdomGrowthBook
			{
				ArrivalCandidateRetiredThrough = 7L, ArrivalCandidate = legacy,
				NextArrivalTick = 20L, ArrivalIntervalTicks = 10L
			};
			Assert.IsTrue(KingdomLifecycleRules.UpgradeHistoricalGrowthArrivalCadence(
				withCandidate));
			Assert.AreSame(legacy, withCandidate.ArrivalCandidate);
			Assert.AreEqual("LegacySettler", withCandidate.ArrivalCandidate.Blueprint);
			Assert.AreEqual(8UL, withCandidate.ArrivalOrdinalHighWater);
			Assert.AreEqual(0, withCandidate.ArrivalDebtRanges.Count);

			KingdomGrowthBook empty = new KingdomGrowthBook
			{
				ArrivalCandidateRetiredThrough = 7L, NextArrivalTick = 20L,
				ArrivalIntervalTicks = 10L
			};
			Assert.IsTrue(KingdomLifecycleRules.UpgradeHistoricalGrowthArrivalCadence(empty));
			Assert.IsTrue(KingdomLifecycleRules.TryBindHistoricalGrowthArrivalCadence(empty,
				1_000L, 10L, 7, 3, out string failure), failure);
			Assert.AreEqual(1_010L, empty.NextArrivalTick);
			Assert.AreEqual(7UL, empty.ArrivalOrdinalHighWater);
			Assert.AreEqual(0, empty.ArrivalDebtRanges.Count);
		}

		[Test]
		public void OrdinalOverflowRefusalIsAtomic()
		{
			KingdomGrowthBook book = ActiveCadence();
			book.ArrivalOrdinalHighWater = ulong.MaxValue;
			book.ArrivalOrdinalRetiredThrough = ulong.MaxValue;
			byte[] before = WriteCadence(book);
			Assert.IsFalse(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(book,
				10L, 10L, 0, 3, out _));
			CollectionAssert.AreEqual(before, WriteCadence(book));
		}

		[Test]
		public void ClockRegressionRefusalIsAtomic()
		{
			KingdomGrowthBook book = ActiveCadence();
			AssertAdvance(book, 5L, 10L, 0);
			byte[] before = WriteCadence(book);
			Assert.IsFalse(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(book,
				4L, 10L, 0, 3, out _));
			CollectionAssert.AreEqual(before, WriteCadence(book));
		}

		[Test]
		public void DebtRangeCapRefusalRetainsEveryExistingOrdinal()
		{
			KingdomGrowthBook book = ActiveCadence();
			book.ArrivalProcessedThroughTick = 64L;
			book.ArrivalCadenceNextDueTick = 100L;
			book.ArrivalOrdinalHighWater = 64UL;
			book.NextArrivalTick = 1L;
			for (int i = 0; i < KingdomLifecycleRules.MaxGrowthArrivalDebtRanges; i++)
				book.ArrivalDebtRanges.Add(new KingdomGrowthArrivalDebtRange
				{
					RulesVersionAtCreation = 3, RateEpoch = i + 1L, Cohort = i,
					FirstOrdinal = (ulong)i + 1UL, Count = 1UL, FirstDueTick = i + 1L,
					IntervalTicks = 1L
				});
			Assert.IsTrue(KingdomLifecycleRules.GrowthArrivalCadenceShape(book));
			byte[] before = WriteCadence(book);
			Assert.IsFalse(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(book,
				100L, 10L, 0, 3, out _));
			CollectionAssert.AreEqual(before, WriteCadence(book));
		}

		[Test]
		public void CurrentCadenceRoundTripIsByteExact()
		{
			KingdomGrowthBook book = ActiveCadence();
			AssertAdvance(book, 25L, 10L, 0); Freeze(book);
			byte[] first = WriteCadence(book);
			KingdomGrowthBook loaded = ReadCadence(first, book.ArrivalIntervalTicks,
				book.NextArrivalTick);
			CollectionAssert.AreEqual(first, WriteCadence(loaded));
			Assert.AreEqual(book.ArrivalOpportunity.PayloadHash,
				loaded.ArrivalOpportunity.PayloadHash);
		}

		[Test]
		public void ArchiveV17CarriesCadenceAndV16RefusesLossyDowngrade()
		{
			KingdomGrowthBook book = ActiveCadence();
			AssertAdvance(book, 25L, 10L, 0); Freeze(book);
			KingdomSettlement settlement = new KingdomSettlement();
			settlement.LifecycleBook.Growth = book;
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(settlement,
				out byte[] v18, out string failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.ExpeditionResultVersion,
				BitConverter.ToInt32(v18, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v18,
				out KingdomSettlement loaded, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			CollectionAssert.AreEqual(WriteCadence(book),
				WriteCadence(loaded.LifecycleBook.Growth));
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncodePhysicalFirstGuestV16ForTests(
				settlement, out byte[] _, out failure));
			StringAssert.Contains("historical arrival cadence", failure);
		}

		[Test]
		public void FrozenPayloadTamperFailsBeforeRetirementOrFurtherAdvance()
		{
			KingdomGrowthBook book = ActiveCadence();
			AssertAdvance(book, 10L, 10L, 0); Freeze(book);
			KingdomGrowthArrivalOpportunity opportunity = book.ArrivalOpportunity;
			opportunity.PersonName = "different";
			Assert.IsFalse(KingdomLifecycleRules.TryRetireGrowthArrivalOpportunity(book,
				opportunity));
			Assert.IsFalse(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(book,
				20L, 10L, 1, 3, out _));
		}

		[Test]
		public void PauseAddsNoDebtAndResumeStartsOneFreshRateEpoch()
		{
			KingdomGrowthBook book = ActiveCadence();
			AssertAdvance(book, 10L, 10L, 0); Freeze(book);
			AssertAdvance(book, 10L, 20L, 1);
			byte[] beforePause = WriteCadence(book);
			book.WorkPaused = true;
			book.OptionState = KingdomLifecycleOptionState.Disabled;
			AssertAdvance(book, 100L, 20L, 1);
			CollectionAssert.AreEqual(beforePause, WriteCadence(book));
			book.WorkPaused = false;
			book.OptionState = KingdomLifecycleOptionState.Enabled;
			book.ArrivalCadenceResumePending = true;
			Assert.IsTrue(KingdomLifecycleRules.TryRestartGrowthArrivalCadenceAfterPause(book,
				100L, 20L, 1, 3, out string failure), failure);
			Assert.AreEqual(3L, book.ArrivalRateEpoch);
			Assert.AreEqual(120L, book.ArrivalCadenceNextDueTick);
			Assert.AreEqual(1UL, book.ArrivalOrdinalHighWater);
		}

		[Test]
		public void RuntimeSourceCapsBodiesButDoesNotBurnSemanticDebt()
		{
			string activation = TestMain.ReadRepositoryText(
				"Growth/KingdomGrowth.z01.Activation.cs");
			StringAssert.Contains("arrivals < KingdomRules.MaxArrivalsPerVisit", activation);
			StringAssert.Contains("AdvanceArrivalCadence(System, Z, timeTicks)", activation);
			StringAssert.DoesNotContain("overshoot is burned", activation);
			string cadence = TestMain.ReadRepositoryText(
				"Experience/KingdomGrowthArrivalCadenceRules.cs");
			StringAssert.Contains("TickMath.TryCountFixedPeriodDue", cadence);
			StringAssert.DoesNotContain("while (", cadence);
			StringAssert.DoesNotContain("for (", cadence);
			string freeze = TestMain.ReadRepositoryText(
				"Growth/KingdomGrowth.ArrivalCadence.cs");
			StringAssert.Contains("TryPrepareGrowthArrivalPayload", freeze);
			StringAssert.DoesNotContain("GameObject", freeze);
			StringAssert.DoesNotContain("TryProbeArrivalCell", freeze);
			string operationCodec = TestMain.ReadRepositoryText(
				"Experience/KingdomLifecycleWireCodec.GrowthOperation.cs");
			string candidateCodec = TestMain.ReadRepositoryText(
				"Experience/KingdomLifecycleWireCodec.GrowthArrival.cs");
			StringAssert.Contains("ArrivalOpportunityPayloadHash", operationCodec);
			StringAssert.Contains("WriteGrowthArrivalCandidateCadence", candidateCodec);
			StringAssert.Contains("ArrivalOpportunityPayloadHash", TestMain.ReadRepositoryText(
				"Experience/KingdomLifecycleWireCodec.GrowthCadence.cs"));
			StringAssert.Contains("ArrivalOpportunityOrdinal", TestMain.ReadRepositoryText(
				"Experience/KingdomGrowthLifecyclePlanCodecRules.cs"));
			StringAssert.Contains("ArrivalOpportunityOrdinal", TestMain.ReadRepositoryText(
				"Experience/KingdomGrowthLifecycleArrivalHashRules.cs"));
		}

		private static KingdomGrowthBook ActiveCadence()
		{
			return new KingdomGrowthBook
			{
				SettlementId = "city-cadence",
				OptionState = KingdomLifecycleOptionState.Enabled,
				HealthState = KingdomGrowthHealthState.Healthy,
				ArrivalCadenceMigrationPending = false, ArrivalRulesVersion = 3,
				ArrivalRateEpoch = 1L, ArrivalRateEpochStartedTick = 0L,
				ArrivalProcessedThroughTick = 0L, ArrivalCadenceNextDueTick = 10L,
				ArrivalRateCohort = 0, ArrivalIntervalTicks = 10L, NextArrivalTick = 10L
			};
		}

		private static void AssertAdvance(KingdomGrowthBook book, long now, long interval,
			int cohort)
		{
			Assert.IsTrue(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(book, now,
				interval, cohort, 3, out string failure), failure);
		}

		private static void Freeze(KingdomGrowthBook book)
		{
			Assert.IsTrue(KingdomLifecycleRules.TryFreezeGrowthArrivalOpportunity(book, 3,
				KingdomLifecycleRules.GrowthArrivalEventStreamId,
				KingdomLifecycleRules.GrowthArrivalEventKindCode, false, "Settler", "Joppa",
				"water", "Ari", "1 of Nivvun, 1000 AR", out _));
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
			return ReadCadence(WriteCadence(source), source.ArrivalIntervalTicks,
				source.NextArrivalTick);
		}

		private static KingdomGrowthBook ReadCadence(byte[] payload, long interval, long next)
		{
			MethodInfo method = typeof(KingdomLifecycleWireCodec).GetMethod(
				"ReadGrowthArrivalCadence", BindingFlags.NonPublic | BindingFlags.Static);
			KingdomGrowthBook book = new KingdomGrowthBook
			{
				SettlementId = "city-cadence",
				OptionState = KingdomLifecycleOptionState.Enabled,
				HealthState = KingdomGrowthHealthState.Healthy,
				ArrivalIntervalTicks = interval, NextArrivalTick = next
			};
			using (MemoryStream stream = new MemoryStream(payload, false))
			using (BinaryReader reader = new BinaryReader(stream))
				method.Invoke(null, new object[] { reader, book });
			return book;
		}
	}
}
#endif
