using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
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

	}
}
