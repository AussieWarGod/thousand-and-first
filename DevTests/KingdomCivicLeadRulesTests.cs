#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCivicLeadRulesTests
	{
		[Test]
		public void StableNontradableProjectionReceiptIsCapacityFirstAndRetryable()
		{
			KingdomCivicLeadCause cause = Cause();
			string stable = KingdomCivicLeadRules.LeadId(cause.SourceId, cause.Locator);
			Assert.AreEqual(stable, KingdomCivicLeadRules.LeadId(cause.SourceId, cause.Locator));
			KingdomCivicLeadBook full = new KingdomCivicLeadBook();
			Assert.IsFalse(KingdomCivicLeadRules.TryPrepare(full, 0, cause,
				KingdomCivicLeadRules.MaxJournalMapNotes, true, out _, out _));
			Assert.AreEqual(0, full.Rows.Count); Assert.AreEqual(0, full.Revision);

			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0, cause, 10, true,
				out KingdomCivicLeadReceipt row, out string failure), failure);
			Assert.AreEqual(KingdomCivicLeadPhase.Prepared, row.Phase);
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0, cause, 511,
				false, out KingdomCivicLeadReceipt duplicate, out failure), failure);
			Assert.AreEqual(row.LeadId, duplicate.LeadId); Assert.AreEqual(1, book.Rows.Count);
			Assert.IsTrue(KingdomCivicLeadRules.TryMarkProjected(book, book.Revision,
				row.SourceId, row.LeadId, row.Locator, out failure), failure);
			Assert.AreEqual(KingdomCivicLeadPhase.Projected, book.Rows[0].Phase);
			Assert.IsTrue(KingdomCivicLeadRules.TryGetTerminalAttentionRelease(book,
				row.SourceId, out KingdomCuratorAttentionRelease projected, out failure), failure);
			Assert.AreEqual(KingdomCuriosityRules.AttentionReservationId(row.SourceId),
				projected.ReservationId);
			KingdomExperienceLedger ledger = EnabledLedger(row.CompletedTick);
			Assert.IsTrue(KingdomExperienceRules.TryReserveAudience(ledger, ledger.Revision,
				Attention(row, ledger), out _, out failure), failure);
			Assert.IsTrue(KingdomCivicLeadRules.TryReleaseTerminalAttention(ledger, book,
				row.SourceId, out KingdomExperienceCapacityFault fault, out failure), failure);
			Assert.AreEqual(KingdomExperienceCapacityFault.None, fault);
			Assert.AreEqual(0, ledger.Audiences.Count);
			Assert.IsTrue(KingdomCivicLeadRules.TryInvalidate(book, book.Revision,
				row.SourceId, out failure), failure);
			Assert.AreEqual(KingdomCivicLeadPhase.Invalidated, book.Rows[0].Phase);
			Assert.IsTrue(KingdomCivicLeadRules.TryGetTerminalAttentionRelease(book,
				row.SourceId, out KingdomCuratorAttentionRelease invalidated, out failure), failure);
			Assert.AreEqual(projected.ReservationId, invalidated.ReservationId);
			long releasedRevision = ledger.Revision;
			Assert.IsTrue(KingdomCivicLeadRules.TryReleaseTerminalAttention(ledger, book,
				row.SourceId, out fault, out failure), failure);
			Assert.AreEqual(releasedRevision, ledger.Revision);
		}

		[Test]
		public void SameSourceRetryExactComparesEveryCauseFieldWithoutFutureQuarantine()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0, Cause(), 0, true,
				out _, out string failure), failure);
			Action<KingdomCivicLeadCause>[] mutations =
			{
				c => c.SourceVersion++, c => c.SettlementId += ":other",
				c => c.Locator = "JoppaWorld.11.20.1.1.11",
				c => c.Title += " changed", c => c.AuthoredReason += " changed",
				c => c.CompletedTick++
			};
			for (int i = 0; i < mutations.Length; i++)
			{
				KingdomCivicLeadCause changed = Cause(); mutations[i](changed);
				Assert.IsFalse(KingdomCivicLeadRules.TryPrepare(book, book.Revision,
					changed, 0, true, out _, out _), "cause mutation " + i);
				Assert.AreEqual(1L, book.Revision); Assert.IsFalse(book.Quarantined);
			}
		}

		[Test]
		public void CivicLeadRowsUseStableSourceOrderAndPreparedHasNoTerminalRelease()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			KingdomCivicLeadCause z = Cause(); z.SourceId = "taf:delve:z";
			KingdomCivicLeadCause a = Cause(); a.SourceId = "taf:delve:a";
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0, z, 0, true,
				out _, out string failure), failure);
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 1, a, 0, true,
				out _, out failure), failure);
			Assert.AreEqual("taf:delve:a", book.Rows[0].SourceId);
			Assert.AreEqual("taf:delve:z", book.Rows[1].SourceId);
			Assert.IsFalse(KingdomCivicLeadRules.TryGetTerminalAttentionRelease(book,
				z.SourceId, out _, out _));
		}

		[Test]
		public void GuessedOrPartialLocatorAndMissingAttentionMutateNothing()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			KingdomCivicLeadCause cause = Cause(); cause.Locator = "the salt dunes";
			Assert.IsFalse(KingdomCivicLeadRules.TryPrepare(book, 0, cause, 0, true,
				out _, out _));
			cause = Cause();
			Assert.IsFalse(KingdomCivicLeadRules.TryPrepare(book, 0, cause, 0, false,
				out _, out _));
			Assert.AreEqual(0, book.Rows.Count); Assert.AreEqual(0, book.Revision);
		}

		private static KingdomCivicLeadCause Cause() => new KingdomCivicLeadCause
		{
			SourceId = "taf:delve-link:receipt:one", SourceVersion = 1,
			SettlementId = "taf:settlement:one", Locator = "JoppaWorld.10.20.1.1.11",
			Title = "the newly opened lower commons",
			AuthoredReason = "The completed city delve-link receipt proves this exact landing.",
			CompletedTick = 100L
		};

		private static KingdomExperienceLedger EnabledLedger(long tick)
		{
			KingdomExperienceLedger ledger = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger,
				"taf:realm:civic-lead", out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				true, true, true, tick, out failure), failure);
			return ledger;
		}

		private static KingdomExperienceAudienceReceipt Attention(KingdomCivicLeadReceipt row,
			KingdomExperienceLedger ledger)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = KingdomCuriosityRules.AttentionReservationId(row.SourceId),
				RealmId = ledger.RealmId, SettlementId = row.SettlementId,
				SourceId = row.SourceId, Lane = KingdomExperienceLane.Curator,
				OptionKind = KingdomExperienceOptionKind.CivicKnowledge,
				CauseTick = row.CompletedTick, ReservedTick = row.CompletedTick,
				EnableEpoch = ledger.Knowledge.EnableEpoch
			};
		}
	}
}
#endif
