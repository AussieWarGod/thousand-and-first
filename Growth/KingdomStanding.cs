namespace ThousandAndFirst
{
	/// <summary>
	/// What stands on one cell, as far as clearing it is concerned. The order is the order of
	/// worth: bare ground gives up almost nothing, a marble seam gives up the rarest thing the
	/// settlement can hold. <c>KingdomMaterials.Classify</c> is the engine-coupled half that
	/// reads a real <c>GameObject</c> into one of these.
	/// </summary>
	public enum KingdomStanding
	{
		Nothing = 0,
		Brush = 1,
		Rubble = 2,
		Tree = 3,
		Rock = 4,
		Ruin = 5,
		MarbleSeam = 6
	}
}
