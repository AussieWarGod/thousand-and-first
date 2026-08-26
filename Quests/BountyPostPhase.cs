using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>Publication of a newly staked, already-durable notice.</summary>
	public enum BountyPostPhase
	{
		None = 0,
		Bound = 1,
		ChronicleDone = 2,
		MessageSettled = 3,
		Complete = 4
	}
}
