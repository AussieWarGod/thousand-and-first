using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Every rule the material economy can settle without a running game: what a cell yields
	/// when it is cleared, what clearing it costs in crew, what a design's material cost parses
	/// to, what a struck building gives back, and which wall material a settlement's own
	/// quarrying has earned it. The engine-coupled half &mdash; reading real objects, destroying
	/// what stood there, putting real items in a real stockpile &mdash; is
	/// <c>KingdomMaterials</c>, in the same folder.
	/// </summary>
	public static partial class KingdomMaterialRules
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

	}
}
