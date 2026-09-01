using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCreedRules
	{

		/// <summary>
		/// Weight one candidate creed draws from the realm's standing with its faction. Vanilla's
		/// own attitude tiers, mirrored from <see cref="KingdomExileRules"/> so a creed's pull
		/// steps exactly where the reputation screen says it should.
		/// </summary>
		/// <param name="Standing">The realm's standing with that faction, on the vanilla scale.</param>
		/// <returns>Zero for a faction that dislikes the realm — its people do not walk here.</returns>
		public static int StandingWeight(int Standing)
		{
			if (Standing <= KingdomExileRules.RegardDisliked)
			{
				return 0;
			}
			if (Standing < KingdomExileRules.RegardLiked)
			{
				return 10;
			}
			if (Standing < KingdomExileRules.RegardLoved)
			{
				return 25;
			}
			return 40;
		}

		/// <summary>
		/// How strongly one creed pulls at an arriving settler: what the realm's books say about
		/// that faction, how many of its people are already here, and whether the founder named it.
		/// </summary>
		/// <param name="Standing">The realm's standing with the creed's faction.</param>
		/// <param name="AlreadyHere">Residents of this city already holding the creed. Negative
		/// reads as none.</param>
		/// <param name="Declared">True if the founder declared this creed the realm's own.</param>
		/// <returns>A weight to compete against <see cref="OrdinaryWeight"/>; never negative.</returns>
		public static int CreedWeight(int Standing, int AlreadyHere, bool Declared)
		{
			int weight = StandingWeight(Standing);
			if (AlreadyHere > 0)
			{
				weight += AlreadyHere * AffinityPerResident;
			}
			if (Declared)
			{
				weight += DeclaredBonus;
			}
			return weight;
		}

		/// <summary>
		/// The whole draw an arriving settler is rolled against: every candidate's weight plus the
		/// ordinary settler's.
		/// </summary>
		/// <param name="Weights">Candidate weights, as from <see cref="CreedWeight"/>. Null reads
		/// as no candidates.</param>
		/// <returns>At least <see cref="OrdinaryWeight"/>, so the roll is never over an empty
		/// range.</returns>
		public static int TotalWeight(int[] Weights)
		{
			int total = OrdinaryWeight;
			if (Weights != null)
			{
				for (int i = 0; i < Weights.Length; i++)
				{
					if (Weights[i] > 0)
					{
						total += Weights[i];
					}
				}
			}
			return total;
		}

		/// <summary>
		/// Draws one arriving settler's creed.
		/// <para>
		/// The ordinary settler sits at the bottom of the range, so a roll below
		/// <see cref="OrdinaryWeight"/> always means "holds with nobody in particular" whatever the
		/// candidates are. Non-positive weights are skipped rather than treated as tiny: a faction
		/// that dislikes the realm sends nobody, not somebody rarely.
		/// </para>
		/// </summary>
		/// <param name="Weights">Candidate weights, parallel to the caller's candidate names.</param>
		/// <param name="Roll">A roll in <c>[0, TotalWeight(Weights))</c>. Out-of-range rolls read
		/// as ordinary rather than throwing, because the caller's roll comes from the engine.</param>
		/// <returns>The index of the drawn creed, or -1 for an ordinary settler.</returns>
		public static int DrawCreed(int[] Weights, int Roll)
		{
			if (Weights == null || Roll < OrdinaryWeight)
			{
				return -1;
			}
			int cursor = OrdinaryWeight;
			for (int i = 0; i < Weights.Length; i++)
			{
				if (Weights[i] <= 0)
				{
					continue;
				}
				cursor += Weights[i];
				if (Roll < cursor)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// The creed of a city, read off its roll of residents.
		/// <para>
		/// A city has a creed when one is held by at least <see cref="MinBelievers"/> people, by at
		/// least <see cref="DominantSharePercent"/> of everyone living there, and by strictly more
		/// people than any rival. Anything else is a city of mixed people, which is not a failure
		/// state — most cities are that, and nothing in this file happens to them.
		/// </para>
		/// <para>
		/// A tie has no winner on purpose. It is the honest answer, and it is also the only answer
		/// that does not depend on the order a dictionary happens to enumerate in.
		/// </para>
		/// </summary>
		/// <param name="Counts">Creed name to residents holding it. Null, empty, and non-positive
		/// entries all read as nobody.</param>
		/// <param name="Population">Everyone living in the city, believers and ordinary alike.</param>
		/// <returns>The dominant creed's name, or null for a city of mixed people.</returns>
		public static string DominantCreed(IDictionary<string, int> Counts, int Population)
		{
			if (Counts == null || Counts.Count == 0 || Population <= 0)
			{
				return null;
			}
			string leader = null;
			int most = 0;
			int second = 0;
			foreach (KeyValuePair<string, int> entry in Counts)
			{
				if (string.IsNullOrEmpty(entry.Key) || entry.Value <= 0)
				{
					continue;
				}
				if (entry.Value > most)
				{
					second = most;
					most = entry.Value;
					leader = entry.Key;
				}
				else if (entry.Value > second)
				{
					second = entry.Value;
				}
			}
			if (leader == null || most < MinBelievers || most <= second)
			{
				return null;
			}
			return (most * 100 >= Population * DominantSharePercent) ? leader : null;
		}
	}
}
