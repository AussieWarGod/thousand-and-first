using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomLodgingRules
	{

		/// <summary>One candidate home, as far as the choice among several eligible ones needs to
		/// know. Everything else (does it meet Needs, does anyone in it conflict) has already
		/// been decided by the time a candidate reaches <see cref="ChooseIndex"/>.</summary>
		public readonly struct LodgingCandidate
		{
			/// <summary>The standing plot's own <c>KingdomPlots.PlotIdProperty</c> &mdash; the
			/// one identity that survives an in-place upgrade, which is why lodging is keyed to
			/// it rather than to the building object itself.</summary>
			public readonly string PlotId;

			public readonly int Capacity;

			public readonly int Occupants;

			public LodgingCandidate(string PlotId, int Capacity, int Occupants)
			{
				this.PlotId = PlotId;
				this.Capacity = Capacity;
				this.Occupants = Occupants;
			}
		}

		/// <summary>
		/// Picks which of several already-eligible homes a resident moves into: fewest free beds
		/// first, so an arrival fills a household that already has people in it before an empty
		/// one is opened, then the plot id itself, ordinal, as a tiebreak that never varies.
		/// </summary>
		/// <param name="Eligible">Candidates that already passed Needs, capacity, and
		/// cohabitation. Order does not matter; every candidate is compared to every other.
		/// </param>
		/// <returns>The winning index, or -1 for an empty list.</returns>
		public static int ChooseIndex(IReadOnlyList<LodgingCandidate> Eligible)
		{
			if (Eligible == null || Eligible.Count == 0)
			{
				return -1;
			}
			int best = 0;
			for (int i = 1; i < Eligible.Count; i++)
			{
				if (ComesBefore(Eligible[i], Eligible[best])) best = i;
			}
			return best;
		}

		/// <summary>
		/// Picks a home for an ordinary, non-luxury resident while holding an eligible fine house
		/// as a last resort. Any otherwise-eligible ordinary home wins before every fine house;
		/// when fine houses are the only eligible shelter, they remain candidates and the resident
		/// is housed. Within the winning class, <see cref="ChooseIndex"/>'s compaction and ordinal
		/// tiebreak are unchanged.
		/// </summary>
		/// <param name="Eligible">Candidates that already passed every hard lodging gate.</param>
		/// <param name="FineHouses">One flag per candidate. A null or mismatched list falls back
		/// to <see cref="ChooseIndex"/> so incomplete advisory metadata can never manufacture
		/// homelessness.</param>
		/// <returns>The winning index in <paramref name="Eligible"/>, or -1 for no candidates.</returns>
		public static int ChooseOrdinaryIndex(IReadOnlyList<LodgingCandidate> Eligible,
			IReadOnlyList<bool> FineHouses)
		{
			if (Eligible == null || Eligible.Count == 0) return -1;
			if (FineHouses == null || FineHouses.Count != Eligible.Count)
				return ChooseIndex(Eligible);
			bool hasOrdinaryHome = false;
			for (int i = 0; i < FineHouses.Count; i++)
			{
				if (!FineHouses[i]) { hasOrdinaryHome = true; break; }
			}
			int best = -1;
			for (int i = 0; i < Eligible.Count; i++)
			{
				if (hasOrdinaryHome && FineHouses[i]) continue;
				if (best < 0 || ComesBefore(Eligible[i], Eligible[best])) best = i;
			}
			return best;
		}

		private static bool ComesBefore(LodgingCandidate Candidate, LodgingCandidate Incumbent)
		{
			int candidateFree = Candidate.Capacity - Candidate.Occupants;
			int incumbentFree = Incumbent.Capacity - Incumbent.Occupants;
			return candidateFree < incumbentFree || (candidateFree == incumbentFree
				&& string.CompareOrdinal(Candidate.PlotId, Incumbent.PlotId) < 0);
		}

		/// <summary>Why a resident who is not housed this pass is not housed, in the order a
		/// founder should hear the reasons: no roof at all outranks a roof nobody fits, which
		/// outranks a roof that fits but is full, which outranks a roof that fits and has room but
		/// holds someone this resident (or who this resident) will not live beside.</summary>
		public enum UnhousedReason
		{
			Housed = 0,
			NoRoofAtAll = 1,
			NeedsUnmet = 2,
			Full = 3,
			Refused = 4,

			/// <summary>Roofs are standing, and every one of them is too far gone to be a roof
			/// (<see cref="IsCondemned"/>).</summary>
			Condemned = 5
		}

		/// <summary>
		/// Reads the coarse facts a pass over the candidate list already has in hand and names
		/// the single reason nobody was eligible.
		/// <para>
		/// The ladder is worst-first, and condemnation sits second because it is the truest
		/// answer whenever it holds: a half-wrecked house answers nobody's needs and has no beds
		/// worth counting, so "the roofs here have fallen in" outranks both, and it is the one
		/// the founder can act on with a mending rather than a new commission.
		/// </para>
		/// </summary>
		/// <param name="AnyRoofAtAll">Any home of any condition stands here.</param>
		/// <param name="AnyMeetsNeeds">Some standing, un-condemned home answers their Needs.</param>
		/// <param name="AnyHasCapacity">Some such home also had a bed free.</param>
		/// <param name="AnyWithoutRefusal">Some such home also held nobody either of them
		/// refuses.</param>
		/// <param name="AnyStanding">Some home here is still sound enough to be a roof. Defaults
		/// to true for a caller that does not judge wear, which is every caller that predates
		/// condemnation.</param>
		public static UnhousedReason Diagnose(bool AnyRoofAtAll, bool AnyMeetsNeeds, bool AnyHasCapacity, bool AnyWithoutRefusal, bool AnyStanding = true)
		{
			if (!AnyRoofAtAll)
			{
				return UnhousedReason.NoRoofAtAll;
			}
			if (!AnyStanding)
			{
				return UnhousedReason.Condemned;
			}
			if (!AnyMeetsNeeds)
			{
				return UnhousedReason.NeedsUnmet;
			}
			if (!AnyHasCapacity)
			{
				return UnhousedReason.Full;
			}
			if (!AnyWithoutRefusal)
			{
				return UnhousedReason.Refused;
			}
			return UnhousedReason.Housed;
		}

	}
}
