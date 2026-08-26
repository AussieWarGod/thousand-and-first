using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One dated moment a rate changes, and which row owns it.</summary>
	internal readonly struct KingdomBreakpoint
	{
		internal readonly KingdomBreakpointKind Kind;

		internal readonly long Tick;

		/// <summary>The row that proposed it, for the deterministic tie-break and for the telling
		/// layer. Negative for a breakpoint no single row owns.</summary>
		internal readonly int RowIndex;

		internal KingdomBreakpoint(KingdomBreakpointKind kind, long tick, int rowIndex)
		{
			Kind = kind;
			Tick = tick;
			RowIndex = rowIndex;
		}

		internal static KingdomBreakpoint Horizon(long tick)
		{
			return new KingdomBreakpoint(KingdomBreakpointKind.Horizon, tick, -1);
		}

		internal static KingdomBreakpoint None
		{
			get { return new KingdomBreakpoint(KingdomBreakpointKind.None, 0L, -1); }
		}
	}
}
