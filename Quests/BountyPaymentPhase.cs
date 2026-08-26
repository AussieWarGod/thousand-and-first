using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>Exact-water payout phases. Bound and DebitIntent always carry vessel rows.</summary>
	public enum BountyPaymentPhase
	{
		None = 0,
		Bound = 1,
		DebitIntent = 2,
		Debited = 3,
		Credited = 4,
		Quarantined = 5
	}
}
