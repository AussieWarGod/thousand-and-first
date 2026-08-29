using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public enum KingdomShopStockVerdict
	{
		RefusedMalformed = 0,
		RefusedNoMerchant = 1,
		RefusedAmbiguousMerchant = 2,
		AlreadyIssued = 3,
		Issue = 4
	}

	/// <summary>One local market-output batch may be issued at each newly reached stock tier.
	/// The persisted ShopTier is the at-most-once receipt, not a promise of endless stock.</summary>
	public static class KingdomShopStockRules
	{
		public const int MaximumTier = 8;
		public const string IssueIntentProperty = "TAFLocalMarketOutputIntent";
		public const string ItemSourceProperty = "TAFLocalMarketOutputSource";
		public const string ItemSettlementProperty = "TAFLocalMarketOutputSettlement";
		public const string ItemTierProperty = "TAFLocalMarketOutputTier";

		public static KingdomShopStockVerdict Classify(int IssuedTier, int RequestedTier,
			int ExactMerchantCount)
		{
			if (IssuedTier < 0 || IssuedTier > MaximumTier || RequestedTier < 1 ||
				RequestedTier > MaximumTier) return KingdomShopStockVerdict.RefusedMalformed;
			if (ExactMerchantCount < 1) return KingdomShopStockVerdict.RefusedNoMerchant;
			if (ExactMerchantCount > 1)
				return KingdomShopStockVerdict.RefusedAmbiguousMerchant;
			return RequestedTier <= IssuedTier
				? KingdomShopStockVerdict.AlreadyIssued
				: KingdomShopStockVerdict.Issue;
		}

		public static int NextIssueTier(int IssuedTier, int AttainedTier)
		{
			if (IssuedTier < 0 || IssuedTier > MaximumTier || AttainedTier < 1
				|| AttainedTier > MaximumTier)
				return AttainedTier;
			return AttainedTier > IssuedTier && IssuedTier < MaximumTier
				? IssuedTier + 1 : AttainedTier;
		}

		public static string SourceId(string RealmId, string SettlementId, int Tier)
		{
			if (!ValidId(RealmId) || !ValidId(SettlementId) ||
				Tier < 1 || Tier > MaximumTier) return null;
			using (SHA256 sha = SHA256.Create())
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
			{
				writer.Write(RealmId); writer.Write(SettlementId); writer.Write(Tier); writer.Flush();
				byte[] hash = sha.ComputeHash(stream.ToArray());
				StringBuilder result = new StringBuilder("taf:local-market-output:v1:");
				for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2"));
				return result.ToString();
			}
		}

		public static string IntentReceipt(string SourceId)
		{
			return string.IsNullOrEmpty(SourceId) ? null : SourceId + ":intent";
		}

		private static bool ValidId(string value)
		{
			if (string.IsNullOrEmpty(value) || value.Length > 128 || value.Trim() != value) return false;
			try { return new UTF8Encoding(false, true).GetByteCount(value) <= 384; }
			catch (EncoderFallbackException) { return false; }
		}
	}
}
