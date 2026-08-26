namespace ThousandAndFirst
{
	/// <summary>The frontier edge a gatehouse crosses. The value is part of the frozen plan.</summary>
	public enum KingdomGatehouseOrientation : byte
	{
		North = 1,
		East = 2,
		South = 3,
		West = 4
	}

	/// <summary>One authored cell in the gatehouse's fixed topology.</summary>
	public struct KingdomGatehouseCell
	{
		public int X;
		public int Y;
		public string Slot;
		public string Blueprint;

		public KingdomGatehouseCell(int X, int Y, string Slot, string Blueprint)
		{
			this.X = X;
			this.Y = Y;
			this.Slot = Slot;
			this.Blueprint = Blueprint;
		}
	}

	/// <summary>
	/// Frozen road-aligned footprint for the catalogue gatehouse. It is deliberately not a
	/// plot tier: the root remains a boundary/network work, while these exact coordinates own
	/// overlap and strike cleanup.
	/// </summary>
	public sealed class KingdomGatehousePlan
	{
		public KingdomGatehouseOrientation Orientation;
		public int GateX;
		public int GateY;
		public int X1;
		public int Y1;
		public int X2;
		public int Y2;
	}
}
