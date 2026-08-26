using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What the advancement driver needs of a model, and nothing more.
	/// <para>
	/// One propose pass (every row emits its next candidate tick), one apply pass (every row
	/// integrates to the chosen tick), and one closed-form escape to the fixed point for the
	/// overflow case. LIVING-CITY-ARCHITECTURE &sect;2.3.
	/// </para>
	/// <para>
	/// A structural interface rather than a base class, and pure: an implementation may not read a
	/// clock — <c>nowTick</c> arrives as <c>horizonTick</c> — and may not touch an engine type, which
	/// is what <c>KingdomComputeSeam</c> checks when the model crosses the executor.
	/// </para>
	/// </summary>
	internal interface IKingdomAdvanceable<TState>
	{
		/// <summary>The live <c>R</c> of LIVING-CITY-ARCHITECTURE &sect;0.0(f). The receipt checks
		/// row-visits against this, never against 14,848.</summary>
		int RowCount(TState state);

		/// <summary>
		/// The earliest tick at which a rate changes, strictly after <paramref name="fromTick"/> and
		/// at or before <paramref name="horizonTick"/>, or <see cref="KingdomBreakpoint.None"/>.
		/// Every candidate is computed — "the tick at which this will happen at the current rates" —
		/// and the minimum taken. Nothing here searches, and nothing here loops a day at a time.
		/// </summary>
		bool TryProposeNext(TState state, long fromTick, long horizonTick, out KingdomBreakpoint breakpoint, out KingdomCityFault fault);

		/// <summary>Integrates every row linearly to the breakpoint's tick and applies it.</summary>
		bool TryApply(TState state, KingdomBreakpoint breakpoint, out TState next, out KingdomCityFault fault);

		/// <summary>
		/// The honest overflow of LIVING-CITY-ARCHITECTURE &sect;2.3: jump to the equilibrium the
		/// model converges on and date the remainder as settled. Not a forgiveness cap in disguise
		/// — the same convergence reached by arithmetic instead of by steps.
		/// </summary>
		bool TryJumpToFixedPoint(TState state, long throughTick, out TState next, out KingdomCityFault fault);
	}
}
