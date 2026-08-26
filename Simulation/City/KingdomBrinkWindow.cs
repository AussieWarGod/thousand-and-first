using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One brink window as a row carries it: whether one stands, the tick the line was crossed,
	/// and the tick the word went out.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;1.2(d) moves these off the settler's property bag, because a
	/// row is what survives a zone going to disk and a property bag is not. The three fields are
	/// exactly what <c>BrinkRecord</c> is built from, and the reason <b>stands</b> is kept apart
	/// from the warned tick is <c>KingdomBrink</c>'s own: "recorded, and the word has not gone out
	/// yet" and "no brink" are different states rather than the same zero.
	/// </para>
	/// <para>
	/// Seventeen declared bytes; two of them plus a creed reference and a channel are the brink
	/// half of the ninety-six &sect;0.0(c) budgets the resident row.
	/// </para>
	/// </summary>
	internal readonly struct KingdomBrinkWindow
	{
		internal readonly bool Stands;

		internal readonly long ReachedTick;

		/// <summary>The anchor of the window. <c>KingdomBrinkRules.Unwarned</c> until the word
		/// goes out; a brink at that value has no deadline, however old it is.</summary>
		internal readonly long WarnedTick;

		internal KingdomBrinkWindow(bool stands, long reachedTick, long warnedTick)
		{
			Stands = stands;
			ReachedTick = stands ? reachedTick : 0L;
			WarnedTick = stands ? warnedTick : 0L;
		}

		/// <summary>No brink. What every row carries nearly always.</summary>
		internal static KingdomBrinkWindow None
		{
			get { return new KingdomBrinkWindow(false, 0L, 0L); }
		}

		internal KingdomBrinkWindow WithWarned(long warnedTick)
		{
			return new KingdomBrinkWindow(Stands, ReachedTick, warnedTick);
		}
	}
}
