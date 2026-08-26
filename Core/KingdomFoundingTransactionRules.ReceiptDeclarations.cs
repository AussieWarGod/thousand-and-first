using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>The three promises the founder's basin can publish.</summary>
	public enum KingdomFoundingKind : byte
	{
		None = 0,
		FirstCity = 1,
		SecondCity = 2,
		VillageCharter = 3
	}

	/// <summary>
	/// Durable phase stored on the exact basin that paid for a rite. A non-empty phase is an
	/// interrupted rite and is resumed, never mistaken for a new pour.
	/// </summary>
	public enum KingdomFoundingPhase : byte
	{
		None = 0,
		WaterCommitted = 1,
		PublicationCommitted = 2,
		RecoveryRequired = 3,
		Complete = 4
	}

	/// <summary>Safe disposition for a decoded kind/phase pair at an entry point.</summary>
	public enum KingdomFoundingReceiptNormalization : byte
	{
		Clean = 0,
		Pending = 1,
		ClearStaged = 2,
		Quarantine = 3
	}

	/// <summary>Provenance of authority to publish a founding.</summary>
	public enum KingdomFoundingOwnerKind : byte
	{
		None = 0,
		Basin = 1,
		Direct = 2
	}

}
