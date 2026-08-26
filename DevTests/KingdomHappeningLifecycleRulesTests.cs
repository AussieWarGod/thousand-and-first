using System;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomHappeningLifecycleRulesTests
	{
		private static KingdomHappeningParticipant Person(int id, int targetX = 11)
		{
			return new KingdomHappeningParticipant(id, "body-" + id, "resident " + id,
				"home-" + id, "JoppaWorld.1.1.1.1.10.10", 10 + id, 10, targetX,
				12, 40 + id, 2, false, false, true);
		}

		private static KingdomHappeningProposal Proposal(bool external = false,
			KingdomHappeningParticipant[] people = null)
		{
			KingdomPhysicalHappeningKind kind = external
				? KingdomPhysicalHappeningKind.Raising
				: KingdomPhysicalHappeningKind.Wedding;
			int subjectB = external ? 0 : 2;
			return new KingdomHappeningProposal("taf:happening:settlement-a:"
				+ (external ? "4" : "1") + ":2400:1:" + subjectB + ":0",
				kind, 2400L, 1, subjectB, 0, "settlement-a",
				"JoppaWorld.1.1.1.1.10", "fixture-1", "r_KingdomBench", 12, 12,
				true, external, external ? "" : "one and two were married",
				external ? "" : "word came that one and two were married",
				external ? "" : "attended ledger", external ? "" : "unattended ledger",
				external ? "" : "attended message", external ? "" : "unattended message",
				"", "gathering bench", "",
				people ?? new[] { Person(1), Person(2, 13) });
		}

		private static KingdomHappeningLifecycleBook Open(bool external = false)
		{
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty, Proposal(external), 2500L,
				out KingdomHappeningLifecycleBook book,
				out KingdomHappeningLifecycleFault fault), fault.ToString());
			return book;
		}

		private static KingdomHappeningLifecycleBook Restored(
			KingdomHappeningLifecycleBook book, long tick = 2700L)
		{
			Assert.IsTrue(KingdomHappeningLifecycleRules.TrySetPhase(book,
				book.Active.EventId, book.Active.Phase,
				KingdomHappeningLifecyclePhase.Restoring, book.Active.Attended, 0L, tick,
				out book, out KingdomHappeningLifecycleFault fault), fault.ToString());
			for (int i = 0; i < book.Active.Participants.Length; i++)
				Assert.IsTrue(KingdomHappeningLifecycleRules.TryMarkRestored(book,
					book.Active.EventId, i, false, tick + i + 1, out book, out fault),
					fault.ToString());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryMarkRestored(book,
				book.Active.EventId, -1, true, tick + 10, out book, out fault),
				fault.ToString());
			return book;
		}

		private static int PhysicalBoolOffset(byte[] payload)
		{
			using (MemoryStream stream = new MemoryStream(payload, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				reader.ReadInt32();
				reader.ReadInt32();
				reader.ReadInt32();
				reader.ReadByte();
				reader.ReadInt32();
				SkipString(reader);
				reader.ReadByte();
				reader.ReadByte();
				for (int i = 0; i < 4; i++) reader.ReadInt64();
				for (int i = 0; i < 3; i++) reader.ReadInt32();
				for (int i = 0; i < 4; i++) SkipString(reader);
				reader.ReadInt32();
				reader.ReadInt32();
				return (int)stream.Position;
			}
		}

		private static void SkipString(BinaryReader reader)
		{
			int count = reader.ReadInt32();
			reader.ReadBytes(count);
		}

		[Test]
		public void Open_FreezesParticipantsAndRefusesSecondOperation()
		{
			KingdomHappeningParticipant[] source = { Person(1), Person(2, 13) };
			KingdomHappeningLifecycleBook book;
			KingdomHappeningLifecycleFault fault;
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty, Proposal(false, source), 2500L,
				out book, out fault));
			source[0] = Person(9);
			Assert.AreEqual(1, book.Active.Participants[0].ResidentId);
			KingdomHappeningParticipant[] copy = book.Active.CopyParticipants();
			copy[0] = Person(8);
			Assert.AreEqual(1, book.Active.Participants[0].ResidentId);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryOpen(book, Proposal(), 2600L,
				out KingdomHappeningLifecycleBook unchanged, out fault));
			Assert.AreSame(book, unchanged);
			Assert.AreEqual(KingdomHappeningLifecycleFault.Busy, fault);
		}

		[Test]
		public void Wire_RoundTripsEveryFrozenReceiptAndRejectsDriftMalformedAndBudget()
		{
			KingdomHappeningLifecycleBook book = Open();
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryEncode(book, out string wire));
			Assert.LessOrEqual(wire.Length, KingdomHappeningLifecycleRules.MaxWireChars);
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryDecode(wire,
				out KingdomHappeningLifecycleBook decoded,
				out KingdomHappeningLifecycleFault fault), fault.ToString());
			Assert.AreEqual(book.Sequence, decoded.Sequence);
			Assert.AreEqual(book.Active.EventId, decoded.Active.EventId);
			Assert.AreEqual(book.Active.Participants[1].TargetX,
				decoded.Active.Participants[1].TargetX);

			byte[] drift = Convert.FromBase64String(wire);
			BitConverter.GetBytes(KingdomHappeningLifecycleRules.CurrentVersion + 1)
				.CopyTo(drift, 4);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryDecode(
				Convert.ToBase64String(drift), out decoded, out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.UnsupportedVersion, fault);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryDecode("not base64", out decoded,
				out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.Malformed, fault);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryDecode(new string('A',
				KingdomHappeningLifecycleRules.MaxWireChars + 1), out decoded, out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.OverBudget, fault);

			byte[] noncanonical = Convert.FromBase64String(wire);
			noncanonical[PhysicalBoolOffset(noncanonical)] = 2;
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryDecode(
				Convert.ToBase64String(noncanonical), out decoded, out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.Malformed, fault);
		}

		[Test]
		public void WireV1_MigratesWithNoSemanticOrRestorationReceipts()
		{
			KingdomHappeningLifecycleBook book = Open();
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryEncodeV1ForTests(book,
				out string wire));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryDecode(wire, out book,
				out KingdomHappeningLifecycleFault fault), fault.ToString());
			Assert.AreEqual(0, book.SemanticReceipts.Length);
			Assert.IsFalse(book.Active.FixtureRestored);
			Assert.IsFalse(book.Active.Participants[0].Restored);
		}

		[TestCase(KingdomHappeningLifecyclePhase.Prepared, true, true, false, false, 2501L,
			KingdomHappeningResumeAction.PreparePosts)]
		[TestCase(KingdomHappeningLifecyclePhase.Walking, true, true, false, false, 2501L,
			KingdomHappeningResumeAction.WaitForArrival)]
		[TestCase(KingdomHappeningLifecyclePhase.Walking, true, true, true, false, 2501L,
			KingdomHappeningResumeAction.BeginHold)]
		[TestCase(KingdomHappeningLifecyclePhase.Holding, true, true, true, true, 2520L,
			KingdomHappeningResumeAction.WaitHold)]
		[TestCase(KingdomHappeningLifecyclePhase.Holding, true, true, true, true, 2600L,
			KingdomHappeningResumeAction.Publish)]
		[TestCase(KingdomHappeningLifecyclePhase.Ready, true, true, true, true, 2600L,
			KingdomHappeningResumeAction.Publish)]
		[TestCase(KingdomHappeningLifecyclePhase.Restoring, false, false, false, false, 2600L,
			KingdomHappeningResumeAction.Restore)]
		internal void ResumeAction_PhasesAreExplicitAndBounded(
			KingdomHappeningLifecyclePhase phase, bool fixture, bool participants,
			bool arrived, bool receipt, long now, KingdomHappeningResumeAction expected)
		{
			KingdomHappeningLifecycleBook book = Open();
			KingdomHappeningOperation op = book.Active.WithPhase(phase,
				phase == KingdomHappeningLifecyclePhase.Ready, phase ==
				KingdomHappeningLifecyclePhase.Holding ? 2550L : 0L, 2500L);
			Assert.AreEqual(expected, KingdomHappeningLifecycleRules.ResumeAction(op, now,
				true, fixture, participants, arrived, receipt));
		}

		[Test]
		public void ResumeAction_LeavingLocusLossAndTimeoutAlwaysRestore()
		{
			KingdomHappeningOperation walking = Open().Active.WithPhase(
				KingdomHappeningLifecyclePhase.Walking, false, 0L, 2500L);
			Assert.AreEqual(KingdomHappeningResumeAction.Restore,
				KingdomHappeningLifecycleRules.ResumeAction(walking, 2501L, false, true,
					true, false, false));
			Assert.AreEqual(KingdomHappeningResumeAction.Restore,
				KingdomHappeningLifecycleRules.ResumeAction(walking,
					walking.StartedTick + KingdomHappeningLifecycleRules.WalkTimeoutTicks,
					true, true, true, false, false));
			Assert.AreEqual(KingdomHappeningResumeAction.Restore,
				KingdomHappeningLifecycleRules.ResumeAction(walking, 2501L, true, false,
					true, false, false));
		}

		[Test]
		public void ExternalSemantic_WaitsAtReadyUntilOwnerAcknowledges()
		{
			KingdomHappeningOperation ready = Open(external: true).Active.WithPhase(
				KingdomHappeningLifecyclePhase.Ready, true, 0L, 2600L);
			Assert.AreEqual(KingdomHappeningResumeAction.WaitExternal,
				KingdomHappeningLifecycleRules.ResumeAction(ready, 2601L, true, true, true,
					true, true));
			Assert.IsTrue(KingdomHappeningLifecycleRules.SinksSettled(ready));
			Assert.AreEqual(KingdomHappeningResumeAction.Restore,
				KingdomHappeningLifecycleRules.ResumeAction(ready,
					ready.UpdatedTick + KingdomHappeningLifecycleRules.ExternalReadyTimeoutTicks,
					true, true, true, true, true));
		}

		[Test]
		public void ReportOnly_HasNoProxyBodyOrFixtureAndPublishesFromDurableReady()
		{
			KingdomHappeningProposal report = new KingdomHappeningProposal(
				"taf:happening:settlement-a:2:2600:7:0:1",
				KingdomPhysicalHappeningKind.Funeral, 2600L, 7, 0, 1, "settlement-a",
				"", "", "", 0, 0, false, false, "attended funeral",
				"dated report only", "", "", "", "", "", "shrine", "", null);
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty, report, 2700L,
				out KingdomHappeningLifecycleBook book,
				out KingdomHappeningLifecycleFault fault), fault.ToString());
			Assert.IsFalse(book.Active.Physical);
			Assert.IsFalse(book.Active.Attended);
			Assert.AreEqual(0, book.Active.Participants.Length);
			Assert.AreEqual("", book.Active.FixtureObjectId);
			Assert.AreEqual(KingdomHappeningLifecyclePhase.Ready, book.Active.Phase);
			Assert.AreEqual(KingdomHappeningResumeAction.Publish,
				KingdomHappeningLifecycleRules.ResumeAction(book.Active, 2800L, false, false,
					false, false, false));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryEncode(book, out string wire));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryDecode(wire, out book, out fault));
			Assert.IsFalse(book.Active.Physical);
			Assert.AreEqual("dated report only", book.Active.ChronicleUnattended);
		}

		[Test]
		public void InterruptedUninspectableSinksBecomeLostButInspectableSinksRetry()
		{
			KingdomHappeningLifecycleBook book = Open();
			book = new KingdomHappeningLifecycleBook(book.Sequence,
				book.Active.WithPhase(KingdomHappeningLifecyclePhase.Ready, true, 0L, 2600L),
				book.SemanticReceipts);
			KingdomHappeningLifecycleFault fault;
			Assert.IsTrue(KingdomHappeningLifecycleRules.TrySetSinks(book,
				book.Active.EventId, KingdomHappeningSinkState.Pending,
				KingdomHappeningSinkState.Pending, KingdomHappeningSinkState.Skipped,
				KingdomHappeningSinkState.Attempting, KingdomHappeningSinkState.Attempting,
				2600L, out book, out fault));
			book = KingdomHappeningLifecycleRules.RecoverInterruptedSinks(book, 2700L);
			Assert.AreEqual(KingdomHappeningSinkState.Pending, book.Active.ChronicleState);
			Assert.AreEqual(KingdomHappeningSinkState.Pending, book.Active.ToldState);
			Assert.AreEqual(KingdomHappeningSinkState.Skipped, book.Active.EffectState);
			Assert.AreEqual(KingdomHappeningSinkState.Lost, book.Active.LedgerState);
			Assert.AreEqual(KingdomHappeningSinkState.Lost, book.Active.MessageState);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TrySetSinks(book,
				book.Active.EventId, KingdomHappeningSinkState.Pending,
				book.Active.ToldState, book.Active.EffectState, KingdomHappeningSinkState.Pending,
				book.Active.MessageState, 2800L, out _, out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.WrongPhase, fault);
		}

		[Test]
		public void SequenceNeverReusesAfterClearAndEventMatchIsDeterministic()
		{
			KingdomHappeningLifecycleBook book = Open();
			string id = book.Active.EventId;
			Assert.IsTrue(KingdomHappeningLifecycleRules.Matches(book.Active,
				KingdomPhysicalHappeningKind.Wedding, 9999L, 1, 2, 0),
				"a pending wedding remains the same pair across a day boundary");
			book = Restored(book);
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryClear(book, id, out book,
				out KingdomHappeningLifecycleFault fault));
			Assert.AreEqual(1, book.Sequence);
			Assert.IsTrue(KingdomHappeningLifecycleRules.AlreadyCompleted(book,
				KingdomPhysicalHappeningKind.Wedding, 1, 2));
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryOpen(book, Proposal(), 3000L,
				out KingdomHappeningLifecycleBook unchanged, out fault));
			Assert.AreSame(book, unchanged);
			Assert.AreEqual(KingdomHappeningLifecycleFault.AlreadyCompleted, fault);
			Assert.AreEqual(1, book.Sequence);
		}

		[Test]
		public void PermanentReceipt_RoundTripsCanonicallyAndFreezesCallerArrays()
		{
			KingdomHappeningLifecycleBook book = Restored(Open());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryClear(book,
				book.Active.EventId, out book, out KingdomHappeningLifecycleFault fault),
				fault.ToString());
			KingdomHappeningSemanticReceipt[] copy = book.CopySemanticReceipts();
			copy[0] = new KingdomHappeningSemanticReceipt(
				KingdomPhysicalHappeningKind.Funeral, 9, 0);
			Assert.IsTrue(KingdomHappeningLifecycleRules.AlreadyCompleted(book,
				KingdomPhysicalHappeningKind.Wedding, 2, 1));
			Assert.IsFalse(KingdomHappeningLifecycleRules.AlreadyCompleted(book,
				KingdomPhysicalHappeningKind.Funeral, 9, 0));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryEncode(book, out string wire));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryDecode(wire, out book, out fault),
				fault.ToString());
			Assert.AreEqual(1, book.SemanticReceipts.Length);
			Assert.AreEqual(KingdomPhysicalHappeningKind.Wedding,
				book.SemanticReceipts[0].Kind);
			Assert.AreEqual(1, book.SemanticReceipts[0].SubjectA);
			Assert.AreEqual(2, book.SemanticReceipts[0].SubjectB);
		}

		[Test]
		public void PermanentReceipt_RefusesCapacityBeforeStagingAndDuplicateWire()
		{
			KingdomHappeningSemanticReceipt[] full = new KingdomHappeningSemanticReceipt[
				KingdomHappeningLifecycleRules.MaxSemanticReceipts];
			int at = 0;
			for (int a = 1; a <= KingdomCityState.MaxResidents; a++)
			{
				for (int b = a + 1; b <= KingdomCityState.MaxResidents; b++)
					full[at++] = new KingdomHappeningSemanticReceipt(
						KingdomPhysicalHappeningKind.Wedding, a, b);
				full[at++] = new KingdomHappeningSemanticReceipt(
					KingdomPhysicalHappeningKind.Funeral, a, 0);
			}
			for (int work = 1; work <= KingdomCityState.MaxWorks; work++)
				full[at++] = new KingdomHappeningSemanticReceipt(
					KingdomPhysicalHappeningKind.Raising, work, 0);
			Assert.AreEqual(full.Length, at);
			KingdomHappeningLifecycleBook capped = new KingdomHappeningLifecycleBook(7,
				null, full);
			KingdomHappeningProposal funeral = new KingdomHappeningProposal(
				"taf:happening:settlement-a:2:3000:61:0:0",
				KingdomPhysicalHappeningKind.Funeral, 3000L, 61, 0, 0, "settlement-a",
				"", "", "", 0, 0, false, false, "funeral", "dated funeral", "", "",
				"", "", "", "", "", null);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryOpen(capped, funeral, 3001L,
				out KingdomHappeningLifecycleBook unchanged,
				out KingdomHappeningLifecycleFault fault));
			Assert.AreSame(capped, unchanged);
			Assert.AreEqual(KingdomHappeningLifecycleFault.OverBudget, fault);

			KingdomHappeningLifecycleBook one = Restored(Open());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryClear(one,
				one.Active.EventId, out one, out fault));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryEncode(one, out string wire));
			byte[] single = Convert.FromBase64String(wire);
			byte[] duplicate = new byte[single.Length + 9];
			Array.Copy(single, duplicate, single.Length);
			BitConverter.GetBytes(2).CopyTo(duplicate, 13);
			Array.Copy(single, 17, duplicate, single.Length, 9);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryDecode(
				Convert.ToBase64String(duplicate), out _, out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.Malformed, fault);

			KingdomHappeningProposal reversedWedding = new KingdomHappeningProposal(
				"taf:happening:settlement-a:1:2500:2:1:0",
				KingdomPhysicalHappeningKind.Wedding, 2500L, 2, 1, 0, "settlement-a",
				"zone", "fixture", "r_KingdomBench", 1, 1, true, false, "a", "b",
				"", "", "", "", "", "", "", new[] { Person(1) });
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty, reversedWedding, 2501L, out unchanged,
				out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.Malformed, fault);
		}

		[Test]
		public void Clear_WaitsForEveryBodyAndFixtureRestorationAcknowledgement()
		{
			KingdomHappeningLifecycleBook book = Open();
			string id = book.Active.EventId;
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryClear(book, id, out _,
				out KingdomHappeningLifecycleFault fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.WrongPhase, fault);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TrySetPhase(book, id,
				KingdomHappeningLifecyclePhase.Prepared, KingdomHappeningLifecyclePhase.Ready,
				true, 0L, 2700L, out _, out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.WrongPhase, fault);
			Assert.IsTrue(KingdomHappeningLifecycleRules.TrySetPhase(book, id,
				KingdomHappeningLifecyclePhase.Prepared,
				KingdomHappeningLifecyclePhase.Restoring, false, 0L, 2700L, out book,
				out fault));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryMarkRestored(book, id, 0,
				false, 2701L, out book, out fault));
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryClear(book, id, out _, out fault));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryMarkRestored(book, id, 1,
				false, 2702L, out book, out fault));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryMarkRestored(book, id, -1,
				true, 2703L, out book, out fault));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryClear(book, id, out book,
				out fault));
			Assert.IsNull(book.Active);
		}

		[Test]
		public void ParticipantAndTextCapsRefuseBeforePublication()
		{
			KingdomHappeningParticipant[] tooMany = new KingdomHappeningParticipant[
				KingdomHappeningLifecycleRules.MaxParticipants + 1];
			for (int i = 0; i < tooMany.Length; i++) tooMany[i] = Person(i + 1);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty, Proposal(false, tooMany), 2500L,
				out KingdomHappeningLifecycleBook unchanged,
				out KingdomHappeningLifecycleFault fault));
			Assert.IsNull(unchanged.Active);
			Assert.AreEqual(KingdomHappeningLifecycleFault.Malformed, fault);

			KingdomHappeningParticipant duplicateObject = new KingdomHappeningParticipant(2,
				"body-1", "resident 2", "home-2", "anchor-2", 12, 10, 13, 12, 42,
				2, false, false, true);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty,
				Proposal(false, new[] { Person(1), duplicateObject }), 2500L,
				out unchanged, out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.Malformed, fault);

			KingdomHappeningParticipant invalidPost = new KingdomHappeningParticipant(2,
				"body-2", "resident 2", "home-2", "anchor-2", 12, 10, 13, 12, 42,
				999, false, false, true);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty,
				Proposal(false, new[] { Person(1), invalidPost }), 2500L,
				out unchanged, out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.Malformed, fault);

			string huge = new string('x', KingdomHappeningLifecycleRules.MaxStringBytes + 1);
			KingdomHappeningProposal malformed = new KingdomHappeningProposal(huge,
				KingdomPhysicalHappeningKind.Feast, 2400L, 0, 0, 1, "settlement-a",
				"zone", "fixture", "Campfire", 1, 1, true, false, "a", "b", "", "",
				"", "", "", "", "", new[] { Person(1) });
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty, malformed, 2500L, out unchanged,
				out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.Malformed, fault);

			KingdomHappeningParticipant sameTarget = Person(2);
			Assert.IsFalse(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty,
				Proposal(false, new[] { Person(1), sameTarget }), 2500L, out unchanged,
				out fault));
			Assert.AreEqual(KingdomHappeningLifecycleFault.Malformed, fault);
		}
	}
}
