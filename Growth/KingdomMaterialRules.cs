using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement's material vocabulary. Six kinds, and no seventh: every one of them is
	/// something that already stands in a Qud zone and can be carried away from it &mdash; mud
	/// from turned ground, brush cut and retted into canvas and cord, timber from trees, stone
	/// from rock walls and boulders, marble from a seam, scrap from a ruin. Nothing here is minted;
	/// clearance, salvage, and trade are the only three doors materials come through.
	/// </summary>
	public enum KingdomMaterial
	{
		Mud = 0,
		Brush = 1,
		Timber = 2,
		Stone = 3,
		Marble = 4,
		Scrap = 5
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
			if (parts.Count == 0)
			{
				return null;
			}
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < parts.Count; i++)
			{
				if (i > 0)
				{
					text.Append((i == parts.Count - 1) ? " and " : ", ");
				}
				text.Append(parts[i]);
			}
			return text.ToString();
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
		/// <c>KingdomMaterialRulesTests</c> so a seventh material cannot be added without the
		/// tallies growing with it.</summary>
		public const int MaterialCount = 6;

		/// <summary>Number of values in <see cref="KingdomStanding"/>.</summary>
		public const int StandingCount = 7;

		/// <summary>
		/// Registry keys, in enum order: what third-party XML writes in a <c>Materials</c>
		/// attribute and what <see cref="TryParseMaterial"/> accepts.
		/// </summary>
		public static readonly string[] MaterialKeys = new string[MaterialCount] { "mud", "brush", "timber", "stone", "marble", "scrap" };

		/// <summary>Player-facing names, in enum order. Lowercase, in the game's register.</summary>
		public static readonly string[] MaterialNames = new string[MaterialCount] { "mud", "brush", "timber", "cut stone", "marble", "scrap metal" };

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
		/// out of hand-written XML, and it accepts the two aliases the prose uses for keys that do
		/// not read the same way in a sentence: "scrap metal" for <c>scrap</c>, and "canvas" for
		/// <c>brush</c>, which is what brush becomes once it has been cut and retted.
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
		/// and the ground is always there.
		/// </summary>
		public static readonly int[] WallMaterialThreshold = new int[MaterialCount] { 0, 4, 8, 10, 14, 10 };

		/// <summary>Materials in the order a settlement would rather build in, richest first.
		/// Mud is last and is the floor nothing ever falls through.</summary>
		public static readonly KingdomMaterial[] WallMaterialPreference = new KingdomMaterial[MaterialCount]
		{
			KingdomMaterial.Marble,
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
	}
}
