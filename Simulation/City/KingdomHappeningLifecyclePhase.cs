using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal enum KingdomHappeningLifecyclePhase : byte
	{
		None = 0,
		Prepared = 1,
		Walking = 2,
		Holding = 3,
		Ready = 4,
		Restoring = 5
	}
}
