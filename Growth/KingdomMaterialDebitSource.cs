namespace ThousandAndFirst
{
	/// <summary>Engine-free reading of one unique physical source.</summary>
	public sealed class KingdomMaterialDebitSource
	{
		public readonly int Source;
		public readonly KingdomMaterialDebitSourceKind Kind;
		public readonly int KindIndex;
		public readonly int Count;
		public readonly KingdomBitTally UnitBits;

		public KingdomMaterialDebitSource(int Source, KingdomMaterialDebitSourceKind Kind,
			int KindIndex, int Count, KingdomBitTally UnitBits = null)
		{
			this.Source = Source;
			this.Kind = Kind;
			this.KindIndex = KindIndex;
			this.Count = Count;
			this.UnitBits = (UnitBits == null) ? new KingdomBitTally() : UnitBits.Copy();
		}
	}
}
