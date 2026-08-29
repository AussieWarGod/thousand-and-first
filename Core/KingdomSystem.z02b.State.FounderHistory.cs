namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>
		/// One public, presentation-only founder memory for this world. It remains realm-external
		/// across exile/refounding so a playthrough can never mint a procession of pseudo-sultans.
		/// </summary>
		public KingdomFounderHistoryReceipt FounderHistory =
			new KingdomFounderHistoryReceipt();
	}
}
