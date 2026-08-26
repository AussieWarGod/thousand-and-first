using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Durable procedure-job phases. Values are persisted; append only.</summary>
	internal enum KingdomLabJobPhase : byte
	{
		Funding = 0,
		FundingRecovery = 1,
		Working = 2,
		Ready = 3,
		Applying = 4,
		ApplicationRecovery = 5,
		Complete = 6,
		Cancelled = 7
	}

}
