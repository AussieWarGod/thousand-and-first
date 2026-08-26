using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Why a rate changed. LIVING-CITY-ARCHITECTURE &sect;2.3 lists them, and the list is closed:
	/// a breakpoint is any moment a rate can change, and a model with no more structural changes
	/// available has no more breakpoints to spend.
	/// </summary>
	internal enum KingdomBreakpointKind : byte
	{
		/// <summary>No structural change remains inside the span.</summary>
		None = 0,

		/// <summary>A stock hit empty — a solvable linear crossing, computed, never searched.</summary>
		StockEmpty = 1,

		/// <summary>A stock hit full.</summary>
		StockFull = 2,

		/// <summary>A crop's next stage tick.</summary>
		CropStage = 3,

		/// <summary>A periodic clock's next due tick, folded O(1) rather than looped.</summary>
		ClockDue = 4,

		/// <summary>A brink window's expiry.</summary>
		BrinkExpiry = 5,

		/// <summary>A subsidence rung change — <c>KingdomSubsidenceRules.Slide</c>'s own breakpoint.</summary>
		SubsidenceRung = 6,

		/// <summary>A stage change, which changes upkeep and therefore every rate at once.</summary>
		StageChange = 7,

		/// <summary>The end of the span. Not a rate change: the closing integration.</summary>
		Horizon = 8
	}
}
