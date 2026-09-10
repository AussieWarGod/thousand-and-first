using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Distinct-ground reservation, kept pure so it can be driven without a game.
	/// <para>
	/// The defect this exists for was real: a "first bare cell in the zone" helper called three
	/// times in a row, before anything had been placed in any of them, returns the SAME cell three
	/// times. Three cases then shared one patch of ground, and each would have inherited the
	/// previous one's hold &mdash; which, for a suite about totals, silently changes the number
	/// under test. So a reservation is remembered the moment it is made, before the ground is
	/// used for anything, and the picks are asserted distinct before any case runs.
	/// </para>
	/// </summary>
	internal static class KingdomDepositOverflowReservation
	{
		/// <summary>The first candidate in scan order that nothing has reserved yet, appended to
		/// <paramref name="Taken"/> so the next call cannot return it again. False when every
		/// candidate is already spoken for; <paramref name="Reserved"/> is then nothing.</summary>
		internal static bool TryReserve(IList<long> Taken, IEnumerable<long> Candidates,
			out long Reserved)
		{
			Reserved = 0L;
			if (Taken == null || Candidates == null)
			{
				return false;
			}
			foreach (long candidate in Candidates)
			{
				if (Taken.Contains(candidate))
				{
					continue;
				}
				Taken.Add(candidate);
				Reserved = candidate;
				return true;
			}
			return false;
		}

		/// <summary>Whether every reservation names different ground. This is the assertion the
		/// original defect would have failed: three picks, one cell.</summary>
		internal static bool AllDistinct(IReadOnlyList<long> Reservations)
		{
			if (Reservations == null)
			{
				return false;
			}
			for (int i = 0; i < Reservations.Count; i++)
				for (int j = i + 1; j < Reservations.Count; j++)
					if (Reservations[i] == Reservations[j])
					{
						return false;
					}
			return true;
		}

		/// <summary>
		/// One cell as a single comparable number, WITHIN ONE ZONE. Reservations never span zones
		/// &mdash; the caller proves the cell belongs to its own frozen zone object before keying
		/// it &mdash; so no zone identity is folded in here.
		/// <para>
		/// Deliberately no hash. A key built from <c>ZoneID.GetHashCode()</c> could not honestly
		/// claim that two zones never collide, and a reservation that collides hands two cases the
		/// same ground, which is the very defect this helper exists for. Within one zone the
		/// coordinates ARE the identity, and this packing is injective over every coordinate a
		/// zone has.
		/// </para>
		/// </summary>
		internal static long Key(int X, int Y)
		{
			return ((long)X << 32) | (uint)Y;
		}
	}
}
