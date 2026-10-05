namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// The scalar founder marks an improvement handover carries from the old work to the new
		/// one, read off the engine objects by the adapters in
		/// <c>Growth/KingdomUpgrade.22.CarryMarks.cs</c>. Wear, the plot rectangle and the plot id
		/// stay with their own carriers; only the marks CarryMarks writes and ExactCarriedMarks
		/// checks are here, so the writer and the checker read one table and cannot drift (#283).
		/// </summary>
		public readonly struct FounderMarks
		{
			public readonly bool Larder;
			public readonly bool Stores;
			public readonly bool Certified;
			public readonly bool Adopted;
			public readonly bool Yielding;
			public readonly string GivenName;

			public FounderMarks(bool Larder, bool Stores, bool Certified, bool Adopted,
				bool Yielding, string GivenName)
			{
				this.Larder = Larder;
				this.Stores = Stores;
				this.Certified = Certified;
				this.Adopted = Adopted;
				this.Yielding = Yielding;
				this.GivenName = GivenName;
			}
		}

		/// <summary>
		/// The marks a handover publishes on the successor. A larder dedication needs a successor
		/// that holds things and a stores dedication one that holds liquid; every other mark is
		/// carried as the founder left it. Yielding is carried exactly as the legacy growth lane
		/// carries it (<c>Growth/KingdomPlot2.22.Growth.cs</c>): a work staked inside the heart's
		/// survey promised to yield to the heart, and an authored renovation does not end that
		/// promise.
		/// </summary>
		public static FounderMarks CarryFounderMarks(FounderMarks Predecessor, bool HasInventory,
			bool HasLiquid)
		{
			return new FounderMarks(Predecessor.Larder && HasInventory,
				Predecessor.Stores && HasLiquid, Predecessor.Certified, Predecessor.Adopted,
				Predecessor.Yielding,
				string.IsNullOrEmpty(Predecessor.GivenName) ? null : Predecessor.GivenName);
		}

		/// <summary>
		/// True when every mark the predecessor carried settled on the successor. One-directional:
		/// a successor may carry a mark its predecessor lacked, so a yielding successor of a
		/// non-yielding predecessor is not a failure.
		/// </summary>
		public static bool FounderMarksSettled(FounderMarks Predecessor, FounderMarks Successor,
			bool HasInventory, bool HasLiquid)
		{
			return FounderMarksSettledExceptYielding(Predecessor, Successor, HasInventory, HasLiquid)
				&& (!Predecessor.Yielding || Successor.Yielding);
		}

		/// <summary><see cref="FounderMarksSettled"/> without the yielding clause: the exact
		/// shape a #283 handover left behind, every other mark settled and only yielding
		/// dropped.</summary>
		public static bool FounderMarksSettledExceptYielding(FounderMarks Predecessor,
			FounderMarks Successor, bool HasInventory, bool HasLiquid)
		{
			return (!Predecessor.Larder || HasInventory && Successor.Larder)
				&& (!Predecessor.Stores || HasLiquid && Successor.Stores)
				&& (!Predecessor.Certified || Successor.Certified)
				&& (string.IsNullOrEmpty(Predecessor.GivenName)
					|| Successor.GivenName == Predecessor.GivenName)
				&& (!Predecessor.Adopted || Successor.Adopted);
		}
	}
}
