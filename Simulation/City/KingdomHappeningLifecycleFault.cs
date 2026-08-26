using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal enum KingdomHappeningLifecycleFault : byte
	{
		None = 0,
		Malformed = 1,
		UnsupportedVersion = 2,
		OverBudget = 3,
		Busy = 4,
		WrongOperation = 5,
		WrongPhase = 6,
		SequenceExhausted = 7,
		AlreadyCompleted = 8
	}
}
