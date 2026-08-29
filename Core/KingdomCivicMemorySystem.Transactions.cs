#if !TAF_TESTS
namespace ThousandAndFirst
{
	public sealed partial class KingdomCivicMemorySystem
	{
		/// <summary>Opens one current known section under an origin- and revision-bound lease.</summary>
		public bool TryReadSection(int SectionId, out KingdomCivicMemorySectionLease Lease,
			out string Failure)
		{
			return Records.TryReadSection(SectionId, out Lease, out Failure);
		}

		/// <summary>Commits one encoded family payload under the lease that read its predecessor.</summary>
		public bool TryCommitSection(KingdomCivicMemorySectionLease Lease, byte[] Payload,
			out string Failure)
		{
			return Records.TryCommitSection(Lease, Payload, out Failure);
		}
	}
}
#endif
