namespace ThousandAndFirst
{
	/// <summary>
	/// One lot-relative coordinate, described ONLY by the architecture that owns it.
	/// <para>
	/// Every field here is derived from the exact architecture owner and the exact lot-marked
	/// components standing on the coordinate. Aggregate cell predicates are deliberately absent: the
	/// engine's own <c>Cell.IsPassable()</c> and <c>Cell.HasOpenLiquidVolume()</c> scan every object
	/// in the cell, so a wandering settler or an unrelated puddle would change a digest that claims
	/// to measure architecture. An inhabited ordinary commission must stay comparable with an empty
	/// gallery staging of the same design.
	/// </para>
	/// </summary>
	public sealed class KingdomRealizedCellFact
	{
		public int X;
		public int Y;

		/// <summary>The architecture owner's own behaviour root stands here.</summary>
		public bool Owner;

		/// <summary>How many exact lot components stand here.</summary>
		public int Components;

		/// <summary>A non-door component here whose blueprint declares solid physics.</summary>
		public bool Blocking;

		/// <summary>A component here the engine reports as a door.</summary>
		public bool Door;

		/// <summary>A component here carrying liquid of its own. Never a puddle on the ground.</summary>
		public bool Liquid;
	}

	/// <summary>
	/// One architecture-owned object inside the lot rect: the owner, or an exact component carrying
	/// this lot's complete marking. Coordinates are lot-relative so two lawful builds of the same
	/// design at different world positions still compare.
	/// </summary>
	public sealed class KingdomRealizedObjectFact
	{
		public int X;
		public int Y;
		public string Blueprint;

		/// <summary>Authored slot name from the stamper's component marking.</summary>
		public string Slot;

		/// <summary>Authored layer ordinal. Stamped as an int property, never as text.</summary>
		public int Layer;

		/// <summary>Stateful anchor role, or null when the placement declares none.</summary>
		public string Anchor;

		/// <summary>
		/// This object's component authority was reproved against its own owner's frozen receipt at
		/// capture time.
		/// <para>
		/// The raw component token is deliberately NOT recorded. It hashes the lot id, and an
		/// ordinary commission and a gallery staging necessarily hold different lot ids, so carrying
		/// the token would make two identical realized builds unable to match by construction. Lot
		/// relationship is a validity precondition, not cross-path identity: the token is proved and
		/// then left out of the comparison.
		/// </para>
		/// </summary>
		public bool AuthorityProved;

		/// <summary>Stamped ExistingAuthority: a bound pre-existing relic, never created here.</summary>
		public bool Existing;

		/// <summary>True for the one architecture owner, false for a component.</summary>
		public bool Owner;

		/// <summary>
		/// This object's LIVE physics part exists.
		/// <para>
		/// Blueprint physics is what the design declares; live physics is what stands there. A
		/// component whose Physics part was stripped after staging blocks nothing any more, and a
		/// digest that reads only the blueprint would call it identical to the intact build.
		/// </para>
		/// </summary>
		public bool PhysicsPresent;

		/// <summary>Live solidity, read off the object's own physics part.</summary>
		public bool Solid;

		/// <summary>What the blueprint declares. Kept beside the live fact so the two can disagree.</summary>
		public bool BlueprintSolid;

		public bool Door;

		/// <summary>Canonical description of this object's own liquid, or null when it holds none.</summary>
		public string Liquid;

		public string Tile;
		public string RenderString;
		public string ColorString;
		public string DetailColor;
		public string TileColor;
		public int RenderLayer;

		/// <summary>Road/path state stamped on this piece. A different path is a different result.</summary>
		public int PathState;
	}
}
