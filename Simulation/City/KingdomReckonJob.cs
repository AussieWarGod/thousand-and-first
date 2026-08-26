using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One reckoning, in the only shape the executor accepts. LIVING-CITY-ARCHITECTURE &sect;2.5.
	/// </summary>
	internal sealed class KingdomReckonJob : IKingdomComputation<KingdomReckonInput, KingdomCityState>
	{
		private readonly KingdomCityAdvanceable model;

		private readonly string label;

		internal KingdomReckonJob(string label, KingdomCityAdvanceable model)
		{
			this.label = label;
			this.model = model;
		}

		public string Label
		{
			get { return label ?? ""; }
		}

		public KingdomBudgetLane Lane
		{
			get { return KingdomBudgetLane.Reckon; }
		}

		public bool TryRun(KingdomReckonInput input, out KingdomCityState output, out KingdomComputeCounters counters, out KingdomCityFault fault)
		{
			output = null;
			counters = KingdomComputeCounters.None;
			if (input.State == null || model == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			KingdomAdvanceOutcome<KingdomCityState> outcome;
			if (!KingdomAdvanceRules.TryRun(model, input.State, input.State.ProcessedThroughTick, input.ToTick, out outcome, out fault))
			{
				return false;
			}
			output = outcome.State;
			// No draws anywhere in W1's reckoning: nothing here rolls, and the receipt says so by
			// reporting a zero the tester can read against §0.0(a)'s per-happening cap.
			counters = new KingdomComputeCounters(outcome.Steps, outcome.RowVisits, 0, 0, 0L);
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
