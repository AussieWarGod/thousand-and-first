using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure identity, text, phase, and receipt law for public founder memory.</summary>
	public static class KingdomFounderHistoryRules
	{
		public const int CurrentVersion = 1;
		public const int MaxIdentityChars = 1024;
		public const int MaxNameChars = 192;
		public const int MaxCauseChars = 768;
		public const int MaxGospelChars = 1536;
		public const int MaxFaultChars = 512;
		public const string EntityType = "taf-founder-memory";
		public const string EventMarker = "taf:founder-memory:v1";
		public const string JournalAttribute = "taf-founder-memory";

		public static bool TryPrepare(string RealmId, string DeathToken, long DeathTick,
			long PreparedTick, long HistoricYear, string FounderName, string CityName,
			string RegionName, string Cause, bool Enabled,
			out KingdomFounderHistoryReceipt Receipt, out string Failure)
		{
			Receipt = null;
			Failure = "";
			string realm = SingleLine(RealmId, MaxIdentityChars);
			string token = SingleLine(DeathToken, MaxIdentityChars);
			string founder = SingleLine(FounderName, MaxNameChars);
			string city = SingleLine(CityName, MaxNameChars);
			string region = SingleLine(RegionName, MaxNameChars);
			string cause = SingleLine(Cause, MaxCauseChars);
			if (string.IsNullOrEmpty(realm) || string.IsNullOrEmpty(token)
				|| string.IsNullOrEmpty(founder) || string.IsNullOrEmpty(city)
				|| DeathTick < 0L || PreparedTick < DeathTick
				|| HistoricYear == long.MinValue)
			{
				Failure = "founder-memory preparation lacks bounded exact identity";
				return false;
			}
			if (string.IsNullOrEmpty(region)) region = "an unnamed reach of Qud";
			if (string.IsNullOrEmpty(cause)) cause = "died beyond any surviving account";
			string digest = Digest(realm, token);
			if (string.IsNullOrEmpty(digest))
			{
				Failure = "founder-memory identity digest was unavailable";
				return false;
			}
			string gospel = Gospel(founder, city, region, cause);
			Receipt = new KingdomFounderHistoryReceipt
			{
				Version = CurrentVersion,
				Phase = Enabled ? KingdomFounderHistoryPhase.Prepared
					: KingdomFounderHistoryPhase.Suppressed,
				PublicationEnabled = Enabled,
				RealmId = realm,
				DeathToken = token,
				DeathTick = DeathTick,
				PreparedTick = PreparedTick,
				HistoricYear = HistoricYear,
				CommittedTick = Enabled ? 0L : PreparedTick,
				FounderName = founder,
				CityName = city,
				RegionName = region,
				Cause = cause,
				Gospel = gospel,
				EntityId = "taf:founder-memory:v1:entity:" + digest,
				NoteId = "taf:founder-memory:v1:note:" + digest,
				ProofId = "taf:founder-memory:v1:proof:" + digest,
				EventId = 0L,
				Fault = ""
			};
			return Validate(Receipt, out Failure);
		}

		public static bool Validate(KingdomFounderHistoryReceipt Receipt,
			out string Failure)
		{
			Failure = "";
			if (Receipt == null || Receipt.Version != CurrentVersion
				|| !Enum.IsDefined(typeof(KingdomFounderHistoryPhase), Receipt.Phase))
				return Fail("unknown founder-memory receipt version or phase", out Failure);
			if (Receipt.Phase == KingdomFounderHistoryPhase.None)
				return Empty(Receipt) || Fail("idle founder-memory receipt carries residue", out Failure);
			if (!Bounded(Receipt.RealmId, MaxIdentityChars)
				|| !Bounded(Receipt.DeathToken, MaxIdentityChars)
				|| !Bounded(Receipt.FounderName, MaxNameChars)
				|| !Bounded(Receipt.CityName, MaxNameChars)
				|| !Bounded(Receipt.RegionName, MaxNameChars)
				|| !Bounded(Receipt.Cause, MaxCauseChars)
				|| !Bounded(Receipt.Gospel, MaxGospelChars)
				|| !Bounded(Receipt.EntityId, MaxIdentityChars)
				|| !Bounded(Receipt.NoteId, MaxIdentityChars)
				|| !Bounded(Receipt.ProofId, MaxIdentityChars)
				|| Receipt.DeathTick < 0L || Receipt.PreparedTick < Receipt.DeathTick
				|| Receipt.HistoricYear == long.MinValue)
				return Fail("founder-memory receipt has malformed bounded evidence", out Failure);
			string digest = Digest(Receipt.RealmId, Receipt.DeathToken);
			if (string.IsNullOrEmpty(digest)
				|| Receipt.EntityId != "taf:founder-memory:v1:entity:" + digest
				|| Receipt.NoteId != "taf:founder-memory:v1:note:" + digest
				|| Receipt.ProofId != "taf:founder-memory:v1:proof:" + digest
				|| Receipt.Gospel != Gospel(Receipt.FounderName, Receipt.CityName,
					Receipt.RegionName, Receipt.Cause))
				return Fail("founder-memory receipt identity or telling diverged", out Failure);
			if (Receipt.Phase == KingdomFounderHistoryPhase.Suppressed)
				return !Receipt.PublicationEnabled && Receipt.EventId == 0L
					&& Receipt.CommittedTick >= Receipt.PreparedTick
					&& string.IsNullOrEmpty(Receipt.Fault)
					|| Fail("suppressed founder-memory receipt carries publication residue", out Failure);
			if (Receipt.Phase == KingdomFounderHistoryPhase.Quarantined)
				return Receipt.PublicationEnabled && Receipt.CommittedTick == 0L
					&& Bounded(Receipt.Fault, MaxFaultChars)
					|| Fail("quarantined founder-memory receipt lacks a bounded fault", out Failure);
			if (!Receipt.PublicationEnabled || !string.IsNullOrEmpty(Receipt.Fault))
				return Fail("active founder-memory receipt has inconsistent option or fault", out Failure);
			bool eventPhase = Receipt.Phase >= KingdomFounderHistoryPhase.EventPublished;
			if (eventPhase != (Receipt.EventId > 0L))
				return Fail("founder-memory event identity disagrees with its phase", out Failure);
			if (Receipt.Phase == KingdomFounderHistoryPhase.Committed)
				return Receipt.CommittedTick >= Receipt.PreparedTick
					|| Fail("committed founder-memory receipt lacks its tick", out Failure);
			return Receipt.CommittedTick == 0L
				|| Fail("open founder-memory receipt carries a terminal tick", out Failure);
		}

		public static bool Owns(KingdomFounderHistoryReceipt Receipt,
			string RealmId, string DeathToken)
		{
			return Receipt != null && Receipt.Phase != KingdomFounderHistoryPhase.None
				&& string.Equals(Receipt.RealmId, RealmId, StringComparison.Ordinal)
				&& string.Equals(Receipt.DeathToken, DeathToken, StringComparison.Ordinal);
		}

		public static string EntityName(KingdomFounderHistoryReceipt Receipt)
		{
			return Receipt == null ? "a founder remembered"
				: Receipt.FounderName + ", founder of " + Receipt.CityName;
		}

		public static string QuarantineReason(string Reason)
		{
			string value = SingleLine(Reason, MaxFaultChars);
			return string.IsNullOrEmpty(value) ? "public founder-memory evidence diverged" : value;
		}

		private static string Gospel(string Founder, string City, string Region, string Cause)
		{
			string value = Founder + " founded " + City + " in " + Region + ". When "
				+ Founder + " " + Cause + ", named residents held a mourning rite and kept "
				+ "the founder in public memory.";
			return SingleLine(value, MaxGospelChars);
		}

		private static string Digest(string RealmId, string DeathToken)
		{
			try
			{
				byte[] payload;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					writer.Write("TAF-FOUNDER-MEMORY-V1");
					writer.Write(RealmId ?? "");
					writer.Write(DeathToken ?? "");
					writer.Flush();
					payload = stream.ToArray();
				}
				using (SHA256 sha = SHA256.Create())
				{
					if (sha == null) return null;
					byte[] digest = sha.ComputeHash(payload);
					StringBuilder text = new StringBuilder(64);
					for (int i = 0; i < digest.Length; i++)
						text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
					return text.ToString();
				}
			}
			catch { return null; }
		}

		private static string SingleLine(string Value, int Limit)
		{
			if (string.IsNullOrWhiteSpace(Value) || Limit < 1) return "";
			StringBuilder text = new StringBuilder(Math.Min(Value.Length, Limit));
			bool space = false;
			for (int i = 0; i < Value.Length && text.Length < Limit; i++)
			{
				char c = Value[i];
				if (char.IsControl(c) || char.IsWhiteSpace(c)) { space = text.Length > 0; continue; }
				if (space && text.Length < Limit) text.Append(' ');
				space = false;
				if (text.Length < Limit) text.Append(c);
			}
			return text.ToString().Trim();
		}

		private static bool Bounded(string Value, int Limit)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= Limit
				&& string.Equals(Value, SingleLine(Value, Limit), StringComparison.Ordinal);
		}

		private static bool Empty(KingdomFounderHistoryReceipt R)
		{
			return !R.PublicationEnabled && string.IsNullOrEmpty(R.RealmId)
				&& string.IsNullOrEmpty(R.DeathToken) && R.DeathTick == 0L
				&& R.PreparedTick == 0L && R.HistoricYear == long.MinValue
				&& R.CommittedTick == 0L && string.IsNullOrEmpty(R.FounderName)
				&& string.IsNullOrEmpty(R.CityName) && string.IsNullOrEmpty(R.RegionName)
				&& string.IsNullOrEmpty(R.Cause) && string.IsNullOrEmpty(R.Gospel)
				&& string.IsNullOrEmpty(R.EntityId) && string.IsNullOrEmpty(R.NoteId)
				&& string.IsNullOrEmpty(R.ProofId) && R.EventId == 0L
				&& string.IsNullOrEmpty(R.Fault);
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
