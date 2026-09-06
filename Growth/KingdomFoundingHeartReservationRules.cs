using System;
using System.Text;

namespace ThousandAndFirst
{
	internal static class KingdomFoundingHeartReservationRules
	{
		internal const string Prefix = "r_TAF_FoundingHeartReserved:";
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static string Encode(KingdomFoundingHeartPlan plan, string id, string role)
		{
			if (!KingdomFoundingHeartRules.Valid(plan) || string.IsNullOrEmpty(id)
				|| !CanonicalRole(role)) return null;
			try
			{
				KingdomFoundingHeartPlan completed = plan.Copy();
				for (int i = 0; i < completed.States.Length; i++) completed.States[i] = 2;
				string encoded = "hr1|" + Convert.ToBase64String(Utf8.GetBytes(plan.TransactionId))
					+ "|" + Convert.ToBase64String(Utf8.GetBytes(plan.ZoneId)) + "|"
					+ Convert.ToBase64String(Utf8.GetBytes(role)) + "|" + id + "|"
					+ KingdomFoundingHeartRules.CompletionSeal(completed);
				return TryRead(Prefix + id, encoded, out _, out _, out _) ? encoded : null;
			}
			catch (EncoderFallbackException) { return null; }
		}

		internal static bool TryRead(string key, string raw, out string transaction,
			out string zoneId, out string id)
		{
			transaction = null; zoneId = null; id = null;
			if (string.IsNullOrEmpty(key) || key.Length > 2048
				|| string.IsNullOrEmpty(raw) || raw.Length > 4096) return false;
			string[] fields = raw.Split('|');
			try
			{
				if (fields.Length != 6 || fields[0] != "hr1") return false;
				transaction = Utf8.GetString(Convert.FromBase64String(fields[1]));
				zoneId = Utf8.GetString(Convert.FromBase64String(fields[2]));
				string role = Utf8.GetString(Convert.FromBase64String(fields[3])); id = fields[4];
				if (!KingdomIdentityRules.IsFoundingTransaction(transaction)
					|| string.IsNullOrEmpty(zoneId) || string.IsNullOrEmpty(id)
					|| key != Prefix + id
					|| KingdomFoundingHeartRules.StableId(transaction, zoneId, role) != id
					|| !CanonicalSeal(fields[5])) return false;
				return CanonicalRole(role);
			}
			catch { return false; }
		}

		private static bool CanonicalRole(string role)
		{
			return role == "final" || role == "slot-0" || role == "slot-1" || role == "slot-2"
				|| role == "slot-3" || role == "slot-4" || role == "slot-5";
		}

		private static bool CanonicalSeal(string seal)
		{
			if (seal == null || seal.Length != 68 || !seal.StartsWith("hs1-", StringComparison.Ordinal)) return false;
			for (int i = 4; i < seal.Length; i++)
				if (!((seal[i] >= '0' && seal[i] <= '9') || (seal[i] >= 'a' && seal[i] <= 'f'))) return false;
			return true;
		}
	}
}
