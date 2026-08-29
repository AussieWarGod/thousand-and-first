using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Pure choice, idempotence, readback, and truthful-prose rules for liquid pieces.</summary>
	internal static class KingdomLiquidConfigurationRules
	{
		// UI order groups cap, four ends, two straights, four corners, four tees, then cross.
		private static readonly int[] LegalMasks = new int[16]
		{
			0, 1, 2, 4, 8, 3, 12, 5, 9, 6, 10, 7, 11, 13, 14, 15
		};

		internal static bool TryMaskForChoice(int Choice, out int Mask)
		{
			Mask = 0;
			if (Choice < 0 || Choice >= LegalMasks.Length) return false;
			Mask = LegalMasks[Choice];
			return true;
		}

		/// <summary>Plans a write. Same semantic mask preserves exact old text and reports no change.</summary>
		internal static bool TryPlanDeclaration(string Current, int Choice, bool Authorized,
			out string Next, out int Mask, out bool Changed, out string Failure)
		{
			Next = Current;
			Mask = 0;
			Changed = false;
			Failure = null;
			if (!Authorized)
			{
				Failure = "Only the player may configure a laid liquid piece.";
				return false;
			}
			if (!TryMaskForChoice(Choice, out Mask))
			{
				Failure = "That liquid-piece form is not legal.";
				return false;
			}
			int currentMask;
			if (KingdomNetworkRules.TryParseJoins(Current, out currentMask) && currentMask == Mask)
				return true;
			if (!KingdomLiquidVisualRules.TryCanonicalJoins(Mask, out Next))
			{
				Failure = "That liquid-piece declaration cannot be written.";
				return false;
			}
			Changed = true;
			return true;
		}

		internal static bool DeclarationReadsBack(string Held, int ExpectedMask)
		{
			int observed;
			return KingdomNetworkRules.TryParseJoins(Held, out observed)
				&& observed == ExpectedMask;
		}

		internal static string[] Options(bool Brine)
		{
			string[] result = new string[LegalMasks.Length];
			for (int i = 0; i < result.Length; i++)
			{
				KingdomLiquidVisualCue cue;
				KingdomLiquidVisualRules.TryCue(LegalMasks[i], Brine, out cue);
				result[i] = KingdomLiquidVisualRules.FormName(LegalMasks[i])
					+ "  [map sign " + (char)cue.Glyph + "]";
			}
			return result;
		}

		internal static string Status(string Liquid, string Joins, bool Tap)
		{
			bool brine = KingdomLiquidVisualRules.IsBrine(Liquid);
			KingdomLiquidVisualCue cue;
			bool valid = KingdomLiquidVisualRules.TryCue(Joins, brine, out cue);
			string kind = Tap ? "tap" : "main";
			string named = LiquidName(Liquid);
			if (!valid)
				return "\n{{rules|" + named + " " + kind
					+ ": unreadable face declaration; fail-closed cap; joins nothing.}}";
			string arrow = cue.Form == KingdomLiquidForm.End
				? " The arrow points at the declared join, not a flow direction." : "";
			return "\n{{rules|" + named + " " + kind + ": frozen "
				+ KingdomLiquidVisualRules.FormName(cue.Mask) + ". "
				+ (brine ? "Double-line" : "Single-line") + " map family; adjacent pieces join only when both faces declare."
				+ arrow + "}}";
		}

		internal static bool TryPlanCrossing(string Current, int Choice, bool Authorized,
			out string Next, out bool Changed, out string Failure)
		{
			Next = Current;
			Changed = false;
			Failure = null;
			if (!Authorized)
			{
				Failure = "Only the player may configure a laid liquid crossing.";
				return false;
			}
			if (Choice < 0 || Choice > 1)
			{
				Failure = "That crossing orientation is not legal.";
				return false;
			}
			int glyph;
			bool freshVertical;
			if (KingdomLiquidVisualRules.TryCrossingCue(Current, out glyph, out freshVertical)
				&& freshVertical == (Choice == 0)) return true;
			Next = Choice == 0 ? "NSEW" : "EWNS";
			Changed = true;
			return true;
		}

		internal static bool CrossingReadsBack(string Held, bool ExpectedFreshVertical)
		{
			int glyph;
			bool observed;
			return KingdomLiquidVisualRules.TryCrossingCue(Held, out glyph, out observed)
				&& observed == ExpectedFreshVertical;
		}

		internal static string CrossingStatus(string Pairs)
		{
			int glyph;
			bool freshVertical;
			if (!KingdomLiquidVisualRules.TryCrossingCue(Pairs, out glyph, out freshVertical))
				return "\n{{rules|Liquid crossing: unreadable or incomplete pair declaration; fail-closed map sign. No liquid pair is claimed by this sign.}}";
			return "\n{{rules|Liquid crossing: fresh water runs "
				+ (freshVertical ? "north-south (single line); brine runs east-west (double line)"
					: "east-west (single line); brine runs north-south (double line)")
				+ ". Opposite faces pair; the two routes never meet.}}";
		}

		private static string LiquidName(string Liquid)
		{
			if (string.Equals(Liquid?.Trim(), "water", StringComparison.OrdinalIgnoreCase))
				return "fresh-water";
			if (KingdomLiquidVisualRules.IsBrine(Liquid)) return "brine";
			return string.IsNullOrWhiteSpace(Liquid) ? "untyped liquid" : Liquid.Trim().ToLowerInvariant();
		}
	}
}
