using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		internal bool TryWriteLegacy(KingdomSealRecord Record, out string Failure)
		{
			Failure = "";
			if (Record == null || Record.Status != KingdomSealStatus.Promoted || !Record.IsResolved)
			{
				Failure = "only a promoted legacy with its fate drawn is written";
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Record.LegacyId) || !KingdomSealReceipt.ValidId(Record.LineageId))
			{
				Failure = "the legacy or lineage is not an identifier this build accepts";
				return false;
			}
			string path = LegacyPath(Record.LegacyId);
			if (TryWriteSeal(path, Record, false, out Failure))
			{
				return true;
			}
			KingdomSealRecord existing = ReadSlot(path);
			if (existing != null && existing.LegacyId == Record.LegacyId && SameRecord(existing, Record))
			{
				Failure = "";
				return true;
			}
			return false;
		}

		internal List<KingdomSealRecord> ReadLegacies(out int Refused)
		{
			Refused = 0;
			List<KingdomSealRecord> legacies = new List<KingdomSealRecord>();
			bool overflow;
			int refusedJunk;
			foreach (string path in Files(LegaciesFolder, SealExtension,
				MaxFilesScanned, out overflow, out refusedJunk))
			{
				if (!path.EndsWith(SealExtension, StringComparison.Ordinal))
				{
					continue;
				}
				string name = Path.GetFileName(path);
				string legacy = name.Substring(0, name.Length - SealExtension.Length);
				KingdomSealRecord record = ReadSlot(path);
				if (record == null || record.LegacyId != legacy || record.Status != KingdomSealStatus.Promoted || !record.IsResolved)
				{
					Refused++;
					continue;
				}
				legacies.Add(record);
			}
			if (overflow)
			{
				Refused++;
			}
			Refused += refusedJunk;
			legacies.Sort(delegate(KingdomSealRecord a, KingdomSealRecord b)
			{
				return string.CompareOrdinal(a.LegacyId, b.LegacyId);
			});
			return legacies;
		}

	}
}
