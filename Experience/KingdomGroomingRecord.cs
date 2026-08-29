using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Bounded, realm-owned proof that one exact resident has been prepared to inherit.
	/// Progress is monotonic evidence, not elapsed time guessed while nobody observed the city.</summary>
	public readonly struct KingdomGroomingRecord
	{
		public const int SchemaVersion = 1;
		public const int MaxRealmIdChars = 256;
		public const int MaxNameChars = 512;
		public const int MaxWireChars = 2048;

		public readonly string RealmId;
		public readonly int ResidentId;
		public readonly string NomineeName;
		public readonly long NominatedTick;
		public readonly int ServiceMarks;
		public readonly int StudyMarks;
		public readonly int Revision;

		private KingdomGroomingRecord(string realmId, int residentId, string nomineeName,
			long nominatedTick, int serviceMarks, int studyMarks, int revision)
		{
			RealmId = realmId; ResidentId = residentId; NomineeName = nomineeName;
			NominatedTick = nominatedTick; ServiceMarks = serviceMarks;
			StudyMarks = studyMarks; Revision = revision;
		}

		public bool Ready => KingdomGroomingRules.Ready(ServiceMarks, StudyMarks);

		public static bool TryCreate(string RealmId, int ResidentId, string NomineeName,
			long NominatedTick, int ServiceMarks, int StudyMarks, int Revision,
			out KingdomGroomingRecord Value)
		{
			Value = default(KingdomGroomingRecord);
			if (string.IsNullOrEmpty(RealmId) || RealmId.Length > MaxRealmIdChars
				|| ResidentId <= 0 || string.IsNullOrEmpty(NomineeName)
				|| NomineeName.Length > MaxNameChars || NominatedTick < 0L || Revision < 0
				|| !KingdomGroomingRules.ValidMarks(ServiceMarks, StudyMarks)) return false;
			Value = new KingdomGroomingRecord(RealmId, ResidentId, NomineeName,
				NominatedTick, ServiceMarks, StudyMarks, Revision);
			return true;
		}

		public static bool TryAdvance(KingdomGroomingRecord Current, int ServiceEvidence,
			int StudyEvidence, out KingdomGroomingRecord Value)
		{
			Value = default(KingdomGroomingRecord);
			KingdomGroomingRecord proved;
			if (!TryCreate(Current.RealmId, Current.ResidentId, Current.NomineeName,
				Current.NominatedTick, Current.ServiceMarks, Current.StudyMarks,
				Current.Revision, out proved)
				|| !KingdomGroomingRules.ValidMarks(ServiceEvidence, StudyEvidence)
				|| Current.Revision == int.MaxValue) return false;
			int service = Math.Max(Current.ServiceMarks, ServiceEvidence);
			int study = Math.Max(Current.StudyMarks, StudyEvidence);
			if (service == Current.ServiceMarks && study == Current.StudyMarks) return false;
			return TryCreate(Current.RealmId, Current.ResidentId, Current.NomineeName,
				Current.NominatedTick, service, study, Current.Revision + 1, out Value);
		}

		public static string Encode(KingdomGroomingRecord Value)
		{
			KingdomGroomingRecord proved;
			if (!TryCreate(Value.RealmId, Value.ResidentId, Value.NomineeName,
				Value.NominatedTick, Value.ServiceMarks, Value.StudyMarks, Value.Revision,
				out proved)) return "";
			string wire = "v1|" + B(Value.RealmId) + "|" + I(Value.ResidentId) + "|"
				+ B(Value.NomineeName) + "|" + L(Value.NominatedTick) + "|"
				+ I(Value.ServiceMarks) + "|" + I(Value.StudyMarks) + "|" + I(Value.Revision);
			return wire.Length <= MaxWireChars ? wire : "";
		}

		public static bool TryDecode(string Wire, out KingdomGroomingRecord Value)
		{
			Value = default(KingdomGroomingRecord);
			if (string.IsNullOrEmpty(Wire) || Wire.Length > MaxWireChars) return false;
			string[] p = Wire.Split('|');
			string realm, name;
			int resident, service, study, revision;
			long nominated;
			if (p.Length != 8 || p[0] != "v1" || !D(p[1], out realm)
				|| !N(p[2], out resident) || !D(p[3], out name) || !T(p[4], out nominated)
				|| !N(p[5], out service) || !N(p[6], out study) || !N(p[7], out revision)
				|| !TryCreate(realm, resident, name, nominated, service, study, revision,
					out Value)) return false;
			return string.Equals(Encode(Value), Wire, StringComparison.Ordinal);
		}

		private static string B(string Text) =>
			Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? ""));
		private static bool D(string Text, out string Value)
		{
			Value = "";
			try
			{
				byte[] bytes = Convert.FromBase64String(Text ?? "");
				Value = new UTF8Encoding(false, true).GetString(bytes);
				return Convert.ToBase64String(bytes) == Text;
			}
			catch { return false; }
		}
		private static string I(int Value) => Value.ToString(CultureInfo.InvariantCulture);
		private static string L(long Value) => Value.ToString(CultureInfo.InvariantCulture);
		private static bool N(string Text, out int Value) =>
			int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
			&& I(Value) == Text;
		private static bool T(string Text, out long Value) =>
			long.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
			&& L(Value) == Text;
	}
}
