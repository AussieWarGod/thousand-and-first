using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the part moves; the
// settlement-side resolver below stays where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// Ground the founder has ordered cleared: a rect, the effort still owed on it, and the day
	/// the crew last swung at it. Carries no <c>WantTurnTick</c> and never will &mdash; clearing
	/// is crew work, and crew work is resolved only from the settlement's ordinary
	/// <c>ZoneActivatedEvent</c> pass, through
	/// <see cref="ThousandAndFirst.KingdomMaterials.OnSettlementPass"/>, so a founder who is not
	/// there is never spending hands they never assigned.
	/// </summary>
	[Serializable]
	public class r_KingdomClearance : IPart
	{
		/// <summary>West edge of the ordered rect, in zone cells, inclusive.</summary>
		public int X1;

		/// <summary>North edge of the ordered rect, in zone cells, inclusive.</summary>
		public int Y1;

		/// <summary>East edge of the ordered rect, in zone cells, inclusive.</summary>
		public int X2;

		/// <summary>South edge of the ordered rect, in zone cells, inclusive.</summary>
		public int Y2;

		/// <summary>Effort still owed. The order is finished the pass this reaches zero.</summary>
		public int EffortLeft;

		/// <summary>Effort the order was assessed at, kept so the founder can be told how far
		/// along it is without the settlement having to re-walk the ground to find out.</summary>
		public int EffortTotal;

		/// <summary>Tick the crew last worked this ground. Zero until the first pass sees it.</summary>
		public long LastWorkedTick;

		/// <summary>Set once the founder has been told there is nobody free to clear. Cleared the
		/// moment hands are free again, so the reason is given once per stall and not once per
		/// visit. STANDARDS 7b.</summary>
		public bool NoHandsAnnounced;

		/// <summary>Set once the founder has been told something is standing in the finished
		/// rect that the settlement will not touch. Cleared when the obstruction goes.</summary>
		public bool BlockedAnnounced;
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The material economy: clearance as extraction, stockpiles as marks, building costs paid
	/// in water and material both, striking as the honest way to take something down again.
	/// <para>
	/// Follows the <see cref="KingdomLarder"/> idiom throughout &mdash; each founder-facing entry
	/// point does its own eligibility check, its own success messaging, and its own chronicle
	/// entry, and surfaces only a decline through <c>Failure</c>. A refusal changes nothing.
	/// </para>
	/// <para>
	/// Nothing here mints a material. Everything the stockpiles hold was carried out of ground
	/// somebody cleared, salvaged from something somebody struck, or delivered under a charter.
	/// And nothing here touches an object the settlement did not create: a rect holding anything
	/// else is refused whole, by name, rather than cleared around.
	/// </para>
	/// </summary>
	public static class KingdomMaterials
	{
		/// <summary>Property marking a container the founder dedicated as a stockpile. The mark
		/// is not a transfer: what is inside stays where it is and stays the founder's, and the
		/// settlement only counts it &mdash; exactly as <c>KingdomLarder</c> works for food.</summary>
		public const string StockpileProperty = "KingdomStockpile";

		/// <summary>
		/// Blueprint tag by which a third-party item declares itself one of the settlement's
		/// materials: <c>&lt;tag Name="r_KingdomMaterial" Value="timber" /&gt;</c>. The extension
		/// point for the material vocabulary, per STANDARDS 6 &mdash; another mod's ironwood beam
		/// counts as timber the moment it carries the tag, with no code here changing.
		/// </summary>
		public const string MaterialTag = "r_KingdomMaterial";

		/// <summary>
		/// Blueprint tag by which a third-party item declares itself one of the settlement's rare
		/// finds: <c>&lt;tag Name="r_KingdomExotic" Value="gem" /&gt;</c>. The same extension point
		/// <see cref="MaterialTag"/> is, for the same reason: another mod's uncut star-sapphire
		/// counts the moment it carries the tag, with no code here changing.
		/// </summary>
		public const string ExoticTag = "r_KingdomExotic";

		/// <summary>
		/// Blueprint tag by which an item declares what bits it is worth to the settlement, when
		/// vanilla's own <c>TinkerItem</c> does not say: <c>&lt;tag Name="r_KingdomBit" Value="34"
		/// /&gt;</c>, written in the same tiers a <c>Bits</c> cost is. Read SECOND, after the
		/// game's own answer, because deriving before authoring is the rule and vanilla already
		/// knows what a fractured microchip disassembles into.
		/// </summary>
		public const string BitTag = "r_KingdomBit";

		/// <summary>Effort still owed on a building the founder ordered struck. Absent or zero on
		/// every building nobody has condemned.</summary>
		public const string StrikeEffortProperty = "KingdomStrikeEffort";

		/// <summary>Effort a strike order was assessed at, for the founder's report.</summary>
		public const string StrikeTotalProperty = "KingdomStrikeTotal";

		/// <summary>Set once the founder has been told a strike order is waiting on hands.</summary>
		public const string StrikeAnnouncedProperty = "KingdomStrikeAnnounced";

		/// <summary>
		/// Tick the crew last worked a strike order, written as a string because the engine's
		/// object properties are ints and a tick is not. Kept on the condemned building itself
		/// rather than appended to any existing part &mdash; a serialized field added to a part
		/// that already ships would move every field after it and cost players their saves.
		/// </summary>
		public const string StrikeWorkedProperty = "KingdomStrikeWorked";

		/// <summary>Blueprint of the marker a clearance order stands as.</summary>
		public const string ClearanceStakeBlueprint = "r_KingdomClearanceStake";

		/// <summary>
		/// Item blueprints the settlement stores each material as, indexed by
		/// <see cref="KingdomMaterial"/>. Scrap is vanilla's own <c>Scrap Metal</c>, because scrap
		/// metal is already a real item in this game and a second one would be a lie; the rest are
		/// ours, because vanilla has no timber, no cut stone, no bundle of brush, and nothing at
		/// all for what comes off a saw-pit.
		/// </summary>
		public static readonly string[] MaterialBlueprints = new string[KingdomMaterialRules.MaterialCount]
		{
			"r_KingdomMud",
			"r_KingdomBrush",
			"r_KingdomTimber",
			"r_KingdomCutStone",
			"r_KingdomMarbleBlock",
			"Scrap Metal",
			"r_KingdomShapedTimber",
			"r_KingdomShapedStone",
			"r_KingdomWorkedMetal"
		};

		/// <summary>
		/// Item blueprints one exotic may be held as, indexed by <see cref="KingdomExotic"/>.
		/// Every one of them is vanilla's own: the settlement never makes a rare find, and a
		/// blueprint of ours standing in for one would be a lie about where it came from. The
		/// gemstone row lists the rough gems the game scatters; anything else a mod wants counted
		/// says so with <see cref="ExoticTag"/>.
		/// </summary>
		public static readonly string[][] ExoticBlueprints = new string[KingdomMaterialRules.ExoticCount][]
		{
			new string[1] { "Bronze Ingot" },
			new string[1] { "Silver Nugget" },
			new string[1] { "Gold Nugget" },
			new string[8] { "Gemstone", "Rough Agate", "Rough Topaz", "Rough Jasper", "Rough Amethyst", "Rough Sapphire", "Rough Emerald", "Rough Peridot" }
		};

		/// <summary>Stockpiles one settlement's keepers can account for on one ground. Mirrors
		/// <c>KingdomRules.MaxDedicatedLarders</c>: a separate cap from water and from food,
		/// because these are separate accounts kept by separate people.</summary>
		public const int MaxStockpiles = 8;

		/// <summary>Whether the material economy resolves at all. Rides the growth toggle rather
		/// than adding a switch of its own: materials are what growth costs.</summary>
		public static bool Enabled => KingdomGrowth.Enabled;

		// --- The registry: material costs, kept beside the catalogue ------------------------

		private static readonly Dictionary<string, KingdomMaterialTally> _costs = new Dictionary<string, KingdomMaterialTally>();

		private static readonly Dictionary<string, KingdomMaterialTally> _upgradeCosts = new Dictionary<string, KingdomMaterialTally>();

		private static readonly Dictionary<string, KingdomMaterialTally> _dealMaterials = new Dictionary<string, KingdomMaterialTally>();

		private static readonly KingdomMaterialTally _empty = new KingdomMaterialTally();

		private static readonly Dictionary<string, KingdomBitTally> _bitCosts = new Dictionary<string, KingdomBitTally>();

		private static readonly Dictionary<string, KingdomExoticTally> _exoticCosts = new Dictionary<string, KingdomExoticTally>();

		private static readonly Dictionary<string, KingdomYard> _refineries = new Dictionary<string, KingdomYard>();

		private static readonly KingdomBitTally _emptyBits = new KingdomBitTally();

		private static readonly KingdomExoticTally _emptyExotics = new KingdomExoticTally();

		/// <summary>
		/// Empties every material cost keyed by a registry key. Called from
		/// <c>KingdomData.EnsureLoaded</c> beside <c>KingdomZoning.ClearGates</c> and
		/// <c>KingdomUpgrade.ClearChains</c>, in the same single pass over the streams, because a
		/// second pass would make the engine warn about every attribute the first pass did not
		/// happen to want.
		/// </summary>
		public static void ClearCosts()
		{
			_costs.Clear();
			_upgradeCosts.Clear();
			_dealMaterials.Clear();
			_bitCosts.Clear();
			_exoticCosts.Clear();
			_refineries.Clear();
		}

		/// <summary>
		/// Records what one catalogue entry costs in material, and what improving into it costs.
		/// Both attributes are optional and both are read whether or not they are present, for
		/// the reason above. A malformed value disables itself with a logged reason and leaves
		/// the design costing water alone; it never crashes the registry and never half-registers.
		/// </summary>
		/// <param name="Key">The design's registry key. Null and empty are ignored.</param>
		/// <param name="Materials">The <c>Materials</c> attribute, or null for water-only.</param>
		/// <param name="UpgradeMaterials">The <c>UpgradeMaterials</c> attribute, or null.</param>
		public static void RegisterCost(string Key, string Materials, string UpgradeMaterials)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomMaterialRules.TryParseMaterialCost(Materials, out var cost, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " has a bad Materials: " + error);
			}
			else if (!cost.IsEmpty())
			{
				_costs[Key] = cost;
			}
			if (!KingdomMaterialRules.TryParseMaterialCost(UpgradeMaterials, out var upgrade, out var upgradeError))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " has a bad UpgradeMaterials: " + upgradeError);
			}
			else if (!upgrade.IsEmpty())
			{
				_upgradeCosts[Key] = upgrade;
			}
		}

		/// <summary>
		/// Records what one catalogue entry costs in the high-craft stock: vanilla's own tinkering
		/// bits, and the rare finds only a great work asks for. Registered beside the material cost
		/// and read out of the same merged draft, so a later file that re-prices a design in bits
		/// layers exactly the way one that re-prices it in timber does.
		/// <para>
		/// Both attributes are optional and both are read whether or not they are present, for the
		/// reason <see cref="RegisterCost"/> gives. A malformed value disables itself with a logged
		/// reason and leaves the design costing what it already cost; it never half-registers.
		/// </para>
		/// </summary>
		/// <param name="Key">The design's registry key. Null and empty are ignored.</param>
		/// <param name="Bits">The <c>Bits</c> attribute, or null for a design that wants none.
		/// </param>
		/// <param name="Exotics">The <c>Exotics</c> attribute, or null.</param>
		public static void RegisterHighCraft(string Key, string Bits, string Exotics)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomMaterialRules.TryParseBitCost(Bits, out var bits, out var bitError))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " has a bad Bits: " + bitError);
			}
			else if (!bits.IsEmpty())
			{
				_bitCosts[Key] = bits;
			}
			if (!KingdomMaterialRules.TryParseExoticCost(Exotics, out var exotics, out var exoticError))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " has a bad Exotics: " + exoticError);
			}
			else if (!exotics.IsEmpty())
			{
				_exoticCosts[Key] = exotics;
			}
		}

		/// <summary>
		/// Records that one catalogue entry is a processing work: a design that turns raw stock
		/// into the refined material named by its <c>Refines</c> attribute. Optional everywhere;
		/// a design that declares nothing is not a yard and never was.
		/// <para>
		/// This is the whole of what makes a yard a yard. A third party's own sawmill is a
		/// sawyer's yard the moment it writes <c>Refines="shapedtimber"</c>, and the infrastructure
		/// gate counts it exactly like ours, because the gate asks the registry what stands and
		/// never asks for a blueprint by name.
		/// </para>
		/// </summary>
		public static void RegisterRefinery(string Key, string Refines)
		{
			if (string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(Refines) || Refines.Trim().Length == 0)
			{
				return;
			}
			if (!KingdomMaterialRules.TryParseYard(Refines, out var yard))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " refines \"" + Refines
					+ "\", which is neither a yard (" + string.Join(", ", KingdomMaterialRules.YardKeys) + ") nor a refined material");
				return;
			}
			_refineries[Key] = yard;
		}

		/// <summary>What a design costs in bits. Never null; empty for everything that wants
		/// none, which is nearly the whole catalogue.</summary>
		public static KingdomBitTally BitCostFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _bitCosts.TryGetValue(Key, out var cost))
			{
				return cost;
			}
			return _emptyBits;
		}

		/// <summary>What a design costs in rare finds. Never null; empty for everything but the
		/// great works.</summary>
		public static KingdomExoticTally ExoticCostFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _exoticCosts.TryGetValue(Key, out var cost))
			{
				return cost;
			}
			return _emptyExotics;
		}

		/// <summary>Which yard a design is, if it is one at all.</summary>
		public static bool TryRefineryOf(string Key, out KingdomYard Yard)
		{
			KingdomData.EnsureBuildings();
			Yard = KingdomYard.Sawyer;
			return !string.IsNullOrEmpty(Key) && _refineries.TryGetValue(Key, out Yard);
		}

		/// <summary>
		/// Records what one charter carries in material per caravan. Optional; absent means the
		/// charter carries water alone, which is every charter written before this existed.
		/// </summary>
		public static void RegisterDealMaterials(string Key, string Materials)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomMaterialRules.TryParseMaterialCost(Materials, out var carried, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomDeals: deal " + Key + " has a bad Materials: " + error);
			}
			else if (!carried.IsEmpty())
			{
				_dealMaterials[Key] = carried;
			}
		}

		/// <summary>What a design costs in material. Never null; empty for a water-only design,
		/// which is the default and the whole of the compatibility guarantee.</summary>
		public static KingdomMaterialTally CostFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _costs.TryGetValue(Key, out var cost))
			{
				return cost;
			}
			return _empty;
		}

		/// <summary>What improving a work into the design named by Key costs in material. Never
		/// null.</summary>
		public static KingdomMaterialTally UpgradeCostFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _upgradeCosts.TryGetValue(Key, out var cost))
			{
				return cost;
			}
			return _empty;
		}

		/// <summary>What one caravan under the charter named by Key carries in material. Never
		/// null.</summary>
		public static KingdomMaterialTally DealMaterialsFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _dealMaterials.TryGetValue(Key, out var carried))
			{
				return carried;
			}
			return _empty;
		}

		// --- The stockpiles ------------------------------------------------------------------

		/// <summary>True for a container the founder has dedicated as a stockpile.</summary>
		public static bool IsStockpile(GameObject Object)
		{
			return Object != null && Object.GetIntProperty(StockpileProperty) == 1;
		}

		/// <summary>Stockpiles currently dedicated on this ground, for the dedication cap.</summary>
		public static int CountStockpiles(Zone Z)
		{
			int total = 0;
			if (Z == null)
			{
				return 0;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (IsStockpile(item))
				{
					total++;
				}
			}
			return total;
		}

		/// <summary>
		/// Which material an item counts as, by the tag a third-party blueprint may carry, then
		/// by our own blueprints, then by vanilla's own scrap tag. Anything else is not a
		/// material and is never counted, spent, or destroyed.
		/// </summary>
		/// <param name="Object">The item to read. Null is not a material.</param>
		/// <param name="Material">Set on success.</param>
		public static bool TryMaterialOf(GameObject Object, out KingdomMaterial Material)
		{
			Material = KingdomMaterial.Mud;
			if (Object == null)
			{
				return false;
			}
			string tagged = Object.GetTag(MaterialTag);
			if (!string.IsNullOrEmpty(tagged) && KingdomMaterialRules.TryParseMaterial(tagged, out Material))
			{
				return true;
			}
			for (int i = 0; i < MaterialBlueprints.Length; i++)
			{
				if (Object.Blueprint == MaterialBlueprints[i])
				{
					Material = (KingdomMaterial)i;
					return true;
				}
			}
			// Vanilla's own scrap, whatever variant of it: "Scrap Metal" is the piece the
			// settlement stores, but a founder who dedicates a chest of salvaged bits has
			// dedicated scrap, and the keepers can tell the difference between that and a tool.
			if (Object.HasTag("SemanticScrap"))
			{
				Material = KingdomMaterial.Scrap;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Which rare find an item counts as. Read the way <see cref="TryMaterialOf"/> reads a
		/// material: a third party's own tag first, then the vanilla blueprints the base game
		/// scatters. Anything else is somebody's jewellery and is never counted or spent.
		/// </summary>
		public static bool TryExoticOf(GameObject Object, out KingdomExotic Exotic)
		{
			Exotic = KingdomExotic.Ingot;
			if (Object == null)
			{
				return false;
			}
			string tagged = Object.GetTag(ExoticTag);
			if (!string.IsNullOrEmpty(tagged) && KingdomMaterialRules.TryParseExotic(tagged, out Exotic))
			{
				return true;
			}
			for (int i = 0; i < ExoticBlueprints.Length; i++)
			{
				string[] blueprints = ExoticBlueprints[i];
				for (int j = 0; j < blueprints.Length; j++)
				{
					if (Object.Blueprint == blueprints[j])
					{
						Exotic = (KingdomExotic)i;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// What one item is worth to the settlement in bits, added into a tally.
		/// <para>
		/// Derived before authored, per Addendum 4's creed principle applied to things rather than
		/// people: vanilla's own <c>TinkerItem</c> already knows what every piece of scrap in the
		/// game disassembles into, and <c>BitType</c> already knows which tier each of those bits
		/// belongs to. A corroded circuit board is two tier-zero bits because the base game says so
		/// and not because we wrote a table. Our own <see cref="BitTag"/> is read only for items
		/// that carry no <c>TinkerItem</c> at all &mdash; a mod's raw ingot of pure alloy, say.
		/// </para>
		/// </summary>
		/// <param name="Object">The item to read.</param>
		/// <param name="Into">Tally to add to. Null is a no-op.</param>
		/// <returns>True when the item was worth any bits at all.</returns>
		public static bool TryBitsOf(GameObject Object, KingdomBitTally Into)
		{
			if (Object == null || Into == null)
			{
				return false;
			}
			KingdomBitTally unit = UnitBits(Object);
			if (unit.IsEmpty())
			{
				return false;
			}
			int count = (Object.Count > 0) ? Object.Count : 1;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				Into.Add(i, unit.Get(i) * count);
			}
			return true;
		}

		/// <summary>
		/// What ONE of a thing is worth in bits, ignoring how many of it are stacked there. The
		/// unit the spending path works in: a stack of four bent metal sheets is four separate
		/// answers to a price, and only as many of them are broken up as the price actually wants.
		/// </summary>
		/// <returns>An empty tally for anything that is worth no bits, which is most things.
		/// </returns>
		public static KingdomBitTally UnitBits(GameObject Object)
		{
			KingdomBitTally worth = new KingdomBitTally();
			if (Object == null)
			{
				return worth;
			}
			TinkerItem tinker = Object.GetPart<TinkerItem>();
			if (tinker != null && tinker.CanDisassemble)
			{
				// GetBitCostFor rather than the instance property on purpose. The property answers
				// out of BitCostMap and returns a bare "0" for a blueprint nothing has primed yet
				// (TinkerItem.cs:56-62), while the static fills the map from the blueprint and
				// hands back real bit colours (TinkerItem.cs:133-157). It also leaves the item's
				// own modifications out of the count, which is right here: the settlement is
				// reading a heap of scrap, not pricing somebody's modded rifle.
				string bits = TinkerItem.GetBitCostFor(tinker.ActiveBlueprint);
				if (!string.IsNullOrEmpty(bits))
				{
					for (int i = 0; i < bits.Length; i++)
					{
						if (KingdomMaterialRules.TryBitTier(bits[i], out var tier))
						{
							worth.Add(tier, 1);
						}
					}
				}
				if (!worth.IsEmpty())
				{
					return worth;
				}
			}
			string tagged = Object.GetTag(BitTag);
			if (!string.IsNullOrEmpty(tagged) && KingdomMaterialRules.TryParseBitCost(tagged, out var declared, out _))
			{
				return declared;
			}
			return worth;
		}

		/// <summary>
		/// How dear a thing is to break up, for the order the keepers reach along a shelf: the sum
		/// of its bits weighted by tier, so a corroded circuit board sorts below a metamorphic
		/// core. Zero for anything worth no bits, which sorts first and is skipped anyway.
		/// </summary>
		private static int BitWorth(GameObject Object)
		{
			KingdomBitTally worth = UnitBits(Object);
			int total = 0;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				total += worth.Get(i) * (i + 1);
			}
			return total;
		}

		/// <summary>
		/// What the dedicated stockpiles on one ground hold, and the means to spend it and fill
		/// it. A snapshot: spending and delivering through this object keep its tally correct,
		/// but an item placed after it was taken does not retroactively appear in it &mdash; the
		/// same contract <c>KingdomSurvey</c> holds for water and food.
		/// </summary>
		public sealed class MaterialStock
		{
			/// <summary>Units of each material the dedicated stockpiles hold.</summary>
			public readonly KingdomMaterialTally Tally = new KingdomMaterialTally();

			/// <summary>Bits the dedicated stockpiles are worth, by tier. Not a separate store and
			/// never was: bits are whatever tinkering stock the founder put in with everything
			/// else, counted the way the workshop would count it.</summary>
			public readonly KingdomBitTally Bits = new KingdomBitTally();

			/// <summary>Rare finds the dedicated stockpiles hold.</summary>
			public readonly KingdomExoticTally Exotics = new KingdomExoticTally();

			/// <summary>The dedicated containers, in the order found. Spending walks these, so
			/// nothing is ever drawn from something the founder did not dedicate.</summary>
			public readonly List<GameObject> Stockpiles = new List<GameObject>();

			/// <summary>Zone this stock was taken from. Null for an empty stock.</summary>
			public Zone Zone;

			/// <summary>True when the founder has dedicated no stockpile here at all, which is a
			/// different thing from having dedicated an empty one.</summary>
			public bool None => Stockpiles.Count == 0;

			/// <summary>
			/// Destroys up to Units of one material from the stockpiles, in the order found.
			/// Counts what actually left rather than what was asked for: an item whose
			/// destruction something else vetoes stops the draw instead of being counted as
			/// spent.
			/// </summary>
			/// <returns>Units actually taken, never more than were there.</returns>
			public int Take(KingdomMaterial Material, int Units)
			{
				int remaining = Units;
				for (int i = 0; i < Stockpiles.Count && remaining > 0; i++)
				{
					GameObject container = Stockpiles[i];
					if (container.Inventory == null)
					{
						continue;
					}
					// Snapshot first: destroying an item below removes it from this same
					// Inventory list, and mutating a collection mid-foreach throws.
					List<GameObject> held = new List<GameObject>(container.Inventory.Objects);
					for (int j = 0; j < held.Count && remaining > 0; j++)
					{
						GameObject item = held[j];
						if (!TryMaterialOf(item, out var kind) || kind != Material)
						{
							continue;
						}
						// Destroy() on a stack of more than one decrements it by exactly one and
						// leaves the object in place (Stacker.HandleEvent(BeforeDestroyObjectEvent));
						// only the last unit removes it. The count is read before and after rather
						// than inferred from the call, so a refused destruction never reads as a
						// unit spent.
						while (remaining > 0 && GameObject.Validate(item))
						{
							int before = item.Count;
							item.Destroy(null, Silent: true);
							if (GameObject.Validate(item) && item.Count >= before)
							{
								break;
							}
							remaining--;
						}
					}
				}
				int taken = Units - remaining;
				Tally.Add(Material, -taken);
				return taken;
			}

			/// <summary>
			/// Spends a whole cost, or spends nothing. Checked against the tally first, so a
			/// settlement is never left holding half a house's worth of missing timber.
			/// </summary>
			/// <returns>False when the stockpiles do not cover the cost; nothing was taken.</returns>
			public bool Spend(KingdomMaterialTally Cost)
			{
				if (Cost == null || Cost.IsEmpty())
				{
					return true;
				}
				if (!KingdomMaterialRules.Covers(Tally, Cost))
				{
					return false;
				}
				for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				{
					KingdomMaterial material = (KingdomMaterial)i;
					int wanted = Cost.Get(material);
					if (wanted > 0)
					{
						Take(material, wanted);
					}
				}
				return true;
			}

			/// <summary>
			/// Spends a whole bit cost by taking apart the stock that carries it, or spends
			/// nothing. Bits are not held loose &mdash; the settlement holds SCRAP, and the
			/// keepers break up whatever answers the price, exactly as a tinker would.
			/// <para>
			/// A piece broken up for one bit gives up whatever else was in it, and that surplus is
			/// gone. That is honest and it is the reason a design is priced in cheap tiers wherever
			/// it can be: nobody breaks an AI master unit for the tier-zero bit in it if there is a
			/// bent metal sheet on the shelf, and this walks the shelf cheapest-first so it does not
			/// either.
			/// </para>
			/// </summary>
			/// <returns>False when the stockpiles do not cover the cost; nothing was taken.</returns>
			public bool SpendBits(KingdomBitTally Cost)
			{
				if (Cost == null || Cost.IsEmpty())
				{
					return true;
				}
				if (!KingdomMaterialRules.CoversBits(Bits, Cost))
				{
					return false;
				}
				KingdomBitTally owed = Cost.Copy();
				for (int i = 0; i < Stockpiles.Count && owed.Total() > 0; i++)
				{
					GameObject container = Stockpiles[i];
					if (container.Inventory == null)
					{
						continue;
					}
					// Snapshot first: destroying an item below removes it from this same Inventory
					// list, and mutating a collection mid-foreach throws. Sorted cheapest-first so
					// the keepers reach for the bent metal sheet before the AI master unit.
					List<GameObject> held = new List<GameObject>(container.Inventory.Objects);
					held.Sort(delegate(GameObject A, GameObject B)
					{
						return BitWorth(A).CompareTo(BitWorth(B));
					});
					for (int j = 0; j < held.Count && owed.Total() > 0; j++)
					{
						GameObject item = held[j];
						// Same exclusion the counting pass makes, and it has to be the same or the
						// settlement would break up the walls' own scrap for a bit it never counted.
						if (TryMaterialOf(item, out _) || TryExoticOf(item, out _))
						{
							continue;
						}
						KingdomBitTally worth = UnitBits(item);
						if (worth.IsEmpty())
						{
							continue;
						}
						while (owed.Total() > 0 && Wanted(owed, worth) && GameObject.Validate(item))
						{
							int before = item.Count;
							item.Destroy(null, Silent: true);
							if (GameObject.Validate(item) && item.Count >= before)
							{
								break;
							}
							for (int tier = 0; tier < KingdomMaterialRules.BitTierCount; tier++)
							{
								int taken = worth.Get(tier);
								if (taken > 0)
								{
									owed.Add(tier, -taken);
									Bits.Add(tier, -taken);
								}
							}
						}
					}
				}
				return owed.Total() == 0;
			}

			/// <summary>
			/// Spends a whole cost in rare finds, or spends nothing. A gemstone is a gemstone: the
			/// keepers take the first one that answers, because nothing here is worth more to a
			/// wall than any other of its kind.
			/// </summary>
			/// <returns>False when the stockpiles do not cover the cost; nothing was taken.</returns>
			public bool SpendExotics(KingdomExoticTally Cost)
			{
				if (Cost == null || Cost.IsEmpty())
				{
					return true;
				}
				if (!KingdomMaterialRules.CoversExotics(Exotics, Cost))
				{
					return false;
				}
				for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
				{
					KingdomExotic exotic = (KingdomExotic)i;
					int remaining = Cost.Get(exotic);
					for (int j = 0; j < Stockpiles.Count && remaining > 0; j++)
					{
						GameObject container = Stockpiles[j];
						if (container.Inventory == null)
						{
							continue;
						}
						List<GameObject> held = new List<GameObject>(container.Inventory.Objects);
						for (int k = 0; k < held.Count && remaining > 0; k++)
						{
							GameObject item = held[k];
							if (!TryExoticOf(item, out var kind) || kind != exotic)
							{
								continue;
							}
							while (remaining > 0 && GameObject.Validate(item))
							{
								int before = item.Count;
								item.Destroy(null, Silent: true);
								if (GameObject.Validate(item) && item.Count >= before)
								{
									break;
								}
								remaining--;
								Exotics.Add(exotic, -1);
							}
						}
					}
					if (remaining > 0)
					{
						return false;
					}
				}
				return true;
			}

			/// <summary>Whether breaking up a thing worth <paramref name="Worth"/> would answer any
			/// part of what is still owed.</summary>
			private static bool Wanted(KingdomBitTally Owed, KingdomBitTally Worth)
			{
				for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
				{
					if (Owed.Get(i) > 0 && Worth.Get(i) > 0)
					{
						return true;
					}
				}
				return false;
			}

			/// <summary>
			/// Puts real items into the stockpiles, and onto the ground when there is nowhere
			/// else for them. Material is never held in the abstract: what the settlement earned
			/// exists somewhere a founder can walk to and pick up.
			/// </summary>
			/// <param name="Material">Which material.</param>
			/// <param name="Units">How many units. Zero and negative do nothing.</param>
			/// <param name="Fallback">Cell the overflow is dropped in when no stockpile can take
			/// it. Null discards the overflow rather than losing track of it, and is only ever
			/// passed by a caller with no ground to drop on.</param>
			/// <returns>Units that went on the ground instead of into a stockpile.</returns>
			public int Put(KingdomMaterial Material, int Units, Cell Fallback)
			{
				if (Units <= 0)
				{
					return 0;
				}
				string blueprint = BlueprintFor(Material);
				if (string.IsNullOrEmpty(blueprint))
				{
					return 0;
				}
				GameObject container = null;
				for (int i = 0; i < Stockpiles.Count; i++)
				{
					if (Stockpiles[i].Inventory != null)
					{
						container = Stockpiles[i];
						break;
					}
				}
				int placed = 0;
				int spilled = 0;
				int remaining = Units;
				while (remaining > 0)
				{
					GameObject item = GameObject.Create(blueprint);
					if (item == null)
					{
						break;
					}
					int batch = 1;
					if (item.HasPart("Stacker") && remaining > 1)
					{
						batch = remaining;
						item.Count = batch;
					}
					if (container != null)
					{
						container.Inventory.AddObject(item);
						placed += batch;
					}
					else if (Fallback != null)
					{
						Fallback.AddObject(item);
						spilled += batch;
					}
					else
					{
						item.Obliterate();
					}
					remaining -= batch;
				}
				Tally.Add(Material, placed + spilled);
				return spilled;
			}

			/// <summary>Puts a whole tally away, reporting how much of it ended up on the
			/// ground.</summary>
			public int PutAll(KingdomMaterialTally Yield, Cell Fallback)
			{
				int spilled = 0;
				if (Yield == null)
				{
					return 0;
				}
				for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				{
					KingdomMaterial material = (KingdomMaterial)i;
					spilled += Put(material, Yield.Get(material), Fallback);
				}
				return spilled;
			}
		}

		/// <summary>Walks a zone once and reads every dedicated stockpile in it.</summary>
		/// <param name="Z">Zone to read. Null yields an empty stock.</param>
		public static MaterialStock Stock(Zone Z)
		{
			MaterialStock stock = new MaterialStock();
			if (Z == null)
			{
				return stock;
			}
			stock.Zone = Z;
			foreach (GameObject item in Z.GetObjects())
			{
				if (!IsStockpile(item) || item.Inventory == null)
				{
					continue;
				}
				stock.Stockpiles.Add(item);
				foreach (GameObject held in item.Inventory.Objects)
				{
					// The material vocabulary claims a thing first and exclusively. Vanilla's own
					// Scrap Metal is how this settlement STORES scrap, and it is also a tinkering
					// bit; counted as both, a wall's worth of it could be spent twice - once on the
					// wall and once on a machine - which is minting. So the shelf's scrap answers
					// for the walls, and the settlement's bits come from the other things a founder
					// donates: fried processing cores, cracked robotics housings, and whatever else
					// came home from a ruin.
					if (TryMaterialOf(held, out var material))
					{
						stock.Tally.Add(material, held.Count);
						continue;
					}
					if (TryExoticOf(held, out var exotic))
					{
						stock.Exotics.Add(exotic, held.Count);
						continue;
					}
					TryBitsOf(held, stock.Bits);
				}
			}
			return stock;
		}

		/// <summary>The item blueprint a material is stored as, or null for a value outside the
		/// enum.</summary>
		public static string BlueprintFor(KingdomMaterial Material)
		{
			int index = (int)Material;
			if (index < 0 || index >= MaterialBlueprints.Length)
			{
				return null;
			}
			return MaterialBlueprints[index];
		}

		/// <summary>
		/// Dedicates a container to the settlement's stockpiles, or releases one already
		/// dedicated. Dedication is a mark and never a transfer &mdash; what is inside stays
		/// where it is and stays the founder's, and releasing it un-counts it without moving a
		/// thing.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the container stands in; must be the kingdom's own ground.</param>
		/// <param name="Container">The container to mark. Must hold things rather than liquid.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// marked when it does.</param>
		/// <returns>True once the container's standing has actually changed.</returns>
		public static bool DedicateStockpile(KingdomSystem System, Zone Z, GameObject Container, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A stockpile is kept on the kingdom's own ground, not in other people's yards.";
				return false;
			}
			if (Container == null || Container.Inventory == null)
			{
				Failure = "A stockpile has to be something that holds things.";
				return false;
			}
			if (IsStockpile(Container))
			{
				Container.SetIntProperty(StockpileProperty, 0);
				MessageQueue.AddPlayerMessage("The " + Container.ShortDisplayName + " is yours alone again. Nothing in it will be counted.");
				return true;
			}
			if (CountStockpiles(Z) >= MaxStockpiles)
			{
				Failure = "The settlement keeps as many stockpiles as anyone can keep an honest account of.";
				return false;
			}
			Container.SetIntProperty(StockpileProperty, 1);
			MessageQueue.AddPlayerMessage("The " + Container.ShortDisplayName + " is a stockpile of " + System.SeatName + " now. What is in it is counted, and still yours.");
			KingdomLog.Log("materials: stockpile dedicated at " + System.SeatName);
			return true;
		}

		/// <summary>One line for the status report: what the stockpiles hold, or why they hold
		/// nothing. Never null.</summary>
		public static string StockLine(Zone Z)
		{
			MaterialStock stock = Stock(Z);
			if (stock.None)
			{
				return "Nothing here is dedicated as a stockpile. Materials cleared from the ground will lie where they fall.";
			}
			string held = stock.Tally.Describe();
			string bits = stock.Bits.Describe();
			string exotics = stock.Exotics.Describe();
			if (held == null && bits == null && exotics == null)
			{
				return "The stockpiles stand empty.";
			}
			// The rare finds and the tinkering stock are counted separately because they are spent
			// separately: neither one is ever drawn on for a wall, and a founder reading one line
			// for all three would not know which of them the next work is short of.
			return ((held == null) ? "The stockpiles hold nothing the walls are made of" : ("The stockpiles hold {{C|" + held + "}}"))
				+ ((bits == null) ? "" : (", and stock enough for bits: {{C|" + bits + "}}"))
				+ ((exotics == null) ? "" : (", and {{C|" + exotics + "}} laid aside"))
				+ ".";
		}

		// --- Paying for a building in material ------------------------------------------------

		/// <summary>
		/// Whether the dedicated stockpiles on this ground cover a design's material cost. A
		/// design with no material cost is always affordable, which is every design the catalogue
		/// carried before materials existed.
		/// </summary>
		/// <param name="Z">Ground the commission would be issued on.</param>
		/// <param name="Key">The design's registry key.</param>
		/// <param name="Failure">A founder-facing reason when this returns false, naming the
		/// shortfall rather than restating the whole price.</param>
		public static bool CanPay(Zone Z, string Key, out string Failure)
		{
			Failure = null;
			// The yards first, and before a single unit is counted. A founder standing at a design
			// the settlement has no mason for should be told THAT, not handed a shopping list they
			// could fill and still be refused (STANDARDS 7b).
			if (!AllowsInfrastructure(Z, Key, out Failure))
			{
				return false;
			}
			KingdomMaterialTally cost = CostFor(Key);
			MaterialStock stock = null;
			if (!cost.IsEmpty())
			{
				stock = Stock(Z);
				if (!KingdomMaterialRules.Covers(stock.Tally, cost))
				{
					string missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
					Failure = "The work wants {{C|" + cost.Describe() + "}}, and the stockpiles are short "
						+ ((missing == null) ? "of it" : ("{{C|" + missing + "}}"))
						+ ". Clear ground for it, trade for it, or strike something that was built of it."
						+ (stock.None ? " Nothing here is dedicated as a stockpile yet." : "");
					return false;
				}
			}
			KingdomBitTally bits = BitCostFor(Key);
			KingdomExoticTally exotics = ExoticCostFor(Key);
			if (bits.IsEmpty() && exotics.IsEmpty())
			{
				return true;
			}
			if (stock == null)
			{
				stock = Stock(Z);
			}
			if (!KingdomMaterialRules.CoversBits(stock.Bits, bits))
			{
				string missing = KingdomMaterialRules.MissingBits(stock.Bits, bits).Describe();
				Failure = "This is high-craft work. It wants {{C|" + bits.Describe()
					+ "}} out of the stockpiles, and the keepers are short " + ((missing == null) ? "of it" : ("{{C|" + missing + "}}"))
					+ ". Bring scrap home and put it in a stockpile; whatever comes apart into the right stock will do.";
				return false;
			}
			if (!KingdomMaterialRules.CoversExotics(stock.Exotics, exotics))
			{
				string missing = KingdomMaterialRules.MissingExotics(stock.Exotics, exotics).Describe();
				Failure = "A work like this is finished in something rarer than stone. It wants {{C|" + exotics.Describe()
					+ "}}, and the stockpiles hold no " + ((missing == null) ? "such thing" : ("{{C|" + missing + "}}"))
					+ ". Nobody here can make one. Somebody has to find one and carry it home.";
				return false;
			}
			return true;
		}

		/// <summary>
		/// Whether the settlement's own infrastructure will carry a design of this size, and why
		/// not when it will not. The engine-coupled half of Addendum 7's gate: this reads what
		/// stands on the ground and hands the verdict to <c>KingdomMaterialRules.AllowsBuild</c>,
		/// which owns the law and the wording.
		/// </summary>
		/// <param name="Z">Ground the commission would be issued on.</param>
		/// <param name="Key">The design's registry key.</param>
		/// <param name="Failure">A founder-facing reason when this returns false, naming the yard.
		/// </param>
		public static bool AllowsInfrastructure(Zone Z, string Key, out string Failure)
		{
			Failure = null;
			if (!KingdomPlots.TryGetSpec(Key, out var spec) || spec == null || !KingdomMaterialRules.RequiresYard(spec.Size))
			{
				return true;
			}
			KingdomMaterialTally cost = CostFor(Key);
			if (KingdomMaterialRules.YardsFor(spec.Size, cost).Count == 0)
			{
				return true;
			}
			string name = KingdomData.TryGetBuilding(Key, out var entry) ? entry.Name : null;
			return KingdomMaterialRules.AllowsBuild(spec.Size, cost, YardsStanding(Z), name, out Failure);
		}

		/// <summary>
		/// What every yard on this ground is doing: standing, staffed this pass, and headed by a
		/// notable. One walk of the zone, and a yard is whatever a design declared itself to be
		/// with <c>Refines</c> &mdash; a third party's sawmill counts exactly like ours.
		/// </summary>
		public static List<KingdomMaterialRules.KingdomYardStanding> YardsStanding(Zone Z)
		{
			List<KingdomMaterialRules.KingdomYardStanding> yards = new List<KingdomMaterialRules.KingdomYardStanding>();
			if (Z == null)
			{
				return yards;
			}
			bool[] standing = new bool[KingdomMaterialRules.YardCount];
			bool[] staffed = new bool[KingdomMaterialRules.YardCount];
			bool[] headed = new bool[KingdomMaterialRules.YardCount];
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") != 1)
				{
					continue;
				}
				if (!TryRefineryOf(item.GetStringProperty(KingdomUpgrade.BuildKeyProperty), out var yard))
				{
					continue;
				}
				int index = (int)yard;
				standing[index] = true;
				// KingdomStaffed is set by the staffing pass earlier in this same visit
				// (KingdomGrowth.AssignWork), so this reads the crew that is actually in the yard
				// today rather than the crew the design asked for.
				staffed[index] |= item.GetIntProperty("KingdomStaffed") == 1;
				headed[index] |= IsHeaded(item);
			}
			for (int i = 0; i < KingdomMaterialRules.YardCount; i++)
			{
				if (standing[i])
				{
					yards.Add(new KingdomMaterialRules.KingdomYardStanding((KingdomYard)i, standing[i], staffed[i], headed[i]));
				}
			}
			return yards;
		}

		/// <summary>
		/// The office layer's answer to "does a named notable head this work?", installed by
		/// whoever owns the office seats (Addendum 6). Left null here on purpose: this file must
		/// not decide who holds an office, and a mod that ships without the office layer must not
		/// find its grand works refused by a question nobody is answering.
		/// <para>
		/// Null therefore reads as "not enforced" rather than "not headed", the same compatibility
		/// rule an absent attribute follows everywhere else in this economy. Once the probe is
		/// installed, an unheaded yard refuses a grand work by name.
		/// </para>
		/// </summary>
		public static System.Func<GameObject, bool> HeadedProbe;

		/// <summary>Whether a work is headed, or true when no office layer has installed a probe.
		/// </summary>
		public static bool IsHeaded(GameObject Work)
		{
			return HeadedProbe == null || (Work != null && HeadedProbe(Work));
		}

		/// <summary>
		/// Spends a design's material cost from this ground's stockpiles, and its bits and rare
		/// finds with it. Spends the whole cost or none of it.
		/// </summary>
		/// <returns>False when the stockpiles did not cover it; nothing was taken.</returns>
		public static bool Pay(Zone Z, string Key)
		{
			KingdomMaterialTally cost = CostFor(Key);
			KingdomBitTally bits = BitCostFor(Key);
			KingdomExoticTally exotics = ExoticCostFor(Key);
			if (cost.IsEmpty() && bits.IsEmpty() && exotics.IsEmpty())
			{
				return true;
			}
			MaterialStock stock = Stock(Z);
			// Every price is checked before any of them is taken, so a design short of one gemstone
			// never costs the settlement the forty stone it was going to be built of.
			if (!KingdomMaterialRules.Covers(stock.Tally, cost)
				|| !KingdomMaterialRules.CoversBits(stock.Bits, bits)
				|| !KingdomMaterialRules.CoversExotics(stock.Exotics, exotics))
			{
				return false;
			}
			if (!stock.Spend(cost))
			{
				return false;
			}
			stock.SpendBits(bits);
			stock.SpendExotics(exotics);
			KingdomLog.Log("materials: spent " + (cost.Describe() ?? "nothing")
				+ ((bits.IsEmpty()) ? "" : (" and bits " + bits.Describe()))
				+ ((exotics.IsEmpty()) ? "" : (" and " + exotics.Describe()))
				+ " on " + Key);
			return true;
		}

		/// <summary>
		/// Whether the dedicated stockpiles on this ground cover an IMPROVEMENT's material cost,
		/// without spending any of it. The absorption law asks this before a work is judged ready,
		/// so an improvement short of material is refused by name rather than begun and abandoned
		/// at the moment <see cref="PayUpgrade"/> would have taken the cost.
		/// </summary>
		/// <param name="Z">Ground the work stands on.</param>
		/// <param name="SuccessorKey">Registry key of the design it would become.</param>
		/// <param name="Missing">What the stockpiles are short of, or null when they cover it.
		/// </param>
		public static bool CanPayUpgrade(Zone Z, string SuccessorKey, out string Missing)
		{
			Missing = null;
			KingdomMaterialTally cost = UpgradeCostFor(SuccessorKey);
			if (cost.IsEmpty())
			{
				return true;
			}
			MaterialStock stock = Stock(Z);
			if (KingdomMaterialRules.Covers(stock.Tally, cost))
			{
				return true;
			}
			Missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
			return false;
		}

		/// <summary>
		/// Spends an improvement's material cost, and says once why an improvement is waiting
		/// when it cannot. The automatic improvement path has no founder standing at it, so the
		/// reason goes in the ledger where the homecoming report will read it out. STANDARDS 7b.
		/// </summary>
		/// <returns>False when the stockpiles did not cover it; nothing was taken.</returns>
		public static bool PayUpgrade(KingdomSystem System, Zone Z, string SuccessorKey)
		{
			KingdomMaterialTally cost = UpgradeCostFor(SuccessorKey);
			if (cost.IsEmpty())
			{
				return true;
			}
			MaterialStock stock = Stock(Z);
			if (stock.Spend(cost))
			{
				return true;
			}
			if (System != null)
			{
				string missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
				System.Ledger.Note("{{r|A work is ready to be bettered, and the stockpiles are short " + ((missing == null) ? "of what it wants" : missing) + ".}}");
			}
			return false;
		}

		/// <summary>
		/// Puts a charter's carried material into this ground's stockpiles, dropping whatever
		/// will not fit at the founder's feet rather than keeping it on the cart. Mirrors what
		/// <c>KingdomTrade</c> already does with water it cannot store.
		/// </summary>
		/// <returns>Units that ended up on the ground rather than in a stockpile.</returns>
		public static int Deliver(KingdomSystem System, Zone Z, KingdomMaterialTally Carried)
		{
			if (Carried == null || Carried.IsEmpty() || Z == null)
			{
				return 0;
			}
			MaterialStock stock = Stock(Z);
			Cell founderCell = The.Player?.CurrentCell;
			Cell fallback = (founderCell != null && founderCell.ParentZone == Z) ? founderCell : Z.GetCell(Z.Width / 2, Z.Height / 2);
			int spilled = stock.PutAll(Carried, fallback);
			if (spilled > 0 && System != null)
			{
				System.Ledger.Note("{{r|" + spilled + " loads came under charter with no stockpile to go in, and were set down on the ground.}}");
			}
			return spilled;
		}

		// --- Clearance ------------------------------------------------------------------------

		/// <summary>What a rect would cost to clear and what it would yield, or why it will not
		/// be cleared at all.</summary>
		public struct ClearanceAssessment
		{
			/// <summary>False only when there was nothing to assess: no zone, no kingdom, or a
			/// rect outside the ground. Every other field is meaningless when this is false.</summary>
			public bool Valid;

			/// <summary>A founder-facing reason the rect will not be cleared, or null when it
			/// will. Names the object standing in the way, because "no" without a name is the one
			/// answer a building system is not allowed to give.</summary>
			public string Refusal;

			/// <summary>Cells in the rect.</summary>
			public int Cells;

			/// <summary>Cells holding something that has to come down.</summary>
			public int Standing;

			/// <summary>Total effort the clearing costs.</summary>
			public int Effort;

			/// <summary>What the clearing would put in the stockpiles.</summary>
			public KingdomMaterialTally Yield;
		}

		/// <summary>
		/// Reads a rect without changing anything: what stands in it, what clearing it costs, and
		/// what it yields. Safe to call for a confirmation popup.
		/// </summary>
		/// <param name="System">The kingdom; must be founded and must hold this ground.</param>
		/// <param name="Z">The ground.</param>
		/// <param name="X1">West edge, inclusive.</param>
		/// <param name="Y1">North edge, inclusive.</param>
		/// <param name="X2">East edge, inclusive.</param>
		/// <param name="Y2">South edge, inclusive.</param>
		public static ClearanceAssessment Assess(KingdomSystem System, Zone Z, int X1, int Y1, int X2, int Y2)
		{
			ClearanceAssessment assessment = default;
			assessment.Yield = new KingdomMaterialTally();
			if (System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return assessment;
			}
			if (X1 > X2 || Y1 > Y2 || X1 < 0 || Y1 < 0 || X2 >= Z.Width || Y2 >= Z.Height)
			{
				return assessment;
			}
			assessment.Valid = true;
			for (int y = Y1; y <= Y2; y++)
			{
				for (int x = X1; x <= X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						assessment.Valid = false;
						return assessment;
					}
					assessment.Cells++;
					KingdomStanding standing = KingdomStanding.Nothing;
					int hitpoints = 0;
					foreach (GameObject item in cell.GetObjects())
					{
						if (IsProtected(item, out var reason))
						{
							assessment.Refusal = reason;
							return assessment;
						}
						if (!TryClassify(item, out var kind))
						{
							continue;
						}
						if (kind > standing)
						{
							standing = kind;
							hitpoints = BaseHitpoints(item);
						}
					}
					assessment.Effort += KingdomMaterialRules.ClearanceEffort(standing, hitpoints);
					if (standing != KingdomStanding.Nothing)
					{
						assessment.Standing++;
						assessment.Yield.Add(KingdomMaterialRules.YieldMaterial(standing), KingdomMaterialRules.YieldUnits(standing));
					}
				}
			}
			assessment.Yield.Add(KingdomMaterial.Mud, KingdomMaterialRules.GroundMud(assessment.Cells));
			return assessment;
		}

		/// <summary>
		/// Stakes a clearance order over a rect. The order does nothing on its own: crew works it
		/// down on the settlement's ordinary passes, and only ever with hands the water detail and
		/// the works have not already claimed.
		/// </summary>
		/// <param name="System">The kingdom; must be founded and must hold this ground.</param>
		/// <param name="Z">The ground.</param>
		/// <param name="X1">West edge, inclusive.</param>
		/// <param name="Y1">North edge, inclusive.</param>
		/// <param name="X2">East edge, inclusive.</param>
		/// <param name="Y2">South edge, inclusive.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// staked and nothing is spent when it does.</param>
		/// <returns>True once the stake is standing.</returns>
		public static bool StakeClearance(KingdomSystem System, Zone Z, int X1, int Y1, int X2, int Y2, out string Failure)
		{
			Failure = null;
			if (!Enabled)
			{
				Failure = "The settlement is not growing.";
				return false;
			}
			ClearanceAssessment assessment = Assess(System, Z, X1, Y1, X2, Y2);
			if (!assessment.Valid)
			{
				Failure = "That ground is not the settlement's to clear.";
				return false;
			}
			if (assessment.Refusal != null)
			{
				Failure = assessment.Refusal;
				return false;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				r_KingdomClearance existing = item.GetPart<r_KingdomClearance>();
				if (existing != null && Overlaps(existing, X1, Y1, X2, Y2))
				{
					Failure = "That ground is already ordered cleared.";
					return false;
				}
			}
			Cell cell = StakeCell(Z, X1, Y1, X2, Y2);
			if (cell == null)
			{
				Failure = "There is nowhere to drive the stake.";
				return false;
			}
			GameObject stake = GameObject.Create(ClearanceStakeBlueprint);
			if (stake == null)
			{
				Failure = "The stake could not be driven.";
				return false;
			}
			r_KingdomClearance order = stake.GetPart<r_KingdomClearance>();
			if (order == null)
			{
				stake.Obliterate();
				Failure = "The stake could not be driven.";
				return false;
			}
			order.X1 = X1;
			order.Y1 = Y1;
			order.X2 = X2;
			order.Y2 = Y2;
			order.EffortTotal = assessment.Effort;
			order.EffortLeft = assessment.Effort;
			order.LastWorkedTick = The.Game.TimeTicks;
			stake.DisplayName = "ground ordered cleared";
			cell.AddObject(stake);
			stake.MakeActive();
			string yield = assessment.Yield.Describe();
			KingdomChronicle.Record(System, System.KingdomDisplayName + " set its people to clearing " + assessment.Cells + " paces of ground");
			int days = KingdomMaterialRules.DaysForOneHand(assessment.Effort);
			MessageQueue.AddPlayerMessage("{{G|The ground is staked for clearing.}} " + assessment.Cells + " paces, "
				+ days + ((days == 1) ? " day" : " days") + " of work for a single pair of hands"
				+ ((yield == null) ? "" : (", and " + yield + " out of it")) + ".");
			KingdomLog.Log("materials: clearance staked " + X1 + "," + Y1 + " to " + X2 + "," + Y2 + " effort=" + assessment.Effort);
			return true;
		}

		/// <summary>
		/// Where the stake goes: the first open, passable cell inside the ordered rect, so the
		/// marker stands in the ground it names rather than on top of the tree it condemns.
		/// Falls back to the rect's north-west corner, which always exists once the rect has been
		/// bounds-checked.
		/// </summary>
		private static Cell StakeCell(Zone Z, int X1, int Y1, int X2, int Y2)
		{
			for (int y = Y1; y <= Y2; y++)
			{
				for (int x = X1; x <= X2; x++)
				{
					Cell candidate = Z.GetCell(x, y);
					if (candidate != null && candidate.IsEmpty() && candidate.IsPassable())
					{
						return candidate;
					}
				}
			}
			return Z.GetCell(X1, Y1);
		}

		// --- Striking -------------------------------------------------------------------------

		/// <summary>
		/// Orders a building the settlement raised taken down, or calls off an order already
		/// standing. Founder-ordered and nothing else: no system anywhere condemns a building on
		/// its own. Nothing comes down the moment this is called &mdash; crew works the order off
		/// over days, and the ceremony is at the end of it.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the building stands in; must be the kingdom's own ground.</param>
		/// <param name="Building">The building to strike. Must be one the settlement built.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// marked when it does.</param>
		/// <returns>True once the order stands, or once it has been called off.</returns>
		public static bool OrderStrike(KingdomSystem System, Zone Z, GameObject Building, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is struck on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Building == null || !GameObject.Validate(Building) || Building.CurrentZone == null || Building.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing there to strike.";
				return false;
			}
			if (Building.GetIntProperty("KingdomBuilt") != 1)
			{
				Failure = "The settlement strikes what it raised. That is not one of its buildings.";
				return false;
			}
			if (Building.GetIntProperty(StrikeEffortProperty) > 0)
			{
				Building.SetIntProperty(StrikeEffortProperty, 0);
				Building.SetIntProperty(StrikeTotalProperty, 0);
				Building.SetIntProperty(StrikeAnnouncedProperty, 0);
				Building.SetStringProperty(StrikeWorkedProperty, null);
				MessageQueue.AddPlayerMessage("{{K|The order to strike the " + Building.ShortDisplayName + " is called off.}} It stands exactly where it stood.");
				return true;
			}
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			KingdomMaterialTally cost = CostFor(key);
			int drams = (KingdomData.TryGetBuilding(key, out var entry) ? entry.CostDrams : 0);
			int effort = KingdomMaterialRules.StrikeEffort(cost.Total(), drams);
			Building.SetIntProperty(StrikeEffortProperty, effort);
			Building.SetIntProperty(StrikeTotalProperty, effort);
			Building.SetIntProperty(StrikeAnnouncedProperty, 0);
			WriteTick(Building, StrikeWorkedProperty, The.Game.TimeTicks);
			string salvage = KingdomMaterialRules.StrikeSalvage(cost).Describe();
			KingdomChronicle.Record(System, "the " + Building.ShortDisplayName + " of " + System.KingdomDisplayName + " was condemned, and the crew set to taking it down");
			int days = KingdomMaterialRules.DaysForOneHand(effort);
			MessageQueue.AddPlayerMessage("{{W|The " + Building.ShortDisplayName + " is condemned.}} The crew will take it down over "
				+ days + ((days == 1) ? " day" : " days") + " of work for a single pair of hands"
				+ ((salvage == null) ? ", and there is nothing in it worth keeping" : (", and " + salvage + " comes back")) + ". No water is refunded.");
			KingdomLog.Log("materials: strike ordered on " + Building.ShortDisplayName + " effort=" + effort);
			return true;
		}

		// --- The pass -------------------------------------------------------------------------

		/// <summary>
		/// Works the settlement's one clearing gang for the days since it last swung. Called from
		/// <c>KingdomGrowth.OnZoneActivated</c> after every water-spending step, because clearing
		/// spends no water at all &mdash; it spends hands, and only the hands the water detail and
		/// the works have left over.
		/// <para>
		/// One gang, one job: strike orders first, because ground a building still stands on
		/// cannot be cleared, then clearance stakes in the order the ground yields them. A second
		/// job waits for the next pass rather than being worked by hands the first one already
		/// spent.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom. Does nothing when unfounded.</param>
		/// <param name="Z">The activated ground. Does nothing when it is not the kingdom's.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			int hands = KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew);
			GameObject strike = null;
			GameObject stakeObject = null;
			r_KingdomClearance stake = null;
			List<GameObject> yards = new List<GameObject>();
			List<int> strength = new List<int>();
			List<int> intelligence = new List<int>();
			foreach (GameObject item in Z.GetObjects())
			{
				if (strike == null && item.GetIntProperty(StrikeEffortProperty) > 0)
				{
					strike = item;
				}
				if (stake == null)
				{
					r_KingdomClearance order = item.GetPart<r_KingdomClearance>();
					if (order != null)
					{
						stake = order;
						stakeObject = item;
					}
				}
				if (item.GetIntProperty("KingdomBuilt") == 1 && TryRefineryOf(item.GetStringProperty(KingdomUpgrade.BuildKeyProperty), out _))
				{
					yards.Add(item);
				}
				else if (item.GetIntProperty("KingdomBorn") == 1 && !item.IsPlayer())
				{
					// Who the settlement's people actually are. Read, never assigned: the founder
					// does not pick who stands in the yard, and a city of strong backs dresses
					// stone faster than a city of scribes whether anybody planned it that way.
					strength.Add(StatOf(item, "Strength"));
					intelligence.Add(StatOf(item, "Intelligence"));
				}
			}
			// The yards first and unconditionally: they are staffed works, and the staffing pass
			// spent their crews before this ran. Refining takes no hand the clearing gang was ever
			// going to have, so it neither waits on a strike order nor competes with one.
			//
			// The keepers' method is realm-wide (RESEARCH-SYSTEM-DESIGN 8.2 -- the keepers write to
			// each other), so it is read ONCE for the whole ground rather than per bench: every
			// yard in this zone works to the same method, and asking the roster once a yard would
			// walk the tree once a yard for one answer.
			int method = KingdomResearch.MethodPercent(System);
			for (int i = 0; i < yards.Count; i++)
			{
				WorkYard(System, Z, yards[i], KingdomMaterialRules.AverageStat(strength), KingdomMaterialRules.AverageStat(intelligence), method, timeTicks);
			}
			if (strike != null)
			{
				WorkStrike(System, Z, strike, hands, timeTicks);
				return;
			}
			if (stake != null)
			{
				WorkClearance(System, Z, stakeObject, stake, hands, timeTicks);
			}
		}

		/// <summary>Tick a yard last turned raw stock into refined, written as a string for the
		/// reason <see cref="StrikeWorkedProperty"/> is: the engine's object properties are ints
		/// and a tick is not, and a serialized field added to a shipped part would move every field
		/// after it and cost players their saves.</summary>
		public const string RefineWorkedProperty = "KingdomRefineWorked";

		/// <summary>Set once a yard has been chronicled for its first run, so the settlement
		/// remembers the day the saws started and never says it twice.</summary>
		public const string RefineOpenedProperty = "KingdomRefineOpened";

		/// <summary>Set once the founder has been told a yard has nothing to work. Cleared the
		/// moment there is stock again, so the reason is given once per stall (STANDARDS 7b).
		/// </summary>
		public const string RefineIdleProperty = "KingdomRefineIdleSaid";

		/// <summary>Set once the founder has been told a yard has nobody standing at it. Cleared
		/// the moment a crew is drawn for it, so an unstaffed yard names itself once per stall
		/// and not once per pass (STANDARDS 7b).</summary>
		public const string RefineUnstaffedProperty = "KingdomRefineUnstaffedSaid";

		/// <summary>
		/// Works one processing yard for the days since it last ran: raw stock out of the
		/// stockpiles, refined material back into them, and the first run of a yard written into
		/// the chronicle.
		/// <para>
		/// Nothing here reads a calendar for anything but "how many days of labour is this crew
		/// owed". A yard nobody staffs makes nothing however long the founder is away, a yard with
		/// no stock makes nothing, both of them say which it was, and neither wears out, because
		/// time is labour and never decay.
		/// </para>
		/// <para>
		/// <b>Idle days are spent, not banked.</b> The day budget advances whether or not anyone
		/// stood at the bench, so an empty yard does not accumulate a debt of labour that a later
		/// crew discharges in one burst. That was already what the code did; what it did not do
		/// was admit it. The gate is now read before the budget is spent, so the two decisions
		/// are made in the order they are explained, and the unstaffed case names itself once
		/// (STANDARDS 7b) instead of leaning on the settlement-wide idle-works tally, which
		/// reports a COUNT and never says which bench it was.
		/// </para>
		/// </summary>
		/// <param name="MethodPercent">What this realm's keepers have worked out, as a percent to
		/// multiply the bench's output by (<c>KingdomResearch.MethodPercent</c>). A hundred is a
		/// realm that has researched nothing, and a hundred changes nothing.</param>
		private static void WorkYard(KingdomSystem System, Zone Z, GameObject Yard, int Strength, int Intelligence, int MethodPercent, long TimeTicks)
		{
			if (!TryRefineryOf(Yard.GetStringProperty(KingdomUpgrade.BuildKeyProperty), out var kind))
			{
				return;
			}
			long worked = ReadTick(Yard, RefineWorkedProperty);
			if (worked <= 0)
			{
				WriteTick(Yard, RefineWorkedProperty, TimeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(TimeTicks - worked);
			if (days <= 0)
			{
				return;
			}
			// The gates are read BEFORE the day budget is spent, so the two decisions are made in
			// the order they are explained -- and the budget is spent either way, because idle
			// days are gone rather than banked: an empty bench does not owe its labour to whoever
			// staffs it next. KingdomMaterialRules.AssessYard holds the ordering ("nobody is
			// here" outranks "there is nothing to work"), so the reason the founder is given is
			// the one they can act on.
			int crew = Yard.GetIntProperty("KingdomStaffNeeded") * Yard.GetIntProperty("KingdomEffectiveness") / 100;
			bool staffed = Yard.GetIntProperty("KingdomStaffed") == 1 && crew > 0;
			MaterialStock stock = null;
			KingdomMaterial raw = default(KingdomMaterial);
			int refinable = 0;
			if (staffed)
			{
				// Reading the whole zone's stockpiles is not free, and there is nobody here to
				// spend them, so an unstaffed yard does not pay for the look.
				stock = Stock(Z);
				refinable = KingdomMaterialRules.RefinableFrom(kind, stock.Tally, out raw);
			}
			KingdomMaterialRules.YardStall stall = KingdomMaterialRules.AssessYard(staffed, crew, refinable);
			WriteTick(Yard, RefineWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
			if (stall != KingdomMaterialRules.YardStall.Working)
			{
				bool unstaffed = stall == KingdomMaterialRules.YardStall.Unstaffed;
				string said = unstaffed ? RefineUnstaffedProperty : RefineIdleProperty;
				// Only one stall at a time is true, so the other reason is unsaid: a yard that was
				// short of stock and is now short of hands gets the new sentence, not silence.
				Yard.SetIntProperty(unstaffed ? RefineIdleProperty : RefineUnstaffedProperty, 0);
				if (Yard.GetIntProperty(said) != 1)
				{
					Yard.SetIntProperty(said, 1);
					System.Ledger.Note("{{r|" + KingdomMaterialRules.YardStallLine(stall, kind, System.SeatName) + "}}");
				}
				return;
			}
			Yard.SetIntProperty(RefineUnstaffedProperty, 0);
			Yard.SetIntProperty(RefineIdleProperty, 0);
			int capability = KingdomMaterialRules.CrewCapability(kind, Strength, Intelligence);
			// RESEARCH-SYSTEM-DESIGN 8.2, the third factor: crew, then condition, then METHOD.
			// It rides the capability percent into the effort because that is the one percent this
			// bench's arithmetic already carries, and it is NOT folded into crew -- crew is what
			// makes an unstaffed yard make nothing, and zero times anything is still zero, so no
			// amount of knowledge can staff a bench nobody is standing at (Addendum 8 clause 2).
			// The raw capability is what the founder is TOLD about below: the word describes who
			// is holding the tool, and the keepers' method is not a thing about that crew.
			int methoded = KingdomProductionRules.Methoded(capability, MethodPercent);
			int made = KingdomMaterialRules.RefinedThisPass(crew, days, methoded, refinable);
			if (made <= 0)
			{
				return;
			}
			KingdomMaterial refined = KingdomMaterialRules.MadeAt(kind);
			// Take first and count what actually came off the shelf, rather than trusting the
			// reading: something may have emptied it since the survey, and a load that does not
			// make a whole unit goes straight back where it was instead of vanishing.
			int taken = stock.Take(raw, KingdomMaterialRules.RawSpentFor(made));
			made = taken / KingdomMaterialRules.RawPerRefined;
			int returned = taken - KingdomMaterialRules.RawSpentFor(made);
			if (returned > 0)
			{
				stock.Put(raw, returned, Yard.CurrentCell);
			}
			if (made <= 0)
			{
				return;
			}
			int before = stock.Tally.Get(refined);
			int spilled = stock.Put(refined, made, Yard.CurrentCell);
			if (stock.Tally.Get(refined) <= before)
			{
				// Loud rather than quiet: the raw stock is already gone, so an item blueprint that
				// does not exist has eaten it. This is a wiring fault in the mod's own files and
				// nobody's fault in the game, and it must not read as a yard having a slow day.
				MetricsManager.LogError("ThousandAndFirst KingdomMaterials: the " + KingdomMaterialRules.YardName(kind)
					+ " made " + made + " " + KingdomMaterialRules.MaterialName(refined) + " and nothing could be created for it; is "
					+ (BlueprintFor(refined) ?? "its blueprint") + " declared?");
				return;
			}
			string madeLine = made + " " + KingdomMaterialRules.MaterialName(refined);
			if (Yard.GetIntProperty(RefineOpenedProperty) != 1)
			{
				Yard.SetIntProperty(RefineOpenedProperty, 1);
				KingdomChronicle.Record(System, "the " + KingdomMaterialRules.YardName(kind) + " of " + System.KingdomDisplayName
					+ " ran for the first time, and " + madeLine + " came off it");
				System.RecordDeed("the first work off the " + KingdomMaterialRules.YardName(kind) + " at " + System.KingdomDisplayName);
				MessageQueue.AddPlayerMessage("{{G|The " + KingdomMaterialRules.YardName(kind) + " is working.}} The first "
					+ madeLine + " is stacked where anyone walking past can see it, and the crew is "
					+ KingdomMaterialRules.CapabilityWord(capability) + " at the work.");
			}
			if (spilled > 0)
			{
				System.Ledger.Note("{{r|" + spilled + " off the " + KingdomMaterialRules.YardName(kind)
					+ " was set down on the ground for want of a stockpile to hold it.}}");
			}
			KingdomLog.Log("materials: " + KingdomMaterialRules.YardKey(kind) + " made=" + made + " from=" + KingdomMaterialRules.MaterialKey(raw)
				+ " crew=" + crew + " days=" + days + " capability=" + capability + " method=" + MethodPercent + " spilled=" + spilled);
		}

		/// <summary>One settler's stat, or <c>KingdomMaterialRules.BaselineStat</c> when the engine
		/// has none to give. Nobody is punished for being unreadable.</summary>
		private static int StatOf(GameObject Citizen, string Stat)
		{
			Statistic statistic = (Citizen == null) ? null : Citizen.GetStat(Stat);
			return (statistic == null) ? KingdomMaterialRules.BaselineStat : statistic.Value;
		}

		private static void WorkStrike(KingdomSystem System, Zone Z, GameObject Building, int Hands, long TimeTicks)
		{
			// The order keeps its own checkpoint, for the same reason the water detail had to
			// grow one: charged per zone activation instead, a founder could work a building down
			// by stepping out of the zone and back in, without a day passing.
			long worked = ReadTick(Building, StrikeWorkedProperty);
			if (worked <= 0)
			{
				WriteTick(Building, StrikeWorkedProperty, TimeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(TimeTicks - worked);
			if (days <= 0)
			{
				return;
			}
			if (Hands <= 0)
			{
				if (Building.GetIntProperty(StrikeAnnouncedProperty) != 1)
				{
					Building.SetIntProperty(StrikeAnnouncedProperty, 1);
					System.Ledger.Note("{{r|The " + Building.ShortDisplayName + " is condemned, and there is nobody free to take it down. Stand a settler down off the water or a work.}}");
				}
				WriteTick(Building, StrikeWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
				return;
			}
			Building.SetIntProperty(StrikeAnnouncedProperty, 0);
			WriteTick(Building, StrikeWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
			int left = Building.GetIntProperty(StrikeEffortProperty) - KingdomMaterialRules.EffortWorked(Hands, days);
			if (left > 0)
			{
				Building.SetIntProperty(StrikeEffortProperty, left);
				return;
			}
			string name = Building.ShortDisplayName;
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			KingdomMaterialTally salvage = KingdomMaterialRules.StrikeSalvage(CostFor(key));
			Cell cell = Building.CurrentCell;
			MaterialStock stock = Stock(Z);
			int spilled = stock.PutAll(salvage, cell);
			// Only ever an object the settlement created and marked, per the protection law:
			// OrderStrike refuses anything without KingdomBuilt, and nothing else sets this.
			// The socket layer reads the plot's own rect off Building before it goes, and reports
			// whether it took over the telling: a live conversion supplies its own single combined
			// line, an ordinary strike leaves the rect a re-buildable socket and says nothing here.
			bool convertedInPlace = KingdomSocket.OnCleared(System, Z, Building);
			Building.Obliterate();
			string returned = salvage.Describe();
			if (!convertedInPlace)
			{
				KingdomChronicle.Record(System, "the " + name + " of " + System.KingdomDisplayName + " was struck, and the crew stood in the gap where it had been");
				System.RecordDeed("the striking of the " + name + " at " + System.KingdomDisplayName);
				MessageQueue.AddPlayerMessage("{{W|The " + name + " comes down.}} "
					+ ((returned == null) ? "Nothing of it was worth keeping." : (returned + " is carried to the stockpiles."))
					+ ((spilled > 0) ? " Some of it went on the ground for want of a stockpile." : ""));
			}
			KingdomLog.Log("materials: struck " + name + " salvage=" + (returned ?? "none") + " spilled=" + spilled);
		}

		private static void WorkClearance(KingdomSystem System, Zone Z, GameObject StakeObject, r_KingdomClearance Order, int Hands, long TimeTicks)
		{
			if (Order.LastWorkedTick <= 0)
			{
				Order.LastWorkedTick = TimeTicks;
				return;
			}
			int days = KingdomRules.ElapsedDays(TimeTicks - Order.LastWorkedTick);
			if (days <= 0)
			{
				return;
			}
			if (Hands <= 0)
			{
				if (!Order.NoHandsAnnounced)
				{
					Order.NoHandsAnnounced = true;
					System.Ledger.Note("{{r|Ground stands staked for clearing at " + System.SeatName + ", and there is nobody free to swing at it. Stand a settler down off the water or a work.}}");
				}
				Order.LastWorkedTick = KingdomRules.AdvanceCheckpoint(Order.LastWorkedTick, TimeTicks);
				return;
			}
			Order.NoHandsAnnounced = false;
			Order.LastWorkedTick = KingdomRules.AdvanceCheckpoint(Order.LastWorkedTick, TimeTicks);
			if (Order.EffortLeft > 0)
			{
				Order.EffortLeft -= KingdomMaterialRules.EffortWorked(Hands, days);
				if (Order.EffortLeft > 0)
				{
					return;
				}
			}
			// Re-read the ground rather than trusting what was assessed at staking: a tree may
			// have burned down since, and something the settlement will not touch may have been
			// set down in the rect. The yield is whatever actually comes out, and an obstruction
			// stops the finish rather than being cleared around.
			ClearanceAssessment assessment = Assess(System, Z, Order.X1, Order.Y1, Order.X2, Order.Y2);
			if (!assessment.Valid || assessment.Refusal != null)
			{
				if (!Order.BlockedAnnounced)
				{
					Order.BlockedAnnounced = true;
					System.Ledger.Note("{{r|" + (assessment.Refusal ?? "The staked ground cannot be read.") + " The clearing waits.}}");
				}
				return;
			}
			Order.BlockedAnnounced = false;
			KingdomMaterialTally yield = new KingdomMaterialTally();
			int removed = 0;
			for (int y = Order.Y1; y <= Order.Y2; y++)
			{
				for (int x = Order.X1; x <= Order.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					List<GameObject> standing = cell.GetObjects();
					for (int i = 0; i < standing.Count; i++)
					{
						GameObject item = standing[i];
						if (!GameObject.Validate(item) || !TryClassify(item, out var kind))
						{
							continue;
						}
						yield.Add(KingdomMaterialRules.YieldMaterial(kind), KingdomMaterialRules.YieldUnits(kind));
						item.Obliterate();
						removed++;
					}
				}
			}
			yield.Add(KingdomMaterial.Mud, KingdomMaterialRules.GroundMud(assessment.Cells));
			Cell stakeCell = StakeObject.CurrentCell;
			MaterialStock stock = Stock(Z);
			int spilled = stock.PutAll(yield, stakeCell);
			StakeObject.Obliterate();
			string carried = yield.Describe();
			KingdomChronicle.Record(System, assessment.Cells + " paces of ground were cleared at " + System.KingdomDisplayName + ", and " + ((carried == null) ? "nothing came of it but turned earth" : ("the settlement was the richer by " + carried)));
			System.RecordDeed("the ground cleared at " + System.KingdomDisplayName);
			MessageQueue.AddPlayerMessage("{{G|The ground is clear.}} " + removed + ((removed == 1) ? " thing came down" : " things came down")
				+ ((carried == null) ? "." : (", and " + carried + " went to the stockpiles."))
				+ ((spilled > 0) ? " Some of it lies on the ground for want of a stockpile." : ""));
			KingdomLog.Log("materials: clearance done cells=" + assessment.Cells + " removed=" + removed + " yield=" + (carried ?? "none") + " spilled=" + spilled);
		}

		// --- Reading the ground ---------------------------------------------------------------

		/// <summary>
		/// Whether one object standing in a rect stops the whole rect from being cleared, and
		/// why. The protection law in one method: the settlement's own works, the founder's own
		/// things, open water, and anything the mod cannot confidently name are all refusals.
		/// Creatures are not &mdash; a settler standing on the ground walks off it.
		/// </summary>
		/// <param name="Object">The object to judge.</param>
		/// <param name="Reason">A founder-facing sentence when this returns true.</param>
		public static bool IsProtected(GameObject Object, out string Reason)
		{
			Reason = null;
			if (Object == null || !GameObject.Validate(Object))
			{
				return false;
			}
			if (Object.IsCreature || Object.IsPlayer())
			{
				return false;
			}
			if (Object.GetPart<XRL.World.Parts.Physics>() == null)
			{
				return false;
			}
			if (Object.GetIntProperty("KingdomBuilt") == 1 || Object.GetIntProperty("KingdomCitizen") == 1
				|| Object.GetIntProperty("KingdomStores") == 1 || Object.GetIntProperty("KingdomLarder") == 1
				|| Object.GetIntProperty(StockpileProperty) == 1
				|| Object.HasPart("r_KingdomScaffold") || Object.HasPart("r_KingdomPlanMarker")
				|| Object.HasPart("r_KingdomPlot"))
			{
				Reason = "The " + Object.ShortDisplayName + " stands on that ground, and the settlement does not clear away its own. Strike it if you want the ground back.";
				return true;
			}
			XRL.World.Parts.LiquidVolume liquid = Object.GetPart<XRL.World.Parts.LiquidVolume>();
			if (liquid != null)
			{
				Reason = (liquid.MaxVolume < 0)
					? "There is open water on that ground. Water is an asset, not an obstacle, and the settlement will not fill it in."
					: ("The " + Object.ShortDisplayName + " stands on that ground, and it is yours, not the settlement's.");
				return true;
			}
			if (TryClassify(Object, out var kind))
			{
				return false;
			}
			if (Object.HasPart("HologramMaterial") || Object.IsDoor())
			{
				Reason = "The " + Object.ShortDisplayName + " on that ground is nothing the settlement knows how to take apart.";
				return true;
			}
			if (Object.IsTakeable() || Object.Inventory != null)
			{
				Reason = "The " + Object.ShortDisplayName + " lies on that ground, and nothing you put down is ever cleared away. Move it, and ask again.";
				return true;
			}
			if (Object.IsWall() || Object.HasTag("Wall"))
			{
				Reason = "The " + Object.ShortDisplayName + " stands on that ground, and the settlement cannot tell what it is made of.";
				return true;
			}
			return false;
		}

		/// <summary>
		/// What one object is worth clearing, by what it is made of. Reads vanilla's own
		/// vocabulary: the <c>Tree</c> and <c>Plant</c> tags, the <c>SemanticGeological</c> tag
		/// rock walls and boulders carry, the <c>Metal</c> part on every manufactured metal wall,
		/// and the <c>PaintedWall</c> and <c>BodyType</c> tags that tell marble from brinestalk
		/// from canvas.
		/// </summary>
		/// <param name="Object">The object to read.</param>
		/// <param name="Standing">Set on success.</param>
		/// <returns>False for anything that is not the settlement's to take &mdash; which is
		/// everything <see cref="IsProtected"/> refuses, plus everything nobody would call
		/// terrain.</returns>
		public static bool TryClassify(GameObject Object, out KingdomStanding Standing)
		{
			Standing = KingdomStanding.Nothing;
			if (Object == null || !GameObject.Validate(Object) || Object.IsCreature || Object.IsPlayer())
			{
				return false;
			}
			if (Object.HasPart("HologramMaterial") || Object.IsDoor())
			{
				return false;
			}
			if (Object.GetIntProperty("KingdomBuilt") == 1 || Object.HasPart("r_KingdomScaffold")
				|| Object.HasPart("r_KingdomPlanMarker") || Object.HasPart("r_KingdomPlot")
				|| Object.HasPart("r_KingdomClearance"))
			{
				return false;
			}
			if (Object.HasTag("Tree"))
			{
				Standing = KingdomStanding.Tree;
				return true;
			}
			bool isWall = Object.IsWall() || Object.HasTag("Wall");
			if (!isWall && (Object.HasTag("Plant") || Object.HasTag("LivePlant")))
			{
				Standing = KingdomStanding.Brush;
				return true;
			}
			if (Object.Blueprint == "Rubble" || Object.Blueprint == "Rubble Grey")
			{
				Standing = KingdomStanding.Rubble;
				return true;
			}
			bool geological = Object.HasTag("SemanticGeological");
			if (geological && !isWall && Object.IsTakeable())
			{
				// A boulder: naturally occurring, portable in principle, and the reason a rocky
				// site is a stone site.
				Standing = KingdomStanding.Rock;
				return true;
			}
			if (!isWall)
			{
				return false;
			}
			string painted = Object.GetTag("PaintedWall", "");
			string bodyType = Object.GetTag("BodyType", "");
			if (painted == "wall_marble")
			{
				Standing = KingdomStanding.MarbleSeam;
				return true;
			}
			if (Object.HasPart("Metal"))
			{
				Standing = KingdomStanding.Ruin;
				return true;
			}
			if (bodyType == "WoodWall" || painted == "wall_wood" || painted == "wall_wood_worn" || painted == "wall_brinestalk")
			{
				Standing = KingdomStanding.Tree;
				return true;
			}
			if (bodyType == "ClothWall" || bodyType == "PlantWall" || bodyType == "FungusWall" || painted == "wall_plant" || painted == "wall_mushroom")
			{
				Standing = KingdomStanding.Brush;
				return true;
			}
			if (geological || painted == "wall_rock" || painted == "wall_stone" || painted == "wall_brick" || painted == "wall_granite")
			{
				Standing = KingdomStanding.Rock;
				return true;
			}
			Standing = KingdomStanding.Ruin;
			return true;
		}

		/// <summary>The base Hitpoints a blueprint declares, which is how hard a thing is to bring
		/// down. Zero when the object carries no such stat.</summary>
		public static int BaseHitpoints(GameObject Object)
		{
			if (Object == null)
			{
				return 0;
			}
			Statistic stat = Object.GetStat("Hitpoints");
			return (stat != null) ? stat.BaseValue : 0;
		}

		/// <summary>Reads a tick stored as a string property. Zero for absent or unreadable,
		/// which every caller treats as "not stamped yet" rather than as an error.</summary>
		public static long ReadTick(GameObject Object, string Property)
		{
			string text = Object?.GetStringProperty(Property);
			if (string.IsNullOrEmpty(text) || !long.TryParse(text, out var tick) || tick < 0)
			{
				return 0L;
			}
			return tick;
		}

		/// <summary>Stamps a tick into a string property.</summary>
		public static void WriteTick(GameObject Object, string Property, long Tick)
		{
			Object?.SetStringProperty(Property, Tick.ToString());
		}

		private static bool Overlaps(r_KingdomClearance Order, int X1, int Y1, int X2, int Y2)
		{
			return Order.X1 <= X2 && Order.X2 >= X1 && Order.Y1 <= Y2 && Order.Y2 >= Y1;
		}

		// --- Wall material: the theme ---------------------------------------------------------

		/// <summary>
		/// The material this settlement's walls are made of, from its style's taste and what its
		/// own quarrying has actually earned it. Never fails: a settlement that has quarried
		/// nothing walls itself in mud, which is what a camp looks like.
		/// </summary>
		/// <param name="System">The kingdom, for its style. Null reads as styleless.</param>
		/// <param name="Z">The ground whose stockpiles are read. Null reads as empty.</param>
		public static KingdomMaterial WallMaterialFor(KingdomSystem System, Zone Z)
		{
			return KingdomMaterialRules.WallMaterialFor(Stock(Z).Tally, (System != null) ? System.Style : null);
		}

		/// <summary>
		/// The vanilla wall blueprint a material builds as, in this settlement's style. Every
		/// name here is one of vanilla's own settlement walls &mdash; the same vocabulary
		/// <c>Village_StructureWall_*Default</c> draws from &mdash; so a stamped plot reads as
		/// part of the world rather than as something the mod invented.
		/// </summary>
		/// <param name="Material">What the settlement has to build in.</param>
		/// <param name="Style">The city style key, or null.</param>
		/// <returns>A blueprint name; never null.</returns>
		public static string WallBlueprint(KingdomMaterial Material, string Style)
		{
			switch (Material)
			{
			case KingdomMaterial.Marble:
				return "Marble";
			case KingdomMaterial.ShapedStone:
				// Vanilla's own dressed-stone wall, and one of the six the game's village
				// generator already picks from (Village_StructureWall_*Default).
				return "Fulcrete";
			case KingdomMaterial.WorkedMetal:
				return "MetalWall";
			case KingdomMaterial.ShapedTimber:
				// Planks rather than stalks: a settlement with its own saw-pit builds in boards,
				// whatever grows nearby, so this one takes no style variant.
				return "WoodWall";
			case KingdomMaterial.Stone:
				return "Limestone";
			case KingdomMaterial.Scrap:
				return "Verdigris";
			case KingdomMaterial.Timber:
				if (Style == "fungal")
				{
					return "MushroomWall-White";
				}
				if (Style == "verdant")
				{
					return "PlantWall";
				}
				return "BrinestalkWall";
			case KingdomMaterial.Brush:
				return "CanvasWall";
			default:
				return "BrickWall";
			}
		}
	}
}
