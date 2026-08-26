using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	/// <summary>Normalized carrier kind used only during one host pass.</summary>
	internal readonly struct KingdomCarrierKindRow
	{
		internal readonly string Key;
		internal readonly string Blueprint;
		internal readonly int WalkTicksPerCell;
		internal readonly int Capacity;

		internal KingdomCarrierKindRow(string key, string blueprint, int walkTicksPerCell, int capacity)
		{
			Key = key;
			Blueprint = blueprint;
			WalkTicksPerCell = walkTicksPerCell;
			Capacity = capacity;
		}
	}

	/// <summary>Durable extension job row. Plans are copied into this row at opening; no extension
	/// callback is required for completion or recovery.</summary>
	internal sealed class KingdomBehaviourJobRow
	{
		private readonly KingdomExtensionLeg[] legs;
		private readonly KingdomResourceChange[] completion;

		internal readonly string Key;
		internal readonly string CarrierKey;
		internal readonly string CarrierBlueprint;
		internal readonly int WalkTicksPerCell;
		internal readonly string CargoResourceKey;
		internal readonly int CargoAmount;
		internal readonly long StartTick;
		internal readonly long DueTick;
		internal readonly KingdomExtensionJobStatus Status;

		internal KingdomBehaviourJobRow(string key, string carrierKey, string carrierBlueprint,
			int walkTicksPerCell, string cargoResourceKey, int cargoAmount, long startTick,
			long dueTick, KingdomExtensionJobStatus status, KingdomExtensionLeg[] legs,
			KingdomResourceChange[] completion)
		{
			Key = key ?? "";
			CarrierKey = carrierKey ?? "";
			CarrierBlueprint = carrierBlueprint ?? "";
			WalkTicksPerCell = walkTicksPerCell;
			CargoResourceKey = cargoResourceKey ?? "";
			CargoAmount = cargoAmount;
			StartTick = startTick;
			DueTick = dueTick;
			Status = status;
			this.legs = Copy(legs);
			this.completion = Copy(completion);
		}

		internal int LegCount { get { return legs.Length; } }
		internal int CompletionCount { get { return completion.Length; } }

		internal bool TryLeg(int index, out KingdomExtensionLeg leg)
		{
			leg = default(KingdomExtensionLeg);
			if (index < 0 || index >= legs.Length) return false;
			leg = legs[index]; return true;
		}

		internal bool TryCompletion(int index, out KingdomResourceChange change)
		{
			change = default(KingdomResourceChange);
			if (index < 0 || index >= completion.Length) return false;
			change = completion[index]; return true;
		}

		internal KingdomBehaviourJobRow WithStatus(KingdomExtensionJobStatus status)
		{
			return new KingdomBehaviourJobRow(Key, CarrierKey, CarrierBlueprint, WalkTicksPerCell,
				CargoResourceKey, CargoAmount, StartTick, DueTick, status, legs, completion);
		}

		internal KingdomExtensionJobReading Reading()
		{
			return new KingdomExtensionJobReading(Key, CarrierKey, CarrierBlueprint,
				CargoResourceKey, CargoAmount, StartTick, DueTick, Status);
		}

		private static KingdomExtensionLeg[] Copy(KingdomExtensionLeg[] source)
		{
			if (source == null || source.Length == 0) return new KingdomExtensionLeg[0];
			KingdomExtensionLeg[] copy = new KingdomExtensionLeg[source.Length];
			Array.Copy(source, copy, source.Length); return copy;
		}

		private static KingdomResourceChange[] Copy(KingdomResourceChange[] source)
		{
			if (source == null || source.Length == 0) return new KingdomResourceChange[0];
			KingdomResourceChange[] copy = new KingdomResourceChange[source.Length];
			Array.Copy(source, copy, source.Length); return copy;
		}
	}

	/// <summary>Frozen durable sidecar state. Every mutation in <see cref="KingdomBehaviourRules"/>
	/// returns another instance; failed rows leave this instance untouched.</summary>
	internal sealed class KingdomBehaviourState
	{
		private readonly KingdomResourceReading[] resources;
		private readonly KingdomBehaviourJobRow[] jobs;
		private readonly KingdomExtensionNetworkReading[] networks;
		private readonly KingdomWorkBehaviourReading[] works;

		internal static readonly KingdomBehaviourState Empty = new KingdomBehaviourState(null, null, null, null);

		internal KingdomBehaviourState(KingdomResourceReading[] resources,
			KingdomBehaviourJobRow[] jobs, KingdomExtensionNetworkReading[] networks,
			KingdomWorkBehaviourReading[] works)
		{
			this.resources = Copy(resources);
			this.jobs = Copy(jobs);
			this.networks = Copy(networks);
			this.works = Copy(works);
		}

		internal int ResourceCount { get { return resources.Length; } }
		internal int JobCount { get { return jobs.Length; } }
		internal int NetworkCount { get { return networks.Length; } }
		internal int WorkCount { get { return works.Length; } }

		internal bool TryResource(int index, out KingdomResourceReading row)
		{
			row = default(KingdomResourceReading);
			if (index < 0 || index >= resources.Length) return false;
			row = resources[index]; return true;
		}

		internal bool TryJob(int index, out KingdomBehaviourJobRow row)
		{
			row = null;
			if (index < 0 || index >= jobs.Length) return false;
			row = jobs[index]; return true;
		}

		internal bool TryNetwork(int index, out KingdomExtensionNetworkReading row)
		{
			row = default(KingdomExtensionNetworkReading);
			if (index < 0 || index >= networks.Length) return false;
			row = networks[index]; return true;
		}

		internal bool TryWork(int index, out KingdomWorkBehaviourReading row)
		{
			row = default(KingdomWorkBehaviourReading);
			if (index < 0 || index >= works.Length) return false;
			row = works[index]; return true;
		}

		internal KingdomResourceReading[] Resources() { return Copy(resources); }
		internal KingdomBehaviourJobRow[] Jobs() { return Copy(jobs); }
		internal KingdomExtensionNetworkReading[] Networks() { return Copy(networks); }
		internal KingdomWorkBehaviourReading[] Works() { return Copy(works); }

		internal KingdomBehaviourReading Reading()
		{
			KingdomExtensionJobReading[] jobReadings = new KingdomExtensionJobReading[jobs.Length];
			for (int i = 0; i < jobs.Length; i++) jobReadings[i] = jobs[i].Reading();
			return new KingdomBehaviourReading(resources, jobReadings, networks, works);
		}

		private static T[] Copy<T>(T[] source)
		{
			if (source == null || source.Length == 0) return new T[0];
			T[] copy = new T[source.Length]; Array.Copy(source, copy, source.Length); return copy;
		}
	}

	/// <summary>Pure host for API-v3 behaviour results: owner qualification, row caps, atomic
	/// resource changes, deterministic itinerary timing, bounded network solve, frozen work state,
	/// and the one canonical durable wire string.</summary>
	internal static class KingdomBehaviourRules
	{
		internal const long TicksPerDay = 1200L;
		internal const long MaxResourceQuantity = 1000000000L;
		internal const int MaxOwedObjectsPerWork = 1000;
		private const int WireMagic = 0x33464154; // TAF3, little-endian
		private const int LegacyWireVersion = 1;
		private const int WireVersion = 2;

		internal static KingdomBehaviourReading Reading(KingdomBehaviourState state)
		{
			return (state ?? KingdomBehaviourState.Empty).Reading();
		}

		internal static bool TryApplyResources(KingdomBehaviourState state, string owner,
			KingdomResourceDefinition[] candidates, out KingdomBehaviourState next, out int kept)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; kept = 0;
			KingdomResourceReading[] rows = state.Resources();
			List<string> seen = new List<string>();
			int owned = CountOwner(rows, owner);
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxResourceKindsPerOwner; i++)
			{
				KingdomResourceDefinition candidate = candidates[i];
				string key = KingdomApiRules.ExtensionKey(owner, candidate.Key);
				string unit = KingdomApiRules.BehaviourIdentifier(candidate.Unit, true);
				string property = KingdomApiRules.BehaviourIdentifier(candidate.ContainerProperty, false);
				string liquid = KingdomApiRules.BehaviourIdentifier(candidate.LiquidId, false);
				string network = string.IsNullOrWhiteSpace(candidate.NetworkKey) ? ""
					: KingdomApiRules.ExtensionKey(owner, candidate.NetworkKey);
				if (key == null || unit == null || property == null || liquid == null || network == null
					|| candidate.Capacity < 0L || candidate.Capacity > MaxResourceQuantity
					|| candidate.InitialLevel < 0L || candidate.InitialLevel > candidate.Capacity
					|| seen.Contains(key)) continue;
				seen.Add(key);
				int at = ResourceIndex(rows, key);
				if (at < 0)
				{
					if (owned >= KingdomApiRules.MaxResourceKindsPerOwner
						|| rows.Length >= KingdomApiRules.MaxResourceKindsPerCity) continue;
					rows = Append(rows, new KingdomResourceReading(key, unit, property, network, liquid,
						candidate.InitialLevel, candidate.Capacity));
					owned++;
				}
				else
				{
					long level = rows[at].Level > candidate.Capacity ? candidate.Capacity : rows[at].Level;
					rows[at] = new KingdomResourceReading(key, unit, property, network, liquid,
						level, candidate.Capacity);
				}
				kept++;
			}
			if (kept > 0) next = new KingdomBehaviourState(rows, state.Jobs(), state.Networks(), state.Works());
			return true;
		}

		internal static KingdomCarrierKindRow[] NormalizeCarriers(string owner,
			KingdomCarrierDefinition[] candidates, out int kept)
		{
			List<KingdomCarrierKindRow> rows = new List<KingdomCarrierKindRow>();
			kept = 0;
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxCarrierKindsPerOwner; i++)
			{
				KingdomCarrierDefinition candidate = candidates[i];
				string key = KingdomApiRules.ExtensionKey(owner, candidate.Key);
				string blueprint = KingdomApiRules.BehaviourIdentifier(candidate.Blueprint, true);
				if (key == null || blueprint == null || candidate.WalkTicksPerCell <= 0
					|| candidate.WalkTicksPerCell > 100000 || candidate.Capacity <= 0
					|| candidate.Capacity > 1000000 || CarrierIndex(rows, key) >= 0) continue;
				rows.Add(new KingdomCarrierKindRow(key, blueprint, candidate.WalkTicksPerCell,
					candidate.Capacity));
				kept++;
			}
			return rows.ToArray();
		}

		internal static bool TryCompleteJobs(KingdomBehaviourState state, long nowTick,
			out KingdomBehaviourState next, out int completed, out int failed)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; completed = 0; failed = 0;
			if (nowTick < 0L) return false;
			KingdomResourceReading[] resources = state.Resources();
			KingdomBehaviourJobRow[] jobs = state.Jobs();
			bool changed = false;
			for (int i = 0; i < jobs.Length; i++)
			{
				KingdomBehaviourJobRow job = jobs[i];
				if (job.Status != KingdomExtensionJobStatus.Open || job.DueTick > nowTick) continue;
				KingdomResourceChange[] changes = JobChanges(job);
				KingdomResourceReading[] posted;
				if (TryApplyOwnedChanges(resources, OwnerOf(job.Key), changes, out posted))
				{
					resources = posted;
					jobs[i] = job.WithStatus(KingdomExtensionJobStatus.Completed);
					completed++;
				}
				else
				{
					// Completion is all-or-nothing. A failed carrier gives its reserved cargo back if
					// capacity still permits; inability to restore is represented by the unchanged
					// bounded stock, never arithmetic overflow.
					int cargo = ResourceIndex(resources, job.CargoResourceKey);
					if (cargo >= 0 && resources[cargo].Level <= resources[cargo].Capacity - job.CargoAmount)
						resources[cargo] = WithLevel(resources[cargo], resources[cargo].Level + job.CargoAmount);
					jobs[i] = job.WithStatus(KingdomExtensionJobStatus.Failed);
					failed++;
				}
				changed = true;
			}
			KingdomBehaviourJobRow[] retained = TrimTerminalJobs(jobs);
			if (retained.Length != jobs.Length)
			{
				jobs = retained;
				changed = true;
			}
			if (changed) next = new KingdomBehaviourState(resources, jobs, state.Networks(), state.Works());
			return true;
		}

		internal static bool TryApplyJobs(KingdomBehaviourState state, string owner,
			KingdomJobPlan[] candidates, KingdomCarrierKindRow[] carriers, KingdomCityReading city,
			long nowTick, out KingdomBehaviourState next, out int kept)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; kept = 0;
			if (city == null || nowTick < 0L) return false;
			KingdomResourceReading[] resources = state.Resources();
			KingdomBehaviourJobRow[] originalJobs = state.Jobs();
			KingdomBehaviourJobRow[] jobs = TrimTerminalJobs(originalJobs);
			int owned = CountOpenOwner(jobs, owner);
			int open = CountOpen(jobs);
			List<string> seen = new List<string>();
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxJobsPerOwner; i++)
			{
				KingdomJobPlan plan = candidates[i];
				if (plan == null || plan.StartTick != nowTick || plan.CargoAmount <= 0
					|| plan.LegCount <= 0 || plan.LegCount > KingdomApiRules.MaxLegsPerJob
					|| plan.CompletionChangeCount > KingdomApiRules.MaxChangesPerResult) continue;
				string key = KingdomApiRules.ExtensionKey(owner, plan.Key);
				string carrierKey = KingdomApiRules.ExtensionKey(owner, plan.CarrierKey);
				string cargoKey = KingdomApiRules.ExtensionKey(owner, plan.CargoResourceKey);
				if (key == null || carrierKey == null || cargoKey == null || seen.Contains(key)
					|| JobIndex(jobs, key) >= 0) continue;
				seen.Add(key);
				int carrierAt = CarrierIndex(carriers, carrierKey);
				int resourceAt = ResourceIndex(resources, cargoKey);
				if (carrierAt < 0 || resourceAt < 0 || plan.CargoAmount > carriers[carrierAt].Capacity
					|| resources[resourceAt].Level < plan.CargoAmount
					|| owned >= KingdomApiRules.MaxJobsPerOwner
					|| open >= KingdomApiRules.MaxJobsPerCity
					|| jobs.Length >= KingdomApiRules.MaxStoredJobsPerCity) continue;
				KingdomExtensionLeg[] legs;
				long due;
				if (!TryLegs(plan, city, carriers[carrierAt].WalkTicksPerCell, nowTick,
					out legs, out due)) continue;
				KingdomResourceChange[] completion;
				if (!TryNormalizeChanges(owner, plan, out completion)) continue;
				resources[resourceAt] = WithLevel(resources[resourceAt],
					resources[resourceAt].Level - plan.CargoAmount);
				jobs = Append(jobs, new KingdomBehaviourJobRow(key, carrierKey,
					carriers[carrierAt].Blueprint, carriers[carrierAt].WalkTicksPerCell, cargoKey,
					plan.CargoAmount, nowTick, due, KingdomExtensionJobStatus.Open, legs, completion));
				owned++; open++; kept++;
			}
			if (kept > 0 || jobs.Length != originalJobs.Length)
				next = new KingdomBehaviourState(resources, jobs, state.Networks(), state.Works());
			return true;
		}

		internal static bool TryApplyNetworks(KingdomBehaviourState state, string owner,
			KingdomNetworkPlan[] candidates, KingdomCityReading city, long nowTick,
			out KingdomBehaviourState next, out int kept)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; kept = 0;
			if (city == null || nowTick < 0L) return false;
			KingdomResourceReading[] resources = state.Resources();
			KingdomExtensionNetworkReading[] rows = state.Networks();
			int owned = CountOwner(rows, owner);
			List<string> seen = new List<string>();
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxNetworksPerOwner; i++)
			{
				KingdomNetworkPlan plan = candidates[i];
				if (plan == null || plan.NodeCount <= 0
					|| plan.NodeCount > KingdomApiRules.MaxNodesPerNetwork
					|| plan.EdgeCount > KingdomApiRules.MaxEdgesPerNetwork) continue;
				string key = KingdomApiRules.ExtensionKey(owner, plan.Key);
				string resourceKey = KingdomApiRules.ExtensionKey(owner, plan.ResourceKey);
				int resourceAt = ResourceIndex(resources, resourceKey);
				if (key == null || resourceKey == null || resourceAt < 0 || seen.Contains(key)) continue;
				if (!string.IsNullOrEmpty(resources[resourceAt].NetworkKey)
					&& resources[resourceAt].NetworkKey != key) continue;
				seen.Add(key);
				int at = NetworkIndex(rows, key);
				if (at < 0 && (owned >= KingdomApiRules.MaxNetworksPerOwner
					|| rows.Length >= KingdomApiRules.MaxNetworksPerCity)) continue;
				int flow, brownout, supply;
				if (!TrySolve(plan, city, out flow, out brownout, out supply)) continue;
				long from = at < 0 ? nowTick : rows[at].ProcessedThroughTick;
				if (from < 0L || nowTick < from) continue;
				long days = nowTick / TicksPerDay - from / TicksPerDay;
				long surplus = supply - flow;
				if (days > 0L && surplus > 0L)
				{
					if (surplus > long.MaxValue / days) continue;
					long made = surplus * days;
					long room = resources[resourceAt].Room;
					if (made > room) made = room;
					resources[resourceAt] = WithLevel(resources[resourceAt],
						resources[resourceAt].Level + made);
				}
				KingdomExtensionNetworkReading reading = new KingdomExtensionNetworkReading(
					key, resourceKey, nowTick, flow, brownout);
				if (at < 0) { rows = Append(rows, reading); owned++; }
				else rows[at] = reading;
				kept++;
			}
			if (kept > 0) next = new KingdomBehaviourState(resources, state.Jobs(), rows, state.Works());
			return true;
		}

		internal static bool TryApplyWorks(KingdomBehaviourState state, string owner,
			KingdomWorkAdvance[] candidates, KingdomCityReading city, long nowTick,
			out KingdomBehaviourState next, out int kept)
		{
			state = state ?? KingdomBehaviourState.Empty;
			next = state; kept = 0;
			if (city == null || nowTick < 0L) return false;
			KingdomResourceReading[] resources = state.Resources();
			KingdomWorkBehaviourReading[] rows = state.Works();
			int owned = CountOwner(rows, owner);
			List<string> seen = new List<string>();
			for (int i = 0; candidates != null && i < candidates.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxWorkBehavioursPerOwner; i++)
			{
				KingdomWorkAdvance result = candidates[i];
				if (result == null || result.WorkId <= 0 || result.NextTick <= nowTick
					|| result.ChangeCount > KingdomApiRules.MaxChangesPerResult
					|| result.MaterialisationCount > KingdomApiRules.MaxMaterialisationsPerAdvance
					|| !HasWork(city, result.WorkId)) continue;
				string key = KingdomApiRules.ExtensionKey(owner, result.BehaviourKey);
				string rowKey = key == null ? null : key + "#" + result.WorkId;
				if (key == null || seen.Contains(rowKey)) continue;
				seen.Add(rowKey);
				int at = WorkIndex(rows, key, result.WorkId);
				if (at >= 0 && rows[at].NextTick > nowTick) continue;
				if (at < 0 && (owned >= KingdomApiRules.MaxWorkBehavioursPerOwner
					|| rows.Length >= KingdomApiRules.MaxWorkBehavioursPerCity)) continue;
				KingdomResourceChange[] changes = WorkChanges(result);
				KingdomResourceReading[] posted;
				if (!TryApplyOwnedChanges(resources, owner, changes, out posted)) continue;
				string owedBlueprint = at < 0 ? "" : rows[at].OwedBlueprint;
				int owedCount = at < 0 ? 0 : rows[at].OwedCount;
				long materialisationSequence = at < 0 ? 0L : rows[at].MaterialisationSequence;
				if (result.MaterialisationCount > 0)
				{
					KingdomMaterialisation materialisation;
					if (!result.TryMaterialisation(0, out materialisation)) continue;
					string blueprint = KingdomApiRules.BehaviourIdentifier(materialisation.Blueprint, true);
					if (blueprint == null || materialisation.Count <= 0
						|| materialisation.Count > MaxOwedObjectsPerWork
						|| (owedCount > 0 && owedBlueprint != blueprint)
						|| owedCount > MaxOwedObjectsPerWork - materialisation.Count
						|| materialisationSequence == long.MaxValue) continue;
					owedBlueprint = blueprint;
					owedCount += materialisation.Count;
					materialisationSequence++;
				}
				resources = posted;
				KingdomWorkBehaviourReading row = new KingdomWorkBehaviourReading(key, result.WorkId,
					result.NextState, result.NextTick, owedBlueprint, owedCount, materialisationSequence);
				if (at < 0) { rows = Append(rows, row); owned++; }
				else rows[at] = row;
				kept++;
			}
			if (kept > 0) next = new KingdomBehaviourState(resources, state.Jobs(), state.Networks(), rows);
			return true;
		}

		/// <summary>Removes landed physical debt from one exact behaviour/work row. Used only after
		/// the engine edge has successfully placed the exact blueprint.</summary>
		internal static bool TryAcknowledgeMaterialisation(KingdomBehaviourState state,
			string behaviourKey, int workId, string blueprint, int count, out KingdomBehaviourState next)
		{
			state = state ?? KingdomBehaviourState.Empty; next = state;
			if (count <= 0) return false;
			KingdomWorkBehaviourReading[] rows = state.Works();
			int at = WorkIndex(rows, behaviourKey, workId);
			if (at < 0 || rows[at].OwedBlueprint != blueprint || rows[at].OwedCount < count) return false;
			int left = rows[at].OwedCount - count;
			rows[at] = new KingdomWorkBehaviourReading(rows[at].BehaviourKey, workId,
				rows[at].State, rows[at].NextTick, left == 0 ? "" : blueprint, left,
				rows[at].MaterialisationSequence);
			next = new KingdomBehaviourState(state.Resources(), state.Jobs(), state.Networks(), rows);
			return true;
		}

		/// <summary>Canonical exact-ground receipt. Generation separates later output from a stale
		/// marker left after acknowledgement; owed count separates each unit within one generation.</summary>
		internal static string MaterialisationReceipt(KingdomWorkBehaviourReading owed)
		{
			return owed.BehaviourKey + "|" + owed.WorkId.ToString(CultureInfo.InvariantCulture)
				+ "|" + owed.MaterialisationSequence.ToString(CultureInfo.InvariantCulture)
				+ "|" + owed.OwedCount.ToString(CultureInfo.InvariantCulture);
		}

		internal static bool TryEncode(KingdomBehaviourState state, out string wire)
		{
			return TryEncodeVersion(state, WireVersion, KingdomApiRules.MaxBehaviourModelBytes,
				out wire);
		}

#if TAF_TESTS
		/// <summary>Produces the exact bounded v1 carrier so migration tests can exercise the old
		/// aggregate ceiling instead of a tiny fixture that never approaches it.</summary>
		internal static bool TryEncodeLegacyV1ForTests(KingdomBehaviourState state,
			out string wire)
		{
			return TryEncodeVersion(state, LegacyWireVersion,
				KingdomApiRules.LegacyBehaviourModelBytes, out wire);
		}
#endif

		private static bool TryEncodeVersion(KingdomBehaviourState state, int wireVersion,
			int maximumBytes, out string wire)
		{
			wire = ""; state = state ?? KingdomBehaviourState.Empty;
			if ((wireVersion != LegacyWireVersion && wireVersion != WireVersion)
				|| maximumBytes <= 0) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
				{
					writer.Write(WireMagic); writer.Write(wireVersion);
					writer.Write(state.ResourceCount);
					for (int i = 0; i < state.ResourceCount; i++)
					{
						KingdomResourceReading row; state.TryResource(i, out row);
						Write(writer, row.Key); Write(writer, row.Unit); Write(writer, row.ContainerProperty);
						Write(writer, row.NetworkKey); Write(writer, row.LiquidId);
						writer.Write(row.Level); writer.Write(row.Capacity);
					}
					writer.Write(state.JobCount);
					for (int i = 0; i < state.JobCount; i++)
					{
						KingdomBehaviourJobRow row; state.TryJob(i, out row);
						Write(writer, row.Key); Write(writer, row.CarrierKey); Write(writer, row.CarrierBlueprint);
						writer.Write(row.WalkTicksPerCell); Write(writer, row.CargoResourceKey);
						writer.Write(row.CargoAmount); writer.Write(row.StartTick); writer.Write(row.DueTick);
						writer.Write((byte)row.Status); writer.Write(row.LegCount);
						for (int j = 0; j < row.LegCount; j++)
						{
							KingdomExtensionLeg leg; row.TryLeg(j, out leg); Write(writer, leg.ZoneId);
							writer.Write(leg.EnterX); writer.Write(leg.EnterY); writer.Write(leg.ExitX); writer.Write(leg.ExitY);
						}
						writer.Write(row.CompletionCount);
						for (int j = 0; j < row.CompletionCount; j++)
						{
							KingdomResourceChange change; row.TryCompletion(j, out change);
							Write(writer, change.ResourceKey); writer.Write(change.Amount);
						}
					}
					writer.Write(state.NetworkCount);
					for (int i = 0; i < state.NetworkCount; i++)
					{
						KingdomExtensionNetworkReading row; state.TryNetwork(i, out row);
						Write(writer, row.Key); Write(writer, row.ResourceKey); writer.Write(row.ProcessedThroughTick);
						writer.Write(row.LastFlowPerDay); writer.Write(row.LastBrownoutPerDay);
					}
					writer.Write(state.WorkCount);
					for (int i = 0; i < state.WorkCount; i++)
					{
						KingdomWorkBehaviourReading row; state.TryWork(i, out row);
						Write(writer, row.BehaviourKey); writer.Write(row.WorkId); writer.Write(row.State);
						writer.Write(row.NextTick); Write(writer, row.OwedBlueprint); writer.Write(row.OwedCount);
						if (wireVersion >= WireVersion) writer.Write(row.MaterialisationSequence);
					}
					writer.Flush();
					if (stream.Length > maximumBytes) return false;
					wire = Convert.ToBase64String(stream.ToArray()); return true;
				}
			}
			catch { wire = ""; return false; }
		}

		internal static bool TryDecode(string wire, out KingdomBehaviourState state)
		{
			state = KingdomBehaviourState.Empty;
			if (string.IsNullOrEmpty(wire)) return true;
			if (wire.Length > ((KingdomApiRules.MaxBehaviourModelBytes + 2) / 3) * 4) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(wire);
				if (bytes.Length > KingdomApiRules.MaxBehaviourModelBytes) return false;
				using (MemoryStream stream = new MemoryStream(bytes, false))
				using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
				{
					if (reader.ReadInt32() != WireMagic) return false;
					int wireVersion = reader.ReadInt32();
					if (wireVersion != LegacyWireVersion && wireVersion != WireVersion) return false;
					if (wireVersion == LegacyWireVersion
						&& bytes.Length > KingdomApiRules.LegacyBehaviourModelBytes) return false;
					int resourceCount = Count(reader, KingdomApiRules.MaxResourceKindsPerCity);
					KingdomResourceReading[] resources = new KingdomResourceReading[resourceCount];
					for (int i = 0; i < resourceCount; i++)
					{
						string key = Read(reader), unit = Read(reader), property = Read(reader);
						string network = Read(reader), liquid = Read(reader);
						long level = reader.ReadInt64(), capacity = reader.ReadInt64();
						if (!ValidStoredResource(key, unit, property, network, liquid, level, capacity)
							|| ResourceIndex(resources, i, key) >= 0) return false;
						resources[i] = new KingdomResourceReading(key, unit, property, network, liquid, level, capacity);
					}
					int jobCount = Count(reader, KingdomApiRules.MaxStoredJobsPerCity);
					KingdomBehaviourJobRow[] jobs = new KingdomBehaviourJobRow[jobCount];
					for (int i = 0; i < jobCount; i++)
					{
						string key = Read(reader), carrier = Read(reader), blueprint = Read(reader);
						int pace = reader.ReadInt32(); string cargo = Read(reader); int amount = reader.ReadInt32();
						long start = reader.ReadInt64(), due = reader.ReadInt64();
						KingdomExtensionJobStatus status = (KingdomExtensionJobStatus)reader.ReadByte();
						int legsCount = Count(reader, KingdomApiRules.MaxLegsPerJob);
						KingdomExtensionLeg[] legs = new KingdomExtensionLeg[legsCount];
						for (int j = 0; j < legsCount; j++) legs[j] = new KingdomExtensionLeg(Read(reader),
							reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
						int changeCount = Count(reader, KingdomApiRules.MaxChangesPerResult);
						KingdomResourceChange[] changes = new KingdomResourceChange[changeCount];
						for (int j = 0; j < changeCount; j++) changes[j] = new KingdomResourceChange(Read(reader), reader.ReadInt64());
						if (!ValidStoredJob(key, carrier, blueprint, pace, cargo, amount, start, due, status,
							legs, changes, resources) || JobIndex(jobs, i, key) >= 0) return false;
						jobs[i] = new KingdomBehaviourJobRow(key, carrier, blueprint, pace, cargo, amount,
							start, due, status, legs, changes);
					}
					if (!ValidStoredJobBounds(jobs)) return false;
					int networkCount = Count(reader, KingdomApiRules.MaxNetworksPerCity);
					KingdomExtensionNetworkReading[] networks = new KingdomExtensionNetworkReading[networkCount];
					for (int i = 0; i < networkCount; i++)
					{
						string key = Read(reader), resource = Read(reader); long tick = reader.ReadInt64();
						int flow = reader.ReadInt32(), brownout = reader.ReadInt32();
						if (!StoredKey(key) || !StoredKey(resource) || ResourceIndex(resources, resource) < 0
							|| tick < 0L || flow < 0 || brownout < 0 || NetworkIndex(networks, i, key) >= 0) return false;
						networks[i] = new KingdomExtensionNetworkReading(key, resource, tick, flow, brownout);
					}
					int workCount = Count(reader, KingdomApiRules.MaxWorkBehavioursPerCity);
					KingdomWorkBehaviourReading[] works = new KingdomWorkBehaviourReading[workCount];
					for (int i = 0; i < workCount; i++)
					{
						string key = Read(reader); int workId = reader.ReadInt32(); long value = reader.ReadInt64();
						long tick = reader.ReadInt64(); string blueprint = Read(reader); int owed = reader.ReadInt32();
						long sequence = wireVersion >= WireVersion ? reader.ReadInt64() : 0L;
						if (!StoredKey(key) || workId <= 0 || tick < 0L || owed < 0 || sequence < 0L
							|| owed > MaxOwedObjectsPerWork || (owed == 0) != string.IsNullOrEmpty(blueprint)
							|| (owed > 0 && KingdomApiRules.BehaviourIdentifier(blueprint, true) == null)
							|| WorkIndex(works, i, key, workId) >= 0) return false;
						works[i] = new KingdomWorkBehaviourReading(key, workId, value, tick, blueprint, owed,
							sequence);
					}
					if (stream.Position != stream.Length) return false;
					state = new KingdomBehaviourState(resources, jobs, networks, works); return true;
				}
			}
			catch { state = KingdomBehaviourState.Empty; return false; }
		}

		/// <summary>Stages the API-v3 standing clocks for one atomic realm-master resume. Network
		/// production starts again at <paramref name="nowTick"/>. A future work breakpoint keeps its
		/// exact remaining duration; work already due at disable remains due. Host-owned jobs and
		/// materialisation debt are committed recovery and remain byte-for-byte in the decoded model.</summary>
		internal static bool TryRebaseAfterPause(string wire, long disabledAtTick, long nowTick,
			out string replacement)
		{
			replacement = wire ?? "";
			if (disabledAtTick < 0L || nowTick < disabledAtTick) return false;
			KingdomBehaviourState state;
			if (!TryDecode(wire, out state)) return false;
			if (string.IsNullOrEmpty(wire)) return true;

			KingdomExtensionNetworkReading[] networks = state.Networks();
			for (int i = 0; i < networks.Length; i++)
			{
				KingdomExtensionNetworkReading row = networks[i];
				if (row.ProcessedThroughTick > disabledAtTick) return false;
				networks[i] = new KingdomExtensionNetworkReading(row.Key, row.ResourceKey, nowTick,
					row.LastFlowPerDay, row.LastBrownoutPerDay);
			}

			long paused = nowTick - disabledAtTick;
			KingdomWorkBehaviourReading[] works = state.Works();
			for (int i = 0; i < works.Length; i++)
			{
				KingdomWorkBehaviourReading row = works[i];
				long nextTick = row.NextTick;
				if (nextTick > disabledAtTick)
				{
					if (nextTick > long.MaxValue - paused) return false;
					nextTick += paused;
				}
				works[i] = new KingdomWorkBehaviourReading(row.BehaviourKey, row.WorkId,
					row.State, nextTick, row.OwedBlueprint, row.OwedCount,
					row.MaterialisationSequence);
			}

			KingdomBehaviourState rebased = new KingdomBehaviourState(state.Resources(), state.Jobs(),
				networks, works);
			return TryEncode(rebased, out replacement);
		}

		private static bool TryLegs(KingdomJobPlan plan, KingdomCityReading city, int pace,
			long start, out KingdomExtensionLeg[] legs, out long due)
		{
			legs = new KingdomExtensionLeg[plan.LegCount]; due = start;
			for (int i = 0; i < legs.Length; i++)
			{
				KingdomExtensionLeg leg;
				if (!plan.TryLeg(i, out leg) || !Held(city, leg.ZoneId)
					|| leg.EnterX < 0 || leg.EnterX >= 80 || leg.ExitX < 0 || leg.ExitX >= 80
					|| leg.EnterY < 0 || leg.EnterY >= 25 || leg.ExitY < 0 || leg.ExitY >= 25) return false;
				int dx = Math.Abs((int)leg.ExitX - leg.EnterX), dy = Math.Abs((int)leg.ExitY - leg.EnterY);
				int cells = Math.Max(dx, dy); if (cells <= 0) cells = 1;
				long cost = (long)cells * pace;
				if (cost <= 0L || due > long.MaxValue - cost) return false;
				due += cost; legs[i] = leg;
			}
			return due > start;
		}

		private static bool TryNormalizeChanges(string owner, KingdomJobPlan plan,
			out KingdomResourceChange[] changes)
		{
			changes = new KingdomResourceChange[plan.CompletionChangeCount];
			List<string> seen = new List<string>();
			for (int i = 0; i < changes.Length; i++)
			{
				KingdomResourceChange change;
				if (!plan.TryCompletionChange(i, out change)) return false;
				string key = KingdomApiRules.ExtensionKey(owner, change.ResourceKey);
				if (key == null || change.Amount == 0L || seen.Contains(key)) return false;
				seen.Add(key); changes[i] = new KingdomResourceChange(key, change.Amount);
			}
			return true;
		}

		private static bool TryApplyOwnedChanges(KingdomResourceReading[] source, string owner,
			KingdomResourceChange[] changes, out KingdomResourceReading[] next)
		{
			next = Copy(source);
			if (changes == null || changes.Length > KingdomApiRules.MaxChangesPerResult) return false;
			List<string> seen = new List<string>();
			for (int i = 0; i < changes.Length; i++)
			{
				string key = KingdomApiRules.ExtensionKey(owner, changes[i].ResourceKey);
				int at = ResourceIndex(next, key);
				if (key == null || at < 0 || changes[i].Amount == 0L || seen.Contains(key)) return false;
				seen.Add(key);
				long amount = changes[i].Amount;
				if ((amount > 0L && next[at].Level > next[at].Capacity - amount)
					|| (amount < 0L && (amount == long.MinValue || next[at].Level < -amount))) return false;
				next[at] = WithLevel(next[at], next[at].Level + amount);
			}
			return true;
		}

		private static bool TrySolve(KingdomNetworkPlan plan, KingdomCityReading city,
			out int flow, out int brownout, out int totalSupply)
		{
			flow = 0; brownout = 0; totalSupply = 0;
			int n = plan.NodeCount, e = plan.EdgeCount;
			KingdomExtensionNetworkNode[] nodes = new KingdomExtensionNetworkNode[n];
			KingdomExtensionNetworkEdge[] edges = new KingdomExtensionNetworkEdge[e];
			List<string> nodeKeys = new List<string>();
			int totalDemand = 0;
			for (int i = 0; i < n; i++)
			{
				if (!plan.TryNode(i, out nodes[i]) || !Held(city, nodes[i].ZoneId)
					|| KingdomApiRules.BehaviourIdentifier(nodes[i].Key, true) == null
					|| nodeKeys.Contains(nodes[i].Key) || nodes[i].RatePerDay < 0
					|| !Enum.IsDefined(typeof(KingdomExtensionNetworkRole), nodes[i].Role)) return false;
				nodeKeys.Add(nodes[i].Key);
				if (nodes[i].Role == KingdomExtensionNetworkRole.Relay && nodes[i].RatePerDay != 0) return false;
				if (nodes[i].Role == KingdomExtensionNetworkRole.Source)
				{
					if (totalSupply > int.MaxValue - nodes[i].RatePerDay) return false;
					totalSupply += nodes[i].RatePerDay;
				}
				else if (nodes[i].Role == KingdomExtensionNetworkRole.Sink)
				{
					if (totalDemand > int.MaxValue - nodes[i].RatePerDay) return false;
					totalDemand += nodes[i].RatePerDay;
				}
			}
			for (int i = 0; i < e; i++)
			{
				if (!plan.TryEdge(i, out edges[i]) || edges[i].A < 0 || edges[i].A >= n
					|| edges[i].B < 0 || edges[i].B >= n || edges[i].A == edges[i].B
					|| edges[i].CapacityPerDay <= 0) return false;
			}
			int[] sourceRemaining = new int[n];
			for (int i = 0; i < n; i++) if (nodes[i].Role == KingdomExtensionNetworkRole.Source)
				sourceRemaining[i] = nodes[i].RatePerDay;
			int[] edgeRemaining = new int[e];
			for (int i = 0; i < e; i++) edgeRemaining[i] = edges[i].CapacityPerDay;
			int[] sinks = new int[n]; int sinkCount = 0;
			for (int i = 0; i < n; i++) if (nodes[i].Role == KingdomExtensionNetworkRole.Sink) sinks[sinkCount++] = i;
			for (int i = 0; i < sinkCount; i++)
				for (int j = i + 1; j < sinkCount; j++)
					if (nodes[sinks[j]].Priority < nodes[sinks[i]].Priority
						|| (nodes[sinks[j]].Priority == nodes[sinks[i]].Priority && sinks[j] < sinks[i]))
					{ int swap = sinks[i]; sinks[i] = sinks[j]; sinks[j] = swap; }
			for (int s = 0; s < sinkCount; s++)
			{
				int demand = nodes[sinks[s]].RatePerDay;
				while (demand > 0)
				{
					int source, bottleneck; int[] path;
					if (!TryPath(sinks[s], nodes, edges, edgeRemaining, sourceRemaining,
						out source, out path, out bottleneck)) break;
					int sent = Math.Min(demand, Math.Min(sourceRemaining[source], bottleneck));
					if (sent <= 0) break;
					sourceRemaining[source] -= sent; demand -= sent; flow += sent;
					for (int p = 0; p < path.Length; p++) edgeRemaining[path[p]] -= sent;
				}
				brownout += demand;
			}
			return flow <= totalSupply && brownout == totalDemand - flow;
		}

		private static bool TryPath(int sink, KingdomExtensionNetworkNode[] nodes,
			KingdomExtensionNetworkEdge[] edges, int[] edgeRemaining, int[] sourceRemaining,
			out int source, out int[] path, out int bottleneck)
		{
			source = -1; path = new int[0]; bottleneck = 0;
			int n = nodes.Length; int[] parentNode = new int[n]; int[] parentEdge = new int[n];
			for (int i = 0; i < n; i++) { parentNode[i] = -2; parentEdge[i] = -1; }
			int[] queue = new int[n]; int head = 0, tail = 0; queue[tail++] = sink; parentNode[sink] = -1;
			while (head < tail && source < 0)
			{
				int here = queue[head++];
				if (nodes[here].Role == KingdomExtensionNetworkRole.Source && sourceRemaining[here] > 0)
				{ source = here; break; }
				for (int i = 0; i < edges.Length; i++)
				{
					if (edgeRemaining[i] <= 0) continue;
					int there = edges[i].A == here ? edges[i].B : (edges[i].B == here ? edges[i].A : -1);
					if (there < 0 || parentNode[there] != -2) continue;
					parentNode[there] = here; parentEdge[there] = i; queue[tail++] = there;
				}
			}
			if (source < 0) return false;
			List<int> found = new List<int>(); bottleneck = int.MaxValue;
			for (int at = source; at != sink; at = parentNode[at])
			{
				int edge = parentEdge[at]; if (edge < 0) return false;
				found.Add(edge); if (edgeRemaining[edge] < bottleneck) bottleneck = edgeRemaining[edge];
			}
			path = found.ToArray(); return path.Length > 0 && bottleneck > 0;
		}

		private static KingdomResourceChange[] JobChanges(KingdomBehaviourJobRow row)
		{
			KingdomResourceChange[] result = new KingdomResourceChange[row.CompletionCount];
			for (int i = 0; i < result.Length; i++) row.TryCompletion(i, out result[i]);
			return result;
		}

		private static KingdomResourceChange[] WorkChanges(KingdomWorkAdvance result)
		{
			KingdomResourceChange[] changes = new KingdomResourceChange[result.ChangeCount];
			for (int i = 0; i < changes.Length; i++) result.TryChange(i, out changes[i]);
			return changes;
		}

		private static bool HasWork(KingdomCityReading city, int workId)
		{
			for (int i = 0; i < city.WorkCount; i++)
			{ KingdomWorkReading row; if (city.TryWork(i, out row) && row.WorkId == workId) return true; }
			return false;
		}

		private static bool Held(KingdomCityReading city, string zoneId)
		{
			if (city == null || string.IsNullOrEmpty(zoneId)) return false;
			for (int i = 0; i < city.ZoneCount; i++)
			{ KingdomZoneReading row; if (city.TryZone(i, out row) && row.ZoneId == zoneId) return true; }
			return false;
		}

		private static KingdomResourceReading WithLevel(KingdomResourceReading row, long level)
		{
			return new KingdomResourceReading(row.Key, row.Unit, row.ContainerProperty,
				row.NetworkKey, row.LiquidId, level, row.Capacity);
		}

		private static int ResourceIndex(KingdomResourceReading[] rows, string key)
		{ return ResourceIndex(rows, rows == null ? 0 : rows.Length, key); }

		private static int ResourceIndex(KingdomResourceReading[] rows, int count, string key)
		{
			for (int i = 0; rows != null && i < count && i < rows.Length; i++) if (rows[i].Key == key) return i;
			return -1;
		}

		private static int JobIndex(KingdomBehaviourJobRow[] rows, string key)
		{ return JobIndex(rows, rows == null ? 0 : rows.Length, key); }

		private static int JobIndex(KingdomBehaviourJobRow[] rows, int count, string key)
		{
			for (int i = 0; rows != null && i < count && i < rows.Length; i++) if (rows[i] != null && rows[i].Key == key) return i;
			return -1;
		}

		private static int NetworkIndex(KingdomExtensionNetworkReading[] rows, string key)
		{ return NetworkIndex(rows, rows == null ? 0 : rows.Length, key); }

		private static int NetworkIndex(KingdomExtensionNetworkReading[] rows, int count, string key)
		{
			for (int i = 0; rows != null && i < count && i < rows.Length; i++) if (rows[i].Key == key) return i;
			return -1;
		}

		private static int WorkIndex(KingdomWorkBehaviourReading[] rows, string key, int workId)
		{ return WorkIndex(rows, rows == null ? 0 : rows.Length, key, workId); }

		private static int WorkIndex(KingdomWorkBehaviourReading[] rows, int count, string key, int workId)
		{
			for (int i = 0; rows != null && i < count && i < rows.Length; i++)
				if (rows[i].BehaviourKey == key && rows[i].WorkId == workId) return i;
			return -1;
		}

		private static int CarrierIndex(List<KingdomCarrierKindRow> rows, string key)
		{
			for (int i = 0; i < rows.Count; i++) if (rows[i].Key == key) return i; return -1;
		}

		private static int CarrierIndex(KingdomCarrierKindRow[] rows, string key)
		{
			for (int i = 0; rows != null && i < rows.Length; i++) if (rows[i].Key == key) return i; return -1;
		}

		private static string OwnerOf(string key)
		{
			int colon = string.IsNullOrEmpty(key) ? -1 : key.IndexOf(':');
			return colon <= 0 ? "" : key.Substring(0, colon);
		}

		private static bool Owned(string key, string owner)
		{
			return OwnerOf(key) == KingdomApiRules.Slug(owner);
		}

		private static int CountOwner(KingdomResourceReading[] rows, string owner)
		{ int count = 0; for (int i = 0; rows != null && i < rows.Length; i++) if (Owned(rows[i].Key, owner)) count++; return count; }
		private static int CountOpenOwner(KingdomBehaviourJobRow[] rows, string owner)
		{
			int count = 0;
			for (int i = 0; rows != null && i < rows.Length; i++)
				if (rows[i] != null && rows[i].Status == KingdomExtensionJobStatus.Open
					&& Owned(rows[i].Key, owner)) count++;
			return count;
		}

		private static int CountOpen(KingdomBehaviourJobRow[] rows)
		{
			int count = 0;
			for (int i = 0; rows != null && i < rows.Length; i++)
				if (rows[i] != null && rows[i].Status == KingdomExtensionJobStatus.Open) count++;
			return count;
		}
		private static int CountOwner(KingdomExtensionNetworkReading[] rows, string owner)
		{ int count = 0; for (int i = 0; rows != null && i < rows.Length; i++) if (Owned(rows[i].Key, owner)) count++; return count; }
		private static int CountOwner(KingdomWorkBehaviourReading[] rows, string owner)
		{ int count = 0; for (int i = 0; rows != null && i < rows.Length; i++) if (Owned(rows[i].BehaviourKey, owner)) count++; return count; }

		/// <summary>Keeps every open job and the newest bounded terminal receipts, preserving the
		/// original deterministic row order. A retained key deduplicates retries; after retirement,
		/// the extension contract forbids recycling that logical-job identity.</summary>
		private static KingdomBehaviourJobRow[] TrimTerminalJobs(KingdomBehaviourJobRow[] rows)
		{
			if (rows == null || rows.Length == 0) return new KingdomBehaviourJobRow[0];
			bool[] keep = new bool[rows.Length];
			Dictionary<string, int> owned = new Dictionary<string, int>(StringComparer.Ordinal);
			int terminal = 0;
			for (int i = rows.Length - 1; i >= 0; i--)
			{
				KingdomBehaviourJobRow row = rows[i];
				if (row == null) continue;
				if (row.Status == KingdomExtensionJobStatus.Open)
				{
					keep[i] = true;
					continue;
				}
				string owner = OwnerOf(row.Key);
				int count;
				owned.TryGetValue(owner, out count);
				if (terminal >= KingdomApiRules.MaxTerminalJobReceiptsPerCity
					|| count >= KingdomApiRules.MaxTerminalJobReceiptsPerOwner) continue;
				keep[i] = true;
				terminal++;
				owned[owner] = count + 1;
			}
			int total = 0;
			for (int i = 0; i < keep.Length; i++) if (keep[i]) total++;
			if (total == rows.Length) return rows;
			KingdomBehaviourJobRow[] result = new KingdomBehaviourJobRow[total];
			for (int i = 0, at = 0; i < rows.Length; i++) if (keep[i]) result[at++] = rows[i];
			return result;
		}

		private static bool ValidStoredJobBounds(KingdomBehaviourJobRow[] rows)
		{
			if (rows == null || rows.Length > KingdomApiRules.MaxStoredJobsPerCity) return false;
			Dictionary<string, int> openByOwner = new Dictionary<string, int>(StringComparer.Ordinal);
			Dictionary<string, int> terminalByOwner = new Dictionary<string, int>(StringComparer.Ordinal);
			int open = 0, terminal = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				KingdomBehaviourJobRow row = rows[i];
				if (row == null) return false;
				string owner = OwnerOf(row.Key);
				Dictionary<string, int> counts = row.Status == KingdomExtensionJobStatus.Open
					? openByOwner : terminalByOwner;
				int count;
				counts.TryGetValue(owner, out count);
				counts[owner] = ++count;
				if (row.Status == KingdomExtensionJobStatus.Open)
				{
					open++;
					if (open > KingdomApiRules.MaxJobsPerCity
						|| count > KingdomApiRules.MaxJobsPerOwner) return false;
				}
				else
				{
					terminal++;
					if (terminal > KingdomApiRules.MaxTerminalJobReceiptsPerCity
						|| count > KingdomApiRules.MaxTerminalJobReceiptsPerOwner) return false;
				}
			}
			return true;
		}

		private static T[] Append<T>(T[] source, T row)
		{
			int count = source == null ? 0 : source.Length; T[] next = new T[count + 1];
			if (count > 0) Array.Copy(source, next, count); next[count] = row; return next;
		}

		private static T[] Copy<T>(T[] source)
		{
			if (source == null || source.Length == 0) return new T[0];
			T[] next = new T[source.Length]; Array.Copy(source, next, source.Length); return next;
		}

		private static void Write(BinaryWriter writer, string value)
		{ writer.Write(value ?? ""); }

		private static string Read(BinaryReader reader)
		{
			string value = reader.ReadString();
			if (value.Length > KingdomApiRules.MaxBehaviourIdentifierLength) throw new InvalidDataException();
			return value;
		}

		private static int Count(BinaryReader reader, int maximum)
		{
			int count = reader.ReadInt32(); if (count < 0 || count > maximum) throw new InvalidDataException(); return count;
		}

		private static bool StoredKey(string key)
		{
			if (string.IsNullOrEmpty(key) || key.Length > KingdomApiRules.MaxBehaviourIdentifierLength) return false;
			int colon = key.IndexOf(':'); return colon > 0 && colon == key.LastIndexOf(':') && colon < key.Length - 1
				&& KingdomApiRules.ExtensionKey(key.Substring(0, colon), key) == key;
		}

		private static bool ValidStoredResource(string key, string unit, string property, string network,
			string liquid, long level, long capacity)
		{
			return StoredKey(key) && KingdomApiRules.BehaviourIdentifier(unit, true) != null
				&& KingdomApiRules.BehaviourIdentifier(property, false) != null
				&& KingdomApiRules.BehaviourIdentifier(liquid, false) != null
				&& (string.IsNullOrEmpty(network) || StoredKey(network))
				&& capacity >= 0L && capacity <= MaxResourceQuantity && level >= 0L && level <= capacity;
		}

		private static bool ValidStoredJob(string key, string carrier, string blueprint, int pace,
			string cargo, int amount, long start, long due, KingdomExtensionJobStatus status,
			KingdomExtensionLeg[] legs, KingdomResourceChange[] changes, KingdomResourceReading[] resources)
		{
			if (!StoredKey(key) || !StoredKey(carrier) || !StoredKey(cargo)
				|| KingdomApiRules.BehaviourIdentifier(blueprint, true) == null || pace <= 0
				|| amount <= 0 || start < 0L || due <= start || !Enum.IsDefined(typeof(KingdomExtensionJobStatus), status)
				|| legs == null || legs.Length <= 0 || ResourceIndex(resources, cargo) < 0) return false;
			for (int i = 0; i < legs.Length; i++)
				if (KingdomApiRules.BehaviourIdentifier(legs[i].ZoneId, true) == null
					|| legs[i].EnterX < 0 || legs[i].EnterX >= 80 || legs[i].ExitX < 0 || legs[i].ExitX >= 80
					|| legs[i].EnterY < 0 || legs[i].EnterY >= 25 || legs[i].ExitY < 0 || legs[i].ExitY >= 25) return false;
			string owner = OwnerOf(key); List<string> seen = new List<string>();
			for (int i = 0; changes != null && i < changes.Length; i++)
				if (!StoredKey(changes[i].ResourceKey) || OwnerOf(changes[i].ResourceKey) != owner
					|| ResourceIndex(resources, changes[i].ResourceKey) < 0 || changes[i].Amount == 0L
					|| seen.Contains(changes[i].ResourceKey)) return false;
				else seen.Add(changes[i].ResourceKey);
			return true;
		}
	}
}
