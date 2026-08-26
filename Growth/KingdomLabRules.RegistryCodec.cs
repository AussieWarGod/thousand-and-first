using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLabRules
	{
		internal static List<KingdomLabRegistryEntry> ParseRegistry(string Text,
			out bool Quarantined)
		{
			List<KingdomLabRegistryEntry> rows = new List<KingdomLabRegistryEntry>();
			Quarantined = false;
			if (string.IsNullOrEmpty(Text))
			{
				return rows;
			}
			string[] lines = Text.Split('\n');
			if (lines.Length == 0 || lines[0] != "v1")
			{
				Quarantined = true;
				return rows;
			}
			for (int i = 1; i < lines.Length; i++)
			{
				if (string.IsNullOrEmpty(lines[i])) continue;
				if (rows.Count >= MaxRegistryRows)
				{
					Quarantined = true;
					break;
				}
				string[] fields = lines[i].Split('|');
				long founded;
				long updated;
				int status;
				string job;
				string building;
				string patient;
				string game;
				string realm;
				string key;
				string grants;
				string manager;
				string detail;
				string fingerprint;
				int version;
				int source;
				int attach;
				if (fields.Length != 16 || !Decode(fields[0], out job)
					|| !Decode(fields[1], out building) || !Decode(fields[2], out patient)
					|| !Decode(fields[3], out game) || !Decode(fields[4], out realm)
					|| !long.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture,
						out founded)
					|| !int.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture,
						out version) || !Decode(fields[7], out key) || !Decode(fields[8], out grants)
					|| !int.TryParse(fields[9], NumberStyles.Integer, CultureInfo.InvariantCulture,
						out source) || !int.TryParse(fields[10], NumberStyles.Integer,
						CultureInfo.InvariantCulture, out attach) || !Decode(fields[11], out manager)
					|| !Decode(fields[12], out detail) || !Decode(fields[13], out fingerprint)
					|| !int.TryParse(fields[14], NumberStyles.None, CultureInfo.InvariantCulture,
					out status) || !Enum.IsDefined(typeof(KingdomLabRegistryStatus),
						(KingdomLabRegistryStatus)status)
					|| !long.TryParse(fields[15], NumberStyles.Integer, CultureInfo.InvariantCulture,
						out updated))
				{
					Quarantined = true;
					continue;
				}
				KingdomLabRegistryEntry row = new KingdomLabRegistryEntry
				{
					JobId = job,
					BuildingId = building,
					PatientId = patient,
					GameId = game,
					RealmId = realm,
					RealmFoundedTick = founded,
					ContractVersion = version,
					ProcedureKey = key,
					Grants = grants,
					Source = source,
					Attach = attach,
					Manager = manager,
					Detail = detail,
					Fingerprint = fingerprint,
					Status = (KingdomLabRegistryStatus)status,
					UpdatedTick = updated
				};
				if (!ValidRegistryEntry(row) || IndexOfRegistry(rows, row.JobId) >= 0)
				{
					Quarantined = true;
					continue;
				}
				rows.Add(row);
			}
			return rows;
		}

		internal static string FormatRegistry(IList<KingdomLabRegistryEntry> Rows)
		{
			StringBuilder text = new StringBuilder("v1");
			int count = Math.Min(Rows?.Count ?? 0, MaxRegistryRows);
			for (int i = 0; i < count; i++)
			{
				KingdomLabRegistryEntry row = Rows[i];
				if (!ValidRegistryEntry(row)) continue;
				text.Append('\n').Append(Encode(row.JobId)).Append('|')
					.Append(Encode(row.BuildingId)).Append('|')
					.Append(Encode(row.PatientId)).Append('|')
					.Append(Encode(row.GameId)).Append('|')
					.Append(Encode(row.RealmId)).Append('|')
					.Append(row.RealmFoundedTick.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.ContractVersion.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(Encode(row.ProcedureKey)).Append('|')
					.Append(Encode(row.Grants)).Append('|')
					.Append(row.Source.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.Attach.ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(Encode(row.Manager)).Append('|')
					.Append(Encode(row.Detail)).Append('|')
					.Append(Encode(row.Fingerprint)).Append('|')
					.Append(((int)row.Status).ToString(CultureInfo.InvariantCulture)).Append('|')
					.Append(row.UpdatedTick.ToString(CultureInfo.InvariantCulture));
			}
			return text.ToString();
		}

		internal static bool UpsertRegistry(List<KingdomLabRegistryEntry> Rows,
			KingdomLabRegistryEntry Entry)
		{
			if (Rows == null || !ValidRegistryEntry(Entry)) return false;
			int at = IndexOfRegistry(Rows, Entry.JobId);
			if (at >= 0)
			{
				KingdomLabRegistryEntry existing = Rows[at];
				if (!RegistryAuthority(existing, Entry, RequireActive: false))
				{
					return false;
				}
				KingdomLabRegistryEntry replacement = Entry.Copy();
				replacement.UpdatedTick = Math.Max(existing.UpdatedTick, replacement.UpdatedTick);
				Rows[at] = replacement;
				return true;
			}
			if (Rows.Count >= MaxRegistryRows)
			{
				// A terminal label alone never proves its physical receipt and every outbox
				// were cleaned. Callers explicitly remove only after recording replay proof.
				return false;
			}
			Rows.Add(Entry.Copy());
			return true;
		}

		internal static bool RemoveRegistry(List<KingdomLabRegistryEntry> Rows,
			string JobId, KingdomLabRegistryStatus ExpectedStatus)
		{
			int at = IndexOfRegistry(Rows, JobId);
			if (at < 0 || Rows[at] == null || Rows[at].Status != ExpectedStatus
				|| ExpectedStatus == KingdomLabRegistryStatus.Active) return false;
			Rows.RemoveAt(at);
			return IndexOfRegistry(Rows, JobId) < 0;
		}

		internal static bool ReplayContains(string Text, string StableId, out bool Malformed)
		{
			byte[] bits;
			long count;
			Malformed = !TryParseReplay(Text, out bits, out count);
			if (Malformed || string.IsNullOrEmpty(StableId)) return true;
			for (int salt = 0; salt < 4; salt++)
			{
				int bit = ReplayBit(StableId, salt);
				if ((bits[bit >> 3] & (1 << (bit & 7))) == 0) return false;
			}
			return true;
		}

		internal static bool AddReplayProof(string Text, string StableId,
			out string Written)
		{
			Written = null;
			byte[] bits;
			long count;
			if (!TryParseReplay(Text, out bits, out count) || string.IsNullOrEmpty(StableId)
				|| StableId.Length > 256) return false;
			bool present = true;
			for (int salt = 0; salt < 4; salt++)
			{
				int bit = ReplayBit(StableId, salt);
				byte mask = (byte)(1 << (bit & 7));
				if ((bits[bit >> 3] & mask) == 0) present = false;
				bits[bit >> 3] |= mask;
			}
			if (!present && count < long.MaxValue) count++;
			Written = "v1|" + count.ToString(CultureInfo.InvariantCulture) + "|"
				+ Convert.ToBase64String(bits);
			return true;
		}

		private static bool TryParseReplay(string Text, out byte[] Bits, out long Count)
		{
			Bits = new byte[ReplayProofBytes];
			Count = 0L;
			if (string.IsNullOrEmpty(Text)) return true;
			string[] fields = Text.Split('|');
			if (fields.Length != 3 || fields[0] != "v1"
				|| !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture,
					out Count) || Count < 0L) return false;
			try
			{
				byte[] parsed = Convert.FromBase64String(fields[2]);
				if (parsed.Length != ReplayProofBytes) return false;
				Bits = parsed;
				return true;
			}
			catch { return false; }
		}

		private static int ReplayBit(string StableId, int Salt)
		{
			ulong hash = 14695981039346656037UL;
			Fold(ref hash, Salt.ToString(CultureInfo.InvariantCulture));
			Fold(ref hash, StableId ?? "");
			return (int)(hash % (ulong)(ReplayProofBytes * 8));
		}

		internal static int IndexOfRegistry(IList<KingdomLabRegistryEntry> Rows, string JobId)
		{
			for (int i = 0; Rows != null && i < Rows.Count; i++)
			{
				if (string.Equals(Rows[i]?.JobId, JobId, StringComparison.Ordinal)) return i;
			}
			return -1;
		}

		private static bool ValidRegistryEntry(KingdomLabRegistryEntry Entry)
		{
			return Entry != null && Bounded(Entry.JobId, 128) && Bounded(Entry.BuildingId, 128)
				&& Bounded(Entry.PatientId, 128) && Bounded(Entry.GameId, 256)
				&& Bounded(Entry.RealmId, 256) && Bounded(Entry.Fingerprint, 32)
				&& ValidEffectContract(Entry.ContractVersion, Entry.ProcedureKey, Entry.Grants,
					Entry.Source, Entry.Attach, Entry.Manager, Entry.Fingerprint, Entry.Detail)
				&& Entry.RealmFoundedTick >= 0L && Entry.UpdatedTick >= 0L
				&& Enum.IsDefined(typeof(KingdomLabRegistryStatus), Entry.Status);
		}

		private static bool Bounded(string Text, int Maximum)
		{
			return !string.IsNullOrEmpty(Text) && Text.Length <= Maximum;
		}

		private static void Fold(ref ulong Hash, string Text)
		{
			string value = Text ?? "";
			for (int i = 0; i < value.Length; i++)
			{
				Hash ^= value[i];
				Hash *= 1099511628211UL;
			}
			Hash ^= 0xffU;
			Hash *= 1099511628211UL;
		}

		private static string Encode(string Value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static bool Decode(string Value, out string Decoded)
		{
			Decoded = null;
			try
			{
				byte[] bytes = Convert.FromBase64String(Value ?? "");
				if (bytes.Length > MaxRegistryFieldChars * 4) return false;
				Decoded = Encoding.UTF8.GetString(bytes);
				return Decoded.Length <= MaxRegistryFieldChars;
			}
			catch
			{
				return false;
			}
		}
	}
}
