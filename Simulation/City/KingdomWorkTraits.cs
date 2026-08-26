using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>The engine facts from which every work kind is classified. Kept as a pure value so
	/// the work row, resident post and tests all use one priority table.</summary>
	internal readonly struct KingdomWorkTraits
	{
		internal readonly bool Growing;
		internal readonly bool Construction;
		internal readonly bool Store;
		internal readonly bool Power;
		internal readonly bool Refiner;
		internal readonly bool Producer;

		internal KingdomWorkTraits(bool growing, bool construction, bool store, bool power,
			bool refiner, bool producer)
		{
			Growing = growing;
			Construction = construction;
			Store = store;
			Power = power;
			Refiner = refiner;
			Producer = producer;
		}
	}
}
