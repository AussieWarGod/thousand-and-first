using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Immutable cumulative price of the standing work, copied onto the physical successor before
	/// it enters the zone. It is deliberately separate from the operation receipt: terminal job
	/// compaction may discard payload detail, while strike and salvage must keep answering from
	/// what this exact building consumed rather than a later catalogue merge.
	/// </summary>
	public sealed class KingdomPaidBuildReceipt
	{
		public readonly int Water;
		public readonly long WorkTicks;
		public readonly KingdomMaterialDebitCost Material;

		public KingdomPaidBuildReceipt(int Water, long WorkTicks,
			KingdomMaterialDebitCost Material)
		{
			this.Water = Water;
			this.WorkTicks = WorkTicks;
			this.Material = (Material ?? new KingdomMaterialDebitCost()).Copy();
		}
	}
}
