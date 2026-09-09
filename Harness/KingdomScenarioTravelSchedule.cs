using System.Collections.Generic;
using XRL;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Read-only schedule continuity, not proof of every economic side effect.</summary>
	internal static class KingdomScenarioTravelSchedule
	{
		private static readonly Dictionary<int, long> Next = new Dictionary<int, long>();
		private static readonly Dictionary<int, int> Ordinal = new Dictionary<int, int>();
		internal static int Observations;

		internal static void Observe(KingdomCityBook Book)
		{
			KingdomScenarioTravel.Require(The.Game != null
				&& ReferenceEquals(The.Game, KingdomScenarioTravel.Game), "schedule observer lost its exact game");
			var seen = new HashSet<int>();
			for (int i = 0; i < Book.ClockKinds.Count; i++)
			{
				int kind = Book.ClockKinds[i], ordinal = Book.ClockOrdinals[i];
				long next = Book.ClockNextDueTicks[i];
				KingdomScenarioTravel.Require(seen.Add(kind) && next >= 0 && ordinal >= 0,
					"schedule contains duplicate kinds or negative values");
				if (Next.TryGetValue(kind, out long before))
					KingdomScenarioTravel.Require(KingdomScenarioTravelRules.Schedule(before, Ordinal[kind], next, ordinal),
						"schedule deadline or ordinal moved backwards");
				Next[kind] = next; Ordinal[kind] = ordinal;
			}
			KingdomScenarioTravel.Require(seen.Count == Next.Count, "a previously observed schedule disappeared");
			Observations++;
		}
	}
}
