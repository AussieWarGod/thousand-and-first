#if TAF_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomSubsidenceOptionIntentTests
	{
		private const long Anchor = 500L;
		private const long Due = Anchor + KingdomSubsidenceStepRules.StepTicks;
		private const long Late = Due + 500L;
		private const string Prior = "v1|E|100|2";
		private const string Next = "v1|D|5800|2";
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);

		private static KingdomSubsidenceStepBook Admitted()
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(fresh, Realm, Settlement,
				out KingdomSubsidenceStepBook admitted));
			return admitted;
		}

		private static KingdomSubsidenceStepBook Begin(int quota = 1)
		{
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), Anchor, Due, GrowthStage.City,
				quota, out KingdomSubsidenceStepBook active, 0, "water"));
			return active;
		}

		private static KingdomResidentDepartureOperation Departure()
		{
			KingdomResidentDepartureOperation departure = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, Revision = 1,
				RealmId = Realm, SettlementId = Settlement, ResidentId = 1,
				BodyObjectId = "option-intent-body", ZoneId = "JoppaWorld.12.24.1.1.10",
				ResidentName = "Option intent fixture resident", PreparedTick = Due,
				OperationId = KingdomResidentDepartureRules.Id(Realm, Settlement, 1, "option-intent-body", Due)
			};
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(departure));
			return departure;
		}

		private static KingdomSubsidenceStepBook Retired(bool fullQuota, long cancelTick = Late)
		{
			KingdomSubsidenceStepBook book = Begin();
			if (fullQuota)
			{
				KingdomResidentDepartureOperation departure = Departure();
				Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId,
					GrowthStage.City, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			}
			else Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, cancelTick, 7, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long target));
			Assert.AreEqual(fullQuota ? Due : cancelTick, target);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, target, out book));
			return book;
		}

		private static KingdomSubsidenceStepBook Cancelled(long tick, long token)
		{
			KingdomSubsidenceStepBook book = Begin();
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, tick, token, out book));
			Assert.IsTrue(book.Active.CancelRequested);
			return book;
		}

		private static KingdomSubsidenceOptionRules.Snapshot Observed(bool enabled, long token,
			long now, string prior)
		{
			KingdomDurableKeyObservation reading = new KingdomDurableKeyObservation
			{ HasString = prior != null, String = prior };
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(reading, enabled,
				token, now, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			Assert.IsTrue(decision.Valid);
			Assert.IsNotNull(snapshot);
			return snapshot;
		}

		private static KingdomSubsidenceOptionIntent Prepared(KingdomSubsidenceStepBook book,
			long beforeTick, string prior = Prior)
		{
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(book, beforeTick,
				Observed(prior == null, 2, Late, prior), out KingdomSubsidenceOptionIntent intent));
			return intent;
		}

		private static KingdomSubsidenceOptionIntent With(KingdomSubsidenceOptionIntent intent,
			string prior = null, string next = null, long? before = null, long? sequence = null,
			long? retired = null, string step = null, long? due = null)
		{
			return new KingdomSubsidenceOptionIntent(intent.PriorPresent, prior ?? intent.PriorWire,
				next ?? intent.NextWire, before ?? intent.BeforeTick, sequence ?? intent.Sequence,
				retired ?? intent.RetiredTick, step ?? intent.StepId, due ?? intent.StepDueTick);
		}

		private static byte[] Bytes(string wire) => Convert.FromBase64String(wire.Substring(4));
		private static string Wire(byte[] bytes) => "so1:" + Convert.ToBase64String(bytes);

		// Rewrites the production framing exactly: magic, explicit presence flag, length-framed
		// strict UTF-8 fields. Proved byte-identical against a canonical wire before it forges.
		private static string Forge(string canonical, string prior, string next, long before,
			long sequence, long retired, string step, long due)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
			{
				writer.Write(Bytes(canonical), 0, 4);
				writer.Write((byte)(prior == null ? 0 : 1));
				if (prior != null) writer.Write(prior);
				writer.Write(next); writer.Write(before); writer.Write(sequence); writer.Write(retired);
				writer.Write(step); writer.Write(due);
				writer.Flush();
				return Wire(stream.ToArray());
			}
		}

		private static void Refuses(string wire)
		{
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryDecode(wire,
				out KingdomSubsidenceOptionIntent decoded), wire);
			Assert.IsNull(decoded);
		}

		[Test]
		public void CanonicalRoundTripKeepsAbsentAndPresentPriorEvidenceApart()
		{
			KingdomSubsidenceOptionIntent present = Prepared(Begin(), Due);
			KingdomSubsidenceOptionIntent absent = Prepared(Begin(), Due, null);
			Assert.IsTrue(present.PriorPresent);
			Assert.AreEqual(Prior, present.PriorWire);
			Assert.AreEqual(Next, present.NextWire);
			Assert.IsFalse(absent.PriorPresent);
			Assert.IsNull(absent.PriorWire);
			Assert.AreEqual("v1|E|5800|2", absent.NextWire);
			foreach (KingdomSubsidenceOptionIntent original in new[] { present, absent })
			{
				Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(original, out string wire));
				Assert.IsTrue(wire.StartsWith("so1:", StringComparison.Ordinal));
				Assert.IsTrue(wire.Length <= KingdomSubsidenceOptionIntentRules.MaxWireChars);
				Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryDecode(wire,
					out KingdomSubsidenceOptionIntent restored));
				Assert.AreNotSame(original, restored);
				Assert.AreEqual(original.PriorPresent, restored.PriorPresent);
				Assert.AreEqual(original.PriorWire, restored.PriorWire);
				Assert.AreEqual(original.NextWire, restored.NextWire);
				Assert.AreEqual(original.BeforeTick, restored.BeforeTick);
				Assert.AreEqual(original.Sequence, restored.Sequence);
				Assert.AreEqual(original.RetiredTick, restored.RetiredTick);
				Assert.AreEqual(original.StepId, restored.StepId);
				Assert.AreEqual(original.StepDueTick, restored.StepDueTick);
				Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(restored, out string repeated));
				Assert.AreEqual(wire, repeated);
			}
		}

		[Test]
		public void CodecRefusesEveryNoncanonicalTruncatedOrOversizedWire()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Due);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(intent, out string wire));
			Assert.AreEqual(wire, Forge(wire, intent.PriorWire, intent.NextWire, intent.BeforeTick,
				intent.Sequence, intent.RetiredTick, intent.StepId, intent.StepDueTick));
			Refuses(null);
			Refuses("");
			Refuses("so1:");
			Refuses("so2:" + wire.Substring(4));
			Refuses(" " + wire);
			Refuses(wire.Substring(0, wire.Length - 4));
			Refuses(wire.Insert(12, "\n"));
			Refuses(new string('x', KingdomSubsidenceOptionIntentRules.MaxWireChars + 1));
			byte[] trailing = Bytes(wire);
			Array.Resize(ref trailing, trailing.Length + 1);
			Refuses(Wire(trailing));
			byte[] flag = Bytes(wire);
			flag[4] = 2;
			Refuses(Wire(flag));
			Refuses(Forge(wire, "", intent.NextWire, intent.BeforeTick, intent.Sequence,
				intent.RetiredTick, intent.StepId, intent.StepDueTick));
			Refuses(Forge(wire, Prior, "v1|D|05800|2", intent.BeforeTick, intent.Sequence,
				intent.RetiredTick, intent.StepId, intent.StepDueTick));
			Refuses(Forge(wire, Prior, intent.NextWire, -1L, intent.Sequence, intent.RetiredTick,
				intent.StepId, intent.StepDueTick));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryEncode(With(intent, prior: ""),
				out string refused));
			Assert.IsNull(refused);
		}

		[Test]
		public void ValidRefusesTamperedPriorNextAndStepEvidence()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(intent));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(null));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, prior: "v1|E|100|9")));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, prior: "v1|E|9000|2")));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, next: "v1|E|5800|2")));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, next: "v1|D|05800|2")));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, next: "")));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, prior: "")));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(new KingdomSubsidenceOptionIntent(
				true, null, intent.NextWire, intent.BeforeTick, intent.Sequence, intent.RetiredTick,
				intent.StepId, intent.StepDueTick)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(new KingdomSubsidenceOptionIntent(
				false, Prior, intent.NextWire, intent.BeforeTick, intent.Sequence, intent.RetiredTick,
				intent.StepId, intent.StepDueTick)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, before: -1L)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, before: Late + 1L)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, sequence: -1L)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, sequence: 0L)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, retired: -1L)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, due: -1L)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, due: 0L)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, step: "")));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent,
				step: intent.StepId.Substring(0, intent.StepId.Length - 1) + "z")));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(With(intent, retired: 7L)));
		}

		[Test]
		public void PrepareRefusesUnadmittedBooksAndNonAnchorDecisions()
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(fresh, 0,
				Observed(false, 2, Late, Prior), out KingdomSubsidenceOptionIntent refused));
			Assert.IsNull(refused);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(null, 0,
				Observed(false, 2, Late, Prior), out refused));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, null, out refused));
			KingdomSubsidenceOptionRules.Snapshot running = Observed(true, 2, Late, Prior);
			Assert.AreEqual(KingdomElapsedOptionAction.Run, running.Decision.Action);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, running, out refused));
			KingdomSubsidenceOptionRules.Snapshot waiting = Observed(true, 2, 100, Prior);
			Assert.AreEqual(KingdomElapsedOptionAction.Wait, waiting.Decision.Action);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Anchor, waiting, out refused));
			Assert.IsNull(refused);
		}

		[Test]
		public void PrepareRefusesEvidenceTheSharedElapsedLawDoesNotReproduce()
		{
			KingdomSubsidenceOptionRules.Snapshot real = Observed(false, 2, Late, Prior);
			KingdomSubsidenceOptionRules.Snapshot forgedAction = new KingdomSubsidenceOptionRules.Snapshot(
				true, Prior, new KingdomElapsedOptionDecision(true,
					new KingdomElapsedOptionRecord(KingdomElapsedOptionState.Enabled, Late, 2),
					KingdomElapsedOptionTransition.Enabled, KingdomElapsedOptionAction.AnchorEnabled));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, forgedAction,
				out KingdomSubsidenceOptionIntent refused));
			KingdomSubsidenceOptionRules.Snapshot swappedPrior = new KingdomSubsidenceOptionRules.Snapshot(
				true, "v1|D|100|2", real.Decision);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, swappedPrior, out refused));
			KingdomSubsidenceOptionRules.Snapshot regressedToken = new KingdomSubsidenceOptionRules.Snapshot(
				true, "v1|D|100|9", real.Decision);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, regressedToken, out refused));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due,
				new KingdomSubsidenceOptionRules.Snapshot(true, "", real.Decision), out refused));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due,
				new KingdomSubsidenceOptionRules.Snapshot(false, Prior, real.Decision), out refused));
			Assert.IsNull(refused);
		}

		[Test]
		public void PrepareRefusesUnfrozenClocksAndStepPositionsItCannotProve()
		{
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Admitted(), Late + 1,
				Observed(false, 2, Late, Prior), out KingdomSubsidenceOptionIntent refused));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Admitted(), -1,
				Observed(false, 2, Late, Prior), out refused));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Anchor + 1,
				Observed(false, 2, Late, Prior), out refused));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), 0,
				Observed(false, 2, Late, Prior), out refused));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Anchor,
				Observed(false, 2, Due - 300, Prior), out refused));
			KingdomSubsidenceStepBook book = Begin();
			KingdomSubsidenceStepBook quarantined = book.With(book.Active.Copy(
				phase: KingdomSubsidenceStepPhase.Quarantined, fault: "torn"), book.Sequence);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(quarantined, out string _));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(quarantined, Due,
				Observed(false, 2, Late, Prior), out refused));
			Assert.IsNull(refused);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Anchor,
				Observed(false, 2, Due, Prior), out KingdomSubsidenceOptionIntent accepted));
			Assert.AreEqual(Anchor, accepted.BeforeTick);
		}

		[Test]
		public void ActiveStepMatchesOnlyItsOwnSequenceIdentityAndReceipt()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			Assert.AreEqual(1L, intent.Sequence);
			Assert.AreEqual(0L, intent.RetiredTick);
			Assert.AreEqual(Due, intent.StepDueTick);
			Assert.AreEqual(Begin().Active.Id, intent.StepId);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Begin()));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, null));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(With(intent, sequence: 5L), Begin()));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(
				With(intent, step: Begin(2).Active.Id), Begin()));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(
				With(intent, before: Late, due: Late), Begin()));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(With(intent, retired: 7L), Begin()));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Retired(true), Due,
				Due + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, 1,
				out KingdomSubsidenceStepBook second, 0, "water"));
			Assert.AreEqual(2L, second.Sequence);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, second));
		}

		[Test]
		public void RetiredStepMatchesFullQuotaDueOrPartialCancellationTickOnly()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			Assert.AreEqual(Due, Retired(true).LastRetiredTick);
			Assert.AreEqual(Late, Retired(false).LastRetiredTick);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(true)));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(false)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(false, Late + 200)));
		}

		[Test]
		public void IntentWithoutAStepMatchesOnlyItsOwnRetiredReceipt()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Retired(true), Due);
			Assert.AreEqual("", intent.StepId);
			Assert.AreEqual(0L, intent.StepDueTick);
			Assert.AreEqual(Due, intent.RetiredTick);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(true)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(false)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Begin()));
			KingdomSubsidenceOptionIntent empty = Prepared(Admitted(), Anchor);
			Assert.AreEqual(0L, empty.Sequence);
			Assert.AreEqual(0L, empty.RetiredTick);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(empty, Admitted()));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(empty, Retired(true)));
		}

		[TestCase(Anchor, true)]
		[TestCase(Due, true)]
		[TestCase(Late, true)]
		[TestCase(0L, false)]
		[TestCase(Anchor + 1L, false)]
		[TestCase(Due - 1L, false)]
		[TestCase(Late + 1L, false)]
		public void CheckpointAcceptsOnlyProvenObservationsAndReturnsTheFrozenTick(long observed, bool allowed)
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			KingdomSubsidenceStepBook retired = Retired(true);
			Assert.AreEqual(allowed, KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, retired,
				observed, out long target));
			Assert.AreEqual(allowed ? Late : 0L, target);
		}

		[Test]
		public void CheckpointRefusesALiveStepAndAForeignBook()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Begin()));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, Begin(), Anchor,
				out long target));
			Assert.AreEqual(0L, target);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent,
				Retired(false, Late + 200), Anchor, out target));
			Assert.AreEqual(0L, target);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, null, Anchor, out target));
			Assert.AreEqual(0L, target);
		}

		[TestCase(Prior, KingdomElapsedOptionTransition.Disabled, KingdomElapsedOptionAction.AnchorDisabled)]
		[TestCase(null, KingdomElapsedOptionTransition.InitializedEnabled, KingdomElapsedOptionAction.AnchorEnabled)]
		public void SnapshotRecoveryReproducesTheFrozenDecisionWithoutALiveOption(string prior,
			KingdomElapsedOptionTransition transition, KingdomElapsedOptionAction action)
		{
			KingdomSubsidenceOptionRules.Snapshot original = Observed(prior == null, 2, Late, prior);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, original,
				out KingdomSubsidenceOptionIntent intent));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TrySnapshot(intent,
				out KingdomSubsidenceOptionRules.Snapshot recovered));
			Assert.IsTrue(recovered.Decision.Valid);
			Assert.AreEqual(original.Present, recovered.Present);
			Assert.AreEqual(original.PriorWire, recovered.PriorWire);
			Assert.AreEqual(original.NextWire, recovered.NextWire);
			Assert.AreEqual(transition, recovered.Decision.Transition);
			Assert.AreEqual(action, recovered.Decision.Action);
			Assert.AreEqual(original.Decision.Record.State, recovered.Decision.Record.State);
			Assert.AreEqual(Late, recovered.Decision.Record.ObservedTick);
			Assert.AreEqual(2L, recovered.Decision.Record.MasterResumeToken);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TrySnapshot(With(intent, next: ""),
				out KingdomSubsidenceOptionRules.Snapshot refused));
			Assert.IsNull(refused);
		}

		[Test]
		public void ValidRefusesClocksThatCannotBelongToTheFrozenStep()
		{
			KingdomSubsidenceOptionIntent anchored = Prepared(Begin(), Anchor);
			KingdomSubsidenceOptionIntent due = Prepared(Begin(), Due);
			KingdomSubsidenceOptionIntent retired = Prepared(Retired(true), Due);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(anchored, retired: Anchor + 1L)));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(With(anchored, retired: Anchor)));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(retired));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(retired, retired: 0L)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(anchored, due: Due + 1L)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(due, retired: Due)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(
				With(anchored, before: 1500L, due: 6300L)));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(
				With(anchored, before: 1000L, due: 5800L)));
		}

		[Test]
		public void CancelledActiveStepMustCarryTheFrozenTransitionTickAndToken()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			Assert.AreEqual(Late, Cancelled(Late, 2).Active.CancelTick);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, Cancelled(Late, 2)));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Cancelled(Late, 2)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, Cancelled(Late, 3)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Cancelled(Late, 3)));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Cancelled(Late + 100, 2)));
		}

		[Test]
		public void ShapeBindingNeverConsultsTheBookValidator()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			KingdomSubsidenceStepBook book = Begin();
			KingdomSubsidenceStepBook broken = book.With(book.Active.Copy(completed: 6), book.Sequence);
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(broken, out string _));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, broken));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, broken));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, null));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, broken, Anchor,
				out long target));
			Assert.AreEqual(0L, target);
		}

		[Test]
		public void CheckpointAcceptsARetiredReceiptOnlyForTheIntentsOwnStep()
		{
			KingdomSubsidenceStepBook retired = Retired(true);
			Assert.AreEqual(Due, retired.LastRetiredTick);
			KingdomSubsidenceOptionIntent stepless = Prepared(retired, Late);
			Assert.AreEqual("", stepless.StepId);
			Assert.AreEqual(Late, stepless.BeforeTick);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(stepless, retired));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(stepless, retired, Due,
				out long target));
			Assert.AreEqual(0L, target);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryCheckpoint(stepless, retired, Late, out target));
			Assert.AreEqual(Late, target);
			KingdomSubsidenceOptionIntent empty = Prepared(Admitted(), Anchor);
			Assert.AreEqual("", empty.StepId);
			Assert.AreEqual(0L, Admitted().LastRetiredTick);
			Assert.AreNotEqual(0L, empty.BeforeTick);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(empty, Admitted(), 0L, out target));
			Assert.AreEqual(0L, target);
			KingdomSubsidenceOptionIntent owned = Prepared(Begin(), Anchor);
			Assert.AreNotEqual("", owned.StepId);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryCheckpoint(owned, retired, Due, out target));
			Assert.AreEqual(Late, target);
		}

		// Shape binding never consults the book validator, so it must refuse these itself.
		[Test]
		public void ActiveStepShapeRequiresItsOwnEdgeAndNoActivityAfterTheFreeze()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			KingdomSubsidenceStepBook book = Begin();
			Assert.AreEqual(Due, book.Active.LastActivityTick);
			KingdomSubsidenceStepBook advanced = book.With(
				book.Active.Copy(lastActivityTick: Late + 1L), book.Sequence);
			Assert.IsTrue(KingdomSubsidenceStepRules.Valid(advanced));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, advanced));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, advanced));
			KingdomSubsidenceStepOperation op = book.Active;
			KingdomSubsidenceStepBook shifted = book.With(new KingdomSubsidenceStepOperation(op.Id,
				op.AnchorTick + 1L, op.DueTick, op.FromStage, op.ReachedStage, op.Quota, op.Completed,
				op.Phase, op.PendingDepartureId, op.PendingCredited, op.CancelRequested, op.CancelTick,
				op.CancelToken, op.RungModel, op.Fault, op.StorageCapacity, op.BindingSupport,
				op.LastActivityTick, op.CreditedDepartureIds, op.PendingIdentity), book.Sequence);
			Assert.AreEqual(op.Id, shifted.Active.Id);
			Assert.AreNotEqual(intent.BeforeTick, shifted.Active.AnchorTick);
			Assert.AreNotEqual(intent.BeforeTick, shifted.Active.DueTick);
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, shifted));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, book));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, book));
		}

		[Test]
		public void SteplessIntentCannotCarryARetiredReceiptWithoutASequence()
		{
			KingdomSubsidenceOptionIntent empty = Prepared(Admitted(), Anchor);
			Assert.AreEqual(0L, empty.Sequence);
			Assert.AreEqual(0L, empty.RetiredTick);
			Assert.AreEqual("", empty.StepId);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(empty));
			Assert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(empty, retired: 5L)));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(empty, out string wire));
			Assert.AreEqual(wire, Forge(wire, Prior, empty.NextWire, empty.BeforeTick, 0L, 0L, "", 0L));
			Refuses(Forge(wire, Prior, empty.NextWire, empty.BeforeTick, 0L, 5L, "", 0L));
		}
	}
}
#endif
