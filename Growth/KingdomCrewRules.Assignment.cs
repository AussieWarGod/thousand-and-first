using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCrewRules
	{

		// --- Parsing (STANDARDS 6: authorable from XML like everything else) -----------------

		/// <summary>
		/// Reads a <c>CrewNeeds</c> attribute &mdash; <c>strength:16</c>, or
		/// <c>strength:16,intelligence:20</c> for a work that wants both answered for. Identical
		/// language to <see cref="KingdomCatalogueRules.TryParseTally"/>'s own <c>Carries</c>
		/// reading, reused rather than re-implemented: a modder who already knows one
		/// <c>kind:amount</c> attribute on a <c>&lt;building&gt;</c> knows this one.
		/// </summary>
		public static bool TryParseCrewNeeds(string Source, out List<KindAmount> Needs, out string Error)
		{
			return KingdomCatalogueRules.TryParseTally(Source, out Needs, out Error);
		}

		/// <summary>The threshold a parsed <c>CrewNeeds</c> names for one kind, repeats summing
		/// exactly as <see cref="KingdomCatalogueRules.AmountOf"/> already does for <c>Carries</c>.
		/// Zero for a kind the design names no threshold for.</summary>
		public static int ThresholdOf(List<KindAmount> Needs, string Kind)
		{
			return KingdomCatalogueRules.AmountOf(Needs, Kind);
		}

		// --- Effectiveness ---------------------------------------------------------------------

		/// <summary>
		/// How much of full output a work manages from the capability its crew actually brought,
		/// 0 to 100. A threshold of zero (no <c>CrewNeeds</c>, or a kind never met) always reads
		/// 100: capability that was never asked for cannot be found wanting. Below the threshold
		/// the work never drops under <see cref="MinCapabilityEffectiveness"/>, met or not &mdash;
		/// a scaffold crewed with willing but unskilled hands runs slow, never stops
		/// (BUILDING-CATALOGUE-BRIEF.md: "damaged works run reduced... never die"; the same law
		/// for a shortfall that was never damage in the first place).
		/// </summary>
		public static int CapabilityEffectiveness(int BestCapability, int Threshold)
		{
			if (Threshold <= 0)
			{
				return 100;
			}
			if (BestCapability >= Threshold)
			{
				return 100;
			}
			int scaled = (BestCapability > 0) ? (BestCapability * 100 / Threshold) : 0;
			return (scaled < MinCapabilityEffectiveness) ? MinCapabilityEffectiveness : scaled;
		}

		/// <summary>The pace a work actually manages: whichever of headcount and capability bites
		/// harder governs, so a fully-staffed but unskilled crew and a skilled but short-handed one
		/// both read exactly as slow as their own real shortfall, never worse for having two
		/// reasons at once.</summary>
		public static int CombinedEffectiveness(int HeadcountEffectiveness, int CapabilityEffectiveness)
		{
			return (HeadcountEffectiveness < CapabilityEffectiveness) ? HeadcountEffectiveness : CapabilityEffectiveness;
		}

		// --- Assignment --------------------------------------------------------------------

		/// <summary>
		/// Allocates a pool of settlers to a priority-ordered list of works, exactly as
		/// <see cref="KingdomRules.AssignCrew"/> allocates plain headcount (same threshold /
		/// scaled semantics, same "spend once, priority order" shape) &mdash; but by identity:
		/// a demand naming a capability kind draws its headcount ablest-first from whoever in the
		/// pool is not already spoken for by an earlier, higher-priority work, so the smithy that
		/// asks for strength is never left the arm the mill already took only because the mill
		/// happened to be listed first.
		/// </summary>
		/// <param name="Pool">Every settler available for these works this pass &mdash; already
		/// reduced by whatever the water detail spent, so hands are still spent exactly once
		/// (BUILDING-CATALOGUE-BRIEF.md Addendum 7 composes with the shipped hands-spent-once
		/// law). Null reads as empty.</param>
		/// <param name="Demands">Works in priority order. Null or empty yields no outcomes.</param>
		/// <returns>One outcome per demand, same order, same length. Never null.</returns>
		public static CrewOutcome[] AssignCrew(SettlerCapability[] Pool, CrewDemand[] Demands)
		{
			return AssignCrew(Pool, Demands, null);
		}

		/// <summary>The same deterministic assignment with a frozen extension-affinity matrix. Rows
		/// are demands, columns are pool settlers. Missing/malformed cells are neutral; no extension
		/// code runs inside this pure allocation.</summary>
		public static CrewOutcome[] AssignCrew(SettlerCapability[] Pool, CrewDemand[] Demands,
			int[,] ExtensionAffinities)
		{
			if (Demands == null || Demands.Length == 0)
			{
				return new CrewOutcome[0];
			}
			SettlerCapability[] pool = Pool ?? EmptyPool;
			bool[] taken = new bool[pool.Length];
			CrewOutcome[] result = new CrewOutcome[Demands.Length];
			for (int i = 0; i < Demands.Length; i++)
			{
				CrewDemand demand = Demands[i];
				int need = (demand.Headcount > 0) ? demand.Headcount : 0;
				if (need == 0)
				{
					result[i] = new CrewOutcome(0, demand.CapabilityKind,
						demand.CapabilityThreshold, 0, EmptyIndices,
						KingdomIdentityAffinityRules.NeutralPercent, demand.WorkKind);
					continue;
				}
				int[] order = RankCandidates(pool, taken, demand.CapabilityKind,
					demand.WorkKind, ExtensionAffinities, i);
				int give = (need <= order.Length) ? need : (demand.Threshold ? 0 : order.Length);
				int[] chosen = new int[give];
				int best = 0;
				int affinity = 0;
				for (int k = 0; k < give; k++)
				{
					int idx = order[k];
					chosen[k] = idx;
					taken[idx] = true;
					int value = pool[idx].ValueOf(demand.CapabilityKind);
					if (value > best)
					{
						best = value;
					}
					affinity += KingdomIdentityAffinityRules.Compose(
						pool[idx].Affinity(demand.WorkKind),
						ExtensionAffinityOf(ExtensionAffinities, i, idx));
				}
				int averageAffinity = give > 0 ? affinity / give
					: KingdomIdentityAffinityRules.NeutralPercent;
				result[i] = new CrewOutcome(give, demand.CapabilityKind,
					demand.CapabilityThreshold, (demand.CapabilityKind != null) ? best : 0,
					chosen, averageAffinity, demand.WorkKind);
			}
			return result;
		}

		// Every not-yet-taken pool index, ablest first when a kind is named (ties broken by
		// stable ascending index, so equal settlers are always drawn in the same order and the
		// assignment never depends on the sort's own implementation), plain arrival order when it
		// is not -- identical to what plain headcount allocation already drew from.
		private static int[] RankCandidates(SettlerCapability[] Pool, bool[] Taken,
			string CapabilityKind, string WorkKind, int[,] ExtensionAffinities,
			int DemandIndex)
		{
			List<int> candidates = new List<int>();
			for (int i = 0; i < Pool.Length; i++)
			{
				if (!Taken[i])
				{
					candidates.Add(i);
				}
			}
			if (!string.IsNullOrEmpty(CapabilityKind) || !string.IsNullOrEmpty(WorkKind))
			{
				candidates.Sort(delegate(int a, int b)
				{
					int byValue = Pool[b].RankedValue(CapabilityKind, WorkKind,
						ExtensionAffinityOf(ExtensionAffinities, DemandIndex, b))
						.CompareTo(Pool[a].RankedValue(CapabilityKind, WorkKind,
							ExtensionAffinityOf(ExtensionAffinities, DemandIndex, a)));
					return (byValue != 0) ? byValue : a.CompareTo(b);
				});
			}
			return candidates.ToArray();
		}

		private static int ExtensionAffinityOf(int[,] Affinities, int Demand, int Settler)
		{
			if (Affinities == null || Demand < 0 || Settler < 0
				|| Demand >= Affinities.GetLength(0) || Settler >= Affinities.GetLength(1))
			{
				return KingdomIdentityAffinityRules.NeutralPercent;
			}
			int value = Affinities[Demand, Settler];
			return value == 0 ? KingdomIdentityAffinityRules.NeutralPercent
				: KingdomIdentityAffinityRules.Clamp(value);
		}

		// --- Naming the shortfall (STANDARDS 7b) ----------------------------------------------

		/// <summary>The word a capability kind reads as in a sentence.</summary>
		public static string DisplayKind(string Kind)
		{
			switch (Kind)
			{
			case KindStrength:
				return "strength";
			case KindIntelligence:
				return "a certified mind";
			case KindTinkering:
				return "a practiced tinker";
			case KindHarvestry:
				return "a practiced harvester";
			case KindCustoms:
				return "a keeper versed in customs";
			case KindPhysic:
				return "a practiced physicker";
			case KindWayfaring:
				return "a practiced wayfarer";
			default:
				return string.IsNullOrEmpty(Kind) ? "capability" : Kind;
			}
		}

		/// <summary>
		/// One line naming a capability shortfall: what work, what it wanted, what its ablest hand
		/// actually brought. Said once per work while the shortfall stands
		/// (<c>KingdomCrews.AnnounceShortfall</c> owns the once-per-work flag); this file only ever
		/// composes the words.
		/// </summary>
		public static string ShortfallLine(string WorkName, string Kind, int Have, int Need)
		{
			string name = string.IsNullOrWhiteSpace(WorkName) ? "A work" : WorkName.Trim();
			string kindName = DisplayKind(Kind);
			if (Have <= 0)
			{
				return name + " runs slow for want of " + kindName + ": it wants " + Need + ", and no hand there has any to speak of.";
			}
			return name + " runs slow for want of " + kindName + ": it wants " + Need + ", and its ablest hand there has " + Have + ".";
		}
	}
}
