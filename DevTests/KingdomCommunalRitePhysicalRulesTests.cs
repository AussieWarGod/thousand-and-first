using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCommunalRitePhysicalRulesTests
	{
		private static KingdomHappeningParticipant Person()
		{
			return new KingdomHappeningParticipant(1, "body-1", "Ava", "home-1", "", 2,
				2, 3, 3, 0, 0, false, false, true);
		}

		private static KingdomHappeningProposal Proposal()
		{
			const string settlement =
				"taf:settlement:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
			const string practice =
				"taf:experience:first-feast:practice:"
				+ "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
			Assert.IsTrue(KingdomCommunalRiteRules.TryPracticeSubject(practice,
				out int subject));
			return new KingdomHappeningProposal(
				KingdomCommunalRiteRules.EventId(settlement, 30L, subject),
				KingdomPhysicalHappeningKind.CommunalRite, 30L, subject, 0, 0, settlement,
				"JoppaWorld.1.1.1.1.10", "bench-1", "r_KingdomBench", 4, 4, true,
				true, "", "", "", "", "", "", "", "First Feast practice",
				"taf:communal-rite-lease:v1:1:" + practice,
				new[] { Person() });
		}

		[Test]
		public void ExternalPracticeProjectionPersistsExactSourceAndOwnsNoSemanticReceipt()
		{
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty, Proposal(), 30L,
				out KingdomHappeningLifecycleBook book,
				out KingdomHappeningLifecycleFault fault), fault.ToString());
			Assert.AreEqual(KingdomPhysicalHappeningKind.CommunalRite, book.Active.Kind);
			StringAssert.Contains("taf:experience:first-feast:practice:", book.Active.PlanQuote);
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryEncode(book, out string wire));
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryDecode(wire, out book, out fault),
				fault.ToString());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TrySetPhase(book, book.Active.EventId,
				KingdomHappeningLifecyclePhase.Prepared, KingdomHappeningLifecyclePhase.Restoring,
				false, 0L, 31L, out book, out fault), fault.ToString());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryMarkRestored(book,
				book.Active.EventId, 0, false, 32L, out book, out fault), fault.ToString());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryMarkRestored(book,
				book.Active.EventId, -1, true, 33L, out book, out fault), fault.ToString());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryClear(book, book.Active.EventId,
				out book, out fault), fault.ToString());
			Assert.AreEqual(0, book.SemanticReceipts.Length,
				"exact D8 authority lives in its authenticated section, never an int hash receipt");
		}

		[Test]
		public void ReadyCommunalRiteCannotTimeoutBeforeCivicMemoryAcknowledges()
		{
			KingdomHappeningLifecycleBook rite = Ready(Proposal(), 30L);
			Assert.AreEqual(KingdomHappeningResumeAction.WaitExternal,
				KingdomHappeningLifecycleRules.ResumeAction(rite.Active,
					34L + KingdomHappeningLifecycleRules.ExternalReadyTimeoutTicks,
					false, false, false, false, false));

			KingdomHappeningProposal source = Proposal();
			KingdomHappeningProposal raising = new KingdomHappeningProposal(
				"taf:happening:" + source.SettlementId + ":4:30:1:0:0",
				KingdomPhysicalHappeningKind.Raising, 30L, 1, 0, 0,
				source.SettlementId, source.ZoneId, source.FixtureObjectId,
				source.FixtureBlueprint, source.FixtureX, source.FixtureY, true, true,
				"", "", "", "", "", "", "", "raising", "proof",
				source.Participants);
			KingdomHappeningLifecycleBook ordinary = Ready(raising, 30L);
			Assert.AreEqual(KingdomHappeningResumeAction.WaitExternal,
				KingdomHappeningLifecycleRules.ResumeAction(ordinary.Active,
					33L + KingdomHappeningLifecycleRules.ExternalReadyTimeoutTicks,
					false, false, false, false, false));
			Assert.AreEqual(KingdomHappeningResumeAction.Restore,
				KingdomHappeningLifecycleRules.ResumeAction(ordinary.Active,
					34L + KingdomHappeningLifecycleRules.ExternalReadyTimeoutTicks,
					false, false, false, false, false),
				"all other external semantic kinds retain their bounded timeout");
		}

		private static KingdomHappeningLifecycleBook Ready(
			KingdomHappeningProposal proposal, long tick)
		{
			Assert.IsTrue(KingdomHappeningLifecycleRules.TryOpen(
				KingdomHappeningLifecycleBook.Empty, proposal, tick,
				out KingdomHappeningLifecycleBook book,
				out KingdomHappeningLifecycleFault fault), fault.ToString());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TrySetPhase(book,
				book.Active.EventId, KingdomHappeningLifecyclePhase.Prepared,
				KingdomHappeningLifecyclePhase.Walking, false, 0L, tick + 1L,
				out book, out fault), fault.ToString());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TrySetPhase(book,
				book.Active.EventId, KingdomHappeningLifecyclePhase.Walking,
				KingdomHappeningLifecyclePhase.Holding, false, tick + 3L, tick + 2L,
				out book, out fault), fault.ToString());
			Assert.IsTrue(KingdomHappeningLifecycleRules.TrySetPhase(book,
				book.Active.EventId, KingdomHappeningLifecyclePhase.Holding,
				KingdomHappeningLifecyclePhase.Ready, true, 0L, tick + 4L,
				out book, out fault), fault.ToString());
			return book;
		}
	}
}
