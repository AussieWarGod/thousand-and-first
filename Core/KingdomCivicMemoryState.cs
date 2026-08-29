using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>What a reading of civic memory turned out to be.</summary>
	public enum KingdomCivicMemoryDisposition
	{
		/// <summary>No block was in the save. The only lawful nothing.</summary>
		Empty,

		/// <summary>Sections this build read, and may still be able to change.</summary>
		Held,

		/// <summary>A payload that was found and refused. Kept as evidence; read-only.</summary>
		Quarantined,

		/// <summary>
		/// An envelope from a later build. Lawful, whole, read-only, and not a fault.
		/// </summary>
		FutureOuter
	}

	/// <summary>
	/// One immutable reading of civic memory.
	/// <para>
	/// Four dispositions, told apart on purpose. <b>Empty</b> means nobody has written civic
	/// memory into this save yet. <b>Held</b> means there are sections. <b>Quarantined</b> means a
	/// payload was found and refused, and the refused bytes are still here. <b>FutureOuter</b>
	/// means the envelope came from a build newer than this one: it is not damaged, not empty and
	/// not this build's business, so it is carried unchanged and never rewritten.
	/// </para>
	/// <para>
	/// A save that has lost its records, a save that never had any, and a save written by next
	/// month's build look identical from a distance. Telling the founder the wrong one of those
	/// three is how a mod eats somebody's city and calls it a fresh start.
	/// </para>
	/// </summary>
	public sealed class KingdomCivicMemoryState
	{
		/// <summary>Bumped by every accepted commit; the value a later commit must still agree with.</summary>
		public readonly long Revision;

		/// <summary>Which of the four this reading is.</summary>
		public readonly KingdomCivicMemoryDisposition Disposition;

		/// <summary>Why it was refused. Empty unless <see cref="Quarantined"/>.</summary>
		public readonly string Fault;

		/// <summary>The envelope version, when this state came from a newer build.</summary>
		public readonly int OuterVersion;

		private readonly List<KingdomCivicMemorySection> Held;
		private readonly byte[] Retained;

		private KingdomCivicMemoryState(long Revision, List<KingdomCivicMemorySection> Sections,
			KingdomCivicMemoryDisposition Disposition, string Fault, byte[] Retained,
			int OuterVersion)
		{
			// A negative revision is a wrapped counter, not a small one. Clamping it to zero would
			// quietly hand the next commit a revision it could match by accident.
			if (Revision < 0L) throw new ArgumentOutOfRangeException("Revision",
				"civic memory revision " + Revision + " is negative; the counter has overflowed");
			this.Revision = Revision;
			this.Disposition = Disposition;
			this.Fault = Fault ?? "";
			this.Retained = Retained == null ? null : (byte[])Retained.Clone();
			this.OuterVersion = OuterVersion;
			Held = Sections == null ? new List<KingdomCivicMemorySection>()
				: new List<KingdomCivicMemorySection>(Sections);
		}

		/// <summary>The one lawful nothing: a save written before this authority existed.</summary>
		public static KingdomCivicMemoryState Empty()
		{
			return new KingdomCivicMemoryState(0L, null,
				KingdomCivicMemoryDisposition.Empty, "", null, 0);
		}

		/// <summary>
		/// A reading of real sections, sorted by id here rather than trusted to arrive sorted, so
		/// the bytes this authority writes never depend on a caller's list order.
		/// </summary>
		public static KingdomCivicMemoryState Of(IList<KingdomCivicMemorySection> Sections,
			long Revision)
		{
			if (Sections == null) throw new ArgumentNullException("Sections",
				"use Empty() when civic memory intentionally has no sections");
			int count = Sections.Count;
			if (count < 0 || count > KingdomCivicMemoryLimits.MaxSections)
				throw new ArgumentOutOfRangeException("Sections", "civic memory section count "
					+ count + " is outside 0 through " + KingdomCivicMemoryLimits.MaxSections);
			List<KingdomCivicMemorySection> ordered =
				new List<KingdomCivicMemorySection>(count);
			for (int i = 0; i < count; i++)
			{
				KingdomCivicMemorySection section = Sections[i];
				if (section == null) throw new ArgumentException(
					"civic memory was given an absent section at position " + i, "Sections");
				ordered.Add(section);
			}
			ordered.Sort((a, b) => a.Id.CompareTo(b.Id));
			return new KingdomCivicMemoryState(Revision, ordered,
				ordered.Count == 0 ? KingdomCivicMemoryDisposition.Empty
					: KingdomCivicMemoryDisposition.Held, "", null, 0);
		}

		/// <summary>
		/// A refusal that keeps its evidence. The bytes are held so a later build &mdash; one that
		/// understands what went wrong, or simply a fixed one &mdash; still has something to read.
		/// </summary>
		public static KingdomCivicMemoryState Quarantine(byte[] Bytes, string Fault, long Revision)
		{
			byte[] kept = Bytes == null ? new byte[0] : (byte[])Bytes.Clone();
			return new KingdomCivicMemoryState(Revision, null,
				KingdomCivicMemoryDisposition.Quarantined,
				string.IsNullOrEmpty(Fault) ? "civic memory payload was refused" : Fault, kept, 0);
		}

		/// <summary>
		/// An envelope from a later build, carried exactly. Not a fault, and never described as
		/// one: the bytes go back to disk the way they arrived.
		/// </summary>
		internal static KingdomCivicMemoryState FutureOuter(byte[] Bytes, int Version, long Revision)
		{
			if (Bytes == null) throw new ArgumentNullException("Bytes");
			if (Version <= KingdomCivicMemoryCodec.CurrentWireVersion)
				throw new ArgumentOutOfRangeException("Version",
					"a future civic-memory state must name a later envelope version");
			return new KingdomCivicMemoryState(Revision, null,
				KingdomCivicMemoryDisposition.FutureOuter, "", (byte[])Bytes.Clone(), Version);
		}

		/// <summary>Nothing here, and nothing lost. False for quarantine and for a future save.</summary>
		public bool IsEmpty => Disposition == KingdomCivicMemoryDisposition.Empty;

		/// <summary>The payload behind this state was found and refused.</summary>
		public bool Quarantined => Disposition == KingdomCivicMemoryDisposition.Quarantined;

		/// <summary>This save was written by a build newer than ours.</summary>
		public bool IsFutureOuter => Disposition == KingdomCivicMemoryDisposition.FutureOuter;

		/// <summary>Whether this state may still be changed by a commit.</summary>
		public bool ReadOnly => Quarantined || IsFutureOuter;

		/// <summary>How many sections this state holds, known and unknown together.</summary>
		public int Count => Held.Count;

		/// <summary>Whether this state carries a section id this build cannot interpret.</summary>
		public bool HasFutureSections
		{
			get
			{
				for (int i = 0; i < Held.Count; i++)
					if (!Held[i].KnownToThisBuild) return true;
				return false;
			}
		}

		/// <summary>
		/// A caller's own list, in id order. The sections inside are safe to share because a
		/// section only ever hands out copies of its bytes; the list itself is fresh so a caller
		/// who adds or removes entries is editing their reading, not this authority.
		/// </summary>
		public List<KingdomCivicMemorySection> Sections()
		{
			return new List<KingdomCivicMemorySection>(Held);
		}

		/// <summary>The section with this id, or null. Never throws on an id nobody wrote.</summary>
		public KingdomCivicMemorySection Section(int Id)
		{
			for (int i = 0; i < Held.Count; i++)
				if (Held[i].Id == Id) return Held[i];
			return null;
		}

		/// <summary>
		/// The bytes kept whole behind a quarantine or a future save, copied. Empty otherwise.
		/// </summary>
		public byte[] RetainedPayload()
		{
			return Retained == null ? new byte[0] : (byte[])Retained.Clone();
		}

		/// <summary>The same content at a new revision, after an accepted commit.</summary>
		internal KingdomCivicMemoryState AtRevision(long Next)
		{
			return new KingdomCivicMemoryState(Next, Held, Disposition, Fault, Retained,
				OuterVersion);
		}

		/// <summary>
		/// A reading of this state that shares nothing with it. Both the section list and every
		/// payload behind it are rebuilt, so a caller cannot reach back through what they were
		/// given and edit the authority that gave it to them.
		/// </summary>
		public KingdomCivicMemoryState Copy()
		{
			List<KingdomCivicMemorySection> copies = new List<KingdomCivicMemorySection>();
			for (int i = 0; i < Held.Count; i++)
				copies.Add(new KingdomCivicMemorySection(Held[i].Id, Held[i].Payload()));
			byte[] retained = Retained == null ? null : (byte[])Retained.Clone();
			return new KingdomCivicMemoryState(Revision, copies, Disposition, Fault, retained,
				OuterVersion);
		}
	}
}
