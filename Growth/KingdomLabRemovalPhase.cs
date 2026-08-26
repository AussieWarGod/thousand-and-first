using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Patient-owned removal phases. Values are persisted; append only.</summary>
	internal enum KingdomLabRemovalPhase : byte
	{
		Funding = 0,
		FundingRecovery = 1,
		Paid = 2,
		Removing = 3,
		RemovalRecovery = 4,
		Removed = 5,
		Complete = 6,
		Quarantined = 7,
		Cancelled = 8
	}

}
