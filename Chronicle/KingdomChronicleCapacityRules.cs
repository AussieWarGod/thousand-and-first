using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed class KingdomChronicleCapacityWitness
	{
		internal readonly string EventId, Fingerprint, RegistryHash;
		internal readonly int RegistryCount;
		internal KingdomChronicleCapacityWitness(string eventId, string fingerprint, int count, string hash)
		{ EventId = eventId; Fingerprint = fingerprint; RegistryCount = count; RegistryHash = hash; }
	}

	/// <summary>Exact full-registry non-publication evidence. No row is created or discarded.</summary>
	internal static class KingdomChronicleCapacityRules
	{
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
		internal static bool TryObserve(KingdomDurableKeyObservation observed, string eventId,
			string fingerprint, out KingdomChronicleCapacityWitness witness)
		{
			witness = null;
			if (!Text(eventId, KingdomChronicleReceiptRules.MaxEventIdChars, false)
				|| !KingdomChronicleReceiptRules.IsSha256(fingerprint)
				|| !KingdomScenarioStateShape.TryAuthorityText(observed, out string raw, out bool present, out _)
				|| !present || !KingdomChronicleReceiptRules.TryParseRegistry(raw,
					out List<KingdomChronicleReceipt> rows, out bool migrated, out _) || migrated
				|| rows.Count != KingdomChronicleReceiptRules.MaxReceipts
				|| !KingdomChronicleReceiptRules.TryWriteRegistry(rows, out string canonical, out _)
				|| !string.Equals(raw, canonical, StringComparison.Ordinal)) return false;
			foreach (KingdomChronicleReceipt row in rows)
				if (string.Equals(row.EventId, eventId, StringComparison.Ordinal)) return false;
			if (!TryHashRegistry(raw, out string hash)) return false;
			witness = new KingdomChronicleCapacityWitness(eventId, fingerprint, rows.Count, hash);
			return true;
		}

		internal static bool Valid(KingdomChronicleCapacityWitness witness, string eventId, string fingerprint)
		{
			return witness != null && witness.EventId == eventId && witness.Fingerprint == fingerprint
				&& Text(eventId, KingdomChronicleReceiptRules.MaxEventIdChars, false)
				&& KingdomChronicleReceiptRules.IsSha256(fingerprint)
				&& witness.RegistryCount == KingdomChronicleReceiptRules.MaxReceipts
				&& KingdomChronicleReceiptRules.IsSha256(witness.RegistryHash);
		}

		internal static bool TryFingerprint(string realm, string settlement, string eventId,
			string text, long tick, out string fingerprint)
		{
			fingerprint = null;
			return KingdomIdentityRules.IsRealmId(realm) && KingdomIdentityRules.IsSettlementId(settlement)
				&& tick >= 0 && Text(eventId, KingdomChronicleReceiptRules.MaxEventIdChars, false)
				&& Text(text, KingdomChronicleReceiptRules.MaxEventTextChars, true)
				&& KingdomChronicleReceiptRules.TryCanonicalHash("taf-chronicle-at-v1",
					new[] { realm, settlement, eventId, text, tick.ToString(CultureInfo.InvariantCulture) }, out fingerprint);
		}

		private static bool Text(string value, int maximum, bool empty)
		{
			if (value == null || value.Length > maximum || !empty && value.Length == 0) return false;
			foreach (char c in value) if (char.IsControl(c)) return false;
			try { Utf8.GetByteCount(value); return true; }
			catch (EncoderFallbackException) { return false; }
		}

		private static bool TryHashRegistry(string raw, out string hash)
		{
			hash = null;
			try
			{
				if (raw == null || raw.Length > KingdomChronicleReceiptRules.MaxRegistryChars
					|| Utf8.GetByteCount(raw) > KingdomChronicleReceiptRules.MaxRegistryChars) return false;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				using (SHA256 sha = SHA256.Create())
				{
					if (sha == null) return false;
					writer.Write("taf-chronicle-capacity-registry-v1"); writer.Write(raw); writer.Flush();
					StringBuilder text = new StringBuilder(64);
					foreach (byte item in sha.ComputeHash(stream.ToArray())) text.Append(item.ToString("x2", CultureInfo.InvariantCulture));
					hash = text.ToString(); return KingdomChronicleReceiptRules.IsSha256(hash);
				}
			}
			catch { hash = null; return false; }
		}
	}
}
