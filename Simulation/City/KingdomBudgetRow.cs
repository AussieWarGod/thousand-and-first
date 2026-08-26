using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One lane's budget. A threshold of <see cref="KingdomBudgetRules.NoLimit"/> is a rung that
	/// never fires — the constitution gives several lanes a warn without a numeric fail, and a
	/// sentinel says so instead of a zero that would fail everything.
	/// </summary>
	internal readonly struct KingdomBudgetRow
	{
		internal readonly KingdomBudgetLane Lane;

		/// <summary>The name this lane writes in the log, per LIVING-CITY-ARCHITECTURE &sect;6.5.</summary>
		internal readonly string LogName;

		internal readonly long WarnMicroseconds;

		internal readonly long FailMicroseconds;

		/// <summary>What the lane's primary count counts, for the log line and the BUDGET line.</summary>
		internal readonly string CountName;

		internal readonly long WarnCount;

		internal readonly long FailCount;

		internal KingdomBudgetRow(
			KingdomBudgetLane lane,
			string logName,
			long warnMicroseconds,
			long failMicroseconds,
			string countName,
			long warnCount,
			long failCount)
		{
			Lane = lane;
			LogName = logName;
			WarnMicroseconds = warnMicroseconds;
			FailMicroseconds = failMicroseconds;
			CountName = countName;
			WarnCount = warnCount;
			FailCount = failCount;
		}
	}
}
