using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Exact caller contract for one attempt or resumption.</summary>
	public enum KingdomFoundingOutcome : byte
	{
		Refused = 0,
		CompensatedFailure = 1,
		RecoverableFailure = 2,
		Committed = 3
	}

	/// <summary>What happened to the basin's measured water.</summary>
	public enum KingdomFoundingWaterDisposition : byte
	{
		Untouched = 0,
		RestoredExactly = 1,
		HeldForRecovery = 2,
		Spent = 3,
		RestorationFailed = 4
	}

	/// <summary>Ordered live projections. Tests inject a failure after every boundary.</summary>
	public enum KingdomFoundingProjection : byte
	{
		None = 0,
		Water = 1,
		Identity = 2,
		Claim = 3,
		Seat = 4,
		Ability = 5,
		Placement = 6,
		Seal = 7
	}

	/// <summary>Durable decision made by a chronicle outbox about its optional journal row.
	/// Required is written before the external Journal callback; Inserted and Skipped are terminal
	/// observations and therefore do not change when the live option changes later.</summary>
	public enum KingdomChronicleDisposition : byte
	{
		None = 0,
		Required = 1,
		Inserted = 2,
		Skipped = 3
	}

}
