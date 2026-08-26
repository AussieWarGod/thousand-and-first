namespace ThousandAndFirst
{

	// Growth-only location contract. Outer lifecycle/carry topology stays byte- and enum-sealed.
	public enum KingdomGrowthLocationKind : byte
	{
		None = 0,
		Absent = 1,
		Escrow = 2,
		Cell = 3,
		Inventory = 4,
		Graveyard = 5
	}
}
