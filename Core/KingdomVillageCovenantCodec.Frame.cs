using System;
using System.Security.Cryptography;

namespace ThousandAndFirst
{
	/// <summary>How an archive's bytes are judged before one field of them is believed.</summary>
	internal enum KingdomVillageCovenantFrameKind : byte
	{
		/// <summary>Nothing here can be trusted, including its own account of itself.</summary>
		Unreadable = 0,

		/// <summary>Whole, and at a revision this build can interpret.</summary>
		Current = 1,

		/// <summary>Whole, and at a revision only a later build can interpret.</summary>
		Future = 2
	}

	internal readonly struct KingdomVillageCovenantFrame
	{
		internal readonly KingdomVillageCovenantFrameKind Kind;
		internal readonly int WireVersion;
		internal readonly int PayloadStart;
		internal readonly int PayloadLength;
		internal readonly string Fault;

		internal KingdomVillageCovenantFrame(KingdomVillageCovenantFrameKind kind, int wireVersion,
			int payloadStart, int payloadLength, string fault)
		{
			Kind = kind;
			WireVersion = wireVersion;
			PayloadStart = payloadStart;
			PayloadLength = payloadLength;
			Fault = fault;
		}
	}

	/// <summary>
	/// The frame around a covenant archive, and everything that must be settled about a payload
	/// before one field of it is believed.
	/// <para>
	/// Nothing here knows what a covenant is. That is the point: the questions it answers &mdash;
	/// are these this family's bytes, is the declared revision one that could ever have been
	/// allocated, does the framed length account for every byte present, does the digest still
	/// cover everything in front of it &mdash; are all answerable without understanding the
	/// contents, and answering them first is what lets a payload from a build too new to read be
	/// shown whole rather than mourned.
	/// </para>
	/// </summary>
	public static partial class KingdomVillageCovenantCodec
	{
		/// <summary>
		/// Takes one private copy of a caller's bytes, and takes it before anything is decided.
		/// <para>
		/// Everything after this point &mdash; the magic, the digest, the rows, and the evidence a
		/// quarantine keeps &mdash; reads the copy. A caller that hands over an array it still holds
		/// could otherwise edit it between the digest check and the parse, and every guard here
		/// would have been applied to bytes that are no longer the bytes being read.
		/// </para>
		/// </summary>
		internal static byte[] Ingress(byte[] bytes, int max, out string fault)
		{
			fault = null;
			if (bytes == null) { fault = "there are no bytes at all"; return null; }
			int length = bytes.Length;
			if (length > max)
			{
				fault = "the archive is " + length + " bytes, past the " + max
					+ "-byte cap this family accepts";
				return null;
			}
			byte[] snapshot = new byte[length];
			Buffer.BlockCopy(bytes, 0, snapshot, 0, length);
			return snapshot;
		}

		/// <summary>
		/// Verify integrity before interpret. Every question answerable without understanding the
		/// contents is answered here: is this the family's wire, is the declared revision one that
		/// could ever have been allocated, does the framed length agree with the bytes present, and
		/// does the digest still cover everything in front of it.
		/// </summary>
		/// <param name="bytes">A snapshot from <see cref="Ingress"/>. Never a caller's array.</param>
		internal static KingdomVillageCovenantFrame Inspect(byte[] bytes)
		{
			if (bytes.Length < EnvelopeOverheadBytes)
				return Unreadable("the archive is shorter than the " + EnvelopeOverheadBytes
					+ "-byte frame");
			if (ReadInt32(bytes, 0) != Magic)
				return Unreadable("the magic is not this family's");
			int version = ReadInt32(bytes, MagicBytes);
			if (version < FirstWireVersion)
				return Unreadable("the wire revision is " + version
					+ ", which no build could have allocated");
			int length = ReadInt32(bytes, MagicBytes + VersionBytes);
			int start = MagicBytes + VersionBytes + LengthBytes;
			if (length < 0 || length > MaxPayloadBytes
				|| bytes.Length != start + length + DigestBytes)
				return Unreadable("the framed payload length does not account for every byte "
					+ "present");
			if (!DigestStands(bytes))
				return Unreadable("the digest no longer covers the bytes in front of it");
			return new KingdomVillageCovenantFrame(version > CurrentWireVersion
				? KingdomVillageCovenantFrameKind.Future
				: KingdomVillageCovenantFrameKind.Current, version, start, length, null);
		}

		/// <summary>
		/// Re-emits an archive this build may not author.
		/// <para>
		/// The caller's own state is evidence of nothing. A future archive's retained bytes are
		/// copied and re-inspected from scratch, and must still declare the very revision the
		/// archive claims, before one byte of them is written back; anything else is a forged
		/// opacity and is refused. A quarantined archive is never written back at all &mdash; its
		/// bytes are kept as evidence of a failure, and evidence is not a save file.
		/// </para>
		/// </summary>
		internal static bool TryReemitOpaque(KingdomVillageCovenantArchive archive,
			out byte[] bytes, out string failure)
		{
			bytes = null;
			if (archive.State == KingdomVillageCovenantState.Quarantined)
				return KingdomVillageCovenantRules.Fail("the covenant archive is quarantined "
					+ "evidence and must not be written back over the records it failed to read",
					out failure);
			byte[] snapshot = Ingress(archive.OpaquePayload, MaxEnvelopeBytes, out string ingress);
			if (snapshot == null)
				return KingdomVillageCovenantRules.Fail("the covenant archive calls itself a later "
					+ "build's, but its retained bytes could not be read at all (" + ingress + ")",
					out failure);
			KingdomVillageCovenantFrame frame = Inspect(snapshot);
			if (frame.Kind != KingdomVillageCovenantFrameKind.Future)
				return KingdomVillageCovenantRules.Fail("the covenant archive calls itself a later "
					+ "build's, but its retained bytes do not verify as one ("
					+ (frame.Fault ?? "they are this build's own revision") + ")", out failure);
			if (frame.WireVersion != archive.OpaqueVersion)
				return KingdomVillageCovenantRules.Fail("the covenant archive claims revision "
					+ archive.OpaqueVersion + " while its retained bytes declare "
					+ frame.WireVersion, out failure);
			bytes = snapshot;
			failure = "";
			return true;
		}

		internal static byte[] Digest(byte[] payload, int length)
		{
			using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(payload, 0, length);
		}

		private static bool DigestStands(byte[] bytes)
		{
			byte[] expected = Digest(bytes, bytes.Length - DigestBytes);
			int offset = bytes.Length - DigestBytes;
			int differences = 0;
			for (int i = 0; i < DigestBytes; i++)
				differences |= expected[i] ^ bytes[offset + i];
			return differences == 0;
		}

		internal static int ReadInt32(byte[] bytes, int offset)
		{
			return bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16
				| bytes[offset + 3] << 24;
		}

		private static KingdomVillageCovenantFrame Unreadable(string fault)
		{
			return new KingdomVillageCovenantFrame(KingdomVillageCovenantFrameKind.Unreadable,
				0, 0, 0, fault);
		}

		internal static KingdomVillageCovenantArchive Quarantine(byte[] bytes, string fault)
		{
			return new KingdomVillageCovenantArchive
			{
				State = KingdomVillageCovenantState.Quarantined,
				OpaquePayload = bytes,
				Fault = "the covenant archive would not read: " + fault
			};
		}
	}
}
