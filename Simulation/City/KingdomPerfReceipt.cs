using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One timing receipt: what was measured, against which row of the constitution, and how it
	/// judged. Immutable, engine-free, and carrying no reference the caller can mutate afterwards.
	/// </summary>
	internal readonly struct KingdomPerfReceipt
	{
		internal readonly KingdomBudgetLane Lane;

		/// <summary>Which city, zone or job this measured. Never null once published.</summary>
		internal readonly string Label;

		internal readonly long Microseconds;

		internal readonly KingdomComputeCounters Counters;

		/// <summary>The lane's primary count for this measurement, in the lane's own unit.</summary>
		internal readonly long PrimaryCount;

		internal readonly KingdomBudgetVerdict TimeVerdict;

		internal readonly KingdomBudgetVerdict CountVerdict;

		internal KingdomPerfReceipt(
			KingdomBudgetLane lane,
			string label,
			long microseconds,
			KingdomComputeCounters counters,
			long primaryCount,
			KingdomBudgetVerdict timeVerdict,
			KingdomBudgetVerdict countVerdict)
		{
			Lane = lane;
			Label = label;
			Microseconds = microseconds;
			Counters = counters;
			PrimaryCount = primaryCount;
			TimeVerdict = timeVerdict;
			CountVerdict = countVerdict;
		}

		/// <summary>The worse of the two rungs. This is the figure a playtest log is read for.</summary>
		internal KingdomBudgetVerdict Verdict
		{
			get { return KingdomBudgetRules.Worse(TimeVerdict, CountVerdict); }
		}
	}
}
