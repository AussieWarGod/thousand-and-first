using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// What a research node names as its effect, beyond the roster keys it mints. Three kinds in
	/// v1, all of them modest and all of them read by a lane that already exists.
	/// </summary>
	public readonly struct ResearchEffect
	{
		/// <summary>One of <see cref="KingdomResearchRules.EffectEfficiency"/>,
		/// <see cref="KingdomResearchRules.EffectStatCap"/>, or
		/// <see cref="KingdomResearchRules.EffectRecruitReveal"/>. A kind this build does not know
		/// is carried rather than refused &mdash; somebody else's vocabulary (STANDARDS 9).</summary>
		public readonly string Kind;

		/// <summary>The stat a <see cref="KingdomResearchRules.EffectStatCap"/> raises the city's
		/// headroom in, folded to lower case, or null for the other kinds.</summary>
		public readonly string Stat;

		public readonly int Amount;

		public ResearchEffect(string Kind, string Stat, int Amount)
		{
			this.Kind = Kind;
			this.Stat = Stat;
			this.Amount = Amount;
		}
	}

	/// <summary>
	/// One authored research node. Prerequisites in the roster vocabulary the catalogue's
	/// <c>Knowledge</c> gates already speak, a tier expressed as an Intelligence threshold, and
	/// exactly one effect &mdash; mint these roster keys &mdash; so that every design gated on
	/// <c>Knowledge=</c>, ours and any third party's, is satisfied by research without one line of
	/// the gate machinery changing.
	/// </summary>
	public sealed class ResearchNode
	{
		/// <summary>Registry identity. Merge-by-key, like every other data lane.</summary>
		public string Key;

		/// <summary>What the founder calls it. Falls back to <see cref="Key"/>.</summary>
		public string DisplayName;

		/// <summary>Which spine it hangs on. A free string, so a mod adds a fourth.</summary>
		public string Branch;

		/// <summary>1 to 4. Each tier names an Intelligence threshold on the city's best
		/// researcher (<see cref="KingdomResearchRules.IntelligenceForTier"/>).</summary>
		public int Tier = 1;

		/// <summary>Roster tokens, ALL required, in <c>KingdomZoningRules.Knows</c>'s own grammar.</summary>
		public string Requires;

		/// <summary>Craft rung the city must have reached to work on it at all.</summary>
		public TechLevel MinTech = TechLevel.Hands;

		/// <summary>Roster keys minted on completion. Defaults to <c>node:&lt;Key&gt;</c>.</summary>
		public string Grants;

		/// <summary>Staff-days of thinking a fully-crewed lab takes.</summary>
		public int Effort = 1;

		/// <summary>Nodes made VISIBLE on completion.</summary>
		public string Reveals;

		/// <summary>Tokens that grant this node outright, with no lab time: a disk taught, a
		/// treatise read. Never a <c>rite:</c> token &mdash; that is a load-time schema error
		/// (Addendum 18).</summary>
		public string TaughtBy;

		/// <summary>Tokens that REVEAL this node and BEGIN it at a head start, never finish it.
		/// Every <c>rite:</c> token lives here and nowhere else.</summary>
		public string SeededBy;

		/// <summary>Creed / culture / species / genotype tokens that make this node invisible and
		/// unreachable, told to nobody.</summary>
		public string Forbidden;

		/// <summary>A vanilla quest, or <c>Quest~Step</c>, that must be finished before this node
		/// exists at all.</summary>
		public string Quest;

		/// <summary>Named non-key effects, as authored.</summary>
		public string Effect;

		/// <summary>The parsed <see cref="Effect"/>. Never null; empty for a node that names none.</summary>
		public List<ResearchEffect> Effects = new List<ResearchEffect>();

		/// <summary>What the founder calls it, with the key as the fallback so a half-authored node
		/// still reads as something.</summary>
		public string Named => string.IsNullOrEmpty(DisplayName) ? Key : DisplayName;
	}

	/// <summary>
	/// One node's standing on the keepers' map: what the founder calls it, how far off it is in
	/// things rather than in numbers, whether the bench has already begun it, and what is in the
	/// way. Only ever built for a node the founder has HEARD of; a hidden one has no row.
	/// </summary>
	public readonly struct ResearchRow
	{
		public readonly string Key;

		public readonly string Name;

		/// <summary>Gates unmet. Zero means the bench could take this up today.</summary>
		public readonly int Distance;

		/// <summary>Whether labour already stands against it, shelved or current.</summary>
		public readonly bool Begun;

		/// <summary>What is in the way, in the founder's words. Empty when nothing is.</summary>
		public readonly string Missing;

		public ResearchRow(string Key, string Name, int Distance, bool Begun, string Missing)
		{
			this.Key = Key;
			this.Name = Name;
			this.Distance = Distance;
			this.Begun = Begun;
			this.Missing = Missing;
		}
	}

	/// <summary>
	/// The research system's arithmetic, engine-free and total: the tier ladder, the rate a lab
	/// works at, what a seed is worth, what a shelved subject costs, the citizen ceiling, and the
	/// parse that turns a <c>&lt;node&gt;</c> element into a record.
	/// <para>
	/// <b>A node's whole effect is minting a roster key.</b> <c>node:</c> joins <c>disk:</c>,
	/// <c>machine:</c> and <c>origin:</c> as a roster KIND and nothing else changes &mdash; which
	/// is what lets a third party's building gate on a third party's research with one
	/// <c>Knowledge="node:theirthing"</c> attribute and no C# at all.
	/// </para>
	/// <para>
	/// <b>Nothing here draws and nothing here reads a clock.</b> Accrual is arithmetic over ticks
	/// somebody else measured; ordering is the registry's own; ties break on key, ascending.
	/// </para>
	/// </summary>
	public static class KingdomResearchRules
	{
		// --- The vocabulary ------------------------------------------------------------------

		/// <summary>A roster kind: a research node this city's keepers have worked out. Worth no
		/// craft points at all &mdash; the craft rung is what the settlement LEARNED and CERTIFIED,
		/// never a readout of the research system.</summary>
		public const string KindNode = "node";

		/// <summary>A roster kind: water shared with a faction, and what they taught over it. A
		/// SEED, never a ceiling (Addendum 18): it opens doors and never rooms.</summary>
		public const string KindRite = "rite";

		/// <summary>A roster kind: a treatise read at the scriptorium.</summary>
		public const string KindBook = "book";

		/// <summary>A roster kind: a lodged notable who teaches while they stay. Live, so it lapses
		/// when they go &mdash; access withdrawn, nothing erased.</summary>
		public const string KindSavant = "savant";

		/// <summary>A roster kind (Addendum 17): what a people KNOWS. Declared here so a node and a
		/// modder's design can gate on it today; nothing in this build MINTS one yet, and an
		/// unminted kind is a gate that stays shut rather than a load error.</summary>
		public const string KindCulture = "culture";

		/// <summary>A roster kind (Addendum 17): what a body IS. Declared, unminted, exactly as
		/// <see cref="KindCulture"/> is.</summary>
		public const string KindSpecies = "species";

		/// <summary>Effect kind: a named rung on the realm-wide worker-method lane.</summary>
		public const string EffectEfficiency = "efficiency";

		/// <summary>Effect kind: the city's headroom over what a citizen walked in with.</summary>
		public const string EffectStatCap = "statcap";

		/// <summary>Effect kind: the guestbook begins carrying word of people worth sending for.</summary>
		public const string EffectRecruitReveal = "recruitreveal";

		/// <summary>The stat name a <c>statcap:any:N</c> writes: every stat this build trains, each
		/// still bound by its own ceiling.</summary>
		public const string StatAny = "any";

		// --- The tier ladder (verdict 5; Addendum 22 E1) ---------------------------------------

		/// <summary>Tiers this build knows. A node outside 1..<see cref="TierCount"/> is a parse
		/// fault, not a node nobody can reach.</summary>
		public const int TierCount = 4;

		/// <summary>
		/// Intelligence the city's best researcher must have for each tier, tier 1 first.
		/// <para>
		/// Vanilla's own shape, read at <c>B/Skills.xml:241-252</c>: the Tinkering tree gates the
		/// TIER on a hard attribute minimum and then never checks the attribute again at the moment
		/// of learning. Hard at the boundary, soft inside it. Tier 4 sits deliberately above what
		/// settlers roll: the top of a city's tree is reached through a person, not a building.
		/// </para>
		/// </summary>
		public static readonly int[] TierIntelligence = new int[TierCount] { 10, 14, 18, 22 };

		/// <summary>
		/// The threshold a tier names. A node that declares no tier at all is a tier-1 node, which
		/// is what an absent attribute has always meant here; a tier ABOVE the ladder reads as the
		/// top one, because an unknown tier must be harder to reach and never easier. Both ends are
		/// already refused at parse, so this is the defensive reading for a node somebody built by
		/// hand.
		/// </summary>
		public static int IntelligenceForTier(int Tier)
		{
			if (Tier <= 1)
			{
				return TierIntelligence[0];
			}
			return (Tier > TierCount) ? TierIntelligence[TierCount - 1] : TierIntelligence[Tier - 1];
		}

		/// <summary>Whether a city with this best researcher may work on a node of this tier at
		/// all. Below the threshold the subject cannot be SET, not merely worked slowly: a tier you
		/// cannot reach is not slow, it is shut.</summary>
		public static bool TierReached(int BestIntelligence, int Tier)
		{
			return BestIntelligence >= IntelligenceForTier(Tier);
		}

		/// <summary>What each point of Intelligence over the threshold is worth to the pace.</summary>
		public const int TierBonusPerPoint = 5;

		/// <summary>The most a mind can be worth to a subject it has already cleared. Above this the
		/// work is the work.</summary>
		public const int MaxTierBonus = 150;

		/// <summary>
		/// The pace a mind sets on a subject it has cleared the tier for: 100 at the threshold,
		/// <see cref="TierBonusPerPoint"/> per point over, capped at <see cref="MaxTierBonus"/>.
		/// Zero below the threshold, which makes the whole rate zero by arithmetic rather than by a
		/// special case.
		/// </summary>
		public static int TierBonus(int BestIntelligence, int Tier)
		{
			int wanted = IntelligenceForTier(Tier);
			if (BestIntelligence < wanted)
			{
				return 0;
			}
			int bonus = 100 + (BestIntelligence - wanted) * TierBonusPerPoint;
			return (bonus > MaxTierBonus) ? MaxTierBonus : bonus;
		}

		// --- The rate (verdict 2; Addendum 8 clause 2; STANDARDS 7b) --------------------------

		/// <summary>A shelf and a copyist. What the keeper's shelf and the scriptorium work at.</summary>
		public const int ScriptoriumPercent = 100;

		/// <summary>A room built to think in. The laboratory's own rung, for the wave that raises
		/// one.</summary>
		public const int LaboratoryPercent = 150;

		/// <summary>The ancients' own bench, understood. The arclight annexe's rung.</summary>
		public const int ArclightAnnexePercent = 200;

		/// <summary>
		/// How many labour ticks one elapsed tick buys against the current subject, as a percent.
		/// <para>
		/// Crew, condition, mind, and bench, multiplied. Every one of them can be zero and any one
		/// of them being zero makes the whole product zero &mdash; an idle lab produces nothing by
		/// arithmetic, and no grant anywhere can make an unstaffed work produce (Addendum 8 clause
		/// 2, RR5).
		/// </para>
		/// </summary>
		/// <param name="CrewEffectiveness">Headcount and capability combined, 0 to 100
		/// (<c>KingdomCrewRules.CombinedEffectiveness</c>).</param>
		/// <param name="WearEffectiveness">What the building's condition leaves of it, 0 to 100.</param>
		/// <param name="TierBonus">From <see cref="TierBonus"/>; zero shuts the subject.</param>
		/// <param name="LabPercent">The bench's own rung.</param>
		public static int InquiryRate(int CrewEffectiveness, int WearEffectiveness, int TierBonus, int LabPercent)
		{
			if (CrewEffectiveness <= 0 || WearEffectiveness <= 0 || TierBonus <= 0 || LabPercent <= 0)
			{
				return 0;
			}
			long rate = (long)Clamp(CrewEffectiveness, 0, 100) * Clamp(WearEffectiveness, 0, 100);
			rate = rate * TierBonus / 10000L;
			rate = rate * LabPercent / 100L;
			return (rate > int.MaxValue) ? int.MaxValue : (int)rate;
		}

		/// <summary>What a node's authored effort is in ticks. Staff-days, at the settlement's own
		/// day.</summary>
		public static int EffortTicks(int EffortDays)
		{
			if (EffortDays <= 0)
			{
				return (int)KingdomRules.TicksPerDay;
			}
			long ticks = (long)EffortDays * KingdomRules.TicksPerDay;
			return (ticks > int.MaxValue) ? int.MaxValue : (int)ticks;
		}

		/// <summary>
		/// Labour actually done over a stretch, at a rate. Wraps <c>KingdomRules.LabouredTicks</c>
		/// so the lab and the scaffold charge time the same way, and clamps to the int the city
		/// carries the accrual in.
		/// </summary>
		public static int Worked(long ElapsedTicks, int Rate)
		{
			if (ElapsedTicks <= 0 || Rate <= 0)
			{
				return 0;
			}
			long worked = KingdomRules.LabouredTicks(ElapsedTicks, Rate);
			return (worked > int.MaxValue) ? int.MaxValue : (int)worked;
		}

		// --- Seeds (Addendum 18; verdict 9) ----------------------------------------------------

		/// <summary>What one seed is worth: a quarter of the walk, and only ever the first quarter.</summary>
		public const int SeedPercent = 25;

		/// <summary>What every seed a node will ever take is worth together. A founder who has
		/// shared water with all of Qud still walks half of every road.</summary>
		public const int MaxSeedPercent = 50;

		/// <summary>
		/// The accrual a node stands at after a seed lands on it. Never lowers what is already
		/// there, never passes <see cref="MaxSeedPercent"/> of the effort, and never completes:
		/// a seed opens a door, and the city walks through it.
		/// </summary>
		/// <param name="EffortDays">The node's authored effort.</param>
		/// <param name="Accrued">What the city has already worked out, in ticks.</param>
		public static int Seeded(int EffortDays, int Accrued)
		{
			int effort = EffortTicks(EffortDays);
			int ceiling = (int)((long)effort * MaxSeedPercent / 100L);
			int seeded = Accrued + (int)((long)effort * SeedPercent / 100L);
			if (seeded > ceiling)
			{
				seeded = ceiling;
			}
			return (seeded < Accrued) ? Accrued : seeded;
		}

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

		// --- Parsing (STANDARDS 6) -------------------------------------------------------------

		/// <summary>
		/// Reads one <c>&lt;node&gt;</c> element into a record.
		/// <para>
		/// A node is REFUSED whole on a fault, unlike a building gate, and the asymmetry is
		/// deliberate: a gate is a restriction on a design that exists either way, and a node
		/// whose tier or effort cannot be read is a thing the founder could work on forever. The
		/// one fault that is a rule rather than a typo is a <c>rite:</c> token in
		/// <see cref="ResearchNode.TaughtBy"/> &mdash; Addendum 18's seed-not-ceiling clause,
		/// refused by file and key rather than left as a convention somebody can forget (RR6).
		/// </para>
		/// </summary>
		/// <param name="Error">Null on success; one sentence naming the key and the fault otherwise.</param>
		/// <returns>True when <paramref name="Node"/> is a node this build can use.</returns>
		public static bool TryParseNodeAttributes(string Key, string DisplayName, string Branch, string Tier,
			string Requires, string MinTech, string Grants, string Effort, string Reveals,
			string TaughtBy, string SeededBy, string Forbidden, string Quest, string Effect,
			out ResearchNode Node, out string Error)
		{
			Node = null;
			string key = Fold(Key);
			if (key == null)
			{
				Error = "a <node> element carries no Key.";
				return false;
			}
			if (key.IndexOf(KingdomZoningRules.KindSeparator) >= 0 || key.IndexOf(KingdomZoningRules.RosterSeparator) >= 0)
			{
				Error = "node " + key + ": a Key may not carry '" + KingdomZoningRules.KindSeparator + "' or '" + KingdomZoningRules.RosterSeparator + "'.";
				return false;
			}
			int tier = 1;
			if (!string.IsNullOrEmpty(Tier) && (!int.TryParse(Tier.Trim(), out tier) || tier < 1 || tier > TierCount))
			{
				Error = "node " + key + ": Tier \"" + Tier + "\" is not one of 1 to " + TierCount + ".";
				return false;
			}
			int effort = 1;
			if (!string.IsNullOrEmpty(Effort) && (!int.TryParse(Effort.Trim(), out effort) || effort < 1))
			{
				Error = "node " + key + ": Effort \"" + Effort + "\" is not a count of staff-days.";
				return false;
			}
			TechLevel minTech = TechLevel.Hands;
			if (!string.IsNullOrEmpty(MinTech) && MinTech.Trim().Length > 0
				&& (!System.Enum.TryParse<TechLevel>(MinTech.Trim(), ignoreCase: true, out minTech) || !KingdomZoningRules.IsKnownTechLevel(minTech)))
			{
				Error = "node " + key + ": MinTech \"" + MinTech + "\" names no craft this build knows.";
				return false;
			}
			string taught = Trimmed(TaughtBy);
			foreach (string token in KingdomZoningRules.Tokens(taught))
			{
				if (KingdomZoningRules.KindOf(token) == KindRite)
				{
					Error = "node " + key + ": TaughtBy names \"" + token
						+ "\". A rite SEEDS a branch and never finishes a node (Addendum 18); move it to SeededBy.";
					return false;
				}
			}
			string grants = Trimmed(Grants);
			if (grants == null)
			{
				grants = KingdomZoningRules.ComposeKey(KindNode, key);
			}
			List<ResearchEffect> effects;
			string effectError;
			if (!TryParseEffects(Effect, out effects, out effectError))
			{
				Error = "node " + key + ": Effect " + effectError;
				return false;
			}
			Node = new ResearchNode
			{
				Key = key,
				DisplayName = Trimmed(DisplayName),
				Branch = Fold(Branch),
				Tier = tier,
				Requires = Trimmed(Requires),
				MinTech = minTech,
				Grants = grants,
				Effort = effort,
				Reveals = Trimmed(Reveals),
				TaughtBy = taught,
				SeededBy = Trimmed(SeededBy),
				Forbidden = Trimmed(Forbidden),
				Quest = Trimmed(Quest),
				Effect = Trimmed(Effect),
				Effects = effects
			};
			Error = null;
			return true;
		}

		/// <summary>
		/// Reads an <c>Effect</c> attribute: <c>efficiency:5</c>, <c>statcap:Intelligence:1</c>,
		/// <c>recruitreveal:1</c>, comma separated. A kind this build does not know is carried with
		/// its amount rather than refused, so a later wave or another mod's lane can read it
		/// (STANDARDS 9); what IS refused is an amount that is not a number, because that is a typo
		/// wearing a rule's clothes.
		/// </summary>
		public static bool TryParseEffects(string Source, out List<ResearchEffect> Effects, out string Error)
		{
			Effects = new List<ResearchEffect>();
			Error = null;
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return true;
			}
			foreach (string token in KingdomZoningRules.Tokens(Source))
			{
				string[] parts = token.Split(KingdomZoningRules.KindSeparator);
				if (parts.Length < 2 || parts.Length > 3)
				{
					Error = "\"" + token + "\" is not kind:amount or kind:stat:amount.";
					Effects = new List<ResearchEffect>();
					return false;
				}
				string kind = Fold(parts[0]);
				string stat = (parts.Length == 3) ? Fold(parts[1]) : null;
				int amount;
				if (kind == null || !int.TryParse(parts[parts.Length - 1].Trim(), out amount))
				{
					Error = "\"" + token + "\" carries no readable amount.";
					Effects = new List<ResearchEffect>();
					return false;
				}
				if (kind == EffectStatCap && stat == null)
				{
					Error = "\"" + token + "\" is a stat cap that names no stat.";
					Effects = new List<ResearchEffect>();
					return false;
				}
				Effects.Add(new ResearchEffect(kind, stat, amount));
			}
			return true;
		}

		/// <summary>
		/// What is wrong with a merged registry, said once at load. Nothing is unregistered: a node
		/// that is wrong about itself stays in the tree and becomes visible, which is the only shape
		/// a check on third-party content can honestly take. The checks are the ones no single node
		/// can see &mdash; a reveal into a key nothing declares, a requirement on a node that does
		/// not exist, a tier below the tier it hangs from.
		/// </summary>
		/// <returns>One sentence per finding, in registry order; never null.</returns>
		public static List<string> Validate(IList<ResearchNode> Nodes)
		{
			List<string> findings = new List<string>();
			if (Nodes == null)
			{
				return findings;
			}
			List<string> keys = new List<string>();
			List<string> granted = new List<string>();
			for (int i = 0; i < Nodes.Count; i++)
			{
				if (Nodes[i] == null || Nodes[i].Key == null)
				{
					continue;
				}
				keys.Add(Nodes[i].Key);
				foreach (string token in KingdomZoningRules.Tokens(Nodes[i].Grants))
				{
					string name = KingdomZoningRules.NameOf(token);
					if (name != null && !granted.Contains(name))
					{
						granted.Add(name);
					}
				}
			}
			for (int i = 0; i < Nodes.Count; i++)
			{
				ResearchNode node = Nodes[i];
				if (node == null || node.Key == null)
				{
					continue;
				}
				foreach (string token in KingdomZoningRules.Tokens(node.Reveals))
				{
					string name = KingdomZoningRules.NameOf(token);
					if (name != null && !keys.Contains(name))
					{
						findings.Add("node " + node.Key + " reveals \"" + token + "\", which no node declares.");
					}
				}
				foreach (string token in KingdomZoningRules.Tokens(node.Requires))
				{
					if (KingdomZoningRules.KindOf(token) != KindNode)
					{
						continue;
					}
					foreach (string arm in token.Split(KingdomZoningRules.RosterSeparator))
					{
						string name = KingdomZoningRules.NameOf(arm);
						if (name != null && !granted.Contains(name))
						{
							findings.Add("node " + node.Key + " requires \"" + arm + "\", which no node grants.");
						}
					}
				}
			}
			return findings;
		}

		// --- The words (STANDARDS 7b; RR4: no percentage, no bar, no ETA) ----------------------

		/// <summary>The roster key the trunk's first node mints, and the one the keepers' map itself
		/// waits on: before it, the keepers write nothing down and there is no map to draw.</summary>
		public const string NotesKey = "node:notes";

		/// <summary>The founder's journal entry for a node, filed unrevealed and revealed the day
		/// they first hear of it. One line, and it names the road rather than the destination.</summary>
		public static string LeadText(string Named, string Branch)
		{
			string named = string.IsNullOrEmpty(Named) ? "something" : Named;
			return string.IsNullOrEmpty(Branch)
				? ("There is a thing called " + named + ", and keepers somewhere know how it is done.")
				: ("There is a thing called " + named + ", and it belongs to the " + Branch + " road.");
		}

		/// <summary>The one sentence a map with no first node draws, and the whole of it.</summary>
		public static string NothingWrittenDown(string CityName)
		{
			return "Nobody at {{C|" + (string.IsNullOrEmpty(CityName) ? "the city" : CityName)
				+ "}} writes anything down. There is no map of what the keepers know, because nobody has begun keeping one.";
		}

		/// <summary>
		/// Why a bench may not take up a subject its city is not clever enough for. Names the mind
		/// the city has and the mind the work wants, and points at the only two things that ever
		/// answer it: somebody who already has it, or a school that raises what the city can teach.
		/// </summary>
		public static string TierRefusal(string CityName, string Named, int BestMind, int Wanted)
		{
			string city = string.IsNullOrEmpty(CityName) ? "the city" : CityName;
			string named = string.IsNullOrEmpty(Named) ? "that" : Named;
			return (BestMind <= 0)
				? ("There is nobody at " + city + " who could begin " + named + ". It wants a mind of {{C|" + Wanted
					+ "}}, and there is nobody at the bench at all. Take in somebody who already knows this kind of work.")
				: ("Nobody at " + city + " can hold " + named + " in their head. It wants a mind of {{C|" + Wanted
					+ "}}, and the ablest keeper there has {{C|" + BestMind
					+ "}}. Take in somebody who already knows this work, lodge a savant who does, or teach what the city can teach.");
		}

		/// <summary>A bench standing over nothing, said once. Not a fault and not a stall: a choice
		/// the founder has not made yet, and one nothing else in the game will remind them of.</summary>
		public static string NoSubjectLine(string LabName, string CityName)
		{
			return (string.IsNullOrEmpty(LabName) ? "The bench" : ("The " + LabName)) + " at "
				+ (string.IsNullOrEmpty(CityName) ? "the city" : CityName)
				+ " stands over nothing. Nobody has been set anything to work out.";
		}

		/// <summary>
		/// Why a lab is producing nothing, said once and unsaid the moment it is not. Names exactly
		/// one thing &mdash; the first one that is actually zero &mdash; because a sentence that
		/// names three lacks tells the founder to fix none of them.
		/// </summary>
		public static string StallLine(string LabName, string Named, int CrewEffectiveness, int WearEffectiveness,
			int BestMind, int Wanted)
		{
			string lab = string.IsNullOrEmpty(LabName) ? "The bench" : ("The " + LabName);
			string named = string.IsNullOrEmpty(Named) ? "the work" : Named;
			if (CrewEffectiveness <= 0)
			{
				return lab + " is empty, and " + named + " waits. Nobody thinks a thing out by being near it.";
			}
			if (WearEffectiveness <= 0)
			{
				return lab + " is in no state to be worked in, and " + named + " waits on the mending.";
			}
			if (BestMind < Wanted)
			{
				return lab + " is crewed, and " + named + " wants a mind of " + Wanted + ". The ablest there has "
					+ ((BestMind > 0) ? BestMind.ToString() : "none to speak of") + ".";
			}
			return lab + " is standing idle, and " + named + " waits.";
		}

		/// <summary>What the ledger says when a ninth shelving pushes the least-advanced subject off
		/// the shelf. Once, by name, so nothing is lost in silence.</summary>
		public static string ForgottenLine(string CityName, string Named)
		{
			return "The keepers of " + (string.IsNullOrEmpty(CityName) ? "the city" : CityName)
				+ " have too many half-finished things on the shelf, and what they had of "
				+ (string.IsNullOrEmpty(Named) ? "the oldest" : Named) + " has gone back to nothing.";
		}

		// --- Small shared helpers --------------------------------------------------------------

		private static int Clamp(int Value, int Low, int High)
		{
			if (Value < Low)
			{
				return Low;
			}
			return (Value > High) ? High : Value;
		}

		private static string Fold(string Value)
		{
			if (Value == null)
			{
				return null;
			}
			string folded = Value.Trim().ToLowerInvariant();
			return (folded.Length == 0) ? null : folded;
		}

		private static string Trimmed(string Value)
		{
			if (Value == null)
			{
				return null;
			}
			string trimmed = Value.Trim();
			return (trimmed.Length == 0) ? null : trimmed;
		}
	}
}
