#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomSubsidenceRungSaveForecastTests
	{
		private const long Anchor = 500, Step = KingdomSubsidenceStepRules.StepTicks;
		private static readonly string Realm = RungFixture.Realm, Settlement = RungFixture.Settlement;

		[TestCase(1, 1, 5, 0, 5, 3)]
		[TestCase(2, 2, 10, 5, 10, 3)]
		[TestCase(3, 3, 15, 10, 15, 3)]
		[TestCase(1, 3, 5, 0, 5, 3)]
		[TestCase(2, 3, 7, 5, 7, 3)]
		[TestCase(1, 1, 10, 0, 5, 2)]
		public void ForecastMatchesLegalRetirementAndNamesTheProjectedDepartureCount(
			int current, int through, int wanted, int prior, int expected, int named)
		{
			KingdomSubsidenceStepBook book = Completed(current, through, wanted);
			KingdomSubsidenceStepOperation active = book.Active;
			KingdomSubsidenceBatch batch = Batch(book);
			string before = Wire(book), batchBefore = book.BatchModel;
			ClassicAssert.AreEqual(prior, batch.Departed);
			ClassicAssert.AreEqual(prior == 0 ? 0 : 2, KingdomSubsidenceBatchRules.Named(batch));
			ClassicAssert.IsTrue(KingdomSubsidenceRungSaveForecast.TryClosingBatch(book, out KingdomSubsidenceBatch projected));
			ClassicAssert.AreEqual(expected, projected.Departed);
			ClassicAssert.AreEqual(named, KingdomSubsidenceBatchRules.Named(projected));
			ClassicAssert.IsTrue(projected.Closing); ClassicAssert.AreEqual(active.DueTick, projected.ClosedTick);
			ClassicAssert.AreEqual(batch.ReportModel, projected.ReportModel);
			ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.PendingReport, projected.ReportModel);
			if (current < through) ClassicAssert.Less(projected.ClosedTick, projected.ThroughTick);
			ClassicAssert.AreEqual(KingdomSubsidenceStepRules.NoRungs, active.RungModel);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, active.DueTick, out KingdomSubsidenceStepBook retired));
			ClassicAssert.AreEqual(retired.BatchModel, BatchWire(projected));
			ClassicAssert.IsNull(retired.Active);
			ClassicAssert.AreSame(active, book.Active); ClassicAssert.AreEqual(batchBefore, book.BatchModel);
			ClassicAssert.AreEqual(prior, batch.Departed); ClassicAssert.IsFalse(batch.Closing);
			ClassicAssert.AreEqual(before, Wire(book));
		}

		[Test]
		public void ForecastLeavesDurableReleaseIntentAndPendingReportUntouchedWhenRetirementRefuses()
		{
			KingdomSubsidenceStepBook book = Completed(1, 1, 5, fall: true);
			ClassicAssert.AreEqual(RungFixture.Due, book.Active.DueTick);
			KingdomSubsidenceRungWork work = RungFixture.Work();
			KingdomSubsidenceRungPlan plan = new KingdomSubsidenceRungPlan(book.Active.Id, Realm, Settlement,
				RungFixture.Zone, GrowthStage.City, GrowthStage.Town, book.Active.DueTick,
				book.Active.DueTick, book.Active.Completed, new[] { work });
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, work.AfterWear, out book));
			KingdomSubsidenceWearReceipt receipt = new KingdomSubsidenceWearReceipt(
				(int)KingdomWearIncidentPhase.Mutated, book.Active.Id, (int)KingdomWearRules.WearCause.Subsidence,
				work.BeforeWear, work.AfterWear, work.AfterWear, 0, null, null, 0);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungRelease(book, 0, true, receipt, out book));
			string before = Wire(book), rung = book.Active.RungModel, report = book.Active.RungReportModel;
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, book.Active.DueTick, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceRungSaveForecast.TryClosingBatch(book, out KingdomSubsidenceBatch projected));
			ClassicAssert.AreEqual(5, projected.Departed); ClassicAssert.AreEqual(3, KingdomSubsidenceBatchRules.Named(projected));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out plan));
			ClassicAssert.AreEqual(KingdomSubsidenceReleasePhase.Intent, plan.Works[0].ReleasePhase);
			ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.PendingReport, report);
			ClassicAssert.AreEqual(rung, book.Active.RungModel); ClassicAssert.AreEqual(report, book.Active.RungReportModel);
			ClassicAssert.AreEqual(before, Wire(book));
		}

		[TestCase("no-active")] [TestCase("zero-completed")] [TestCase("incomplete")]
		[TestCase("not-closing")] [TestCase("pending")] [TestCase("cancel")]
		[TestCase("option")] [TestCase("quarantined")] [TestCase("no-batch")]
		public void ValidBooksOutsideTheForecastContractRefuseAndKeepTheirExactWire(string shape)
		{
			KingdomSubsidenceStepBook book = Completed(1, 1, 5);
			switch (shape)
			{
				case "no-active": book = Attached(1, 5); break;
				case "zero-completed": book = Begin(Attached(1, 5)); break;
				case "incomplete": book = Begin(Attached(1, 5)); Credit(ref book, 1); break;
				case "not-closing": book = Completed(1, 2, 10); break;
				case "pending": book = Completed(1, 1, 5, release: false); break;
				case "cancel":
					book = Begin(Attached(1, 5)); Credit(ref book, 1);
					ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, book.Active.DueTick + 1, 2, out book)); break;
				case "option": book = WithOption(book); break;
				case "quarantined":
					book = book.With(book.Active.Copy(phase: KingdomSubsidenceStepPhase.Quarantined,
						fault: "fixture unresolved receipt"), book.Sequence); break;
				case "no-batch": book = book.WithBatch(KingdomSubsidenceBatchRules.None); break;
			}
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.Valid(book));
			Refuses(book);
		}

		[TestCase("null")] [TestCase("invalid-book")] [TestCase("malformed-batch")]
		[TestCase("noncanonical-batch")] [TestCase("mismatched-batch")]
		public void MalformedWholeBooksAndBatchStorageNeverProduceAPartialProjection(string shape)
		{
			KingdomSubsidenceStepBook book = Completed(3, 3, 15);
			if (shape == "null") book = null;
			if (shape == "invalid-book") book = book.With(book.Active.Copy(completed: int.MaxValue), book.Sequence);
			if (shape == "malformed-batch") book = book.WithBatch("sb1:broken");
			if (shape == "noncanonical-batch") book = book.WithBatch(book.BatchModel + "\n");
			if (shape == "mismatched-batch")
			{
				KingdomSubsidenceBatch mismatch = Batch(book).Copy(departed: 0);
				ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.Valid(mismatch));
				ClassicAssert.IsFalse(KingdomSubsidenceBatchRules.MatchesShape(mismatch, book));
				book = book.WithBatch(BatchWire(mismatch));
			}
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.Valid(book));
			Refuses(book);
		}

		private static void Refuses(KingdomSubsidenceStepBook book)
		{
			string before = KingdomSubsidenceStepRules.Valid(book) ? Wire(book) : null;
			string batch = book?.BatchModel, option = book?.OptionModel;
			KingdomSubsidenceStepOperation active = book?.Active;
			KingdomSubsidenceBatch projected = Batch(Attached(1, 5));
			ClassicAssert.IsFalse(KingdomSubsidenceRungSaveForecast.TryClosingBatch(book, out projected));
			ClassicAssert.IsNull(projected);
			ClassicAssert.AreEqual(batch, book?.BatchModel); ClassicAssert.AreEqual(option, book?.OptionModel);
			ClassicAssert.AreSame(active, book?.Active);
			if (before != null) ClassicAssert.AreEqual(before, Wire(book));
		}

		private static KingdomSubsidenceStepBook WithOption(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.Observe(new KingdomDurableKeyObservation
				{ HasString = true, String = "v1|E|100|2" }, false, 2, book.Active.DueTick + 1,
				out KingdomSubsidenceOptionRules.Snapshot snapshot).Valid);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(book, book.Active.AnchorTick,
				snapshot, out KingdomSubsidenceOptionIntent intent));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeOption(book, intent, out book));
			return book;
		}

		private static KingdomSubsidenceStepBook Attached(int through, int wanted)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(KingdomSubsidenceStepCodec.FreshWire,
				out KingdomSubsidenceStepBook book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(book, Realm, Settlement, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryBegin(book, Anchor, Anchor + through * Step,
				wanted, "Forecast fixture", "water", out KingdomSubsidenceBatch batch));
			book = book.WithBatch(BatchWire(batch));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.Valid(book));
			return book;
		}

		private static KingdomSubsidenceStepBook Begin(KingdomSubsidenceStepBook book)
		{
			KingdomSubsidenceBatch batch = Batch(book);
			long anchor = book.Sequence < batch.FirstSequence ? batch.AnchorTick : book.LastRetiredTick;
			int quota = System.Math.Min(5, batch.Wanted - batch.Departed);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(book, anchor, anchor + Step, GrowthStage.City,
				quota, out book, 0, "water"));
			return book;
		}

		private static KingdomSubsidenceStepBook Completed(int current, int through, int wanted,
			bool fall = false, bool release = true)
		{
			KingdomSubsidenceStepBook book = Attached(through, wanted);
			int id = 0;
			for (int step = 1; step <= current; step++)
			{
				book = Begin(book);
				int quota = book.Active.Quota;
				for (int credit = 1; credit <= quota; credit++)
				{
					bool last = step == current && credit == quota;
					Credit(ref book, ++id, !last || release, last && fall ? GrowthStage.Town : GrowthStage.City);
				}
				if (step < current)
					ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, book.Active.DueTick, out book));
			}
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.Valid(book));
			return book;
		}

		private static void Credit(ref KingdomSubsidenceStepBook book, int id, bool release = true,
			GrowthStage reached = GrowthStage.City)
		{
			long tick = book.Active.DueTick;
			string body = "forecast-body-" + id;
			KingdomResidentDepartureOperation departure = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion, Revision = 1,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, RealmId = Realm, SettlementId = Settlement,
				ResidentId = id, BodyObjectId = body, ZoneId = RungFixture.Zone, ResidentName = "Forecast fixture",
				PreparedTick = tick, OperationId = KingdomResidentDepartureRules.Id(Realm, Settlement, id, body, tick)
			};
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, reached, out book));
			if (release) ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
		}

		private static KingdomSubsidenceBatch Batch(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch));
			return batch;
		}
		private static string BatchWire(KingdomSubsidenceBatch batch)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceBatchCodec.TryEncode(batch, out string wire));
			return wire;
		}
		private static string Wire(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			return wire;
		}
	}
}
#endif
