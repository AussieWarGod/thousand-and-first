#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The wire, pinned to bytes rather than to whatever the encoder happens to do today.
	/// <para>
	/// Every array below is a literal captured once and checked in. Nothing here calls the writer
	/// to find out what the past looked like, and that restraint is the whole point of the file:
	/// an encoder asked to reproduce history will always agree with itself, so a round-trip
	/// through today's writer proves the writer is self-consistent and proves nothing whatever
	/// about the save on a founder's disk. These bytes came off the revision 1 writer before it
	/// was replaced, and they do not move again.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomCuriosityLeadGoldenTests
	{
		/// <summary>
		/// A revision 1 curiosity book: one viewed row, no category field anywhere in it.
		/// Captured from the pre-shard writer for the cause named in <see cref="GoldenSourceId"/>.
		/// </summary>
		private const string GoldenCuriosityV1 =
			"54435531010000000200000000000000010000000100000002110000007461663a736f75"
			+ "7263653a676f6c64656e03000000150000007461663a736574746c656d656e743a676f6c"
			+ "64656e07000000030000004172690e0000007461663a6f626a6563743a6172690f000000"
			+ "7461663a6e6f74653a676f6c64656e170000004a6f707061576f726c642e31302e32302e"
			+ "312e322e3130130000007468652064726f776e6564206369737465726e36000000626563"
			+ "6175736520746865206c6f776572206369737465726e206265617273206f7572206d6173"
			+ "6f6e2773206578616374206d61726b14000000000000001500000000000000";

		/// <summary>The same book at revision 2: the category field and the digest, and not one
		/// byte of the fourteen revision 1 fields moved.</summary>
		private const string GoldenCuriosityV2 =
			"54435531020000000200000000000000010000000200000002110000007461663a736f75"
			+ "7263653a676f6c64656e03000000150000007461663a736574746c656d656e743a676f6c"
			+ "64656e07000000030000004172690e0000007461663a6f626a6563743a6172690f000000"
			+ "7461663a6e6f74653a676f6c64656e170000004a6f707061576f726c642e31302e32302e"
			+ "312e322e3130130000007468652064726f776e6564206369737465726e36000000626563"
			+ "6175736520746865206c6f776572206369737465726e206265617273206f7572206d6173"
			+ "6f6e2773206578616374206d61726b140000000000000015000000000000000e00000048"
			+ "6973746f7269632053697465738edb3ee03df8c0d12dadc86363e63c8d1ae8751a334809"
			+ "7517c23166760a47f3";

		/// <summary>A revision 1 civic-lead book: one projected row, ending in the four-byte
		/// absence marker the revision 1 cap forgot to count.</summary>
		private const string GoldenLeadsV1 =
			"54434c310100000002000000000000000100000001000000021d0000007461663a64656c"
			+ "76652d6c696e6b3a726563656970743a676f6c64656e02000000150000007461663a7365"
			+ "74746c656d656e743a676f6c64656e520000007461663a63697669632d6c6561643a7631"
			+ "3a36303534653162643336656635653638363136346565366430623136626231663461643"
			+ "234613836613839343834633165333333313939343964636231343337170000004a6f7070"
			+ "61576f726c642e31302e32302e312e312e31311e000000746865206e65776c79206f70656"
			+ "e6564206c6f77657220636f6d6d6f6e734000000054686520636f6d706c65746564206369"
			+ "74792064656c76652d6c696e6b20726563656970742070726f766573207468697320657861"
			+ "6374206c616e64696e672e6400000000000000ffffffff";

		/// <summary>
		/// A revision 1 curiosity book at the widest the first grammar allowed: a 256-character
		/// locator with a padded parasang, a parasang far outside the world grid, and stratum 255.
		/// The canonical grammar refuses every one of those, and this save still has to load.
		/// </summary>
		private const string GoldenCuriosityV1Legacy =
			"54435531010000000900000000000000010000000100000002150000007461663a736f75"
			+ "7263653a6c65676163792d6d617804000000190000007461663a736574746c656d656e74"
			+ "3a6c65676163792d6d61780b0000000400000050746f680f0000007461663a6f626a6563"
			+ "743a70746f68130000007461663a6e6f74653a6c65676163792d6d617800010000575757"
			+ "575757575757575757575757575757575757575757575757575757575757575757575757"
			+ "575757575757575757575757575757575757575757575757575757575757575757575757"
			+ "575757575757575757575757575757575757575757575757575757575757575757575757"
			+ "575757575757575757575757575757575757575757575757575757575757575757575757"
			+ "575757575757575757575757575757575757575757575757575757575757575757575757"
			+ "575757575757575757575757575757575757575757575757575757575757575757575757"
			+ "575757575757575757575757575757575757572e3039393939392e32302e312e322e3235"
			+ "35210000007468652064656570206369737465726e2062656e6561746820746865207361"
			+ "6c741e0000006265636175736520746865206f6c64206368617274206e616d6564206974"
			+ "1e000000000000001f00000000000000";

		private const string LegacyLocator =
			"WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW"
			+ "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW"
			+ "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW"
			+ "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW"
			+ ".099999.20.1.2.255";

		private const string GoldenSourceId = "taf:source:golden";
		private const string GoldenLeadSourceId = "taf:delve-link:receipt:golden";
		private const string GoldenLocator = "JoppaWorld.10.20.1.2.10";

		[Test]
		public void RevisionOneCuriosityGoldenReadsAndKeepsItsSilenceAboutCategory()
		{
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(
				Bytes(GoldenCuriosityV1));
			Assert.AreEqual(KingdomCuriosityBookState.Compatible, book.State, book.Fault);
			Assert.IsFalse(book.Quarantined); Assert.IsFalse(book.IsOpaqueFuture);
			Assert.AreEqual(2L, book.Revision);
			Assert.AreEqual(1, book.Rows.Count);
			KingdomCuriosityReceipt row = book.Rows[0];
			Assert.AreEqual(KingdomCuriosityReceipt.FirstVersion, row.Version);
			Assert.IsNull(row.NoteCategory,
				"a revision 1 save records no category and this build must not supply one");
			Assert.AreEqual(GoldenSourceId, row.SourceId);
			Assert.AreEqual(GoldenLocator, row.Locator);
			Assert.AreEqual(KingdomCuriosityState.Viewed, row.State);
			Assert.AreEqual(20L, row.PreparedTick); Assert.AreEqual(21L, row.ClosedTick);
		}

		/// <summary>
		/// The migration promise in bytes: a book that gained nothing is written back exactly as
		/// it arrived. A writer that "helpfully" upgraded every row on load would pass a
		/// round-trip test and fail this one.
		/// </summary>
		[Test]
		public void RevisionOneBooksAreReemittedByteForByte()
		{
			byte[] curiosity = Bytes(GoldenCuriosityV1);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(
				KingdomCuriosityLeadCodec.DecodeCuriosity(curiosity), out byte[] again,
				out string failure), failure);
			CollectionAssert.AreEqual(curiosity, again,
				"a revision 1 curiosity book must survive a load and save unchanged");

			byte[] leads = Bytes(GoldenLeadsV1);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(
				KingdomCuriosityLeadCodec.DecodeLeads(leads), out again, out failure), failure);
			CollectionAssert.AreEqual(leads, again,
				"the civic-lead book gained no field and must not gain any bytes");
		}

		[Test]
		public void RevisionOneLeadGoldenReadsWithItsDerivedIdentityIntact()
		{
			KingdomCivicLeadBook book = KingdomCuriosityLeadCodec.DecodeLeads(Bytes(GoldenLeadsV1));
			Assert.AreEqual(KingdomCuriosityBookState.Compatible, book.State, book.Fault);
			Assert.AreEqual(1, book.Rows.Count);
			KingdomCivicLeadReceipt row = book.Rows[0];
			Assert.AreEqual(KingdomCivicLeadPhase.Projected, row.Phase);
			Assert.AreEqual(GoldenLeadSourceId, row.SourceId);
			Assert.IsNull(row.Fault);
			Assert.AreEqual(KingdomCivicLeadRules.LeadId(row.SourceId, row.Locator), row.LeadId,
				"the stored identity must still be the one this build would derive");
		}

		[Test]
		public void RevisionTwoGoldenCarriesTheExactCategoryAndReemitsUnchanged()
		{
			byte[] golden = Bytes(GoldenCuriosityV2);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(golden);
			Assert.AreEqual(KingdomCuriosityBookState.Compatible, book.State, book.Fault);
			KingdomCuriosityReceipt row = book.Rows[0];
			Assert.AreEqual(KingdomCuriosityReceipt.CategoryVersion, row.Version);
			Assert.AreEqual("Historic Sites", row.NoteCategory);
			Assert.AreEqual(GoldenSourceId, row.SourceId);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] again,
				out string failure), failure);
			CollectionAssert.AreEqual(golden, again);
		}

		/// <summary>
		/// The two goldens differ only by the category field and the digest, and agree byte for
		/// byte up to the point where the older one stops having anything to say. If revision 2
		/// ever reorders a revision 1 field, this is the assertion that notices.
		/// </summary>
		[Test]
		public void RevisionTwoIsRevisionOnePlusTheCategoryAndTheDigest()
		{
			byte[] one = Bytes(GoldenCuriosityV1);
			byte[] two = Bytes(GoldenCuriosityV2);
			Assert.AreEqual(one.Length + 4 + "Historic Sites".Length
				+ KingdomCuriosityLeadCodec.DigestBytes, two.Length);

			// Two words differ by design: the frame's wire revision, and the row's own.
			Assert.AreEqual(KingdomCuriosityLeadCodec.FirstWireVersion,
				KingdomCuriosityLeadCodec.ReadInt32(one, 4));
			Assert.AreEqual(KingdomCuriosityLeadCodec.CuriosityHighestKnownVersion,
				KingdomCuriosityLeadCodec.ReadInt32(two, 4));
			int rowVersionAt = KingdomCuriosityLeadCodec.HeaderBytes;
			Assert.AreEqual(KingdomCuriosityReceipt.FirstVersion,
				KingdomCuriosityLeadCodec.ReadInt32(one, rowVersionAt));
			Assert.AreEqual(KingdomCuriosityReceipt.CategoryVersion,
				KingdomCuriosityLeadCodec.ReadInt32(two, rowVersionAt));

			// Every other byte the older revision wrote is in the same place in the newer one.
			for (int i = 8; i < rowVersionAt; i++)
				Assert.AreEqual(one[i], two[i], "frame byte " + i + " moved between revisions");
			for (int i = rowVersionAt + 4; i < one.Length; i++)
				Assert.AreEqual(one[i], two[i], "row byte " + i + " moved between revisions");
		}

		/// <summary>
		/// A migrated row meets a replay of its own cause. It is recognised, because everything
		/// revision 1 stored still matches; and it is <b>not</b> improved, because the category
		/// the replay knows is evidence gathered today about a decision taken long ago. The
		/// founder gets their own receipt back, silent field and all.
		/// </summary>
		[Test]
		public void ReplayRecognisesAMigratedRowWithoutTeachingItACategory()
		{
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(
				Bytes(GoldenCuriosityV1));
			long revision = book.Revision;
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, revision, GoldenCause(),
				GoldenNotes(), out KingdomCuriosityReceipt receipt, out string failure), failure);
			Assert.AreEqual(KingdomCuriosityReceipt.FirstVersion, receipt.Version);
			Assert.IsNull(receipt.NoteCategory);
			Assert.AreEqual(revision, book.Revision, "recognition must not spend a revision");
			Assert.AreEqual(1, book.Rows.Count);
			Assert.IsNull(book.Rows[0].NoteCategory,
				"the stored row must not be rewritten with a category it never had");
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] again, out failure),
				failure);
			CollectionAssert.AreEqual(Bytes(GoldenCuriosityV1), again,
				"a recognised replay must leave the save byte-identical");
		}

		/// <summary>A replay whose cause differs is refused against a migrated row exactly as it
		/// is against a fresh one; migration is not an amnesty.</summary>
		[Test]
		public void ReplayAgainstAMigratedRowStillComparesEveryFieldRevisionOneStored()
		{
			Action<KingdomCuriosityCause>[] mutations =
			{
				c => c.SourceVersion++, c => c.SettlementId += ":other",
				c => c.CuratorResidentId++, c => c.CuratorName += " II",
				c => c.CuratorObjectId += ":other", c => c.Reason += " otherwise",
				c => c.CompletedTick++
			};
			for (int i = 0; i < mutations.Length; i++)
			{
				KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(
					Bytes(GoldenCuriosityV1));
				KingdomCuriosityCause changed = GoldenCause(); mutations[i](changed);
				Assert.IsFalse(KingdomCuriosityRules.TryPrepare(book, book.Revision, changed,
					GoldenNotes(), out _, out string failure), "cause mutation " + i);
				Assert.IsNotEmpty(failure);
				Assert.AreEqual(2L, book.Revision); Assert.AreEqual(1, book.Rows.Count);
			}
		}

		/// <summary>
		/// A book holding one migrated row and one fresh row is written at revision 2 because one
		/// row needs it, and the migrated row keeps declaring revision 1 inside it. "Only where
		/// needed" is a per-row promise, not a per-file one.
		/// </summary>
		[Test]
		public void AMixedBookIsWrittenAtRevisionTwoAndKeepsEachRowsOwnRevision()
		{
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(
				Bytes(GoldenCuriosityV1));
			KingdomCuriosityCause fresh = GoldenCause();
			fresh.SourceId = "taf:source:zzz-later";
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, book.Revision, fresh,
				GoldenNotes(), out _, out string failure), failure);
			Assert.AreEqual(KingdomCuriosityLeadCodec.CuriosityHighestKnownVersion,
				KingdomCuriosityLeadCodec.WireVersionFor(book));
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] bytes, out failure),
				failure);
			KingdomCuriosityBook back = KingdomCuriosityLeadCodec.DecodeCuriosity(bytes);
			Assert.AreEqual(KingdomCuriosityBookState.Compatible, back.State, back.Fault);
			Assert.AreEqual(2, back.Rows.Count);
			KingdomCuriosityReceipt migrated = Row(back, GoldenSourceId);
			KingdomCuriosityReceipt added = Row(back, "taf:source:zzz-later");
			Assert.AreEqual(KingdomCuriosityReceipt.FirstVersion, migrated.Version);
			Assert.IsNull(migrated.NoteCategory);
			Assert.AreEqual(KingdomCuriosityReceipt.CategoryVersion, added.Version);
			Assert.AreEqual("Historic Sites", added.NoteCategory);
		}

		/// <summary>
		/// A save written against the first grammar still loads, at the widest that grammar
		/// allowed. Every part of this locator is something the canonical grammar refuses &mdash;
		/// a padded parasang, a parasang far outside the world grid, stratum 255, and 256
		/// characters of it &mdash; and none of that makes a founder's real records damage.
		/// </summary>
		[Test]
		public void AMaximalHistoricalBookLoadsAndIsWrittenBackUnchanged()
		{
			Assert.AreEqual(256, LegacyLocator.Length);
			Assert.IsFalse(KingdomCuriosityRules.TryFullLocator(LegacyLocator),
				"the canonical grammar must refuse this, or the fixture proves nothing");
			Assert.IsTrue(KingdomCuriosityRules.LegacyFullLocator(LegacyLocator));

			byte[] golden = Bytes(GoldenCuriosityV1Legacy);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(golden);
			Assert.AreEqual(KingdomCuriosityBookState.Compatible, book.State, book.Fault);
			Assert.AreEqual(1, book.Rows.Count);
			Assert.AreEqual(KingdomCuriosityReceipt.FirstVersion, book.Rows[0].Version);
			Assert.AreEqual(LegacyLocator, book.Rows[0].Locator);
			Assert.IsNull(book.Rows[0].NoteCategory);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] again,
				out string failure), failure);
			CollectionAssert.AreEqual(golden, again,
				"a historical save must survive a load and save without being rewritten");
		}

		/// <summary>
		/// The accepted cap is exactly what the first writer could emit, which is why no save
		/// exceeds it: three maximal revision 1 rows under a twenty-byte frame. The exact cap this
		/// build writes to is smaller, and must stay smaller.
		/// </summary>
		[Test]
		public void TheAcceptedCapIsExactlyWhatTheFirstWriterCouldHaveEmitted()
		{
			const int revisionOneRow = 7337;
			Assert.AreEqual(20 + KingdomCuriosityBook.MaxRows * revisionOneRow,
				KingdomCuriosityLeadCodec.MaxCuriosityBookBytes);
			Assert.Less(KingdomCuriosityLeadCodec.ExactCuriosityBookBytes,
				KingdomCuriosityLeadCodec.MaxCuriosityBookBytes);
			Assert.LessOrEqual(Bytes(GoldenCuriosityV1Legacy).Length,
				KingdomCuriosityLeadCodec.MaxCuriosityBookBytes);
		}

		/// <summary>A revision 2 row may not hide inside a revision 1 frame: the frame has no
		/// room for its category, so the payload is lying about one of the two.</summary>
		[Test]
		public void ARevisionTwoRowInsideARevisionOneFrameIsDamageNotAFuture()
		{
			byte[] forged = Bytes(GoldenCuriosityV1);
			forged[KingdomCuriosityLeadCodec.HeaderBytes] =
				(byte)KingdomCuriosityReceipt.CategoryVersion;
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(forged);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			Assert.IsFalse(book.IsOpaqueFuture);
			CollectionAssert.AreEqual(forged, book.OpaquePayload,
				"quarantine keeps the real bytes it was handed");
		}

		private static KingdomCuriosityReceipt Row(KingdomCuriosityBook book, string sourceId)
		{
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].SourceId == sourceId) return book.Rows[i];
			Assert.Fail("no row for " + sourceId); return null;
		}

		private static KingdomCuriosityCause GoldenCause() => new KingdomCuriosityCause
		{
			SourceId = GoldenSourceId, SourceVersion = 3,
			SettlementId = "taf:settlement:golden", CuratorResidentId = 7,
			CuratorName = "Ari", CuratorObjectId = "taf:object:ari",
			Reason = "because the lower cistern bears our mason's exact mark",
			RequiredCategory = "Historic Sites", CompletedTick = 20L
		};

		private static System.Collections.Generic.List<KingdomCuriosityNote> GoldenNotes()
			=> new System.Collections.Generic.List<KingdomCuriosityNote>
			{
				new KingdomCuriosityNote("taf:note:golden", GoldenLocator,
					"the drowned cistern", "Historic Sites", true)
			};

		internal static byte[] Bytes(string hex)
		{
			byte[] bytes = new byte[hex.Length / 2];
			for (int i = 0; i < bytes.Length; i++)
				bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
			return bytes;
		}
	}
}
#endif
