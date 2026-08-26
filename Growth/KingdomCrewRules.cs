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
	public static class KingdomCrewRules
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

		/// <summary>Frozen vanilla skill facts for one person. Values are 0/1 because the catalogue
		/// asks whether a practiced hand is present; attribute magnitude remains the separate
		/// Strength/Intelligence lane.</summary>
		public readonly struct WorkerSkills
		{
			public readonly int Tinkering;
			public readonly int Harvestry;
			public readonly int Customs;
			public readonly int Physic;
			public readonly int Wayfaring;

			public WorkerSkills(bool Tinkering, bool Harvestry, bool Customs, bool Physic,
				bool Wayfaring)
			{
				this.Tinkering = Tinkering ? 1 : 0;
				this.Harvestry = Harvestry ? 1 : 0;
				this.Customs = Customs ? 1 : 0;
				this.Physic = Physic ? 1 : 0;
				this.Wayfaring = Wayfaring ? 1 : 0;
			}

			public int ValueOf(string Kind)
			{
				switch (Kind)
				{
				case KindTinkering: return Tinkering;
				case KindHarvestry: return Harvestry;
				case KindCustoms: return Customs;
				case KindPhysic: return Physic;
				case KindWayfaring: return Wayfaring;
				default: return 0;
				}
			}
		}

		/// <summary>What a robot's strength never falls under, tireless and built for it
		/// (BUILDING-CATALOGUE-BRIEF.md Addendum 7: "a robot is tireless, strong"). Chosen to
		/// answer a modest early <c>CrewNeeds</c> threshold outright while still losing to a truly
		/// mighty organic hand, so the floor is a fact about robots, never a ceiling on anyone
		/// else.</summary>
		public const int TirelessStrengthFloor = 20;

		/// <summary>The least a capability shortfall ever slows a work to. Headcount, not
		/// capability, is what can idle a work outright (<see cref="KingdomRules.CrewEffectiveness"/>
		/// already returns zero for no hands at all); a work that HAS hands, however unskilled,
		/// keeps moving.</summary>
		public const int MinCapabilityEffectiveness = 25;

		/// <summary>
		/// One settler's build-relevant stats, plus the one derived fact
		/// <see cref="KingdomCrews.CapabilityOf"/> reads off a robot before any author touches it.
		/// Immutable: a snapshot for the one pass that draws crew, never a thing that accumulates.
		/// </summary>
		public readonly struct SettlerCapability
		{
			public readonly int Strength;

			public readonly int Intelligence;

			/// <summary>Robot: no fatigue, built to work. Raises <see cref="Strength"/> to
			/// <see cref="TirelessStrengthFloor"/> when the settler's own value reads lower; never
			/// touches <see cref="Intelligence"/> &mdash; being tireless says nothing about being
			/// certified.</summary>
			public readonly bool Tireless;

			/// <summary>Addendum 17's one identity field: open culture/species strings plus
			/// vanilla-authored activity evidence. It affects assignment and the separate
			/// affinity factor, never the Intelligence tier read performed by <see cref="ValueOf"/>.</summary>
			public readonly KingdomIdentityAffinityRules.WorkerIdentity Identity;

			/// <summary>The skill half of Addendum 17's full capability tuple, read from vanilla
			/// <c>HasSkill</c> and consumed by the same ablest-first assignment.</summary>
			public readonly WorkerSkills Skills;

			public SettlerCapability(int Strength, int Intelligence, bool Tireless)
				: this(Strength, Intelligence, Tireless,
					default(KingdomIdentityAffinityRules.WorkerIdentity), default(WorkerSkills))
			{
			}

			public SettlerCapability(int Strength, int Intelligence, bool Tireless,
				KingdomIdentityAffinityRules.WorkerIdentity Identity)
				: this(Strength, Intelligence, Tireless, Identity, default(WorkerSkills))
			{
			}

			public SettlerCapability(int Strength, int Intelligence, bool Tireless,
				KingdomIdentityAffinityRules.WorkerIdentity Identity, WorkerSkills Skills)
			{
				this.Strength = Strength;
				this.Intelligence = Intelligence;
				this.Tireless = Tireless;
				this.Identity = Identity;
				this.Skills = Skills;
			}

			/// <summary>The value this settler brings to one capability kind. Zero for a kind
			/// this file does not know, which is the correct answer: nobody is ever measured
			/// against a stat that does not exist.</summary>
			public int ValueOf(string Kind)
			{
				switch (Kind)
				{
				case KindStrength:
					return (Tireless && Strength < TirelessStrengthFloor) ? TirelessStrengthFloor : Strength;
				case KindIntelligence:
					return Intelligence;
				default:
					return Skills.ValueOf(Kind);
				}
			}

			/// <summary>Per-person work affinity. Kept separate from the raw stat so culture
			/// cannot satisfy or skip a research Intelligence tier.</summary>
			public int Affinity(string WorkKind)
			{
				return Identity.Affinity(WorkKind);
			}

			internal int RankedValue(string CapabilityKind, string WorkKind)
			{
				return RankedValue(CapabilityKind, WorkKind,
					KingdomIdentityAffinityRules.NeutralPercent);
			}

			internal int RankedValue(string CapabilityKind, string WorkKind,
				int ExtensionAffinity)
			{
				int raw = string.IsNullOrEmpty(CapabilityKind) ? 100 : ValueOf(CapabilityKind);
				return KingdomIdentityAffinityRules.Apply(raw,
					KingdomIdentityAffinityRules.Compose(Affinity(WorkKind), ExtensionAffinity));
			}
		}

		/// <summary>One work's headcount and (optionally) capability demand for this pass.
		/// <see cref="CapabilityKind"/> null or <see cref="CapabilityThreshold"/> zero both mean
		/// "any hands will do" &mdash; the ordinary case for every design written before
		/// <c>CrewNeeds</c> existed.</summary>
		public readonly struct CrewDemand
		{
			public readonly int Headcount;

			public readonly bool Threshold;

			public readonly string CapabilityKind;

			public readonly int CapabilityThreshold;

			/// <summary>The building's open catalogue category. Null keeps the pre-identity
			/// assignment exactly neutral.</summary>
			public readonly string WorkKind;

			public CrewDemand(int Headcount, bool Threshold, string CapabilityKind, int CapabilityThreshold)
				: this(Headcount, Threshold, CapabilityKind, CapabilityThreshold, null)
			{
			}

			public CrewDemand(int Headcount, bool Threshold, string CapabilityKind,
				int CapabilityThreshold, string WorkKind)
			{
				this.Headcount = Headcount;
				this.Threshold = Threshold;
				this.CapabilityKind = CapabilityKind;
				this.CapabilityThreshold = CapabilityThreshold;
				this.WorkKind = WorkKind;
			}
		}

		/// <summary>What one demand drew from the pool: how many hands, the best value among them
		/// for the demand's own capability kind, and which pool slots they were &mdash; the last
		/// so a caller with real <c>GameObject</c>s beside the pool can say who is building what.
		/// </summary>
		public readonly struct CrewOutcome
		{
			public readonly int Assigned;

			public readonly string CapabilityKind;

			public readonly int CapabilityThreshold;

			/// <summary>The highest capability value among the settlers actually assigned, for
			/// <see cref="CapabilityKind"/>. Zero when nobody was assigned or the demand named no
			/// kind &mdash; never a partial credit for anyone left out.</summary>
			public readonly int BestCapability;

			/// <summary>Average affinity of the hands actually assigned, 70-130. A separate
			/// factor from headcount/capability and condition; neutral when nobody was assigned.</summary>
			public readonly int IdentityAffinity;

			public readonly string WorkKind;

			/// <summary>Indices into the pool <see cref="AssignCrew"/> was called with, ablest
			/// first. Empty, never null, when nobody was assigned.</summary>
			public readonly int[] SettlerIndices;

			public CrewOutcome(int Assigned, string CapabilityKind, int CapabilityThreshold, int BestCapability, int[] SettlerIndices)
				: this(Assigned, CapabilityKind, CapabilityThreshold, BestCapability,
					SettlerIndices, KingdomIdentityAffinityRules.NeutralPercent, null)
			{
			}

			public CrewOutcome(int Assigned, string CapabilityKind, int CapabilityThreshold,
				int BestCapability, int[] SettlerIndices, int IdentityAffinity, string WorkKind)
			{
				this.Assigned = Assigned;
				this.CapabilityKind = CapabilityKind;
				this.CapabilityThreshold = CapabilityThreshold;
				this.BestCapability = BestCapability;
				this.SettlerIndices = SettlerIndices;
				this.IdentityAffinity = KingdomIdentityAffinityRules.Clamp(IdentityAffinity);
				this.WorkKind = WorkKind;
			}
		}

		private static readonly int[] EmptyIndices = new int[0];

		private static readonly SettlerCapability[] EmptyPool = new SettlerCapability[0];

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
