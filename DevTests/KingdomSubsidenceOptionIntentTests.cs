#if TAF_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(fresh, Realm, Settlement,
				out KingdomSubsidenceStepBook admitted));
			return admitted;
		}

		private static KingdomSubsidenceStepBook Begin(int quota = 1)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), Anchor, Due, GrowthStage.City,
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
			ClassicAssert.IsTrue(KingdomResidentDepartureRules.Valid(departure));
			return departure;
		}

		private static KingdomSubsidenceStepBook Retired(bool fullQuota, long cancelTick = Late)
		{
			KingdomSubsidenceStepBook book = Begin();
			if (fullQuota)
			{
				KingdomResidentDepartureOperation departure = Departure();
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId,
					GrowthStage.City, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			}
			else ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, cancelTick, 7, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long target));
			ClassicAssert.AreEqual(fullQuota ? Due : cancelTick, target);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, target, out book));
			return book;
		}

		private static KingdomSubsidenceStepBook Cancelled(long tick, long token)
		{
			KingdomSubsidenceStepBook book = Begin();
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, tick, token, out book));
			ClassicAssert.IsTrue(book.Active.CancelRequested);
			return book;
		}

		private static KingdomSubsidenceOptionRules.Snapshot Observed(bool enabled, long token,
			long now, string prior)
		{
			KingdomDurableKeyObservation reading = new KingdomDurableKeyObservation
			{ HasString = prior != null, String = prior };
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(reading, enabled,
				token, now, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			ClassicAssert.IsTrue(decision.Valid);
			ClassicAssert.IsNotNull(snapshot);
			return snapshot;
		}

		private static KingdomSubsidenceOptionIntent Prepared(KingdomSubsidenceStepBook book,
			long beforeTick, string prior = Prior)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(book, beforeTick,
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
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryDecode(wire,
				out KingdomSubsidenceOptionIntent decoded), wire);
			ClassicAssert.IsNull(decoded);
		}

		[Test]
		public void CanonicalRoundTripKeepsAbsentAndPresentPriorEvidenceApart()
		{
			KingdomSubsidenceOptionIntent present = Prepared(Begin(), Due);
			KingdomSubsidenceOptionIntent absent = Prepared(Begin(), Due, null);
			ClassicAssert.IsTrue(present.PriorPresent);
			ClassicAssert.AreEqual(Prior, present.PriorWire);
			ClassicAssert.AreEqual(Next, present.NextWire);
			ClassicAssert.IsFalse(absent.PriorPresent);
			ClassicAssert.IsNull(absent.PriorWire);
			ClassicAssert.AreEqual("v1|E|5800|2", absent.NextWire);
			foreach (KingdomSubsidenceOptionIntent original in new[] { present, absent })
			{
				ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(original, out string wire));
				ClassicAssert.IsTrue(wire.StartsWith("so1:", StringComparison.Ordinal));
				ClassicAssert.IsTrue(wire.Length <= KingdomSubsidenceOptionIntentRules.MaxWireChars);
				ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryDecode(wire,
					out KingdomSubsidenceOptionIntent restored));
				ClassicAssert.AreNotSame(original, restored);
				ClassicAssert.AreEqual(original.PriorPresent, restored.PriorPresent);
				ClassicAssert.AreEqual(original.PriorWire, restored.PriorWire);
				ClassicAssert.AreEqual(original.NextWire, restored.NextWire);
				ClassicAssert.AreEqual(original.BeforeTick, restored.BeforeTick);
				ClassicAssert.AreEqual(original.Sequence, restored.Sequence);
				ClassicAssert.AreEqual(original.RetiredTick, restored.RetiredTick);
				ClassicAssert.AreEqual(original.StepId, restored.StepId);
				ClassicAssert.AreEqual(original.StepDueTick, restored.StepDueTick);
				ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(restored, out string repeated));
				ClassicAssert.AreEqual(wire, repeated);
			}
		}

		[Test]
		public void CodecRefusesEveryNoncanonicalTruncatedOrOversizedWire()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Due);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(intent, out string wire));
			ClassicAssert.AreEqual(wire, Forge(wire, intent.PriorWire, intent.NextWire, intent.BeforeTick,
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
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryEncode(With(intent, prior: ""),
				out string refused));
			ClassicAssert.IsNull(refused);
		}

		[Test]
		public void ValidRefusesTamperedPriorNextAndStepEvidence()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(intent));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(null));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, prior: "v1|E|100|9")));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, prior: "v1|E|9000|2")));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, next: "v1|E|5800|2")));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, next: "v1|D|05800|2")));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, next: "")));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, prior: "")));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(new KingdomSubsidenceOptionIntent(
				true, null, intent.NextWire, intent.BeforeTick, intent.Sequence, intent.RetiredTick,
				intent.StepId, intent.StepDueTick)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(new KingdomSubsidenceOptionIntent(
				false, Prior, intent.NextWire, intent.BeforeTick, intent.Sequence, intent.RetiredTick,
				intent.StepId, intent.StepDueTick)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, before: -1L)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, before: Late + 1L)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, sequence: -1L)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, sequence: 0L)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, retired: -1L)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, due: -1L)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, due: 0L)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent, step: "")));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(intent,
				step: intent.StepId.Substring(0, intent.StepId.Length - 1) + "z")));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(With(intent, retired: 7L)));
		}

		[Test]
		public void PrepareRefusesUnadmittedBooksAndNonAnchorDecisions()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(fresh, 0,
				Observed(false, 2, Late, Prior), out KingdomSubsidenceOptionIntent refused));
			ClassicAssert.IsNull(refused);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(null, 0,
				Observed(false, 2, Late, Prior), out refused));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, null, out refused));
			KingdomSubsidenceOptionRules.Snapshot running = Observed(true, 2, Late, Prior);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Run, running.Decision.Action);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, running, out refused));
			KingdomSubsidenceOptionRules.Snapshot waiting = Observed(true, 2, 100, Prior);
			ClassicAssert.AreEqual(KingdomElapsedOptionAction.Wait, waiting.Decision.Action);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Anchor, waiting, out refused));
			ClassicAssert.IsNull(refused);
		}

		[Test]
		public void PrepareRefusesEvidenceTheSharedElapsedLawDoesNotReproduce()
		{
			KingdomSubsidenceOptionRules.Snapshot real = Observed(false, 2, Late, Prior);
			KingdomSubsidenceOptionRules.Snapshot forgedAction = new KingdomSubsidenceOptionRules.Snapshot(
				true, Prior, new KingdomElapsedOptionDecision(true,
					new KingdomElapsedOptionRecord(KingdomElapsedOptionState.Enabled, Late, 2),
					KingdomElapsedOptionTransition.Enabled, KingdomElapsedOptionAction.AnchorEnabled));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, forgedAction,
				out KingdomSubsidenceOptionIntent refused));
			KingdomSubsidenceOptionRules.Snapshot swappedPrior = new KingdomSubsidenceOptionRules.Snapshot(
				true, "v1|D|100|2", real.Decision);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, swappedPrior, out refused));
			KingdomSubsidenceOptionRules.Snapshot regressedToken = new KingdomSubsidenceOptionRules.Snapshot(
				true, "v1|D|100|9", real.Decision);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, regressedToken, out refused));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due,
				new KingdomSubsidenceOptionRules.Snapshot(true, "", real.Decision), out refused));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due,
				new KingdomSubsidenceOptionRules.Snapshot(false, Prior, real.Decision), out refused));
			ClassicAssert.IsNull(refused);
		}

		[Test]
		public void PrepareRefusesUnfrozenClocksAndStepPositionsItCannotProve()
		{
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Admitted(), Late + 1,
				Observed(false, 2, Late, Prior), out KingdomSubsidenceOptionIntent refused));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Admitted(), -1,
				Observed(false, 2, Late, Prior), out refused));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Anchor + 1,
				Observed(false, 2, Late, Prior), out refused));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), 0,
				Observed(false, 2, Late, Prior), out refused));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Anchor,
				Observed(false, 2, Due - 300, Prior), out refused));
			KingdomSubsidenceStepBook book = Begin();
			KingdomSubsidenceStepBook quarantined = book.With(book.Active.Copy(
				phase: KingdomSubsidenceStepPhase.Quarantined, fault: "torn"), book.Sequence);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(quarantined, out string _));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryPrepare(quarantined, Due,
				Observed(false, 2, Late, Prior), out refused));
			ClassicAssert.IsNull(refused);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Anchor,
				Observed(false, 2, Due, Prior), out KingdomSubsidenceOptionIntent accepted));
			ClassicAssert.AreEqual(Anchor, accepted.BeforeTick);
		}

		[Test]
		public void ActiveStepMatchesOnlyItsOwnSequenceIdentityAndReceipt()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			ClassicAssert.AreEqual(1L, intent.Sequence);
			ClassicAssert.AreEqual(0L, intent.RetiredTick);
			ClassicAssert.AreEqual(Due, intent.StepDueTick);
			ClassicAssert.AreEqual(Begin().Active.Id, intent.StepId);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Begin()));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, null));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(With(intent, sequence: 5L), Begin()));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(
				With(intent, step: Begin(2).Active.Id), Begin()));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(
				With(intent, before: Late, due: Late), Begin()));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(With(intent, retired: 7L), Begin()));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Retired(true), Due,
				Due + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, 1,
				out KingdomSubsidenceStepBook second, 0, "water"));
			ClassicAssert.AreEqual(2L, second.Sequence);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, second));
		}

		[Test]
		public void RetiredStepMatchesFullQuotaDueOrPartialCancellationTickOnly()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			ClassicAssert.AreEqual(Due, Retired(true).LastRetiredTick);
			ClassicAssert.AreEqual(Late, Retired(false).LastRetiredTick);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(true)));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(false)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(false, Late + 200)));
		}

		[Test]
		public void IntentWithoutAStepMatchesOnlyItsOwnRetiredReceipt()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Retired(true), Due);
			ClassicAssert.AreEqual("", intent.StepId);
			ClassicAssert.AreEqual(0L, intent.StepDueTick);
			ClassicAssert.AreEqual(Due, intent.RetiredTick);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(true)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Retired(false)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Begin()));
			KingdomSubsidenceOptionIntent empty = Prepared(Admitted(), Anchor);
			ClassicAssert.AreEqual(0L, empty.Sequence);
			ClassicAssert.AreEqual(0L, empty.RetiredTick);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(empty, Admitted()));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(empty, Retired(true)));
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
			ClassicAssert.AreEqual(allowed, KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, retired,
				observed, out long target));
			ClassicAssert.AreEqual(allowed ? Late : 0L, target);
		}

		[Test]
		public void CheckpointRefusesALiveStepAndAForeignBook()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Begin()));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, Begin(), Anchor,
				out long target));
			ClassicAssert.AreEqual(0L, target);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent,
				Retired(false, Late + 200), Anchor, out target));
			ClassicAssert.AreEqual(0L, target);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, null, Anchor, out target));
			ClassicAssert.AreEqual(0L, target);
		}

		[TestCase(Prior, KingdomElapsedOptionTransition.Disabled, KingdomElapsedOptionAction.AnchorDisabled)]
		[TestCase(null, KingdomElapsedOptionTransition.InitializedEnabled, KingdomElapsedOptionAction.AnchorEnabled)]
		public void SnapshotRecoveryReproducesTheFrozenDecisionWithoutALiveOption(string prior,
			KingdomElapsedOptionTransition transition, KingdomElapsedOptionAction action)
		{
			KingdomSubsidenceOptionRules.Snapshot original = Observed(prior == null, 2, Late, prior);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(Begin(), Due, original,
				out KingdomSubsidenceOptionIntent intent));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TrySnapshot(intent,
				out KingdomSubsidenceOptionRules.Snapshot recovered));
			ClassicAssert.IsTrue(recovered.Decision.Valid);
			ClassicAssert.AreEqual(original.Present, recovered.Present);
			ClassicAssert.AreEqual(original.PriorWire, recovered.PriorWire);
			ClassicAssert.AreEqual(original.NextWire, recovered.NextWire);
			ClassicAssert.AreEqual(transition, recovered.Decision.Transition);
			ClassicAssert.AreEqual(action, recovered.Decision.Action);
			ClassicAssert.AreEqual(original.Decision.Record.State, recovered.Decision.Record.State);
			ClassicAssert.AreEqual(Late, recovered.Decision.Record.ObservedTick);
			ClassicAssert.AreEqual(2L, recovered.Decision.Record.MasterResumeToken);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TrySnapshot(With(intent, next: ""),
				out KingdomSubsidenceOptionRules.Snapshot refused));
			ClassicAssert.IsNull(refused);
		}

		[Test]
		public void ValidRefusesClocksThatCannotBelongToTheFrozenStep()
		{
			KingdomSubsidenceOptionIntent anchored = Prepared(Begin(), Anchor);
			KingdomSubsidenceOptionIntent due = Prepared(Begin(), Due);
			KingdomSubsidenceOptionIntent retired = Prepared(Retired(true), Due);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(anchored, retired: Anchor + 1L)));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(With(anchored, retired: Anchor)));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(retired));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(retired, retired: 0L)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(anchored, due: Due + 1L)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(due, retired: Due)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(
				With(anchored, before: 1500L, due: 6300L)));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(
				With(anchored, before: 1000L, due: 5800L)));
		}

		[Test]
		public void CancelledActiveStepMustCarryTheFrozenTransitionTickAndToken()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			ClassicAssert.AreEqual(Late, Cancelled(Late, 2).Active.CancelTick);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, Cancelled(Late, 2)));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, Cancelled(Late, 2)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, Cancelled(Late, 3)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Cancelled(Late, 3)));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, Cancelled(Late + 100, 2)));
		}

		[Test]
		public void ShapeBindingNeverConsultsTheBookValidator()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			KingdomSubsidenceStepBook book = Begin();
			KingdomSubsidenceStepBook broken = book.With(book.Active.Copy(completed: 6), book.Sequence);
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(broken, out string _));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, broken));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, broken));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, null));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, broken, Anchor,
				out long target));
			ClassicAssert.AreEqual(0L, target);
		}

		[Test]
		public void CheckpointAcceptsARetiredReceiptOnlyForTheIntentsOwnStep()
		{
			KingdomSubsidenceStepBook retired = Retired(true);
			ClassicAssert.AreEqual(Due, retired.LastRetiredTick);
			KingdomSubsidenceOptionIntent stepless = Prepared(retired, Late);
			ClassicAssert.AreEqual("", stepless.StepId);
			ClassicAssert.AreEqual(Late, stepless.BeforeTick);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(stepless, retired));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(stepless, retired, Due,
				out long target));
			ClassicAssert.AreEqual(0L, target);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryCheckpoint(stepless, retired, Late, out target));
			ClassicAssert.AreEqual(Late, target);
			KingdomSubsidenceOptionIntent empty = Prepared(Admitted(), Anchor);
			ClassicAssert.AreEqual("", empty.StepId);
			ClassicAssert.AreEqual(0L, Admitted().LastRetiredTick);
			ClassicAssert.AreNotEqual(0L, empty.BeforeTick);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.TryCheckpoint(empty, Admitted(), 0L, out target));
			ClassicAssert.AreEqual(0L, target);
			KingdomSubsidenceOptionIntent owned = Prepared(Begin(), Anchor);
			ClassicAssert.AreNotEqual("", owned.StepId);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryCheckpoint(owned, retired, Due, out target));
			ClassicAssert.AreEqual(Late, target);
		}

		// Shape binding never consults the book validator, so it must refuse these itself.
		[Test]
		public void ActiveStepShapeRequiresItsOwnEdgeAndNoActivityAfterTheFreeze()
		{
			KingdomSubsidenceOptionIntent intent = Prepared(Begin(), Anchor);
			KingdomSubsidenceStepBook book = Begin();
			ClassicAssert.AreEqual(Due, book.Active.LastActivityTick);
			KingdomSubsidenceStepBook advanced = book.With(
				book.Active.Copy(lastActivityTick: Late + 1L), book.Sequence);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.Valid(advanced));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, advanced));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Matches(intent, advanced));
			KingdomSubsidenceStepOperation op = book.Active;
			KingdomSubsidenceStepBook shifted = book.With(new KingdomSubsidenceStepOperation(op.Id,
				op.AnchorTick + 1L, op.DueTick, op.FromStage, op.ReachedStage, op.Quota, op.Completed,
				op.Phase, op.PendingDepartureId, op.PendingCredited, op.CancelRequested, op.CancelTick,
				op.CancelToken, op.RungModel, op.Fault, op.StorageCapacity, op.BindingSupport,
				op.LastActivityTick, op.CreditedDepartureIds, op.PendingIdentity), book.Sequence);
			ClassicAssert.AreEqual(op.Id, shifted.Active.Id);
			ClassicAssert.AreNotEqual(intent.BeforeTick, shifted.Active.AnchorTick);
			ClassicAssert.AreNotEqual(intent.BeforeTick, shifted.Active.DueTick);
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, shifted));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.MatchesShape(intent, book));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, book));
		}

		[Test]
		public void SteplessIntentCannotCarryARetiredReceiptWithoutASequence()
		{
			KingdomSubsidenceOptionIntent empty = Prepared(Admitted(), Anchor);
			ClassicAssert.AreEqual(0L, empty.Sequence);
			ClassicAssert.AreEqual(0L, empty.RetiredTick);
			ClassicAssert.AreEqual("", empty.StepId);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Valid(empty));
			ClassicAssert.IsFalse(KingdomSubsidenceOptionIntentRules.Valid(With(empty, retired: 5L)));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(empty, out string wire));
			ClassicAssert.AreEqual(wire, Forge(wire, Prior, empty.NextWire, empty.BeforeTick, 0L, 0L, "", 0L));
			Refuses(Forge(wire, Prior, empty.NextWire, empty.BeforeTick, 0L, 5L, "", 0L));
		}
	}
}
#endif
