using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>Paid-notice publication phases. Intent precedes every uninspectable output.</summary>
	public enum BountyTerminalPhase
	{
		None = 0,
		ChronicleDone = 1,
		LedgerIntent = 2,
		LedgerDone = 3,
		MessageIntent = 4,
		MessageDone = 5,
		CleanupAttempting = 6,
		CleanupLost = 7
	}
}
