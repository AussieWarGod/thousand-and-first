using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomLabRules
	{
		internal static List<KingdomLabRegistryEntry> ParseRegistry(string Text,
			out bool Quarantined)
		{
			List<KingdomLabRegistryEntry> rows = new List<KingdomLabRegistryEntry>();
			Quarantined = false;
			if (string.IsNullOrEmpty(Text)) return rows;
			string[] lines = Text.Split('\n');
			bool rulerLifeWire = lines.Length > 0 && lines[0] == "v2";
			if (lines.Length == 0 || (!rulerLifeWire && lines[0] != "v1"))
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
				KingdomLabRegistryEntry row;
				if (!TryParseRegistryRow(lines[i], rulerLifeWire, out row)
					|| !ValidRegistryEntry(row) || IndexOfRegistry(rows, row.JobId) >= 0)
				{
					Quarantined = true;
					continue;
				}
				rows.Add(row);
			}
			return rows;
		}

		private static bool TryParseRegistryRow(string Text, bool RulerLifeWire,
			out KingdomLabRegistryEntry Row)
		{
			Row = null;
			string[] fields = Text.Split('|');
			if (fields.Length != (RulerLifeWire ? 18 : 16)) return false;
			int ordinal = -1;
			string life = "";
			if (RulerLifeWire && (!int.TryParse(fields[6], NumberStyles.Integer,
				CultureInfo.InvariantCulture, out ordinal) || !Decode(fields[7], out life)))
				return false;
			int at = RulerLifeWire ? 8 : 6;
			long founded;
			long updated;
			int status;
			int version;
			int source;
			int attach;
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
			if (!Decode(fields[0], out job) || !Decode(fields[1], out building)
				|| !Decode(fields[2], out patient) || !Decode(fields[3], out game)
				|| !Decode(fields[4], out realm) || !long.TryParse(fields[5],
					NumberStyles.Integer, CultureInfo.InvariantCulture, out founded)
				|| !int.TryParse(fields[at], NumberStyles.None, CultureInfo.InvariantCulture,
					out version) || !Decode(fields[at + 1], out key)
				|| !Decode(fields[at + 2], out grants) || !int.TryParse(fields[at + 3],
					NumberStyles.Integer, CultureInfo.InvariantCulture, out source)
				|| !int.TryParse(fields[at + 4], NumberStyles.Integer,
					CultureInfo.InvariantCulture, out attach)
				|| !Decode(fields[at + 5], out manager) || !Decode(fields[at + 6], out detail)
				|| !Decode(fields[at + 7], out fingerprint)
				|| !int.TryParse(fields[at + 8], NumberStyles.None,
					CultureInfo.InvariantCulture, out status)
				|| !Enum.IsDefined(typeof(KingdomLabRegistryStatus),
					(KingdomLabRegistryStatus)status)
				|| !long.TryParse(fields[at + 9], NumberStyles.Integer,
					CultureInfo.InvariantCulture, out updated)) return false;
			Row = new KingdomLabRegistryEntry
			{
				JobId = job,
				BuildingId = building,
				PatientId = patient,
				GameId = game,
				RealmId = realm,
				RealmFoundedTick = founded,
				RulerSuccessionOrdinal = ordinal,
				RulerLifeId = life,
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
			return true;
		}
	}
}
