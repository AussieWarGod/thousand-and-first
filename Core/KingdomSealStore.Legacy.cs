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
			FileStream gate;
			if (!TryLockLegacies(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				string path = LegacyPath(Record.LegacyId);
				KingdomSealRecord existing = ReadSlot(path);
				if (existing != null)
				{
					if (existing.LegacyId == Record.LegacyId && SameRecord(existing, Record))
					{
						Failure = "";
						return true;
					}
					Failure = "a different durable legacy already owns that generation identity";
					return false;
				}
				return TryWriteSeal(path, Record, false, out Failure);
			}
		}

		private bool TryLockLegacies(out FileStream Gate, out string Failure)
		{
			Gate = null;
			Failure = "";
			try
			{
				string folder;
				bool folderExists;
				if (!TrySafeFolder(LegaciesFolder, true, out folder, out folderExists, out Failure)
					|| !folderExists)
				{
					return false;
				}
				string path = Path.Combine(_root, LegaciesFolder, ".legacies.lock");
				bool exists;
				if (!TrySafeLeaf(path, out exists, out Failure))
				{
					return false;
				}
				Gate = new FileStream(path,
					FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				bool openedExists;
				if (!TrySafeLeaf(path, out openedExists, out Failure) || !openedExists)
				{
					Gate.Dispose();
					Gate = null;
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the legacy publication lock is unavailable: " + ex.Message;
				return false;
			}
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
