namespace ThousandAndFirst
{
	/// <summary>One exact plot-owned object. Same object and ID cross the handover.</summary>
	public sealed class KingdomRelocationRow
	{
		public string ObjectId;
		public string Blueprint;
		public int OffsetX;
		public int OffsetY;
		public bool Root;
		public KingdomRelocationRowState State;
	}

	/// <summary>One exact natural obstruction disclosed in destination preparation.</summary>
	public sealed class KingdomRelocationClearRow
	{
		public string ObjectId;
		public string Blueprint;
		public int X;
		public int Y;
		public KingdomRelocationClearState State;
	}
}
