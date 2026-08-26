using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCrewRules
	{
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
	}
}
