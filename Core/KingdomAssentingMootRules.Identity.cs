using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMootRules
	{
		private static void Seal(KingdomAssentingMootReceipt Receipt)
		{
			Receipt.Version = CurrentReceiptVersion;
			Receipt.AuthorityId = "taf:assenting-moot:v1:authority:" + Digest(
				"TAF-ASSENTING-MOOT-AUTHORITY-V1", Receipt.RealmId, Receipt.SettlementId,
				Receipt.ZoneId, Receipt.BuildingObjectId, Receipt.LotId,
				Receipt.Generation.ToString(CultureInfo.InvariantCulture));
			List<string> fields = new List<string>
			{
				"TAF-ASSENTING-MOOT-MEMBERSHIP-V1", Receipt.AuthorityId,
				Receipt.AssentResidentIds.Count.ToString(CultureInfo.InvariantCulture)
			};
			AppendRows(fields, "assent", Receipt.AssentResidentIds,
				Receipt.AssentResidentNames, Receipt.AssentBodyObjectIds);
			fields.Add(Receipt.ExemptResidentIds.Count.ToString(CultureInfo.InvariantCulture));
			AppendRows(fields, "exempt", Receipt.ExemptResidentIds,
				Receipt.ExemptResidentNames, Receipt.ExemptBodyObjectIds);
			Receipt.MembershipFingerprint = "taf:assenting-moot:v1:members:"
				+ Digest(fields.ToArray());
		}

		private static void AppendRows(List<string> Fields, string Role, List<int> Ids,
			List<string> Names, List<string> Bodies)
		{
			for (int i = 0; i < Ids.Count; i++)
			{
				Fields.Add(Role);
				Fields.Add(Ids[i].ToString(CultureInfo.InvariantCulture));
				Fields.Add(Names[i]);
				Fields.Add(Bodies[i]);
			}
		}

		private static string Digest(params string[] Fields)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					for (int i = 0; i < Fields.Length; i++) writer.Write(Fields[i] ?? "");
					writer.Flush();
					using (SHA256 sha = SHA256.Create())
					{
						byte[] hash = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < hash.Length; i++)
							text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
						return text.ToString();
					}
				}
			}
			catch { return ""; }
		}

		private static string SingleLine(string Value, int Limit)
		{
			if (string.IsNullOrWhiteSpace(Value) || Limit < 1) return "";
			StringBuilder text = new StringBuilder(Math.Min(Value.Length, Limit));
			bool space = false;
			for (int i = 0; i < Value.Length && text.Length < Limit; i++)
			{
				char c = Value[i];
				if (char.IsControl(c) || char.IsWhiteSpace(c))
				{
					space = text.Length > 0;
					continue;
				}
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

		private static bool OptionalBounded(string Value, int Limit)
		{
			return string.IsNullOrEmpty(Value) || Bounded(Value, Limit);
		}
	}
}
