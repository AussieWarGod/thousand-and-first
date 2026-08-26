using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>The one durable provenance lane permitted to justify an immutable id.</summary>
	public enum KingdomIdentityOrigin : byte
	{
		None = 0,
		FoundingTransaction = 1,
		LegacyMigration = 2,
		Quarantined = 3
	}
}
