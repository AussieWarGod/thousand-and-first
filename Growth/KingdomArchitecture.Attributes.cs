using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		// --- Attribute parsing and validation ---------------------------------------------

		private static bool Required(LoadState State, RawRecord Raw, string Name, out string Value)
		{
			Value = null;
			if (Raw.BadAttributes.Contains(Name) || !Raw.Values.TryGetValue(Name, out Value)
				|| string.IsNullOrEmpty(Value))
				return Fault(State, Raw.Key + " " + Name, "required attribute is absent or malformed");
			return true;
		}

		private static string Optional(RawRecord Raw, string Name)
		{
			string result;
			return Raw.BadAttributes.Contains(Name) || !Raw.Values.TryGetValue(Name, out result)
				? null : result;
		}

		private static bool Has(RawRecord Raw, string Name)
		{
			return Raw.BadAttributes.Contains(Name) || Raw.Values.ContainsKey(Name);
		}

		private static bool RequiredInt(LoadState State, RawRecord Raw, string Name,
			int Minimum, int Maximum, out int Value)
		{
			Value = 0;
			string text;
			if (!Required(State, Raw, Name, out text)
				|| !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out Value)
				|| Value < Minimum || Value > Maximum)
				return Fault(State, Raw.Key + " " + Name, "integer is outside its bound");
			return true;
		}

		private static bool OptionalInt(LoadState State, RawRecord Raw, string Name,
			int Minimum, int Maximum, int Default, out int Value)
		{
			Value = Default;
			if (Raw.BadAttributes.Contains(Name))
				return Fault(State, Raw.Key + " " + Name, "integer attribute is malformed");
			string text;
			if (!Raw.Values.TryGetValue(Name, out text)) return true;
			if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out Value)
				|| Value < Minimum || Value > Maximum)
				return Fault(State, Raw.Key + " " + Name, "integer is outside its bound");
			return true;
		}

		private static bool OptionalBoolean(LoadState State, RawRecord Raw, string Name,
			bool Default, out bool Value)
		{
			Value = Default;
			if (Raw.BadAttributes.Contains(Name))
				return Fault(State, Raw.Key + " " + Name, "boolean attribute is malformed");
			string text;
			if (!Raw.Values.TryGetValue(Name, out text)) return true;
			if (!TryBoolean(text, out Value))
				return Fault(State, Raw.Key + " " + Name, "expected yes/no, true/false, or 1/0");
			return true;
		}

		private static bool RequiredClaim(LoadState State, RawRecord Raw,
			out ArchitectureClaim Value)
		{
			Value = ArchitectureClaim.Unclaimed;
			if (Raw.BadAttributes.Contains("Claim"))
				return Fault(State, Raw.Key + " Claim", "claim attribute is malformed");
			string text;
			if (!Raw.Values.TryGetValue("Claim", out text) || string.IsNullOrEmpty(text))
				return Fault(State, Raw.Key + " Claim", "building or yard claim is required");
			string folded = Fold(text);
			if (folded == "building") Value = ArchitectureClaim.Building;
			else if (folded == "yard") Value = ArchitectureClaim.Yard;
			else return Fault(State, Raw.Key + " Claim", "expected exactly building or yard");
			return true;
		}

		private static bool TryFootprint(string Text, int MapWidth, int MapHeight,
			out int X, out int Y, out int Width, out int Height)
		{
			X = 0; Y = 0; Width = 0; Height = 0;
			if (string.IsNullOrEmpty(Text)) return false;
			string[] terms = Text.Split(',');
			string[] size = terms.Length == 3 ? terms[2].Split('x') : new string[0];
			if (terms.Length != 3 || size.Length != 2
				|| !int.TryParse(terms[0], NumberStyles.None, CultureInfo.InvariantCulture, out X)
				|| !int.TryParse(terms[1], NumberStyles.None, CultureInfo.InvariantCulture, out Y)
				|| !int.TryParse(size[0], NumberStyles.None, CultureInfo.InvariantCulture, out Width)
				|| !int.TryParse(size[1], NumberStyles.None, CultureInfo.InvariantCulture, out Height)
				|| X < 0 || Y < 0 || Width < 1 || Height < 1
				|| (long)X + Width > MapWidth || (long)Y + Height > MapHeight)
				return false;
			return Text == X.ToString(CultureInfo.InvariantCulture) + ","
				+ Y.ToString(CultureInfo.InvariantCulture) + ","
				+ Width.ToString(CultureInfo.InvariantCulture) + "x"
				+ Height.ToString(CultureInfo.InvariantCulture);
		}

		private static bool OptionalPassability(LoadState State, RawRecord Raw,
			out ArchitecturePassability Value)
		{
			Value = ArchitecturePassability.Walkable;
			if (Raw.BadAttributes.Contains("Pass"))
				return Fault(State, Raw.Key + " Pass", "passability attribute is malformed");
			string text;
			if (!Raw.Values.TryGetValue("Pass", out text)) return true;
			string folded = Fold(text);
			if (folded == "walk" || folded == "walkable") Value = ArchitecturePassability.Walkable;
			else if (folded == "block" || folded == "blocked") Value = ArchitecturePassability.Blocked;
			else if (folded == "adjacent") Value = ArchitecturePassability.Adjacent;
			else return Fault(State, Raw.Key + " Pass", "unknown passability " + text);
			return true;
		}

		private static bool OptionalStage(LoadState State, RawRecord Raw, string Name,
			int Default, out int Value)
		{
			Value = Default;
			if (Raw.BadAttributes.Contains(Name))
				return Fault(State, Raw.Key + " " + Name, "stage selector is malformed");
			string text;
			if (!Raw.Values.TryGetValue(Name, out text)) return true;
			int numeric;
			GrowthStage stage;
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)
				&& numeric >= (int)GrowthStage.Camp && numeric <= (int)GrowthStage.City)
				Value = numeric;
			else if (Enum.TryParse(text, true, out stage) && KingdomRules.IsKnownStage(stage))
				Value = (int)stage;
			else return Fault(State, Raw.Key + " " + Name, "unknown growth stage " + text);
			return true;
		}

		private static bool OptionalTech(LoadState State, RawRecord Raw, string Name,
			int Default, out int Value)
		{
			Value = Default;
			if (Raw.BadAttributes.Contains(Name))
				return Fault(State, Raw.Key + " " + Name, "technology selector is malformed");
			string text;
			if (!Raw.Values.TryGetValue(Name, out text)) return true;
			int numeric;
			TechLevel tech;
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)
				&& numeric >= (int)TechLevel.Hands && numeric <= (int)TechLevel.Arclight)
				Value = numeric;
			else if (Enum.TryParse(text, true, out tech) && KingdomZoningRules.IsKnownTechLevel(tech))
				Value = (int)tech;
			else return Fault(State, Raw.Key + " " + Name, "unknown craft rung " + text);
			return true;
		}

		private static bool TryBoolean(string Text, out bool Value)
		{
			Value = false;
			if (string.IsNullOrWhiteSpace(Text)) return false;
			switch (Text.Trim().ToLowerInvariant())
			{
			case "yes": case "true": case "1": Value = true; return true;
			case "no": case "false": case "0": Value = false; return true;
			default: return false;
			}
		}

		private static bool TryCover(string Text, out ArchitectureCover Value)
		{
			Value = ArchitectureCover.Open;
			string folded = Fold(Text);
			if (folded == "open") Value = ArchitectureCover.Open;
			else if (folded == "soft") Value = ArchitectureCover.Soft;
			else if (folded == "walled") Value = ArchitectureCover.Walled;
			else if (folded == "natural" || folded == "carved") Value = ArchitectureCover.Natural;
			else return false;
			return true;
		}

		private static bool TryFrontage(string Text, out ArchitectureFrontage Value)
		{
			Value = ArchitectureFrontage.Heart;
			string folded = Fold(Text);
			if (folded == "heart") Value = ArchitectureFrontage.Heart;
			else if (folded == "road") Value = ArchitectureFrontage.Road;
			else return false;
			return true;
		}

		private static bool TryLotSize(string Text, out ArchitectureLotSize Value)
		{
			Value = 0;
			string folded = Fold(Text);
			if (folded == "s" || folded == "small") Value = ArchitectureLotSize.Small;
			else if (folded == "m" || folded == "medium") Value = ArchitectureLotSize.Medium;
			else if (folded == "l" || folded == "large") Value = ArchitectureLotSize.Large;
			else if (folded == "xl" || folded == "huge") Value = ArchitectureLotSize.Huge;
			else return false;
			return true;
		}

		private static bool TryLotSize(KingdomPlotRules.PlotSize Size,
			out ArchitectureLotSize Value)
		{
			Value = 0;
			switch (Size)
			{
			case KingdomPlotRules.PlotSize.Small: Value = ArchitectureLotSize.Small; return true;
			case KingdomPlotRules.PlotSize.Medium: Value = ArchitectureLotSize.Medium; return true;
			case KingdomPlotRules.PlotSize.Large: Value = ArchitectureLotSize.Large; return true;
			case KingdomPlotRules.PlotSize.Huge: Value = ArchitectureLotSize.Huge; return true;
			default: return false;
			}
		}

		private static bool KnownLotSize(ArchitectureLotSize Size)
		{
			return Size == ArchitectureLotSize.Small || Size == ArchitectureLotSize.Medium
				|| Size == ArchitectureLotSize.Large || Size == ArchitectureLotSize.Huge;
		}

		private static string ExactRecordKey(string BuildKey, string FoldedType,
			ArchitectureLotSize ActualLotSize)
		{
			// Newlines cannot occur in validated keys, making this bounded identity injective.
			return BuildKey + "\n" + FoldedType + "\n"
				+ ((int)ActualLotSize).ToString(CultureInfo.InvariantCulture);
		}

		private static string BindingRecordKey(string PlanKey, string BindingKey,
			string FoldedType, ArchitectureLotSize ActualLotSize)
		{
			return PlanKey + "\n" + BindingKey + "\n" + FoldedType + "\n"
				+ ((int)ActualLotSize).ToString(CultureInfo.InvariantCulture);
		}

	}
}
