#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCuriosityRulesTests
	{
		private const string Locator = "JoppaWorld.10.20.1.2.10";

		[Test]
		public void KnownNoteSnapshotIsDeterministicAndClosesQuietly()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			List<KingdomCuriosityNote> notes = new List<KingdomCuriosityNote>
			{
				new KingdomCuriosityNote("note-z", Locator, "Zed", "Historic Sites", true),
				new KingdomCuriosityNote("note-a", Locator, "Ari", "Historic Sites", true),
				new KingdomCuriosityNote("hidden", Locator, "Hidden", "Historic Sites", false)
			};
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0, Cause(), notes,
				out KingdomCuriosityReceipt row, out string failure), failure);
			ClassicAssert.AreEqual(KingdomCuriosityState.Available, row.State);
			ClassicAssert.AreEqual("note-a", row.NoteId); ClassicAssert.AreEqual(Locator, row.Locator);
			ClassicAssert.AreEqual("because the lower cistern bears our mason's exact mark", row.Reason);
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryClose(book, book.Revision, row.SourceId,
				KingdomCuriosityState.Viewed, 21L, out failure), failure);
			long revision = book.Revision;
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryClose(book, revision, row.SourceId,
				KingdomCuriosityState.Viewed, 22L, out failure), failure);
			ClassicAssert.AreEqual(revision, book.Revision);
		}

		[Test]
		public void InvalidatedSnapshotNeverRetargets()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0, Cause(), Notes(),
				out KingdomCuriosityReceipt row, out string failure), failure);
			ClassicAssert.IsFalse(KingdomCuriosityRules.SameForeignNote(row,
				new KingdomCuriosityNote(row.NoteId, "JoppaWorld.1.1.1.1.10",
					row.NoteText, "Historic Sites", true)));
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryClose(book, book.Revision, row.SourceId,
				KingdomCuriosityState.Invalidated, 21L, out failure), failure);
			ClassicAssert.IsFalse(KingdomCuriosityRules.TryPrepare(book, book.Revision, Cause(),
				new List<KingdomCuriosityNote> { new KingdomCuriosityNote("replacement",
					Locator, "Other", "Historic Sites", true) }, out row, out failure));
			ClassicAssert.IsNull(row);
			ClassicAssert.AreEqual(KingdomCuriosityState.Invalidated, book.Rows[0].State);
			ClassicAssert.AreEqual("note-a", book.Rows[0].NoteId);
		}

		[Test]
		public void ExistingSourceNeverRetargetsWhenANewLowerSortedNoteAppears()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			List<KingdomCuriosityNote> first = new List<KingdomCuriosityNote>
			{
				new KingdomCuriosityNote("note-z", Locator, "Zed", "Historic Sites", true)
			};
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L, Cause(), first,
				out KingdomCuriosityReceipt frozen, out string failure), failure);
			List<KingdomCuriosityNote> later = new List<KingdomCuriosityNote>
			{
				new KingdomCuriosityNote("note-a", Locator, "Ari", "Historic Sites", true),
				first[0]
			};
			long revision = book.Revision;
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, revision, Cause(), later,
				out KingdomCuriosityReceipt retry, out failure), failure);
			ClassicAssert.AreEqual("note-z", retry.NoteId); ClassicAssert.AreEqual(frozen.NoteId, retry.NoteId);
			ClassicAssert.AreEqual(revision, book.Revision);
		}

		[Test]
		public void DeclinedSnapshotClosesAndRepeatedConversationStaysQuiet()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0, Cause(), Notes(),
				out KingdomCuriosityReceipt row, out string failure), failure);
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryClose(book, book.Revision, row.SourceId,
				KingdomCuriosityState.Declined, 21L, out failure), failure);
			long revision = book.Revision;
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, revision, Cause(), Notes(),
				out row, out failure), failure);
			ClassicAssert.AreEqual(KingdomCuriosityState.Declined, row.State);
			ClassicAssert.AreEqual(revision, book.Revision);
		}

		[Test]
		public void SameSourceRetryRequiresFullFrozenCauseAndSnapshot()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0, Cause(), Notes(),
				out KingdomCuriosityReceipt first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0, Cause(), Notes(),
				out KingdomCuriosityReceipt retry, out failure), failure);
			ClassicAssert.AreEqual(first.NoteId, retry.NoteId);
			ClassicAssert.AreEqual(1L, book.Revision);

			Action<KingdomCuriosityCause>[] mutations =
			{
				c => c.SourceVersion++, c => c.SettlementId += ":other",
				c => c.CuratorResidentId++, c => c.CuratorName += " II",
				c => c.CuratorObjectId += ":other", c => c.Reason += " otherwise",
				c => c.RequiredCategory = "Ruins", c => c.CompletedTick++
			};
			for (int i = 0; i < mutations.Length; i++)
			{
				KingdomCuriosityCause changed = Cause(); mutations[i](changed);
				ClassicAssert.IsFalse(KingdomCuriosityRules.TryPrepare(book, book.Revision,
					changed, Notes(), out _, out _), "cause mutation " + i);
				ClassicAssert.AreEqual(1L, book.Revision); ClassicAssert.IsFalse(book.Quarantined);
			}
			List<KingdomCuriosityNote>[] snapshots =
			{
				new List<KingdomCuriosityNote> { new KingdomCuriosityNote("note-b", Locator,
					"Ari", "Historic Sites", true) },
				new List<KingdomCuriosityNote> { new KingdomCuriosityNote("note-a",
					"JoppaWorld.11.20.1.2.10", "Ari", "Historic Sites", true) },
				new List<KingdomCuriosityNote> { new KingdomCuriosityNote("note-a", Locator,
					"Changed", "Historic Sites", true) },
				new List<KingdomCuriosityNote> { new KingdomCuriosityNote("note-a", Locator,
					"Ari", "Ruins", true) },
				new List<KingdomCuriosityNote> { new KingdomCuriosityNote("note-a", Locator,
					"Ari", "Historic Sites", false) }
			};
			for (int i = 0; i < snapshots.Length; i++)
			{
				ClassicAssert.IsFalse(KingdomCuriosityRules.TryPrepare(book, book.Revision,
					Cause(), snapshots[i], out _, out _), "snapshot mutation " + i);
				ClassicAssert.AreEqual(1L, book.Revision); ClassicAssert.IsFalse(book.Quarantined);
			}
		}

		[Test]
		public void RowsAreCanonicalAndDuplicateForeignNoteIdentityIsRejected()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			KingdomCuriosityCause z = Cause(); z.SourceId = "taf:source:z";
			KingdomCuriosityCause a = Cause(); a.SourceId = "taf:source:a";
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0, z, Notes(),
				out _, out string failure), failure);
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 1, a, Notes(),
				out _, out failure), failure);
			ClassicAssert.AreEqual("taf:source:a", book.Rows[0].SourceId);
			ClassicAssert.AreEqual("taf:source:z", book.Rows[1].SourceId);

			KingdomCuriosityBook empty = new KingdomCuriosityBook();
			List<KingdomCuriosityNote> duplicate = Notes(); duplicate.Add(Notes()[0]);
			ClassicAssert.IsFalse(KingdomCuriosityRules.TryPrepare(empty, 0, Cause(), duplicate,
				out _, out _));
			ClassicAssert.AreEqual(0, empty.Rows.Count); ClassicAssert.AreEqual(0L, empty.Revision);
		}

		[TestCase(KingdomCuriosityState.Viewed)]
		[TestCase(KingdomCuriosityState.Declined)]
		[TestCase(KingdomCuriosityState.Invalidated)]
		public void EveryTerminalStateExposesExactIdempotentAttentionCleanup(
			KingdomCuriosityState terminal)
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0, Cause(), Notes(),
				out KingdomCuriosityReceipt row, out string failure), failure);
			ClassicAssert.IsFalse(KingdomCuriosityRules.TryGetTerminalAttentionRelease(book,
				row.SourceId, out _, out _));
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryClose(book, book.Revision, row.SourceId,
				terminal, 21L, out failure), failure);
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryGetTerminalAttentionRelease(book,
				row.SourceId, out KingdomCuratorAttentionRelease release, out failure), failure);
			ClassicAssert.AreEqual(KingdomCuriosityRules.AttentionReservationId(row.SourceId),
				release.ReservationId);
			ClassicAssert.AreEqual(row.SourceId, release.SourceId);
			ClassicAssert.AreEqual(row.SettlementId, release.SettlementId);
			ClassicAssert.AreEqual(row.PreparedTick, release.EarliestCauseTick);

			KingdomExperienceLedger ledger = EnabledLedger(row.PreparedTick);
			KingdomExperienceAudienceReceipt lease = Attention(row, ledger,
				KingdomExperienceLane.Curator);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReserveAudience(ledger, ledger.Revision,
				lease, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryReleaseTerminalAttention(ledger, book,
				row.SourceId, out KingdomExperienceCapacityFault fault, out failure), failure);
			ClassicAssert.AreEqual(KingdomExperienceCapacityFault.None, fault);
			ClassicAssert.AreEqual(0, ledger.Audiences.Count);
			long releasedRevision = ledger.Revision;
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryReleaseTerminalAttention(ledger, book,
				row.SourceId, out fault, out failure), failure);
			ClassicAssert.AreEqual(releasedRevision, ledger.Revision);
		}

		[Test]
		public void TerminalRecoveryNeverReleasesMismatchedLane()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0, Cause(), Notes(),
				out KingdomCuriosityReceipt row, out string failure), failure);
			ClassicAssert.IsTrue(KingdomCuriosityRules.TryClose(book, book.Revision, row.SourceId,
				KingdomCuriosityState.Viewed, 21L, out failure), failure);
			KingdomExperienceLedger ledger = EnabledLedger(row.PreparedTick);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReserveAudience(ledger, ledger.Revision,
				Attention(row, ledger, KingdomExperienceLane.CivicVoices), out _, out failure),
				failure);
			long revision = ledger.Revision;
			ClassicAssert.IsFalse(KingdomCuriosityRules.TryReleaseTerminalAttention(ledger, book,
				row.SourceId, out KingdomExperienceCapacityFault fault, out failure));
			ClassicAssert.AreEqual(KingdomExperienceCapacityFault.OwnershipMismatch, fault);
			ClassicAssert.AreEqual(1, ledger.Audiences.Count); ClassicAssert.AreEqual(revision, ledger.Revision);
		}

		[TestCase("JoppaWorld.10.20.1.2.10", true)]
		[TestCase("JoppaWorld.10.20.3.2.10", false)]
		[TestCase("JoppaWorld.10.20.1.2", false)]
		[TestCase("JoppaWorld.-1.20.1.2.10", false)]
		public void FullLocatorValidationIsExact(string locator, bool valid)
		{
			ClassicAssert.AreEqual(valid, KingdomCuriosityRules.TryFullLocator(locator));
		}

		private static KingdomCuriosityCause Cause() => new KingdomCuriosityCause
		{
			SourceId = "taf:source:curiosity", SourceVersion = 1,
			SettlementId = "taf:settlement:curiosity", CuratorResidentId = 7,
			CuratorName = "Ari", CuratorObjectId = "taf:object:ari",
			Reason = "because the lower cistern bears our mason's exact mark",
			RequiredCategory = "Historic Sites", CompletedTick = 20L
		};
		private static List<KingdomCuriosityNote> Notes() => new List<KingdomCuriosityNote>
		{
			new KingdomCuriosityNote("note-a", Locator, "Ari", "Historic Sites", true)
		};

		private static KingdomExperienceLedger EnabledLedger(long tick)
		{
			KingdomExperienceLedger ledger = new KingdomExperienceLedger();
			ClassicAssert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger,
				"taf:realm:curiosity", out string failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				true, true, true, tick, out failure), failure);
			return ledger;
		}

		private static KingdomExperienceAudienceReceipt Attention(
			KingdomCuriosityReceipt row, KingdomExperienceLedger ledger,
			KingdomExperienceLane lane)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = KingdomCuriosityRules.AttentionReservationId(row.SourceId),
				RealmId = ledger.RealmId, SettlementId = row.SettlementId,
				SourceId = row.SourceId, Lane = lane,
				OptionKind = KingdomExperienceOptionKind.CivicKnowledge,
				CauseTick = row.PreparedTick, ReservedTick = row.PreparedTick,
				EnableEpoch = ledger.Knowledge.EnableEpoch
			};
		}
	}
}
#endif
