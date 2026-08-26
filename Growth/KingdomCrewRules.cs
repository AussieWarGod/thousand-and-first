using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for who among a crew is fit to raise a demanding work
	/// (BUILDING-CATALOGUE-BRIEF.md Addendum 7: "the physical material chain"). A design may
	/// declare <c>CrewNeeds</c> &mdash; a stat and a threshold, <c>strength:16</c> in the same
	/// <c>kind:amount</c> language <see cref="KingdomCatalogueRules.TryParseTally"/> already reads
	/// for <c>Carries</c> &mdash; and a crew that falls short of it still raises the work, only
	/// slower, named once (STANDARDS 7b). Nothing here ever stalls a build to zero for want of
	/// skill: headcount answers "is this work crewed at all", capability only ever answers "how
	/// fast", and the two are combined by <see cref="CombinedEffectiveness"/>, the lesser
	/// governing.
	/// <para>
	/// <b>Derive before authoring.</b> A settler's capability is read off real stats
	/// (<c>Strength</c>, <c>Intelligence</c>) plus one QoL-derived fact this file takes as given:
	/// <c>Tireless</c>, true for anything <c>KingdomQolRules.ResidentTruth.Robot</c> already calls a
	/// robot &mdash; strong and tireless by what it is, before any author writes a word about it.
	/// The engine-coupled half that performs those reads against a real <c>GameObject</c> is
	/// <c>KingdomCrews</c>, beside it.
	/// </para>
	/// <para>
	/// <b>Ablest-first, deterministic.</b> <see cref="AssignCrew"/> draws, for a demand that names
	/// a capability kind, the highest-valued settlers first; ties break on the settler's stable
	/// position in the pool, ascending. The same pool in the same order always yields the same
	/// assignment &mdash; the founder assigns nobody (Addendum 6/7), but the roll of who is
	/// building what is never a coin flip either.
	/// </para>
	/// </summary>
	public static partial class KingdomCrewRules
	{
		// --- The vocabulary --------------------------------------------------------------------

		/// <summary>Haulage and stonework: raising a wall, quarrying, shaping timber.</summary>
		public const string KindStrength = "strength";

		/// <summary>Certified tech: a work a mind has to understand before it runs right.</summary>
		public const string KindIntelligence = "intelligence";

		/// <summary>Open skill-capability kinds. They are thresholds on skills real settlers carry,
		/// not research keys and never substitutes for Intelligence tier gates.</summary>
		public const string KindTinkering = "skill.tinkering";
		public const string KindHarvestry = "skill.harvestry";
		public const string KindCustoms = "skill.customs";
		public const string KindPhysic = "skill.physic";
		public const string KindWayfaring = "skill.wayfaring";

		/// <summary>Every capability kind this file itself answers for, in the order a design's
		/// <c>CrewNeeds</c> is read: the first kind a design names a positive threshold for is the
		/// one its crew is measured against (<see cref="KingdomCrews.AssignWorks"/>). Not
		/// restricted to this list on the parsing side &mdash; a kind nobody's stats answer to yet
		/// is somebody else's vocabulary and is logged, not refused (STANDARDS 9).</summary>
		public static readonly string[] KnownKinds = new string[7]
		{
			KindStrength, KindIntelligence, KindTinkering, KindHarvestry, KindCustoms,
			KindPhysic, KindWayfaring
		};

		public static bool IsKnownKind(string Kind)
		{
			if (string.IsNullOrEmpty(Kind)) return false;
			for (int i = 0; i < KnownKinds.Length; i++)
				if (string.Equals(KnownKinds[i], Kind, System.StringComparison.OrdinalIgnoreCase))
					return true;
			return false;
		}

		/// <summary>The vanilla <c>Statistics</c> key an attribute kind reads. Skill kinds return
		/// null because <see cref="WorkerSkills"/> reads them through <c>HasSkill</c>; an unknown kind
		/// also returns null.</summary>
		public static string StatNameFor(string Kind)
		{
			switch (Kind)
			{
			case KindStrength:
				return "Strength";
			case KindIntelligence:
				return "Intelligence";
			default:
				return null;
			}
		}

	}
}
