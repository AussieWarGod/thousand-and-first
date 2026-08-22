using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name. This part is
// only ever added in code, but it lives here anyway alongside r_KingdomScaffold and
// r_KingdomPlot: a part whose namespace depends on how it happened to be attached is a trap
// waiting for the first blueprint that names it.
namespace XRL.World.Parts
{
	/// <summary>
	/// The settlement's intent about one work it raised: what that work is to become, whether
	/// the founder has told the settlement to leave it alone, and &mdash; while an improvement
	/// is actually under way &mdash; the handover of everything the old work was carrying into
	/// the new one.
	/// <para>
	/// Attached lazily, and only to a work whose design actually names a successor, so an
	/// ordinary building never gains a part it has no use for. Absent means "no opinion", which
	/// is the state every building in every existing save is in.
	/// </para>
	/// <para>
	/// The work itself is <c>r_KingdomScaffold</c>'s, not this part's. This part does one thing
	/// the scaffold cannot: the scaffold creates the successor and destroys itself, and nobody
	/// but the predecessor knows what the predecessor was holding. So the predecessor keeps
	/// standing, and working, for the whole build, and steps aside only once its replacement is
	/// actually on the ground with the contents safely moved across.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomImprovement : IPart
	{
		/// <summary>Registry key of the design being built while <see cref="Working"/>. Null
		/// otherwise; the design this work grows into in general is read from the registry, not
		/// from here.</summary>
		public string SuccessorKey;

		/// <summary>Blueprint the scaffold was pointed at, so the finished object can be
		/// recognised in the cell without trusting the scaffold to have survived.</summary>
		public string SuccessorBlueprint;

		/// <summary>The founder's standing "leave this one as it is". Persists on the object,
		/// shows in its description, and is only ever set or cleared from the Charter.</summary>
		public bool Held;

		/// <summary>True between the scaffold going up and the handover completing.</summary>
		public bool Working;

		/// <summary>The scaffold raised for this improvement. Goes invalid the instant the
		/// scaffold completes &mdash; which is exactly the signal the handover waits on.</summary>
		public GameObject Scaffold;

		/// <summary>Tick the scaffold was due to finish, used only to decide when a work that
		/// never appeared has to be given up on rather than polled forever.</summary>
		public long WorkCompleteTick;

		/// <summary>
		/// The <c>KingdomUpgradeRules.UpgradeVerdict</c> last announced to the founder for this
		/// work, as an int. Zero means nothing has been announced &mdash; unambiguous because
		/// zero is <c>Ready</c>, which is never announced as a block. Announcing again is gated
		/// on the reason having actually CHANGED, so a settlement that has been short of water
		/// for a season says so once and then stops.
		/// </summary>
		public int AnnouncedReason;

		/// <summary>
		/// Ticks past the scaffold's due time before an improvement whose successor never
		/// appeared is given up on. Generous: the scaffold only ticks in an active zone, so a
		/// founder who walks away mid-build must be able to come back to the same work.
		/// </summary>
		public const long AbandonGraceTicks = 2400L;

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (Working)
			{
				KingdomSystem.Guard("improvement handover", delegate
				{
					PollHandover(TimeTick);
				});
			}
			base.TurnTick(TimeTick, Amount);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		/// <summary>
		/// Puts the settlement's intent for this work on the work itself, so the founder can
		/// read it by looking at the thing rather than only in the Charter.
		/// </summary>
		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (Working)
			{
				E.Postfix.Append("\n{{rules|The settlement is raising this into ")
					.Append(KingdomUpgrade.DisplayNameOf(SuccessorKey))
					.Append(".}}");
			}
			else if (Held)
			{
				E.Postfix.Append("\n{{rules|The settlement will leave this as it is.}}");
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Watches for the scaffold's own completion and moves everything across when it comes.
		/// Called once a turn while an improvement is under way, and does nothing at all until
		/// the scaffold is gone, which is cheap.
		/// </summary>
		/// <param name="TimeTick">Engine tick, for the abandonment grace period.</param>
		public void PollHandover(long TimeTick)
		{
			if (GameObject.Validate(ref Scaffold))
			{
				return;
			}
			Cell cell = ParentObject?.CurrentCell;
			GameObject successor = FindSuccessor(cell);
			if (successor != null)
			{
				KingdomUpgrade.HandOver(ParentObject, successor, SuccessorKey);
				return;
			}
			if (TimeTick < WorkCompleteTick + AbandonGraceTicks)
			{
				return;
			}
			// The scaffold is gone and nothing was raised: something took it down. Nothing was
			// destroyed here - this work never stopped working - but the water is spent, the same
			// way a commissioned scaffold's is, and the founder is owed the news.
			Working = false;
			SuccessorKey = null;
			SuccessorBlueprint = null;
			AnnouncedReason = 0;
			MessageQueue.AddPlayerMessage("{{r|The work on " + KingdomDesign.ReferenceFor(ParentObject, ParentObject.ShortDisplayName) + " came to nothing. It stands as it did.}}");
			KingdomLog.Log("improvement abandoned: " + ParentObject.Blueprint + " scaffold lost");
		}

		/// <summary>
		/// The finished successor, once it is standing in the same cell. Matched on blueprint and
		/// on the settlement's own build mark, so an unrelated object dropped in the cell mid-
		/// build can never be mistaken for the new work.
		/// </summary>
		/// <param name="Where">Cell this work stands in. Null finds nothing.</param>
		public GameObject FindSuccessor(Cell Where)
		{
			if (Where == null || string.IsNullOrEmpty(SuccessorBlueprint))
			{
				return null;
			}
			List<GameObject> objects = Where.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject candidate = objects[i];
				if (candidate != ParentObject && candidate.Blueprint == SuccessorBlueprint
					&& candidate.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1)
				{
					return candidate;
				}
			}
			return null;
		}
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Works that get better. A design may name what it grows into; when the settlement has
	/// earned it, the settlement raises the new work itself, out of what its stores can spare,
	/// through the same scaffold every commission uses &mdash; so an improvement is visibly work
	/// happening on the ground, not a number changing.
	/// <para>
	/// Improvements are AUTOMATIC, with a standing opt-out. That is a deliberate choice against
	/// the alternative of offering each one:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Clicking through a confirmation for every work at every stage is the
	/// foreman's job the mod refuses to hand the player. The founder sets intent; the settlement
	/// acts on it.</description></item>
	/// <item><description>An improvement only ever adds. Everything the old work held and
	/// everything the settlement had marked on it is carried across, so a founder who never
	/// opens the Charter loses nothing they had and simply comes home to a better
	/// settlement.</description></item>
	/// <item><description>It is never a surprise, because it is announced three times before it
	/// is a fact: once per game when the settlement first becomes able to do this at all, once
	/// when a particular work starts, and continuously by the scaffold standing in the
	/// cell.</description></item>
	/// <item><description>It can always be refused, permanently, without losing anything: a
	/// single work, or this whole ground, can be held as it is, and that choice persists and is
	/// visible on the object itself.</description></item>
	/// <item><description>It cannot cause a thirst. The cost never draws the stores below the
	/// reserve the settlement lives on, and it never spends a settler who is doing something
	/// else.</description></item>
	/// </list>
	/// <para>
	/// The arithmetic and every refusal sentence are in <see cref="KingdomUpgradeRules"/>.
	/// </para>
	/// </summary>
	public static class KingdomUpgrade
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionImprovement") != "No";

		public const string BuiltProperty = "KingdomBuilt";

		public const string AdoptedProperty = "KingdomAdopted";

		/// <summary>
		/// The registry key a standing work was raised as, stamped by this file when one work
		/// becomes another so the next link of a chain resolves exactly rather than by guessing
		/// from the blueprint. Absent on everything built before improvements existed, which is
		/// why <see cref="DesignKeyOf"/> falls back to the blueprint.
		/// </summary>
		public const string BuildKeyProperty = "KingdomBuildKey";

		/// <summary>Game state remembering that the founder has been told, once, that the
		/// settlement betters its own works.</summary>
		public const string NoticedState = "r_TAF_ImprovementNoticed";

		/// <summary>Prefix of the per-zone game state carrying "leave this ground as it is".
		/// Keyed by zone rather than by settlement because a founder's wish to keep a camp crude
		/// is about a place, and because it then works without a new serialized field on any
		/// existing save.</summary>
		public const string GroundHeldState = "r_TAF_ImprovementHeld:";

		// Chains live beside the catalog rather than inside KingdomRules.BuildEntry so the registry
		// parser needs one line of wiring instead of a rewritten entry type. Filled by
		// KingdomData's single pass over the mergeable KingdomBuildings root, in that file's load
		// order and with the same last-wins override, so a third-party file can add a chain to our
		// design, replace ours, or clear it by re-declaring the entry without an UpgradesTo.
		private static readonly Dictionary<string, KingdomUpgradeRules.UpgradeChain> _chains = new Dictionary<string, KingdomUpgradeRules.UpgradeChain>();

		/// <summary>Every upgrade chain the loaded <c>KingdomBuildings</c> files declare, keyed by
		/// the design that grows.</summary>
		public static Dictionary<string, KingdomUpgradeRules.UpgradeChain> Chains
		{
			get
			{
				KingdomData.EnsureBuildings();
				return _chains;
			}
		}

		/// <summary>
		/// Forgets every registered chain. Called by the registry loader before it re-reads the
		/// XML streams, so a reload never leaves a chain behind for a design that no longer
		/// declares one.
		/// </summary>
		public static void ClearChains()
		{
			_chains.Clear();
		}

		/// <summary>
		/// Registers one entry's upgrade attributes as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the raw attribute
		/// strings; all five may be null, which registers "this design never changes".
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="UpgradesTo">Raw <c>UpgradesTo</c> attribute.</param>
		/// <param name="UpgradeCost">Raw <c>UpgradeCost</c> attribute.</param>
		/// <param name="UpgradeTicks">Raw <c>UpgradeTicks</c> attribute.</param>
		/// <param name="UpgradeCrew">Raw <c>UpgradeCrew</c> attribute.</param>
		/// <param name="UpgradeMinStage">Raw <c>UpgradeMinStage</c> attribute.</param>
		public static void RegisterChain(string Key, string UpgradesTo, string UpgradeCost, string UpgradeTicks, string UpgradeCrew, string UpgradeMinStage)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomUpgradeRules.TryParseUpgradeAttributes(Key, UpgradesTo, UpgradeCost, UpgradeTicks, UpgradeCrew, UpgradeMinStage, out KingdomUpgradeRules.UpgradeChain chain, out string error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				// A malformed chain leaves the design unable to change rather than half-chained,
				// and clears whatever an earlier file registered under this key: the entry that
				// carried it has just been replaced.
				chain = new KingdomUpgradeRules.UpgradeChain();
			}
			_chains[Key] = chain;
		}

		/// <summary>Drops the parsed chains so the next read re-reads the XML. For the dev
		/// reload wish; ordinary play never needs it.</summary>
		public static void Reload()
		{
			KingdomData.Reload();
		}

		/// <summary>The chain a design declares, if any.</summary>
		/// <param name="Key">Registry key of the standing design.</param>
		/// <param name="Chain">The chain, or null.</param>
		/// <returns>True only when a usable chain was declared.</returns>
		public static bool TryGetChain(string Key, out KingdomUpgradeRules.UpgradeChain Chain)
		{
			Chain = null;
			if (string.IsNullOrEmpty(Key))
			{
				return false;
			}
			KingdomData.EnsureBuildings();
			return _chains.TryGetValue(Key, out Chain) && Chain != null && Chain.Defined;
		}

		/// <summary>
		/// What a standing work counts as in the registry. Prefers the key the settlement
		/// stamped when it raised or improved the work, then the key it was adopted under, and
		/// only then reads the blueprint back &mdash; which is what lets works raised before
		/// improvements existed take part without a migration.
		/// </summary>
		/// <param name="Work">The standing object.</param>
		/// <returns>A registry key, or null when no design matches.</returns>
		public static string DesignKeyOf(GameObject Work)
		{
			if (Work == null)
			{
				return null;
			}
			string stamped = Work.GetStringProperty(BuildKeyProperty);
			if (!string.IsNullOrEmpty(stamped))
			{
				return stamped;
			}
			string adopted = Work.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
			if (!string.IsNullOrEmpty(adopted))
			{
				return adopted;
			}
			List<KingdomRules.BuildEntry> entries = KingdomData.Buildings;
			List<string> keys = new List<string>();
			List<bool> chained = new List<bool>();
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].Blueprint == Work.Blueprint)
				{
					keys.Add(entries[i].Key);
					chained.Add(TryGetChain(entries[i].Key, out _));
				}
			}
			int chosen = KingdomUpgradeRules.ChooseDesignIndex(chained.ToArray());
			return (chosen < 0) ? null : keys[chosen];
		}

		/// <summary>What a design is called, for a sentence. Falls back to the key so a
		/// half-loaded registry still produces readable prose.</summary>
		public static string DisplayNameOf(string Key)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return "something better";
			}
			if (KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry))
			{
				return entry.Name;
			}
			return Key;
		}

		/// <summary>Whether the founder has told the settlement to leave this whole ground as it
		/// is.</summary>
		/// <param name="Z">Zone to ask about. Null is never held.</param>
		public static bool IsGroundHeld(Zone Z)
		{
			if (Z == null || The.Game == null)
			{
				return false;
			}
			return The.Game.GetIntGameState(GroundHeldState + Z.ZoneID) == 1;
		}

		/// <summary>Sets or clears "leave this ground as it is". Nothing standing is changed
		/// either way; only what the settlement will do next.</summary>
		/// <param name="Z">Zone to hold or release.</param>
		/// <param name="Hold">True to hold.</param>
		public static void SetGroundHeld(Zone Z, bool Hold)
		{
			if (Z != null && The.Game != null)
			{
				The.Game.SetIntGameState(GroundHeldState + Z.ZoneID, Hold ? 1 : 0);
			}
		}

		/// <summary>Everything the settlement knows about one work's improvement, without
		/// changing anything.</summary>
		public struct Assessment
		{
			/// <summary>False when there was nothing to assess at all.</summary>
			public bool Valid;

			public KingdomUpgradeRules.UpgradeVerdict Verdict;

			/// <summary>Registry key of the standing design, or null.</summary>
			public string Key;

			/// <summary>Registry key of the design it grows into, or null.</summary>
			public string SuccessorKey;

			/// <summary>The successor's registry entry, or null when it did not resolve.</summary>
			public KingdomRules.BuildEntry Successor;

			public int CostDrams;

			public int Reserve;

			public int Shortfall;

			public int CrewNeeded;

			public GrowthStage StageNeeded;

			public long BuildTicks;

			/// <summary>Sustained output this work contributes, in drams a day &mdash; the
			/// <c>water</c> it carries. Zero for a work the settlement does not drink from.
			/// </summary>
			public int SupportPerDay;

			/// <summary>Drams the settlement goes without while this work is rebuilt, from
			/// <c>KingdomUpgradeRules.OutputLost</c>.</summary>
			public int OutputLost;

			/// <summary>Drams the stores would still hold above the reserve once the improvement
			/// is paid for and the outage borne. Negative is the dip a forced improvement takes.
			/// </summary>
			public int Margin;

			/// <summary>What the absorption law was told about this work, kept so the Charter can
			/// disclose the dip without measuring it a second time.</summary>
			public KingdomUpgradeRules.AbsorptionDemand Demand;

			/// <summary>The sentence the founder is owed, or null when the verdict correctly
			/// says nothing.</summary>
			public string Reason;
		}

		/// <summary>
		/// Reads one standing work against the settlement and reports what its improvement is
		/// waiting on, changing nothing. Safe to call for a listing.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the work stands in.</param>
		/// <param name="Work">The standing work.</param>
		/// <param name="Survey">This pass's survey, for the stores.</param>
		/// <param name="FreeHands">Settlers not already spoken for.</param>
		/// <param name="OtherWorkUnderway">Whether another improvement is already going on this
		/// ground.</param>
		public static Assessment Assess(KingdomSystem System, Zone Z, GameObject Work, KingdomSurvey Survey, int FreeHands, bool OtherWorkUnderway)
		{
			Assessment assessment = default;
			if (System == null || !System.Founded || Z == null || Survey == null)
			{
				return assessment;
			}
			if (Work == null || !GameObject.Validate(Work))
			{
				return assessment;
			}
			assessment.Valid = true;
			assessment.Key = DesignKeyOf(Work);
			if (!TryGetChain(assessment.Key, out KingdomUpgradeRules.UpgradeChain chain))
			{
				assessment.Verdict = KingdomUpgradeRules.UpgradeVerdict.NoSuccessor;
				return assessment;
			}
			assessment.SuccessorKey = chain.SuccessorKey;
			bool known = KingdomData.TryGetBuilding(chain.SuccessorKey, out KingdomRules.BuildEntry successor)
				&& GameObjectFactory.Factory.GetBlueprintIfExists(successor.Blueprint) != null;
			assessment.Successor = known ? successor : null;
			KingdomRules.BuildEntry predecessor;
			int predecessorCost = KingdomData.TryGetBuilding(assessment.Key, out predecessor) ? predecessor.CostDrams : 0;
			assessment.CostDrams = KingdomUpgradeRules.CostDrams(known ? successor.CostDrams : 0, predecessorCost, chain.CostDramsOverride);
			assessment.BuildTicks = KingdomUpgradeRules.BuildTicks(known ? successor.BuildTicks : 0L, chain.BuildTicksOverride);
			assessment.CrewNeeded = KingdomUpgradeRules.CrewRequired(known ? successor.Staff : 0, chain.CrewOverride);
			assessment.StageNeeded = KingdomUpgradeRules.StageRequired(known ? successor.MinStage : GrowthStage.Camp, chain.HasMinStageOverride, chain.MinStageOverride);
			assessment.Reserve = KingdomUpgradeRules.ReserveDrams(System.Population, System.Stage);
			assessment.Shortfall = KingdomUpgradeRules.Shortfall(Survey.StoredWater, assessment.CostDrams, assessment.Reserve);
			// The absorption law (brief, Addendum 3). Measured here, judged in the rules half.
			assessment.Demand = MeasureAbsorption(System, Z, Work, predecessor, assessment.SuccessorKey, assessment.BuildTicks);
			assessment.SupportPerDay = assessment.Demand.SupportPerDay;
			assessment.OutputLost = KingdomUpgradeRules.OutputLost(assessment.Demand.SupportPerDay, assessment.BuildTicks);
			assessment.Margin = KingdomUpgradeRules.AbsorptionMargin(Survey.StoredWater, assessment.CostDrams, assessment.Reserve, assessment.OutputLost);
			r_KingdomImprovement improvement = Work.GetPart<r_KingdomImprovement>();
			assessment.Verdict = KingdomUpgradeRules.Assess(
				HasSuccessor: true,
				SuccessorKnown: known,
				StyleAllowed: !known || KingdomRules.StyleAllows(successor.Styles, System.Style),
				OurWork: Work.GetIntProperty(BuiltProperty) == 1 && Work.GetIntProperty(AdoptedProperty) != 1,
				AlreadyWorking: improvement != null && improvement.Working,
				HeldOnThisGround: IsGroundHeld(Z),
				HeldByFounder: improvement != null && improvement.Held,
				Stage: System.Stage,
				StageNeeded: assessment.StageNeeded,
				FreeHands: FreeHands,
				CrewNeeded: assessment.CrewNeeded,
				ContentsFit: ContentsWouldFit(Work, known ? successor.Blueprint : null),
				StoredWater: Survey.StoredWater,
				Cost: assessment.CostDrams,
				Reserve: assessment.Reserve,
				OtherWorkUnderway: OtherWorkUnderway,
				Absorption: assessment.Demand);
			assessment.Reason = KingdomUpgradeRules.ReasonLine(assessment.Verdict, KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName), known ? successor.Name : null, assessment.StageNeeded, assessment.CrewNeeded, assessment.Shortfall);
			// An improvement climbs within the ground it was staked on. When the next tier wants more
			// of the plot than the founder staked, or the ground it would grow onto is where a
			// household's yard trade stands, the founder is told by name and chooses.
			if (KingdomUpgradeRules.IsReady(assessment.Verdict)
				&& KingdomPlots.GrowRefused(Work, assessment.SuccessorKey, out string groundRefusal))
			{
				assessment.Verdict = KingdomUpgradeRules.UpgradeVerdict.NoGroundToGrow;
				assessment.Reason = groundRefusal;
			}
			return assessment;
		}

		/// <summary>
		/// Whether everything the predecessor is carrying would have somewhere to go. Read off
		/// the successor's BLUEPRINT rather than a created object, because this is asked before
		/// anything is built and the answer must be able to refuse.
		/// </summary>
		/// <param name="Work">The standing work.</param>
		/// <param name="SuccessorBlueprint">Blueprint of what it would become. Null fits
		/// nothing that is being carried.</param>
		public static bool ContentsWouldFit(GameObject Work, string SuccessorBlueprint)
		{
			if (Work == null)
			{
				return false;
			}
			int storedLiquid = 0;
			LiquidVolume volume = Work.GetPart<LiquidVolume>();
			if (volume != null && volume.Volume > 0)
			{
				storedLiquid = volume.Volume;
			}
			int heldItems = (Work.Inventory != null) ? Work.Inventory.Objects.Count : 0;
			GameObjectBlueprint blueprint = string.IsNullOrEmpty(SuccessorBlueprint) ? null : GameObjectFactory.Factory.GetBlueprintIfExists(SuccessorBlueprint);
			if (blueprint == null)
			{
				return storedLiquid <= 0 && heldItems <= 0;
			}
			int capacity = 0;
			if (blueprint.HasPart("LiquidVolume"))
			{
				capacity = blueprint.HasPartParameter("LiquidVolume", "MaxVolume")
					? blueprint.GetPartParameter("LiquidVolume", "MaxVolume", KingdomUpgradeRules.UnknownCapacity)
					: KingdomUpgradeRules.UnknownCapacity;
			}
			return KingdomUpgradeRules.ContentsWouldFit(storedLiquid, capacity, heldItems, blueprint.HasPart("Inventory"));
		}

		/// <summary>
		/// Measures everything the absorption law (brief, Addendum 3) judges, off real ground.
		/// Nothing here reads the clock, the age of the work, or how long anything has stood: the
		/// figures are what the settlement holds right now and what the designs declare. The only
		/// duration involved is the improvement's own build time, which sizes the outage and never
		/// causes the trigger.
		/// </summary>
		/// <param name="System">The kingdom, for its population.</param>
		/// <param name="Z">Zone the work stands in, walked once for the lodging elsewhere.</param>
		/// <param name="Work">The standing work.</param>
		/// <param name="Predecessor">Its registry entry, or null when it did not resolve.</param>
		/// <param name="SuccessorKey">Registry key of the design it would become.</param>
		/// <param name="BuildTicks">The improvement's build time, from
		/// <c>KingdomUpgradeRules.BuildTicks</c>.</param>
		public static KingdomUpgradeRules.AbsorptionDemand MeasureAbsorption(KingdomSystem System, Zone Z, GameObject Work, KingdomRules.BuildEntry Predecessor, string SuccessorKey, long BuildTicks)
		{
			KingdomUpgradeRules.AbsorptionDemand demand = KingdomUpgradeRules.AbsorptionDemand.None;
			demand.BuildTicks = BuildTicks;
			if (Predecessor == null)
			{
				return demand;
			}
			List<KindAmount> carries;
			if (!KingdomCatalogueRules.TryParseTally(Predecessor.Carries, out carries, out _))
			{
				// A malformed Carries is already reported by the catalogue validator. Everything it
				// managed to parse still counts, which is what TryParseTally hands back.
			}
			demand.IsHousing = string.Equals(Predecessor.Category, HousingCategory, StringComparison.OrdinalIgnoreCase);
			demand.Residents = KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportRoof);
			demand.LuxuryCarried = KingdomCatalogueRules.AmountOf(carries, LuxurySupport);
			demand.SupportPerDay = KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportWater);
			demand.CurrentShelter = ShelterOf(Predecessor.Key);
			int lodgingElsewhere = 0;
			int bestShelter = 0;
			string bestKey = null;
			if (Z != null)
			{
				foreach (GameObject item in Z.GetObjects())
				{
					if (item == Work || item.GetIntProperty(BuiltProperty) != 1)
					{
						continue;
					}
					string key = DesignKeyOf(item);
					KingdomRules.BuildEntry entry;
					if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
					{
						continue;
					}
					List<KindAmount> theirs;
					KingdomCatalogueRules.TryParseTally(entry.Carries, out theirs, out _);
					int roof = KingdomCatalogueRules.AmountOf(theirs, KingdomCatalogueRules.SupportRoof);
					if (roof <= 0)
					{
						continue;
					}
					lodgingElsewhere += roof;
					int shelter = ShelterOf(key);
					if (shelter > bestShelter || bestKey == null)
					{
						bestShelter = (shelter > bestShelter) ? shelter : bestShelter;
						bestKey = key;
					}
				}
			}
			int spare = lodgingElsewhere - ((System == null) ? 0 : System.Population);
			demand.SpareLodging = (spare > 0) ? spare : 0;
			demand.OfferedShelter = bestShelter;
			// Addendum 4: the best roof on offer must also be somewhere these people would actually
			// live. One citizen with nowhere to charge holds the rebuild exactly as a missing roof
			// does -- and holds it only: nobody is moved, and the refusal is named by the verdict.
			demand.QuartersRefused = demand.IsHousing
				&& KingdomUpgradeRules.QuartersRefused(KingdomQol.OfferOf(bestKey, Z), ResidentProfilesIn(Z), out _);
			demand.MaterialsInHand = Z == null || KingdomMaterials.CanPayUpgrade(Z, SuccessorKey, out _);
			demand.CraftMet = CraftReaches(System, Z, SuccessorKey);
			return demand;
		}

		/// <summary>
		/// The quality-of-life profiles of the citizens standing on this ground, for the Needs
		/// check Addendum 4 re-bases displacement tolerance onto. Read fresh every time, because
		/// nothing in that vocabulary is stored anywhere.
		/// </summary>
		/// <returns>Never null; empty for a null zone or a zone with nobody in it, which refuses
		/// nothing.</returns>
		private static List<QolProfile> ResidentProfilesIn(Zone Z)
		{
			List<QolProfile> profiles = new List<QolProfile>();
			if (Z == null)
			{
				return profiles;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1)
				{
					profiles.Add(KingdomQol.ProfileOf(item));
				}
			}
			return profiles;
		}

		/// <summary>The catalogue category housing is filed under, which the absorption law judges
		/// by displacement rather than by the output margin.</summary>
		public const string HousingCategory = "housing";

		/// <summary>The lifting support the luxury lane is denominated in. A design that lifts it
		/// houses somebody with a standard; one that does not houses settlers.</summary>
		public const string LuxurySupport = "luxury";

		/// <summary>
		/// Shelter rank of a design's own tier. A design that is not a plot has no roof state of
		/// its own and is read as a walled room, because a single-cell work the settlement raised
		/// stands as an object with its own walls rather than as open ground.
		/// </summary>
		/// <param name="Key">Registry key of the design.</param>
		public static int ShelterOf(string Key)
		{
			KingdomPlotRules.PlotSpec spec;
			if (string.IsNullOrEmpty(Key) || !KingdomPlots.TryGetSpec(Key, out spec) || spec == null)
			{
				return KingdomUpgradeRules.RoomShelter;
			}
			return KingdomPlotRules.ShelterRank(spec.Roof);
		}

		/// <summary>
		/// Whether the settlement's craft and learning reach a design. The district and territory
		/// gates are deliberately NOT applied: the predecessor is already standing on this ground,
		/// so re-asking where it may stand would refuse improvements the founder sited legitimately
		/// and could no longer do anything about.
		/// </summary>
		public static bool CraftReaches(KingdomSystem System, Zone Z, string Key)
		{
			KingdomRules.BuildEntry entry;
			if (System == null || string.IsNullOrEmpty(Key) || !KingdomData.TryGetBuilding(Key, out entry))
			{
				return true;
			}
			ZoningJudgement judgement = KingdomZoning.Judge(System, Z?.ZoneID, entry);
			return judgement.Verdict != ZoningVerdict.RefusedUnlearned
				&& judgement.Verdict != ZoningVerdict.RefusedTechLevel;
		}

		/// <summary>
		/// The settlement's improvement pass: completes any handover that finished while the
		/// founder was away, then starts at most one improvement and says at most one thing.
		/// Called from the settlement's zone-activated pass after growth, because growth is what
		/// decides which settlers are already spoken for.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the founder is standing in.</param>
		/// <param name="Survey">This pass's survey.</param>
		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			// HandOver asks for one more pass so a founder who stands and watches sees the next
			// work start. That call must not re-enter this one: the settlement betters one work
			// per visit, and a pass that started an improvement inside its own handover would
			// start as many as there were works to hand over.
			if (_resolving)
			{
				return;
			}
			_resolving = true;
			try
			{
				Resolve(System, Z, Survey);
			}
			finally
			{
				_resolving = false;
			}
		}

		// True while OnZoneActivated is inside its own pass. Not serialized and not state: it
		// describes the call stack, not the settlement.
		private static bool _resolving;

		private static void Resolve(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			int freeHands = System.Population - System.AssignedCrew;
			if (freeHands < 0)
			{
				freeHands = 0;
			}
			// Finished improvements are handed over here as well as on the work's own turn tick.
			// The tick is the responsive path; this is the one that cannot be missed, because the
			// settlement pass runs whenever the founder is standing here and a handover that never
			// happens leaves the old work standing beside its replacement forever.
			List<GameObject> pending = new List<GameObject>();
			foreach (GameObject item in Z.GetObjects())
			{
				r_KingdomImprovement working = item.GetPart<r_KingdomImprovement>();
				if (working != null && working.Working)
				{
					pending.Add(item);
				}
			}
			for (int i = 0; i < pending.Count; i++)
			{
				GameObject item = pending[i];
				if (!GameObject.Validate(ref item))
				{
					continue;
				}
				r_KingdomImprovement working = item.GetPart<r_KingdomImprovement>();
				if (working != null && working.Working)
				{
					working.PollHandover(The.Game.TimeTicks);
				}
			}
			List<GameObject> works = new List<GameObject>();
			bool otherWorkUnderway = false;
			foreach (GameObject item in Z.GetObjects())
			{
				r_KingdomImprovement improvement = item.GetPart<r_KingdomImprovement>();
				if (improvement != null && improvement.Working)
				{
					otherWorkUnderway = true;
				}
				if (item.GetIntProperty(BuiltProperty) == 1)
				{
					works.Add(item);
				}
			}
			GameObject readyWork = null;
			Assessment readyAssessment = default;
			GameObject speaksFirst = null;
			Assessment speaksFirstAssessment = default;
			bool anyImprovable = false;
			for (int i = 0; i < works.Count; i++)
			{
				Assessment assessment = Assess(System, Z, works[i], Survey, freeHands, otherWorkUnderway);
				if (!assessment.Valid || assessment.Verdict == KingdomUpgradeRules.UpgradeVerdict.NoSuccessor)
				{
					continue;
				}
				anyImprovable = true;
				if (KingdomUpgradeRules.IsReady(assessment.Verdict) && readyWork == null)
				{
					readyWork = works[i];
					readyAssessment = assessment;
				}
				else if (KingdomUpgradeRules.IsBlocked(assessment.Verdict) && speaksFirst == null
					&& works[i].RequirePart<r_KingdomImprovement>().AnnouncedReason != (int)assessment.Verdict)
				{
					speaksFirst = works[i];
					speaksFirstAssessment = assessment;
				}
			}
			if (anyImprovable)
			{
				GiveFirstNotice(System);
			}
			if (readyWork != null)
			{
				Begin(System, Z, readyWork, readyAssessment, Survey);
				return;
			}
			if (speaksFirst != null && speaksFirstAssessment.Reason != null)
			{
				speaksFirst.RequirePart<r_KingdomImprovement>().AnnouncedReason = (int)speaksFirstAssessment.Verdict;
				MessageQueue.AddPlayerMessage("{{K|" + speaksFirstAssessment.Reason + "}}");
				System.Ledger.Note("{{K|" + speaksFirstAssessment.Reason + "}}");
			}
		}

		/// <summary>
		/// Tells the founder once per game that the settlement betters its own works, and where
		/// to stop it. Modal on purpose and exactly once: this is the only moment in the mod
		/// where the settlement will change something the founder placed the order for, and
		/// nobody should ever discover that by finding it already done.
		/// </summary>
		/// <param name="System">The kingdom, for its name.</param>
		public static void GiveFirstNotice(KingdomSystem System)
		{
			if (The.Game == null || The.Game.GetIntGameState(NoticedState) == 1)
			{
				return;
			}
			The.Game.SetIntGameState(NoticedState, 1);
			Popup.Show(KingdomUpgradeRules.FirstNoticeLine(System.SeatName));
		}

		/// <summary>
		/// Raises the scaffolding for one improvement, in the predecessor's own cell. The
		/// predecessor keeps standing and keeps working for the whole build; nothing it holds is
		/// touched until its replacement is actually on the ground.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the work stands in.</param>
		/// <param name="Work">The work being improved.</param>
		/// <param name="A">Its assessment, which must be <c>Ready</c>.</param>
		/// <param name="Survey">This pass's survey, which the cost is drawn through so its
		/// counters stay true for everything that runs after.</param>
		/// <returns>True once scaffolding is actually standing and the water is actually
		/// spent.</returns>
		public static bool Begin(KingdomSystem System, Zone Z, GameObject Work, Assessment A, KingdomSurvey Survey)
		{
			if (!A.Valid || !KingdomUpgradeRules.IsReady(A.Verdict) || A.Successor == null)
			{
				return false;
			}
			Cell cell = Work?.CurrentCell;
			if (cell == null)
			{
				return false;
			}
			// Raised before it is paid for, the same way a commission is: if the scaffold cannot
			// be created the settlement must not already have spent the water on nothing.
			GameObject scaffold = GameObject.Create("r_KingdomScaffold");
			if (scaffold == null)
			{
				return false;
			}
			int paid = Survey.Consume(A.CostDrams);
			if (paid < A.CostDrams)
			{
				// Never trust the affordability check over the actual delta. Whatever came out
				// goes straight back and nothing happens this pass.
				Survey.Store(paid);
				scaffold.Obliterate();
				KingdomLog.Log("improvement aborted: wanted " + A.CostDrams + " drams, drew " + paid);
				return false;
			}
			// The same all-or-nothing rule for material. PayUpgrade says once, in the ledger, why a
			// work that is ready to be bettered is standing still (STANDARDS 7b).
			if (!KingdomMaterials.PayUpgrade(System, cell.ParentZone, A.SuccessorKey))
			{
				Survey.Store(paid);
				scaffold.Obliterate();
				KingdomLog.Log("improvement aborted: the stockpiles could not cover " + A.SuccessorKey);
				return false;
			}
			scaffold.SetStringProperty(BuildKeyProperty, A.SuccessorKey);
			r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
			if (part != null)
			{
				part.TargetBlueprint = A.Successor.Blueprint;
				part.TargetDisplayName = A.Successor.Name;
				part.CompleteTick = The.Game.TimeTicks + A.BuildTicks;
				part.StaffNeeded = A.Successor.Staff;
				part.ThresholdManning = KingdomRules.IsThresholdManning(A.Successor.Manning);
				if (A.Successor.Defence > 0)
				{
					bool hasTinkering = The.Player != null && The.Player.HasSkill("Tinkering");
					bool hasAdvancedTinkering = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
					scaffold.SetIntProperty("KingdomDefencePending", KingdomRules.WallDefence(A.Successor.Defence, System.FoundingTerrainBlueprint, System.FoundingRegionName, hasTinkering, hasAdvancedTinkering));
				}
			}
			cell.AddObject(scaffold);
			r_KingdomImprovement improvement = Work.RequirePart<r_KingdomImprovement>();
			improvement.SuccessorKey = A.SuccessorKey;
			improvement.SuccessorBlueprint = A.Successor.Blueprint;
			improvement.Working = true;
			improvement.Scaffold = scaffold;
			improvement.WorkCompleteTick = The.Game.TimeTicks + A.BuildTicks;
			improvement.AnnouncedReason = 0;
			string standing = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string line = KingdomUpgradeRules.BegunLine(standing, A.Successor.Name, A.CostDrams);
			MessageQueue.AddPlayerMessage("{{G|" + line + "}}");
			System.Ledger.Note("{{G|" + line + "}}");
			KingdomChronicle.Record(System, "the " + standing + " at " + System.KingdomDisplayName + " was set to be raised into " + KingdomUpgradeRules.Article(A.Successor.Name));
			KingdomLog.Log("improvement begun: " + A.Key + " -> " + A.SuccessorKey + " cost=" + A.CostDrams + " ticks=" + A.BuildTicks + " at " + cell.X + "," + cell.Y);
			return true;
		}

		/// <summary>
		/// Begins an improvement the settlement offered and would not start on its own: a working
		/// building the city leans on (<c>HeldOffer</c>). The dip must already have been disclosed
		/// to the founder and consented to &mdash; <see cref="OpenHeldOffer"/> is the only caller,
		/// and it shows <c>KingdomUpgradeRules.DipLine</c> before it asks.
		/// <para>
		/// The verdict is copied to <c>Ready</c> before <see cref="Begin"/> is called, because the
		/// founder's word is exactly what makes it ready: every other condition the law checks has
		/// already passed, and the offer is the ONE verdict a founder may overrule. Nothing else
		/// is relaxed &mdash; <see cref="Begin"/> still refuses if the water or the material is
		/// not actually there when it reaches for it.
		/// </para>
		/// </summary>
		/// <returns>True once scaffolding is standing and the water is spent.</returns>
		public static bool Force(KingdomSystem System, Zone Z, GameObject Work, Assessment A, KingdomSurvey Survey)
		{
			if (!A.Valid || !KingdomUpgradeRules.IsOffer(A.Verdict) || A.Successor == null)
			{
				return false;
			}
			Assessment consented = A;
			consented.Verdict = KingdomUpgradeRules.UpgradeVerdict.Ready;
			if (!Begin(System, Z, Work, consented, Survey))
			{
				return false;
			}
			string standing = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string forced = KingdomUpgradeRules.ForcedLine(standing, A.Successor.Name, A.Margin);
			MessageQueue.AddPlayerMessage("{{W|" + forced + "}}");
			System.Ledger.Note("{{W|" + forced + "}}");
			KingdomChronicle.Record(System, "the " + standing + " at " + System.KingdomDisplayName + " was set to be raised on the founder's word, and the settlement went into its reserve to do it");
			KingdomLog.Log("improvement forced: " + A.Key + " -> " + A.SuccessorKey + " outage=" + A.OutputLost + " margin=" + A.Margin);
			return true;
		}

		/// <summary>
		/// Puts one held offer to the founder with the dip disclosed BEFORE consent, and forces it
		/// only if they say so. Answers whether the work was started, so the caller knows the
		/// listing behind it is stale.
		/// </summary>
		public static bool OpenHeldOffer(KingdomSystem System, Zone Z, GameObject Work, Assessment A, KingdomSurvey Survey)
		{
			if (!KingdomUpgradeRules.IsOffer(A.Verdict))
			{
				return false;
			}
			string standing = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string successor = (A.Successor != null) ? A.Successor.Name : DisplayNameOf(A.SuccessorKey);
			int picked = Popup.PickOption(
				Title: standing,
				Intro: KingdomUpgradeRules.DipLine(standing, successor, A.SupportPerDay, A.BuildTicks, A.Margin),
				Options: new string[2] { "Raise it anyway, and go into the reserve", "Leave it as it is for now" },
				AllowEscape: true);
			if (picked != 0)
			{
				return false;
			}
			return Force(System, Z, Work, A, Survey);
		}

		/// <summary>
		/// Moves everything from the old work into the new one and takes the old work down.
		/// <para>
		/// Carries the contents first &mdash; liquid by its actual mixture, then every held
		/// object &mdash; and only then the settlement's own marks. A dedication is the founder's
		/// decision about a thing, and losing one because the thing improved would be the worst
		/// bug this system could have, so <c>KingdomLarder</c> and <c>KingdomStores</c> are
		/// carried explicitly rather than left to the scaffold's own blueprint-keyed guess.
		/// </para>
		/// <para>
		/// Anything that still will not fit is poured or dropped in the cell rather than
		/// destroyed. That path should be unreachable &mdash;
		/// <see cref="ContentsWouldFit(GameObject, string)"/> refuses the improvement before it
		/// starts &mdash; but water nobody can see is water this mod has quietly invented a
		/// second place to keep.
		/// </para>
		/// </summary>
		/// <param name="Predecessor">The old work. Destroyed once emptied.</param>
		/// <param name="Successor">The new work, already standing.</param>
		/// <param name="SuccessorKey">Registry key to stamp on the new work.</param>
		public static void HandOver(GameObject Predecessor, GameObject Successor, string SuccessorKey)
		{
			if (Predecessor == null || Successor == null)
			{
				return;
			}
			Cell cell = Predecessor.CurrentCell ?? Successor.CurrentCell;
			int carriedLiquid = CarryLiquid(Predecessor, Successor);
			int carriedItems = CarryInventory(Predecessor, Successor, cell);
			CarryMarks(Predecessor, Successor, SuccessorKey);
			// A plot grows in place: the successor keeps the ground the predecessor was staked on and
			// the next tier's footprint is stamped inside it, walls and all. Read while the
			// predecessor still stands, because everything the ground was recorded as rides on it. A
			// single-cell design carries nothing and this is a no-op.
			KingdomPlots.GrowInPlace(Predecessor, Successor, SuccessorKey);
			string predecessorName = KingdomDesign.ReferenceFor(Predecessor, Predecessor.ShortDisplayName);
			LiquidVolume remaining = Predecessor.GetPart<LiquidVolume>();
			if (remaining != null && remaining.Volume > 0 && cell != null)
			{
				remaining.EmptyIntoCell(cell);
			}
			Predecessor.Destroy(null, Silent: true);
			if (carriedLiquid > 0 || carriedItems > 0)
			{
				MessageQueue.AddPlayerMessage("{{G|Everything the " + predecessorName + " held was moved into " + KingdomDesign.ReferenceFor(Successor, Successor.ShortDisplayName) + ".}}");
			}
			KingdomLog.Log("improvement handover: " + predecessorName + " -> " + Successor.Blueprint + " liquid=" + carriedLiquid + " items=" + carriedItems);
			// A settlement that is standing there watching should be able to watch the next one
			// start, rather than having to walk out and back in. Bounded by the reserve and by
			// free hands exactly as the pass itself is, and it still only ever starts one - and it
			// is a no-op when the handover was itself driven by that pass, which is what keeps
			// "one work per visit" true.
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			Zone zone = Successor.CurrentZone;
			if (system != null && zone != null)
			{
				KingdomSystem.Guard("improvement follow-on", delegate
				{
					OnZoneActivated(system, zone, KingdomSurvey.Take(zone, system));
				});
			}
		}

		/// <summary>
		/// Pours the predecessor's liquid into the successor, mixture and all. Every component is
		/// moved at its real dram count &mdash; <c>ComponentLiquids</c> holds parts per thousand,
		/// not drams &mdash; and both ends are measured rather than assumed, because
		/// <c>AddDrams</c> clamps silently and <c>UseDrams</c> reports something else entirely.
		/// </summary>
		/// <returns>Drams actually moved.</returns>
		public static int CarryLiquid(GameObject Predecessor, GameObject Successor)
		{
			LiquidVolume source = Predecessor?.GetPart<LiquidVolume>();
			LiquidVolume target = Successor?.GetPart<LiquidVolume>();
			if (source == null || target == null || source.Volume <= 0)
			{
				return 0;
			}
			int total = source.Volume;
			int moved = 0;
			List<string> components = new List<string>(source.ComponentLiquids.Keys);
			int assigned = 0;
			for (int i = 0; i < components.Count; i++)
			{
				int drams = (i == components.Count - 1) ? (total - assigned) : (total * source.ComponentLiquids[components[i]] / 1000);
				assigned += drams;
				if (drams <= 0)
				{
					continue;
				}
				int added = KingdomLiquids.Fill(target, components[i], drams);
				if (added > 0)
				{
					moved += KingdomLiquids.Drain(source, added);
				}
			}
			return moved;
		}

		/// <summary>
		/// Moves every object out of the predecessor. Anything the successor will not take is put
		/// down in the cell rather than destroyed.
		/// </summary>
		/// <returns>Objects actually moved or set down.</returns>
		public static int CarryInventory(GameObject Predecessor, GameObject Successor, Cell Where)
		{
			if (Predecessor?.Inventory == null)
			{
				return 0;
			}
			// Snapshot first: removing from an Inventory mutates the list being walked.
			List<GameObject> held = new List<GameObject>(Predecessor.Inventory.Objects);
			int moved = 0;
			for (int i = 0; i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!Predecessor.Inventory.RemoveObjectFromInventory(item, null, Silent: true))
				{
					continue;
				}
				if (Successor?.Inventory != null)
				{
					Successor.Inventory.AddObject(item, null, Silent: true);
					moved++;
				}
				else if (Where != null)
				{
					Where.AddObject(item);
					moved++;
				}
			}
			return moved;
		}

		/// <summary>
		/// Carries the settlement's own marks onto the improved work. The scaffold has already
		/// set what the new DESIGN says it is &mdash; built, staffed, defended, and dedicated if
		/// it holds liquid. This carries what the FOUNDER decided about the old one, which the
		/// scaffold cannot know: a larder dedication (which the scaffold keys off one blueprint
		/// and would silently drop), a stores dedication, an adoption, and a machine's grid
		/// certification.
		/// </summary>
		public static void CarryMarks(GameObject Predecessor, GameObject Successor, string SuccessorKey)
		{
			if (Predecessor == null || Successor == null)
			{
				return;
			}
			Successor.SetIntProperty(BuiltProperty, 1);
			if (!string.IsNullOrEmpty(SuccessorKey))
			{
				Successor.SetStringProperty(BuildKeyProperty, SuccessorKey);
			}
			if (Predecessor.GetIntProperty(KingdomAdopt.LarderProperty) == 1 && Successor.Inventory != null)
			{
				Successor.SetIntProperty(KingdomAdopt.LarderProperty, 1);
			}
			if (Predecessor.GetIntProperty(KingdomAdopt.StoresProperty) == 1 && Successor.GetPart<LiquidVolume>() != null)
			{
				Successor.SetIntProperty(KingdomAdopt.StoresProperty, 1);
			}
			if (Predecessor.GetIntProperty(KingdomSalvage.CertifiedProperty) == 1)
			{
				Successor.SetIntProperty(KingdomSalvage.CertifiedProperty, 1);
			}
			// A name the founder gave is the most personal decision anything in this mod records.
			// Losing one because the thing it was given to got better would be the same bug as
			// losing a dedication, so it is carried the same way and for the same reason.
			string given = Predecessor.GetStringProperty(KingdomDesign.GivenNameProperty);
			if (!string.IsNullOrEmpty(given))
			{
				Successor.SetStringProperty(KingdomDesign.GivenNameProperty, given);
			}
			// An adopted work is never improved (UpgradeVerdict.NotOurWork), so this is
			// unreachable today. It is carried anyway because the cost of being wrong is a
			// founder's own building quietly losing the settlement's recognition of it.
			if (Predecessor.GetIntProperty(AdoptedProperty) == 1)
			{
				Successor.SetIntProperty(AdoptedProperty, 1);
				string adoptedKey = Predecessor.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
				if (!string.IsNullOrEmpty(adoptedKey))
				{
					Successor.SetStringProperty(KingdomAdopt.AdoptedKeyProperty, adoptedKey);
				}
				string adoptedMark = Predecessor.GetStringProperty(KingdomAdopt.AdoptedMarkProperty);
				if (!string.IsNullOrEmpty(adoptedMark))
				{
					Successor.SetStringProperty(KingdomAdopt.AdoptedMarkProperty, adoptedMark);
				}
			}
		}

		/// <summary>
		/// The Charter's improvements screen: everything on this ground that can grow, what it
		/// grows into, and &mdash; for anything that is not growing &mdash; why not, in one
		/// sentence each. Picking a work holds it or releases it; the last entry does the same
		/// for the whole ground.
		/// <para>
		/// Nothing here starts, cancels, or hurries a work. The founder's only decision in this
		/// screen is what to leave alone, which is the one decision the settlement cannot make
		/// for them.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		public static void ShowImprovements(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			if (!Enabled)
			{
				Popup.Show("The settlement is not bettering its own works. (That module is switched off in the options.)");
				return;
			}
			Zone zone = The.Player?.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Your works are looked over on the kingdom's own ground.");
				return;
			}
			while (true)
			{
				KingdomSurvey survey = KingdomSurvey.Take(zone, System);
				int freeHands = System.Population - System.AssignedCrew;
				if (freeHands < 0)
				{
					freeHands = 0;
				}
				List<GameObject> works = new List<GameObject>();
				List<Assessment> assessments = new List<Assessment>();
				List<string> lines = new List<string>();
				bool otherWorkUnderway = false;
				foreach (GameObject item in zone.GetObjects())
				{
					r_KingdomImprovement improvement = item.GetPart<r_KingdomImprovement>();
					if (improvement != null && improvement.Working)
					{
						otherWorkUnderway = true;
					}
				}
				foreach (GameObject item in zone.GetObjects())
				{
					if (item.GetIntProperty(BuiltProperty) != 1)
					{
						continue;
					}
					Assessment assessment = Assess(System, zone, item, survey, freeHands, otherWorkUnderway);
					if (!assessment.Valid || assessment.Verdict == KingdomUpgradeRules.UpgradeVerdict.NoSuccessor)
					{
						continue;
					}
					works.Add(item);
					assessments.Add(assessment);
					lines.Add(EntryLine(item, assessment));
				}
				bool groundHeld = IsGroundHeld(zone);
				lines.Add(groundHeld
					? "{{W|Let this ground improve itself again}}"
					: "{{K|Leave this ground as it is}}");
				if (works.Count == 0)
				{
					Popup.Show("Nothing standing here is built to grow into anything else yet.");
					return;
				}
				int picked = Popup.PickOption(Title: "The works of " + System.SeatName, Intro: "Pick a work to leave as it is, or to let grow again.", Options: lines, AllowEscape: true);
				if (picked < 0)
				{
					return;
				}
				if (picked >= works.Count)
				{
					SetGroundHeld(zone, !groundHeld);
					continue;
				}
				// A held offer is the one verdict the founder may overrule, so picking it asks
				// rather than toggling: the dip is disclosed and consented to before anything moves.
				// Everything else in this screen still only ever decides what to leave alone.
				Assessment picking = assessments[picked];
				if (KingdomUpgradeRules.IsOffer(picking.Verdict) && OpenHeldOffer(System, zone, works[picked], picking, survey))
				{
					continue;
				}
				r_KingdomImprovement held = works[picked].RequirePart<r_KingdomImprovement>();
				held.Held = !held.Held;
				held.AnnouncedReason = 0;
			}
		}

		/// <summary>One work's line in the Charter listing: what it is, what it would become, and
		/// its state or the reason it has none.</summary>
		public static string EntryLine(GameObject Work, Assessment A)
		{
			string name = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string successor = (A.Successor != null) ? A.Successor.Name : DisplayNameOf(A.SuccessorKey);
			switch (A.Verdict)
			{
			case KingdomUpgradeRules.UpgradeVerdict.AlreadyWorking:
				return "{{G|" + name + "}} - being raised into " + successor;
			case KingdomUpgradeRules.UpgradeVerdict.Ready:
				return "{{G|" + name + "}} - ready to be raised into " + successor + " for {{C|" + A.CostDrams + " drams}}";
			case KingdomUpgradeRules.UpgradeVerdict.HeldOffer:
				return "{{W|" + name + "}} - ready to improve, and held: the city leans on it. Pick it to raise it anyway.";
			case KingdomUpgradeRules.UpgradeVerdict.NotOurWork:
				return "{{K|" + name + "}} - yours, not the settlement's. It is left exactly as you made it.";
			case KingdomUpgradeRules.UpgradeVerdict.StyleForbids:
				return "{{K|" + name + "}} - " + successor + " is not built in a city of this kind";
			default:
				return "{{K|" + name + "}} - " + (A.Reason ?? ("would become " + successor));
			}
		}
	}
}
