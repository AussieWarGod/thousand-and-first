using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
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

	}
}
