using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One receipt-bound dedicated water vessel.</summary>
	internal readonly struct KingdomExpeditionWaterLeg
	{
		internal readonly string OwnerId;
		internal readonly int BeforeVolume;
		internal readonly int AfterVolume;
		internal readonly int MaxVolume;

		internal KingdomExpeditionWaterLeg(string ownerId, int beforeVolume, int afterVolume,
			int maxVolume)
		{
			OwnerId = ownerId;
			BeforeVolume = beforeVolume;
			AfterVolume = afterVolume;
			MaxVolume = maxVolume;
		}
	}
}
