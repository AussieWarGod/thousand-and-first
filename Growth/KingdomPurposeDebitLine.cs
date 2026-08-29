namespace ThousandAndFirst
{
	/// <summary>One exact physical before/after allocation. Object identity never substitutes.</summary>
	public sealed class KingdomPurposeDebitLine
	{
		public KingdomPurposeDebitKind Kind;
		public string ContainerId;
		public string ObjectId;
		public string Blueprint;
		public int Before;
		public int After;
		public int TypeIndex;
		public int Capacity;

		public KingdomPurposeDebitLine Copy()
		{
			return (KingdomPurposeDebitLine)MemberwiseClone();
		}
	}
}
