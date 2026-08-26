using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One receipt-bound food stack. A debit may stop at any count between before and
	/// after; retry resumes from that observed count instead of spending the quoted amount twice.</summary>
	internal readonly struct KingdomExpeditionProvisionLeg
	{
		internal readonly string LarderId;
		internal readonly string ItemId;
		internal readonly int BeforeCount;
		internal readonly int AfterCount;

		internal KingdomExpeditionProvisionLeg(string larderId, string itemId, int beforeCount,
			int afterCount)
		{
			LarderId = larderId;
			ItemId = itemId;
			BeforeCount = beforeCount;
			AfterCount = afterCount;
		}
	}
}
