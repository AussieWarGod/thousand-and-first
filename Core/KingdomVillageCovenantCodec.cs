using System;
using System.Security.Cryptography;

namespace ThousandAndFirst
{
	/// <summary>
	/// The wire for the village-covenant archive: one magic, one frame, and one rule about
	/// strangers.
	/// <para>
	/// There is no earlier format. This family is introduced already bound to a realm and already
	/// closing with a digest, so no save on any disk carries an unbound or unsealed archive and
	/// there is nothing to migrate. That absence is worth stating plainly, because a migration path
	/// invented for a format that never existed is a door with no building behind it &mdash; and
	/// the one thing such a door reliably does is admit something.
	/// </para>
	/// <para>
	/// Verify integrity, then interpret. The length is checked and the bytes copied once; the
	/// magic, the revision and the digest are read from that copy; and only a payload that survives
	/// all of it is sorted into one this build reads and one a later build wrote. An archive whose
	/// digest still covers it and whose revision is beyond ours is
	/// <see cref="KingdomVillageCovenantState.FutureOpaque"/>: held byte-for-byte, never edited,
	/// written back exactly as it came. One at a revision we do know that will not read is
	/// <see cref="KingdomVillageCovenantState.Quarantined"/>, and keeps its real bytes as the
	/// evidence of what went wrong. A future is never called damage, and damage is never dressed
	/// up as a future.
	/// </para>
	/// <para>
	/// What the digest is not: a signature. SHA-256 detects change and says nothing whatever about
	/// who wrote the bytes, so an edit that recomputes the tail passes exactly as the original
	/// does. It is claimed for one thing only &mdash; showing that a payload too new to read is
	/// internally whole &mdash; and it is enough for that one thing.
	/// </para>
	/// </summary>
	public static partial class KingdomVillageCovenantCodec
	{
		/// <summary>"TVC1" in wire order. The magic names the family, not the revision.</summary>
		internal const int Magic = 0x31435654;

		public const int FirstWireVersion = 1;
		public const int CurrentWireVersion = 1;

		/// <summary>Magic 4, wire revision 4, payload length 4, and a SHA-256 tail of 32. This is a
		/// permanent promise rather than a v1 detail: no later revision may move the leading magic
		/// and revision or drop the trailing digest, because those are the only things a build too
		/// old to read the payload can still use to prove the payload is intact.</summary>
		public const int EnvelopeOverheadBytes = 44;
		internal const int MagicBytes = 4;
		internal const int VersionBytes = 4;
		internal const int LengthBytes = 4;
		public const int DigestBytes = 32;

		/// <summary>Four length bytes, at most seventy-seven strict UTF-8 realm-id bytes, and one
		/// bound byte. A realm id is a fixed prefix and sixty-four hex digits, so seventy-seven is
		/// exact rather than generous.</summary>
		public const int MaxRealmIdBytes = 77;
		public const int IdentityFramingBytes = 82;

		/// <summary>Book magic 4, book revision 4, archive revision 8, row count 4.</summary>
		public const int HeaderBytes = 20;

		/// <summary>Every row is length-prefixed and capped, so one wide row cannot eat the archive.</summary>
		public const int RowFramingBytes = 4;
		internal const int Int32Bytes = 4;
		internal const int Int64Bytes = 8;
		internal const int LengthPrefixBytes = 4;

		/// <summary>Eight length-prefixed strings: the realm, the receipt id, the transaction, the
		/// encoded authority, the faction key, its display name, the site and the chronicle
		/// event.</summary>
		internal const int RowStringFields = 8;

		/// <summary>
		/// The widest row this build can author, from the contracts that produce each field:
		/// a row revision, eight length prefixes, an 88-byte receipt id, a 32-byte transaction, a
		/// 77-byte realm id, a 2048-byte authority, two 768-byte names, a 205-byte locator, a
		/// 60-byte chronicle event, a sealed standing and a reservation tick.
		/// <para>
		/// 4 + 32 + 88 + 32 + 77 + 2048 + 768 + 768 + 205 + 60 + 4 + 8 = 4,094.
		/// </para>
		/// </summary>
		public const int MaxAuthoredRowBytes = Int32Bytes
			+ RowStringFields * LengthPrefixBytes
			+ KingdomVillageCovenantRules.MaxReceiptIdBytes
			+ KingdomVillageCovenantRules.MaxTransactionIdBytes
			+ MaxRealmIdBytes
			+ KingdomVillageCovenantRules.MaxAuthorityBytes
			+ KingdomVillageCovenantRules.MaxFactionIdBytes
			+ KingdomVillageCovenantRules.MaxDisplayNameBytes
			+ KingdomVillageCovenantRules.MaxZoneIdBytes
			+ KingdomVillageCovenantRules.MaxChronicleEventBytes
			+ Int32Bytes + Int64Bytes;

		/// <summary>
		/// The widest row this build will accept, and it is deliberately two bytes above what this
		/// build can author.
		/// <para>
		/// The two do different jobs. What is written is bounded by the arithmetic above, exactly;
		/// what is read back is bounded by this, which is a round number the section cap was
		/// derived from before any of those fields were fixed. Keeping them apart means a later
		/// revision that spends its bytes differently is refused by its own arithmetic rather than
		/// by a decoder that has no idea what it was trying to do.
		/// </para>
		/// </summary>
		public const int MaxRowBytes = 4096;

		/// <summary>
		/// The largest payload and envelope this build will write or accept.
		/// <para>
		/// 82 + 20 + 48 &times; (4 + 4096) = 196,902 bytes of payload, and 196,946 with the frame.
		/// Both numbers are the arithmetic above and nothing else, and the accepted cap is the
		/// authored cap because this family has no earlier revision that spent its bytes
		/// differently. A later revision that needs more room must raise these deliberately, and
		/// the civic-memory envelope will refuse the change until its own totals are re-derived.
		/// </para>
		/// </summary>
		public const int MaxPayloadBytes = IdentityFramingBytes + HeaderBytes
			+ KingdomVillageCovenantArchive.MaxRows * (RowFramingBytes + MaxRowBytes);

		public const int MaxEnvelopeBytes = MaxPayloadBytes + EnvelopeOverheadBytes;

		/// <summary>Encodes an archive, or explains why this build may not write it.</summary>
		public static bool TryEncode(KingdomVillageCovenantArchive archive, out byte[] bytes,
			out string failure)
		{
			bytes = null;
			failure = null;
			if (archive == null)
				return KingdomVillageCovenantRules.Fail("the covenant archive is absent",
					out failure);
			if (!KingdomVillageCovenantRules.Defined(archive.State))
				return KingdomVillageCovenantRules.Fail("the covenant archive reports state "
					+ (int)archive.State + ", which this build does not define; nothing is written "
					+ "for it", out failure);
			if (archive.State != KingdomVillageCovenantState.Compatible)
				return TryReemitOpaque(archive, out bytes, out failure);
			if (!KingdomVillageCovenantRules.TryValidate(archive, out failure)) return false;
			if (!archive.IdentityBound)
				return KingdomVillageCovenantRules.Fail("an unbound covenant archive has no realm "
					+ "to be saved under", out failure);
			return TryWrite(archive, out bytes, out failure);
		}

		/// <summary>Reads whatever it is handed, and always says explicitly what it made of it.</summary>
		public static KingdomVillageCovenantArchive Decode(byte[] bytes)
		{
			// One private copy, taken before anything is judged. Everything below reads it.
			byte[] snapshot = Ingress(bytes, MaxEnvelopeBytes, out string ingress);
			if (snapshot == null) return Quarantine(null, ingress);
			KingdomVillageCovenantFrame frame = Inspect(snapshot);
			if (frame.Kind == KingdomVillageCovenantFrameKind.Unreadable)
				return Quarantine(snapshot, frame.Fault);
			if (frame.Kind == KingdomVillageCovenantFrameKind.Future)
				return new KingdomVillageCovenantArchive
				{
					State = KingdomVillageCovenantState.FutureOpaque,
					OpaqueVersion = frame.WireVersion,
					OpaquePayload = snapshot,
					Fault = "the covenant archive was written at wire revision " + frame.WireVersion
						+ ", which this build can keep but not read"
				};
			return Read(snapshot, frame);
		}
	}
}
