using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>Honest outcome of an uninspectable founder-facing sink.</summary>
	public enum BountySinkDisposition
	{
		None = 0,
		Pending = 1,
		Attempting = 2,
		Delivered = 3,
		Skipped = 4,
		Lost = 5
	}
}
