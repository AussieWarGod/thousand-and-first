using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement's material vocabulary, in two halves.
	/// <para>
	/// <b>Raw</b> &mdash; mud from turned ground, brush cut and retted into canvas and cord, timber
	/// from trees, stone from rock walls and boulders, marble from a seam, scrap from a ruin. Every
	/// one of them already stands in a Qud zone and can be carried away from it. Nothing here is
	/// minted; clearance, salvage, and trade are the only three doors raw material comes through.
	/// </para>
	/// <para>
	/// <b>Refined</b> &mdash; shaped timber off a sawyer's yard, shaped stone off a mason's yard,
	/// worked metal out of a smelter. These come through a fourth door and only that one: a staffed
	/// yard, standing on the settlement's own ground, working raw stock the settlement already
	/// earned (<see cref="KingdomMaterialRules.RawPerRefined"/>). No clearance yields them, no seam
	/// holds them, and no amount of waiting makes them: they are labour, which is the only thing in
	/// this economy that ever turns one good into a better one.
	/// </para>
	/// </summary>
	public enum KingdomMaterial
	{
		Mud = 0,
		Brush = 1,
		Timber = 2,
		Stone = 3,
		Marble = 4,
		Scrap = 5,
		ShapedTimber = 6,
		ShapedStone = 7,
		WorkedMetal = 8
	}

	/// <summary>
	/// The three processing works, one per refined material. A yard is an ordinary catalogue
	/// design that happens to declare what it refines; this enum only names the three the base
	/// catalogue ships, so the rules can talk about them without reading the registry.
	/// </summary>
	public enum KingdomYard
	{
		/// <summary>Saw-pit and trestles. Timber in, shaped timber out.</summary>
		Sawyer = 0,

		/// <summary>Banker, chisels, and a heap of spoil. Stone (or marble) in, shaped stone out.
		/// </summary>
		Mason = 1,

		/// <summary>Furnace and crucible. Scrap in, worked metal out.</summary>
		Smelter = 2
	}

	/// <summary>
	/// Which of a settler's own numbers a kind of work is done with. Read off who the people are
	/// (<c>Strength</c>, <c>Intelligence</c>) rather than assigned by the founder, per Addendum 7:
	/// stonework and haulage are muscle, a furnace and a certified machine are mind.
	/// </summary>
	public enum KingdomCapability
	{
		Muscle = 0,
		Mind = 1
	}

	/// <summary>
	/// The rare finds a great work is short of. Every one of them is an item the game already
	/// ships and scatters &mdash; a bronze ingot, a silver or gold nugget, a rough gemstone off the
	/// same ground &mdash; so an exotic is never crafted, never minted, and never something the
	/// settlement can decide to make. Somebody walks it home, and until somebody does, the
	/// cathedral's dome waits and says so.
	/// </summary>
	public enum KingdomExotic
	{
		Ingot = 0,
		Silver = 1,
		Gold = 2,
		Gem = 3
	}

	/// <summary>
	/// What stands on one cell, as far as clearing it is concerned. The order is the order of
	/// worth: bare ground gives up almost nothing, a marble seam gives up the rarest thing the
	/// settlement can hold. <c>KingdomMaterials.Classify</c> is the engine-coupled half that
	/// reads a real <c>GameObject</c> into one of these.
	/// </summary>
	public enum KingdomStanding
	{
		Nothing = 0,
		Brush = 1,
		Rubble = 2,
		Tree = 3,
		Rock = 4,
		Ruin = 5,
		MarbleSeam = 6
	}

	/// <summary>
	/// A count of each material: what a stockpile holds, what a design costs, what a cleared
	/// rect will yield. Never serialized and never persisted &mdash; a stockpile's contents are
	/// real items in a real container, and a tally is only ever the reading taken of them, the
	/// same way <c>KingdomSurvey.FoodStored</c> is a reading and not a store.
	/// </summary>
	public sealed class KingdomMaterialTally
	{
		private readonly int[] Amounts = new int[KingdomMaterialRules.MaterialCount];

		/// <summary>Units of one material held. Never negative.</summary>
		public int Get(KingdomMaterial Material)
		{
			int index = (int)Material;
			if (index < 0 || index >= Amounts.Length)
			{
				return 0;
			}
			return Amounts[index];
		}

		/// <summary>
		/// Adds units of one material, clamping the result at zero rather than going negative.
		/// </summary>
		/// <param name="Material">Which material.</param>
		/// <param name="Units">May be negative, to take units away.</param>
		public void Add(KingdomMaterial Material, int Units)
		{
			int index = (int)Material;
			if (index < 0 || index >= Amounts.Length)
			{
				return;
			}
			int total = Amounts[index] + Units;
			Amounts[index] = (total > 0) ? total : 0;
		}

		/// <summary>Sets units of one material outright, clamping negatives to zero.</summary>
		public void Set(KingdomMaterial Material, int Units)
		{
			int index = (int)Material;
			if (index < 0 || index >= Amounts.Length)
			{
				return;
			}
			Amounts[index] = (Units > 0) ? Units : 0;
		}

		/// <summary>Adds every material of Other into this tally. Null is a no-op.</summary>
		public void AddAll(KingdomMaterialTally Other)
		{
			if (Other == null)
			{
				return;
			}
			for (int i = 0; i < Amounts.Length; i++)
			{
				Add((KingdomMaterial)i, Other.Amounts[i]);
			}
		}

		/// <summary>Units of every material added together.</summary>
		public int Total()
		{
			int total = 0;
			for (int i = 0; i < Amounts.Length; i++)
			{
				total += Amounts[i];
			}
			return total;
		}

		/// <summary>True when nothing at all is held. An absent material cost is empty, and an
		/// empty cost is what makes every design written before materials existed still buildable
		/// for water alone.</summary>
		public bool IsEmpty()
		{
			return Total() == 0;
		}

		/// <summary>An independent copy; mutating the copy never touches this tally.</summary>
		public KingdomMaterialTally Copy()
		{
			KingdomMaterialTally copy = new KingdomMaterialTally();
			for (int i = 0; i < Amounts.Length; i++)
			{
				copy.Amounts[i] = Amounts[i];
			}
			return copy;
		}

		/// <summary>
		/// This tally with every material multiplied by Percent and rounded down. Used for
		/// partial salvage; a percentage that would round a single unit away really does round
		/// it away, because a struck hut does not return the beam it was built from whole.
		/// </summary>
		public KingdomMaterialTally Scaled(int Percent)
		{
			KingdomMaterialTally scaled = new KingdomMaterialTally();
			if (Percent <= 0)
			{
				return scaled;
			}
			for (int i = 0; i < Amounts.Length; i++)
			{
				scaled.Amounts[i] = Amounts[i] * Percent / 100;
			}
			return scaled;
		}

		/// <summary>Player-facing prose: "8 timber and 4 cut stone", or null when empty.</summary>
		public string Describe()
		{
			List<string> parts = new List<string>();
			for (int i = 0; i < Amounts.Length; i++)
			{
				if (Amounts[i] > 0)
				{
					parts.Add(Amounts[i] + " " + KingdomMaterialRules.MaterialName((KingdomMaterial)i));
				}
			}
			return KingdomMaterialRules.JoinPhrases(parts);
		}

		/// <summary>Units of every REFINED material held together &mdash; what the yards have
		/// made, as against what the ground gave up.</summary>
		public int RefinedTotal()
		{
			int total = 0;
			for (int i = 0; i < Amounts.Length; i++)
			{
				if (KingdomMaterialRules.IsRefined((KingdomMaterial)i))
				{
					total += Amounts[i];
				}
			}
			return total;
		}
	}

	/// <summary>
	/// A count of bits by tier: what a design costs in tinkering stock, and what the stockpiles
	/// hold of it. Never serialized, for the same reason <see cref="KingdomMaterialTally"/> is not
	/// &mdash; the settlement's bits are real items in a real container, and this is only ever the
	/// reading taken of them. A founder donates bits by putting the scrap in the stockpile.
	/// </summary>
	public sealed class KingdomBitTally
	{
		private readonly int[] Amounts = new int[KingdomMaterialRules.BitTierCount];

		/// <summary>Bits of one tier held. Never negative; a tier outside the ladder is zero.
		/// </summary>
		public int Get(int Tier)
		{
			return (Tier < 0 || Tier >= Amounts.Length) ? 0 : Amounts[Tier];
		}

		/// <summary>Adds bits of one tier, clamping at zero rather than going negative.</summary>
		public void Add(int Tier, int Count)
		{
			if (Tier < 0 || Tier >= Amounts.Length)
			{
				return;
			}
			int total = Amounts[Tier] + Count;
			Amounts[Tier] = (total > 0) ? total : 0;
		}

		/// <summary>Sets bits of one tier outright, clamping negatives to zero.</summary>
		public void Set(int Tier, int Count)
		{
			if (Tier < 0 || Tier >= Amounts.Length)
			{
				return;
			}
			Amounts[Tier] = (Count > 0) ? Count : 0;
		}

		/// <summary>Adds every tier of Other into this tally. Null is a no-op.</summary>
		public void AddAll(KingdomBitTally Other)
		{
			if (Other == null)
			{
				return;
			}
			for (int i = 0; i < Amounts.Length; i++)
			{
				Add(i, Other.Amounts[i]);
			}
		}

		/// <summary>Bits of every tier added together.</summary>
		public int Total()
		{
			int total = 0;
			for (int i = 0; i < Amounts.Length; i++)
			{
				total += Amounts[i];
			}
			return total;
		}

		/// <summary>True when no bits at all are held. An absent bit cost is empty, and an empty
		/// cost is what every design written before bits existed goes on costing.</summary>
		public bool IsEmpty()
		{
			return Total() == 0;
		}

		/// <summary>An independent copy; mutating the copy never touches this tally.</summary>
		public KingdomBitTally Copy()
		{
			KingdomBitTally copy = new KingdomBitTally();
			for (int i = 0; i < Amounts.Length; i++)
			{
				copy.Amounts[i] = Amounts[i];
			}
			return copy;
		}

		/// <summary>This tally with every tier multiplied by Percent and rounded down. Used for
		/// the share of a design's bits a repair puts back.</summary>
		public KingdomBitTally Scaled(int Percent)
		{
			KingdomBitTally scaled = new KingdomBitTally();
			if (Percent <= 0)
			{
				return scaled;
			}
			for (int i = 0; i < Amounts.Length; i++)
			{
				scaled.Amounts[i] = Amounts[i] * Percent / 100;
			}
			return scaled;
		}

		/// <summary>Player-facing prose: "2 of scrap and 1 of pure alloy", or null when empty.
		/// </summary>
		public string Describe()
		{
			List<string> parts = new List<string>();
			for (int i = 0; i < Amounts.Length; i++)
			{
				if (Amounts[i] > 0)
				{
					parts.Add(Amounts[i] + " of " + KingdomMaterialRules.BitTierName(i));
				}
			}
			return KingdomMaterialRules.JoinPhrases(parts);
		}
	}

	/// <summary>
	/// A count of rare finds: what a great work is short of, and what the stockpiles hold. Never
	/// serialized, for <see cref="KingdomMaterialTally"/>'s own reason.
	/// </summary>
	public sealed class KingdomExoticTally
	{
		private readonly int[] Amounts = new int[KingdomMaterialRules.ExoticCount];

		/// <summary>Units of one exotic held. Never negative.</summary>
		public int Get(KingdomExotic Exotic)
		{
			int index = (int)Exotic;
			return (index < 0 || index >= Amounts.Length) ? 0 : Amounts[index];
		}

		/// <summary>Adds units of one exotic, clamping at zero rather than going negative.
		/// </summary>
		public void Add(KingdomExotic Exotic, int Units)
		{
			int index = (int)Exotic;
			if (index < 0 || index >= Amounts.Length)
			{
				return;
			}
			int total = Amounts[index] + Units;
			Amounts[index] = (total > 0) ? total : 0;
		}

		/// <summary>Sets units of one exotic outright, clamping negatives to zero.</summary>
		public void Set(KingdomExotic Exotic, int Units)
		{
			int index = (int)Exotic;
			if (index < 0 || index >= Amounts.Length)
			{
				return;
			}
			Amounts[index] = (Units > 0) ? Units : 0;
		}

		/// <summary>Units of every exotic added together.</summary>
		public int Total()
		{
			int total = 0;
			for (int i = 0; i < Amounts.Length; i++)
			{
				total += Amounts[i];
			}
			return total;
		}

		/// <summary>True when nothing rare is held or wanted.</summary>
		public bool IsEmpty()
		{
			return Total() == 0;
		}

		/// <summary>An independent copy; mutating the copy never touches this tally.</summary>
		public KingdomExoticTally Copy()
		{
			KingdomExoticTally copy = new KingdomExoticTally();
			for (int i = 0; i < Amounts.Length; i++)
			{
				copy.Amounts[i] = Amounts[i];
			}
			return copy;
		}

		/// <summary>Player-facing prose: "2 gold nuggets and 1 rough gemstone", or null when
		/// empty.</summary>
		public string Describe()
		{
			List<string> parts = new List<string>();
			for (int i = 0; i < Amounts.Length; i++)
			{
				if (Amounts[i] > 0)
				{
					parts.Add(Amounts[i] + " " + KingdomMaterialRules.ExoticName((KingdomExotic)i, Amounts[i]));
				}
			}
			return KingdomMaterialRules.JoinPhrases(parts);
		}
	}

	/// <summary>
	/// Every rule the material economy can settle without a running game: what a cell yields
	/// when it is cleared, what clearing it costs in crew, what a design's material cost parses
	/// to, what a struck building gives back, and which wall material a settlement's own
	/// quarrying has earned it. The engine-coupled half &mdash; reading real objects, destroying
	/// what stood there, putting real items in a real stockpile &mdash; is
	/// <c>KingdomMaterials</c>, in the same folder.
	/// </summary>
	public static class KingdomMaterialRules
	{
		/// <summary>Number of values in <see cref="KingdomMaterial"/>. Sized against the enum by
		/// <c>KingdomMaterialRulesTests</c> so a tenth material cannot be added without the
		/// tallies growing with it.</summary>
		public const int MaterialCount = 9;

		/// <summary>Number of values in <see cref="KingdomStanding"/>.</summary>
		public const int StandingCount = 7;

		/// <summary>Number of values in <see cref="KingdomYard"/>, which is also the number of
		/// refined materials: one yard refines one thing, always.</summary>
		public const int YardCount = 3;

		/// <summary>
		/// Registry keys, in enum order: what third-party XML writes in a <c>Materials</c>
		/// attribute and what <see cref="TryParseMaterial"/> accepts.
		/// </summary>
		public static readonly string[] MaterialKeys = new string[MaterialCount] { "mud", "brush", "timber", "stone", "marble", "scrap", "shapedtimber", "shapedstone", "workedmetal" };

		/// <summary>Player-facing names, in enum order. Lowercase, in the game's register.</summary>
		public static readonly string[] MaterialNames = new string[MaterialCount] { "mud", "brush", "timber", "cut stone", "marble", "scrap metal", "shaped timber", "shaped stone", "worked metal" };

		/// <summary>The registry key for a material. Empty for a value outside the enum.</summary>
		public static string MaterialKey(KingdomMaterial Material)
		{
			int index = (int)Material;
			if (index < 0 || index >= MaterialKeys.Length)
			{
				return "";
			}
			return MaterialKeys[index];
		}

		/// <summary>The player-facing name for a material. Empty for a value outside the enum.</summary>
		public static string MaterialName(KingdomMaterial Material)
		{
			int index = (int)Material;
			if (index < 0 || index >= MaterialNames.Length)
			{
				return "";
			}
			return MaterialNames[index];
		}

		/// <summary>
		/// Reads a registry key. Case-insensitive and whitespace-tolerant, because the keys come
		/// out of hand-written XML, and it accepts the aliases the prose uses for keys that do
		/// not read the same way in a sentence: "scrap metal" for <c>scrap</c>, "canvas" for
		/// <c>brush</c>, which is what brush becomes once it has been cut and retted, and the
		/// spaced spellings of the three refined materials, which are two words everywhere except
		/// in an attribute.
		/// </summary>
		/// <param name="Key">Text to read. Null, empty, and unknown all fail.</param>
		/// <param name="Material">Set on success; <see cref="KingdomMaterial.Mud"/> otherwise,
		/// which callers must not read.</param>
		/// <returns>False for anything that is not one of <see cref="MaterialKeys"/>.</returns>
		public static bool TryParseMaterial(string Key, out KingdomMaterial Material)
		{
			Material = KingdomMaterial.Mud;
			if (string.IsNullOrEmpty(Key))
			{
				return false;
			}
			string trimmed = Key.Trim().ToLowerInvariant();
			if (trimmed == "scrap metal" || trimmed == "scrapmetal")
			{
				Material = KingdomMaterial.Scrap;
				return true;
			}
			if (trimmed == "canvas")
			{
				Material = KingdomMaterial.Brush;
				return true;
			}
			if (trimmed == "shaped timber" || trimmed == "sawn timber")
			{
				Material = KingdomMaterial.ShapedTimber;
				return true;
			}
			if (trimmed == "shaped stone" || trimmed == "dressed stone")
			{
				Material = KingdomMaterial.ShapedStone;
				return true;
			}
			if (trimmed == "worked metal")
			{
				Material = KingdomMaterial.WorkedMetal;
				return true;
			}
			for (int i = 0; i < MaterialKeys.Length; i++)
			{
				if (MaterialKeys[i] == trimmed)
				{
					Material = (KingdomMaterial)i;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Reads a design's material cost: <c>"timber:8, stone:4"</c>.
		/// <para>
		/// An absent or empty attribute is not an error and never was &mdash; it parses to an
		/// empty tally, which is exactly the water-only cost every design in the catalogue had
		/// before materials existed. That is the whole compatibility guarantee, and it is a
		/// guarantee about third-party files as much as ours.
		/// </para>
		/// </summary>
		/// <param name="Text">The attribute's value, or null.</param>
		/// <param name="Cost">Always set to a tally, empty when this returns false, so a caller
		/// that logs the error and carries on is never handed null.</param>
		/// <param name="Error">Null on success, else a log-facing reason naming the offending
		/// term. The whole attribute is rejected; nothing is half-parsed.</param>
		/// <returns>False only for text that is present and malformed.</returns>
		public static bool TryParseMaterialCost(string Text, out KingdomMaterialTally Cost, out string Error)
		{
			Cost = new KingdomMaterialTally();
			Error = null;
			if (string.IsNullOrEmpty(Text) || Text.Trim().Length == 0)
			{
				return true;
			}
			string[] terms = Text.Split(',');
			bool[] seen = new bool[MaterialCount];
			for (int i = 0; i < terms.Length; i++)
			{
				string term = terms[i].Trim();
				if (term.Length == 0)
				{
					Error = "empty term in material cost \"" + Text + "\"";
					Cost = new KingdomMaterialTally();
					return false;
				}
				int split = term.LastIndexOf(':');
				if (split <= 0 || split == term.Length - 1)
				{
					Error = "material term \"" + term + "\" is not of the form material:units";
					Cost = new KingdomMaterialTally();
					return false;
				}
				if (!TryParseMaterial(term.Substring(0, split), out var material))
				{
					Error = "unknown material \"" + term.Substring(0, split).Trim() + "\"";
					Cost = new KingdomMaterialTally();
					return false;
				}
				if (!int.TryParse(term.Substring(split + 1).Trim(), out var units) || units <= 0)
				{
					Error = "material \"" + MaterialKey(material) + "\" needs a positive whole number of units";
					Cost = new KingdomMaterialTally();
					return false;
				}
				if (seen[(int)material])
				{
					Error = "material \"" + MaterialKey(material) + "\" is named twice";
					Cost = new KingdomMaterialTally();
					return false;
				}
				seen[(int)material] = true;
				Cost.Set(material, units);
			}
			return true;
		}

		/// <summary>Whether Stock holds at least every unit Cost asks for. A null or empty cost
		/// is always covered, including by an empty stock.</summary>
		public static bool Covers(KingdomMaterialTally Stock, KingdomMaterialTally Cost)
		{
			if (Cost == null || Cost.IsEmpty())
			{
				return true;
			}
			for (int i = 0; i < MaterialCount; i++)
			{
				KingdomMaterial material = (KingdomMaterial)i;
				int held = (Stock == null) ? 0 : Stock.Get(material);
				if (held < Cost.Get(material))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// What Stock is short of Cost, per material. Empty when <see cref="Covers"/> is true, so
		/// a refusal can name the shortfall rather than restating the whole price.
		/// </summary>
		public static KingdomMaterialTally Missing(KingdomMaterialTally Stock, KingdomMaterialTally Cost)
		{
			KingdomMaterialTally missing = new KingdomMaterialTally();
			if (Cost == null)
			{
				return missing;
			}
			for (int i = 0; i < MaterialCount; i++)
			{
				KingdomMaterial material = (KingdomMaterial)i;
				int held = (Stock == null) ? 0 : Stock.Get(material);
				int shortfall = Cost.Get(material) - held;
				if (shortfall > 0)
				{
					missing.Set(material, shortfall);
				}
			}
			return missing;
		}

		// --- Clearance: what removal earns, and what it costs in crew ------------------------

		/// <summary>Effort one cell costs before its hardness is read, indexed by
		/// <see cref="KingdomStanding"/>.</summary>
		public static readonly int[] StandingEffort = new int[StandingCount] { 1, 4, 8, 20, 30, 40, 40 };

		/// <summary>Units one cell yields, indexed by <see cref="KingdomStanding"/>. Bare ground
		/// yields nothing on its own &mdash; its mud comes from <see cref="GroundMud"/>, which is
		/// counted once for the whole rect rather than once per empty cell.</summary>
		public static readonly int[] StandingYield = new int[StandingCount] { 0, 1, 2, 3, 3, 2, 2 };

		/// <summary>Cells of turned ground that give up one load of mud. Mud is the spoil of
		/// digging, not a thing that stood anywhere, so it is counted against the rect rather
		/// than against what was removed from it.</summary>
		public const int MudPerCells = 4;

		/// <summary>Effort one settler removes in one day of clearing.</summary>
		public const int EffortPerHandPerDay = 10;

		/// <summary>
		/// Settlers who can usefully swing at one clearance at once. A bounded consequence per
		/// visit: a large settlement clears faster than a small one, but never instantly, and a
		/// founder who returns after a long absence still finds work left to watch.
		/// </summary>
		public const int MaxClearingHands = 6;

		/// <summary>What a material costs the settlement in effort to bring down, by hardness.
		/// The hardness bands are read off vanilla's own <c>Hitpoints</c> stat &mdash; canvas 15,
		/// a plain wall 100, shale 200, limestone 1000, marble 6000, granite 26000.</summary>
		public static int HardnessPercent(int Hitpoints)
		{
			if (Hitpoints <= 50)
			{
				return 60;
			}
			if (Hitpoints <= 200)
			{
				return 100;
			}
			if (Hitpoints <= 1000)
			{
				return 140;
			}
			if (Hitpoints <= 6000)
			{
				return 200;
			}
			return 300;
		}

		/// <summary>
		/// Effort one cell costs to clear. Bare ground is a fixed nominal cost regardless of what
		/// hardness is passed, because nothing stands on it to be hard; everything else scales
		/// with <see cref="HardnessPercent"/> and never falls below one, so no cell is ever free.
		/// </summary>
		/// <param name="Standing">What stands on the cell.</param>
		/// <param name="Hitpoints">The standing object's base Hitpoints, or 0 for bare ground.</param>
		public static int ClearanceEffort(KingdomStanding Standing, int Hitpoints)
		{
			int index = (int)Standing;
			if (index < 0 || index >= StandingCount)
			{
				return 0;
			}
			if (Standing == KingdomStanding.Nothing)
			{
				return StandingEffort[index];
			}
			int effort = StandingEffort[index] * HardnessPercent(Hitpoints) / 100;
			return (effort < 1) ? 1 : effort;
		}

		/// <summary>The material one cleared cell yields up.</summary>
		public static KingdomMaterial YieldMaterial(KingdomStanding Standing)
		{
			switch (Standing)
			{
			case KingdomStanding.Brush:
				return KingdomMaterial.Brush;
			case KingdomStanding.Tree:
				return KingdomMaterial.Timber;
			case KingdomStanding.Rubble:
			case KingdomStanding.Rock:
				return KingdomMaterial.Stone;
			case KingdomStanding.Ruin:
				return KingdomMaterial.Scrap;
			case KingdomStanding.MarbleSeam:
				return KingdomMaterial.Marble;
			default:
				return KingdomMaterial.Mud;
			}
		}

		/// <summary>Units one cleared cell yields of <see cref="YieldMaterial"/>.</summary>
		public static int YieldUnits(KingdomStanding Standing)
		{
			int index = (int)Standing;
			if (index < 0 || index >= StandingCount)
			{
				return 0;
			}
			return StandingYield[index];
		}

		/// <summary>Loads of mud a rect of the given size gives up as the ground is turned over.
		/// Zero for a rect small enough that nobody would call it digging.</summary>
		public static int GroundMud(int CellsCleared)
		{
			if (CellsCleared <= 0)
			{
				return 0;
			}
			return CellsCleared / MudPerCells;
		}

		/// <summary>
		/// Settlers free to clear: everyone the settlement has who is not already carrying water
		/// or crewing a work. Hands are spent once, and this is the third and last claim on them.
		/// </summary>
		/// <param name="Population">The settlement's people.</param>
		/// <param name="AssignedCrew">Citizens the staffing pass already spent, water detail
		/// included &mdash; <c>KingdomSystem.AssignedCrew</c> counts both.</param>
		public static int FreeHands(int Population, int AssignedCrew)
		{
			int free = Population - AssignedCrew;
			return (free > 0) ? free : 0;
		}

		/// <summary>
		/// Days one pair of hands would need to work off the given effort, rounded up, so a job
		/// worth any effort at all is never reported as taking no time. The unit the founder is
		/// quoted in: effort points mean nothing to anybody standing in a field.
		/// </summary>
		public static int DaysForOneHand(int Effort)
		{
			if (Effort <= 0)
			{
				return 0;
			}
			return (Effort + EffortPerHandPerDay - 1) / EffortPerHandPerDay;
		}

		/// <summary>
		/// Effort a clearing detail removes over the days since it was last worked. Clamped at
		/// <see cref="MaxClearingHands"/>; a caller supplies Days from
		/// <c>KingdomRules.HeartbeatDays</c>, which caps absence in its own right, so no length
		/// of absence resolves more than a few days of digging in one visit.
		/// </summary>
		public static int EffortWorked(int FreeHands, int Days)
		{
			if (FreeHands <= 0 || Days <= 0)
			{
				return 0;
			}
			int hands = (FreeHands > MaxClearingHands) ? MaxClearingHands : FreeHands;
			return hands * Days * EffortPerHandPerDay;
		}

		// --- Striking: what comes down, and what comes back ----------------------------------

		/// <summary>Effort even the flimsiest building costs to take down honestly.</summary>
		public const int StrikeBaseEffort = 20;

		/// <summary>Extra effort per unit of material the building was raised from.</summary>
		public const int StrikeEffortPerUnit = 3;

		/// <summary>Drams of the original commission that add one more point of strike effort.
		/// A costly building is a large building, whether or not it was built of anything.</summary>
		public const int StrikeDramsPerEffort = 10;

		/// <summary>
		/// Share of a building's material cost its striking returns. Half, and deliberately less
		/// than all: taking a thing down carefully is still taking it down, and nothing about
		/// striking is a refund. No water is ever returned.
		/// </summary>
		public const int StrikeSalvagePercent = 50;

		/// <summary>
		/// Effort taking a building down costs, from what it was made of and what it cost to
		/// commission. Negative inputs are clamped rather than paying the settlement to demolish.
		/// </summary>
		public static int StrikeEffort(int MaterialUnits, int CostDrams)
		{
			int units = (MaterialUnits > 0) ? MaterialUnits : 0;
			int drams = (CostDrams > 0) ? CostDrams : 0;
			return StrikeBaseEffort + units * StrikeEffortPerUnit + drams / StrikeDramsPerEffort;
		}

		/// <summary>
		/// What striking a building of the given material cost returns to the stockpiles. A
		/// design that cost no materials returns none, and says so rather than inventing timber
		/// out of a water-only hut.
		/// </summary>
		public static KingdomMaterialTally StrikeSalvage(KingdomMaterialTally Cost)
		{
			if (Cost == null)
			{
				return new KingdomMaterialTally();
			}
			return Cost.Scaled(StrikeSalvagePercent);
		}

		// --- Wall material: the theme, chosen by what the settlement has quarried -------------

		/// <summary>
		/// Units of a material a settlement must hold before its walls can be said to be made of
		/// it. Indexed by <see cref="KingdomMaterial"/>; mud is zero, because mud is the ground
		/// and the ground is always there. The refined three sit LOWER than the raw stock they
		/// came from, on purpose: a yard turns two loads into one, so holding six shaped timbers
		/// is holding twelve trees' worth of work, and a settlement that has done that much
		/// dressing is a settlement whose walls look it.
		/// </summary>
		public static readonly int[] WallMaterialThreshold = new int[MaterialCount] { 0, 4, 8, 10, 14, 10, 6, 8, 8 };

		/// <summary>Materials in the order a settlement would rather build in, richest first.
		/// Mud is last and is the floor nothing ever falls through.</summary>
		public static readonly KingdomMaterial[] WallMaterialPreference = new KingdomMaterial[MaterialCount]
		{
			KingdomMaterial.Marble,
			KingdomMaterial.ShapedStone,
			KingdomMaterial.WorkedMetal,
			KingdomMaterial.ShapedTimber,
			KingdomMaterial.Stone,
			KingdomMaterial.Scrap,
			KingdomMaterial.Timber,
			KingdomMaterial.Brush,
			KingdomMaterial.Mud
		};

		/// <summary>
		/// The material a settlement of the given style would choose first if it could afford to,
		/// whatever its stock says. A style is a taste, not a supply: an unmet taste changes
		/// nothing and costs nothing.
		/// </summary>
		/// <param name="Style">A city style key. Unknown and null styles have no preference.</param>
		/// <param name="Material">Set on success.</param>
		/// <returns>False when the style expresses no preference at all.</returns>
		public static bool TryStylePreference(string Style, out KingdomMaterial Material)
		{
			Material = KingdomMaterial.Mud;
			if (string.IsNullOrEmpty(Style))
			{
				return false;
			}
			switch (Style)
			{
			case "verdant":
			case "fungal":
				Material = KingdomMaterial.Timber;
				return true;
			case "gyre":
				Material = KingdomMaterial.Marble;
				return true;
			case "eater":
				Material = KingdomMaterial.Scrap;
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// The material a settlement's walls are made of: its style's taste if its own quarrying
		/// has met the threshold for that material, else the richest material it holds enough of,
		/// else mud. Never fails and never returns something the settlement does not have &mdash;
		/// a settlement that has quarried nothing builds in mud, which is what a camp looks like.
		/// </summary>
		/// <param name="Stock">What the stockpiles hold. Null reads as empty.</param>
		/// <param name="Style">The city's style key, or null.</param>
		public static KingdomMaterial WallMaterialFor(KingdomMaterialTally Stock, string Style)
		{
			if (TryStylePreference(Style, out var preferred) && HasWallMaterial(Stock, preferred))
			{
				return preferred;
			}
			for (int i = 0; i < WallMaterialPreference.Length; i++)
			{
				if (HasWallMaterial(Stock, WallMaterialPreference[i]))
				{
					return WallMaterialPreference[i];
				}
			}
			return KingdomMaterial.Mud;
		}

		/// <summary>Whether the stock has reached the threshold for building walls of a
		/// material. Mud's threshold is zero, so this is always true of mud.</summary>
		public static bool HasWallMaterial(KingdomMaterialTally Stock, KingdomMaterial Material)
		{
			int index = (int)Material;
			if (index < 0 || index >= WallMaterialThreshold.Length)
			{
				return false;
			}
			int held = (Stock == null) ? 0 : Stock.Get(Material);
			return held >= WallMaterialThreshold[index];
		}

		// --- The refined half: what a yard makes, and out of what ------------------------------

		/// <summary>Registry keys for the three yards, in enum order: what a <c>Refines</c>
		/// attribute may write instead of the refined material's own key.</summary>
		public static readonly string[] YardKeys = new string[YardCount] { "sawyer", "mason", "smelter" };

		/// <summary>Player-facing names for the three yards, in enum order.</summary>
		public static readonly string[] YardNames = new string[YardCount] { "sawyer's yard", "mason's yard", "smelter" };

		/// <summary>What each yard turns raw stock INTO, in yard order.</summary>
		public static readonly KingdomMaterial[] YardMakes = new KingdomMaterial[YardCount]
		{
			KingdomMaterial.ShapedTimber,
			KingdomMaterial.ShapedStone,
			KingdomMaterial.WorkedMetal
		};

		/// <summary>
		/// What each yard EATS, richest acceptable stock first. A mason's yard will dress marble
		/// as readily as shale and the settlement would rather it did not, so the plain stock is
		/// listed first everywhere and the rarer alternative last &mdash; a yard reaches for the
		/// marble only when there is no ordinary stone to work.
		/// </summary>
		public static readonly KingdomMaterial[][] YardEats = new KingdomMaterial[YardCount][]
		{
			new KingdomMaterial[1] { KingdomMaterial.Timber },
			new KingdomMaterial[2] { KingdomMaterial.Stone, KingdomMaterial.Marble },
			new KingdomMaterial[1] { KingdomMaterial.Scrap }
		};

		/// <summary>Raw loads one refined unit is made of. Two: a yard is a place where half of
		/// what comes in leaves as spoil, sawdust, and slag, and the other half leaves better than
		/// it arrived.</summary>
		public const int RawPerRefined = 2;

		/// <summary>Effort one refined unit costs a crew. Dearer per unit than clearing a cell
		/// (<see cref="StandingEffort"/>) because the work is finer, and denominated in the same
		/// effort points so one day of one pair of hands means the same thing everywhere.</summary>
		public const int RefineEffortPerUnit = 15;

		/// <summary>
		/// Refined units one yard can finish in one visit however long the founder was away. The
		/// same bounded-consequence rule <see cref="MaxClearingHands"/> keeps on the clearing gang:
		/// a settlement makes more than it did, never a stockpile out of an absence.
		/// </summary>
		public const int MaxRefinedPerPass = 8;

		/// <summary>Whether a material is one a yard makes rather than one the ground gives up.
		/// </summary>
		public static bool IsRefined(KingdomMaterial Material)
		{
			return Material == KingdomMaterial.ShapedTimber || Material == KingdomMaterial.ShapedStone || Material == KingdomMaterial.WorkedMetal;
		}

		/// <summary>The yard that makes a refined material. False for anything raw.</summary>
		public static bool TryYardFor(KingdomMaterial Refined, out KingdomYard Yard)
		{
			Yard = KingdomYard.Sawyer;
			for (int i = 0; i < YardCount; i++)
			{
				if (YardMakes[i] == Refined)
				{
					Yard = (KingdomYard)i;
					return true;
				}
			}
			return false;
		}

		/// <summary>What a yard makes. <see cref="KingdomMaterial.ShapedTimber"/> for a value
		/// outside the enum, which no caller reads, because they all check the bool first.</summary>
		public static KingdomMaterial MadeAt(KingdomYard Yard)
		{
			int index = (int)Yard;
			return (index < 0 || index >= YardCount) ? KingdomMaterial.ShapedTimber : YardMakes[index];
		}

		/// <summary>The registry key of a yard, or empty for a value outside the enum.</summary>
		public static string YardKey(KingdomYard Yard)
		{
			int index = (int)Yard;
			return (index < 0 || index >= YardCount) ? "" : YardKeys[index];
		}

		/// <summary>The player-facing name of a yard, or empty for a value outside the enum.
		/// </summary>
		public static string YardName(KingdomYard Yard)
		{
			int index = (int)Yard;
			return (index < 0 || index >= YardCount) ? "" : YardNames[index];
		}

		/// <summary>
		/// Reads a <c>Refines</c> attribute. Accepts the yard's own key (<c>mason</c>) and the
		/// refined material's key (<c>shapedstone</c>, and its spaced spelling), because an author
		/// writing "what this building makes" and an author writing "what kind of yard this is"
		/// are both saying the same thing and neither should have to look up which spelling we
		/// wanted.
		/// </summary>
		/// <param name="Key">Text to read. Null, empty, a raw material, and an unknown word all
		/// fail.</param>
		/// <param name="Yard">Set on success.</param>
		public static bool TryParseYard(string Key, out KingdomYard Yard)
		{
			Yard = KingdomYard.Sawyer;
			if (string.IsNullOrEmpty(Key))
			{
				return false;
			}
			string trimmed = Key.Trim().ToLowerInvariant();
			for (int i = 0; i < YardCount; i++)
			{
				if (YardKeys[i] == trimmed)
				{
					Yard = (KingdomYard)i;
					return true;
				}
			}
			if (TryParseMaterial(trimmed, out var material) && IsRefined(material))
			{
				return TryYardFor(material, out Yard);
			}
			return false;
		}

		/// <summary>
		/// Which raw material a yard would reach for out of the stock it can see, and how many
		/// refined units that stock could yield. A yard with less than <see cref="RawPerRefined"/>
		/// of everything it eats has nothing to work on, which is a thing it says out loud rather
		/// than a pass that quietly does nothing (STANDARDS 7b).
		/// </summary>
		/// <param name="Yard">Which yard.</param>
		/// <param name="Stock">What the stockpiles hold. Null reads as empty.</param>
		/// <param name="Raw">Set on success to the stock it would eat.</param>
		/// <returns>Refined units that stock covers, or zero when there is nothing to work.</returns>
		public static int RefinableFrom(KingdomYard Yard, KingdomMaterialTally Stock, out KingdomMaterial Raw)
		{
			Raw = KingdomMaterial.Timber;
			int index = (int)Yard;
			if (index < 0 || index >= YardCount || Stock == null)
			{
				return 0;
			}
			KingdomMaterial[] eats = YardEats[index];
			for (int i = 0; i < eats.Length; i++)
			{
				int units = Stock.Get(eats[i]) / RawPerRefined;
				if (units > 0)
				{
					Raw = eats[i];
					return units;
				}
			}
			return 0;
		}

		/// <summary>
		/// Refined units a crew finishes in the days since it last worked: the effort those hands
		/// put in, divided by what one unit costs, capped at <see cref="MaxRefinedPerPass"/> and at
		/// what the raw stock covers. Zero hands make nothing, which is the idle case and is said
		/// once by the caller rather than being a silent nothing.
		/// </summary>
		/// <param name="Crew">Settlers actually standing in the yard this pass.</param>
		/// <param name="Days">Days since the yard last worked, already capped by the absence rule.
		/// </param>
		/// <param name="Capability">Who those settlers are, as a percentage
		/// (<see cref="CrewCapability"/>). 100 is an ordinary pair of hands.</param>
		/// <param name="RefinableUnits">What the raw stock covers, from
		/// <see cref="RefinableFrom"/>.</param>
		public static int RefinedThisPass(int Crew, int Days, int Capability, int RefinableUnits)
		{
			if (Crew <= 0 || Days <= 0 || RefinableUnits <= 0)
			{
				return 0;
			}
			int capability = (Capability > 0) ? Capability : 0;
			int effort = Crew * Days * EffortPerHandPerDay * capability / 100;
			int units = effort / RefineEffortPerUnit;
			if (units > MaxRefinedPerPass)
			{
				units = MaxRefinedPerPass;
			}
			return (units > RefinableUnits) ? RefinableUnits : units;
		}

		/// <summary>Why a yard is or is not shaping anything for the days it was just handed.
		/// <see cref="YardStall.Working"/> is the only verdict that produces.</summary>
		public enum YardStall
		{
			/// <summary>A crew is standing there and there is stock to work.</summary>
			Working,

			/// <summary>Nobody is at the bench. The days are still spent -- an empty yard does not
			/// owe its labour to whoever staffs it next -- and they buy nothing.</summary>
			Unstaffed,

			/// <summary>A crew is there and the stockpiles are empty.</summary>
			NoStock
		}

		/// <summary>
		/// Which of the two ways a yard can stand idle this is, if either.
		/// <para>
		/// Split out from the caller so the ORDER of the two gates is a thing a test can hold:
		/// staffing is asked first, because "nobody is here" is the truer answer than "there is
		/// nothing to work" when both are true, and the founder can only act on one of them at a
		/// time.
		/// </para>
		/// </summary>
		/// <param name="Staffed">Whether the staffing pass drew any crew for it.</param>
		/// <param name="Crew">Hands the pass actually put there, after effectiveness.</param>
		/// <param name="RefinableUnits">What the raw stock covers
		/// (<see cref="RefinableFrom"/>).</param>
		public static YardStall AssessYard(bool Staffed, int Crew, int RefinableUnits)
		{
			if (!Staffed || Crew <= 0)
			{
				return YardStall.Unstaffed;
			}
			if (RefinableUnits <= 0)
			{
				return YardStall.NoStock;
			}
			return YardStall.Working;
		}

		/// <summary>
		/// What a stalled yard says, once, where the founder will see it (STANDARDS 7b). Null for
		/// <see cref="YardStall.Working"/>, which is the caller's signal to unsay whatever it
		/// said last.
		/// <para>
		/// Both stalls name the yard and the city, because the settlement-wide idle-works line
		/// reports a COUNT and never says which bench it was &mdash; and "three works stand idle"
		/// is not a thing a founder can act on.
		/// </para>
		/// </summary>
		public static string YardStallLine(YardStall Stall, KingdomYard Yard, string SeatName)
		{
			string yard = YardName(Yard);
			string place = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			switch (Stall)
			{
			case YardStall.Unstaffed:
				return "The " + yard + " of " + place + " stands with nobody at the bench. Nothing is being shaped there.";
			case YardStall.NoStock:
				return "The " + yard + " of " + place + " stands over an empty bench. There is nothing in the stockpiles for it to work.";
			default:
				return null;
			}
		}

		/// <summary>Raw loads a run of refining eats. Always exactly what it made, times
		/// <see cref="RawPerRefined"/>: nothing is refined out of nothing.</summary>
		public static int RawSpentFor(int RefinedUnits)
		{
			return (RefinedUnits > 0) ? (RefinedUnits * RawPerRefined) : 0;
		}

		// --- Crews have capability, and it is read off who they are ---------------------------

		/// <summary>Which of a settler's numbers a yard's work is done with. Sawing and dressing
		/// stone are muscle; a furnace is a machine somebody has to understand.</summary>
		public static KingdomCapability CapabilityFor(KingdomYard Yard)
		{
			return (Yard == KingdomYard.Smelter) ? KingdomCapability.Mind : KingdomCapability.Muscle;
		}

		/// <summary>
		/// The stat an ordinary person has. Vanilla's own humanoid rolls <c>14,1d3</c> on every
		/// attribute (<c>BaseHumanoid</c> in the game's own Creatures.xml), so sixteen is the
		/// middle of what walks up the road, and a crew of ordinary people works at exactly 100.
		/// </summary>
		public const int BaselineStat = 16;

		/// <summary>Percentage points one point of the relevant stat is worth.</summary>
		public const int CapabilityPerPoint = 5;

		/// <summary>Floor on capability. Nobody is useless, and a settlement that has only weak
		/// hands still gets its beams cut, slowly.</summary>
		public const int MinCapabilityPercent = 50;

		/// <summary>Ceiling on capability. The strong settler is worth having and is never worth
		/// three ordinary ones, because the yard is the bottleneck and not the arm.</summary>
		public const int MaxCapabilityPercent = 150;

		/// <summary>What one stat value is worth, as a percentage of an ordinary pair of hands.
		/// </summary>
		public static int CapabilityPercent(int Stat)
		{
			int percent = 100 + (Stat - BaselineStat) * CapabilityPerPoint;
			if (percent < MinCapabilityPercent)
			{
				return MinCapabilityPercent;
			}
			return (percent > MaxCapabilityPercent) ? MaxCapabilityPercent : percent;
		}

		/// <summary>
		/// What a crew is worth at one yard's work, read off the people themselves. The founder
		/// assigns nobody: the settlement's own hands are what they are, and a city of scribes
		/// smelts better than it saws.
		/// </summary>
		/// <param name="Yard">The work being done.</param>
		/// <param name="Strength">The crew's Strength, averaged. Zero and negative read as
		/// <see cref="BaselineStat"/>, so a caller that could not read the people gets an ordinary
		/// crew rather than a punished one.</param>
		/// <param name="Intelligence">The crew's Intelligence, averaged. Same rule.</param>
		public static int CrewCapability(KingdomYard Yard, int Strength, int Intelligence)
		{
			int stat = (CapabilityFor(Yard) == KingdomCapability.Mind) ? Intelligence : Strength;
			return CapabilityPercent((stat > 0) ? stat : BaselineStat);
		}

		/// <summary>
		/// The average of a set of stat readings, or <see cref="BaselineStat"/> when there is
		/// nothing to read. Rounded down, because a crew is only as quick as its slowest half.
		/// </summary>
		public static int AverageStat(IList<int> Values)
		{
			if (Values == null || Values.Count == 0)
			{
				return BaselineStat;
			}
			int total = 0;
			for (int i = 0; i < Values.Count; i++)
			{
				total += Values[i];
			}
			return total / Values.Count;
		}

		/// <summary>One word for a crew's quality, for the line the founder reads. Never null.
		/// </summary>
		public static string CapabilityWord(int Percent)
		{
			if (Percent >= 120)
			{
				return "deft";
			}
			if (Percent <= 80)
			{
				return "slow";
			}
			return "steady";
		}

		// --- Bits: vanilla's own tinkering stock, priced into high-craft designs ---------------

		/// <summary>
		/// Bit tiers, which are the game's own and not ours: <c>BitType.Init</c> files twelve bit
		/// colours under nine levels, and <c>BitType.GetBitTier</c> is the map from colour to
		/// level. A cost is written in those levels &mdash; <c>Bits="0034"</c> is two of the
		/// commonest and one each of tier three and four &mdash; which is exactly how vanilla's own
		/// <c>TinkerItem Bits</c> attribute is written.
		/// </summary>
		public const int BitTierCount = 9;

		/// <summary>
		/// What each tier is called, in tier order, taken from the descriptions
		/// <c>BitType.Init</c> gives them. Tier zero holds four colours at once (scrap power
		/// systems, crystal, metal, electronics), so it is named for the thing they have in
		/// common: it is scrap, and the settlement does not care which.
		/// </summary>
		public static readonly string[] BitTierNames = new string[BitTierCount]
		{
			"scrap",
			"phasic power systems",
			"flawless crystal",
			"pure alloy",
			"pristine electronics",
			"nanomaterials",
			"photonics",
			"AI microcontrollers",
			"metacrystal"
		};

		/// <summary>
		/// The twelve bit colours in vanilla's own order (<c>BitType.Init</c>), used to read a cost
		/// written in colours rather than tiers. Kept here rather than reached for through the
		/// engine so these rules stay engine-free and testable; the tiers are asserted against
		/// <c>BitType.GetBitTier</c>'s own table by the tests.
		/// </summary>
		public const string BitColours = "RGBCrgbcKWYM";

		/// <summary>The tier of each colour in <see cref="BitColours"/>, same order.</summary>
		public static readonly int[] BitColourTiers = new int[12] { 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 };

		/// <summary>The name of one bit tier, or empty for a tier outside the ladder.</summary>
		public static string BitTierName(int Tier)
		{
			return (Tier < 0 || Tier >= BitTierCount) ? "" : BitTierNames[Tier];
		}

		/// <summary>
		/// The tier one bit colour belongs to, mirroring <c>BitType.GetBitTier</c>. False for any
		/// character that is not one of the game's twelve.
		/// </summary>
		public static bool TryBitTier(char Colour, out int Tier)
		{
			Tier = 0;
			int at = BitColours.IndexOf(Colour);
			if (at < 0)
			{
				return false;
			}
			Tier = BitColourTiers[at];
			return true;
		}

		/// <summary>
		/// Reads a bit cost: <c>"0034"</c>, or the same thing written in the game's own colours,
		/// <c>"BBbc"</c>. Whitespace and commas anywhere are ignored, so <c>"00, 3, 4"</c> reads
		/// the same as <c>"0034"</c>.
		/// <para>
		/// An absent or empty attribute is not an error: it parses to an empty cost, which is what
		/// every design in the catalogue costs in bits today and what every third-party design that
		/// never heard of bits goes on costing.
		/// </para>
		/// </summary>
		/// <param name="Text">The attribute's value, or null.</param>
		/// <param name="Cost">Always set to a tally, empty when this returns false.</param>
		/// <param name="Error">Null on success, else a log-facing reason naming the offending
		/// character. The whole attribute is rejected; nothing is half-parsed.</param>
		public static bool TryParseBitCost(string Text, out KingdomBitTally Cost, out string Error)
		{
			Cost = new KingdomBitTally();
			Error = null;
			if (string.IsNullOrEmpty(Text) || Text.Trim().Length == 0)
			{
				return true;
			}
			for (int i = 0; i < Text.Length; i++)
			{
				char c = Text[i];
				if (c == ' ' || c == '\t' || c == ',' || c == '\r' || c == '\n')
				{
					continue;
				}
				if (c >= '0' && c <= '8')
				{
					Cost.Add(c - '0', 1);
					continue;
				}
				if (TryBitTier(c, out var tier))
				{
					Cost.Add(tier, 1);
					continue;
				}
				Error = "\"" + c + "\" is not a bit tier (0-8) or one of the game's own bit colours (" + BitColours + ")";
				Cost = new KingdomBitTally();
				return false;
			}
			return true;
		}

		/// <summary>Whether a bit stock holds at least every bit a cost asks for. A null or empty
		/// cost is always covered, including by an empty locker.</summary>
		public static bool CoversBits(KingdomBitTally Stock, KingdomBitTally Cost)
		{
			if (Cost == null || Cost.IsEmpty())
			{
				return true;
			}
			for (int i = 0; i < BitTierCount; i++)
			{
				int held = (Stock == null) ? 0 : Stock.Get(i);
				if (held < Cost.Get(i))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>What a bit stock is short of a cost, per tier. Empty when
		/// <see cref="CoversBits"/> is true.</summary>
		public static KingdomBitTally MissingBits(KingdomBitTally Stock, KingdomBitTally Cost)
		{
			KingdomBitTally missing = new KingdomBitTally();
			if (Cost == null)
			{
				return missing;
			}
			for (int i = 0; i < BitTierCount; i++)
			{
				int held = (Stock == null) ? 0 : Stock.Get(i);
				int shortfall = Cost.Get(i) - held;
				if (shortfall > 0)
				{
					missing.Set(i, shortfall);
				}
			}
			return missing;
		}

		// --- Exotic materials: rare finds, and the only things an XL special is short of -------

		/// <summary>Number of values in <see cref="KingdomExotic"/>.</summary>
		public const int ExoticCount = 4;

		/// <summary>Registry keys, in enum order: what an <c>Exotics</c> attribute writes.
		/// </summary>
		public static readonly string[] ExoticKeys = new string[ExoticCount] { "ingot", "silver", "gold", "gem" };

		/// <summary>Player-facing names, singular, in enum order. Every one of them is a real
		/// vanilla item somebody carried home: bronze ingots, silver and gold nuggets, and the
		/// rough gemstones that come out of the same ground.</summary>
		public static readonly string[] ExoticNames = new string[ExoticCount] { "bronze ingot", "silver nugget", "gold nugget", "rough gemstone" };

		/// <summary>The same names in the plural, for a cost of more than one.</summary>
		public static readonly string[] ExoticPlurals = new string[ExoticCount] { "bronze ingots", "silver nuggets", "gold nuggets", "rough gemstones" };

		/// <summary>The registry key of an exotic, or empty for a value outside the enum.
		/// </summary>
		public static string ExoticKey(KingdomExotic Exotic)
		{
			int index = (int)Exotic;
			return (index < 0 || index >= ExoticCount) ? "" : ExoticKeys[index];
		}

		/// <summary>The name of an exotic, pluralised for a count of more than one.</summary>
		public static string ExoticName(KingdomExotic Exotic, int Units = 1)
		{
			int index = (int)Exotic;
			if (index < 0 || index >= ExoticCount)
			{
				return "";
			}
			return (Units == 1) ? ExoticNames[index] : ExoticPlurals[index];
		}

		/// <summary>
		/// Reads an exotic key. Case-insensitive, whitespace-tolerant, and it accepts the item's
		/// own name as the game writes it, because an author reading "gold nugget" off the ground
		/// should be able to write that.
		/// </summary>
		public static bool TryParseExotic(string Key, out KingdomExotic Exotic)
		{
			Exotic = KingdomExotic.Ingot;
			if (string.IsNullOrEmpty(Key))
			{
				return false;
			}
			string trimmed = Key.Trim().ToLowerInvariant();
			for (int i = 0; i < ExoticCount; i++)
			{
				if (ExoticKeys[i] == trimmed || ExoticNames[i] == trimmed || ExoticPlurals[i] == trimmed)
				{
					Exotic = (KingdomExotic)i;
					return true;
				}
			}
			switch (trimmed)
			{
			case "bronze":
			case "bronzeingot":
				Exotic = KingdomExotic.Ingot;
				return true;
			case "silvernugget":
				Exotic = KingdomExotic.Silver;
				return true;
			case "goldnugget":
				Exotic = KingdomExotic.Gold;
				return true;
			case "gemstone":
			case "gems":
				Exotic = KingdomExotic.Gem;
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// Reads an exotic cost: <c>"gold:2, gem:1"</c>. Same grammar and same forgiveness as
		/// <see cref="TryParseMaterialCost"/>, for the same reason: an absent attribute is a design
		/// that wants no rare find, which is nearly all of them.
		/// </summary>
		public static bool TryParseExoticCost(string Text, out KingdomExoticTally Cost, out string Error)
		{
			Cost = new KingdomExoticTally();
			Error = null;
			if (string.IsNullOrEmpty(Text) || Text.Trim().Length == 0)
			{
				return true;
			}
			string[] terms = Text.Split(',');
			bool[] seen = new bool[ExoticCount];
			for (int i = 0; i < terms.Length; i++)
			{
				string term = terms[i].Trim();
				if (term.Length == 0)
				{
					Error = "empty term in exotic cost \"" + Text + "\"";
					Cost = new KingdomExoticTally();
					return false;
				}
				int split = term.LastIndexOf(':');
				if (split <= 0 || split == term.Length - 1)
				{
					Error = "exotic term \"" + term + "\" is not of the form exotic:units";
					Cost = new KingdomExoticTally();
					return false;
				}
				if (!TryParseExotic(term.Substring(0, split), out var exotic))
				{
					Error = "unknown exotic \"" + term.Substring(0, split).Trim() + "\"";
					Cost = new KingdomExoticTally();
					return false;
				}
				if (!int.TryParse(term.Substring(split + 1).Trim(), out var units) || units <= 0)
				{
					Error = "exotic \"" + ExoticKey(exotic) + "\" needs a positive whole number of units";
					Cost = new KingdomExoticTally();
					return false;
				}
				if (seen[(int)exotic])
				{
					Error = "exotic \"" + ExoticKey(exotic) + "\" is named twice";
					Cost = new KingdomExoticTally();
					return false;
				}
				seen[(int)exotic] = true;
				Cost.Set(exotic, units);
			}
			return true;
		}

		/// <summary>Whether a stock of rare finds covers a cost. A null or empty cost always is.
		/// </summary>
		public static bool CoversExotics(KingdomExoticTally Stock, KingdomExoticTally Cost)
		{
			if (Cost == null || Cost.IsEmpty())
			{
				return true;
			}
			for (int i = 0; i < ExoticCount; i++)
			{
				KingdomExotic exotic = (KingdomExotic)i;
				int held = (Stock == null) ? 0 : Stock.Get(exotic);
				if (held < Cost.Get(exotic))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>What a stock of rare finds is short of a cost. Empty when
		/// <see cref="CoversExotics"/> is true.</summary>
		public static KingdomExoticTally MissingExotics(KingdomExoticTally Stock, KingdomExoticTally Cost)
		{
			KingdomExoticTally missing = new KingdomExoticTally();
			if (Cost == null)
			{
				return missing;
			}
			for (int i = 0; i < ExoticCount; i++)
			{
				KingdomExotic exotic = (KingdomExotic)i;
				int held = (Stock == null) ? 0 : Stock.Get(exotic);
				int shortfall = Cost.Get(exotic) - held;
				if (shortfall > 0)
				{
					missing.Set(exotic, shortfall);
				}
			}
			return missing;
		}

		// --- Infrastructure gates construction ------------------------------------------------

		/// <summary>
		/// One yard as the gate needs to see it. Standing is the building; staffed is whether
		/// anybody is in it this pass; headed is whether the office layer has seated a notable over
		/// it. All three are read, never assigned.
		/// </summary>
		public struct KingdomYardStanding
		{
			public KingdomYard Yard;

			/// <summary>A finished building of this kind stands on the settlement's ground.
			/// </summary>
			public bool Standing;

			/// <summary>Somebody is working it this pass.</summary>
			public bool Staffed;

			/// <summary>A named notable heads it (Addendum 6's office rule).</summary>
			public bool Headed;

			public KingdomYardStanding(KingdomYard Yard, bool Standing, bool Staffed, bool Headed)
			{
				this.Yard = Yard;
				this.Standing = Standing;
				this.Staffed = Staffed;
				this.Headed = Headed;
			}
		}

		/// <summary>Whether a design of this size needs a yard standing behind it at all. Small and
		/// middling works are raised by whoever is free; the big ones are not.</summary>
		public static bool RequiresYard(KingdomPlotRules.PlotSize Size)
		{
			return Size == KingdomPlotRules.PlotSize.Large || Size == KingdomPlotRules.PlotSize.Huge;
		}

		/// <summary>Whether a design of this size needs its yard HEADED as well as staffed. Only
		/// the grand ones: a great work is led by somebody with a name (Addendum 6).</summary>
		public static bool RequiresHeadedYard(KingdomPlotRules.PlotSize Size)
		{
			return Size == KingdomPlotRules.PlotSize.Huge;
		}

		/// <summary>
		/// Which yards a design's material cost implies, in yard order.
		/// <para>
		/// Every refined material a design names says its own yard outright: shaped stone is a
		/// mason's yard, and there is no other way to have any. A design that names none is judged
		/// by what it is mostly made OF &mdash; a temple of forty stone wants a mason's yard behind
		/// it whether or not the stone was dressed &mdash; which is what makes the gate reach
		/// designs written before yards existed without a single attribute being added to them.
		/// A design made of mud and brush alone implies no yard at all, and is raised by hands.
		/// </para>
		/// </summary>
		/// <param name="Size">The design's plot tier. Anything under Large implies nothing.</param>
		/// <param name="Cost">The design's material cost. Null and empty imply nothing.</param>
		public static List<KingdomYard> YardsFor(KingdomPlotRules.PlotSize Size, KingdomMaterialTally Cost)
		{
			List<KingdomYard> yards = new List<KingdomYard>();
			if (!RequiresYard(Size) || Cost == null || Cost.IsEmpty())
			{
				return yards;
			}
			for (int i = 0; i < YardCount; i++)
			{
				if (Cost.Get(YardMakes[i]) > 0)
				{
					yards.Add((KingdomYard)i);
				}
			}
			if (yards.Count == 0 && TryDominantYard(Cost, out var dominant))
			{
				yards.Add(dominant);
			}
			return yards;
		}

		/// <summary>
		/// The yard whose stock a cost is mostly made of: timber to the sawyer, stone and marble to
		/// the mason, scrap to the smelter, with each yard's own refined output counted alongside
		/// the raw it came from. Ties go to the earlier yard in
		/// <see cref="KingdomYard"/> order, which is a rule rather than an accident so the same
		/// cost always names the same yard.
		/// </summary>
		/// <returns>False for a cost of nothing but mud and brush, which no yard touches.</returns>
		public static bool TryDominantYard(KingdomMaterialTally Cost, out KingdomYard Yard)
		{
			Yard = KingdomYard.Sawyer;
			if (Cost == null)
			{
				return false;
			}
			int best = 0;
			bool found = false;
			for (int i = 0; i < YardCount; i++)
			{
				int units = Cost.Get(YardMakes[i]);
				KingdomMaterial[] eats = YardEats[i];
				for (int j = 0; j < eats.Length; j++)
				{
					units += Cost.Get(eats[j]);
				}
				if (units > best)
				{
					best = units;
					Yard = (KingdomYard)i;
					found = true;
				}
			}
			return found;
		}

		/// <summary>What one yard's standing is in a list of them, or a yard that stands nowhere.
		/// </summary>
		public static KingdomYardStanding StandingOf(IList<KingdomYardStanding> Yards, KingdomYard Yard)
		{
			if (Yards != null)
			{
				for (int i = 0; i < Yards.Count; i++)
				{
					if (Yards[i].Yard == Yard)
					{
						return Yards[i];
					}
				}
			}
			return new KingdomYardStanding(Yard, Standing: false, Staffed: false, Headed: false);
		}

		/// <summary>
		/// Whether the settlement's infrastructure will carry this design, and what is missing when
		/// it will not.
		/// <para>
		/// The law of Addendum 7 in one method: a large work wants the relevant yard standing and
		/// staffed, and a grand one wants it headed as well. Every refusal names the yard and the
		/// state it is in, once, where the founder is standing (STANDARDS 7b) &mdash; "there is no
		/// mason's yard" and "the mason's yard stands idle" are different problems with different
		/// answers, and a founder told only "you cannot build this" has been told nothing.
		/// </para>
		/// </summary>
		/// <param name="Size">The design's plot tier.</param>
		/// <param name="Cost">The design's material cost.</param>
		/// <param name="Yards">What the settlement's yards are doing. Null reads as none standing.
		/// </param>
		/// <param name="DesignName">What the founder calls the design, for the sentence. Null is
		/// accepted and reads as "the work".</param>
		/// <param name="Refusal">Null when this returns true, else the founder-facing reason.
		/// </param>
		public static bool AllowsBuild(KingdomPlotRules.PlotSize Size, KingdomMaterialTally Cost, IList<KingdomYardStanding> Yards, string DesignName, out string Refusal)
		{
			Refusal = null;
			List<KingdomYard> wanted = YardsFor(Size, Cost);
			if (wanted.Count == 0)
			{
				return true;
			}
			string name = string.IsNullOrEmpty(DesignName) ? "the work" : ("the " + DesignName);
			bool headed = RequiresHeadedYard(Size);
			for (int i = 0; i < wanted.Count; i++)
			{
				KingdomYardStanding standing = StandingOf(Yards, wanted[i]);
				string yard = YardName(wanted[i]);
				if (!standing.Standing)
				{
					Refusal = "A work of this size is not raised by willing hands alone. " + Capitalise(name)
						+ " wants {{C|a " + yard + "}} standing in the settlement, and there is none. Raise one first.";
					return false;
				}
				if (!standing.Staffed)
				{
					Refusal = Capitalise(name) + " wants the {{C|" + yard
						+ "}}, and it stands idle. Stand a settler down off the water or another work and it will be worked again.";
					return false;
				}
				if (headed && !standing.Headed)
				{
					Refusal = Capitalise(name) + " is a great work, and a great work is led. The {{C|" + yard
						+ "}} wants somebody named over it before the settlement will attempt this.";
					return false;
				}
			}
			return true;
		}

		/// <summary>One line for the founder about what a design's size will ask of the yards,
		/// before they order it. Null when the design asks nothing.</summary>
		public static string YardRequirementLine(KingdomPlotRules.PlotSize Size, KingdomMaterialTally Cost)
		{
			List<KingdomYard> wanted = YardsFor(Size, Cost);
			if (wanted.Count == 0)
			{
				return null;
			}
			List<string> names = new List<string>();
			for (int i = 0; i < wanted.Count; i++)
			{
				names.Add("a " + YardName(wanted[i]));
			}
			string list = JoinPhrases(names);
			return RequiresHeadedYard(Size)
				? ("A work this size wants " + list + ", worked and headed.")
				: ("A work this size wants " + list + ", worked.");
		}

		// --- Wear, and what mending it costs ---------------------------------------------------

		/// <summary>
		/// The most wear a work ever carries. Damage runs a work down and never stops it: a
		/// settlement that comes home to a burnt mill finds it turning slowly, not gone. Nothing
		/// here is ever reached by the calendar &mdash; wear comes from events (a raid, hard
		/// running, temperamental certified tech) and from nothing else. Time is labour, never
		/// decay.
		/// </summary>
		public const int MaxWearPercent = 60;

		/// <summary>Wear a work carries after an event adds to what it already had, clamped both
		/// ways. Nothing ever wears past <see cref="MaxWearPercent"/>.</summary>
		public static int AddWear(int Wear, int Added)
		{
			int total = ((Wear > 0) ? Wear : 0) + ((Added > 0) ? Added : 0);
			return (total > MaxWearPercent) ? MaxWearPercent : total;
		}

		/// <summary>How well a worn work runs, as a percentage of what it does whole. Never zero:
		/// the floor is <c>100 - </c><see cref="MaxWearPercent"/>.</summary>
		public static int ConditionPercent(int Wear)
		{
			int wear = (Wear > 0) ? Wear : 0;
			if (wear > MaxWearPercent)
			{
				wear = MaxWearPercent;
			}
			return 100 - wear;
		}

		/// <summary>One word for the state of a work, for the line the founder reads. Never null.
		/// </summary>
		public static string ConditionWord(int Wear)
		{
			if (Wear <= 0)
			{
				return "sound";
			}
			if (Wear < 20)
			{
				return "knocked about";
			}
			return (Wear < 40) ? "badly used" : "half-wrecked";
		}

		/// <summary>
		/// What mending a work costs in material: the share of what it was built from that the wear
		/// stands for, and never the whole building again. A design built for nothing is mended for
		/// nothing, which is honest &mdash; there is nothing in a mud wall to replace.
		/// </summary>
		/// <param name="BuildCost">What the design cost to raise.</param>
		/// <param name="Wear">How worn it is, as a percentage.</param>
		public static KingdomMaterialTally RepairCost(KingdomMaterialTally BuildCost, int Wear)
		{
			if (BuildCost == null || Wear <= 0)
			{
				return new KingdomMaterialTally();
			}
			int wear = (Wear > MaxWearPercent) ? MaxWearPercent : Wear;
			return BuildCost.Scaled(wear);
		}

		/// <summary>
		/// What mending a work costs in bits: the same share of what its design was priced in.
		/// This is the certified-tech half of Addendum 7 &mdash; a temperamental machine is mended
		/// with the same stock it was built from, and a settlement that has no bits has a machine
		/// running at reduced effect and a reason it can read.
		/// </summary>
		public static KingdomBitTally RepairBits(KingdomBitTally BuildBits, int Wear)
		{
			if (BuildBits == null || Wear <= 0)
			{
				return new KingdomBitTally();
			}
			int wear = (Wear > MaxWearPercent) ? MaxWearPercent : Wear;
			return BuildBits.Scaled(wear);
		}

		/// <summary>Effort mending a work costs, from what has to be put back into it. Always at
		/// least one for any wear at all: nothing is mended for free.</summary>
		public static int RepairEffort(int MaterialUnits, int Wear)
		{
			if (Wear <= 0)
			{
				return 0;
			}
			int units = (MaterialUnits > 0) ? MaterialUnits : 0;
			int effort = StrikeBaseEffort / 2 + units * StrikeEffortPerUnit;
			return (effort < 1) ? 1 : effort;
		}

		/// <summary>
		/// The one line a damaged work gets, said once when the damage happens and not again
		/// (STANDARDS 7b). Null for a work that is sound, so a caller never announces nothing.
		/// </summary>
		public static string DamageLine(string Name, int Wear)
		{
			if (Wear <= 0)
			{
				return null;
			}
			string name = string.IsNullOrEmpty(Name) ? "a work" : ("the " + Name);
			return "{{r|" + Capitalise(name) + " is " + ConditionWord(Wear) + ", and runs at " + ConditionPercent(Wear)
				+ " parts in a hundred until somebody mends it. It will not fail, and it will not mend itself.}}";
		}

		// --- Small shared helpers --------------------------------------------------------------

		/// <summary>
		/// Joins phrases the way a person would: "a", "a and b", "a, b and c". Null for an empty
		/// list, so every caller has one thing to test rather than two, and so a tally with nothing
		/// in it never produces a sentence about nothing.
		/// </summary>
		public static string JoinPhrases(List<string> Parts)
		{
			if (Parts == null || Parts.Count == 0)
			{
				return null;
			}
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < Parts.Count; i++)
			{
				if (i > 0)
				{
					text.Append((i == Parts.Count - 1) ? " and " : ", ");
				}
				text.Append(Parts[i]);
			}
			return text.ToString();
		}

		/// <summary>The same sentence with its first letter raised. Left alone when it already is,
		/// and when there is nothing to raise.</summary>
		private static string Capitalise(string Text)
		{
			if (string.IsNullOrEmpty(Text))
			{
				return Text;
			}
			return char.ToUpperInvariant(Text[0]) + Text.Substring(1);
		}
	}
}
