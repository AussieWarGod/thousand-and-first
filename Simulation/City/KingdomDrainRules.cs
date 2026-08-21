namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One dedicated vessel as the model knows it: what it holds, how much, and when the founder
	/// gave it to the city.
	/// <para>
	/// <see cref="DedicationOrdinal"/> is a stored fact rather than a ranking recomputed from
	/// contents, which is the whole reason LIVING-CITY-ARCHITECTURE &sect;3.9 chooses it: "smallest
	/// first" is not stable, because the smallest remaining vessel changes as the drain proceeds,
	/// so a reload resuming from a slightly different intermediate state can pick a different urn.
	/// </para>
	/// </summary>
	internal readonly struct KingdomVesselRow
	{
		internal readonly int VesselId;

		/// <summary>Dedication order. Lower is older.</summary>
		internal readonly int DedicationOrdinal;

		internal readonly KingdomStockKind Holds;

		internal readonly long Level;

		internal readonly long Capacity;

		/// <summary>
		/// Whether a water vessel holds fresh water. Qud's salt pools are <c>water-600,salt-400</c>,
		/// so a primary-liquid test admits brine; STANDARDS &sect;1 and LIVING-CITY-ARCHITECTURE
		/// &sect;3.9 both rule that a drain may never launder brine into the books.
		/// </summary>
		internal readonly bool Fresh;

		internal KingdomVesselRow(int vesselId, int dedicationOrdinal, KingdomStockKind holds, long level, long capacity, bool fresh)
		{
			VesselId = vesselId;
			DedicationOrdinal = dedicationOrdinal;
			Holds = holds;
			Level = level;
			Capacity = capacity;
			Fresh = fresh;
		}
	}

	/// <summary>
	/// Deficits drain real containers, in a stated deterministic order.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.9, invariant I4: the city drinks from the vessel it was
	/// given first, and the founder's newest dedication is the reserve that outlives everything
	/// else. Legible — a player can plan around <i>the oldest cask goes first</i> — and
	/// deterministic without a draw, which is what makes step 90g reproduce across a reload.
	/// </para>
	/// <para>
	/// Pure and engine-free: the vessel rows arrive measured. The live half — measuring the delta
	/// rather than trusting <c>UseDrams</c>'s return value, STANDARDS &sect;1 — belongs to
	/// <c>KingdomLiquids.Drain</c> at the engine edge, and never here.
	/// </para>
	/// </summary>
	internal static class KingdomDrainRules
	{
		/// <summary>
		/// The order vessels are drained in: oldest dedication first, ties broken on the lower
		/// vessel id. Both keys are stored facts, so the order is stable under a reload and under
		/// any intermediate state the drain passes through.
		/// <para>
		/// A selection sort over a bounded row set, written out rather than delegated, because the
		/// comparison IS the invariant and a comparer indirection would hide it.
		/// </para>
		/// </summary>
		internal static bool TryOrder(KingdomVesselRow[] vessels, int count, int[] order, out KingdomCityFault fault)
		{
			if (vessels == null || order == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > vessels.Length || count > order.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			for (int i = 0; i < count; i++)
			{
				order[i] = i;
			}
			for (int i = 0; i < count; i++)
			{
				int best = i;
				for (int j = i + 1; j < count; j++)
				{
					if (Precedes(vessels[order[j]], vessels[order[best]]))
					{
						best = j;
					}
				}
				if (best != i)
				{
					int swap = order[i];
					order[i] = order[best];
					order[best] = swap;
				}
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Spreads one demand across the vessels in dedication order, taking what each holds until
		/// the demand is met.
		/// <para>
		/// <paramref name="drawn"/> is indexed as <paramref name="vessels"/> is, not as the order
		/// is, so a caller can post the result straight back against its own rows. A vessel of the
		/// wrong kind, or a water vessel that is not fresh, is passed over rather than partly
		/// drained. What the vessels could not cover comes back as
		/// <paramref name="shortfall"/> — named, never silently forgiven, because
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 rules that a mismatch is attributed and told and
		/// never silently repaired.
		/// </para>
		/// </summary>
		internal static bool TryApportion(
			KingdomVesselRow[] vessels,
			int count,
			KingdomStockKind kind,
			long demand,
			long[] drawn,
			out long shortfall,
			out KingdomCityFault fault)
		{
			shortfall = 0L;
			if (vessels == null || drawn == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > vessels.Length || count > drawn.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (demand < 0L)
			{
				fault = KingdomCityFault.InvalidRate;
				return false;
			}
			int[] order = new int[count];
			if (!TryOrder(vessels, count, order, out fault))
			{
				return false;
			}
			for (int i = 0; i < count; i++)
			{
				drawn[i] = 0L;
			}
			long remaining = demand;
			for (int i = 0; i < count && remaining > 0L; i++)
			{
				int index = order[i];
				KingdomVesselRow vessel = vessels[index];
				if (vessel.Holds != kind)
				{
					continue;
				}
				if (kind == KingdomStockKind.Water && !vessel.Fresh)
				{
					continue;
				}
				if (vessel.Level <= 0L)
				{
					continue;
				}
				long take = (vessel.Level < remaining) ? vessel.Level : remaining;
				drawn[index] = take;
				remaining -= take;
			}
			shortfall = remaining;
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool Precedes(KingdomVesselRow candidate, KingdomVesselRow standing)
		{
			if (candidate.DedicationOrdinal != standing.DedicationOrdinal)
			{
				return candidate.DedicationOrdinal < standing.DedicationOrdinal;
			}
			return candidate.VesselId < standing.VesselId;
		}
	}
}
