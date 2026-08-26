using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Api
{
	/// <summary>Bounded durable per-source cursors for the compatible v1 happening contract.
	/// Source identity is the SHA-256 digest of immutable manifest ID plus exact assembly/type;
	/// titles, registration order, and another source's schedule cannot move its window.</summary>
	internal static class KingdomHappeningCursorRules
	{
		private const int Magic = 0x31434854; // THC1, little-endian
		private const int Version = 1;
		private const int DigestBytes = 32;
		private const int DigestChars = DigestBytes * 2;
		internal const int MaxSources = 128;
		internal const int MaxBytes = 12 + MaxSources * (DigestBytes + sizeof(long));
		internal const int MaxChars = ((MaxBytes + 2) / 3) * 4;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		/// <summary>Derives one collision-resistant source identity without lossy slugging.</summary>
		internal static bool TrySourceKey(string owner, string assemblyName, string typeName,
			out string key)
		{
			key = "";
			if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(assemblyName)
				|| string.IsNullOrEmpty(typeName)) return false;
			try
			{
				byte[] preimage;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write("taf:happening-source:v1");
					writer.Write(owner);
					writer.Write(assemblyName);
					writer.Write(typeName);
					writer.Flush();
					preimage = stream.ToArray();
				}
				byte[] digest;
				using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(preimage);
				StringBuilder text = new StringBuilder(DigestChars);
				for (int i = 0; i < digest.Length; i++)
					text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
				key = text.ToString();
				return key.Length == DigestChars;
			}
			catch
			{
				key = "";
				return false;
			}
		}

		/// <summary>Seeds exact active sources from the retired city-wide receipt. Called only when
		/// the new wire is absent and the legacy receipt proves this city already ran the published
		/// lane; this prevents an upgrade from replaying every old source from tick zero.</summary>
		internal static bool TrySeedLegacy(IList<string> sourceKeys, long legacyTick,
			out string replacement)
		{
			replacement = "";
			if (sourceKeys == null || sourceKeys.Count > MaxSources || legacyTick <= 0L)
				return false;
			Dictionary<string, long> rows =
				new Dictionary<string, long>(StringComparer.Ordinal);
			for (int i = 0; i < sourceKeys.Count; i++)
			{
				if (!ValidKey(sourceKeys[i]) || rows.ContainsKey(sourceKeys[i])) return false;
				rows.Add(sourceKeys[i], legacyTick);
			}
			return TryEncode(rows, out replacement);
		}

		/// <summary>Retains only currently active source receipts before one pass. This bounds mod
		/// churn and makes a removed-then-reinstalled source's next call an explicit first call.</summary>
		internal static bool TryRetain(string wire, IList<string> activeKeys, out string replacement)
		{
			replacement = wire ?? "";
			Dictionary<string, long> current;
			if (!TryDecode(wire, out current) || activeKeys == null
				|| activeKeys.Count > MaxSources) return false;
			HashSet<string> active = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < activeKeys.Count; i++)
				if (!ValidKey(activeKeys[i]) || !active.Add(activeKeys[i])) return false;
			Dictionary<string, long> retained = new Dictionary<string, long>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, long> row in current)
				if (active.Contains(row.Key)) retained.Add(row.Key, row.Value);
			return TryEncode(retained, out replacement);
		}

		/// <summary>Returns this source's last ask (zero when absent) and prepares its current-tick
		/// receipt. Caller publishes <paramref name="replacement"/> before invoking third-party code,
		/// preserving the documented advance-on-fault policy.</summary>
		internal static bool TryAdvance(string wire, string sourceKey, long nowTick,
			out long sinceTick, out string replacement)
		{
			sinceTick = 0L;
			replacement = wire ?? "";
			if (!ValidKey(sourceKey) || nowTick <= 0L) return false;
			Dictionary<string, long> rows;
			if (!TryDecode(wire, out rows)) return false;
			long found;
			if (rows.TryGetValue(sourceKey, out found))
			{
				if (found <= 0L || found > nowTick) return false;
				sinceTick = found;
			}
			else if (rows.Count >= MaxSources)
			{
				return false;
			}
			rows[sourceKey] = nowTick;
			return TryEncode(rows, out replacement);
		}

		/// <summary>Stages every existing source receipt at the master-resume boundary. A receipt
		/// newer than the disable boundary proves that automatic work crossed the pause and refuses the
		/// whole resume plan; empty remains the first-call state.</summary>
		internal static bool TryRebaseAfterPause(string wire, long disabledAtTick, long nowTick,
			out string replacement)
		{
			replacement = wire ?? "";
			if (disabledAtTick < 0L || nowTick < disabledAtTick) return false;
			Dictionary<string, long> rows;
			if (!TryDecode(wire, out rows)) return false;
			if (rows.Count == 0) return true;
			if (nowTick <= 0L) return false;
			List<string> keys = new List<string>(rows.Keys);
			for (int i = 0; i < keys.Count; i++)
			{
				if (rows[keys[i]] > disabledAtTick) return false;
				rows[keys[i]] = nowTick;
			}
			return TryEncode(rows, out replacement);
		}

		private static bool TryDecode(string wire, out Dictionary<string, long> rows)
		{
			rows = new Dictionary<string, long>(StringComparer.Ordinal);
			if (string.IsNullOrEmpty(wire)) return true;
			if (wire.Length > MaxChars) return false;
			try
			{
				byte[] payload = Convert.FromBase64String(wire);
				if (payload.Length < 12 || payload.Length > MaxBytes
					|| Convert.ToBase64String(payload) != wire) return false;
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) return false;
					int count = reader.ReadInt32();
					if (count < 0 || count > MaxSources
						|| payload.Length != 12 + count * (DigestBytes + sizeof(long))) return false;
					string previous = null;
					for (int i = 0; i < count; i++)
					{
						byte[] digest = reader.ReadBytes(DigestBytes);
						if (digest.Length != DigestBytes) return false;
						StringBuilder text = new StringBuilder(DigestChars);
						for (int j = 0; j < digest.Length; j++)
							text.Append(digest[j].ToString("x2", CultureInfo.InvariantCulture));
						string key = text.ToString();
						long tick = reader.ReadInt64();
						if (tick <= 0L || (previous != null
							&& string.CompareOrdinal(previous, key) >= 0)) return false;
						rows.Add(key, tick);
						previous = key;
					}
					return stream.Position == stream.Length;
				}
			}
			catch
			{
				rows = new Dictionary<string, long>(StringComparer.Ordinal);
				return false;
			}
		}

		private static bool TryEncode(Dictionary<string, long> rows, out string wire)
		{
			wire = "";
			if (rows == null || rows.Count > MaxSources) return false;
			try
			{
				List<string> keys = new List<string>(rows.Keys);
				keys.Sort(StringComparer.Ordinal);
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(Version);
					writer.Write(keys.Count);
					for (int i = 0; i < keys.Count; i++)
					{
						string key = keys[i];
						long tick = rows[key];
						if (!ValidKey(key) || tick <= 0L) return false;
						for (int at = 0; at < key.Length; at += 2)
							writer.Write(byte.Parse(key.Substring(at, 2), NumberStyles.HexNumber,
								CultureInfo.InvariantCulture));
						writer.Write(tick);
					}
					writer.Flush();
					byte[] payload = stream.ToArray();
					if (payload.Length > MaxBytes) return false;
					wire = Convert.ToBase64String(payload);
					return wire.Length <= MaxChars;
				}
			}
			catch
			{
				wire = "";
				return false;
			}
		}

		private static bool ValidKey(string key)
		{
			if (key == null || key.Length != DigestChars) return false;
			for (int i = 0; i < key.Length; i++)
				if (!((key[i] >= '0' && key[i] <= '9') || (key[i] >= 'a' && key[i] <= 'f')))
					return false;
			return true;
		}
	}
}
