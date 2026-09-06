#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomSubsidenceExecutionClockTests
	{
		private const long Anchor = 500;
		private const long Due = Anchor + KingdomSubsidenceStepRules.StepTicks;
		private const long Late = Due + 500;
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);

		[TestCase(-1L, 0L, false)]
		[TestCase(long.MinValue, 0L, false)]
		[TestCase(10000L, long.MinValue, false)]
		[TestCase(10000L, -1L, false)]
		[TestCase(10000L, 10001L, false)]
		[TestCase(10000L, long.MaxValue, false)]
		[TestCase(0L, 0L, true)]
		[TestCase(10000L, 0L, true)]
		[TestCase(10000L, 10000L, true)]
		[TestCase(long.MaxValue, 0L, true)]
		[TestCase(long.MaxValue, long.MaxValue, true)]
		public void RawWorldBoundsNeverNormalizeEvidence(long now, long raw, bool allowed)
		{
			Check(Admitted(), now, raw, allowed);
		}

		[Test]
		public void FreshAndLegacyAdmissionDoNotBypassRawClockValidation()
		{
			foreach (KingdomSubsidenceAdmission admission in new[] {
				KingdomSubsidenceAdmission.Fresh, KingdomSubsidenceAdmission.Legacy })
			{
				KingdomSubsidenceStepBook book = new KingdomSubsidenceStepBook(admission, "", "", 0, null);
				Check(book, 10000, 0, true); Check(book, 10000, -1, false);
			}
		}

		[TestCase(true, 10000L, KingdomElapsedOptionAction.Run)]
		[TestCase(false, 10000L, KingdomElapsedOptionAction.Disabled)]
		[TestCase(true, 100L, KingdomElapsedOptionAction.Wait)]
		public void UnchangedOptionDecisionsDoNotReplaceClockValidation(bool enabled,
			long now, KingdomElapsedOptionAction expected)
		{
			KingdomElapsedOptionRecord prior = new KingdomElapsedOptionRecord(enabled
				? KingdomElapsedOptionState.Enabled : KingdomElapsedOptionState.Disabled, 100, 0);
			KingdomElapsedOptionDecision decision = KingdomElapsedOptionRules.Observe(prior, enabled, 0, now);
			Assert.IsTrue(decision.Valid); Assert.AreEqual(expected, decision.Action);
			Check(Admitted(), now, 0, true); Check(Admitted(), now, -1, false);
			Check(Admitted(), now, now + 1, false);
			Assert.AreEqual(KingdomElapsedOptionRules.Encode(prior), KingdomElapsedOptionRules.Encode(decision.Record));
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
		public void ActiveClockNeedsAnchorUntilExactDepartureRungAndReportAuthority(int state)
		{
			KingdomSubsidenceStepBook book = Begin(state == 4 ? 2 : 1);
			KingdomResidentDepartureOperation departure = Departure();
			if (state > 0) Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			if (state > 1) Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId,
				state == 5 ? GrowthStage.Town : GrowthStage.City, out book));
			if (state > 2) Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			Check(book, Late, Anchor, true); Check(book, Late, Anchor + 1, false);
			Check(book, Late, Due, state == 3); Check(book, Late, Late, false);
			Assert.AreEqual(state == 3, KingdomSubsidenceStepRules.TryCheckpoint(book, Due, out _));
		}

		[TestCase(false, false, false)] [TestCase(false, false, true)]
		[TestCase(false, true, false)] [TestCase(false, true, true)]
		[TestCase(true, false, false)] [TestCase(true, false, true)]
		[TestCase(true, true, false)] [TestCase(true, true, true)]
		public void CancellationCutsKeepPendingCreditRollbackAndFullQuotaCheckpoints(bool quotaOne,
			bool committed, bool frozenOption)
		{
			KingdomSubsidenceStepBook book = Begin(quotaOne ? 1 : 2);
			KingdomResidentDepartureOperation departure = Departure();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			if (frozenOption) book = Freeze(book, Anchor);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Late, 2, out book));
			Check(book, Late, Anchor, true); Check(book, Late, Late, false);
			if (committed)
			{
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.City, out book));
				Check(book, Late, Anchor, true); Check(book, Late, Late, false);
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			}
			else Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRolledBack(book, departure.OperationId, out book));
			long target = quotaOne && committed ? Due : Late;
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long proved));
			Assert.AreEqual(target, proved);
			Check(book, Late, Anchor, true); Check(book, Late, target, true);
			Check(book, Late, target == Due ? Late : Due, false);
			Check(book, Late, target - 1, false);
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)]
		public void PendingOptionWithoutActiveStepUsesItsOwnCheckpointOracle(int mode)
		{
			KingdomSubsidenceStepBook book = mode == 0 ? Freeze(Admitted(), Anchor)
				: Retired(mode == 1, true);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryDecode(book.OptionModel,
				out KingdomSubsidenceOptionIntent intent));
			foreach (long raw in new[] { 0L, Anchor, Anchor + 1, Due, Late, Late + 1 })
			{
				bool allowed = KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, book, raw, out long target);
				if (allowed) Assert.AreEqual(Late, target);
				Check(book, Late + 10, raw, allowed);
			}
			Check(book, Late, Anchor, true);
		}

		[Test]
		public void SteplessOptionCannotBorrowAnUnrelatedRetiredTick()
		{
			KingdomSubsidenceStepBook book = Freeze(Retired(true, false), Due + 100);
			Check(book, Late, Due, false); Check(book, Late, Due + 100, true);
			Check(book, Late, Late, true);
		}

		[TestCase(false)] [TestCase(true)]
		public void ActiveOptionDueBeforeTickNeedsActualTerminalAuthority(bool complete)
		{
			KingdomSubsidenceStepBook book = complete ? Completed() : Begin();
			book = Freeze(book, Due);
			Check(book, Late, Anchor, complete); Check(book, Late, Due, complete);
			Check(book, Late, Late, false);
			if (!complete) Check(book, Late, Anchor, false, "unproved active-step checkpoint");
		}

		[Test]
		public void ActiveFrozenAnchorAllowsContinuationButNotPrematureOptionTarget()
		{
			KingdomSubsidenceStepBook book = Freeze(Begin(), Anchor);
			Check(book, Late, Anchor, true); Check(book, Late, Due, false); Check(book, Late, Late, false);
		}

		[TestCase("due")] [TestCase("activity")] [TestCase("cancellation")]
		[TestCase("option")] [TestCase("retired")]
		public void FrozenFutureDatesRefuseEvenWhenTheRawClockIsInRange(string kind)
		{
			KingdomSubsidenceStepBook book = Begin(); long now = Due;
			if (kind == "due") now--;
			if (kind == "activity") book = book.With(book.Active.Copy(lastActivityTick: Late), book.Sequence);
			if (kind == "cancellation") Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Late, 2, out book));
			if (kind == "option") book = Freeze(book, Anchor);
			if (kind == "retired") { book = Retired(true, false); now--; }
			Check(book, now, Anchor, false, kind);
		}

		[Test]
		public void RetiredRollbackAndQuarantinedStepCannotExecute()
		{
			KingdomSubsidenceStepBook book = Retired(true, false);
			Check(book, Late, Due - 1, false, "precedes"); Check(book, Late, Due, true);
			book = Begin(); book = book.With(book.Active.Copy(
				phase: KingdomSubsidenceStepPhase.Quarantined, fault: "clock fixture"), book.Sequence);
			Check(book, Late, Anchor, false, "quarantined"); Check(book, Late, Due, false);
		}

		[Test]
		public void MaximumWorldTickAndExactTerminalStepNeedNoOverflowingArithmetic()
		{
			long anchor = long.MaxValue - KingdomSubsidenceStepRules.StepTicks;
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), anchor, long.MaxValue,
				GrowthStage.City, 1, out KingdomSubsidenceStepBook book, 0, "water"));
			Check(book, long.MaxValue, anchor, true); Check(book, long.MaxValue, long.MaxValue, false);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, long.MaxValue, 0, out book));
			Check(book, long.MaxValue, long.MaxValue, true);
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
		public void MalformedOrForeignFrozenBooksRefuseWithoutReplacingEvidence(int kind)
		{
			KingdomSubsidenceStepBook book = Begin();
			if (kind == 0) book = null;
			if (kind == 1) book = book.With(book.Active.Copy(completed: 6), book.Sequence);
			if (kind == 2) book = book.WithOption("so1:broken");
			if (kind == 3) book = book.WithOption(Freeze(Begin(2), Anchor).OptionModel);
			string option = book?.OptionModel; KingdomSubsidenceStepOperation active = book?.Active;
			Assert.IsFalse(KingdomSubsidenceStepRules.Valid(book));
			Assert.IsFalse(KingdomSubsidenceExecutionClockRules.TryValidate(Late, Anchor, book, out string issue));
			StringAssert.Contains("invalid", issue);
			Assert.AreEqual(option, book?.OptionModel); Assert.AreSame(active, book?.Active);
		}

		[TestCase(Due, false)] [TestCase(Late, true)]
		public void FrozenRungPreparationMustNotPostdateActualWorldEvenAtAnAuthorizedClockCut(long now, bool allowed)
		{
			KingdomSubsidenceStepBook book = Begin();
			KingdomResidentDepartureOperation departure = Departure();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.Town, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			KingdomSubsidenceRungPlan rung = new KingdomSubsidenceRungPlan(book.Active.Id, Realm, Settlement,
				departure.ZoneId, GrowthStage.City, GrowthStage.Town, Due, Late, book.Active.Completed,
				new KingdomSubsidenceRungWork[0]);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, rung, out book));
			KingdomSubsidenceReportPlan report = new KingdomSubsidenceReportPlan(book.Active.Id, Realm, Settlement,
				new[] { new KingdomSubsidenceReportEntry("The clock fixture fell to a town.", "", Due) });
			Assert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(report, 0, new string[0], out report));
			Assert.IsTrue(KingdomSubsidenceReportRules.TryProveChronicle(report, 0, out report));
			Assert.IsTrue(KingdomSubsidenceReportRules.Settled(report));
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string reportWire));
			book = book.With(book.Active.Copy(rungReportModel: reportWire), book.Sequence);
			Assert.IsTrue(KingdomSubsidenceStepRules.Valid(book));
			Assert.AreEqual(KingdomSubsidenceStepPhase.Settling, book.Active.Phase);
			Assert.AreEqual(Due, book.Active.LastActivityTick);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out rung));
			Assert.AreEqual(Late, rung.PreparedTick);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long target));
			Assert.AreEqual(Due, target);
			Check(book, now, Anchor, allowed, allowed ? null : "rung preparation");
			Check(book, now, Due, allowed, allowed ? null : "rung preparation");
		}

		private static KingdomSubsidenceStepBook Admitted()
		{
			KingdomSubsidenceStepBook fresh = new KingdomSubsidenceStepBook(KingdomSubsidenceAdmission.Fresh, "", "", 0, null);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(fresh, Realm, Settlement, out KingdomSubsidenceStepBook book));
			return book;
		}
		private static KingdomSubsidenceStepBook Begin(int quota = 1)
		{
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), Anchor, Due,
				GrowthStage.City, quota, out KingdomSubsidenceStepBook book, 0, "water")); return book;
		}
		private static KingdomResidentDepartureOperation Departure()
		{
			const string body = "execution-clock-body";
			KingdomResidentDepartureOperation departure = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion, Revision = 1,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, RealmId = Realm, SettlementId = Settlement,
				ResidentId = 1, BodyObjectId = body, ZoneId = "JoppaWorld.12.24.1.1.10",
				ResidentName = "Clock fixture", PreparedTick = Due,
				OperationId = KingdomResidentDepartureRules.Id(Realm, Settlement, 1, body, Due)
			};
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(departure)); return departure;
		}
		private static KingdomSubsidenceStepBook Completed()
		{
			KingdomSubsidenceStepBook book = Begin(); KingdomResidentDepartureOperation departure = Departure();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.City, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book)); return book;
		}
		private static KingdomSubsidenceStepBook Freeze(KingdomSubsidenceStepBook book, long before)
		{
			KingdomDurableKeyObservation prior = new KingdomDurableKeyObservation { HasString = true, String = "v1|E|100|2" };
			Assert.IsTrue(KingdomSubsidenceOptionRules.Observe(prior, false, 2, Late,
				out KingdomSubsidenceOptionRules.Snapshot snapshot).Valid);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(book, before, snapshot, out KingdomSubsidenceOptionIntent intent));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryFreezeOption(book, intent, out book)); return book;
		}
		private static KingdomSubsidenceStepBook Retired(bool complete, bool frozen)
		{
			KingdomSubsidenceStepBook book = complete ? Completed() : Begin();
			if (frozen) book = Freeze(book, Anchor);
			if (!complete) Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Late, 2, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, complete ? Due : Late, out book)); return book;
		}
		private static string Wire(KingdomSubsidenceStepBook book)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire)); return wire;
		}
		private static void Check(KingdomSubsidenceStepBook book, long now, long raw, bool allowed, string reason = null)
		{
			string wire = Wire(book); KingdomSubsidenceStepOperation active = book.Active;
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook restored));
			foreach (KingdomSubsidenceStepBook candidate in new[] { book, restored })
			{
				Assert.AreEqual(allowed, KingdomSubsidenceExecutionClockRules.TryValidate(now, raw, candidate, out string issue));
				if (allowed) Assert.IsNull(issue); else Assert.IsNotEmpty(issue);
				if (reason != null) StringAssert.Contains(reason, issue);
				Assert.AreEqual(wire, Wire(candidate));
			}
			Assert.AreSame(active, book.Active);
		}
	}
}
#endif
