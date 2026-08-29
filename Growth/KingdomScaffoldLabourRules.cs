using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Bounded crew facts witnessed while one scaffold's owner zone was loaded.</summary>
	public sealed class KingdomScaffoldLabourWindow
	{
		public long Tick;
		public int EffectivenessPercent;
		public int Hands;
		public bool Selected;
	}

	/// <summary>Pure result of spending one scaffold labour interval.</summary>
	public sealed class KingdomScaffoldLabourStep
	{
		public long PreviousTick;
		public long NextTick;
		public long RemainingTicks;
		public long WorkedTicks;
		public long CompletionTick;
		public bool Complete;
	}

	/// <summary>Canonical window codec and overflow-safe scaffold labour arithmetic.</summary>
	public static class KingdomScaffoldLabourWindowRules
	{
		public const int Schema = 1;
		public const int MaxHands = KingdomRules.RaisingHandsWanted;

		public static bool TryEncode(KingdomScaffoldLabourWindow Window, out string Encoded)
		{
			Encoded = null;
			if (!Valid(Window)) return false;
			Encoded = "s1|" + Window.Tick.ToString(CultureInfo.InvariantCulture)
				+ "|" + Window.EffectivenessPercent.ToString(CultureInfo.InvariantCulture)
				+ "|" + Window.Hands.ToString(CultureInfo.InvariantCulture)
				+ "|" + (Window.Selected ? "1" : "0");
			return true;
		}

		public static bool TryDecode(string Encoded, out KingdomScaffoldLabourWindow Window)
		{
			Window = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > 64) return false;
			string[] fields = Encoded.Split('|');
			long tick;
			int effectiveness;
			int hands;
			if (fields.Length != 5 || fields[0] != "s1"
				|| !long.TryParse(fields[1], NumberStyles.None,
					CultureInfo.InvariantCulture, out tick)
				|| !int.TryParse(fields[2], NumberStyles.None,
					CultureInfo.InvariantCulture, out effectiveness)
				|| !int.TryParse(fields[3], NumberStyles.None,
					CultureInfo.InvariantCulture, out hands)
				|| (fields[4] != "0" && fields[4] != "1")) return false;
			KingdomScaffoldLabourWindow parsed = new KingdomScaffoldLabourWindow
			{
				Tick = tick,
				EffectivenessPercent = effectiveness,
				Hands = hands,
				Selected = fields[4] == "1"
			};
			if (!Valid(parsed) || !TryEncode(parsed, out string canonical)
				|| canonical != Encoded) return false;
			Window = parsed;
			return true;
		}

		/// <summary>Only a canonical witness at the prior labour tick can price an interval.</summary>
		public static bool TryForInterval(string Encoded, long ExpectedTick,
			out KingdomScaffoldLabourWindow Window)
		{
			if (!TryDecode(Encoded, out Window) || Window.Tick != ExpectedTick)
			{
				Window = null;
				return false;
			}
			return true;
		}

		private static bool Valid(KingdomScaffoldLabourWindow Window)
		{
			if (Window == null || Window.Tick < 0L || Window.EffectivenessPercent < 0
				|| Window.EffectivenessPercent > 100 || Window.Hands < 0
				|| Window.Hands > MaxHands) return false;
			if (!Window.Selected
				&& (Window.Hands != 0 || Window.EffectivenessPercent != 0)) return false;
			return Window.Hands != 0 || Window.EffectivenessPercent == 0;
		}
	}

	public static class KingdomScaffoldLabourRules
	{
		/// <summary>
		/// Spends every forward tick, even at zero effectiveness. Completion uses monotonic binary
		/// search, avoiding the overflowing <c>remaining * 100</c> ceiling expression.
		/// </summary>
		public static KingdomScaffoldLabourStep Advance(long LastTick, long TimeTick,
			long RemainingTicks, int EffectivenessPercent)
		{
			KingdomScaffoldLabourStep result = new KingdomScaffoldLabourStep
			{
				PreviousTick = LastTick,
				NextTick = LastTick,
				RemainingTicks = RemainingTicks > 0L ? RemainingTicks : 0L,
				CompletionTick = RemainingTicks <= 0L ? LastTick : 0L,
				Complete = RemainingTicks <= 0L
			};
			if (result.Complete || LastTick < 0L || TimeTick <= LastTick) return result;
			result.NextTick = TimeTick;
			int effectiveness = EffectivenessPercent <= 0 ? 0
				: (EffectivenessPercent >= 100 ? 100 : EffectivenessPercent);
			long elapsed = TimeTick - LastTick;
			long available = KingdomRules.LabouredTicks(elapsed, effectiveness);
			if (available <= 0L) return result;
			result.WorkedTicks = available < result.RemainingTicks
				? available : result.RemainingTicks;
			result.RemainingTicks -= result.WorkedTicks;
			if (result.RemainingTicks > 0L) return result;
			result.Complete = true;
			long low = 1L;
			long high = elapsed;
			while (low < high)
			{
				long middle = low + (high - low) / 2L;
				if (KingdomRules.LabouredTicks(middle, effectiveness)
					>= result.WorkedTicks) high = middle;
				else low = middle + 1L;
			}
			result.CompletionTick = LastTick + low;
			return result;
		}
	}
}
