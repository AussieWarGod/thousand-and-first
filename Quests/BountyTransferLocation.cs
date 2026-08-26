using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>Where a receipt-bound item can be proved to be.</summary>
	public enum BountyTransferLocation
	{
		Missing = 0,
		SourceOnly = 1,
		Detached = 2,
		DestinationOnly = 3,
		Both = 4,
		Elsewhere = 5
	}
}
