using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One total, engine-free classifier for work rows and resident posts.</summary>
	internal static class KingdomWorkRules
	{
		internal static KingdomWorkKind Classify(KingdomWorkTraits traits)
		{
			if (traits.Growing) return KingdomWorkKind.Growing;
			if (traits.Construction) return KingdomWorkKind.Construction;
			if (traits.Store) return KingdomWorkKind.Store;
			if (traits.Power) return KingdomWorkKind.Power;
			if (traits.Refiner) return KingdomWorkKind.Refiner;
			if (traits.Producer) return KingdomWorkKind.Producer;
			return KingdomWorkKind.Other;
		}
	}
}
