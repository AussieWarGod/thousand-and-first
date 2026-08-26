using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>The one frozen answer a salvage commission may return.</summary>
	internal enum KingdomExpeditionOutcome : byte
	{
		None = 0,
		PickedClean = 1,
		ModestFind = 2,
		RichFind = 3,
		Cancelled = 4,
		ResidentDiedOnGround = 5,
		ResidentMissingFromBoundGround = 6,
		ResidentJoinedFounder = 7
	}
}
