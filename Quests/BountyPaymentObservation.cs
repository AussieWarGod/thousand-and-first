using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public enum BountyPaymentObservation
	{
		Malformed = 0,
		Original = 1,
		Debited = 2,
		Mixed = 3,
		Uncertain = 4
	}
}
