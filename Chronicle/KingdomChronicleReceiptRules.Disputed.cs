namespace ThousandAndFirst
{
	public static partial class KingdomChronicleReceiptRules
	{
		/// <summary>Fingerprints one exact contested telling. Unlike the ordinary v3
		/// fingerprint, this binds the two rendered register entries themselves, so the
		/// compact terminal receipt remains proof of both accounts after active prose is shed.</summary>
		internal static bool TryDisputedFingerprint(string EventId, string OfficialAccount,
			string OutsiderAccount, bool Accomplishment, string MuralText,
			out string Fingerprint)
		{
			Fingerprint = null;
			if (string.IsNullOrEmpty(EventId) || EventId.Length > MaxEventIdChars ||
				string.IsNullOrEmpty(OfficialAccount) || OfficialAccount.Length > MaxEntryChars ||
				string.IsNullOrEmpty(OutsiderAccount) || OutsiderAccount.Length > MaxEntryChars ||
				(MuralText != null && MuralText.Length > MaxMuralTextChars)) return false;
			return TryCanonicalHash("taf-chronicle-disputed-fingerprint-v1",
				new string[5]
				{
					EventId, OfficialAccount, OutsiderAccount,
					Accomplishment ? "1" : "0", MuralText
				}, out Fingerprint);
		}
	}
}
