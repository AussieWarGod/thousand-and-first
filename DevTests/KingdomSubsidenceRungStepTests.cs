#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomSubsidenceRungStepTests
	{
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);
		private const long Anchor = 500;
		private static long Due => Anchor + KingdomSubsidenceStepRules.StepTicks;

		private static KingdomSubsidenceStepBook Step(int completed, bool release = true)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(book, Realm, Settlement, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(book, Anchor, Due, GrowthStage.City,
				5, out book, 1024, "water"));
			for (int i = 1; i <= completed; i++)
			{
				KingdomResidentDepartureOperation departure = new KingdomResidentDepartureOperation
				{
					Version = KingdomResidentDepartureOperation.CurrentVersion,
					Phase = (int)KingdomResidentDeparturePhase.Prepared, Revision = 1,
					RealmId = Realm, SettlementId = Settlement, ResidentId = i,
					BodyObjectId = "rung-resident-" + i, ZoneId = "JoppaWorld.1.1.1.1.10",
					ResidentName = "resident", PreparedTick = Due,
					OperationId = KingdomResidentDepartureRules.Id(Realm, Settlement, i, "rung-resident-" + i, Due)
				};
				Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.Town, out book));
				if (release || i != completed)
					Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			}
			return book;
		}

		private static KingdomSubsidenceRungPlan Plan(KingdomSubsidenceStepBook book, bool work = false,
			long? prepared = null)
		{
			KingdomSubsidenceRungWork[] works = new KingdomSubsidenceRungWork[0];
			if (work)
			{
				string id = null;
				for (int i = 0; i < 10000 && id == null; i++)
					if (KingdomSubsidenceRules.RollRuin(Settlement, "rung-work-" + i, (ulong)Due, GrowthStage.City))
						id = "rung-work-" + i;
				Assert.IsNotNull(id);
				works = new[] { new KingdomSubsidenceRungWork(KingdomCityRules.StableId(id), id,
					"hut", "plot", null, "hut", 3, 4, false, 0,
					KingdomMaterialRules.AddWear(0, KingdomSubsidenceRules.RolledRuinIncrement(Settlement, id, (ulong)Due)),
					KingdomSubsidenceEffectPhase.Prepared, new KingdomSubsidenceRungRoof[0]) };
			}
			return new KingdomSubsidenceRungPlan(book.Active.Id, Realm, Settlement, "JoppaWorld.1.1.1.1.10",
				GrowthStage.City, GrowthStage.Town, Due, prepared ?? Due, book.Active.Completed, works);
		}

		private static KingdomSubsidenceStepBook RoundTrip(KingdomSubsidenceStepBook book)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			Assert.IsTrue(wire.StartsWith("ss5:", StringComparison.Ordinal));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out book));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string repeated));
			Assert.AreEqual(wire, repeated);
			return book;
		}

		[TestCase("third-state")] [TestCase("reset")] [TestCase("chronicle")]
		public void FailedTellingRetiresPhysicalStepButPreservesExactUndeliveredEvidence(string cause)
		{
			KingdomSubsidenceStepBook book = Step(5);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book), out book));
			var report = new KingdomSubsidenceReportPlan(book.Active.Id, Realm, Settlement,
				new[] { new KingdomSubsidenceReportEntry("The city became a town.", "The city fell.", Due) });
			Assert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(report, 0, new string[0], out report));
			if (cause == "chronicle")
			{
				Assert.IsTrue(KingdomSubsidenceReportRules.TryProveLedger(report, 0, new[] { "The city fell." }, out report));
				Assert.IsTrue(KingdomSubsidenceReportRules.TryLoseChronicle(report, 0, out report));
			}
			else
			{
				Assert.IsTrue(KingdomSubsidenceReportRules.TryLoseLedger(report, 0,
					cause == "reset" ? new string[0] : new[] { "Unrelated news." }, cause == "reset", out report));
				Assert.IsTrue(KingdomSubsidenceReportRules.TryProveChronicle(report, 0, out report));
			}
			Assert.IsFalse(KingdomSubsidenceReportRules.Complete(report));
			Assert.IsTrue(KingdomSubsidenceReportRules.Settled(report));
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string reportWire));
			book = RoundTrip(book.With(book.Active.Copy(rungReportModel: reportWire), book.Sequence));
			KingdomSubsidenceStepBook full = book;
			for (int i = 1; i <= KingdomSubsidenceReportArchive.MaxReports; i++)
			{
				var other = new KingdomSubsidenceReportPlan("taf:subsidence-step:v1:" + i.ToString("x64"),
					Realm, Settlement, report.Entries);
				Assert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(other, out string otherWire));
				Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(full, otherWire, out full));
			}
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(full, Anchor, out _),
				"no clock can be spent when retirement would evict an unread failure");
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(full, Due, out _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(full.WithFailures(KingdomSubsidenceReportArchive.None),
				Anchor, out _), "the founder can acknowledge prior failures without discarding this active report");
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long checkpoint));
			Assert.AreEqual(Due, checkpoint);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, checkpoint, out var retired));
			retired = RoundTrip(retired);
			Assert.IsNull(retired.Active); Assert.AreEqual(book.Sequence, retired.Sequence);
			Assert.AreEqual(Due, retired.LastRetiredTick);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRead(retired.FailureModel, out var retained));
			CollectionAssert.AreEqual(new[] { reportWire }, retained);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(retired, Due,
				Due + KingdomSubsidenceStepRules.StepTicks, GrowthStage.Town, 1, out var next, 0, "water"));
			Assert.AreEqual(retired.FailureModel, next.FailureModel);
		}

		[TestCase(0, false)] [TestCase(1, false)] [TestCase(4, false)] [TestCase(5, true)]
		public void OneFrozenPlanWaitsForTheWholeQuotaUnlessCancellationIsDurable(int completed, bool expected)
		{
			KingdomSubsidenceStepBook book = Step(completed);
			Assert.AreEqual(expected, KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book), out _));
		}

		[Test]
		public void UnretiredDepartureCannotBeCoveredByAPlanEvenAfterLastCredit()
		{
			KingdomSubsidenceStepBook book = Step(5, false);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book), out _));
		}

		[TestCase(0, false)] [TestCase(9, false)] [TestCase(10, true)] [TestCase(11, true)]
		public void CancelledPartialPlanKeepsDueTickButCannotPredateCancellation(int later, bool expected)
		{
			KingdomSubsidenceStepBook book = Step(1);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Due + 10, 3, out book));
			Assert.AreEqual(expected, KingdomSubsidenceStepRules.TryFreezeRungPlan(book,
				Plan(book, prepared: Due + later), out KingdomSubsidenceStepBook next));
			if (!expected) return;
			next = RoundTrip(next);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(next, out KingdomSubsidenceRungPlan plan));
			Assert.AreEqual(Due, plan.DueTick);
			Assert.AreEqual(1, plan.Departed);
			Assert.AreEqual(GrowthStage.Town, plan.To);
		}

		[Test]
		public void ExactRepeatCannotRefreezeDifferentTargetsOrLocations()
		{
			KingdomSubsidenceStepBook book = Step(5);
			KingdomSubsidenceRungPlan plan = Plan(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
			book = RoundTrip(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out KingdomSubsidenceStepBook repeated));
			Assert.AreSame(book, repeated);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book, true), out _));
			KingdomSubsidenceRungPlan moved = new KingdomSubsidenceRungPlan(plan.StepId, plan.RealmId,
				plan.SettlementId, "another-zone", plan.From, plan.To, plan.DueTick, plan.PreparedTick,
				plan.Departed, plan.Works);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, moved, out _));
		}

		[Test]
		public void ParentWireRetainsWearIntentAndMeasuredProofWithoutSpendingUnfinishedReporting()
		{
			KingdomSubsidenceStepBook book = Step(5);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book, true), out book));
			book = RoundTrip(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan plan));
			int after = plan.Works[0].AfterWear;
			Assert.IsFalse(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, after, out _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
			book = RoundTrip(book);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, false, true, after, out _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, after, out book));
			book = RoundTrip(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out plan));
			Assert.IsTrue(KingdomSubsidenceRungRules.PhysicalComplete(plan));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _),
				"Physical adapter and keyed reporting integration remain required; this receipt alone cannot retire.");
		}

		[Test]
		public void PhysicalRungWaitsForProvedReportAcrossSs3CutsBeforeCheckpointAndRetirement()
		{
			KingdomSubsidenceStepBook book = PhysicallyComplete();
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Due, out _));
			KingdomSubsidenceReportPlan report = new KingdomSubsidenceReportPlan(book.Active.Id, Realm,
				Settlement, new[] { new KingdomSubsidenceReportEntry("A rung was lost", "", Due) });
			book = WithReport(book, report);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			Assert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(report, 0, new string[0], out report));
			Assert.AreEqual(ReportLedgerPhase.Skipped, report.Entries[0].LedgerPhase);
			book = WithReport(book, report);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Due, out _));
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(book.Active.RungReportModel, out report));
			Assert.IsTrue(KingdomSubsidenceReportRules.TryProveChronicle(report, 0, out report));
			book = WithReport(book, report);
			Assert.IsTrue(KingdomSubsidenceStepRules.Valid(book), "Legacy completed reports remain readable.");
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Due, out _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan rung));
			KingdomSubsidenceRungWork work = rung.Works[0];
			// Fresh observation of an exact legacy-cleared receipt, not migration credit.
			KingdomSubsidenceWearReceipt cleared = new KingdomSubsidenceWearReceipt(0, null,
				(int)KingdomWearRules.WearCause.Subsidence, work.BeforeWear, work.AfterWear,
				work.AfterWear, 0, rung.StepId, null, 0);
			string reportWire = book.Active.RungReportModel;
			Assert.IsTrue(KingdomSubsidenceStepRules.TryArmRungRelease(book, 0, true, cleared, out book));
			book = RoundTrip(book);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryProveRungRelease(book, 0, true, cleared, out book));
			book = RoundTrip(book);
			Assert.AreEqual(reportWire, book.Active.RungReportModel);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long checkpoint));
			Assert.AreEqual(Due, checkpoint);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Due, out long repeated));
			Assert.AreEqual(checkpoint, repeated);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Due + 1, out _));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, checkpoint, out KingdomSubsidenceStepBook retired));
			retired = RoundTrip(retired);
			Assert.IsNull(retired.Active); Assert.AreEqual(Due, retired.LastRetiredTick);
			Assert.AreEqual(5, book.Active.Completed); Assert.AreEqual(0, book.LastRetiredTick);
		}

		[TestCase("empty")] [TestCase("wrong-date")] [TestCase("foreign-owner")]
		public void IndependentlyValidReportCannotEraseOrRebindAnOwedRung(string change)
		{
			KingdomSubsidenceStepBook original = PhysicallyComplete();
			KingdomSubsidenceReportEntry[] entries = change == "empty" ? new KingdomSubsidenceReportEntry[0]
				: new[] { new KingdomSubsidenceReportEntry("A rung was lost", "", change == "wrong-date" ? Due + 1 : Due) };
			string owner = change == "foreign-owner" ? "taf:subsidence-step:v1:" + new string('c', 64) : original.Active.Id;
			KingdomSubsidenceReportPlan report = new KingdomSubsidenceReportPlan(owner, Realm, Settlement, entries);
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string wire));
			KingdomSubsidenceStepBook forged = original.With(original.Active.Copy(rungReportModel: wire), original.Sequence);
			Assert.IsFalse(KingdomSubsidenceStepRules.Valid(forged));
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(forged, out string refused)); Assert.IsNull(refused);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(forged, Anchor, out _));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(forged, Due, out _));
			Assert.AreEqual(KingdomSubsidenceBatchRules.PendingReport, RoundTrip(original).Active.RungReportModel);
		}

		private static KingdomSubsidenceStepBook PhysicallyComplete()
		{
			KingdomSubsidenceStepBook book = Step(5);
			KingdomSubsidenceRungPlan plan = Plan(book, true);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, plan.Works[0].AfterWear, out book));
			book = RoundTrip(book);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out plan));
			Assert.IsTrue(KingdomSubsidenceRungRules.PhysicalComplete(plan)); return book;
		}

		private static KingdomSubsidenceStepBook WithReport(KingdomSubsidenceStepBook book, KingdomSubsidenceReportPlan report)
		{
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string wire));
			return RoundTrip(book.With(book.Active.Copy(rungReportModel: wire), book.Sequence));
		}

		[TestCase(GrowthStage.City, true)] [TestCase(GrowthStage.Town, true)]
		[TestCase(GrowthStage.Village, false)] [TestCase(GrowthStage.Camp, false)]
		public void OneStepCannotClaimMultipleTrajectoryBreakpoints(GrowthStage reached, bool expected)
		{
			KingdomSubsidenceStepBook book = Step(1, false);
			KingdomSubsidenceStepOperation op = book.Active.Copy(reachedStage: reached,
				rungModel: reached == GrowthStage.City ? "sr1:none" : "sr1:pending",
				rungReportModel: reached == GrowthStage.City ? "st1:none" : "st1:pending");
			Assert.AreEqual(expected, KingdomSubsidenceStepRules.Valid(book.With(op, book.Sequence)));
		}

		[TestCase(int.MaxValue, 1, 60)] [TestCase(1, int.MaxValue, 60)]
		[TestCase(int.MaxValue, int.MaxValue, 60)] [TestCase(int.MinValue, int.MinValue, 0)]
		[TestCase(-1, 5, 5)] [TestCase(5, -1, 5)] [TestCase(35, 20, 55)]
		public void SharedWearArithmeticCannotWrapUnderHostileInputs(int wear, int added, int expected)
		{
			Assert.AreEqual(expected, KingdomMaterialRules.AddWear(wear, added));
		}
	}
}
#endif
