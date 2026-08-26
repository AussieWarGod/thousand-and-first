using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>One explicit, directional same-set plan-change declaration.</summary>
	public sealed class KingdomSocketTransition
	{
		public string Key;
		public string FromBuildKey;
		public string ToBuildKey;
		public string LotType;
		public ArchitectureLotSize LotSize;
		public int WaterDrams;
		public long WorkTicks;
		public KingdomMaterialTally Materials;
	}

	/// <summary>Pure validation for the mergeable socket-transition schema.</summary>
	public static class KingdomSocketTransitionRules
	{
		public const int Schema = 1;
		public const int MaxTransitions = 256;
		public const int MaxKeyChars = 128;
		public const long MaxWorkTicks = 100000000L;

		public static bool TryParse(string Key, string From, string To, string Type,
			string Size, string Water, string Materials, string Ticks,
			out KingdomSocketTransition Transition, out string Failure)
		{
			Transition = null;
			Failure = null;
			int water;
			long ticks;
			ArchitectureLotSize size;
			KingdomMaterialTally materials;
			string materialFailure;
			string type = Fold(Type);
			if (!ValidKey(Key) || !ValidKey(From) || !ValidKey(To) || From == To
				|| !ValidKey(type) || !TrySize(Size, out size)
				|| !int.TryParse(Water, NumberStyles.None, CultureInfo.InvariantCulture, out water)
				|| water < 0
				|| !long.TryParse(Ticks, NumberStyles.None, CultureInfo.InvariantCulture, out ticks)
				|| ticks < 1L || ticks > MaxWorkTicks
				|| !KingdomMaterialRules.TryParseMaterialCost(Materials, out materials,
					out materialFailure))
			{
				Failure = "transition " + (Key ?? "<unnamed>")
					+ " has malformed identity, typed lot, water, materials, or work";
				return false;
			}
			Transition = new KingdomSocketTransition
			{
				Key = Key, FromBuildKey = From, ToBuildKey = To, LotType = type,
				LotSize = size, WaterDrams = water, WorkTicks = ticks,
				Materials = materials
			};
			return true;
		}

		public static string IndexKey(string From, string To, string Type,
			ArchitectureLotSize Size)
		{
			string type = Fold(Type);
			return !ValidKey(From) || !ValidKey(To) || !ValidKey(type)
				? null : From + "\n" + To + "\n" + type + "\n"
					+ ((int)Size).ToString(CultureInfo.InvariantCulture);
		}

		public static string RefuseUndeclared(string FromName, string ToName)
		{
			return "The pattern-book declares no safe same-set change from the "
				+ (FromName ?? "standing work") + " to " + (ToName ?? "that design")
				+ ". Strike it and commission fresh, or add an explicit transition declaration.";
		}

		private static bool TrySize(string Text, out ArchitectureLotSize Size)
		{
			Size = 0;
			if (string.Equals(Text, "S", StringComparison.OrdinalIgnoreCase))
				Size = ArchitectureLotSize.Small;
			else if (string.Equals(Text, "M", StringComparison.OrdinalIgnoreCase))
				Size = ArchitectureLotSize.Medium;
			else if (string.Equals(Text, "L", StringComparison.OrdinalIgnoreCase))
				Size = ArchitectureLotSize.Large;
			else if (string.Equals(Text, "XL", StringComparison.OrdinalIgnoreCase))
				Size = ArchitectureLotSize.Huge;
			return Size != 0;
		}

		private static string Fold(string Value)
		{
			return string.IsNullOrWhiteSpace(Value) ? null : Value.Trim().ToLowerInvariant();
		}

		private static bool ValidKey(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxKeyChars) return false;
			for (int i = 0; i < Value.Length; i++)
				if (char.IsControl(Value[i]) || char.IsWhiteSpace(Value[i])) return false;
			return true;
		}
	}
}
