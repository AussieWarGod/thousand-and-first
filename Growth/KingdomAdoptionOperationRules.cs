using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public sealed class KingdomAdoptionOperationReceipt
	{
		public string RootId;
		public string BuildingKey;
		public string Category;
		public int StaffNeeded;
		public bool ThresholdManning;
		public string Revision;
	}

	/// <summary>Signed immutable operating contract for one adopted staffed room.</summary>
	public static class KingdomAdoptionOperationRules
	{
		public const int Schema = 1;
		public const int MaxReceiptChars = 2048;

		public static bool RequiresContract(string Category, int StaffNeeded)
		{
			return StaffNeeded > 0
				&& KingdomAdoptRules.ClassifyRole(Category) == KingdomAdoptRules.RoleKind.Work;
		}

		public static bool TryCreate(string RootId, string BuildingKey, string Category,
			int StaffNeeded, bool ThresholdManning,
			out KingdomAdoptionOperationReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			string category = Fold(Category);
			if (!KingdomDesignationRules.SafeToken(RootId, 256)
				|| !KingdomDesignationRules.SafeToken(BuildingKey, 128)
				|| !KingdomDesignationRules.SafeToken(category, 64))
				return Fail("adoption operation identity is malformed", out Failure);
			if (!RequiresContract(category, StaffNeeded))
				return Fail("only a staffed non-storage work may carry an operation contract",
					out Failure);
			KingdomAdoptionOperationReceipt receipt = new KingdomAdoptionOperationReceipt {
				RootId = RootId, BuildingKey = BuildingKey, Category = category,
				StaffNeeded = StaffNeeded, ThresholdManning = ThresholdManning
			};
			receipt.Revision = Hash(Body(receipt)); Receipt = receipt; return true;
		}

		public static string Encode(KingdomAdoptionOperationReceipt Receipt)
		{
			if (Receipt == null) return null;
			string body = Body(Receipt);
			if (Receipt.Revision != Hash(body)) return null;
			string encoded = body + "|" + Receipt.Revision;
			return encoded.Length <= MaxReceiptChars ? encoded : null;
		}

		public static bool TryDecode(string Encoded,
			out KingdomAdoptionOperationReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxReceiptChars)
				return Fail("adoption operation receipt is absent or over its bound", out Failure);
			string[] fields = Encoded.Split('|');
			if (fields.Length != 7 || fields[0] != "o1"
				|| !Unframe(fields[1], out string root)
				|| !Unframe(fields[2], out string building)
				|| !Unframe(fields[3], out string category)
				|| !int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture,
					out int staff) || staff.ToString(CultureInfo.InvariantCulture) != fields[4]
				|| (fields[5] != "0" && fields[5] != "1"))
				return Fail("adoption operation receipt text is malformed", out Failure);
			if (!TryCreate(root, building, category, staff, fields[5] == "1",
				out Receipt, out Failure) || Receipt.Revision != fields[6]
				|| Encode(Receipt) != Encoded)
				return Failure != null ? false
					: Fail("adoption operation receipt hash disagrees", out Failure);
			return true;
		}

		private static string Body(KingdomAdoptionOperationReceipt Receipt)
		{
			return "o1|" + Frame(Receipt.RootId) + "|" + Frame(Receipt.BuildingKey) + "|"
				+ Frame(Fold(Receipt.Category)) + "|"
				+ Receipt.StaffNeeded.ToString(CultureInfo.InvariantCulture) + "|"
				+ (Receipt.ThresholdManning ? "1" : "0");
		}

		private static string Frame(string Value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static bool Unframe(string Value, out string Result)
		{
			try { Result = Encoding.UTF8.GetString(Convert.FromBase64String(Value)); return true; }
			catch { Result = null; return false; }
		}

		private static string Hash(string Value)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Value));
				StringBuilder result = new StringBuilder(64);
				for (int i = 0; i < bytes.Length; i++)
					result.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
				return result.ToString();
			}
		}

		private static string Fold(string Value)
		{
			return (Value ?? "").Trim().ToLowerInvariant();
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
