#if !TAF_TESTS
using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCivicMemorySystem
	{
		private const int BlockMagic = 0x4D434654;
		private const int CurrentBlockVersion = 1;

		[NonSerialized]
		private bool CustomReadCompleted;

		/// <summary>
		/// Off, so the engine reflects nothing into this system and the block below is the only
		/// thing on disk.
		/// <para>
		/// <c>IGameSystem</c> declares this virtual and true (<c>XRL/IGameSystem.cs:40</c>).
		/// Overriding it to false changes the type code the writer emits, which is what makes the
		/// difference real: <c>SerializationWriter.SerializeComposite</c> writes
		/// <c>ICompositeType</c> instead of <c>ICompositeFieldType</c> and skips
		/// <c>WriteTypeFields</c> entirely (<c>XRL/World/SerializationWriter.cs:2756-2770</c>),
		/// and <c>SerializationReader.DeserializeComposite</c> reads that code and skips
		/// <c>ReadTypeFields</c> to match (<c>XRL/World/SerializationReader.cs:1329-1333</c>).
		/// Every byte in the block is therefore written by <see cref="Write"/> and read by
		/// <see cref="Read"/>, which is the only way a bounded, versioned, integrity-checked payload
		/// can be promised at all &mdash; a reflected field list would change shape with the
		/// class and take the save's meaning with it. That is not hypothetical here; it is
		/// exactly how <c>KingdomSystem</c>'s v1 layout became unreadable.
		/// </para>
		/// </summary>
		public override bool WantFieldReflection => false;

		/// <summary>
		/// Writes the block: a marker, a version, and the length-prefixed envelope.
		/// <para>
		/// By the time this runs the engine has already committed to putting this instance on
		/// disk, so there is nothing to decide here and no way to refuse. The refusal lives one
		/// step earlier in <see cref="BeforeSave"/>.
		/// </para>
		/// </summary>
		public override void Write(SerializationWriter Writer)
		{
			byte[] envelope = Records.Encode();
			Writer.Write(BlockMagic);
			Writer.Write(CurrentBlockVersion);
			Writer.Write(envelope.Length);
			Writer.WriteBytesDirect(envelope);
		}

		/// <summary>
		/// Reads the block, and refuses to pretend when it cannot.
		/// <para>
		/// The engine does not stop loading when this throws. <c>DeserializeComposite</c> runs
		/// this method inside a try and hands the exception to <c>SkipBlock</c>, which counts an
		/// error, logs it, winds the stream forward to the end of the block and returns this
		/// same half-built instance to <c>LoadSystems</c> anyway
		/// (<c>XRL/World/SerializationReader.cs:1320-1340</c>, <c>:2186-2193</c>;
		/// <c>XRL/XRLGame.cs:1592-1603</c>). A mod that merely threw here would find itself alive,
		/// blank, and about to save that blankness over the founder's records.
		/// </para>
		/// <para>
		/// So the two failures are handled differently on purpose. If the block's own framing is
		/// intact but the envelope inside it is not, the stream is already where it should be:
		/// the payload is quarantined whole, the latch is thrown, and this returns normally so
		/// the evidence survives in a live instance. If the framing itself is wrong there is no
		/// safe position to continue from, so it latches and quarantines first and <i>then</i>
		/// throws, letting <c>SkipBlock</c> do the one thing it is good for. Either way the latch
		/// stands and <see cref="BeforeSave"/> will refuse the next save.
		/// </para>
		/// <para>
		/// A save written before this system existed carries no block at all, is never read here,
		/// and is added fresh and empty by <c>RequireSystem</c> after the load finishes
		/// (<c>XRL/XRLGame.cs:321-332</c>, <c>:1954</c>). That is the only lawful empty.
		/// </para>
		/// </summary>
		public override void Read(SerializationReader Reader)
		{
			CustomReadCompleted = false;
			List<byte> recovered = new List<byte>();
			int length;
			try
			{
				int magic = Word(Reader, recovered);
				if (magic != BlockMagic)
					throw new System.IO.InvalidDataException("civic memory block marker reads 0x"
						+ magic.ToString("X8") + ", not 0x" + BlockMagic.ToString("X8"));
				int version = Word(Reader, recovered);
				if (version < 1 || version > CurrentBlockVersion)
					throw new System.IO.InvalidDataException("civic memory block version "
						+ version + " is outside the range 1 through " + CurrentBlockVersion
						+ " this build can read");
				length = Word(Reader, recovered);
				if (length < 0 || length > KingdomCivicMemoryLimits.MaxEnvelopeBytes)
					throw new System.IO.InvalidDataException("civic memory block declares "
						+ length + " bytes, outside its cap of "
						+ KingdomCivicMemoryLimits.MaxEnvelopeBytes);
			}
			catch (Exception e)
			{
				// The latch keeps its first cause, so the true one has to be set here rather than
				// left to a decode of something this method invented. What was actually recovered
				// off the stream is quarantined as-is; nothing is substituted for it.
				Records.AdoptUnreadableFraming(recovered.ToArray(),
					"the civic memory block's own framing could not be read (" + e.Message + ")");
				throw;
			}
			// Framing is sound, but BinaryReader.ReadBytes may legally return fewer bytes at EOF.
			// A short or throwing payload read must latch before Qud's block skipper returns this
			// same instance; otherwise it would be an empty authority eligible for overwrite.
			byte[] payload = null;
			try
			{
				payload = Reader.ReadBytesDirect(length);
				if (payload == null || payload.Length != length)
					throw new System.IO.EndOfStreamException("civic memory block returned "
						+ (payload == null ? 0 : payload.Length) + " of " + length + " bytes");
				Records.AdoptSaved(payload);
				CustomReadCompleted = true;
			}
			catch (Exception e)
			{
				if (!Records.Latch.Tripped)
				{
					if (payload != null) recovered.AddRange(payload);
					Records.AdoptUnreadableFraming(recovered.ToArray(),
						"the civic memory block payload could not be read (" + e.Message + ")");
				}
				throw;
			}
		}

		/// <summary>
		/// Reads one 32-bit word and keeps the bytes it was made of.
		/// <para>
		/// <c>SerializationWriter</c> extends <c>BinaryWriter</c> and does not override
		/// <c>Write(int)</c> (<c>XRL/World/SerializationWriter.cs:36</c>; its optimised integer
		/// form is the separate opt-in <c>WriteOptimized</c> at <c>:1488</c>), and
		/// <c>SerializationReader</c> extends <c>BinaryReader</c> without overriding
		/// <c>ReadInt32</c> (<c>XRL/World/SerializationReader.cs:25</c>). The pair is therefore a
		/// plain little-endian four-byte word, and these four bytes are the real ones from the
		/// save rather than a reconstruction that merely looks like them.
		/// </para>
		/// </summary>
		private static int Word(SerializationReader Reader, List<byte> Recovered)
		{
			int value = Reader.ReadInt32();
			Recovered.Add((byte)value);
			Recovered.Add((byte)(value >> 8));
			Recovered.Add((byte)(value >> 16));
			Recovered.Add((byte)(value >> 24));
			return value;
		}

		/// <summary>
		/// The last gate before a buffered save reaches the primary file, and the only one this
		/// class has.
		/// <para>
		/// <c>XRLGame.SaveSystems</c> calls <c>system?.BeforeSave()</c> immediately before
		/// <c>Writer.Write(system)</c> in the same loop (<c>XRL/XRLGame.cs:1580-1590</c>), and
		/// that loop runs at <c>:2305</c> &mdash; long before <c>FinalizeWrite</c> at <c>:2335</c>,
		/// before the existing save is copied to <c>.bak</c> at <c>:2345</c>, and before
		/// <c>WriteAllBytes</c> replaces the primary file at <c>:2356</c>. An exception here
		/// unwinds past all three to the outer handler at <c>:2383-2387</c>, whose
		/// <c>SaveGameError</c> call takes the default <c>RestoreBackup = false</c> and therefore
		/// only logs and reports (<c>:2459-2474</c>). Throwing is not a side effect of this
		/// override; it is the entire mechanism, and the engine offers a mod no other way to veto
		/// a save.
		/// </para>
		/// <para>
			/// This override only reads. It never sets, clears, or repairs the latch, and no such method
			/// exists &mdash; see <see cref="KingdomCivicMemoryLatch"/> for the C17
		/// failure this shape is built against, where a veto was retired by the code that
		/// displayed its warning.
		/// </para>
		/// </summary>
		public override void BeforeSave()
		{
			string derivation;
			if (!KingdomCivicMemoryDerivation.Verify(out derivation))
			{
				throw new InvalidOperationException(
					"ThousandAndFirst: refusing to save. Civic memory's section limits no longer " +
					"match the wire families they were derived from (" + derivation + "), so it " +
					"can no longer promise the records it holds will fit. Quit without saving " +
						"and report this; the save on disk is untouched.");
			}
			if (!Records.FamiliesComplete)
			{
				throw new InvalidOperationException(
					"ThousandAndFirst: refusing to save. Civic memory has no reader for at least " +
					"one known section, so this build cannot prove its own records. Quit without " +
					"saving and report this; the save on disk is untouched.");
			}
			if (Records.Latch.Tripped)
			{
				throw new InvalidOperationException(
					"ThousandAndFirst: refusing to save. This save's civic records could not be " +
					"read (" + Records.Latch.Reason + "), and saving now would write that " +
					"failure over the good save you already have. Quit without saving; the save " +
					"on disk is untouched. This session cannot safely be saved after the failed " +
					"load, even after its warning has been dismissed.");
			}
		}
	}
}
#endif
