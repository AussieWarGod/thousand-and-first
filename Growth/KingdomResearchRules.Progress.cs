using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomResearchRules
	{
		// --- The shelf (verdict; §5.4) ---------------------------------------------------------

		/// <summary>Shelved subjects a city remembers. The ninth shelving drops the least advanced,
		/// named once.</summary>
		public const int ShelfRows = 8;

		/// <summary>
		/// Which shelved subject a ninth shelving pushes off, or null when there is room. Least
		/// advanced first; ties break on key, ascending, so the same city on the same save always
		/// forgets the same row.
		/// </summary>
		/// <param name="Shelf">Key to accrued ticks. Null or short reads as room to spare.</param>
		public static string Crowded(IDictionary<string, int> Shelf)
		{
			if (Shelf == null || Shelf.Count < ShelfRows)
			{
				return null;
			}
			string worst = null;
			int worstTicks = int.MaxValue;
			foreach (KeyValuePair<string, int> row in Shelf)
			{
				if (row.Key == null)
				{
					continue;
				}
				if (worst == null || row.Value < worstTicks || (row.Value == worstTicks && string.CompareOrdinal(row.Key, worst) < 0))
				{
					worst = row.Key;
					worstTicks = row.Value;
				}
			}
			return worst;
		}

		// --- The worker-method lane (§8.2) -----------------------------------------------------

		/// <summary>What method may ever be worth. The cap is on the LANE, not on the sum of the
		/// grants: the tree's tail is never a linear damage multiplier.</summary>
		public const int MaxMethodPercent = 150;

		/// <summary>
		/// The method factor a realm's held nodes are worth, as a percent to multiply a work's
		/// output by. Never under 100 &mdash; knowledge is not a tax.
		/// </summary>
		public static int MethodPercent(int SumEfficiency)
		{
			int method = 100 + ((SumEfficiency > 0) ? SumEfficiency : 0);
			return (method > MaxMethodPercent) ? MaxMethodPercent : method;
		}

		/// <summary>Every <see cref="EffectEfficiency"/> grant in a set of held nodes, summed.</summary>
		public static int Efficiency(IEnumerable<ResearchEffect> Held)
		{
			int sum = 0;
			if (Held != null)
			{
				foreach (ResearchEffect effect in Held)
				{
					if (effect.Kind == EffectEfficiency)
					{
						sum += effect.Amount;
					}
				}
			}
			return sum;
		}

		// --- The citizen ceiling (§8.3; RR8; Addendum 22 E2) -----------------------------------

		/// <summary>The most a city may ever teach one citizen in one stat.</summary>
		public const int MaxHeadroomPerStat = 3;

		/// <summary>
		/// The most a city may ever teach one citizen in Intelligence, whatever its nodes say.
		/// <para>
		/// Addendum 22 E2: <i>"schooling may raise the citizen Int cap by +1, never stacking."</i>
		/// Enforced here rather than in the authoring, so a second node that grants Intelligence
		/// headroom &mdash; ours, or a third party's &mdash; cannot open the loop by addition.
		/// This is the clause that stops research raising the ceiling on the stat that gates
		/// research faster than the world can supply the minds.
		/// </para>
		/// </summary>
		public const int MaxHeadroomIntelligence = 1;

		/// <summary>The most a city may teach one citizen across every stat at once.</summary>
		public const int MaxHeadroomTotal = 6;

		/// <summary>The vanilla stat names this build trains, folded. A stat outside this list is
		/// carried as somebody else's vocabulary and trains nobody.</summary>
		public static readonly string[] TrainedStats = new string[4] { "strength", "intelligence", "toughness", "agility" };

		/// <summary>The ceiling one stat's headroom may ever reach.</summary>
		public static int MaxHeadroomFor(string Stat)
		{
			return (Fold(Stat) == "intelligence") ? MaxHeadroomIntelligence : MaxHeadroomPerStat;
		}

		/// <summary>
		/// How far above what they walked in with this city may teach one citizen in one stat.
		/// <para>
		/// Summed over the held nodes' <c>statcap:</c> grants, <c>any</c> counting toward every
		/// trained stat, then clamped by <see cref="MaxHeadroomFor"/> and by
		/// <see cref="MaxHeadroomTotal"/> across all of them. A citizen never exceeds what they
		/// walked in with plus what the city taught them.
		/// </para>
		/// </summary>
		/// <param name="Held">Effects of every node the city holds. Null reads as none.</param>
		/// <param name="Stat">The stat, case folded away.</param>
		public static int Headroom(IEnumerable<ResearchEffect> Held, string Stat)
		{
			string stat = Fold(Stat);
			if (stat == null)
			{
				return 0;
			}
			int wanted = 0;
			int total = 0;
			if (Held != null)
			{
				foreach (ResearchEffect effect in Held)
				{
					if (effect.Kind != EffectStatCap || effect.Amount <= 0)
					{
						continue;
					}
					string named = Fold(effect.Stat);
					if (named == StatAny || named == stat)
					{
						wanted += effect.Amount;
					}
					total += effect.Amount;
				}
			}
			int capped = Clamp(wanted, 0, MaxHeadroomFor(stat));
			// The total cap binds the stat that asked, not the sum of the grants: a city with six
			// points of headroom spread over four stats still teaches each of them only its own.
			return (total > MaxHeadroomTotal) ? Clamp(capped, 0, MaxHeadroomTotal) : capped;
		}

		/// <summary>
		/// The highest this city may ever train one citizen's stat to: what they walked in with,
		/// plus what the city knows how to teach. Ours, enforced in our own training code &mdash;
		/// vanilla's <c>Statistic.Max</c> is a static dictionary keyed by stat NAME, so writing it
		/// would raise the ceiling for every creature in Qud, the player included (RR8).
		/// </summary>
		public static int Ceiling(int BaseAtJoining, int Headroom)
		{
			int headroom = (Headroom > 0) ? Headroom : 0;
			return ((BaseAtJoining > 0) ? BaseAtJoining : 0) + headroom;
		}

		/// <summary>
		/// What one point of practice leaves a citizen's stat at. Never above the ceiling, never
		/// below where they already stand: training is a thing the city does TO a number and never
		/// a thing that takes one away (RR11).
		/// </summary>
		public static int TrainedValue(int Current, int BaseAtJoining, int Headroom)
		{
			int ceiling = Ceiling(BaseAtJoining, Headroom);
			if (Current >= ceiling)
			{
				return Current;
			}
			return Current + 1;
		}

		/// <summary>Whether this city could teach this citizen anything at all in this stat.</summary>
		public static bool CanTrain(int Current, int BaseAtJoining, int Headroom)
		{
			return Current < Ceiling(BaseAtJoining, Headroom);
		}

		// --- Distance and prose (RR4: no percentage, no bar, no ETA) ---------------------------

		/// <summary>
		/// How many things stand between a city and a node it can see. Counted the way the map
		/// already counts a design's gates, in the order they are judged: the tier, then the craft
		/// rung, then each unmet requirement.
		/// </summary>
		public static int Distance(bool TierShort, bool TechShort, int MissingRequirements)
		{
			int distance = 0;
			if (TierShort)
			{
				distance++;
			}
			if (TechShort)
			{
				distance++;
			}
			if (MissingRequirements > 0)
			{
				distance += MissingRequirements;
			}
			return distance;
		}

		/// <summary>
		/// How far off, in words. <i>Begun</i> sits between <i>one thing away</i> and <i>within
		/// reach</i>: a node the keepers have started is nearer than one they have not, and there
		/// is still no number attached to it (RR4).
		/// </summary>
		public static string Reach(int Distance, bool Begun)
		{
			if (Distance <= 0)
			{
				return Begun ? "{{W|begun}}" : "{{G|within reach}}";
			}
			return KingdomTechMapRules.Reach(Distance);
		}

		// --- The visibility law, as arithmetic over one token ----------------------------------

		/// <summary>
		/// Whether one requirement token leaves the founder any road they can SEE.
		/// <para>
		/// The whole visibility law's decision, in one place and tabled, because it is the rule
		/// every surface in the mod leans on. A token has arms (<c>a|b</c>, any one satisfying it).
		/// An arm that is not a <c>node:</c> key is always a road the founder can see &mdash; a disk
		/// to carry home, a machine to certify, people to take in. An arm that IS a node key is a
		/// road only if they have heard of that node. A token whose every arm is a node they have
		/// never heard of is a requirement pointing at nothing they could go and do, and the design
		/// behind it is ABSENT: not greyed, not counted, not named. That is vanilla's own rule for
		/// an unknown tinker recipe, and it is what makes "cannot unlock" and "have not discovered"
		/// the same absence of a row.
		/// </para>
		/// </summary>
		/// <param name="Token">One requirement token, arms and all.</param>
		/// <param name="DiscoveredKeys">Node keys the founder has heard of. Null reads as none.</param>
		public static bool AnyRoadVisible(string Token, ICollection<string> DiscoveredKeys)
		{
			if (string.IsNullOrEmpty(Token))
			{
				return true;
			}
			bool anyNodeArm = false;
			string[] arms = Token.Split(KingdomZoningRules.RosterSeparator);
			for (int i = 0; i < arms.Length; i++)
			{
				if (KingdomZoningRules.KindOf(arms[i]) != KindNode)
				{
					return true;
				}
				anyNodeArm = true;
				string name = KingdomZoningRules.NameOf(arms[i]);
				if (name != null && DiscoveredKeys != null && DiscoveredKeys.Contains(name))
				{
					return true;
				}
			}
			return !anyNodeArm;
		}

	}
}
