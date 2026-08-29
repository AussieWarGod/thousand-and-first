using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>What a known family made of the bytes in its section.</summary>
	public enum KingdomCivicMemoryNested
	{
		/// <summary>The family read it and recognised every row.</summary>
		Current,

		/// <summary>The family recognised a version newer than itself and kept the payload whole.</summary>
		Future,

		/// <summary>The family refused it. Read-only from here; the bytes are kept as evidence.</summary>
		Malformed
	}

	/// <summary>Asks one family whether the bytes in its section are its own.</summary>
	public delegate KingdomCivicMemoryNested KingdomCivicMemoryFamilyReader(byte[] Payload,
		out string Fault);

	/// <summary>
	/// Which family answers for which section id.
	/// <para>
	/// The envelope stores bytes, but it must not <i>accept</i> bytes it has no reason to believe.
	/// A section carrying a known id is a claim about which codec can read it, and the only way to
	/// test that claim is to ask the codec. Doing that on the way in means a malformed known
	/// payload is caught while the good save is still on disk, rather than at some later moment
	/// when the family finally goes looking and finds nothing there.
	/// </para>
	/// <para>
	/// The readers arrive through the constructor rather than being named here, and that is not
	/// ceremony. Two of the nine section readers reach the game engine through their own rules
	/// (<c>KingdomCuriosityRules</c> and <c>KingdomCivicLeadRules</c> both take a
	/// <c>KingdomExperienceLedger</c> for their attention API, which pulls in
	/// <c>ThousandAndFirst.Simulation</c> and from there <c>XRL</c>). Naming those codecs directly
	/// from this file would drag the engine into every part of this authority that is deliberately
	/// free of it, and there would be nothing left that could be tested without a game running.
	/// </para>
	/// <para>
	/// An id with no reader registered is refused, never waved through. A table assembled wrongly
	/// must fail loudly and safely, not quietly accept whatever it is handed.
	/// </para>
	/// </summary>
	public sealed class KingdomCivicMemoryFamilyTable
	{
		private readonly Dictionary<int, KingdomCivicMemoryFamilyReader> Readers =
			new Dictionary<int, KingdomCivicMemoryFamilyReader>();

		/// <summary>
		/// Names the family that answers for one section id. An id may be claimed once: a second
		/// claim is a wiring mistake, and silently letting the later one win is how a permissive
		/// reader ends up installed over a strict one.
		/// </summary>
		public KingdomCivicMemoryFamilyTable Add(int Id, KingdomCivicMemoryFamilyReader Reader)
		{
			if (Reader == null) throw new System.ArgumentNullException("Reader");
			if (!KingdomCivicMemoryLimits.Known(Id))
				throw new System.ArgumentOutOfRangeException("Id",
					"civic memory section " + Id + " is not a known family");
			if (Readers.ContainsKey(Id))
				throw new System.InvalidOperationException(
					"civic memory section " + Id + " already has a family reader");
			Readers.Add(Id, Reader);
			return this;
		}

		/// <summary>Whether every known section id has someone to answer for it.</summary>
		public bool Complete
		{
			get
			{
				for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
					id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
					if (!Readers.ContainsKey(id)) return false;
				return true;
			}
		}

		/// <summary>
		/// Asks the family that owns <paramref name="Id"/> what its payload is.
		/// </summary>
		/// <param name="Id">A known section id. Unknown ids have nothing to ask.</param>
		/// <param name="Payload">The section's bytes.</param>
		/// <param name="Fault">The family's complaint, when it had one.</param>
		public KingdomCivicMemoryNested Inspect(int Id, byte[] Payload, out string Fault)
		{
			if (!KingdomCivicMemoryLimits.Known(Id))
				throw new System.InvalidOperationException(
					"civic memory asked unknown section id " + Id + " to identify itself");
			KingdomCivicMemoryFamilyReader reader;
			if (!Readers.TryGetValue(Id, out reader))
			{
				Fault = "no family reader is installed for civic memory section " + Id
					+ ", so nothing here can vouch for its contents";
				return KingdomCivicMemoryNested.Malformed;
			}
			if (Payload == null || Payload.Length == 0)
			{
				Fault = "civic memory section " + Id + " carries no encoded payload at all";
				return KingdomCivicMemoryNested.Malformed;
			}
			return reader(Payload, out Fault);
		}
	}
}
