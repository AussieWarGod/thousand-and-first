using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free civic-memory transaction surface shared by the save system and pure authority.
	/// Reads return copies or origin-bound leases; callers never receive the mutable store itself.
	/// </summary>
	public interface IKingdomCivicMemoryAuthority
	{
		long Revision { get; }
		bool ReadOnly { get; }
		string ReadOnlyReason { get; }
		KingdomCivicMemoryState Read();
		bool TryCommit(IList<KingdomCivicMemorySection> Candidate,
			long ExpectedRevision, out string Failure);
		bool TryReadSection(int SectionId, out KingdomCivicMemorySectionLease Lease,
			out string Failure);
		bool TryCommitSection(KingdomCivicMemorySectionLease Lease, byte[] Payload,
			out string Failure);
	}
}
