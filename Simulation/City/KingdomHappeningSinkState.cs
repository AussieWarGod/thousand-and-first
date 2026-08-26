using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal enum KingdomHappeningSinkState : byte
	{
		Pending = 0,
		Attempting = 1,
		Delivered = 2,
		Skipped = 3,
		Lost = 4
	}
}
