using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal enum KingdomPhysicalHappeningKind : byte
	{
		None = 0,
		Wedding = 1,
		Funeral = 2,
		Feast = 3,
		Raising = 4,
		CommunalRite = 5
	}
}
