namespace ThousandAndFirst
{
	public static class KingdomMarketStockAuthorityRules
	{
		/// <summary>Ordinary item movement releases only exact authority of the running realm.</summary>
		public static bool MayRetire(string CurrentRealm, string ReceiptRealm,
			bool ExactReceipt)
		{
			return ExactReceipt && !string.IsNullOrEmpty(CurrentRealm)
				&& CurrentRealm == ReceiptRealm;
		}
	}
}
