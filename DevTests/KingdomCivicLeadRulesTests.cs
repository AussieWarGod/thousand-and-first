#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.AreEqual(stable, KingdomCivicLeadRules.LeadId(cause.SourceId, cause.Locator));
			KingdomCivicLeadBook full = new KingdomCivicLeadBook();
			ClassicAssert.IsFalse(KingdomCivicLeadRules.TryPrepare(full, 0, cause,
				KingdomCivicLeadRules.MaxJournalMapNotes, true, out _, out _));
			ClassicAssert.AreEqual(0, full.Rows.Count); ClassicAssert.AreEqual(0, full.Revision);

			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0, cause, 10, true,
				out KingdomCivicLeadReceipt row, out string failure), failure);
			ClassicAssert.AreEqual(KingdomCivicLeadPhase.Prepared, row.Phase);
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0, cause, 511,
				false, out KingdomCivicLeadReceipt duplicate, out failure), failure);
			ClassicAssert.AreEqual(row.LeadId, duplicate.LeadId); ClassicAssert.AreEqual(1, book.Rows.Count);
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryMarkProjected(book, book.Revision,
				row.SourceId, row.LeadId, row.Locator, out failure), failure);
			ClassicAssert.AreEqual(KingdomCivicLeadPhase.Projected, book.Rows[0].Phase);
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryGetTerminalAttentionRelease(book,
				row.SourceId, out KingdomCuratorAttentionRelease projected, out failure), failure);
			ClassicAssert.AreEqual(KingdomCuriosityRules.AttentionReservationId(row.SourceId),
				projected.ReservationId);
			KingdomExperienceLedger ledger = EnabledLedger(row.CompletedTick);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReserveAudience(ledger, ledger.Revision,
				Attention(row, ledger), out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryReleaseTerminalAttention(ledger, book,
				row.SourceId, out KingdomExperienceCapacityFault fault, out failure), failure);
			ClassicAssert.AreEqual(KingdomExperienceCapacityFault.None, fault);
			ClassicAssert.AreEqual(0, ledger.Audiences.Count);
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryInvalidate(book, book.Revision,
				row.SourceId, out failure), failure);
			ClassicAssert.AreEqual(KingdomCivicLeadPhase.Invalidated, book.Rows[0].Phase);
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryGetTerminalAttentionRelease(book,
				row.SourceId, out KingdomCuratorAttentionRelease invalidated, out failure), failure);
			ClassicAssert.AreEqual(projected.ReservationId, invalidated.ReservationId);
			long releasedRevision = ledger.Revision;
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryReleaseTerminalAttention(ledger, book,
				row.SourceId, out fault, out failure), failure);
			ClassicAssert.AreEqual(releasedRevision, ledger.Revision);
		}

		[Test]
		public void SameSourceRetryExactComparesEveryCauseFieldWithoutFutureQuarantine()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0, Cause(), 0, true,
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
				ClassicAssert.IsFalse(KingdomCivicLeadRules.TryPrepare(book, book.Revision,
					changed, 0, true, out _, out _), "cause mutation " + i);
				ClassicAssert.AreEqual(1L, book.Revision); ClassicAssert.IsFalse(book.Quarantined);
			}
		}

		[Test]
		public void CivicLeadRowsUseStableSourceOrderAndPreparedHasNoTerminalRelease()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			KingdomCivicLeadCause z = Cause(); z.SourceId = "taf:delve:z";
			KingdomCivicLeadCause a = Cause(); a.SourceId = "taf:delve:a";
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0, z, 0, true,
				out _, out string failure), failure);
			ClassicAssert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 1, a, 0, true,
				out _, out failure), failure);
			ClassicAssert.AreEqual("taf:delve:a", book.Rows[0].SourceId);
			ClassicAssert.AreEqual("taf:delve:z", book.Rows[1].SourceId);
			ClassicAssert.IsFalse(KingdomCivicLeadRules.TryGetTerminalAttentionRelease(book,
				z.SourceId, out _, out _));
		}

		[Test]
		public void GuessedOrPartialLocatorAndMissingAttentionMutateNothing()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			KingdomCivicLeadCause cause = Cause(); cause.Locator = "the salt dunes";
			ClassicAssert.IsFalse(KingdomCivicLeadRules.TryPrepare(book, 0, cause, 0, true,
				out _, out _));
			cause = Cause();
			ClassicAssert.IsFalse(KingdomCivicLeadRules.TryPrepare(book, 0, cause, 0, false,
				out _, out _));
			ClassicAssert.AreEqual(0, book.Rows.Count); ClassicAssert.AreEqual(0, book.Revision);
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
			ClassicAssert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger,
				"taf:realm:civic-lead", out string failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
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
