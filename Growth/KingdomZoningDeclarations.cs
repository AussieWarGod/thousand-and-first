namespace ThousandAndFirst
{
	/// <summary>
	/// How far a settlement's own craft has come. Derived, never authored and never set: it is a
	/// readout of what the keepers have been taught and what they have certified fit for the grid
	/// (<see cref="KingdomZoningRules.TechPoints"/>), so it rises by playing rather than by
	/// spending anything on a research screen.
	/// <para>
	/// Per city, not per realm (Addendum 22 B7). The roster this is derived from sits on the
	/// settlement record, so a design's <c>MinTech</c> is judged against the city it is being
	/// raised in and the keepers' map finally means the city it is titled with.
	/// </para>
	/// <para>
	/// This file used to say the mod had no research tree and did not want one. It has one
	/// (Addendum 14), and the sentence that mattered survives intact: a tree is a second job, so
	/// the tree is not a screen the founder spends into. Research is a LADDER BESIDE this one and
	/// never a source for it &mdash; it mints roster keys, and roster keys of kind
	/// <see cref="KingdomZoningRules.KindNode"/> are worth no craft points at all
	/// (<see cref="KingdomZoningRules.TechPointsPerNode"/>). What rises this level is still a disk
	/// carried home and a machine certified, exactly as before.
	/// </para>
	/// </summary>
	public enum TechLevel
	{
		/// <summary>What hands can shape without help. Where every settlement starts.</summary>
		Hands = 0,
		/// <summary>The settlement can make use of what is dragged home from a ruin.</summary>
		Salvage = 1,
		/// <summary>There is a bench, and people who know what to do at it.</summary>
		Workshop = 2,
		/// <summary>Heat, pressure, and the confidence to run both unattended.</summary>
		Foundry = 3,
		/// <summary>The ancients' own work, understood well enough to raise more of it.</summary>
		Arclight = 4
	}

	/// <summary>
	/// Why a design may or may not be raised, beyond the city style and growth stage
	/// <c>KingdomRules.BuildEntry</c> already carries. Ordered the way
	/// <see cref="KingdomZoningRules.Judge"/> checks them, which is from the most fundamental
	/// lack to the most local one: nobody here knows how, then the settlement is not that
	/// advanced, then the realm is too small, then this stratum will never take it, then finally
	/// &mdash; and only then &mdash; this is the wrong ground. District comes last deliberately,
	/// so the founder hears "the forgeworks would take it" at the moment that sentence is the
	/// only thing standing between them and the building.
	/// <para>
	/// Stratum sits directly above district because the two are the same kind of refusal told at
	/// different scales: this ground will not take it, and here is ground that would. Both are
	/// answered by walking &mdash; ground can be named a district tomorrow, and a claim reaches the
	/// stratum above or below the one the city holds. What no walking answers is the weather, which
	/// is why the stratum's two refusals are told in the order
	/// <see cref="KingdomZoningRules.Judge"/> tells them.
	/// </para>
	/// <para>
	/// <b>The three creed gates are checked FIRST and are numbered LAST</b>, which is the one
	/// place where the ordinals and the reading order disagree. They belong at the head of the
	/// order because who your people are is more fundamental than what your keepers were taught:
	/// a city with nobody who has ever held a creed is not one disk away from its shrine, it is a
	/// different city. They are numbered at the end because these ordinals are published
	/// (STANDARDS &sect;9), and renumbering a published enum to make it read prettily would move
	/// every value a third party already switched on. Appending is additive; renumbering is a
	/// break. <see cref="KingdomZoningRules.Judge"/> is the authority on the order.
	/// </para>
	/// </summary>
	public enum ZoningVerdict
	{
		Permitted = 0,
		RefusedUnlearned = 1,
		RefusedTechLevel = 2,
		RefusedTerritory = 3,
		RefusedStratum = 4,
		RefusedDistrict = 5,

		/// <summary>Nobody living here holds &mdash; or has ever held &mdash; the creed the design
		/// belongs to. Checked first of all.</summary>
		RefusedUnaligned = 6,

		/// <summary>The creed is held here, but by too few of the city for a work of it to stand.
		/// </summary>
		RefusedCreedShare = 7,

		/// <summary>The hands the design asks for are not among this city's people.</summary>
		RefusedBuilders = 8,

		/// <summary>
		/// This city already keeps a megastructure, and it is not this one (Addendum 22 A1). A city
		/// is about one great thing; the contention is not for ground but for what the place is FOR.
		/// <para>
		/// Appended, like every value above it, for the reason the type's own summary gives:
		/// renumbering a published enum moves every value a third party already switched on.
		/// </para>
		/// </summary>
		RefusedMegastructure = 9,

		/// <summary>
		/// This design is an outpost of a great work, and nowhere in the realm does that work stand
		/// (END-STATE-CITIES-RESEARCH &sect;5.5). <see cref="ZoningJudgement.Detail"/> carries the
		/// PARENT's registry key.
		/// </summary>
		RefusedSatellite = 10,

		/// <summary>
		/// This city already keeps an outpost of the same great work, and one to a city is the whole
		/// of the rule. <see cref="ZoningJudgement.Detail"/> carries the KEPT outpost's key.
		/// <para>
		/// <b>A second value rather than a second reading of <see cref="RefusedSatellite"/>'s
		/// detail</b>, and the precedent is <see cref="RefusedUnaligned"/> beside
		/// <see cref="RefusedCreedShare"/>: two flavours of one gate, told apart by the verdict,
		/// because a composer that had to guess which key a Detail held would guess wrong the first
		/// time an outpost was named after its parent.
		/// </para>
		/// </summary>
		RefusedSatelliteKept = 11,

		/// <summary>
		/// Only a capital may raise this, and the crown is not set down in this city (Addendum 22
		/// A4; the capital ruling extending Addendum 19). <see cref="ZoningJudgement.Detail"/>
		/// carries the city keeping the crown, or null when the realm has no capital at all
		/// &mdash; which are two different sentences and one verdict, because the ACT the founder
		/// is being pointed at is the same one either way.
		/// </summary>
		RefusedUncrowned = 12,

		/// <summary>
		/// The design belongs to a covenant whose standing threshold the realm has not reached.
		/// Checked before the ground and knowledge gates because no choice of plot can lift it.
		/// <see cref="ZoningJudgement.Detail"/> carries the faction key.
		/// </summary>
		RefusedCovenantStanding = 13
	}
	/// <summary>
	/// One gate's answer: whether the design may be raised, and &mdash; when it may not &mdash;
	/// the two pieces of prose a refusal owes the founder. STANDARDS 7b is the reason both
	/// strings exist: a refusal that does not name what would fix it is a locked door.
	/// </summary>
	public readonly struct ZoningJudgement
	{
		public readonly ZoningVerdict Verdict;

		/// <summary>What is missing, in the settlement's own words: "the forgeworks", "3 claimed
		/// zones", "solar condenser", "foundry". Null when nothing is missing.</summary>
		public readonly string Detail;

		/// <summary>The short tag a menu line carries so a founder can see which designs are
		/// blocked before choosing one. Null when nothing is missing.</summary>
		public readonly string Note;

		public ZoningJudgement(ZoningVerdict Verdict, string Detail, string Note)
		{
			this.Verdict = Verdict;
			this.Detail = Detail;
			this.Note = Note;
		}

		public bool Permitted => Verdict == ZoningVerdict.Permitted;

		/// <summary>The judgement a design with nothing to prove receives.</summary>
		public static ZoningJudgement Allowed => new ZoningJudgement(ZoningVerdict.Permitted, null, null);
	}

	/// <summary>
	/// One building-side covenant gate. Both values are authored together; the faction key is
	/// resolved against Qud's live registry by the engine-coupled loader, while this value and its
	/// parser remain deterministic and testable without the engine.
	/// </summary>
	public readonly struct CovenantGate
	{
		public readonly string Faction;

		public readonly int MinStanding;

		public CovenantGate(string Faction, int MinStanding)
		{
			this.Faction = Faction;
			this.MinStanding = MinStanding;
		}

		public bool IsOpen => string.IsNullOrEmpty(Faction);

		public static CovenantGate Open => default(CovenantGate);
	}
}
