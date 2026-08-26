using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
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
	public static partial class KingdomResearchRules
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

		/// <summary>A live roster kind (Addendum 17): what a resident people KNOWS, read from
		/// vanilla culture rather than persisted as learned city knowledge.</summary>
		public const string KindCulture = "culture";

		/// <summary>A live roster kind (Addendum 17): what a resident body IS, read from vanilla
		/// species separately from culture.</summary>
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
			return InquiryRate(CrewEffectiveness, WearEffectiveness, TierBonus, LabPercent,
				KingdomIdentityAffinityRules.NeutralPercent);
		}

		/// <summary>The same lane with Addendum 17's per-crew identity factor kept distinct.
		/// Raw Intelligence still owns the tier gate; affinity changes pace only.</summary>
		public static int InquiryRate(int CrewEffectiveness, int WearEffectiveness,
			int TierBonus, int LabPercent, int IdentityAffinity)
		{
			if (CrewEffectiveness <= 0 || WearEffectiveness <= 0 || TierBonus <= 0 || LabPercent <= 0)
			{
				return 0;
			}
			long rate = (long)Clamp(CrewEffectiveness, 0, 100) * Clamp(WearEffectiveness, 0, 100);
			rate = rate * TierBonus / 10000L;
			rate = rate * LabPercent / 100L;
			rate = rate * KingdomIdentityAffinityRules.Clamp(IdentityAffinity) / 100L;
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

	}
}
