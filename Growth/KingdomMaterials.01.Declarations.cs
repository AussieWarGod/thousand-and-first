using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

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
	public static partial class KingdomMaterials
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

		/// <summary>Stable key stamped on each exact salvage output before insertion.</summary>
		public const string StrikeSalvageReceiptProperty = "KingdomStrikeSalvageReceipt";

		/// <summary>Blueprint of the marker a clearance order stands as.</summary>
		public const string ClearanceStakeBlueprint = "r_KingdomClearanceStake";

		/// <summary>Property-backed ground-yield commit phase: 0 unsent, 1 callback pending,
		/// 2 settled. Kept off the shipped serialized part so old saves retain field layout.</summary>
		public const string ClearanceGroundPhaseProperty = "KingdomClearanceGroundPhase";

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
	}
}
