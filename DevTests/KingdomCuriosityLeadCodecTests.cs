#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// What the wire refuses, what it keeps, and what it refuses to keep quiet about.
	/// </summary>
	[TestFixture]
	public sealed class KingdomCuriosityLeadCodecTests
	{
		/// <summary>
		/// Every cap is arithmetic over the primitive bounds, and this is where the arithmetic is
		/// reproduced by hand. A test that only compared the constant to itself would agree with
		/// any change at all; these totals are written out field by field so that widening a bound
		/// has to be admitted here in the same breath.
		/// </summary>
		[Test]
		public void EveryCapIsRederivedFromThePrimitiveBoundsItRestsOn()
		{
			const int lengthPrefix = 4;
			int idField = lengthPrefix + 256 * 3;
			int textField = lengthPrefix + 384 * 3;
			int categoryField = lengthPrefix + 64 * 3;
			int locatorField = lengthPrefix + 64 * 3 + 5 + 8;
			int leadIdField = lengthPrefix + 82;

			Assert.AreEqual(772, idField); Assert.AreEqual(1156, textField);
			Assert.AreEqual(196, categoryField); Assert.AreEqual(209, locatorField);
			Assert.AreEqual(86, leadIdField);
			Assert.AreEqual(20, KingdomCuriosityLeadCodec.HeaderBytes);
			Assert.AreEqual(32, KingdomCuriosityLeadCodec.DigestBytes);

			int curiosityRow = 4 + 1 + idField + 4 + idField + 4 + textField + idField + idField
				+ locatorField + textField + textField + 8 + 8 + categoryField;
			int leadRow = 4 + 1 + idField + 4 + idField + leadIdField + locatorField
				+ textField + textField + 8 + lengthPrefix;
			Assert.AreEqual(6990, curiosityRow);
			Assert.AreEqual(4172, leadRow);
			Assert.AreEqual(curiosityRow, KingdomCuriosityLeadCodec.MaxCuriosityRowBytes);
			Assert.AreEqual(leadRow, KingdomCuriosityLeadCodec.MaxCivicLeadRowBytes);

			Assert.AreEqual(20 + 3 * 6990 + 32, KingdomCuriosityLeadCodec.ExactCuriosityBookBytes);
			Assert.AreEqual(20 + 8 * 4172 + 32, KingdomCuriosityLeadCodec.ExactLeadBookBytes);
			Assert.AreEqual(21022, KingdomCuriosityLeadCodec.ExactCuriosityBookBytes);
			Assert.AreEqual(33428, KingdomCuriosityLeadCodec.ExactLeadBookBytes);
			Assert.AreEqual(KingdomCuriosityLeadCodec.MaxLeadBookBytes,
				KingdomCuriosityLeadCodec.MaxBookBytes);
			Assert.AreEqual(3, KingdomCuriosityBook.MaxRows);
			Assert.AreEqual(8, KingdomCivicLeadBook.MaxRows);
		}

		/// <summary>
		/// Two caps, two jobs, and the one relation that must hold between them.
		/// <para>
		/// The accepted caps are what the first writer declared and are never recomputed: they are
		/// exactly what that writer would emit before refusing itself, so nothing larger was ever
		/// written to any disk, and a later build spending its bytes differently still fits. The
		/// exact caps are what this build may author, and they must stay strictly inside the
		/// accepted ones &mdash; the day they meet, an overrun of ours becomes indistinguishable
		/// from a successor we ought to be keeping.
		/// </para>
		/// </summary>
		[Test]
		public void TheAcceptedCapsAreHistoricalAndTheExactCapsStayInsideThem()
		{
			Assert.AreEqual(22031, KingdomCuriosityLeadCodec.MaxCuriosityBookBytes);
			Assert.AreEqual(37708, KingdomCuriosityLeadCodec.MaxLeadBookBytes);
			Assert.Less(KingdomCuriosityLeadCodec.ExactCuriosityBookBytes,
				KingdomCuriosityLeadCodec.MaxCuriosityBookBytes);
			Assert.Less(KingdomCuriosityLeadCodec.ExactLeadBookBytes,
				KingdomCuriosityLeadCodec.MaxLeadBookBytes);

			// The accepted curiosity cap is exactly three maximal revision 1 rows under the frame:
			// the same row arithmetic without the category field and without the digest.
			const int revisionOneCuriosityRow = 7337;
			Assert.AreEqual(20 + 3 * revisionOneCuriosityRow,
				KingdomCuriosityLeadCodec.MaxCuriosityBookBytes);

			// A payload one byte past the accepted cap is refused without being copied at all.
			byte[] over = new byte[KingdomCuriosityLeadCodec.MaxCuriosityBookBytes + 1];
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(over);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			Assert.IsNull(book.OpaquePayload);

			// A payload at exactly the accepted cap is copied and judged on its contents.
			byte[] at = new byte[KingdomCuriosityLeadCodec.MaxCuriosityBookBytes];
			book = KingdomCuriosityLeadCodec.DecodeCuriosity(at);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			Assert.AreEqual(at.Length, book.OpaquePayload.Length,
				"a payload inside the accepted cap is kept as evidence even when it will not read");
		}

		/// <summary>
		/// The decoders take one private copy of a caller's bytes before anything is decided, so a
		/// caller that keeps hold of the array it handed over cannot change the payload between
		/// the digest check and the parse. The retained evidence is that copy, never the caller's.
		/// </summary>
		[Test]
		public void IngressBytesAreCopiedOnceAndNothingLaterReadsTheCallersArray()
		{
			byte[] mine = ValidWithCategory();
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(mine);
			Assert.AreEqual(KingdomCuriosityBookState.Compatible, book.State, book.Fault);
			string locator = book.Rows[0].Locator;
			mine[mine.Length - 1] ^= 0xFF;
			Assert.AreEqual(locator, book.Rows[0].Locator,
				"a decoded book must not change when the caller edits the array afterwards");

			byte[] future = Future(0x31554354, 7);
			byte[] handed = (byte[])future.Clone();
			KingdomCuriosityBook opaque = KingdomCuriosityLeadCodec.DecodeCuriosity(handed);
			Assert.AreEqual(KingdomCuriosityBookState.FutureOpaque, opaque.State);
			Assert.AreNotSame(handed, opaque.OpaquePayload);
			handed[8] ^= 0xFF;
			CollectionAssert.AreEqual(future, opaque.OpaquePayload,
				"a future book's retained bytes are its own snapshot");
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(opaque, out byte[] again,
				out string failure), failure);
			CollectionAssert.AreEqual(future, again);

			byte[] damaged = { 1, 2, 3, 4, 5 };
			byte[] handedDamaged = (byte[])damaged.Clone();
			KingdomCivicLeadBook leads = KingdomCuriosityLeadCodec.DecodeLeads(handedDamaged);
			Assert.AreNotSame(handedDamaged, leads.OpaquePayload);
			handedDamaged[0] = 99;
			CollectionAssert.AreEqual(damaged, leads.OpaquePayload);
		}

		/// <summary>
		/// A state outside the three this build defines is refused everywhere it could be acted
		/// on. There is no fourth branch to fall through to, and no leniency for a book that
		/// cannot say what it is.
		/// </summary>
		[TestCase(3)]
		[TestCase(7)]
		[TestCase(255)]
		public void AnUndefinedBookStateIsRefusedRatherThanDefaultedToAnything(int raw)
		{
			KingdomCuriosityBookState undefined = (KingdomCuriosityBookState)raw;
			Assert.IsFalse(KingdomCuriosityLeadCodec.Defined(undefined));

			KingdomCuriosityBook curiosity = new KingdomCuriosityBook();
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(curiosity, 0L, Cause("one"), Notes(),
				out _, out string failure), failure);
			curiosity.State = undefined;
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(curiosity, out byte[] bytes,
				out failure));
			Assert.IsNull(bytes);
			StringAssert.Contains("nothing is written for it", failure,
				"the writer must refuse this itself rather than pass it further in");

			KingdomCivicLeadBook leads = new KingdomCivicLeadBook { State = undefined };
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(leads, out bytes, out failure));
			Assert.IsNull(bytes);
			StringAssert.Contains("nothing is written for it", failure);
			Assert.IsFalse(KingdomCuriosityRules.ValidBook(curiosity));
			Assert.IsFalse(KingdomCivicLeadRules.ValidBook(leads));
		}

		/// <summary>
		/// The re-emission path refuses an undefined state on its own account, not merely because
		/// the writer above it happened to look first. It is internal and a later caller can reach
		/// it directly, so it is checked directly here.
		/// </summary>
		[TestCase(4)]
		[TestCase(200)]
		public void OpaqueReemissionRefusesAnUndefinedStateOnItsOwnAccount(int raw)
		{
			byte[] future = Future(0x31554354, 7);
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryReemitOpaque(
				(KingdomCuriosityBookState)raw, future, 7,
				KingdomCuriosityLeadCodec.CuriosityMagic,
				KingdomCuriosityLeadCodec.MaxCuriosityBookBytes,
				KingdomCuriosityLeadCodec.CuriosityHighestKnownVersion, "curiosity",
				out byte[] bytes, out string failure));
			Assert.IsNull(bytes);
			StringAssert.Contains("will not be re-emitted", failure);

			// The same bytes under the state that does describe them are re-emitted exactly.
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryReemitOpaque(
				KingdomCuriosityBookState.FutureOpaque, future, 7,
				KingdomCuriosityLeadCodec.CuriosityMagic,
				KingdomCuriosityLeadCodec.MaxCuriosityBookBytes,
				KingdomCuriosityLeadCodec.CuriosityHighestKnownVersion, "curiosity",
				out bytes, out failure), failure);
			CollectionAssert.AreEqual(future, bytes);
		}

		[Test]
		public void TheThreeDefinedStatesAreExactlyTheOnesThisBuildAnswersFor()
		{
			Assert.IsTrue(KingdomCuriosityLeadCodec.Defined(
				KingdomCuriosityBookState.Compatible));
			Assert.IsTrue(KingdomCuriosityLeadCodec.Defined(
				KingdomCuriosityBookState.FutureOpaque));
			Assert.IsTrue(KingdomCuriosityLeadCodec.Defined(
				KingdomCuriosityBookState.Quarantined));
			// The predicate root maps into the civic-memory bindings, pinned on both books.
			Assert.IsTrue(KingdomCuriosityLeadCodec.DecodeCuriosity(
				Future(0x31554354, 5)).IsOpaqueFuture);
			Assert.IsTrue(KingdomCuriosityLeadCodec.DecodeLeads(
				Future(0x314C4354, 5)).IsOpaqueFuture);
			Assert.IsFalse(KingdomCuriosityLeadCodec.DecodeCuriosity(
				new byte[] { 1, 2, 3 }).IsOpaqueFuture);
			Assert.IsFalse(KingdomCuriosityLeadCodec.DecodeLeads(
				new byte[] { 1, 2, 3 }).IsOpaqueFuture);
		}

		/// <summary>The absent fault marker costs four bytes on the wire, and the cap counts
		/// them. The revision 1 arithmetic did not, and under-bounded the lead book by the
		/// thirty-two bytes eight rows of it spend.</summary>
		[Test]
		public void TheAbsentFaultMarkerIsPaidForInTheCap()
		{
			const int revisionOneRow = 4711;
			const int revisionOneLocatorField = 752;
			const int canonicalLocatorField = 209;
			Assert.AreEqual(4, KingdomCuriosityLeadCodec.AbsentStringBytes);
			Assert.AreEqual(
				revisionOneRow - revisionOneLocatorField + canonicalLocatorField
					+ KingdomCuriosityLeadCodec.AbsentStringBytes,
				KingdomCuriosityLeadCodec.MaxCivicLeadRowBytes,
				"the revision 1 row claimed 4711: it forgot the four-byte fault marker every row "
					+ "actually writes, and allowed a 748-byte locator the canonical grammar "
					+ "caps at 205");
			Assert.AreEqual(20 + 8 * revisionOneRow, 37708,
				"the revision 1 book cap was the header plus eight of those rows");
			Assert.AreEqual(37708 - KingdomCuriosityLeadCodec.ExactLeadBookBytes, 4280);
			Assert.AreEqual(22031 - KingdomCuriosityLeadCodec.ExactCuriosityBookBytes, 1009);
		}

		[Test]
		public void BothBooksFillToTheirCapAndRoundTripInsideIt()
		{
			KingdomCuriosityBook curiosity = new KingdomCuriosityBook();
			for (int i = 0; i < KingdomCuriosityBook.MaxRows; i++)
				Assert.IsTrue(KingdomCuriosityRules.TryPrepare(curiosity, curiosity.Revision,
					Cause("source-" + i), Notes(), out _, out string f), f);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(curiosity, out byte[] bytes,
				out string failure), failure);
			Assert.LessOrEqual(bytes.Length, KingdomCuriosityLeadCodec.MaxCuriosityBookBytes);
			KingdomCuriosityBook back = KingdomCuriosityLeadCodec.DecodeCuriosity(bytes);
			Assert.AreEqual(KingdomCuriosityBookState.Compatible, back.State, back.Fault);
			Assert.AreEqual(KingdomCuriosityBook.MaxRows, back.Rows.Count);

			KingdomCivicLeadBook leads = FullLeadBook();
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(leads, out bytes, out failure),
				failure);
			Assert.LessOrEqual(bytes.Length, KingdomCuriosityLeadCodec.MaxLeadBookBytes);
			Assert.AreEqual(KingdomCivicLeadBook.MaxRows,
				KingdomCuriosityLeadCodec.DecodeLeads(bytes).Rows.Count);
		}

		// ---- refusals -------------------------------------------------------------------

		[Test]
		public void ANullPayloadIsQuarantinedWithNothingInventedToHold()
		{
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(null);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			Assert.IsNull(book.OpaquePayload);
			Assert.IsNotEmpty(book.Fault);
			Assert.AreEqual(0, book.Rows.Count);
		}

		[Test]
		public void AShortPayloadIsQuarantinedAndItsRealBytesAreKept()
		{
			byte[] stub = { 1, 2, 3 };
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(stub);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			CollectionAssert.AreEqual(stub, book.OpaquePayload);
			Assert.AreNotSame(stub, book.OpaquePayload, "evidence must be this book's own copy");
		}

		[Test]
		public void AnOversizedPayloadIsRefusedAndItsLengthIsRecordedRatherThanItsBytes()
		{
			byte[] huge = Valid();
			Array.Resize(ref huge, KingdomCuriosityLeadCodec.MaxCuriosityBookBytes + 1);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(huge);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			Assert.IsNull(book.OpaquePayload);
			StringAssert.Contains(huge.Length.ToString(), book.Fault);
		}

		/// <summary>
		/// Bytes past the end of the rows are refused on their own account.
		/// <para>
		/// A revision 1 book has no digest, so nothing but the end-of-rows check stands between a
		/// padded payload and a reader that shrugs. And a revision 2 book is padded <i>inside</i>
		/// the sealed region and resealed, so its digest is perfectly valid and the same check is
		/// again the only thing that can refuse it. Padding a revision 2 book from the outside
		/// would only have proven the digest works.
		/// </para>
		/// </summary>
		[Test]
		public void TrailingBytesAreRefusedOnTheirOwnAccountInBothRevisions()
		{
			byte[] leads = ValidLeads();
			Assert.AreEqual(KingdomCuriosityLeadCodec.FirstWireVersion,
				KingdomCuriosityLeadCodec.ReadInt32(leads, 4), "the lead book carries no digest");
			byte[] padded = new byte[leads.Length + 1];
			Buffer.BlockCopy(leads, 0, padded, 0, leads.Length);
			KingdomCivicLeadBook leadBook = KingdomCuriosityLeadCodec.DecodeLeads(padded);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, leadBook.State);
			StringAssert.Contains("past the end of the rows", leadBook.Fault);
			CollectionAssert.AreEqual(padded, leadBook.OpaquePayload);

			byte[] valid = ValidWithCategory();
			int bodyEnd = valid.Length - KingdomCuriosityLeadCodec.DigestBytes;
			byte[] inside = new byte[valid.Length + 1];
			Buffer.BlockCopy(valid, 0, inside, 0, bodyEnd);
			inside[bodyEnd] = 0x7F;
			Buffer.BlockCopy(valid, bodyEnd, inside, bodyEnd + 1,
				KingdomCuriosityLeadCodec.DigestBytes);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(Reseal(inside));
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			StringAssert.Contains("past the end of the rows", book.Fault,
				"the digest was resealed over the padding, so only the end-of-rows check is left");
		}

		[Test]
		public void AForeignMagicIsRefusedByBothBooks()
		{
			byte[] curiosity = Valid();
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined,
				KingdomCuriosityLeadCodec.DecodeLeads(curiosity).State,
				"a curiosity book must not read as a civic-lead book");
			byte[] leads = ValidLeads();
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined,
				KingdomCuriosityLeadCodec.DecodeCuriosity(leads).State);
		}

		/// <summary>A wire revision at or below zero is what a wrapped counter looks like from the
		/// far side. It is refused outright and never clamped up into revision 1.</summary>
		[TestCase(0)]
		[TestCase(-1)]
		[TestCase(int.MinValue)]
		public void ARevisionNoBuildCouldHaveAllocatedIsRefusedAndNeverClamped(int version)
		{
			byte[] bytes = Valid();
			Write(bytes, 4, version);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(bytes);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			Assert.IsFalse(book.IsOpaqueFuture);
			Assert.AreEqual(0, book.OpaqueVersion);
			StringAssert.Contains(version.ToString(), book.Fault);
		}

		/// <summary>A book revision that has run out cannot advance. The counter is refused
		/// rather than wrapped into the past.</summary>
		[Test]
		public void AnExhaustedBookRevisionRefusesEveryMutationInsteadOfWrapping()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("one"), Notes(),
				out KingdomCuriosityReceipt row, out string failure), failure);
			book.Revision = long.MaxValue;
			Assert.IsFalse(KingdomCuriosityRules.TryClose(book, long.MaxValue, row.SourceId,
				KingdomCuriosityState.Viewed, 30L, out failure));
			StringAssert.Contains("exhausted", failure);
			Assert.AreEqual(long.MaxValue, book.Revision);
			Assert.IsFalse(KingdomCuriosityRules.TryPrepare(book, long.MaxValue, Cause("two"),
				Notes(), out _, out failure));
			Assert.AreEqual(1, book.Rows.Count);

			KingdomCivicLeadBook leads = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(leads, 0L, LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt lead, out failure), failure);
			leads.Revision = long.MaxValue;
			Assert.IsFalse(KingdomCivicLeadRules.TryMarkProjected(leads, long.MaxValue,
				lead.SourceId, lead.LeadId, lead.Locator, out failure));
			StringAssert.Contains("exhausted", failure);
			Assert.IsFalse(KingdomCivicLeadRules.TryInvalidate(leads, long.MaxValue,
				lead.SourceId, out failure));
			Assert.AreEqual(long.MaxValue, leads.Revision);
			Assert.AreEqual(KingdomCivicLeadPhase.Prepared, leads.Rows[0].Phase);
		}

		[Test]
		public void ANegativeBookRevisionOnTheWireIsRefused()
		{
			byte[] bytes = Valid();
			Write(bytes, 8, -1); Write(bytes, 12, -1);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined,
				KingdomCuriosityLeadCodec.DecodeCuriosity(Reseal(bytes)).State);
		}

		[Test]
		public void DuplicateAndUnsortedRowsAreBothRefused()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("a"), Notes(),
				out _, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, book.Revision, Cause("b"),
				Notes(), out _, out failure), failure);

			KingdomCuriosityBook unsorted = new KingdomCuriosityBook { Revision = book.Revision };
			unsorted.Rows.Add(book.Rows[1].Copy()); unsorted.Rows.Add(book.Rows[0].Copy());
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(unsorted, out _, out failure));
			StringAssert.Contains("own rules", failure);

			KingdomCuriosityBook duplicate = new KingdomCuriosityBook { Revision = book.Revision };
			duplicate.Rows.Add(book.Rows[0].Copy()); duplicate.Rows.Add(book.Rows[0].Copy());
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(duplicate, out _, out failure));
		}

		[Test]
		public void InvalidUtf8NeverEscapesInEitherDirection()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("one"), Notes(),
				out _, out string failure), failure);
			book.Rows[0].Reason = "bad\ud800";
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(book, out _, out failure));
			Assert.IsNotEmpty(failure);

			byte[] bytes = Valid();
			int at = IndexOfAscii(bytes, "because the lower");
			Assert.Greater(at, 0);
			bytes[at] = 0xFF; bytes[at + 1] = 0xFE;
			KingdomCuriosityBook broken = KingdomCuriosityLeadCodec.DecodeCuriosity(
				Reseal(bytes));
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, broken.State);
			Assert.AreEqual(0, broken.Rows.Count);
		}

		/// <summary>
		/// A string that cannot survive the wire is refused at ingress, before the book has moved.
		/// <para>
		/// A lone surrogate is an ordinary <c>char</c> and an impossible piece of text. If the
		/// rules accepted one, the revision would advance and the founder's records would then
		/// hold a row the writer cannot emit &mdash; a book that has changed and can no longer be
		/// saved. So it is refused where it arrives, and the revision never moves.
		/// </para>
		/// </summary>
		[Test]
		public void AStringThatCannotSurviveTheWireIsRefusedBeforeTheRevisionMoves()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("one"), Notes(),
				out _, out string failure), failure);
			long revision = book.Revision;

			System.Action<KingdomCuriosityCause>[] poisons =
			{
				c => c.CuratorName += "\uD800",
				c => c.Reason += "\uDC00",
				c => c.CuratorObjectId += "\uD83C",
				c => c.RequiredCategory = "Historic\uD800Sites"
			};
			for (int i = 0; i < poisons.Length; i++)
			{
				KingdomCuriosityCause cause = Cause("two"); poisons[i](cause);
				Assert.IsFalse(KingdomCuriosityRules.TryPrepare(book, revision, cause, Notes(),
					out _, out failure), "poison " + i);
				Assert.AreEqual(revision, book.Revision, "poison " + i + " advanced the revision");
				Assert.AreEqual(1, book.Rows.Count);
			}

			System.Collections.Generic.List<KingdomCuriosityNote> poisoned =
				new System.Collections.Generic.List<KingdomCuriosityNote>
				{
					new KingdomCuriosityNote("taf:note:one", "JoppaWorld.10.20.1.2.10",
						"the drowned \uD800cistern", "Historic Sites", true)
				};
			Assert.IsFalse(KingdomCuriosityRules.TryPrepare(book, revision, Cause("three"),
				poisoned, out _, out failure));
			Assert.AreEqual(revision, book.Revision);

			// And a book that is already sound stays writable, so the guard is not simply refusing
			// everything.
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out _, out failure), failure);
		}

		/// <summary>
		/// The note list is a foreign store and a caller's object. It is copied once and every
		/// pass reads the copy, so a list that changes between the eligibility pass and the
		/// duplicate-identity pass cannot have one note chosen and a different set counted.
		/// </summary>
		[Test]
		public void TheKnownNoteListIsSnapshotOnceBeforeItIsWalkedTwice()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			ShiftingNotes notes = new ShiftingNotes(
				new KingdomCuriosityNote("taf:note:one", "JoppaWorld.10.20.1.2.10",
					"the drowned cistern", "Historic Sites", true));
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("one"), notes,
				out KingdomCuriosityReceipt row, out string failure), failure);
			Assert.AreEqual("taf:note:one", row.NoteId);
			Assert.AreEqual(1, notes.Copies,
				"the list must be copied exactly once, not walked twice through the caller");
		}

		/// <summary>A list that counts how many times it is copied, and would report a different
		/// second reading if it were walked twice.</summary>
		private sealed class ShiftingNotes : System.Collections.Generic.IList<KingdomCuriosityNote>
		{
			private readonly KingdomCuriosityNote Note;
			internal int Copies;
			internal ShiftingNotes(KingdomCuriosityNote note) { Note = note; }
			public int Count => 1;
			public bool IsReadOnly => true;
			public KingdomCuriosityNote this[int index]
			{
				get { return Note; }
				set { throw new System.NotSupportedException(); }
			}
			public void CopyTo(KingdomCuriosityNote[] array, int index)
			{ Copies++; array[index] = Note; }
			public System.Collections.Generic.IEnumerator<KingdomCuriosityNote> GetEnumerator()
			{ yield return Note; }
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{ yield return Note; }
			public int IndexOf(KingdomCuriosityNote item) => 0;
			public void Insert(int index, KingdomCuriosityNote item)
			{ throw new System.NotSupportedException(); }
			public void RemoveAt(int index) { throw new System.NotSupportedException(); }
			public void Add(KingdomCuriosityNote item)
			{ throw new System.NotSupportedException(); }
			public void Clear() { throw new System.NotSupportedException(); }
			public bool Contains(KingdomCuriosityNote item) => true;
			public bool Remove(KingdomCuriosityNote item)
			{ throw new System.NotSupportedException(); }
		}

		/// <summary>A quarantined book is not authority: nothing may be prepared into it, closed
		/// in it, or released from it.</summary>
		[Test]
		public void AQuarantinedBookIsNeverAuthorityForAnything()
		{
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(
				new byte[] { 1, 2, 3 });
			Assert.IsFalse(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("one"), Notes(),
				out _, out _));
			Assert.IsFalse(KingdomCuriosityRules.TryClose(book, 0L, "one",
				KingdomCuriosityState.Viewed, 1L, out _));
			Assert.IsFalse(KingdomCuriosityRules.TryGetTerminalAttentionRelease(book, "one",
				out _, out _));
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] bytes, out string f));
			Assert.IsNull(bytes);
			StringAssert.Contains("evidence", f);
		}

		/// <summary>
		/// A lead's identity is derived from its source and its place, so a row carrying any other
		/// identity is claiming a journal note it cannot account for. The check is re-run on both
		/// sides of the wire: a tampered row is neither written nor read back as authority.
		/// </summary>
		[Test]
		public void ALeadRowWhoseIdentityIsNotItsOwnDerivationIsRefusedBothWays()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0L, LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt row, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] sound,
				out failure), failure);

			string honest = row.LeadId;
			book.Rows[0].LeadId = KingdomCivicLeadRules.LeadId("taf:delve:other",
				row.Locator);
			Assert.AreNotEqual(honest, book.Rows[0].LeadId);
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(book, out _, out failure),
				"a lead identity that is not this row's own derivation must not be written");
			StringAssert.Contains("does not satisfy its own rules", failure);

			int at = IndexOfAscii(sound, honest);
			Assert.Greater(at, 0);
			byte[] tampered = (byte[])sound.Clone();
			tampered[at + KingdomCivicLeadRules.LeadIdPrefix.Length] =
				tampered[at + KingdomCivicLeadRules.LeadIdPrefix.Length] == (byte)'0'
					? (byte)'1' : (byte)'0';
			KingdomCivicLeadBook back = KingdomCuriosityLeadCodec.DecodeLeads(tampered);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, back.State,
				"a wire row whose identity is not its own derivation is not authority");
			CollectionAssert.AreEqual(tampered, back.OpaquePayload);
		}

		// ---- futures --------------------------------------------------------------------

		/// <summary>
		/// A structurally valid later book is preserved exactly, held read-only, and is never
		/// called malformed. Both books answer the same way; the civic-lead book reads only to
		/// revision 1, so revision 2 is already a future for it.
		/// </summary>
		[TestCase(3)]
		[TestCase(7)]
		[TestCase(int.MaxValue)]
		public void ALaterCuriosityBookIsHeldExactlyAndNeverCalledMalformed(int version)
		{
			byte[] future = Future(0x31554354, version);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(future);
			Assert.AreEqual(KingdomCuriosityBookState.FutureOpaque, book.State);
			Assert.IsTrue(book.IsOpaqueFuture); Assert.IsFalse(book.Quarantined);
			Assert.AreEqual(version, book.OpaqueVersion);
			CollectionAssert.AreEqual(future, book.OpaquePayload);
			Assert.AreEqual(0, book.Rows.Count);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] again,
				out string failure), failure);
			CollectionAssert.AreEqual(future, again);
			Assert.AreNotSame(future, again);
			Assert.IsFalse(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("one"), Notes(),
				out _, out _), "a future book is read-only");
		}

		[TestCase(2)]
		[TestCase(9)]
		public void ALaterCivicLeadBookIsHeldExactlyFromRevisionTwoOnward(int version)
		{
			byte[] future = Future(0x314C4354, version);
			KingdomCivicLeadBook book = KingdomCuriosityLeadCodec.DecodeLeads(future);
			Assert.AreEqual(KingdomCuriosityBookState.FutureOpaque, book.State, book.Fault);
			Assert.AreEqual(version, book.OpaqueVersion);
			CollectionAssert.AreEqual(future, book.OpaquePayload);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] again,
				out string failure), failure);
			CollectionAssert.AreEqual(future, again);
			Assert.IsFalse(KingdomCivicLeadRules.TryPrepare(book, 0L, LeadCause(0), 0, true,
				out _, out _));
		}

		/// <summary>
		/// A later revision that does not close with a digest is damage, not a future. That is the
		/// price of the frame promise: it is only worth anything if a payload that breaks it is
		/// refused rather than trusted.
		/// </summary>
		[Test]
		public void ALaterRevisionThatBreaksTheFramePromiseIsDamage()
		{
			byte[] future = Future(0x31554354, 7);
			byte[] unsealed_ = new byte[future.Length - KingdomCuriosityLeadCodec.DigestBytes];
			Buffer.BlockCopy(future, 0, unsealed_, 0, unsealed_.Length);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(unsealed_);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			Assert.IsFalse(book.IsOpaqueFuture);
			CollectionAssert.AreEqual(unsealed_, book.OpaquePayload);
		}

		[Test]
		public void ALaterBookWhoseDigestNoLongerCoversItIsDamage()
		{
			byte[] future = Future(0x31554354, 7);
			future[12] ^= 0x01;
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(future);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			StringAssert.Contains("digest", book.Fault);
			CollectionAssert.AreEqual(future, book.OpaquePayload);
		}

		// ---- forged opacity -------------------------------------------------------------

		/// <summary>
		/// The writer trusts nothing the caller says about a book's state. Each case below sets
		/// the future flag by hand over bytes whose integrity does not verify as a future's, and each is
		/// refused with nothing written.
		/// </summary>
		[Test]
		public void ForgedOpacityIsRefusedBecauseTheWriterReverifiesTheBytes()
		{
			byte[][] forgeries =
			{
				null,
				new byte[0],
				new byte[] { 1, 2, 3 },
				Valid(),
				Future(0x314C4354, 7),
				Tampered(Future(0x31554354, 7)),
				Truncated(Future(0x31554354, 7))
			};
			for (int i = 0; i < forgeries.Length; i++)
			{
				KingdomCuriosityBook forged = new KingdomCuriosityBook
				{
					State = KingdomCuriosityBookState.FutureOpaque,
					OpaqueVersion = 7, OpaquePayload = forgeries[i]
				};
				Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(forged, out byte[] bytes,
					out string failure), "forgery " + i + " was written back");
				Assert.IsNull(bytes, "forgery " + i);
				Assert.IsNotEmpty(failure, "forgery " + i);
			}
		}

		/// <summary>
		/// The sharpest forgery: a caller labels a perfectly good book of <i>this</i> build's own
		/// revision as a future, and declares the revision its bytes really carry. Nothing about
		/// the payload is malformed and nothing about the declaration is inconsistent &mdash; the
		/// only thing wrong is that these bytes are not a later build's, and the writer must
		/// notice that on its own rather than on the back of some other check.
		/// </summary>
		[Test]
		public void ACurrentBookRelabelledAsAFutureAtItsOwnRevisionIsStillRefused()
		{
			byte[] valid = Valid();
			int declared = KingdomCuriosityLeadCodec.ReadInt32(valid, 4);
			KingdomCuriosityBook forged = new KingdomCuriosityBook
			{
				State = KingdomCuriosityBookState.FutureOpaque,
				OpaqueVersion = declared, OpaquePayload = valid
			};
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(forged, out byte[] bytes,
				out string failure));
			Assert.IsNull(bytes);
			StringAssert.Contains("do not verify as one", failure);

			byte[] leads = ValidLeads();
			KingdomCivicLeadBook forgedLeads = new KingdomCivicLeadBook
			{
				State = KingdomCuriosityBookState.FutureOpaque,
				OpaqueVersion = KingdomCuriosityLeadCodec.ReadInt32(leads, 4),
				OpaquePayload = leads
			};
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(forgedLeads, out bytes,
				out failure));
			Assert.IsNull(bytes);
		}

		/// <summary>
		/// A string field is bounded by its own field's grammar, not by whatever is left in the
		/// payload. The distinction shows in the refusal: a declared length past the bound is
		/// refused for being past the bound, before a buffer that size is ever asked for.
		/// </summary>
		[Test]
		public void AStringFieldPastItsOwnBoundIsRefusedBeforeItIsRead()
		{
			byte[] bytes = ValidWithCategory();
			int at = IndexOfAscii(bytes, "Historic Sites");
			Assert.Greater(at, 0);
			Write(bytes, at - 4, 16000000);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(Reseal(bytes));
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			StringAssert.Contains("declares 16000000 bytes", book.Fault,
				"the field's own bound must refuse this, not the end of the payload");
		}

		/// <summary>The row count is held to the book's own maximum before a single row is read,
		/// so an impossible count is named rather than discovered by running out of bytes.</summary>
		[Test]
		public void ARowCountPastTheBooksMaximumIsRefusedBeforeAnyRowIsRead()
		{
			byte[] bytes = Valid();
			Write(bytes, 16, KingdomCuriosityBook.MaxRows + 1);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(
				Reseal(bytes));
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			StringAssert.Contains("the row count is 4 against a maximum of 3", book.Fault);

			byte[] enormous = Valid();
			Write(enormous, 16, int.MaxValue);
			book = KingdomCuriosityLeadCodec.DecodeCuriosity(Reseal(enormous));
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State);
			StringAssert.Contains("the row count is", book.Fault);
		}

		/// <summary>
		/// A revision 2 row inside a revision 1 frame, with a well-formed category sitting behind
		/// it. Every byte reads; the payload is only wrong about what frame it is in. Without the
		/// frame-versus-row check this decodes cleanly and a revision 1 save silently acquires a
		/// category, which is precisely the invented evidence this family refuses to produce.
		/// </summary>
		[Test]
		public void AWellFormedRevisionTwoRowInsideARevisionOneFrameIsRefused()
		{
			byte[] two = ValidWithCategory();
			byte[] mislabelled =
				new byte[two.Length - KingdomCuriosityLeadCodec.DigestBytes];
			Buffer.BlockCopy(two, 0, mislabelled, 0, mislabelled.Length);
			Write(mislabelled, 4, KingdomCuriosityLeadCodec.FirstWireVersion);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(mislabelled);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, book.State,
				"a revision 1 frame has no room for a revision 2 row, however well formed");
			Assert.AreEqual(0, book.Rows.Count);
			CollectionAssert.AreEqual(mislabelled, book.OpaquePayload);
		}

		/// <summary>A row must be exactly one of the two lawful shapes. A revision 2 row with no
		/// category claims a field it does not carry; a revision 1 row with one carries a field it
		/// could never have learned.</summary>
		[Test]
		public void ARowMustMatchItsOwnDeclaredRevisionInBothDirections()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("one"), Notes(),
				out _, out string failure), failure);
			Assert.AreEqual(KingdomCuriosityReceipt.CategoryVersion, book.Rows[0].Version);

			book.Rows[0].NoteCategory = null;
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(book, out _, out failure),
				"a revision 2 row with no category must not be written");
			StringAssert.Contains("does not satisfy its own rules", failure,
				"the row must be refused by the book's rules, not by the writer running out of "
					+ "a string to write");

			book.Rows[0].Version = KingdomCuriosityReceipt.FirstVersion;
			book.Rows[0].NoteCategory = "Historic Sites";
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(book, out _, out failure),
				"a revision 1 row carrying a category must not be written");
			StringAssert.Contains("does not satisfy its own rules", failure);

			book.Rows[0].NoteCategory = null;
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out _, out failure), failure);
		}

		/// <summary>A book whose category a revision 1 row cannot testify to is not asked about
		/// it. A revision 2 row is, and a changed category makes the note a different one.</summary>
		[Test]
		public void CategoryIsComparedOnlyWhereTheReceiptCanTestifyToIt()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("one"), Notes(),
				out KingdomCuriosityReceipt current, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityRules.SameForeignNote(current,
				new KingdomCuriosityNote(current.NoteId, current.Locator, current.NoteText,
					"Historic Sites", true)));
			Assert.IsFalse(KingdomCuriosityRules.SameForeignNote(current,
				new KingdomCuriosityNote(current.NoteId, current.Locator, current.NoteText,
					"Ruins", true)),
				"a revision 2 receipt records the category and a changed one is a changed note");

			KingdomCuriosityReceipt migrated = current.Copy();
			migrated.Version = KingdomCuriosityReceipt.FirstVersion;
			migrated.NoteCategory = null;
			Assert.IsTrue(KingdomCuriosityRules.SameForeignNote(migrated,
				new KingdomCuriosityNote(migrated.NoteId, migrated.Locator, migrated.NoteText,
					"Ruins", true)),
				"a revision 1 receipt stored no category, so silence must not be read as denial");
		}

		/// <summary>A book whose retained bytes verify as a future, but as a <i>different</i>
		/// future than it claims, is refused too: the declared revision is part of what a caller
		/// asserts, and an assertion that does not match the evidence is not a smaller lie.</summary>
		[Test]
		public void AFutureBookThatMisdeclaresItsOwnRevisionIsRefused()
		{
			byte[] future = Future(0x31554354, 7);
			KingdomCuriosityBook book = KingdomCuriosityLeadCodec.DecodeCuriosity(future);
			Assert.AreEqual(7, book.OpaqueVersion);
			book.OpaqueVersion = 8;
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] bytes,
				out string failure));
			Assert.IsNull(bytes);
			StringAssert.Contains("claims revision 8", failure);
		}

		/// <summary>A caller cannot launder a quarantined book into a future by relabelling it,
		/// nor a compatible book into one by attaching a payload it did not come from.</summary>
		[Test]
		public void RelabellingNeverChangesWhatTheBytesAre()
		{
			byte[] stub = { 1, 2, 3, 4, 5 };
			KingdomCuriosityBook relabelled = KingdomCuriosityLeadCodec.DecodeCuriosity(stub);
			relabelled.State = KingdomCuriosityBookState.FutureOpaque;
			relabelled.OpaqueVersion = 5;
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(relabelled, out _, out string f));
			Assert.IsNotEmpty(f);

			KingdomCivicLeadBook leadRelabelled = KingdomCuriosityLeadCodec.DecodeLeads(stub);
			leadRelabelled.State = KingdomCuriosityBookState.FutureOpaque;
			leadRelabelled.OpaqueVersion = 5;
			Assert.IsFalse(KingdomCuriosityLeadCodec.TryEncode(leadRelabelled, out _, out f));
			Assert.IsNotEmpty(f);
		}

		// ---- fixtures -------------------------------------------------------------------

		internal static byte[] Future(int magic, int version)
		{
			byte[] body = new byte[24];
			Write(body, 0, magic); Write(body, 4, version);
			for (int i = 8; i < body.Length; i++) body[i] = (byte)(0xA0 + i);
			byte[] digest = KingdomCuriosityLeadCodec.Digest(body, body.Length);
			byte[] all = new byte[body.Length + KingdomCuriosityLeadCodec.DigestBytes];
			Buffer.BlockCopy(body, 0, all, 0, body.Length);
			Buffer.BlockCopy(digest, 0, all, body.Length, digest.Length);
			return all;
		}

		private static byte[] Tampered(byte[] bytes)
		{
			byte[] copy = (byte[])bytes.Clone(); copy[10] ^= 0xFF; return copy;
		}

		private static byte[] Truncated(byte[] bytes)
		{
			byte[] copy = new byte[bytes.Length - 1];
			Buffer.BlockCopy(bytes, 0, copy, 0, copy.Length); return copy;
		}

		/// <summary>Re-seals a payload this test just edited, so the assertion under it is about
		/// the edit and not about the digest the edit happened to break.</summary>
		private static byte[] Reseal(byte[] bytes)
		{
			int version = KingdomCuriosityLeadCodec.ReadInt32(bytes, 4);
			if (version < KingdomCuriosityLeadCodec.FirstDigestVersion) return bytes;
			int bodyEnd = bytes.Length - KingdomCuriosityLeadCodec.DigestBytes;
			byte[] digest = KingdomCuriosityLeadCodec.Digest(bytes, bodyEnd);
			Buffer.BlockCopy(digest, 0, bytes, bodyEnd, digest.Length);
			return bytes;
		}

		private static void Write(byte[] bytes, int offset, int value)
		{
			bytes[offset] = (byte)value; bytes[offset + 1] = (byte)(value >> 8);
			bytes[offset + 2] = (byte)(value >> 16); bytes[offset + 3] = (byte)(value >> 24);
		}

		private static void Write(byte[] bytes, int offset, long value)
		{
			for (int i = 0; i < 8; i++) bytes[offset + i] = (byte)(value >> i * 8);
		}

		private static int IndexOfAscii(byte[] bytes, string needle)
		{
			for (int i = 0; i + needle.Length <= bytes.Length; i++)
			{
				int j = 0;
				while (j < needle.Length && bytes[i + j] == (byte)needle[j]) j++;
				if (j == needle.Length) return i;
			}
			return -1;
		}

		private static byte[] Valid()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L, Cause("one"), Notes(),
				out _, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] bytes,
				out failure), failure);
			return bytes;
		}

		/// <summary>A revision 2 curiosity book: one row, carrying its category.</summary>
		private static byte[] ValidWithCategory()
		{
			byte[] bytes = Valid();
			Assert.AreEqual(KingdomCuriosityLeadCodec.CuriosityHighestKnownVersion,
				KingdomCuriosityLeadCodec.ReadInt32(bytes, 4),
				"a freshly prepared row carries a category, so its book is revision 2");
			return bytes;
		}

		private static byte[] ValidLeads()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0L, LeadCause(0), 0, true,
				out _, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCodec.TryEncode(book, out byte[] bytes,
				out failure), failure);
			return bytes;
		}

		private static KingdomCivicLeadBook FullLeadBook()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			for (int i = 0; i < KingdomCivicLeadBook.MaxRows; i++)
				Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, book.Revision, LeadCause(i),
					i, true, out _, out string failure), failure);
			return book;
		}

		internal static KingdomCuriosityCause Cause(string suffix) => new KingdomCuriosityCause
		{
			SourceId = "taf:source:" + suffix, SourceVersion = 1,
			SettlementId = "taf:settlement:one", CuratorResidentId = 7,
			CuratorName = "Ari", CuratorObjectId = "taf:object:ari",
			Reason = "because the lower cistern bears our mason's exact mark",
			RequiredCategory = "Historic Sites", CompletedTick = 20L
		};

		internal static System.Collections.Generic.List<KingdomCuriosityNote> Notes()
			=> new System.Collections.Generic.List<KingdomCuriosityNote>
			{
				new KingdomCuriosityNote("taf:note:one", "JoppaWorld.10.20.1.2.10",
					"the drowned cistern", "Historic Sites", true)
			};

		internal static KingdomCivicLeadCause LeadCause(int i) => new KingdomCivicLeadCause
		{
			SourceId = "taf:delve:" + i, SourceVersion = 1,
			SettlementId = "taf:settlement:one", Locator = "JoppaWorld." + i + ".1.1.1.10",
			Title = "the newly opened lower commons",
			AuthoredReason = "A completed city delve opened this exact lower landing.",
			CompletedTick = i
		};
	}
}
#endif
