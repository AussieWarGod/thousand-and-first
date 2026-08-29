using System;
using System.Security.Cryptography;

namespace ThousandAndFirst
{
	/// <summary>How a book's bytes are judged before a single field of them is believed.</summary>
	internal enum KingdomCuriosityFrameKind : byte
	{
		/// <summary>Nothing about these bytes can be trusted, including their own claims.</summary>
		Unreadable = 0,

		/// <summary>Intact, and at a revision this build can interpret.</summary>
		Current = 1,

		/// <summary>Intact, and at a revision only a later build can interpret.</summary>
		Future = 2
	}

	internal readonly struct KingdomCuriosityFrame
	{
		internal readonly KingdomCuriosityFrameKind Kind;
		internal readonly int WireVersion;

		/// <summary>Where the rows stop: the whole payload at revision 1, and everything before
		/// the digest from revision 2 on.</summary>
		internal readonly int BodyEnd;
		internal readonly string Fault;

		internal KingdomCuriosityFrame(KingdomCuriosityFrameKind kind, int wireVersion,
			int bodyEnd, string fault)
		{ Kind = kind; WireVersion = wireVersion; BodyEnd = bodyEnd; Fault = fault; }
	}

	public static partial class KingdomCuriosityLeadCodec
	{
		/// <summary>
		/// Takes one private copy of a caller's bytes, and takes it before anything is decided.
		/// <para>
		/// Everything after this point &mdash; the length, the magic, the digest, the rows, and
		/// the evidence a quarantine keeps &mdash; reads the copy. A caller that hands over an
		/// array it still holds a reference to could otherwise edit it between the digest check
		/// and the parse, and every guard in this file would have been applied to bytes that are
		/// no longer the bytes being read.
		/// </para>
		/// </summary>
		/// <param name="max">The accepted cap. Bytes past it are refused without being copied.</param>
		internal static byte[] Ingress(byte[] bytes, int max, out string fault)
		{
			fault = null;
			if (bytes == null) { fault = "there are no bytes at all"; return null; }
			int length = bytes.Length;
			if (length > max)
			{
				fault = "the payload is " + length + " bytes, past the " + max
					+ "-byte cap this family has ever accepted";
				return null;
			}
			byte[] snapshot = new byte[length];
			Buffer.BlockCopy(bytes, 0, snapshot, 0, length);
			return snapshot;
		}

		/// <summary>
		/// Verify integrity before interpret.
		/// <para>
		/// Every question that can be answered without understanding the contents is answered
		/// here: is this family's wire, does it declare a revision that could ever have been
		/// allocated, and &mdash; from revision 2 on &mdash; does its own digest still cover its
		/// own bytes. Only a payload that survives all of that is sorted into "we can read this"
		/// and "a later build wrote this"; one that fails any of it is damage, and is never
		/// dressed up as a future.
		/// </para>
		/// <para>
		/// The digest proves nothing about <i>who</i> wrote these bytes. SHA-256 detects change;
		/// it is not a signature and there is no secret behind it, so a deliberate edit that
		/// recomputes the tail passes exactly as the original would. What it buys is the one thing
		/// this frame actually needs: a payload from a build we cannot read can still be shown to
		/// be internally whole, which is what separates a lawful successor from a bad sector.
		/// </para>
		/// </summary>
		/// <param name="bytes">A snapshot from <see cref="Ingress"/>. Never a caller's array.</param>
		internal static KingdomCuriosityFrame Inspect(byte[] bytes, int magic,
			int highestKnownVersion)
		{
			if (bytes.Length < HeaderBytes)
				return Unreadable("the payload is shorter than the " + HeaderBytes + "-byte frame");
			if (ReadInt32(bytes, 0) != magic) return Unreadable("the magic is not this family's");
			int version = ReadInt32(bytes, MagicBytes);
			if (version < FirstWireVersion)
				return Unreadable("the wire revision is " + version
					+ ", which no build could have allocated");
			if (version < FirstDigestVersion)
				return new KingdomCuriosityFrame(KingdomCuriosityFrameKind.Current, version,
					bytes.Length, null);
			if (bytes.Length < HeaderBytes + DigestBytes)
				return Unreadable("revision " + version + " must close with a " + DigestBytes
					+ "-byte digest and there is no room for one");
			if (!DigestStands(bytes))
				return Unreadable("the digest no longer covers the bytes in front of it");
			return new KingdomCuriosityFrame(version > highestKnownVersion
				? KingdomCuriosityFrameKind.Future : KingdomCuriosityFrameKind.Current,
				version, bytes.Length - DigestBytes, null);
		}

		/// <summary>
		/// Re-emits a book this build may not author.
		/// <para>
		/// The caller's own <c>State</c> is evidence of nothing. A future book's retained bytes
		/// are copied and re-inspected from scratch, and must still declare the very revision the
		/// book claims, before a single byte of them is written back; anything else is a forged
		/// opacity and is refused. A quarantined book is never written back at all &mdash; its
		/// bytes are kept as evidence of a failure, and evidence is not a save file.
		/// </para>
		/// </summary>
		internal static bool TryReemitOpaque(KingdomCuriosityBookState state, byte[] retained,
			int declaredVersion, int magic, int max, int highestKnownVersion, string family,
			out byte[] bytes, out string failure)
		{
			bytes = null; failure = null;
			// Checked again here, and not only at the writer's door. This is internal, so a later
			// caller can reach it directly, and the one thing it must never do is hand back bytes
			// on the strength of a state nobody can read.
			if (!Defined(state))
				return Refuse(UndefinedState(family, state)
					+ "; its retained bytes will not be re-emitted", out failure);
			if (state == KingdomCuriosityBookState.Quarantined)
				return Refuse("the " + family + " book is quarantined evidence and must not be "
					+ "written back over the records it failed to read", out failure);
			byte[] snapshot = Ingress(retained, max, out string ingress);
			if (snapshot == null)
				return Refuse("the " + family + " book calls itself a later build's, but its "
					+ "retained bytes could not be read at all (" + ingress + ")", out failure);
			KingdomCuriosityFrame frame = Inspect(snapshot, magic, highestKnownVersion);
			if (frame.Kind != KingdomCuriosityFrameKind.Future)
				return Refuse("the " + family + " book calls itself a later build's, but its "
					+ "retained bytes do not verify as one (" + (frame.Fault
						?? "they are this build's own revision") + ")", out failure);
			if (frame.WireVersion != declaredVersion)
				return Refuse("the " + family + " book claims revision " + declaredVersion
					+ " while its retained bytes declare " + frame.WireVersion, out failure);
			bytes = snapshot;
			return true;
		}

		/// <summary>The digest a revision 2 or later frame must end with.</summary>
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

		private static KingdomCuriosityFrame Unreadable(string fault)
		{
			return new KingdomCuriosityFrame(KingdomCuriosityFrameKind.Unreadable, 0, 0, fault);
		}
	}
}
