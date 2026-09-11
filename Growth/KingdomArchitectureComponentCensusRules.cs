using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Whether the components standing at one authored slot are exactly what this settlement
	/// expects, generation by generation.
	///
	/// <para>THE PROBLEM. The lot survives an authored upgrade and a slot name is local to the
	/// layout it came from, so during the retag pass two generations legitimately stand under one
	/// lot at the same slot string, on different world cells. Counting by lot and slot alone
	/// counted two and quarantined a settlement behaving exactly as designed.</para>
	///
	/// <para>THE RULE. Every candidate at that lot and slot is classified and nothing is skipped:
	/// one carrying this generation's token is THIS; one that IS the mapped peer -- same recorded
	/// identity, same world cell, same token, and still allowed to stand -- is OTHER; anything
	/// else is FOREIGN and fails the census outright. A census that merely declined to count a
	/// stranger would be the weakening this rule exists to prevent.</para>
	///
	/// <para>WHY A MAPPED PEER AND NOT A TOKEN. A retagged component overwrites its own slot,
	/// hash and token with the successor's, so its predecessor token no longer exists anywhere.
	/// The component sharing this slot NAME is a different object from a different retained pair,
	/// with its own cell and identity. Only the frozen retained pairing names it, so the caller
	/// resolves it per census call -- its retain state changes during the pass -- and this shard
	/// decides membership from the values it is handed.</para>
	/// </summary>
	public static class KingdomArchitectureComponentCensusRules
	{
		/// <summary>A candidate of the generation being settled.</summary>
		public const int This = 1;

		/// <summary>The one mapped peer of the other generation.</summary>
		public const int Other = 2;

		/// <summary>Neither, and therefore a refusal.</summary>
		public const int Foreign = 3;

		/// <summary>Whether a candidate stands at the lot and slot this census is about.</summary>
		public static bool AtSlot(string Lot, string Slot, string CandidateLot,
			string CandidateSlot)
		{
			return !string.IsNullOrEmpty(Lot) && !string.IsNullOrEmpty(Slot)
				&& CandidateLot == Lot && CandidateSlot == Slot;
		}

		/// <summary>
		/// Classifies one candidate already known to stand at this lot and slot.
		/// <para>The peer is proved by all three of identity, cell and token together. A peer
		/// token on the wrong cell, or on an object with another identity, is FOREIGN: those are
		/// exactly the shapes a stranger would present.</para>
		/// </summary>
		/// <param name="Token">This generation's component token for the slot.</param>
		/// <param name="CandidateToken">The candidate's own component token property.</param>
		/// <param name="CandidateId">The candidate's own engine identity.</param>
		/// <param name="CandidateAtPeerCell">Whether the candidate stands on the peer's own world
		/// cell, as the caller compared it by reference.</param>
		/// <param name="PeerId">The peer's recorded output identity, or null when none is
		/// permitted here.</param>
		/// <param name="PeerToken">The peer's component token, or null.</param>
		/// <param name="PeerAllowed">Whether the peer's retain state still lets it stand.</param>
		public static int Classify(string Token, string CandidateToken, string CandidateId,
			bool CandidateAtPeerCell, string PeerId, string PeerToken, bool PeerAllowed)
		{
			if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(CandidateToken))
			{
				return Foreign;
			}
			if (CandidateToken == Token)
			{
				return This;
			}
			if (PeerAllowed && !string.IsNullOrEmpty(PeerId) && !string.IsNullOrEmpty(PeerToken)
				&& !string.IsNullOrEmpty(CandidateId) && CandidateAtPeerCell
				&& CandidateId == PeerId && CandidateToken == PeerToken)
			{
				return Other;
			}
			return Foreign;
		}

		/// <summary>
		/// Whether a completed census settles: no stranger, exactly one component of this
		/// generation, and at most one peer.
		/// <para>At most one, never exactly one: a peer that has already been retagged now reads
		/// its own successor slot and has left this census, so demanding its presence would refuse
		/// every slot settled after it. Two proved peers are a duplicate and refuse.</para>
		/// </summary>
		public static bool Settled(int ThisCount, int OtherCount, int ForeignCount)
		{
			return ForeignCount == 0 && ThisCount == 1 && OtherCount >= 0 && OtherCount <= 1;
		}

		/// <summary>
		/// Whether the peer's retain state still permits it to stand at this slot.
		/// <para>Retain state is 0 unpublished, 1 successor identity published with the retag
		/// pending or in flight, 2 settled on the successor. An After-generation census may meet a
		/// peer that has not settled yet (state under 2); a Before-generation census may meet one
		/// whose successor identity is already published (state above 0).</para>
		/// </summary>
		public static bool PeerAllowed(int RetainState, bool AfterCensus)
		{
			if (RetainState < 0 || RetainState > 2)
			{
				return false;
			}
			return AfterCensus ? RetainState < 2 : RetainState > 0;
		}

		/// <summary>
		/// The frozen retained pair whose component may share this slot name, from the delta's own
		/// pairing. An After-generation census looks for the predecessor placement carrying the
		/// slot; a Before-generation census looks for the successor placement carrying it.
		/// </summary>
		/// <param name="RetainKey">The predecessor placement of that pair, which is what the
		/// retain state and the recorded output identity are keyed by.</param>
		public static bool TryPeerPlacement(ArchitectureLayoutDelta Delta, string Slot,
			bool AfterCensus, out ArchitecturePlacement Peer, out ArchitecturePlacement RetainKey)
		{
			Peer = null;
			RetainKey = null;
			if (Delta == null || string.IsNullOrEmpty(Slot)) return false;
			List<ArchitecturePlacement> before = Delta.Retained;
			List<ArchitecturePlacement> after = Delta.RetainedAfter;
			if (before == null || after == null || before.Count != after.Count) return false;
			for (int i = 0; i < before.Count; i++)
			{
				ArchitecturePlacement candidate = AfterCensus ? before[i] : after[i];
				if (candidate == null || candidate.Slot != Slot) continue;
				if (Peer != null)
				{
					// The delta keys retained pairs by slot, so two pairs claiming one slot name
					// is a malformed delta, not a choice to make.
					Peer = null;
					RetainKey = null;
					return false;
				}
				Peer = candidate;
				RetainKey = before[i];
			}
			return Peer != null;
		}
	}
}
