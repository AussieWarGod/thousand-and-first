using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Why a standing notice is not moving. Two families, and the difference decides how it is
	/// spoken (STANDARDS 7b): a <b>block</b> can lift on its own and is announced once per stall,
	/// while a <b>permanent</b> reason means the notice can never be attempted at all and is
	/// announced once, for good.
	/// </summary>
	public enum BountyBlock
	{
		/// <summary>Nothing is wrong; the notice simply has not been taken yet.</summary>
		None = 0,

		/// <summary>Nobody lives here to read it.</summary>
		NobodyToTry = 1,

		/// <summary>The rect holds nothing that has to come down. Permanent.</summary>
		NothingStanding = 2,

		/// <summary>The marked pile holds no material. Permanent.</summary>
		PileEmpty = 3,

		/// <summary>No stockpile is dedicated to carry the pile into.</summary>
		NowhereToCarry = 4,

		/// <summary>The settlement has no works at all. Permanent.</summary>
		NoWorks = 5,

		/// <summary>Every work already has its hands.</summary>
		NoIdleWork = 6,

		/// <summary>The claim has no unclaimed edge left to walk. Permanent.</summary>
		NoFrontier = 7,

		/// <summary>The work is done and the stores cannot cover the price.</summary>
		StoresCannotPay = 8,

		/// <summary>The exact work named at posting no longer exists as a staffed work.</summary>
		ManningTargetLost = 9,

		/// <summary>The named worker is not presently grounded at the settlement.</summary>
		ManningWorkerAbsent = 10,

		/// <summary>No ordinary work-pool hand remains for this contract.</summary>
		NoFreeHands = 11
	}
}
