#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCuriosityLeadTransactionTests
	{
		[Test]
		public void PlanWithoutAttentionChangesNeitherC18NorItsCallerReceipt()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryPlanCuriosity(authority,
				KingdomCuriosityLeadCodecTests.Cause("plan"),
				KingdomCuriosityLeadCodecTests.Notes(), out KingdomCuriosityLeadPlan plan,
				out string failure), failure);
			KingdomCuriosityReceipt caller = plan.CuriosityReceipt;
			caller.NoteText = "fabricated";
			long before = authority.Revision;
			Assert.IsFalse(KingdomCuriosityLeadTransactions.TryCommit(plan, authority,
				EnabledLedger(), out bool committed, out failure));
			Assert.IsFalse(committed); Assert.AreEqual(before, authority.Revision);
			StringAssert.Contains("audience", failure);
			Assert.AreNotEqual("fabricated", plan.CuriosityReceipt.NoteText,
				"receipts handed to callers are copies, not plan authority");
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryProveSourceAbsent(authority,
				plan.CuriosityReceipt.SourceId, out bool absent, out failure), failure);
			Assert.IsTrue(absent);
		}

		[Test]
		public void ExactAttentionPublishesAndInternalReadbackMatchesTheFrozenPlan()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomExperienceLedger ledger = EnabledLedger();
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryPlanCuriosity(authority,
				KingdomCuriosityLeadCodecTests.Cause("publish"),
				KingdomCuriosityLeadCodecTests.Notes(), out KingdomCuriosityLeadPlan plan,
				out string failure), failure);
			KingdomCuriosityReceipt expected = plan.CuriosityReceipt;
			Reserve(ledger, expected.SourceId, expected.SettlementId, expected.PreparedTick);
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryCommit(plan, authority, ledger,
				out bool committed, out failure), failure);
			Assert.IsTrue(committed);
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryReadExactCuriosity(authority,
				expected, out KingdomCuriosityReceipt durable, out failure), failure);
			Assert.AreEqual(expected.NoteId, durable.NoteId);

			KingdomCuriosityReceipt fabricated = expected.Copy(); fabricated.Reason += " changed";
			Assert.IsFalse(KingdomCuriosityLeadTransactions.TryReadExactCuriosity(authority,
				fabricated, out _, out failure));
			StringAssert.Contains("differs", failure);
		}

		[Test]
		public void PlanIsOriginAndRevisionBoundAndExactRetrySpendsNoRevision()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomCivicMemoryAuthority other = Authority();
			KingdomExperienceLedger ledger = EnabledLedger();
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryPlanLead(authority,
				KingdomCuriosityLeadCodecTests.LeadCause(3), 0,
				out KingdomCuriosityLeadPlan plan, out string failure), failure);
			KingdomCivicLeadReceipt row = plan.CivicLeadReceipt;
			Reserve(ledger, row.SourceId, row.SettlementId, row.CompletedTick);
			Assert.IsFalse(KingdomCuriosityLeadTransactions.TryCommit(plan, other, ledger,
				out _, out failure));
			StringAssert.Contains("another authority", failure);

			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryCommit(plan, authority, ledger,
				out bool committed, out failure), failure);
			Assert.IsTrue(committed);
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryReadExactLead(authority, row,
				out _, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryPlanLead(authority,
				KingdomCuriosityLeadCodecTests.LeadCause(3), 511, out plan, out failure), failure);
			long retryRevision = authority.Revision;
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryCommit(plan, authority, ledger,
				out committed, out failure), failure);
			Assert.IsFalse(committed); Assert.AreEqual(retryRevision, authority.Revision);

			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority,
				new KingdomCuriosityBook(), new KingdomCivicLeadBook(), authority.Revision,
				out _, out failure), failure);
			Assert.IsFalse(KingdomCuriosityLeadTransactions.TryCommit(plan, authority, ledger,
				out _, out failure));
			StringAssert.Contains("changed", failure);
		}

		[Test]
		public void RealmReplacementRetiresEveryForeignSettlementRowAndFreesBothCaps()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			string failure;
			KingdomCuriosityBook curiosity = new KingdomCuriosityBook();
			for (int i = 0; i < KingdomCuriosityBook.MaxRows; i++)
			{
				KingdomCuriosityCause cause = KingdomCuriosityLeadCodecTests.Cause("old-" + i);
				cause.SettlementId = "taf:settlement:old";
				Assert.IsTrue(KingdomCuriosityRules.TryPrepare(curiosity, curiosity.Revision,
					cause, KingdomCuriosityLeadCodecTests.Notes(), out _, out failure), failure);
			}
			KingdomCivicLeadBook leads = new KingdomCivicLeadBook();
			for (int i = 0; i < KingdomCivicLeadBook.MaxRows; i++)
			{
				KingdomCivicLeadCause cause = KingdomCuriosityLeadCodecTests.LeadCause(i);
				cause.SettlementId = "taf:settlement:old";
				Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(leads, leads.Revision, cause, i,
					true, out _, out failure), failure);
			}
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, curiosity, leads,
				authority.Revision, out _, out string seeded), seeded);
			long before = authority.Revision;
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryRetireForeignSettlements(authority,
				new[] { "taf:settlement:new" }, out bool committed, out int retired,
				out failure), failure);
			Assert.IsTrue(committed); Assert.AreEqual(11, retired);
			Assert.Greater(authority.Revision, before);
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryRead(authority, out _,
				out curiosity, out leads, out failure), failure);
			Assert.AreEqual(0, curiosity.Rows.Count); Assert.AreEqual(0, leads.Rows.Count);

			long settled = authority.Revision;
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryRetireForeignSettlements(authority,
				new[] { "taf:settlement:new" }, out committed, out retired, out failure), failure);
			Assert.IsFalse(committed); Assert.AreEqual(0, retired);
			Assert.AreEqual(settled, authority.Revision);
		}

		[Test]
		public void InvalidReplacementTopologyCannotRetireAnything()
		{
			KingdomCivicMemoryAuthority authority = Authority(); long before = authority.Revision;
			Assert.IsFalse(KingdomCuriosityLeadTransactions.TryRetireForeignSettlements(authority,
				new[] { "taf:settlement:one", "taf:settlement:one" }, out _, out _,
				out string failure));
			StringAssert.Contains("topology", failure); Assert.AreEqual(before, authority.Revision);
		}

		private static KingdomExperienceLedger EnabledLedger()
		{
			KingdomExperienceLedger ledger = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger,
				"taf:realm:transaction-test", out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				true, true, true, 0L, out failure), failure);
			return ledger;
		}

		private static void Reserve(KingdomExperienceLedger ledger, string source,
			string settlement, long tick)
		{
			KingdomExperienceAudienceReceipt request = new KingdomExperienceAudienceReceipt
			{
				ReservationId = KingdomCuriosityRules.AttentionReservationId(source),
				RealmId = ledger.RealmId, SettlementId = settlement, SourceId = source,
				Lane = KingdomExperienceLane.Curator,
				OptionKind = KingdomExperienceOptionKind.CivicKnowledge,
				CauseTick = tick, ReservedTick = tick, EnableEpoch = ledger.Knowledge.EnableEpoch
			};
			Assert.IsTrue(KingdomExperienceRules.TryReserveAudience(ledger, ledger.Revision,
				request, out _, out string failure), failure);
		}

		private static KingdomCivicMemoryAuthority Authority()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, id == KingdomCivicMemoryLimits.SectionCuriosity ? ReadCuriosity
					: id == KingdomCivicMemoryLimits.SectionCivicLeads ? ReadLeads : Anything);
			return new KingdomCivicMemoryAuthority(table);
		}

		private static KingdomCivicMemoryNested ReadCuriosity(byte[] payload, out string fault)
			=> Verdict(KingdomCuriosityLeadCodec.DecodeCuriosity(payload).State, out fault);
		private static KingdomCivicMemoryNested ReadLeads(byte[] payload, out string fault)
			=> Verdict(KingdomCuriosityLeadCodec.DecodeLeads(payload).State, out fault);
		private static KingdomCivicMemoryNested Verdict(KingdomCuriosityBookState state,
			out string fault)
		{
			fault = state == KingdomCuriosityBookState.Quarantined ? "unreadable" : "";
			return state == KingdomCuriosityBookState.FutureOpaque ? KingdomCivicMemoryNested.Future
				: state == KingdomCuriosityBookState.Quarantined
					? KingdomCivicMemoryNested.Malformed : KingdomCivicMemoryNested.Current;
		}
		private static KingdomCivicMemoryNested Anything(byte[] payload, out string fault)
		{ fault = ""; return KingdomCivicMemoryNested.Current; }
	}
}
#endif
