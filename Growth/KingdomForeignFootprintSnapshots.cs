using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal enum KingdomForeignProviderStatus : byte
	{
		Absent = 0,
		Observed = 1,
		Faulted = 2
	}

	/// <summary>One normalized, owned row inside a provider-wide observation.</summary>
	internal sealed class KingdomForeignFootprintEvidence
	{
		internal string ProviderId;
		internal string ProviderVersion;
		internal string Identity;
		internal string Revision;
		internal string Refusal;
		internal string ZoneId;
		internal string SectorId = "";
		internal int OriginX;
		internal int OriginY;
		internal List<ArchitecturePoint> Cells = new List<ArchitecturePoint>();

		internal bool IsRefused => !string.IsNullOrEmpty(Refusal);
	}

	/// <summary>Typed result of exactly one registered provider call. A provider-wide fault owns no
	/// rows. An observed snapshot may retain bounded row-local faults beside healthy or exact-cell
	/// refused rows; those diagnostics never make a healthy sibling disappear.</summary>
	internal sealed class KingdomForeignProviderSnapshot
	{
		internal string ProviderId;
		internal string ProviderVersion;
		internal KingdomForeignProviderStatus Status;
		internal string Fault;
		internal List<KingdomForeignFootprintEvidence> Rows =
			new List<KingdomForeignFootprintEvidence>();
		internal List<string> RowFaults = new List<string>();
	}
}
