using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Frozen provenance for one death. It prevents a changed custom, renamed resident,
	/// or recovery pass from selecting again.</summary>
	public readonly struct KingdomSuccessionSelectionReceipt
	{
		public const int MaxWireChars = 4096;
		public readonly string RealmId;
		public readonly string DeathToken;
		public readonly int ConfigurationRevision;
		public readonly int HeirResidentId;
		public readonly string HeirName;
		public readonly int LawHeirResidentId;
		public readonly string LawHeirName;
		public readonly HeirChoice Choice;
		public readonly bool CostsTheSeat;
		public readonly SuccessionSelectionReason Reason;

		private KingdomSuccessionSelectionReceipt(string realmId, string deathToken,
			int revision, int heirResidentId, string heirName, int lawResidentId,
			string lawName, HeirChoice choice, bool costsTheSeat,
			SuccessionSelectionReason reason)
		{
			RealmId = realmId; DeathToken = deathToken; ConfigurationRevision = revision;
			HeirResidentId = heirResidentId; HeirName = heirName;
			LawHeirResidentId = lawResidentId; LawHeirName = lawName;
			Choice = choice; CostsTheSeat = costsTheSeat; Reason = reason;
		}

		public static bool TryCreate(string RealmId, string DeathToken, int Revision,
			int HeirResidentId, string HeirName, int LawHeirResidentId, string LawHeirName,
			HeirChoice Choice, bool CostsTheSeat, SuccessionSelectionReason Reason,
			out KingdomSuccessionSelectionReceipt Value)
		{
			Value = default(KingdomSuccessionSelectionReceipt);
			bool chosen = Choice == HeirChoice.Chosen;
			bool groomed = Choice == HeirChoice.Groomed;
			int deathOrdinal;
			long deathTick;
			if (string.IsNullOrEmpty(RealmId)
				|| RealmId.Length > KingdomSuccessionConfiguration.MaxRealmIdChars
				|| string.IsNullOrEmpty(DeathToken)
				|| DeathToken.Length > KingdomSuccessionRules.MaxDeathTokenChars
				|| !KingdomSuccessionRules.TryReadDeathToken(DeathToken,
					out deathOrdinal, out deathTick)
				|| Revision < 0 || HeirResidentId <= 0 || LawHeirResidentId <= 0
				|| string.IsNullOrEmpty(HeirName) || HeirName.Length > 512
				|| string.IsNullOrEmpty(LawHeirName) || LawHeirName.Length > 512
				|| !Enum.IsDefined(typeof(HeirChoice), Choice)
				|| !Enum.IsDefined(typeof(SuccessionSelectionReason), Reason)
				|| CostsTheSeat && !chosen || chosen && HeirResidentId == LawHeirResidentId
				|| chosen != (Reason == SuccessionSelectionReason.Chosen)
				|| groomed != (Reason == SuccessionSelectionReason.Groomed)) return false;
			Value = new KingdomSuccessionSelectionReceipt(RealmId, DeathToken, Revision,
				HeirResidentId, HeirName, LawHeirResidentId, LawHeirName, Choice,
				CostsTheSeat, Reason);
			return true;
		}

		public static string Encode(KingdomSuccessionSelectionReceipt Value)
		{
			KingdomSuccessionSelectionReceipt proved;
			if (!TryCreate(Value.RealmId, Value.DeathToken, Value.ConfigurationRevision,
				Value.HeirResidentId, Value.HeirName, Value.LawHeirResidentId,
				Value.LawHeirName, Value.Choice, Value.CostsTheSeat, Value.Reason,
				out proved)) return "";
			string wire = "v1|" + B(Value.RealmId) + "|" + B(Value.DeathToken) + "|"
				+ I(Value.ConfigurationRevision) + "|" + I(Value.HeirResidentId) + "|"
				+ B(Value.HeirName) + "|" + I(Value.LawHeirResidentId) + "|"
				+ B(Value.LawHeirName) + "|" + I((int)Value.Choice) + "|"
				+ (Value.CostsTheSeat ? "1" : "0") + "|" + I((int)Value.Reason);
			return wire.Length <= MaxWireChars ? wire : "";
		}

		public static bool TryDecode(string Wire, out KingdomSuccessionSelectionReceipt Value)
		{
			Value = default(KingdomSuccessionSelectionReceipt);
			if (string.IsNullOrEmpty(Wire) || Wire.Length > MaxWireChars) return false;
			string[] p = Wire.Split('|');
			string realm, death, heirName, lawName;
			int revision, heir, law, choice, reason;
			if (p.Length != 11 || p[0] != "v1" || !D(p[1], out realm)
				|| !D(p[2], out death) || !N(p[3], out revision) || !N(p[4], out heir)
				|| !D(p[5], out heirName) || !N(p[6], out law) || !D(p[7], out lawName)
				|| !N(p[8], out choice) || (p[9] != "0" && p[9] != "1")
				|| !N(p[10], out reason)
				|| !TryCreate(realm, death, revision, heir, heirName, law, lawName,
					(HeirChoice)choice, p[9] == "1", (SuccessionSelectionReason)reason,
					out Value)) return false;
			return string.Equals(Encode(Value), Wire, StringComparison.Ordinal);
		}

		private static string B(string Text) => KingdomSuccessionConfiguration.ToBase64(Text);
		private static bool D(string Text, out string Value) =>
			KingdomSuccessionConfiguration.TryFromBase64(Text, out Value);
		private static string I(int Value) => Value.ToString(CultureInfo.InvariantCulture);
		private static bool N(string Text, out int Value) =>
			int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
			&& I(Value) == Text;
	}
}
