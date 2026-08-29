using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// What civic memory should make of the bytes in section nine, answered by the family that
	/// owns them.
	/// <para>
	/// The other eight sections are dispositioned inside the envelope's own binding file, and for
	/// a reason that does not apply here: two of those families reach the game engine through
	/// their own rules, so a pure project cannot compile them and the only way to check their
	/// wiring is to read the source. This family reaches nothing. Putting its judgement here means
	/// the mapping from a covenant archive's three states to the envelope's three verdicts is a
	/// thing that can be run in a test rather than a thing that can only be read, and the binding
	/// file keeps one line instead of thirty.
	/// </para>
	/// <para>
	/// The order below is the order that matters. Future is asked first, because an archive from a
	/// later build is lawful and must never be swept up with damage. Readable is asked next. Only
	/// then is quarantine reached, and a state outside the three is malformed rather than falling
	/// through to whichever branch happened to be written last.
	/// </para>
	/// </summary>
	public static class KingdomVillageCovenantInspection
	{
		private const string Family = "village-covenant archive";

		public static KingdomCivicMemoryNested Inspect(byte[] Payload, out string Fault)
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantCodec.Decode(Payload);
			if (archive == null)
			{
				Fault = "the " + Family + " codec returned nothing at all";
				return KingdomCivicMemoryNested.Malformed;
			}
			if (archive.State == KingdomVillageCovenantState.FutureOpaque)
			{
				Fault = "";
				return KingdomCivicMemoryNested.Future;
			}
			if (archive.State == KingdomVillageCovenantState.Compatible)
			{
				Fault = "";
				return KingdomCivicMemoryNested.Current;
			}
			if (archive.State != KingdomVillageCovenantState.Quarantined)
			{
				Fault = "the " + Family + " returned unsupported state " + (int)archive.State;
				return KingdomCivicMemoryNested.Malformed;
			}
			Fault = "the " + Family + " was refused by its own codec ("
				+ (string.IsNullOrEmpty(archive.Fault) ? "no reason given" : archive.Fault) + ")";
			return KingdomCivicMemoryNested.Malformed;
		}

		/// <summary>
		/// The same judgement with the codec's own exceptions treated as a wire problem.
		/// <para>
		/// The decoder is written not to throw for a payload fault &mdash; it says which of the
		/// three states the bytes are in and keeps them &mdash; so this catch should never fire.
		/// It is here because the envelope hands this payload to us and must not be brought down
		/// by us, and because "should never" is a claim about today's code rather than tomorrow's.
		/// </para>
		/// </summary>
		public static KingdomCivicMemoryNested InspectGuarded(byte[] Payload, out string Fault)
		{
			try { return Inspect(Payload, out Fault); }
			catch (Exception error) when (KingdomVillageCovenantCodec.WireFault(error))
			{
				Fault = "the " + Family + " was refused by its own codec (" + error.Message + ")";
				return KingdomCivicMemoryNested.Malformed;
			}
		}
	}
}
