using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What is owed, sorted the one way the spend is allowed to consume it: everything whose anchor
	/// cell is in the founder's field of view first, then the rest in stable row order.
	/// </summary>
	internal readonly struct KingdomReifyDemand
	{
		internal readonly int VisibleHeavy;

		internal readonly int VisibleMedium;

		internal readonly int VisibleLight;

		internal readonly int RestHeavy;

		internal readonly int RestMedium;

		internal readonly int RestLight;

		internal KingdomReifyDemand(int visibleHeavy, int visibleMedium, int visibleLight, int restHeavy, int restMedium, int restLight)
		{
			VisibleHeavy = visibleHeavy;
			VisibleMedium = visibleMedium;
			VisibleLight = visibleLight;
			RestHeavy = restHeavy;
			RestMedium = restMedium;
			RestLight = restLight;
		}

		internal bool IsEmpty
		{
			get
			{
				return VisibleHeavy == 0 && VisibleMedium == 0 && VisibleLight == 0
					&& RestHeavy == 0 && RestMedium == 0 && RestLight == 0;
			}
		}
	}
}
