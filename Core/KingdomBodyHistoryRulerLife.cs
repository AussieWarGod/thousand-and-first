using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Immutable identity of one player-controlled body in one exact reign.</summary>
	public sealed class KingdomRulerLifeSnapshot
	{
		public string RealmId = "";
		public int SuccessionOrdinal;
		public string BodyObjectId = "";
		public string RulerLifeId = "";

		public KingdomRulerLifeSnapshot Copy()
		{
			return new KingdomRulerLifeSnapshot
			{
				RealmId = RealmId ?? "",
				SuccessionOrdinal = SuccessionOrdinal,
				BodyObjectId = BodyObjectId ?? "",
				RulerLifeId = RulerLifeId ?? ""
			};
		}
	}

	/// <summary>Pure, bounded identity rules shared by commission and current-body views.</summary>
	public static class KingdomBodyHistoryRulerLifeRules
	{
		private const string LifePrefix = "taf:ruler-life:v1:";
		private const string ObjectPrefix = "taf:object:";

		public static string Identity(string RealmId, int SuccessionOrdinal,
			string BodyObjectId)
		{
			if (!Inputs(RealmId, SuccessionOrdinal, BodyObjectId)) return null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					writer.Write("TAF-RULER-LIFE-V1");
					writer.Write(RealmId);
					writer.Write(SuccessionOrdinal);
					writer.Write(BodyObjectId);
					writer.Flush();
					using (SHA256 sha = SHA256.Create())
					{
						byte[] digest = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(LifePrefix);
						for (int i = 0; i < digest.Length; i++)
							text.Append(digest[i].ToString("x2",
								CultureInfo.InvariantCulture));
						return text.ToString();
					}
				}
			}
			catch (EncoderFallbackException) { return null; }
		}

		public static bool Valid(KingdomRulerLifeSnapshot Snapshot)
		{
			return Snapshot != null && Inputs(Snapshot.RealmId,
				Snapshot.SuccessionOrdinal, Snapshot.BodyObjectId)
				&& string.Equals(Snapshot.RulerLifeId, Identity(Snapshot.RealmId,
					Snapshot.SuccessionOrdinal, Snapshot.BodyObjectId),
					StringComparison.Ordinal);
		}

		public static bool ValidIdentity(string RealmId, int SuccessionOrdinal,
			string BodyObjectId, string RulerLifeId)
		{
			return string.Equals(RulerLifeId,
				Identity(RealmId, SuccessionOrdinal, BodyObjectId),
				StringComparison.Ordinal);
		}

		private static bool Inputs(string RealmId, int SuccessionOrdinal,
			string BodyObjectId)
		{
			if (SuccessionOrdinal < 0 || SuccessionOrdinal == int.MaxValue
				|| !KingdomIdentityRules.IsRealmId(RealmId)
				|| string.IsNullOrEmpty(BodyObjectId)
				|| !BodyObjectId.StartsWith(ObjectPrefix, StringComparison.Ordinal)) return false;
			try
			{
				UTF8Encoding utf8 = new UTF8Encoding(false, true);
				return BodyObjectId.IndexOf('\0') < 0
					&& utf8.GetByteCount(RealmId) <= KingdomBodyHistoryCodec.MaxRealmIdBytes
					&& utf8.GetByteCount(BodyObjectId) <= KingdomBodyHistoryRules.MaxIdBytes;
			}
			catch (EncoderFallbackException) { return false; }
		}
	}
}
