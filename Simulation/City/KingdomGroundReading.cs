using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What one check-in found the ground holding, in the model's own units. The engine edge fills
	/// this from <c>KingdomSurvey</c>; nothing downstream of it touches a <c>Zone</c>.
	/// </summary>
	internal readonly struct KingdomGroundReading
	{
		internal readonly long WaterLevel;

		internal readonly long WaterCapacity;

		internal readonly long FoodLevel;

		internal readonly long FoodCapacity;

		internal readonly int Defence;

		internal KingdomGroundReading(long waterLevel, long waterCapacity, long foodLevel, long foodCapacity, int defence)
		{
			WaterLevel = waterLevel;
			WaterCapacity = waterCapacity;
			FoodLevel = foodLevel;
			FoodCapacity = foodCapacity;
			Defence = defence;
		}
	}
}
