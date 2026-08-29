namespace ThousandAndFirst
{
	/// <summary>Engine-free cardinality shared by topology and callback-cut receipts.</summary>
	public static class KingdomGatehouseTopology
	{
		public const int SatelliteCount = 6;
	}

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
	/// One immutable v2 gatehouse form. These values are copied into the paid plan before any
	/// debit; projection and strike recovery read only that plan, never the live catalogue.
	/// </summary>
	public sealed class KingdomGatehouseForm
	{
		public string FormKey;
		public string WallBlueprint;
		public string WatchBlueprint;
		public string RootRenderString;
		public string RootColorString;
		public string RootTileColor;
		public string RootDetailColor;
		public string RootClosedTile;
		public string RootOpenTile;
		public string WallRenderString;
		public string WallColorString;
		public string WallTileColor;
		public string WallDetailColor;
		public string WatchRenderString;
		public string WatchColorString;
		public string WatchTileColor;
		public string WatchDetailColor;
		public string WatchTile;
		public string MaterialClaim;
	}

	/// <summary>
	/// Frozen road-aligned footprint for the catalogue gatehouse. It is deliberately not a
	/// plot tier: the root remains a boundary/network work, while these exact coordinates own
	/// overlap and strike cleanup.
	/// </summary>
	public sealed class KingdomGatehousePlan
	{
		/// <summary>Receipt schema. One is the original geometry-only migration shape.</summary>
		public int ReceiptVersion;
		public KingdomGatehouseOrientation Orientation;
		public int GateX;
		public int GateY;
		public int X1;
		public int Y1;
		public int X2;
		public int Y2;
		public string FormKey;
		public string WallBlueprint;
		public string WatchBlueprint;
		public string RootRenderString;
		public string RootColorString;
		public string RootTileColor;
		public string RootDetailColor;
		public string RootClosedTile;
		public string RootOpenTile;
		public string WallRenderString;
		public string WallColorString;
		public string WallTileColor;
		public string WallDetailColor;
		public string WatchRenderString;
		public string WatchColorString;
		public string WatchTileColor;
		public string WatchDetailColor;
		public string WatchTile;
		public string MaterialClaim;
	}
}
