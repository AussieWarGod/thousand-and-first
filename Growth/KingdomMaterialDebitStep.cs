namespace ThousandAndFirst
{
	/// <summary>One allocation from one exclusive physical source.</summary>
	public sealed class KingdomMaterialDebitStep
	{
		public readonly int Source;
		public readonly KingdomMaterialDebitSourceKind Kind;
		public readonly int KindIndex;
		public readonly int Original;
		public readonly int Taken;
		public readonly KingdomBitTally UnitBits;

		public int Remaining => Original - Taken;

		public bool NeedsFinalization => Taken == Original;

		public KingdomMaterialDebitStep(int Source, KingdomMaterialDebitSourceKind Kind,
			int KindIndex, int Original, int Taken, KingdomBitTally UnitBits = null)
		{
			this.Source = Source;
			this.Kind = Kind;
			this.KindIndex = KindIndex;
			this.Original = Original;
			this.Taken = Taken;
			this.UnitBits = (UnitBits == null) ? new KingdomBitTally() : UnitBits.Copy();
		}
	}
}
