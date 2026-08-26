using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Why a realm/settlement identity set was refused.</summary>
	public enum KingdomIdentityFault : byte
	{
		None = 0,
		InvalidTransaction = 1,
		InvalidRealm = 2,
		InvalidEvidence = 3,
		NullSet = 4,
		TooManySettlements = 5,
		InvalidSettlement = 6,
		DuplicateSettlement = 7,
		CryptographicFailure = 8,
		InvalidOrigin = 9,
		InvalidVersion = 10,
		IdentityMismatch = 11,
		EmptySettlementSet = 12,
		RaggedSettlementNames = 13,
		AmbiguousSettlementName = 14
	}
}
