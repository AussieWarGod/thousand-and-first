using System;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free per-case pass/fail counting for
	/// <see cref="KingdomQuoteSitingOccupancyNativeChecks"/>. No <c>XRL</c> type appears here, so
	/// the exact counting contract the setup verb's own "cases=3 passed=/failed=" line rests on --
	/// one failing case increments Failed and journals by name without aborting a sibling, Passed
	/// never moves on a failure, and neither count is ever fabricated -- runs directly under both
	/// public test projects against a plain throwing delegate, not only as a source pin.
	/// </summary>
	internal sealed class KingdomQuoteSitingOccupancyCaseRunner
	{
		internal int Passed;
		internal int Failed;
		internal readonly StringBuilder Evidence = new StringBuilder();

		/// <summary>False the instant one case has failed -- the verb-level truthfulness the
		/// review named: a real per-case failure must REFUSE the whole verb (Ok=false), not just
		/// widen the journal text while the out-parameter still reports success.</summary>
		internal bool Ok { get { return Failed == 0; } }

		/// <summary>Runs <paramref name="Body"/> under <paramref name="Name"/>. A thrown
		/// exception (a Require refusal or anything else) is caught here, counted as failed, and
		/// journaled by name; it never escapes to abort a sibling case or this verb.</summary>
		internal void Run(string Name, Action Body)
		{
			try
			{
				Body();
				Passed++;
			}
			catch (Exception error)
			{
				Failed++;
				Evidence.Append("; case=").Append(Name).Append(" outcome=FAILED reason=")
					.Append(KingdomScenarioRules.Bounded(
						error.GetType().Name + ": " + error.Message));
			}
		}
	}
}
