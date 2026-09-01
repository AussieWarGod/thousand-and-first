using System;
using System.Collections.Generic;
using ThousandAndFirst.Api;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of crew capability (<see cref="KingdomCrewRules"/> is the whole of
	/// the arithmetic, and is engine-free): the registry of what each design's <c>CrewNeeds</c>
	/// asks for, the reads that turn a real settler into a
	/// <see cref="KingdomCrewRules.SettlerCapability"/>, and the one entry point
	/// <c>KingdomGrowth.AssignWork</c> calls to crew a pass's works ablest-first.
	/// <para>
	/// <b>No state of its own beyond the registry.</b> Like <c>KingdomQol</c> beside it, the
	/// registry is keyed by building <c>Key</c>, cleared and refilled by the loader's single pass;
	/// nothing here is a per-city or realm-level field, and nothing accumulates across passes. A
	/// shortfall's "said once" flag lives on the work object itself
	/// (<see cref="ShortfallAnnouncedProperty"/>), the same idiom <c>r_KingdomPlot</c> already uses
	/// for its own once-only announcements, so a shortfall a founder fixes stops repeating without
	/// this file remembering anything about the pass that fixed it.
	/// </para>
	/// <para>
	/// <b>Derive before authoring.</b> <see cref="CapabilityOf"/> reads <c>Strength</c> and
	/// <c>Intelligence</c> straight off <c>GameObject.GetStatValue</c>, and asks
	/// <c>KingdomQol.TruthOf</c> &mdash; the vocabulary's own robot read &mdash; whether this
	/// settler is a robot, rather than testing <c>HasPart&lt;Robot&gt;()</c> a second time. A
	/// modded creature with real stats and no special part is therefore a correct crew member
	/// before its author has heard of this file.
	/// </para>
	/// </summary>
	public static partial class KingdomCrews
	{
		/// <summary>The separate Addendum 17 factor stamped beside crew effectiveness. Absent
		/// means neutral for every work built by an older save.</summary>
		public const string IdentityAffinityProperty = "KingdomIdentityAffinity";

		public static int AffinityOf(GameObject Work)
		{
			if (!GameObject.Validate(Work)) return KingdomIdentityAffinityRules.NeutralPercent;
			int stored = Work.GetIntProperty(IdentityAffinityProperty);
			return stored == 0 ? KingdomIdentityAffinityRules.NeutralPercent
				: KingdomIdentityAffinityRules.Clamp(stored);
		}

		public static int ApplyAffinity(GameObject Work, int Value)
		{
			return KingdomIdentityAffinityRules.Apply(Value, AffinityOf(Work));
		}

		// Keyed by building Key like KingdomQol's own Declared registry (STANDARDS 6): a later
		// file re-using a key owns that design's whole CrewNeeds, and a re-declaration that names
		// no CrewNeeds at all correctly leaves it with none.
		private static readonly Dictionary<string, string> Declared = new Dictionary<string, string>();

		private static readonly Dictionary<string, List<KindAmount>> NeedsCache = new Dictionary<string, List<KindAmount>>();

		private static readonly List<KindAmount> EmptyNeeds = new List<KindAmount>();

		/// <summary>Forgets every registered <c>CrewNeeds</c>. Called by the registry loader
		/// before it re-reads the XML streams, beside <c>KingdomQol.ClearProvides</c>.</summary>
		public static void ClearCrewNeeds()
		{
			Declared.Clear();
			NeedsCache.Clear();
		}

		/// <summary>
		/// Registers one entry's <c>CrewNeeds</c> as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the merged raw attribute;
		/// null or blank registers "needs no particular hand", which is every design written
		/// before this attribute existed.
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="CrewNeeds">Raw <c>CrewNeeds</c> attribute: a <c>kind:amount</c> list in
		/// <see cref="KingdomCrewRules.TryParseCrewNeeds"/>'s language. A malformed value is
		/// logged and the design is left needing nothing, never refused outright &mdash; a
		/// third-party typo must not be able to take down a design the base catalogue relies on.
		/// </param>
		public static void RegisterCrewNeeds(string Key, string CrewNeeds)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			NeedsCache.Remove(Key);
			if (string.IsNullOrEmpty(CrewNeeds) || CrewNeeds.Trim().Length == 0)
			{
				Declared.Remove(Key);
				return;
			}
			if (!KingdomCrewRules.TryParseCrewNeeds(CrewNeeds, out var needs, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " CrewNeeds \"" + CrewNeeds + "\" " + error + ".");
				return;
			}
			Declared[Key] = CrewNeeds;
			for (int i = 0; i < needs.Count; i++)
			{
				if (!KingdomCrewRules.IsKnownKind(needs[i].Kind))
				{
					// A note, never a fault, exactly as KingdomQol logs an un-namespaced Provides
					// tag: a kind no stat answers to yet in THIS build may be read by a later wave
					// or another mod's own crew system.
					KingdomLog.Log("KingdomBuildings: building " + Key + " CrewNeeds names \"" + needs[i].Kind + "\", which no crew stat answers to yet.");
				}
			}
		}

		/// <summary>The parsed <c>CrewNeeds</c> for one design. Never null; empty for a design
		/// that declares none.</summary>
		public static List<KindAmount> NeedsOf(string BuildingKey)
		{
			if (string.IsNullOrEmpty(BuildingKey))
			{
				return EmptyNeeds;
			}
			if (NeedsCache.TryGetValue(BuildingKey, out var cached))
			{
				return cached;
			}
			List<KindAmount> needs = EmptyNeeds;
			if (Declared.TryGetValue(BuildingKey, out var declared) && KingdomCrewRules.TryParseCrewNeeds(declared, out var parsed, out _))
			{
				needs = parsed;
			}
			NeedsCache[BuildingKey] = needs;
			return needs;
		}

		/// <summary>What a work standing on the ground needs, read off the design key it was
		/// raised under (<c>KingdomUpgrade.BuildKeyProperty</c>). This is an authored crew contract,
		/// not a physical-benefit read. A work with no key on it needs
		/// nothing in particular.</summary>
		public static List<KindAmount> NeedsOf(GameObject Work)
		{
			if (!GameObject.Validate(Work))
			{
				return EmptyNeeds;
			}
			if (Work.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1
				|| Work.Blueprint == KingdomAdopt.WorkMarkerBlueprint)
				return KingdomAdoptionOperation.TryRead(Work,
					out KingdomAdoptionOperationReceipt adopted, out _)
					? NeedsOf(adopted.BuildingKey) : EmptyNeeds;
			return NeedsOf(Work.GetStringProperty(KingdomUpgrade.BuildKeyProperty));
		}

		// --- Reading a settler ----------------------------------------------------------------

		/// <summary>What one real settler brings to a crew: <c>Strength</c> and
		/// <c>Intelligence</c> read straight off their stats, plus whether they are a robot
		/// (<c>KingdomQol.TruthOf</c>'s own read, reused rather than repeated).</summary>
		public static KingdomCrewRules.SettlerCapability CapabilityOf(GameObject Settler)
		{
			if (Settler == null)
			{
				return default(KingdomCrewRules.SettlerCapability);
			}
			int strength = Settler.GetStatValue("Strength");
			int intelligence = Settler.GetStatValue("Intelligence");
			bool tireless = KingdomQol.TruthOf(Settler).Robot;
			GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(
				Settler.Blueprint);
			KingdomIdentityAffinityRules.WorkerIdentity identity =
				new KingdomIdentityAffinityRules.WorkerIdentity(Settler.GetCulture(),
					Settler.GetSpecies(), Fragment(blueprint, "Activity"),
					Fragment(blueprint, "VillageActivity"), Fragment(blueprint, "ValuedOre"),
					Fragment(blueprint, "SacredThing"), Fragment(blueprint, "ArableLand"));
			KingdomCrewRules.WorkerSkills skills = new KingdomCrewRules.WorkerSkills(
				HasAnySkill(Settler, "Tinkering", "Tinkering_GadgetInspector",
					"Tinkering_Repair", "Tinkering_Tinker1", "Tinkering_Tinker2",
					"Tinkering_Tinker3"),
				HasAnySkill(Settler, "CookingAndGathering",
					"CookingAndGathering_Harvestry"),
				HasAnySkill(Settler, "Customs", "Customs_Tactful"),
				HasAnySkill(Settler, "Physic", "Physic_StaunchWounds", "Physic_Nostrums",
					"Physic_AmputateLimb", "Physic_Apothecary"),
				HasAnySkill(Settler, "Survival", "Survival_Camp", "Survival_Trailblazer"));
			return new KingdomCrewRules.SettlerCapability(strength, intelligence, tireless,
				identity, skills);
		}

		private static bool HasAnySkill(GameObject Settler, params string[] Skills)
		{
			if (Settler == null || Skills == null) return false;
			for (int i = 0; i < Skills.Length; i++)
				if (Settler.HasSkill(Skills[i])) return true;
			return false;
		}

		private static string Fragment(GameObjectBlueprint Blueprint, string Name)
		{
			return Blueprint?.GetxTag("TextFragments", Name, null);
		}

	}
}
