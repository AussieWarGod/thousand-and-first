using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomExtensions
	{
		/// <summary>Frozen compound input for every v3 callback.</summary>
		private readonly struct BehaviourCallInput
		{
			internal readonly KingdomCityReading City;
			internal readonly KingdomBehaviourReading Model;

			internal BehaviourCallInput(KingdomCityReading city, KingdomBehaviourReading model)
			{
				City = city;
				Model = model;
			}
		}

		private abstract class BehaviourJobBase
		{
			protected readonly BehaviourCallInput Input;
			protected readonly KingdomExtensionDraws Draws;
			private readonly string label;

			protected BehaviourJobBase(BehaviourCallInput input, KingdomExtensionDraws draws,
				string owner, string lane)
			{
				Input = input; Draws = draws;
				label = "ext:" + lane + ":" + KingdomApiRules.Slug(owner);
			}

			public string Label { get { return label; } }
			public KingdomBudgetLane Lane { get { return KingdomBudgetLane.Reckon; } }
			protected KingdomComputeCounters Counters(int rows)
			{
				return new KingdomComputeCounters(0, rows < 0 ? 0L : rows,
					Draws.ReportedDraws, 0, 0L);
			}
		}

		private sealed class ResourceJob : BehaviourJobBase,
			IKingdomComputation<BehaviourCallInput, KingdomResourceDefinition[]>
		{
			private readonly IResourceKind source;
			internal ResourceJob(IResourceKind source, BehaviourCallInput input,
				KingdomExtensionDraws draws, string owner)
				: base(input, draws, owner, "resources") { this.source = source; }
			public bool TryRun(BehaviourCallInput input, out KingdomResourceDefinition[] output,
				out KingdomComputeCounters counters, out KingdomCityFault fault)
			{ output = source.Resources(input.City, input.Model, Draws); counters = Counters(output == null ? 0 : output.Length); fault = KingdomCityFault.None; return true; }
		}

		private sealed class CarrierJob : BehaviourJobBase,
			IKingdomComputation<BehaviourCallInput, KingdomCarrierDefinition[]>
		{
			private readonly ICarrierKind source;
			internal CarrierJob(ICarrierKind source, BehaviourCallInput input,
				KingdomExtensionDraws draws, string owner)
				: base(input, draws, owner, "carriers") { this.source = source; }
			public bool TryRun(BehaviourCallInput input, out KingdomCarrierDefinition[] output,
				out KingdomComputeCounters counters, out KingdomCityFault fault)
			{ output = source.Carriers(input.City, input.Model, Draws); counters = Counters(output == null ? 0 : output.Length); fault = KingdomCityFault.None; return true; }
		}

		private sealed class JobKindJob : BehaviourJobBase,
			IKingdomComputation<BehaviourCallInput, KingdomJobPlan[]>
		{
			private readonly IJobKind source;
			internal JobKindJob(IJobKind source, BehaviourCallInput input,
				KingdomExtensionDraws draws, string owner)
				: base(input, draws, owner, "jobs") { this.source = source; }
			public bool TryRun(BehaviourCallInput input, out KingdomJobPlan[] output,
				out KingdomComputeCounters counters, out KingdomCityFault fault)
			{ output = source.Jobs(input.City, input.Model, Draws); counters = Counters(output == null ? 0 : output.Length); fault = KingdomCityFault.None; return true; }
		}

		private sealed class NetworkJob : BehaviourJobBase,
			IKingdomComputation<BehaviourCallInput, KingdomNetworkPlan[]>
		{
			private readonly INetworkKind source;
			internal NetworkJob(INetworkKind source, BehaviourCallInput input,
				KingdomExtensionDraws draws, string owner)
				: base(input, draws, owner, "networks") { this.source = source; }
			public bool TryRun(BehaviourCallInput input, out KingdomNetworkPlan[] output,
				out KingdomComputeCounters counters, out KingdomCityFault fault)
			{ output = source.Networks(input.City, input.Model, Draws); counters = Counters(output == null ? 0 : output.Length); fault = KingdomCityFault.None; return true; }
		}

		private sealed class WorkJob : BehaviourJobBase,
			IKingdomComputation<BehaviourCallInput, KingdomWorkAdvance[]>
		{
			private readonly IWorkBehaviour source;
			internal WorkJob(IWorkBehaviour source, BehaviourCallInput input,
				KingdomExtensionDraws draws, string owner)
				: base(input, draws, owner, "works") { this.source = source; }
			public bool TryRun(BehaviourCallInput input, out KingdomWorkAdvance[] output,
				out KingdomComputeCounters counters, out KingdomCityFault fault)
			{ output = source.Advance(input.City, input.Model, Draws); counters = Counters(output == null ? 0 : output.Length); fault = KingdomCityFault.None; return true; }
		}
	}
}
