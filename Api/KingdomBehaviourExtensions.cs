using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	/// <summary>Runtime half of API-v3 behaviour contracts. Every third-party call is one executor
	/// job; every accepted result is normalized by <see cref="KingdomBehaviourRules"/> and published
	/// as one replacement of the durable sidecar wire.</summary>
	public static partial class KingdomExtensions
	{
		/// <summary>Whether any admitted extension owns a durable behaviour dimension.</summary>
		internal static bool AnyBehaviourSource()
		{
			foreach (Binding binding in Registry())
				if (binding.Extension is IResourceKind || binding.Extension is ICarrierKind
					|| binding.Extension is IJobKind || binding.Extension is INetworkKind
					|| binding.Extension is IWorkBehaviour) return true;
			return false;
		}

		/// <summary>Advances every durable behaviour dimension to the reading's processed tick. A
		/// malformed existing wire is retained for diagnosis and no callback runs; a faulted source
		/// contributes nothing while later sources continue.</summary>
		/// <param name="System">Realm seed authority.</param>
		/// <param name="Reading">Frozen ordinary city reading.</param>
		/// <param name="Wire">Settlement's current sidecar wire.</param>
		/// <returns>Canonical replacement wire, or the original on decode/encode failure.</returns>
		internal static string AdvanceBehaviourModel(KingdomSystem System, KingdomCityReading Reading,
			string Wire)
		{
			if (System == null || Reading == null) return Wire ?? "";
			bool hasSources = AnyBehaviourSource();
			if (!hasSources && string.IsNullOrEmpty(Wire)) return "";
			KingdomBehaviourState state;
			if (!KingdomBehaviourRules.TryDecode(Wire, out state))
			{
				Fault("The Thousand and First", "behaviour model", "MalformedWire");
				return Wire ?? "";
			}
			KingdomBehaviourState completed;
			int completedJobs, failedJobs;
			if (!KingdomBehaviourRules.TryCompleteJobs(state, Reading.ProcessedThroughTick,
				out completed, out completedJobs, out failedJobs)) return Wire ?? "";
			state = completed;
			string durable;
			if (!KingdomBehaviourRules.TryEncode(state, out durable))
			{
				Fault("The Thousand and First", "behaviour model", "EncodeCapAfterCompletion");
				return Wire ?? "";
			}
			// In-flight rows retain everything needed to complete after their owning mod is disabled.
			// Only proposal phases require live code; terminal settlement is still host authority.
			if (!hasSources)
			{
				return durable;
			}

			// Phase 1: kinds exist before anything may reference them.
			foreach (Binding binding in Registry())
			{
				IResourceKind source = binding.Extension as IResourceKind;
				if (source == null) continue;
				BehaviourCallInput input = new BehaviourCallInput(Reading, state.Reading());
				ResourceJob job = new ResourceJob(source, input, DrawHandle(System, Reading, binding), binding.ModName);
				KingdomComputeResult<KingdomResourceDefinition[]> result = KingdomCity.Seam.Submit(input, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "resource kinds", result.Status.ToString());
					continue;
				}
				KingdomBehaviourState posted; int kept;
				if (KingdomBehaviourRules.TryApplyResources(state, binding.ModName, result.Value,
					out posted, out kept))
					TryAdmitBehaviourState(ref state, posted, binding.ModName, "resource kinds",
						ref durable);
			}

			// Phase 2: carriers are ephemeral definitions, aggregated by owner under one owner cap.
			Dictionary<string, List<KingdomCarrierKindRow>> carrierRows =
				new Dictionary<string, List<KingdomCarrierKindRow>>(StringComparer.Ordinal);
			foreach (Binding binding in Registry())
			{
				ICarrierKind source = binding.Extension as ICarrierKind;
				if (source == null) continue;
				BehaviourCallInput input = new BehaviourCallInput(Reading, state.Reading());
				CarrierJob job = new CarrierJob(source, input, DrawHandle(System, Reading, binding), binding.ModName);
				KingdomComputeResult<KingdomCarrierDefinition[]> result = KingdomCity.Seam.Submit(input, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "carrier kinds", result.Status.ToString());
					continue;
				}
				int kept;
				KingdomCarrierKindRow[] normalized = KingdomBehaviourRules.NormalizeCarriers(
					binding.ModName, result.Value, out kept);
				List<KingdomCarrierKindRow> owned;
				if (!carrierRows.TryGetValue(binding.ModName, out owned))
				{
					owned = new List<KingdomCarrierKindRow>();
					carrierRows.Add(binding.ModName, owned);
				}
				for (int i = 0; i < normalized.Length
					&& owned.Count < KingdomApiRules.MaxCarrierKindsPerOwner; i++)
				{
					bool duplicate = false;
					for (int j = 0; j < owned.Count; j++) if (owned[j].Key == normalized[i].Key) duplicate = true;
					if (!duplicate) owned.Add(normalized[i]);
				}
			}

			// Phase 3: networks integrate before works and jobs inspect their resulting stock.
			foreach (Binding binding in Registry())
			{
				INetworkKind source = binding.Extension as INetworkKind;
				if (source == null) continue;
				BehaviourCallInput input = new BehaviourCallInput(Reading, state.Reading());
				NetworkJob job = new NetworkJob(source, input, DrawHandle(System, Reading, binding), binding.ModName);
				KingdomComputeResult<KingdomNetworkPlan[]> result = KingdomCity.Seam.Submit(input, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "network kinds", result.Status.ToString());
					continue;
				}
				KingdomBehaviourState posted; int kept;
				if (KingdomBehaviourRules.TryApplyNetworks(state, binding.ModName, result.Value,
					Reading, Reading.ProcessedThroughTick, out posted, out kept))
					TryAdmitBehaviourState(ref state, posted, binding.ModName, "network kinds",
						ref durable);
			}

			// Phase 4: work rows publish state, resource changes and explicit physical debt atomically.
			foreach (Binding binding in Registry())
			{
				IWorkBehaviour source = binding.Extension as IWorkBehaviour;
				if (source == null) continue;
				BehaviourCallInput input = new BehaviourCallInput(Reading, state.Reading());
				WorkJob job = new WorkJob(source, input, DrawHandle(System, Reading, binding), binding.ModName);
				KingdomComputeResult<KingdomWorkAdvance[]> result = KingdomCity.Seam.Submit(input, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "work behaviours", result.Status.ToString());
					continue;
				}
				KingdomBehaviourState posted; int kept;
				if (KingdomBehaviourRules.TryApplyWorks(state, binding.ModName, result.Value,
					Reading, Reading.ProcessedThroughTick, out posted, out kept))
					TryAdmitBehaviourState(ref state, posted, binding.ModName, "work behaviours",
						ref durable);
			}

			// Phase 5: jobs see every kind and work/network result from this same frozen host pass.
			foreach (Binding binding in Registry())
			{
				IJobKind source = binding.Extension as IJobKind;
				if (source == null) continue;
				BehaviourCallInput input = new BehaviourCallInput(Reading, state.Reading());
				JobKindJob job = new JobKindJob(source, input, DrawHandle(System, Reading, binding), binding.ModName);
				KingdomComputeResult<KingdomJobPlan[]> result = KingdomCity.Seam.Submit(input, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "job kinds", result.Status.ToString());
					continue;
				}
				List<KingdomCarrierKindRow> owned;
				KingdomCarrierKindRow[] carriers = carrierRows.TryGetValue(binding.ModName, out owned)
					? owned.ToArray() : new KingdomCarrierKindRow[0];
				KingdomBehaviourState posted; int kept;
				if (KingdomBehaviourRules.TryApplyJobs(state, binding.ModName, result.Value, carriers,
					Reading, Reading.ProcessedThroughTick, out posted, out kept))
					TryAdmitBehaviourState(ref state, posted, binding.ModName, "job kinds",
						ref durable);
			}
			return durable;
		}

		/// <summary>Commits one callback's whole normalized result only when the bounded durable codec
		/// can represent it. An oversized owner therefore cannot roll back host-completed jobs or a
		/// preceding owner's accepted result.</summary>
		private static bool TryAdmitBehaviourState(ref KingdomBehaviourState state,
			KingdomBehaviourState candidate, string owner, string lane, ref string durable)
		{
			string encoded;
			if (candidate == null || !KingdomBehaviourRules.TryEncode(candidate, out encoded))
			{
				Fault(owner, lane, "EncodeCap");
				return false;
			}
			state = candidate;
			durable = encoded;
			return true;
		}

		/// <summary>Reads a durable wire for reports and extension snapshots. Malformed input becomes
		/// an empty reading only on this presentation path; authoritative advancement retains it.</summary>
		internal static KingdomBehaviourReading BehaviourReading(string wire)
		{
			KingdomBehaviourState state;
			return KingdomBehaviourRules.TryDecode(wire, out state)
				? state.Reading() : KingdomBehaviourState.Empty.Reading();
		}

		private static KingdomExtensionDraws DrawHandle(KingdomSystem system,
			KingdomCityReading reading, Binding binding)
		{
			return new KingdomExtensionDraws(system.SimulationSeed, reading.SettlementId,
				binding.ModName);
		}

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
