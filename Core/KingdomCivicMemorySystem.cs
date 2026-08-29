#if !TAF_TESTS
using System;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>
	/// The durable owner of civic memory: the save-scoped home for eight wire families that had
	/// bounded codecs and nowhere to live.
	/// <para>
	/// Civic artifacts (O5/D6), civic practice (D1/D12), body history (D5), curiosity and civic
	/// leads (O6/D7), treaty, communal rites (D8), and guest-feast loops (O11) each froze with a
	/// codec that knows exactly how to write itself
	/// and exactly how large it can lawfully get, and each has been sitting unwritten because no
	/// game system was carrying it. This system carries them. That is the whole of its remit:
	/// it does not produce a record, grant a reward, move an actor, or give a treaty an effect,
	/// and a reader looking here for any of those is in the wrong file.
	/// </para>
	/// <para>
	/// It holds sections as bytes and never opens them &mdash; see
	/// <see cref="KingdomCivicMemorySection"/> for why a substrate that second-guessed its
	/// tenants would be worse than one that did not. All state lives on
	/// <see cref="KingdomCivicMemoryAuthority"/>, which is where copy-on-read, revision checking
	/// and quarantine actually happen; this class is the engine's handle on it.
	/// </para>
	/// </summary>
	[Serializable]
	public sealed partial class KingdomCivicMemorySystem : IGameSystem,
		IKingdomCivicMemoryAuthority
	{
		/// <summary>
		/// The records themselves. Not serialized by reflection and not serializable by it:
		/// <see cref="WantFieldReflection"/> is false, so the engine writes nothing here that
		/// this class did not write by hand.
		/// </summary>
		private readonly KingdomCivicMemoryAuthority Records =
			new KingdomCivicMemoryAuthority(KingdomCivicMemoryFamilyBindings.Table());

		/// <summary>The revision a caller must name for its commit to be accepted.</summary>
		public long Revision => Records.Revision;

		/// <summary>
		/// Whether civic memory has stopped accepting changes for this session, either because
		/// the save could not be read or because it is holding records it could not parse.
		/// </summary>
		public bool ReadOnly => Records.ReadOnly;

		/// <summary>Whether the records on disk were refused rather than read.</summary>
		public bool Quarantined => Records.Quarantined;

		/// <summary>
		/// Whether this save's civic memory was written by a build newer than ours. Lawful, and
		/// distinct from both empty and quarantined: it is carried unchanged, not repaired.
		/// </summary>
		public bool IsFutureOuter => Records.IsFutureOuter;

		/// <summary>
		/// Nothing held and nothing lost. True only for a save that never carried a civic-memory
		/// block; a failed read is <see cref="Quarantined"/>, which is a different answer.
		/// </summary>
		public bool IsEmpty => Records.IsEmpty;

		/// <summary>
		/// Why this session's civic memory is read-only, in the founder's words. Empty while it
		/// is not. Reading this can never retire the condition it describes &mdash; see
		/// <see cref="KingdomCivicMemoryLatch"/>.
		/// </summary>
		public string ReadOnlyReason => Records.ReadOnlyReason;

		/// <summary>A copy of the held records. The caller owns everything it is handed.</summary>
		public KingdomCivicMemoryState Read()
		{
			return Records.Read();
		}

		/// <summary>
		/// Offers a replacement set of sections, accepted only if the caller is still up to date
		/// and the result would survive a save. A refusal changes nothing.
		/// </summary>
		/// <param name="Candidate">The sections to hold.</param>
		/// <param name="ExpectedRevision">The revision the caller last read.</param>
		/// <param name="Failure">Why the commit was refused, or an empty string.</param>
		public bool TryCommit(System.Collections.Generic.IList<KingdomCivicMemorySection> Candidate,
			long ExpectedRevision, out string Failure)
		{
			return Records.TryCommit(Candidate, ExpectedRevision, out Failure);
		}
	}
}
#endif
