using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private bool EnsureArchiveChronicleState(KingdomRealmArchive Archive,
			KingdomChronicleDeclaration Declaration, KingdomChronicleReceipt EventReceipt,
			string Registry, string RegistryFault, string FrozenRegistryHash,
			out string Refusal)
		{
			Refusal = "";
			if (Archive == null || Declaration == null || EventReceipt == null ||
				!KingdomChronicleReceiptRules.IsTerminal(EventReceipt) ||
				!KingdomChronicleReceiptRules.TryHashList("official", ChronicleEntries,
					out string officialLive) ||
				!KingdomChronicleReceiptRules.TryHashList("outsider", OutsiderEntries,
					out string outsiderLive))
				return QuarantineReturn(Archive,
					"Chronicle terminal archive state cannot be bounded", out Refusal);
			string officialExpected = EventReceipt.OfficialState ==
				KingdomChronicleSinkDisposition.Delivered ? Declaration.OfficialAfter :
				EventReceipt.OfficialState == KingdomChronicleSinkDisposition.Lost ?
				Declaration.OfficialBefore : null;
			string outsiderExpected = EventReceipt.OutsiderState ==
				KingdomChronicleSinkDisposition.Delivered ? Declaration.OutsiderAfter :
				EventReceipt.OutsiderState == KingdomChronicleSinkDisposition.Lost ?
				Declaration.OutsiderBefore : null;
			if (officialExpected == null || outsiderExpected == null ||
				officialLive != officialExpected || outsiderLive != outsiderExpected ||
				!TryHashTextPair(Registry, RegistryFault, out string desiredRegistryHash) ||
				!TryHashTextPair(Archive.ChronicleRegistry, Archive.ChronicleRegistryFault,
					out string archivedRegistryHash))
				return QuarantineReturn(Archive,
					"Chronicle terminal sinks do not match declared state", out Refusal);
			if (!KingdomChronicleReceiptRules.TryHashList("official", Archive.ChronicleEntries,
					out string archivedOfficial) ||
				(archivedOfficial != Declaration.OfficialBefore &&
				 archivedOfficial != officialExpected) ||
				!KingdomChronicleReceiptRules.TryHashList("outsider", Archive.OutsiderEntries,
					out string archivedOutsider) ||
				(archivedOutsider != Declaration.OutsiderBefore &&
				 archivedOutsider != outsiderExpected) ||
				(archivedRegistryHash != FrozenRegistryHash &&
				 archivedRegistryHash != desiredRegistryHash))
				return QuarantineReturn(Archive,
					"archived Chronicle CAS reached a third state", out Refusal);
			if (archivedOfficial == Declaration.OfficialBefore)
				Archive.ChronicleEntries = KingdomRealmArchive.CloneStrings(ChronicleEntries);
			if (archivedOutsider == Declaration.OutsiderBefore)
				Archive.OutsiderEntries = KingdomRealmArchive.CloneStrings(OutsiderEntries);
			if (archivedRegistryHash == FrozenRegistryHash)
			{
				Archive.ChronicleRegistry = Registry;
				Archive.ChronicleRegistryFault = RegistryFault;
			}
			return true;
		}

		private static bool TryInspectChronicle(string EventId, string Fingerprint,
			out string RegistryHash, out bool Present, out bool Terminal, out bool Lost,
			out bool Conflict, out string Registry, out string RegistryFault,
			out string OtherRegistryHash, out KingdomChronicleReceipt EventReceipt)
		{
			RegistryHash = null; Present = false; Terminal = false; Lost = false;
			Conflict = false; Registry = null; RegistryFault = null; OtherRegistryHash = null;
			EventReceipt = null;
			if (!KingdomChronicle.TryCaptureRealmRegistry(out Registry, out RegistryFault,
				out string failure) || !TryHashTextPair(Registry, RegistryFault, out RegistryHash) ||
				!KingdomChronicleReceiptRules.TryParseRegistry(Registry,
					out List<KingdomChronicleReceipt> rows, out bool migrated,
					out KingdomChronicleRegistryFault fault) || migrated) return false;
			List<KingdomChronicleReceipt> otherRows =
				new List<KingdomChronicleReceipt>(rows.Count);
			for (int i = 0; i < rows.Count; i++)
			{
				if (!string.Equals(rows[i].EventId, EventId, StringComparison.Ordinal))
				{
					otherRows.Add(rows[i].Copy());
					continue;
				}
				if (Present) { Conflict = true; return true; }
				Present = true;
				if (!string.Equals(rows[i].Fingerprint, Fingerprint, StringComparison.Ordinal))
				{
					Conflict = true; return true;
				}
				EventReceipt = rows[i].Copy();
				Terminal = KingdomChronicleReceiptRules.IsTerminal(rows[i]);
				Lost = rows[i].OfficialState == KingdomChronicleSinkDisposition.Lost ||
					rows[i].OutsiderState == KingdomChronicleSinkDisposition.Lost ||
					rows[i].JournalState == KingdomChronicleSinkDisposition.Lost;
			}
			return KingdomChronicleReceiptRules.TryWriteRegistry(otherRows,
				out string otherRegistry, out KingdomChronicleRegistryFault otherFault) &&
				otherFault == KingdomChronicleRegistryFault.None &&
				// Fault state is diagnostic output of this exact callback (not unrelated row
				// authority): an honest Lost sink may update it. Freeze only other receipt rows.
				TryHashTextPair(otherRegistry, null, out OtherRegistryHash);
		}

		private const string ChronicleIntentPrefix = "chronicle-v2";

		private static bool TryCreateChronicleIntent(string EventId,
			KingdomChronicleDeclaration Declaration, string RegistryHash,
			string OtherRegistryHash, string RegistryFault, out string Intent)
		{
			Intent = null;
			if (Declaration == null || !ValidProofHash(RegistryHash) ||
				!ValidProofHash(OtherRegistryHash) ||
				!ValidProofHash(Declaration.Fingerprint) ||
				!ValidProofHash(Declaration.OfficialBefore) ||
				!ValidProofHash(Declaration.OfficialAfter) ||
				!ValidProofHash(Declaration.OutsiderBefore) ||
				!ValidProofHash(Declaration.OutsiderAfter) || RegistryFault == null ||
				RegistryFault.Length > 160 ||
				!string.Equals(EventId, Declaration.EventId, StringComparison.Ordinal)) return false;
			try
			{
				System.Text.UTF8Encoding utf8 = new System.Text.UTF8Encoding(false, true);
				Intent = ChronicleIntentPrefix + "|" +
					Convert.ToBase64String(utf8.GetBytes(EventId)) + "|" +
					Declaration.Fingerprint + "|" + RegistryHash + "|" + OtherRegistryHash + "|" +
					Declaration.OfficialBefore + "|" + Declaration.OfficialAfter + "|" +
					Declaration.OutsiderBefore + "|" + Declaration.OutsiderAfter + "|" +
					Convert.ToBase64String(utf8.GetBytes(Declaration.Official)) + "|" +
					Convert.ToBase64String(utf8.GetBytes(Declaration.Outsider)) + "|" +
					Convert.ToBase64String(utf8.GetBytes(RegistryFault));
				return Intent.Length <= KingdomRealmCallbackReceipt.MaxEffectChars;
			}
			catch { Intent = null; return false; }
		}

		private static bool TryParseChronicleIntent(string Intent, string ExpectedEventId,
			string Text, bool Accomplishment, string MuralText,
			out KingdomChronicleDeclaration Declaration, out string RegistryHash,
			out string OtherRegistryHash, out string RegistryFault)
		{
			Declaration = null; RegistryHash = null; OtherRegistryHash = null;
			RegistryFault = null;
			if (Intent == null || Intent.Length > KingdomRealmCallbackReceipt.MaxEffectChars)
				return false;
			string[] fields = Intent.Split('|');
			if (fields.Length != 12 || fields[0] != ChronicleIntentPrefix ||
				fields[1].Length > KingdomChronicleReceiptRules.MaxEventIdChars * 6 ||
				fields[9].Length > KingdomChronicleReceiptRules.MaxEntryChars * 6 ||
				fields[10].Length > KingdomChronicleReceiptRules.MaxEntryChars * 6 ||
				fields[11].Length > 960 ||
				!ValidProofHash(fields[2]) || !ValidProofHash(fields[3]) ||
				!ValidProofHash(fields[4]) || !ValidProofHash(fields[5]) ||
				!ValidProofHash(fields[6]) || !ValidProofHash(fields[7]) ||
				!ValidProofHash(fields[8])) return false;
			try
			{
				System.Text.UTF8Encoding utf8 = new System.Text.UTF8Encoding(false, true);
				string eventId = utf8.GetString(Convert.FromBase64String(fields[1]));
				string official = utf8.GetString(Convert.FromBase64String(fields[9]));
				string outsider = utf8.GetString(Convert.FromBase64String(fields[10]));
				string registryFault = utf8.GetString(Convert.FromBase64String(fields[11]));
				if (!string.Equals(eventId, ExpectedEventId, StringComparison.Ordinal) ||
					string.IsNullOrEmpty(official) || string.IsNullOrEmpty(outsider) ||
					official.Length > KingdomChronicleReceiptRules.MaxEntryChars ||
					outsider.Length > KingdomChronicleReceiptRules.MaxEntryChars ||
					registryFault.Length > 160 ||
					!KingdomChronicleReceiptRules.TryFingerprint(eventId, Text, Accomplishment,
						MuralText, out string fingerprint) || fingerprint != fields[2]) return false;
				Declaration = new KingdomChronicleDeclaration(eventId, Text, Accomplishment,
					MuralText, fields[2], official, outsider, fields[5], fields[6],
					fields[7], fields[8]);
				RegistryHash = fields[3]; OtherRegistryHash = fields[4];
				RegistryFault = registryFault;
				return true;
			}
			catch { Declaration = null; RegistryFault = null; return false; }
		}

		private static string ChronicleObserved(string RegistryHash, string OtherRegistryHash,
			string OfficialHash, string OutsiderHash, KingdomChronicleReceipt Receipt)
		{
			return Receipt == null ? null : ChronicleIntentPrefix + "|observed|" + RegistryHash +
				"|" + OtherRegistryHash + "|" + OfficialHash + "|" + OutsiderHash + "|" +
				((int)Receipt.OfficialState).ToString() + "|" +
				((int)Receipt.OutsiderState).ToString() + "|" +
				((int)Receipt.JournalState).ToString();
		}

		private static bool ValidProofHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9') ||
					(Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static bool TryHashTextPair(string Left, string Right, out string Hash)
		{
			Hash = null;
			try
			{
				System.Text.UTF8Encoding utf8 = new System.Text.UTF8Encoding(false, true);
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream, utf8, true))
				{
					WriteHashText(writer, Left, utf8); WriteHashText(writer, Right, utf8);
					writer.Flush();
					if (stream.Length > KingdomChronicleReceiptRules.MaxRegistryChars * 4L + 1024L)
						return false;
					using (global::System.Security.Cryptography.SHA256 sha =
						global::System.Security.Cryptography.SHA256.Create())
					{
						byte[] digest = sha.ComputeHash(stream.ToArray());
						System.Text.StringBuilder text = new System.Text.StringBuilder(64);
						for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
						Hash = text.ToString(); return true;
					}
				}
			}
			catch { return false; }
		}

		private static void WriteProofString(System.IO.BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			byte[] bytes = new System.Text.UTF8Encoding(false, true).GetBytes(Value);
			if (bytes.Length > 16384) throw new System.IO.InvalidDataException(
				"Engine callback proof string exceeds cap.");
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static void WriteProofStringDictionary(System.IO.BinaryWriter Writer,
			Dictionary<string, string> Values)
		{
			if (Values == null || Values.Count > 4096) throw new System.IO.InvalidDataException(
				"Engine callback proof dictionary exceeds cap.");
			List<string> keys = new List<string>(Values.Keys);
			keys.Sort(StringComparer.Ordinal); Writer.Write(keys.Count);
			for (int i = 0; i < keys.Count; i++)
			{
				WriteProofString(Writer, keys[i]); WriteProofString(Writer, Values[keys[i]]);
			}
		}

		private static void WriteWorshipProof(System.IO.BinaryWriter Writer,
			List<WorshipTracking> Values)
		{
			if (Values == null || Values.Count > 4096) throw new System.IO.InvalidDataException(
				"Engine callback worship proof exceeds cap.");
			Writer.Write(Values.Count);
			for (int i = 0; i < Values.Count; i++)
			{
				WorshipTracking row = Values[i];
				Writer.Write(row == null ? (byte)0 : (byte)1);
				if (row == null) continue;
				WriteProofString(Writer, row.Name); WriteProofString(Writer, row.Faction);
				Writer.Write(row.Devoted); Writer.Write(row.Times);
				Writer.Write(row.First); Writer.Write(row.Last);
			}
		}

	}
}
