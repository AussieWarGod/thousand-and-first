using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>
	/// Bounded facts witnessed while a plot's owner zone was loaded. They price only the interval
	/// beginning at <see cref="Tick"/>; a later wake may never substitute its newly seated crew or
	/// newly changed yards for these facts.
	/// </summary>
	public sealed class KingdomPlotLabourWindow
	{
		public long Tick;
		public int LabourPercent;
		public int InfrastructurePercent;
		public int Hands;
		public bool Selected;
	}

	/// <summary>Pure, canonical codec and interval authority for plot labour windows.</summary>
	public static class KingdomPlotLabourWindowRules
	{
		public const int Schema = 1;
		public const int MaxHands = KingdomRules.RaisingHandsWanted;
		public const int InfrastructureUnavailable = 0;
		public const int InfrastructureReady = 100;

		public static bool TryEncode(KingdomPlotLabourWindow Window, out string Encoded)
		{
			Encoded = null;
			if (!Valid(Window)) return false;
			Encoded = "w1|" + Window.Tick.ToString(CultureInfo.InvariantCulture)
				+ "|" + Window.LabourPercent.ToString(CultureInfo.InvariantCulture)
				+ "|" + Window.InfrastructurePercent.ToString(CultureInfo.InvariantCulture)
				+ "|" + Window.Hands.ToString(CultureInfo.InvariantCulture)
				+ "|" + (Window.Selected ? "1" : "0");
			return true;
		}

		public static bool TryDecode(string Encoded, out KingdomPlotLabourWindow Window)
		{
			Window = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > 64) return false;
			string[] fields = Encoded.Split('|');
			long tick;
			int labour;
			int infrastructure;
			int hands;
			if (fields.Length != 6 || fields[0] != "w1"
				|| !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture,
					out tick)
				|| !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture,
					out labour)
				|| !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture,
					out infrastructure)
				|| !int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture,
					out hands)
				|| (fields[5] != "0" && fields[5] != "1")) return false;
			KingdomPlotLabourWindow parsed = new KingdomPlotLabourWindow
			{
				Tick = tick,
				LabourPercent = labour,
				InfrastructurePercent = infrastructure,
				Hands = hands,
				Selected = fields[5] == "1"
			};
			if (!Valid(parsed) || !TryEncode(parsed, out string canonical)
				|| canonical != Encoded) return false;
			Window = parsed;
			return true;
		}

		/// <summary>
		/// Reads only a window anchored to the labour receipt. Missing, corrupt, or mismatched state
		/// is loss-only: zero work for this interval, never an inference about unloaded actors.
		/// </summary>
		public static bool TryForInterval(string Encoded, long ExpectedTick,
			out KingdomPlotLabourWindow Window)
		{
			if (!TryDecode(Encoded, out Window) || Window.Tick != ExpectedTick)
			{
				Window = null;
				return false;
			}
			return true;
		}

		private static bool Valid(KingdomPlotLabourWindow Window)
		{
			if (Window == null || Window.Tick < 0L || Window.LabourPercent < 0
				|| Window.LabourPercent > 100 || Window.Hands < 0 || Window.Hands > MaxHands
				|| (Window.InfrastructurePercent != InfrastructureUnavailable
					&& Window.InfrastructurePercent != InfrastructureReady)) return false;
			if (!Window.Selected && (Window.Hands != 0 || Window.LabourPercent != 0)) return false;
			return Window.Hands != 0 || Window.LabourPercent == 0;
		}
	}
}
