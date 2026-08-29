using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>One realm-bound, versioned succession custom. The resident identity is a
	/// realm-global id, never a display name.</summary>
	public readonly struct KingdomSuccessionConfiguration
	{
		public const int SchemaVersion = 2;
		public const int MaxRealmIdChars = 256;
		public const int MaxWireChars = 1024;

		public readonly string RealmId;
		public readonly HeirChoice Choice;
		public readonly int ChosenResidentId;
		public readonly bool SeatCostEnabled;
		public readonly int Revision;

		private KingdomSuccessionConfiguration(string realmId, HeirChoice choice,
			int chosenResidentId, bool seatCostEnabled, int revision)
		{
			RealmId = realmId;
			Choice = choice;
			ChosenResidentId = chosenResidentId;
			SeatCostEnabled = seatCostEnabled;
			Revision = revision;
		}

		public static bool TryCreate(string RealmId, HeirChoice Choice, int ChosenResidentId,
			bool SeatCostEnabled, int Revision, out KingdomSuccessionConfiguration Value)
		{
			Value = default(KingdomSuccessionConfiguration);
			if (string.IsNullOrEmpty(RealmId) || RealmId.Length > MaxRealmIdChars
				|| !Enum.IsDefined(typeof(HeirChoice), Choice) || Revision < 0
				|| (Choice == HeirChoice.Law
					&& (ChosenResidentId != 0 || !SeatCostEnabled))
				|| (Choice == HeirChoice.Chosen && ChosenResidentId <= 0)
				|| (Choice == HeirChoice.Groomed
					&& (ChosenResidentId <= 0 || !SeatCostEnabled))) return false;
			Value = new KingdomSuccessionConfiguration(RealmId, Choice, ChosenResidentId,
				SeatCostEnabled, Revision);
			return true;
		}

		public static bool TryDefault(string RealmId, out KingdomSuccessionConfiguration Value)
		{
			return TryCreate(RealmId, HeirChoice.Law, 0, true, 0, out Value);
		}

		/// <summary>Produces one changed revision. A no-op and revision overflow both refuse.</summary>
		public static bool TryRevise(KingdomSuccessionConfiguration Current,
			HeirChoice Choice, int ChosenResidentId, bool SeatCostEnabled,
			out KingdomSuccessionConfiguration Value)
		{
			Value = default(KingdomSuccessionConfiguration);
			KingdomSuccessionConfiguration proved;
			if (!TryCreate(Current.RealmId, Current.Choice, Current.ChosenResidentId,
				Current.SeatCostEnabled, Current.Revision, out proved)
				|| Current.Revision == int.MaxValue
				|| (Current.Choice == Choice && Current.ChosenResidentId == ChosenResidentId
					&& Current.SeatCostEnabled == SeatCostEnabled)) return false;
			return TryCreate(Current.RealmId, Choice, ChosenResidentId, SeatCostEnabled,
				Current.Revision + 1, out Value);
		}

		public static string Encode(KingdomSuccessionConfiguration Value)
		{
			KingdomSuccessionConfiguration proved;
			if (!TryCreate(Value.RealmId, Value.Choice, Value.ChosenResidentId,
				Value.SeatCostEnabled, Value.Revision, out proved)) return "";
			string wire = "v2|" + ToBase64(Value.RealmId) + "|"
				+ ((int)Value.Choice).ToString(CultureInfo.InvariantCulture) + "|"
				+ Value.ChosenResidentId.ToString(CultureInfo.InvariantCulture) + "|"
				+ (Value.SeatCostEnabled ? "1" : "0") + "|"
				+ Value.Revision.ToString(CultureInfo.InvariantCulture);
			return wire.Length <= MaxWireChars ? wire : "";
		}

		public static bool TryDecode(string Wire, out KingdomSuccessionConfiguration Value)
		{
			Value = default(KingdomSuccessionConfiguration);
			if (string.IsNullOrEmpty(Wire) || Wire.Length > MaxWireChars) return false;
			string[] p = Wire.Split('|');
			string realm;
			int choice, resident, revision;
			if (p.Length != 6 || (p[0] != "v1" && p[0] != "v2")
				|| !TryFromBase64(p[1], out realm)
				|| !TryInt(p[2], out choice) || !TryInt(p[3], out resident)
				|| (p[4] != "0" && p[4] != "1") || !TryInt(p[5], out revision)
				|| (p[0] == "v1" && choice == (int)HeirChoice.Groomed)
				|| !TryCreate(realm, (HeirChoice)choice, resident, p[4] == "1",
					revision, out Value)) return false;
			string canonical = p[0] + "|" + ToBase64(Value.RealmId) + "|"
				+ ((int)Value.Choice).ToString(CultureInfo.InvariantCulture) + "|"
				+ Value.ChosenResidentId.ToString(CultureInfo.InvariantCulture) + "|"
				+ (Value.SeatCostEnabled ? "1" : "0") + "|"
				+ Value.Revision.ToString(CultureInfo.InvariantCulture);
			return string.Equals(canonical, Wire, StringComparison.Ordinal);
		}

		private static bool TryInt(string Text, out int Value)
		{
			return int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value.ToString(CultureInfo.InvariantCulture) == Text;
		}

		internal static string ToBase64(string Text)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? ""));
		}

		internal static bool TryFromBase64(string Encoded, out string Text)
		{
			Text = "";
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded ?? "");
				Text = new UTF8Encoding(false, true).GetString(bytes);
				return Convert.ToBase64String(bytes) == Encoded;
			}
			catch { return false; }
		}
	}
}
