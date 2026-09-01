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

		private static bool TryCreateChronicleIntent(string EventId,
			KingdomChronicleDeclaration Declaration, string RegistryHash,
			string OtherRegistryHash, string RegistryFault, out string Intent)
		{
			Intent = null;
			if (Declaration == null || Declaration.AuthoredOutsiderText == null ||
				!string.Equals(EventId, Declaration.EventId, StringComparison.Ordinal)) return false;
			return KingdomRealmChronicleIntentRules.TryEncode(
				new KingdomRealmChronicleIntent
				{
					Version = KingdomRealmChronicleIntentRules.CurrentVersion,
					EventId = EventId, OfficialText = Declaration.Text,
					OutsiderText = Declaration.AuthoredOutsiderText,
					Accomplishment = Declaration.Accomplishment,
					MuralText = Declaration.MuralText,
					Fingerprint = Declaration.Fingerprint, RegistryHash = RegistryHash,
					OtherRegistryHash = OtherRegistryHash,
					OfficialBefore = Declaration.OfficialBefore,
					OfficialAfter = Declaration.OfficialAfter,
					OutsiderBefore = Declaration.OutsiderBefore,
					OutsiderAfter = Declaration.OutsiderAfter,
					Official = Declaration.Official, Outsider = Declaration.Outsider,
					RegistryFault = RegistryFault
				}, out Intent);
		}

		private static bool TryParseChronicleIntent(string Intent, string ExpectedEventId,
			string LegacyText, bool LegacyAccomplishment, string LegacyMuralText,
			out KingdomChronicleDeclaration Declaration, out string RegistryHash,
			out string OtherRegistryHash, out string RegistryFault, out bool Legacy)
		{
			Declaration = null; RegistryHash = null; OtherRegistryHash = null;
			RegistryFault = null; Legacy = false;
			KingdomRealmChronicleIntent value;
			if (!KingdomRealmChronicleIntentRules.TryDecodeCurrent(Intent, ExpectedEventId,
				out value))
			{
				if (!KingdomRealmChronicleIntentRules.TryDecodeLegacy(Intent, ExpectedEventId,
					LegacyText, LegacyAccomplishment, LegacyMuralText, out value)) return false;
				Legacy = true;
			}
			Declaration = new KingdomChronicleDeclaration(value.EventId, value.OfficialText,
				value.Accomplishment, value.MuralText, value.OutsiderText, value.Fingerprint,
				value.Official, value.Outsider, value.OfficialBefore, value.OfficialAfter,
				value.OutsiderBefore, value.OutsiderAfter);
			RegistryHash = value.RegistryHash; OtherRegistryHash = value.OtherRegistryHash;
			RegistryFault = value.RegistryFault;
			return true;
		}

		private static string ChronicleObserved(string RegistryHash, string OtherRegistryHash,
			string OfficialHash, string OutsiderHash, KingdomChronicleReceipt Receipt,
			bool Legacy)
		{
			string prefix = Legacy ? KingdomRealmChronicleIntentRules.LegacyPrefix :
				KingdomRealmChronicleIntentRules.CurrentPrefix;
			return Receipt == null ? null : prefix + "|observed|" + RegistryHash +
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
