using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	internal static partial class KingdomBehaviourRules
	{
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

	}
}
