using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The outer envelope: magic, version, a counted run of numbered sections, and a hash over
	/// all of it.
	/// <para>
	/// The shape is deliberately the same one the frozen families already use &mdash; a
	/// 44-byte frame of magic, version, a count and a SHA-256 tail &mdash; because a reader who
	/// has learned to distrust one of those payloads should not have to learn a second idiom to
	/// distrust this one. What the envelope adds is only what a container has to add: stable ids
	/// so a payload is found by number rather than by position, a fixed order so the same state
	/// always writes the same bytes, and a refusal to tolerate anything after the hash.
	/// </para>
	/// <para>
	/// It does not open the sections. See <see cref="KingdomCivicMemorySection"/>.
	/// </para>
	/// </summary>
	public static partial class KingdomCivicMemoryCodec
	{
		private const int Magic = 0x4D434654;

		/// <summary>The only envelope version this build writes.</summary>
		public const int CurrentWireVersion = 1;

		private const int HashBytes = 32;

		/// <summary>
		/// Writes a state to bytes, refusing rather than truncating anything oversized.
		/// </summary>
		/// <param name="State">The state to write. A quarantined state is never writable.</param>
		public static byte[] Encode(KingdomCivicMemoryState State)
		{
			if (State == null) throw new InvalidDataException("civic memory state is absent");
			if (State.Quarantined) throw new InvalidDataException(
				"a quarantined civic memory state is evidence, not something to write back");
			// A save from a later build goes back exactly as it arrived. Re-encoding it would
			// mean re-deciding a layout this build has never seen, which is the one thing a
			// forward-compatible container must never do.
			if (State.IsFutureOuter)
			{
				byte[] retained = State.RetainedPayload();
				KingdomCivicMemoryState verified = Decode(retained, State.Revision);
				if (!verified.IsFutureOuter || verified.OuterVersion != State.OuterVersion)
					throw new InvalidDataException(
						"future civic memory does not match its retained integrity-checked envelope");
				return retained;
			}
			List<KingdomCivicMemorySection> sections = State.Sections();
			Validate(sections);
			using (MemoryStream body = new MemoryStream())
			using (BinaryWriter writer = Writer(body))
			{
				writer.Write(Magic);
				writer.Write(CurrentWireVersion);
				writer.Write(sections.Count);
				for (int i = 0; i < sections.Count; i++)
				{
					KingdomCivicMemorySection section = sections[i];
					writer.Write(section.Id);
					writer.Write(section.Length);
					writer.Write(section.Payload());
				}
				writer.Flush();
				byte[] framed = body.ToArray();
				byte[] envelope = new byte[framed.Length + HashBytes];
				System.Buffer.BlockCopy(framed, 0, envelope, 0, framed.Length);
				System.Buffer.BlockCopy(Hash(framed), 0, envelope, framed.Length, HashBytes);
				if (envelope.Length > KingdomCivicMemoryLimits.MaxEnvelopeBytes)
					throw new InvalidDataException("civic memory envelope exceeds its cap");
				return envelope;
			}
		}

		/// <summary>
		/// Every bound an envelope has to satisfy before it is worth writing, checked in the same
		/// order the reader checks them so that a payload this build produces is one it accepts.
		/// </summary>
		private static void Validate(List<KingdomCivicMemorySection> Sections)
		{
			if (Sections.Count > KingdomCivicMemoryLimits.MaxSections)
				throw new InvalidDataException("civic memory carries more sections than the "
					+ "envelope reserves room for");
			long cumulative = 0L;
			int previous = int.MinValue;
			for (int i = 0; i < Sections.Count; i++)
			{
				KingdomCivicMemorySection section = Sections[i];
				if (!KingdomCivicMemoryLimits.Allocatable(section.Id))
					throw new InvalidDataException("civic memory section id " + section.Id
						+ " is below the first id that could ever be allocated ("
						+ KingdomCivicMemoryLimits.MinSectionId + ")");
				// Strictly ascending does duplicate rejection and order determinism at once: a
				// repeated id cannot be greater than itself.
				if (section.Id <= previous) throw new InvalidDataException(
					"civic memory section ids must be unique and ascending; " + section.Id
					+ " follows " + previous);
				previous = section.Id;
				if (section.Length > KingdomCivicMemoryLimits.SectionCap(section.Id))
					throw new InvalidDataException("civic memory section " + section.Id
						+ " exceeds its frozen cap of "
						+ KingdomCivicMemoryLimits.SectionCap(section.Id) + " bytes");
				cumulative += section.Length;
			}
			if (cumulative > KingdomCivicMemoryLimits.MaxCumulativePayloadBytes)
				throw new InvalidDataException("civic memory sections total " + cumulative
					+ " bytes, over the cumulative cap of "
					+ KingdomCivicMemoryLimits.MaxCumulativePayloadBytes);
		}

		private static BinaryWriter Writer(MemoryStream Stream)
		{
			return new BinaryWriter(Stream, new UTF8Encoding(false, true), true);
		}

		private static BinaryReader Reader(MemoryStream Stream)
		{
			return new BinaryReader(Stream, new UTF8Encoding(false, true), true);
		}

		private static byte[] Hash(byte[] Framed)
		{
			using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(Framed);
		}

		private static bool SameHash(byte[] Left, byte[] Right)
		{
			if (Left.Length != Right.Length) return false;
			int difference = 0;
			for (int i = 0; i < Left.Length; i++) difference |= Left[i] ^ Right[i];
			return difference == 0;
		}
	}
}
