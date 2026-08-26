namespace ThousandAndFirst
{
	/// <summary>
	/// The three processing works, one per refined material. A yard is an ordinary catalogue
	/// design that happens to declare what it refines; this enum only names the three the base
	/// catalogue ships, so the rules can talk about them without reading the registry.
	/// </summary>
	public enum KingdomYard
	{
		/// <summary>Saw-pit and trestles. Timber in, shaped timber out.</summary>
		Sawyer = 0,

		/// <summary>Banker, chisels, and a heap of spoil. Stone (or marble) in, shaped stone out.
		/// </summary>
		Mason = 1,

		/// <summary>Furnace and crucible. Scrap in, worked metal out.</summary>
		Smelter = 2
	}
}
