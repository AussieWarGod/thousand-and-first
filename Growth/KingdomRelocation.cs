using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine boundary for founder-consented, sequential whole-lot moves which clear exact
	/// yielding plots from a heart rung. Zone receipt owns state; original objects own identity.
	/// </summary>
	public static partial class KingdomRelocation
	{
		public const string ReceiptProperty = "r_TAF_HeartRelocationReceipt";
		public const string LastReceiptProperty = "r_TAF_HeartRelocationLast";
		public const string FaultProperty = "r_TAF_HeartRelocationFault";
		public const string FramePlanProperty = "r_TAF_RelocationFramePlan";
		public const string FrameMoveProperty = "r_TAF_RelocationFrameMove";
		public const string FrameKindProperty = "r_TAF_RelocationFrameKind";
		public const string FrameBlueprint = "r_KingdomRelocationFrame";
		public const string StakeBlueprint = "r_KingdomRelocationStake";
		private const string EscrowPrefix = "r_TAF_RelocationEscrow:";

		internal sealed class PreparedPlan
		{
			internal KingdomRelocationReceipt Receipt;
			internal string Preview;
		}

		private static KingdomRelocationRect Frozen(KingdomPlotRules.PlotRect Rect)
		{
			return new KingdomRelocationRect(Rect.X1, Rect.Y1, Rect.X2, Rect.Y2);
		}

		private static KingdomPlotRules.PlotRect Runtime(KingdomRelocationRect Rect)
		{
			return new KingdomPlotRules.PlotRect(Rect.X1, Rect.Y1, Rect.X2, Rect.Y2);
		}

		private static string Bounded(string Text)
		{
			if (string.IsNullOrEmpty(Text)) return "relocation authority diverged";
			return Text.Length <= KingdomRelocationRules.MaxFailureChars ? Text
				: Text.Substring(0, KingdomRelocationRules.MaxFailureChars);
		}

		public static bool HasActive(Zone Zone)
		{
			return Zone != null && !string.IsNullOrEmpty(
				Zone.GetZoneProperty(ReceiptProperty, null));
		}
	}
}
