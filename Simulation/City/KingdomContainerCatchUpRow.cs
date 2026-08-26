namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One physical civic container as a ground survey measured it. Room and contents are kept
	/// separately because the signed city debt decides which one is executable. One row is one
	/// MEDIUM reify unit, regardless of how many drams or servings that touch can move.
	/// </summary>
	internal readonly struct KingdomContainerCatchUpRow
	{
		internal readonly int ContainerId;
		internal readonly int DedicationOrdinal;
		internal readonly KingdomStockKind Kind;
		internal readonly bool Visible;
		internal readonly int Room;
		internal readonly int Contents;

		internal KingdomContainerCatchUpRow(int containerId, int dedicationOrdinal,
			KingdomStockKind kind, bool visible, int room, int contents)
		{
			ContainerId = containerId;
			DedicationOrdinal = dedicationOrdinal;
			Kind = kind;
			Visible = visible;
			Room = room;
			Contents = contents;
		}
	}
}
