using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The seam's own clock, and the only one in the room. A job may not read a clock; the seam
	/// must, to time it. Injected rather than ambient so a test can drive a job over a budget edge
	/// without waiting for one.
	/// </summary>
	internal interface IKingdomComputeClock
	{
		long NowMicroseconds();
	}
}
