using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Where a measurement sits against its lane's budget.</summary>
	internal enum KingdomBudgetVerdict : byte
	{
		Within = 0,
		Warn = 1,
		Over = 2
	}
}
