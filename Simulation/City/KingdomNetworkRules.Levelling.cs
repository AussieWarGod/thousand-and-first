using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One store's fill state for the zone-levelling pass in <c>KingdomNetworks.Attend</c>, with
	/// the one fact the selection must never look past: whether pouring fresh water into it is
	/// lawful at all.
	/// <para>
	/// Deliberately not a <c>LiquidVolume</c>. Which store gives and which receives is pure
	/// arithmetic over three numbers; carrying the engine object into the selection would smuggle
	/// engine state into what STANDARDS keeps engine-free and total over representable input, and
	/// would put the purity question behind a seam this repository's tests cannot see through.
	/// </para>
	/// </summary>
	internal readonly struct KingdomNetworkStoreLevel
	{
		internal readonly int Volume;

		internal readonly int MaxVolume;

		/// <summary>Whatever the caller's own <c>KingdomLiquids.CanReceiveFreshWater</c> read of
		/// the real store said &mdash; the same predicate every other water lane already checks
		/// before a <c>Fill</c>, reused rather than reinvented (Addendum 13's mesh condition).</summary>
		internal readonly bool Receivable;

		internal KingdomNetworkStoreLevel(int volume, int maxVolume, bool receivable)
		{
			Volume = volume;
			MaxVolume = maxVolume;
			Receivable = receivable;
		}
	}

	/// <summary>
	/// The zone-levelling pass's choice of who gives and who takes, split out of
	/// <c>KingdomNetworks.Attend</c> so the choice itself is provable without a live zone.
	/// </summary>
	internal static partial class KingdomNetworkRules
	{
		/// <summary>
		/// Picks the levelling pair the way <c>KingdomNetworks.Attend</c> pours: the fullest
		/// nonempty pure-fresh store by fill fraction gives, and the emptiest store BY FILL
		/// FRACTION AMONG THOSE THAT MAY LAWFULLY RECEIVE FRESH WATER takes. A brine or otherwise
		/// incompatible store is neither donor nor landing candidate.
		/// <para>
		/// A network whose only stores are unreceivable, that has fewer than two eligible stores,
		/// or where the chosen giver is not actually fuller than the chosen taker by fill fraction,
		/// picks nothing at all &mdash; the caller must not report a fill that never happened.
		/// </para>
		/// </summary>
		/// <param name="stores">One entry per surveyed store, in survey order. A default entry
		/// (zero <see cref="KingdomNetworkStoreLevel.MaxVolume"/>) stands in for a null store and
		/// is skipped exactly as a null store is.</param>
		/// <param name="count">How many leading entries of <paramref name="stores"/> are live.</param>
		internal static bool TrySelectLevellingPair(KingdomNetworkStoreLevel[] stores, int count,
			out int fullestIndex, out int emptiestIndex)
		{
			fullestIndex = -1;
			emptiestIndex = -1;
			if (stores == null || count < 2 || count > stores.Length)
			{
				return false;
			}
			for (int i = 0; i < count; i++)
			{
				KingdomNetworkStoreLevel store = stores[i];
				if (store.MaxVolume == 0 && store.Volume == 0)
				{
					continue;
				}
				if (store.MaxVolume <= 0 || store.Volume < 0 || store.Volume > store.MaxVolume)
				{
					fullestIndex = -1; emptiestIndex = -1; return false;
				}
				// The caller's predicate is true only for an empty vessel or pure fresh water.
				// A nonempty donor must therefore also be receivable; otherwise Drain followed by
				// Fill("water") would transmute brine or another liquid into fresh water.
				if (store.Receivable && store.Volume > 0 && (fullestIndex < 0
					|| (long)store.Volume * stores[fullestIndex].MaxVolume
					> (long)stores[fullestIndex].Volume * store.MaxVolume))
				{
					fullestIndex = i;
				}
				// The emptiest candidate is chosen only among receivable stores: an unreceivable
				// store is invisible to this half of the comparison no matter how empty it reads.
				if (store.Receivable && (emptiestIndex < 0
					|| (long)store.Volume * stores[emptiestIndex].MaxVolume
						< (long)stores[emptiestIndex].Volume * store.MaxVolume))
				{
					emptiestIndex = i;
				}
			}
			if (fullestIndex < 0 || emptiestIndex < 0 || fullestIndex == emptiestIndex)
			{
				// Never hand back a half-found index: a caller that skipped the bool check must
				// not be able to mistake leftover loop state for a real pick.
				fullestIndex = -1;
				emptiestIndex = -1;
				return false;
			}
			KingdomNetworkStoreLevel fullest = stores[fullestIndex];
			KingdomNetworkStoreLevel emptiest = stores[emptiestIndex];
			// Levelling only. A main that pushed a cask past the one it was drawing from would be
			// running uphill, and a founder watching it would be right to call it a bug.
			if ((long)fullest.Volume * emptiest.MaxVolume <= (long)emptiest.Volume * fullest.MaxVolume)
			{
				fullestIndex = -1;
				emptiestIndex = -1;
				return false;
			}
			return true;
		}
	}
}
