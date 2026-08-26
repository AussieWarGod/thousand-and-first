using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>Durable founder withdrawal, including its one-shot destruction callback.</summary>
	public enum BountyWithdrawPhase
	{
		None = 0,
		Bound = 1,
		MarkCleared = 2,
		ChronicleDone = 3,
		MessageSettled = 4,
		CleanupAttempting = 5,
		CleanupLost = 6
	}
}
