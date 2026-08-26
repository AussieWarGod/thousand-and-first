using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Breakpoint integration: O(model), never O(days).
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;2.3, generalising the shape
	/// <c>KingdomSubsidenceRules.Slide</c> already uses: <i>between two consecutive breakpoints,
	/// every rate in the model is constant</i>, so integrate linearly to the next breakpoint, apply
	/// it, and repeat — and the number of breakpoints is bounded by the model, not by the elapsed.
	/// </para>
	/// <para>
	/// Pure and engine-free. Nothing here reads a clock: the span arrives as two ticks, which is
	/// also what makes an advancement replayable in a test.
	/// </para>
	/// </summary>
	internal static partial class KingdomAdvanceRules
	{
		/// <summary>
		/// Passes one advancement may spend. LIVING-CITY-ARCHITECTURE &sect;0.0(a) / &sect;2.3: the
		/// 64-cap is belt-and-braces over a loop that already terminates in O(model), and its
		/// overflow is honest rather than silent.
		/// <para>
		/// The last affordable pass is spent on the fixed-point jump rather than on another step,
		/// so row-visits are <c>Steps x 2R</c> and never exceed <c>64 x 2R</c> — the figure the
		/// constitution's table is written against.
		/// </para>
		/// </summary>
		internal const int MaxPasses = KingdomBudgetRules.MaxBreakpoints;

		/// <summary>
		/// Runs a model forward across one span.
		/// <para>
		/// Total over representable input, publishing nothing on a fault: a refusal leaves the
		/// caller holding exactly the state it handed in. An empty span (<c>toTick</c> equal to
		/// <c>fromTick</c>) is a no-op with one closing pass, which is what makes calling this
		/// twice at the same tick idempotent.
		/// </para>
		/// </summary>
		internal static bool TryRun<TState>(
			IKingdomAdvanceable<TState> model,
			TState state,
			long fromTick,
			long toTick,
			out KingdomAdvanceOutcome<TState> outcome,
			out KingdomCityFault fault)
		{
			outcome = default(KingdomAdvanceOutcome<TState>);
			if (model == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			KernelFaultCode kernelFault;
			if (!TickMath.TryValidateAdvance(fromTick, toTick, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			int rows = model.RowCount(state);
			if (rows < 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			long perPass = 2L * rows;

			TState current = state;
			long cursor = fromTick;
			int steps = 0;
			bool overflowed = false;
			while (true)
			{
				KingdomBreakpoint breakpoint;
				if (!model.TryProposeNext(current, cursor, toTick, out breakpoint, out fault))
				{
					return false;
				}
				bool closing = breakpoint.Kind == KingdomBreakpointKind.None
					|| breakpoint.Kind == KingdomBreakpointKind.Horizon
					|| breakpoint.Tick >= toTick;
				bool lastAffordablePass = (steps + 1) >= MaxPasses;
				TState next;
				if (closing)
				{
					if (!model.TryApply(current, KingdomBreakpoint.Horizon(toTick), out next, out fault))
					{
						return false;
					}
					current = next;
					steps++;
					cursor = toTick;
					break;
				}
				if (lastAffordablePass)
				{
					if (!model.TryJumpToFixedPoint(current, toTick, out next, out fault))
					{
						return false;
					}
					current = next;
					steps++;
					cursor = toTick;
					overflowed = true;
					break;
				}
				if (breakpoint.Tick < cursor)
				{
					fault = KingdomCityFault.ClockRegression;
					return false;
				}
				if (!model.TryApply(current, breakpoint, out next, out fault))
				{
					return false;
				}
				current = next;
				cursor = breakpoint.Tick;
				steps++;
			}

			outcome = new KingdomAdvanceOutcome<TState>(current, steps, (long)steps * perPass, cursor, overflowed);
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
