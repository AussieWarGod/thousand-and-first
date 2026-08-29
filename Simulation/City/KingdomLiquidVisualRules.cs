using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Topology family shown by one frozen liquid-piece declaration.</summary>
	internal enum KingdomLiquidForm : byte
	{
		Cap = 0,
		End = 1,
		Straight = 2,
		Corner = 3,
		Tee = 4,
		Cross = 5
	}

	/// <summary>One color-independent, vanilla CP437 map cue.</summary>
	internal readonly struct KingdomLiquidVisualCue
	{
		internal readonly int Mask;
		internal readonly int Glyph;
		internal readonly KingdomLiquidForm Form;
		internal readonly bool Brine;
		internal readonly bool Valid;

		internal KingdomLiquidVisualCue(int Mask, int Glyph, KingdomLiquidForm Form,
			bool Brine, bool Valid)
		{
			this.Mask = Mask;
			this.Glyph = Glyph;
			this.Form = Form;
			this.Brine = Brine;
			this.Valid = Valid;
		}
	}

	/// <summary>
	/// Pure rendering vocabulary for the LIQUID LAW. Fresh lines use CP437's single-line family;
	/// brine uses its double-line family. Color remains decoration, never the only distinction.
	/// </summary>
	internal static class KingdomLiquidVisualRules
	{
		private static readonly int[] FreshGlyphs = new int[16]
		{
			250, 24, 25, 179, 26, 192, 218, 195,
			27, 217, 191, 180, 196, 193, 194, 197
		};

		private static readonly int[] BrineGlyphs = new int[16]
		{
			254, 30, 31, 186, 16, 200, 201, 204,
			17, 188, 187, 185, 205, 202, 203, 206
		};

		/// <summary>Maps every four-bit declaration to one deterministic topology cue.</summary>
		internal static bool TryCue(int Mask, bool Brine, out KingdomLiquidVisualCue Cue)
		{
			if (Mask < 0 || Mask > KingdomNetworkRules.JoinAll)
			{
				Cue = new KingdomLiquidVisualCue(0, Brine ? BrineGlyphs[0] : FreshGlyphs[0],
					KingdomLiquidForm.Cap, Brine, false);
				return false;
			}
			Cue = new KingdomLiquidVisualCue(Mask,
				Brine ? BrineGlyphs[Mask] : FreshGlyphs[Mask], FormOf(Mask), Brine, true);
			return true;
		}

		/// <summary>Unreadable declarations render as their family's closed cap.</summary>
		internal static bool TryCue(string Joins, bool Brine, out KingdomLiquidVisualCue Cue)
		{
			int mask;
			if (!KingdomNetworkRules.TryParseJoins(Joins, out mask))
			{
				TryCue(0, Brine, out Cue);
				Cue = new KingdomLiquidVisualCue(0, Cue.Glyph, KingdomLiquidForm.Cap, Brine, false);
				return false;
			}
			return TryCue(mask, Brine, out Cue);
		}

		internal static bool IsBrine(string Liquid)
		{
			return string.Equals(Liquid?.Trim(), "salt", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(Liquid?.Trim(), "brine", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Canonical face order. Stored text stays a frozen declaration, not adjacency.</summary>
		internal static bool TryCanonicalJoins(int Mask, out string Joins)
		{
			Joins = null;
			if (Mask < 0 || Mask > KingdomNetworkRules.JoinAll) return false;
			char[] held = new char[4];
			int count = 0;
			if ((Mask & KingdomNetworkRules.JoinNorth) != 0) held[count++] = 'N';
			if ((Mask & KingdomNetworkRules.JoinSouth) != 0) held[count++] = 'S';
			if ((Mask & KingdomNetworkRules.JoinEast) != 0) held[count++] = 'E';
			if ((Mask & KingdomNetworkRules.JoinWest) != 0) held[count++] = 'W';
			Joins = new string(held, 0, count);
			return true;
		}

		internal static KingdomLiquidForm FormOf(int Mask)
		{
			switch (Mask)
			{
			case 0: return KingdomLiquidForm.Cap;
			case 1:
			case 2:
			case 4:
			case 8: return KingdomLiquidForm.End;
			case 3:
			case 12: return KingdomLiquidForm.Straight;
			case 5:
			case 6:
			case 9:
			case 10: return KingdomLiquidForm.Corner;
			case 7:
			case 11:
			case 13:
			case 14: return KingdomLiquidForm.Tee;
			default: return KingdomLiquidForm.Cross;
			}
		}

		internal static string FormName(int Mask)
		{
			switch (Mask)
			{
			case 0: return "cap; no joins";
			case 1: return "end; north";
			case 2: return "end; south";
			case 3: return "straight; north-south";
			case 4: return "end; east";
			case 5: return "corner; north-east";
			case 6: return "corner; south-east";
			case 7: return "tee; north-south-east";
			case 8: return "end; west";
			case 9: return "corner; north-west";
			case 10: return "corner; south-west";
			case 11: return "tee; north-south-west";
			case 12: return "straight; east-west";
			case 13: return "tee; north-east-west";
			case 14: return "tee; south-east-west";
			default: return "cross; north-south-east-west";
			}
		}

		/// <summary>
		/// Mixed single/double CP437 crossings state which isolated route carries which liquid.
		/// Old <c>NSEW</c> rows remain fresh north-south; <c>EWNS</c> rotates that fact.
		/// </summary>
		internal static bool TryCrossingCue(string Pairs, out int Glyph,
			out bool FreshVertical)
		{
			Glyph = 254;
			FreshVertical = true;
			int mask;
			if (!KingdomNetworkRules.TryParseJoins(Pairs, out mask)
				|| mask != KingdomNetworkRules.JoinAll) return false;
			string value = Pairs.Trim().ToUpperInvariant();
			int vertical = FirstOf(value, 'N', 'S');
			int horizontal = FirstOf(value, 'E', 'W');
			if (vertical < 0 || horizontal < 0) return false;
			FreshVertical = vertical < horizontal;
			Glyph = FreshVertical ? 216 : 215;
			return true;
		}

		private static int FirstOf(string Value, char A, char B)
		{
			int first = Value.IndexOf(A);
			int second = Value.IndexOf(B);
			if (first < 0) return second;
			if (second < 0) return first;
			return Math.Min(first, second);
		}
	}
}
