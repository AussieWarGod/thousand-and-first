using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	internal static partial class KingdomBehaviourRules
	{
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
