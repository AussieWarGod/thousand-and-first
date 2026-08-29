#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Behavioural coverage for the civic-memory envelope: what it accepts, what it refuses, and
	/// what it is obliged to carry unchanged.
	/// <para>
	/// The malformed envelopes are built by the transcription below rather than by the production
	/// encoder, so a test that says "this is what a duplicate id looks like on the wire" is
	/// making its own claim about the format instead of agreeing with the code under test. If the
	/// two ever disagree, that is the point.
	/// </para>
	/// </summary>
	[TestFixture]
	public class KingdomCivicMemoryCodecTests
	{
		// Transcribed from Core/KingdomCivicMemoryCodec.cs. Deliberately a second copy.
		private const int Magic = 0x4D434654;

		private static byte[] Filled(int Length, byte Value)
		{
			byte[] bytes = new byte[Length];
			for (int i = 0; i < Length; i++) bytes[i] = Value;
			return bytes;
		}

		private static KingdomCivicMemorySection Section(int Id, int Length)
		{
			return new KingdomCivicMemorySection(Id, Filled(Length, (byte)(Id & 0xFF)));
		}

		private static KingdomCivicMemoryState StateOf(params KingdomCivicMemorySection[] Sections)
		{
			return KingdomCivicMemoryState.Of(new List<KingdomCivicMemorySection>(Sections), 0L);
		}

		/// <summary>
		/// An independent writer for the wire format, so malformed cases can be built by hand.
		/// Pass <paramref name="IntegrityValid"/> false to leave a deliberately wrong digest.
		/// </summary>
		private static byte[] Envelope(int Version, IList<int> Ids, IList<byte[]> Payloads,
			int DeclaredCount, bool IntegrityValid)
		{
			using (MemoryStream body = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(body))
			{
				writer.Write(Magic);
				writer.Write(Version);
				writer.Write(DeclaredCount);
				for (int i = 0; i < Ids.Count; i++)
				{
					writer.Write(Ids[i]);
					writer.Write(Payloads[i].Length);
					writer.Write(Payloads[i]);
				}
				writer.Flush();
				byte[] framed = body.ToArray();
				byte[] hash;
				using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(framed);
				if (!IntegrityValid) hash[0] ^= 0xFF;
				byte[] envelope = new byte[framed.Length + 32];
				Buffer.BlockCopy(framed, 0, envelope, 0, framed.Length);
				Buffer.BlockCopy(hash, 0, envelope, framed.Length, 32);
				return envelope;
			}
		}

		private static byte[] Envelope(int Version, IList<int> Ids, IList<byte[]> Payloads)
		{
			return Envelope(Version, Ids, Payloads, Ids.Count, true);
		}

		private static readonly int[] AllIds =
		{
			KingdomCivicMemoryLimits.SectionCivicArtifacts,
			KingdomCivicMemoryLimits.SectionCivicPractice,
			KingdomCivicMemoryLimits.SectionBodyHistory,
			KingdomCivicMemoryLimits.SectionCuriosity,
			KingdomCivicMemoryLimits.SectionCivicLeads,
			KingdomCivicMemoryLimits.SectionTreaty,
			KingdomCivicMemoryLimits.SectionCommunalRite,
			KingdomCivicMemoryLimits.SectionGuestFeast
		};

		[Test]
		public void RoundTripsEverySectionByteForByte()
		{
			List<KingdomCivicMemorySection> sections = new List<KingdomCivicMemorySection>();
			for (int i = 0; i < AllIds.Length; i++) sections.Add(Section(AllIds[i], 16 + i));
			KingdomCivicMemoryState decoded = KingdomCivicMemoryCodec.Decode(
				KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(sections, 0L)), 7L);

			Assert.AreEqual(AllIds.Length, decoded.Count);
			Assert.AreEqual(7L, decoded.Revision);
			Assert.IsFalse(decoded.Quarantined);
			Assert.IsFalse(decoded.IsEmpty);
			for (int i = 0; i < AllIds.Length; i++)
				CollectionAssert.AreEqual(sections[i].Payload(),
					decoded.Section(AllIds[i]).Payload());
		}

		[Test]
		public void WritesTheSameBytesWhateverOrderSectionsArriveIn()
		{
			byte[] ascending = KingdomCivicMemoryCodec.Encode(StateOf(
				Section(1, 8), Section(3, 8), Section(6, 8)));
			byte[] shuffled = KingdomCivicMemoryCodec.Encode(StateOf(
				Section(6, 8), Section(1, 8), Section(3, 8)));
			CollectionAssert.AreEqual(ascending, shuffled,
				"section order on the wire must not depend on the caller's list order");
		}

		[Test]
		public void EmptyStateRoundTripsAsEmptyAndNotAsQuarantined()
		{
			KingdomCivicMemoryState decoded = KingdomCivicMemoryCodec.Decode(
				KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Empty()), 0L);
			Assert.IsTrue(decoded.IsEmpty);
			Assert.IsFalse(decoded.Quarantined);
		}

		[Test]
		public void RefusesTruncationAtEveryByteOfTheEnvelope()
		{
			byte[] whole = KingdomCivicMemoryCodec.Encode(StateOf(
				Section(1, 5), Section(2, 5), Section(3, 5), Section(6, 5)));
			for (int length = 0; length < whole.Length; length++)
			{
				byte[] cut = new byte[length];
				Buffer.BlockCopy(whole, 0, cut, 0, length);
				Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(cut, 0L),
					"a " + length + "-byte prefix of a " + whole.Length
					+ "-byte envelope must be refused, not partly believed");
			}
			Assert.DoesNotThrow(() => KingdomCivicMemoryCodec.Decode(whole, 0L));
		}

		[Test]
		public void RefusesABadMagic()
		{
			byte[] bytes = KingdomCivicMemoryCodec.Encode(StateOf(Section(1, 8)));
			bytes[0] ^= 0xFF;
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(bytes, 0L));
		}

		[Test]
		public void RefusesAVersionBelowTheFirstThisFormatEverHad()
		{
			List<int> ids = new List<int> { 1 };
			List<byte[]> payloads = new List<byte[]> { Filled(8, 1) };
			Assert.Throws<InvalidDataException>(
				() => KingdomCivicMemoryCodec.Decode(Envelope(0, ids, payloads), 0L),
				"version 0 is below the readable range");
			Assert.Throws<InvalidDataException>(
				() => KingdomCivicMemoryCodec.Decode(Envelope(-1, ids, payloads), 0L),
				"a negative version is not a future one");
			Assert.DoesNotThrow(() => KingdomCivicMemoryCodec.Decode(
				Envelope(KingdomCivicMemoryCodec.CurrentWireVersion, ids, payloads), 0L));
		}

		[Test]
		public void RefusesSectionIdsBelowTheFirstThatCouldEverBeAllocated()
		{
			foreach (int id in new[] { 0, -1, int.MinValue })
			{
				int subject = id;
				Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(
					Envelope(1, new List<int> { subject },
						new List<byte[]> { Filled(4, 1) }), 0L),
					"section id " + subject + " must be refused, not filed under future");
				Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Encode(
					StateOf(new KingdomCivicMemorySection(subject, Filled(4, 1)))),
					"the writer must refuse to emit section id " + subject);
			}
		}

		[Test]
		public void RefusesANullSectionPayloadInsteadOfInventingEmptyBytes()
		{
			Assert.Throws<ArgumentNullException>(
				() => new KingdomCivicMemorySection(1, null));
		}

		[Test]
		public void RefusesAForgedFutureStateAtEncode()
		{
			KingdomCivicMemoryState forged = KingdomCivicMemoryState.FutureOuter(
				new byte[KingdomCivicMemoryLimits.EnvelopeOverheadBytes],
				KingdomCivicMemoryCodec.CurrentWireVersion + 1, 0L);
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Encode(forged),
				"future opacity is earned by a valid retained frame, not a caller-set disposition");
		}

		[Test]
		public void RefusesDuplicateSectionIdsOnBothSidesOfTheWire()
		{
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Encode(
				StateOf(Section(2, 8), Section(2, 8))),
				"the writer must refuse to emit the same id twice");
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(
				Envelope(1, new List<int> { 2, 2 },
					new List<byte[]> { Filled(8, 1), Filled(8, 2) }), 0L),
				"the reader must refuse an envelope carrying the same id twice");
		}

		[Test]
		public void RefusesSectionIdsThatAreNotAscending()
		{
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(
				Envelope(1, new List<int> { 3, 1 },
					new List<byte[]> { Filled(4, 1), Filled(4, 2) }), 0L),
				"descending ids would make the same state encodable two different ways");
		}

		[Test]
		public void RefusesASectionCountThatDisagreesWithTheSectionsPresent()
		{
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(
				Envelope(1, new List<int> { 1 }, new List<byte[]> { Filled(4, 1) }, 2, true), 0L),
				"a count promising more sections than follow must be refused");
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(
				Envelope(1, new List<int> { 1, 2 },
					new List<byte[]> { Filled(4, 1), Filled(4, 2) }, 1, true), 0L),
				"a count hiding a section must be refused as trailing bytes");
		}

		[Test]
		public void RefusesASectionCountBeyondTheEnvelopeReserve()
		{
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(
				Envelope(1, new List<int>(), new List<byte[]>(),
					KingdomCivicMemoryLimits.MaxSections + 1, true), 0L));
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(
				Envelope(1, new List<int>(), new List<byte[]>(), -1, true), 0L));
		}

		[Test]
		public void FutureOuterDoesNotPretendItsMiddleUsesVersionOneFraming()
		{
			byte[] future = Envelope(KingdomCivicMemoryCodec.CurrentWireVersion + 1,
				new List<int>(), new List<byte[]>(),
				KingdomCivicMemoryLimits.MaxSections + 1, true);
			KingdomCivicMemoryState decoded = KingdomCivicMemoryCodec.Decode(future, 0L);

			Assert.IsTrue(decoded.IsFutureOuter,
				"a v2 middle is opaque; its first word must not be interpreted as v1 section count");
			CollectionAssert.AreEqual(future, KingdomCivicMemoryCodec.Encode(decoded));
		}

		[Test]
		public void RefusesTrailingBytesAfterTheHash()
		{
			byte[] whole = KingdomCivicMemoryCodec.Encode(StateOf(Section(1, 8)));
			byte[] padded = new byte[whole.Length + 1];
			Buffer.BlockCopy(whole, 0, padded, 0, whole.Length);
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(padded, 0L),
				"one byte past the hash must be enough to refuse the whole envelope");
		}

		[Test]
		public void RefusesAnEnvelopeWhoseHashDoesNotMatch()
		{
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(
				Envelope(1, new List<int> { 1 }, new List<byte[]> { Filled(8, 1) }, 1, false), 0L));
		}

		[Test]
		public void RefusesAPayloadLengthThatTheStreamCannotSatisfy()
		{
			// A declared length inside the section cap but past the end of the stream: the reader
			// must disbelieve the header rather than allocate against it.
			// Layout: magic 0-3, version 4-7, count 8-11, first id 12-15, first length 16-19.
			byte[] bytes = Envelope(1, new List<int> { 1 }, new List<byte[]> { Filled(8, 1) });
			bytes[16] = 0x00; bytes[17] = 0x10; bytes[18] = 0x00; bytes[19] = 0x00;
			byte[] framed = new byte[bytes.Length - 32];
			Buffer.BlockCopy(bytes, 0, framed, 0, framed.Length);
			using (SHA256 sha = SHA256.Create())
				Buffer.BlockCopy(sha.ComputeHash(framed), 0, bytes, framed.Length, 32);
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Decode(bytes, 0L),
				"4096 declared bytes with 40 left in the stream must be refused");
		}
	}
}
#endif
