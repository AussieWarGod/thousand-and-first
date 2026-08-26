using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Why a city rule refused.
	/// <para>
	/// The kernel's <c>KernelFaultCode</c> names the arithmetic refusals and this names the model's
	/// own; <see cref="KingdomCityFaults.FromKernel"/> is the one translation between them, so a
	/// tick fault raised in <c>TickMath</c> reaches a caller here without a second arithmetic
	/// implementation being written to avoid the conversion.
	/// </para>
	/// </summary>
	internal enum KingdomCityFault : byte
	{
		None = 0,
		NullArgument = 1,
		RowCapExceeded = 2,
		InvalidIndex = 3,
		InvalidTick = 4,
		ClockRegression = 5,
		ArithmeticOverflow = 6,
		InvalidInterval = 7,
		InvalidRate = 8,
		InvalidCapacity = 9,
		InvalidLegOrder = 10,
		OutsideItinerary = 11,
		StepBudgetExhausted = 12,
		DuplicateBinding = 13,
		UnknownBinding = 14,
		CauseRequired = 15,
		TerminalStanding = 16
	}
}
