using System;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure, bounded payload frozen inside one realm callback receipt.</summary>
	internal sealed class KingdomRealmChronicleIntent
	{
		internal int Version;
		internal string EventId;
		internal string OfficialText;
		internal string OutsiderText;
		internal bool Accomplishment;
		internal string MuralText;
		internal string Fingerprint;
		internal string RegistryHash;
		internal string OtherRegistryHash;
		internal string OfficialBefore;
		internal string OfficialAfter;
		internal string OutsiderBefore;
		internal string OutsiderAfter;
		internal string Official;
		internal string Outsider;
		internal string RegistryFault;
	}

	/// <summary>Current disputed-telling wire plus exact, non-authorizing v2 decode.</summary>
	internal static class KingdomRealmChronicleIntentRules
	{
		internal const string CurrentPrefix = "chronicle-v3";
		internal const string LegacyPrefix = "chronicle-v2";
		internal const int CurrentVersion = 3;
		internal const int LegacyVersion = 2;
		internal const int MaxWireChars = 65536;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		internal static bool TryEncode(KingdomRealmChronicleIntent Value, out string Wire)
		{
			Wire = null;
			if (!ValidCurrent(Value)) return false;
			try
			{
				Wire = CurrentPrefix + "|" + Encode(Value.EventId) + "|" + Value.Fingerprint +
					"|" + Value.RegistryHash + "|" + Value.OtherRegistryHash + "|" +
					Value.OfficialBefore + "|" + Value.OfficialAfter + "|" +
					Value.OutsiderBefore + "|" + Value.OutsiderAfter + "|" +
					(Value.Accomplishment ? "1" : "0") + "|" + Encode(Value.OfficialText) +
					"|" + Encode(Value.OutsiderText) + "|" + EncodeNullable(Value.MuralText) +
					"|" + Encode(Value.Official) + "|" + Encode(Value.Outsider) + "|" +
					Encode(Value.RegistryFault);
				return Wire.Length <= MaxWireChars;
			}
			catch { Wire = null; return false; }
		}

		internal static bool TryDecodeCurrent(string Wire, string ExpectedEventId,
			out KingdomRealmChronicleIntent Value)
		{
			Value = null;
			if (!WireShape(Wire, CurrentPrefix, 15, out string[] field) ||
				!Decode(field[1], KingdomChronicleReceiptRules.MaxEventIdChars, out string eventId) ||
				!Decode(field[10], KingdomChronicleReceiptRules.MaxEventTextChars,
					out string officialText) ||
				!Decode(field[11], KingdomChronicleReceiptRules.MaxEventTextChars,
					out string outsiderText) ||
				!DecodeNullable(field[12], KingdomChronicleReceiptRules.MaxMuralTextChars,
					out string muralText) ||
				!Decode(field[13], KingdomChronicleReceiptRules.MaxEntryChars,
					out string official) ||
				!Decode(field[14], KingdomChronicleReceiptRules.MaxEntryChars,
					out string outsider) || !Decode(field[15], 160, out string registryFault) ||
				(field[9] != "0" && field[9] != "1") ||
				!string.Equals(eventId, ExpectedEventId, StringComparison.Ordinal)) return false;
			Value = Build(CurrentVersion, eventId, officialText, outsiderText, field[9] == "1",
				muralText, field, official, outsider, registryFault);
			return ValidCurrent(Value);
		}

		/// <summary>Reads the pre-counter-history intent exactly. Its old fingerprint may
		/// finish only the already-published outer callback; callers never upgrade or replay it.</summary>
		internal static bool TryDecodeLegacy(string Wire, string ExpectedEventId,
			string OfficialText, bool Accomplishment, string MuralText,
			out KingdomRealmChronicleIntent Value)
		{
			Value = null;
			if (!WireShape(Wire, LegacyPrefix, 11, out string[] field) ||
				!Decode(field[1], KingdomChronicleReceiptRules.MaxEventIdChars, out string eventId) ||
				!Decode(field[9], KingdomChronicleReceiptRules.MaxEntryChars, out string official) ||
				!Decode(field[10], KingdomChronicleReceiptRules.MaxEntryChars, out string outsider) ||
				!Decode(field[11], 160, out string registryFault) ||
				!string.Equals(eventId, ExpectedEventId, StringComparison.Ordinal) ||
				!KingdomChronicleReceiptRules.TryFingerprint(eventId, OfficialText,
					Accomplishment, MuralText, out string fingerprint) || fingerprint != field[2])
				return false;
			Value = Build(LegacyVersion, eventId, OfficialText, null, Accomplishment,
				MuralText, field, official, outsider, registryFault);
			return ValidShared(Value) && Value.Fingerprint == fingerprint;
		}

		private static KingdomRealmChronicleIntent Build(int Version, string EventId,
			string OfficialText, string OutsiderText, bool Accomplishment, string MuralText,
			string[] Field, string Official, string Outsider, string RegistryFault)
		{
			return new KingdomRealmChronicleIntent
			{
				Version = Version, EventId = EventId, OfficialText = OfficialText,
				OutsiderText = OutsiderText, Accomplishment = Accomplishment,
				MuralText = MuralText, Fingerprint = Field[2], RegistryHash = Field[3],
				OtherRegistryHash = Field[4], OfficialBefore = Field[5],
				OfficialAfter = Field[6], OutsiderBefore = Field[7],
				OutsiderAfter = Field[8], Official = Official, Outsider = Outsider,
				RegistryFault = RegistryFault
			};
		}

		private static bool ValidCurrent(KingdomRealmChronicleIntent Value)
		{
			return Value != null && Value.Version == CurrentVersion &&
				!string.IsNullOrEmpty(Value.OutsiderText) &&
				Value.OutsiderText.Length <= KingdomChronicleReceiptRules.MaxEventTextChars &&
				KingdomChronicleReceiptRules.TryDisputedFingerprint(Value.EventId,
					Value.Official, Value.Outsider, Value.Accomplishment, Value.MuralText,
					out string fingerprint) && fingerprint == Value.Fingerprint && ValidShared(Value);
		}

		private static bool ValidShared(KingdomRealmChronicleIntent Value)
		{
			return Value != null && !string.IsNullOrEmpty(Value.EventId) &&
				Value.EventId.Length <= KingdomChronicleReceiptRules.MaxEventIdChars &&
				Value.OfficialText != null &&
				Value.OfficialText.Length <= KingdomChronicleReceiptRules.MaxEventTextChars &&
				(Value.MuralText == null ||
				 Value.MuralText.Length <= KingdomChronicleReceiptRules.MaxMuralTextChars) &&
				!string.IsNullOrEmpty(Value.Official) &&
				Value.Official.Length <= KingdomChronicleReceiptRules.MaxEntryChars &&
				!string.IsNullOrEmpty(Value.Outsider) &&
				Value.Outsider.Length <= KingdomChronicleReceiptRules.MaxEntryChars &&
				Value.RegistryFault != null && Value.RegistryFault.Length <= 160 &&
				Hash(Value.Fingerprint) && Hash(Value.RegistryHash) &&
				Hash(Value.OtherRegistryHash) && Hash(Value.OfficialBefore) &&
				Hash(Value.OfficialAfter) && Hash(Value.OutsiderBefore) &&
				Hash(Value.OutsiderAfter);
		}

		private static bool WireShape(string Wire, string Prefix, int Separators,
			out string[] Field)
		{
			Field = null;
			if (string.IsNullOrEmpty(Wire) || Wire.Length > MaxWireChars ||
				!Wire.StartsWith(Prefix + "|", StringComparison.Ordinal)) return false;
			int count = 0;
			for (int i = 0; i < Wire.Length; i++) if (Wire[i] == '|') count++;
			if (count != Separators) return false;
			Field = Wire.Split('|');
			return Field.Length == Separators + 1 && Field[0] == Prefix;
		}

		private static string Encode(string Value)
		{
			return Convert.ToBase64String(StrictUtf8.GetBytes(Value ?? ""));
		}

		private static string EncodeNullable(string Value)
		{
			return Value == null ? "-" : Encode(Value);
		}

		private static bool Decode(string Value, int MaxChars, out string Text)
		{
			Text = null;
			if (Value == null || Value.Length > MaxChars * 6) return false;
			try
			{
				Text = StrictUtf8.GetString(Convert.FromBase64String(Value));
				return Text.Length <= MaxChars;
			}
			catch { Text = null; return false; }
		}

		private static bool DecodeNullable(string Value, int MaxChars, out string Text)
		{
			if (Value == "-") { Text = null; return true; }
			return Decode(Value, MaxChars, out Text);
		}

		private static bool Hash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9') ||
					(Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}
	}
}
