using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>One exact inventory transfer's durable phase. Values are save format.</summary>
	public enum BountyTransferPhase
	{
		None = 0,
		Bound = 1,
		RemoveIntent = 2,
		Detached = 3,
		AddIntent = 4,
		Arrived = 5,
		Quarantined = 6
	}
}
