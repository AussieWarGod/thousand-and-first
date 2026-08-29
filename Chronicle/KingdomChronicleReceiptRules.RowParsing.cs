using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomChronicleReceiptRules
	{
		private static bool TryParseV3Row(string Line, out KingdomChronicleReceipt Receipt)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(Line) || Line.Length > MaxActiveRowChars) return false;
			if (Line.StartsWith("a|", StringComparison.Ordinal))
			{
				if (Count(Line, '|') != 13) return false;
				string[] field = Line.Split('|');
				string id, official, outsider;
				KingdomChronicleSinkDisposition officialState, outsiderState, journalState;
				long updated;
				bool legacy;
				if (field.Length != 14 || !Decode(field[1], MaxEncodedIdChars,
						MaxEventIdChars, 1024, out id)
					|| !Decode(field[3], MaxEncodedEntryChars, MaxEntryChars, 24576,
						out official)
					|| !Decode(field[4], MaxEncodedEntryChars, MaxEntryChars, 24576,
						out outsider)
					|| !TryState(field[9], out officialState)
					|| !TryState(field[10], out outsiderState)
					|| !TryState(field[11], out journalState)
					|| !TryLong(field[12], out updated) || !TryBool(field[13], out legacy)) return false;
				Receipt = new KingdomChronicleReceipt
				{
					EventId = id,
					Fingerprint = field[2],
					Official = official,
					Outsider = outsider,
					OfficialBefore = field[5],
					OfficialAfter = field[6],
					OutsiderBefore = field[7],
					OutsiderAfter = field[8],
					OfficialState = officialState,
					OutsiderState = outsiderState,
					JournalState = journalState,
					Updated = updated,
					Compact = false,
					LegacyBlocked = legacy
				};
				return ReceiptValid(Receipt);
			}
			if (Line.StartsWith("tg|", StringComparison.Ordinal))
			{
				if (Line.Length > MaxTerminalRowChars || Count(Line, '|') != 7) return false;
				string[] field = Line.Split('|');
				string id;
				if (field.Length != 8 || !Decode(field[1], MaxEncodedIdChars,
					MaxEventIdChars, 1024, out id)) return false;
				return TryBuildTerminal(id, field[2], field[3], field[4], field[5],
					field[6], field[7], out Receipt);
			}
			if (Line.StartsWith("tc|", StringComparison.Ordinal))
			{
				if (Line.Length > MaxTerminalRowChars || Count(Line, '|') != 8) return false;
				string[] field = Line.Split('|');
				string coordinate;
				if (field.Length != 9 || !IsLowerHex(field[1], 32)
					|| !Decode(field[2], MaxEncodedIdChars, MaxConstructionCoordinateChars,
						1024, out coordinate)) return false;
				string id = "construction:" + field[1] + ":" + coordinate;
				string job;
				string exactCoordinate;
				if (!TryConstructionIdentity(id, out job, out exactCoordinate)
					|| job != field[1] || exactCoordinate != coordinate) return false;
				return TryBuildTerminal(id, field[3], field[4], field[5], field[6],
					field[7], field[8], out Receipt);
			}
			return false;
		}

		private static bool TryBuildTerminal(string Id, string Fingerprint,
			string OfficialStateText, string OutsiderStateText, string JournalStateText,
			string UpdatedText, string LegacyText, out KingdomChronicleReceipt Receipt)
		{
			Receipt = null;
			KingdomChronicleSinkDisposition official, outsider, journal;
			long updated;
			bool legacy;
			if (!TryState(OfficialStateText, out official)
				|| !TryState(OutsiderStateText, out outsider)
				|| !TryState(JournalStateText, out journal)
				|| !TryLong(UpdatedText, out updated) || !TryBool(LegacyText, out legacy)) return false;
			Receipt = new KingdomChronicleReceipt
			{
				EventId = Id,
				Fingerprint = Fingerprint == "-" ? null : Fingerprint,
				OfficialState = official,
				OutsiderState = outsider,
				JournalState = journal,
				Updated = updated,
				Compact = true,
				LegacyBlocked = legacy
			};
			return ReceiptValid(Receipt);
		}

		private static bool TryParseLegacy(string Text, List<KingdomChronicleReceipt> Receipts,
			out KingdomChronicleRegistryFault Fault)
		{
			Fault = KingdomChronicleRegistryFault.None;
			int separators = Count(Text, '\n');
			if (separators > LegacyMaxReceipts)
			{
				Fault = KingdomChronicleRegistryFault.TooManyRows;
				return false;
			}
			string[] lines = Text.Split('\n');
			if (lines.Length != separators + 1 || lines[0] != "v1")
			{
				Fault = KingdomChronicleRegistryFault.MalformedHeader;
				return false;
			}
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 1; i < lines.Length; i++)
			{
				string line = lines[i];
				if (string.IsNullOrEmpty(line) || line.Length > LegacyMaxRowChars
					|| Count(line, '|') != 9)
				{
					Fault = KingdomChronicleRegistryFault.MalformedRow;
					Receipts.Clear();
					return false;
				}
				string[] field = line.Split('|');
				string id, fingerprint, official, outsider, ob, oa, ub, ua;
				int phase;
				long updated;
				if (field.Length != 10
					|| !Decode(field[0], MaxEncodedIdChars, MaxEventIdChars, 1024, out id)
					|| !Decode(field[1], LegacyMaxHashEncodedChars, LegacyHashChars, 64,
						out fingerprint)
					|| !Decode(field[2], MaxEncodedEntryChars, MaxEntryChars, 24576,
						out official)
					|| !Decode(field[3], MaxEncodedEntryChars, MaxEntryChars, 24576,
						out outsider)
					|| !Decode(field[4], LegacyMaxHashEncodedChars, LegacyHashChars, 64, out ob)
					|| !Decode(field[5], LegacyMaxHashEncodedChars, LegacyHashChars, 64, out oa)
					|| !Decode(field[6], LegacyMaxHashEncodedChars, LegacyHashChars, 64, out ub)
					|| !Decode(field[7], LegacyMaxHashEncodedChars, LegacyHashChars, 64, out ua)
					|| !TryInt(field[8], out phase) || phase < 0 || phase > 31
					|| ((phase & 2) != 0 && (phase & 1) == 0)
					|| ((phase & 8) != 0 && (phase & 4) == 0)
					|| !TryLong(field[9], out updated) || string.IsNullOrEmpty(id)
					|| string.IsNullOrEmpty(official) || string.IsNullOrEmpty(outsider)
					|| !IsLowerHex(fingerprint, LegacyHashChars)
					|| !IsLowerHex(ob, LegacyHashChars) || !IsLowerHex(oa, LegacyHashChars)
					|| !IsLowerHex(ub, LegacyHashChars) || !IsLowerHex(ua, LegacyHashChars)
					|| !ids.Add(id))
				{
					Fault = KingdomChronicleRegistryFault.MalformedRow;
					Receipts.Clear();
					return false;
				}
				Receipts.Add(new KingdomChronicleReceipt
				{
					EventId = id,
					OfficialState = KingdomChronicleSinkDisposition.Lost,
					OutsiderState = KingdomChronicleSinkDisposition.Lost,
					JournalState = KingdomChronicleSinkDisposition.Lost,
					Updated = updated,
					Compact = true,
					LegacyBlocked = true
				});
			}
			return true;
		}

	}
}
