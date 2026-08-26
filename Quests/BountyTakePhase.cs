using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>Durable take-over phases. Values are save format: append only.</summary>
	public enum BountyTakePhase
	{
		None = 0,
		Bound = 1,
		TaskIntent = 2,
		TaskDone = 3,
		ChronicleDone = 4,
		LedgerIntent = 5,
		LedgerDone = 6,
		MessageIntent = 7,
		MessageDone = 8,
		Complete = 9,
		Quarantined = 10
	}
}
