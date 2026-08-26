using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The state the engine cannot carry for a work, and nothing else. A growing ground's stage and
	/// next-stage tick, a producer's progress, a power work's charge — one slot, read by kind.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;1.2(c): "a work's row carries state the engine cannot carry
	/// for it, and nothing else." Appearance, name, tile and contents stay on the object; the crop
	/// blueprint travels on the row's shared <c>DesignKey</c> reference rather than as a second
	/// string here, which is what holds this slot to the sixteen bytes &sect;0.0(c) budgets.
	/// </para>
	/// </summary>
	internal readonly struct KingdomWorkRunState
	{
		internal readonly KingdomWorkKind Kind;

		/// <summary>Growth stage for a growing ground; unread for every other kind.</summary>
		internal readonly byte Stage;

		/// <summary>Progress ticks for a producer or refiner, charge for a power work.</summary>
		internal readonly int Progress;

		/// <summary>Next stage tick for a growing ground; a breakpoint, never a countdown.</summary>
		internal readonly long NextTick;

		internal KingdomWorkRunState(KingdomWorkKind kind, byte stage, int progress, long nextTick)
		{
			Kind = kind;
			Stage = stage;
			Progress = progress;
			NextTick = nextTick;
		}
	}
}
