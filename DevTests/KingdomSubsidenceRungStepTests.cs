#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(book, Realm, Settlement, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(book, Anchor, Due, GrowthStage.City,
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
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.Town, out book));
				if (release || i != completed)
					ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
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
				ClassicAssert.IsNotNull(id);
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
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			ClassicAssert.IsTrue(wire.StartsWith("ss5:", StringComparison.Ordinal));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string repeated));
			ClassicAssert.AreEqual(wire, repeated);
			return book;
		}

		[TestCase("third-state")] [TestCase("reset")] [TestCase("chronicle")]
		public void FailedTellingRetiresPhysicalStepButPreservesExactUndeliveredEvidence(string cause)
		{
			KingdomSubsidenceStepBook book = Step(5);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book), out book));
			var report = new KingdomSubsidenceReportPlan(book.Active.Id, Realm, Settlement,
				new[] { new KingdomSubsidenceReportEntry("The city became a town.", "The city fell.", Due) });
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(report, 0, new string[0], out report));
			if (cause == "chronicle")
			{
				ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryProveLedger(report, 0, new[] { "The city fell." }, out report));
				ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryLoseChronicle(report, 0, out report));
			}
			else
			{
				ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryLoseLedger(report, 0,
					cause == "reset" ? new string[0] : new[] { "Unrelated news." }, cause == "reset", out report));
				ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryProveChronicle(report, 0, out report));
			}
			ClassicAssert.IsFalse(KingdomSubsidenceReportRules.Complete(report));
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.Settled(report));
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string reportWire));
			book = RoundTrip(book.With(book.Active.Copy(rungReportModel: reportWire), book.Sequence));
			KingdomSubsidenceStepBook full = book;
			for (int i = 1; i <= KingdomSubsidenceReportArchive.MaxReports; i++)
			{
				var other = new KingdomSubsidenceReportPlan("taf:subsidence-step:v1:" + i.ToString("x64"),
					Realm, Settlement, report.Entries);
				ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(other, out string otherWire));
				ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(full, otherWire, out full));
			}
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(full, Anchor, out _),
				"no clock can be spent when retirement would evict an unread failure");
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(full, Due, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(full.WithFailures(KingdomSubsidenceReportArchive.None),
				Anchor, out _), "the founder can acknowledge prior failures without discarding this active report");
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long checkpoint));
			ClassicAssert.AreEqual(Due, checkpoint);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, checkpoint, out var retired));
			retired = RoundTrip(retired);
			ClassicAssert.IsNull(retired.Active); ClassicAssert.AreEqual(book.Sequence, retired.Sequence);
			ClassicAssert.AreEqual(Due, retired.LastRetiredTick);
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRead(retired.FailureModel, out var retained));
			CollectionAssert.AreEqual(new[] { reportWire }, retained);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(retired, Due,
				Due + KingdomSubsidenceStepRules.StepTicks, GrowthStage.Town, 1, out var next, 0, "water"));
			ClassicAssert.AreEqual(retired.FailureModel, next.FailureModel);
		}

		[TestCase(0, false)] [TestCase(1, false)] [TestCase(4, false)] [TestCase(5, true)]
		public void OneFrozenPlanWaitsForTheWholeQuotaUnlessCancellationIsDurable(int completed, bool expected)
		{
			KingdomSubsidenceStepBook book = Step(completed);
			ClassicAssert.AreEqual(expected, KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book), out _));
		}

		[Test]
		public void UnretiredDepartureCannotBeCoveredByAPlanEvenAfterLastCredit()
		{
			KingdomSubsidenceStepBook book = Step(5, false);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book), out _));
		}

		[TestCase(0, false)] [TestCase(9, false)] [TestCase(10, true)] [TestCase(11, true)]
		public void CancelledPartialPlanKeepsDueTickButCannotPredateCancellation(int later, bool expected)
		{
			KingdomSubsidenceStepBook book = Step(1);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Due + 10, 3, out book));
			ClassicAssert.AreEqual(expected, KingdomSubsidenceStepRules.TryFreezeRungPlan(book,
				Plan(book, prepared: Due + later), out KingdomSubsidenceStepBook next));
			if (!expected) return;
			next = RoundTrip(next);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(next, out KingdomSubsidenceRungPlan plan));
			ClassicAssert.AreEqual(Due, plan.DueTick);
			ClassicAssert.AreEqual(1, plan.Departed);
			ClassicAssert.AreEqual(GrowthStage.Town, plan.To);
		}

		[Test]
		public void ExactRepeatCannotRefreezeDifferentTargetsOrLocations()
		{
			KingdomSubsidenceStepBook book = Step(5);
			KingdomSubsidenceRungPlan plan = Plan(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
			book = RoundTrip(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out KingdomSubsidenceStepBook repeated));
			ClassicAssert.AreSame(book, repeated);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book, true), out _));
			KingdomSubsidenceRungPlan moved = new KingdomSubsidenceRungPlan(plan.StepId, plan.RealmId,
				plan.SettlementId, "another-zone", plan.From, plan.To, plan.DueTick, plan.PreparedTick,
				plan.Departed, plan.Works);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, moved, out _));
		}

		[Test]
		public void ParentWireRetainsWearIntentAndMeasuredProofWithoutSpendingUnfinishedReporting()
		{
			KingdomSubsidenceStepBook book = Step(5);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, Plan(book, true), out book));
			book = RoundTrip(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan plan));
			int after = plan.Works[0].AfterWear;
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, after, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
			book = RoundTrip(book);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, false, true, after, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, after, out book));
			book = RoundTrip(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out plan));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.PhysicalComplete(plan));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _),
				"Physical adapter and keyed reporting integration remain required; this receipt alone cannot retire.");
		}

		[Test]
		public void PhysicalRungWaitsForProvedReportAcrossSs3CutsBeforeCheckpointAndRetirement()
		{
			KingdomSubsidenceStepBook book = PhysicallyComplete();
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Due, out _));
			KingdomSubsidenceReportPlan report = new KingdomSubsidenceReportPlan(book.Active.Id, Realm,
				Settlement, new[] { new KingdomSubsidenceReportEntry("A rung was lost", "", Due) });
			book = WithReport(book, report);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(report, 0, new string[0], out report));
			ClassicAssert.AreEqual(ReportLedgerPhase.Skipped, report.Entries[0].LedgerPhase);
			book = WithReport(book, report);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Due, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(book.Active.RungReportModel, out report));
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryProveChronicle(report, 0, out report));
			book = WithReport(book, report);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.Valid(book), "Legacy completed reports remain readable.");
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Due, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan rung));
			KingdomSubsidenceRungWork work = rung.Works[0];
			// Fresh observation of an exact legacy-cleared receipt, not migration credit.
			KingdomSubsidenceWearReceipt cleared = new KingdomSubsidenceWearReceipt(0, null,
				(int)KingdomWearRules.WearCause.Subsidence, work.BeforeWear, work.AfterWear,
				work.AfterWear, 0, rung.StepId, null, 0);
			string reportWire = book.Active.RungReportModel;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungRelease(book, 0, true, cleared, out book));
			book = RoundTrip(book);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungRelease(book, 0, true, cleared, out book));
			book = RoundTrip(book);
			ClassicAssert.AreEqual(reportWire, book.Active.RungReportModel);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long checkpoint));
			ClassicAssert.AreEqual(Due, checkpoint);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Due, out long repeated));
			ClassicAssert.AreEqual(checkpoint, repeated);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Due + 1, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, checkpoint, out KingdomSubsidenceStepBook retired));
			retired = RoundTrip(retired);
			ClassicAssert.IsNull(retired.Active); ClassicAssert.AreEqual(Due, retired.LastRetiredTick);
			ClassicAssert.AreEqual(5, book.Active.Completed); ClassicAssert.AreEqual(0, book.LastRetiredTick);
		}

		[TestCase("empty")] [TestCase("wrong-date")] [TestCase("foreign-owner")]
		public void IndependentlyValidReportCannotEraseOrRebindAnOwedRung(string change)
		{
			KingdomSubsidenceStepBook original = PhysicallyComplete();
			KingdomSubsidenceReportEntry[] entries = change == "empty" ? new KingdomSubsidenceReportEntry[0]
				: new[] { new KingdomSubsidenceReportEntry("A rung was lost", "", change == "wrong-date" ? Due + 1 : Due) };
			string owner = change == "foreign-owner" ? "taf:subsidence-step:v1:" + new string('c', 64) : original.Active.Id;
			KingdomSubsidenceReportPlan report = new KingdomSubsidenceReportPlan(owner, Realm, Settlement, entries);
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string wire));
			KingdomSubsidenceStepBook forged = original.With(original.Active.Copy(rungReportModel: wire), original.Sequence);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.Valid(forged));
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(forged, out string refused)); ClassicAssert.IsNull(refused);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(forged, Anchor, out _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(forged, Due, out _));
			ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.PendingReport, RoundTrip(original).Active.RungReportModel);
		}

		private static KingdomSubsidenceStepBook PhysicallyComplete()
		{
			KingdomSubsidenceStepBook book = Step(5);
			KingdomSubsidenceRungPlan plan = Plan(book, true);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, plan.Works[0].AfterWear, out book));
			book = RoundTrip(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out plan));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.PhysicalComplete(plan)); return book;
		}

		private static KingdomSubsidenceStepBook WithReport(KingdomSubsidenceStepBook book, KingdomSubsidenceReportPlan report)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string wire));
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
			ClassicAssert.AreEqual(expected, KingdomSubsidenceStepRules.Valid(book.With(op, book.Sequence)));
		}

		[TestCase(int.MaxValue, 1, 60)] [TestCase(1, int.MaxValue, 60)]
		[TestCase(int.MaxValue, int.MaxValue, 60)] [TestCase(int.MinValue, int.MinValue, 0)]
		[TestCase(-1, 5, 5)] [TestCase(5, -1, 5)] [TestCase(35, 20, 55)]
		public void SharedWearArithmeticCannotWrapUnderHostileInputs(int wear, int added, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.AddWear(wear, added));
		}
	}
}
#endif
