namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>
		/// One TAF-owned, presentation-only founder memory for this world. It remains realm-external
		/// across exile/refounding and never enters vanilla Sultan/history/journal selection pools.
		/// </summary>
		public KingdomFounderHistoryReceipt FounderHistory =
			new KingdomFounderHistoryReceipt();
	}
}
