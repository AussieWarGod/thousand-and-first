using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomCivicMemoryCodec
	{
		/// <summary>
		/// Reads an envelope, or throws.
		/// <para>
		/// The hash is checked before section framing is interpreted, and that ordering is the whole
		/// basis of forward compatibility. The hash detects accidental change; it is not a signature
		/// and makes no claim about who produced the bytes. If an intact, structurally valid envelope
		/// names a version above ours, the honest answer is that we cannot read it, not that it is
		/// broken. Such an envelope is returned
		/// whole as <see cref="KingdomCivicMemoryDisposition.FutureOuter"/> and goes back to disk
		/// unchanged. The 44-byte frame is a permanent promise for exactly this reason &mdash; see
		/// <see cref="KingdomCivicMemoryLimits.EnvelopeOverheadBytes"/>.
		/// </para>
		/// <para>
		/// Everything else here is a refusal rather than a repair. A reader that quietly dropped a
		/// bad section, or accepted a short one, or ignored what came after the hash, would hand
		/// back a state that looks like a smaller save instead of a broken one &mdash; and the
		/// caller would then write that smaller save back over the real one.
		/// </para>
		/// <para>
		/// A section id this build has never heard of is not an error. It is copied out untouched,
		/// counted against the same budget as anything else, and handed back so it can be written
		/// again exactly as it was found. An id below <see cref="KingdomCivicMemoryLimits.MinSectionId"/>
		/// is not a future id and is refused outright.
		/// </para>
		/// </summary>
		/// <param name="Bytes">The envelope, as read from the save block.</param>
		/// <param name="Revision">The revision to stamp on the resulting state.</param>
		public static KingdomCivicMemoryState Decode(byte[] Bytes, long Revision)
		{
			if (Bytes == null) throw new InvalidDataException("civic memory envelope is absent");
			if (Bytes.Length > KingdomCivicMemoryLimits.MaxEnvelopeBytes)
				throw new InvalidDataException("civic memory envelope exceeds its cap");
			byte[] snapshot = (byte[])Bytes.Clone();
			if (snapshot.Length < KingdomCivicMemoryLimits.EnvelopeOverheadBytes)
				throw new InvalidDataException("civic memory envelope is shorter than its frame");
			try
			{
				using (MemoryStream stream = new MemoryStream(snapshot, false))
				using (BinaryReader reader = Reader(stream))
				{
					if (reader.ReadInt32() != Magic)
						throw new InvalidDataException("civic memory magic does not match");
					int version = reader.ReadInt32();
					if (version < 1) throw new InvalidDataException(
						"civic memory envelope version " + version + " is below the first version "
						+ "this format ever had");
					VerifyIntegrity(snapshot);
					// Only magic, version, total size and the trailing digest are frozen across outer
					// versions. The middle belongs to that version and is deliberately not parsed by v1.
					if (version > CurrentWireVersion)
						return KingdomCivicMemoryState.FutureOuter(snapshot, version, Revision);
					List<KingdomCivicMemorySection> sections = ReadSections(reader, stream,
						snapshot.Length - HashBytes);
					if (stream.Position != snapshot.Length - HashBytes)
						throw new InvalidDataException("civic memory envelope has trailing bytes");
					return KingdomCivicMemoryState.Of(sections, Revision);
				}
			}
			catch (EndOfStreamException e)
			{
				throw new InvalidDataException("civic memory envelope is truncated", e);
			}
			catch (DecoderFallbackException e)
			{
				throw new InvalidDataException("civic memory envelope is not strict UTF-8", e);
			}
		}

		/// <summary>
		/// Checks the trailing hash over everything before it. Version-independent by contract.
		/// </summary>
		private static void VerifyIntegrity(byte[] Bytes)
		{
			byte[] framed = new byte[Bytes.Length - HashBytes];
			byte[] tail = new byte[HashBytes];
			System.Buffer.BlockCopy(Bytes, 0, framed, 0, framed.Length);
			System.Buffer.BlockCopy(Bytes, framed.Length, tail, 0, HashBytes);
			if (!SameHash(tail, Hash(framed)))
				throw new InvalidDataException("civic memory envelope failed its integrity check");
		}

		private static List<KingdomCivicMemorySection> ReadSections(BinaryReader Reader,
			MemoryStream Stream, int Ceiling)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > KingdomCivicMemoryLimits.MaxSections)
				throw new InvalidDataException("civic memory section count " + count
					+ " is outside 0 through " + KingdomCivicMemoryLimits.MaxSections);
			List<KingdomCivicMemorySection> sections = new List<KingdomCivicMemorySection>();
			long cumulative = 0L;
			int previous = int.MinValue;
			for (int i = 0; i < count; i++)
			{
				int id = Reader.ReadInt32();
				if (!KingdomCivicMemoryLimits.Allocatable(id)) throw new InvalidDataException(
					"civic memory section id " + id + " is below the first id that could ever be "
					+ "allocated (" + KingdomCivicMemoryLimits.MinSectionId + ")");
				if (id <= previous) throw new InvalidDataException(
					"civic memory section ids must be unique and ascending; " + id
					+ " follows " + previous);
				previous = id;
				int length = Reader.ReadInt32();
				int cap = KingdomCivicMemoryLimits.SectionCap(id);
				if (length < 0 || length > cap) throw new InvalidDataException(
					"civic memory section " + id + " declares " + length
					+ " bytes, outside its frozen cap of " + cap);
				cumulative += length;
				if (cumulative > KingdomCivicMemoryLimits.MaxCumulativePayloadBytes)
					throw new InvalidDataException("civic memory sections total more than the "
						+ "cumulative cap of "
						+ KingdomCivicMemoryLimits.MaxCumulativePayloadBytes + " bytes");
				// Guard the allocation against a declared length the stream cannot satisfy, so a
				// corrupt header cannot ask for a buffer before it is disbelieved. The ceiling is
				// the start of the hash: a section may never reach into the tail.
				if (Ceiling - Stream.Position < length) throw new EndOfStreamException();
				byte[] payload = Reader.ReadBytes(length);
				if (payload.Length != length) throw new EndOfStreamException();
				sections.Add(new KingdomCivicMemorySection(id, payload));
			}
			return sections;
		}
	}
}
