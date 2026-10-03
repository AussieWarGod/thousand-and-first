using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The sealed second-city script, kept in its own engine-free shard so the provider, the
	/// checks and the DevTests pin all read ONE array rather than three copies of it.
	/// Step 1 is the built-in realize verb: city one is founded by the production first-city
	/// transaction, never by this harness.
	/// </summary>
	internal static class KingdomSecondCityScript
	{
		internal const string SetupVerb = "second-city-setup";
		internal const string CheckVerb = "second-city-check";

		/// <summary>
		/// One ordinary turn between realize and setup. Production's ZoneActivatedEvent handler
		/// seats a returning founder only after KingdomMaster.ObserveAutomaticWake, and that
		/// wake is consumed on the tick the master latch changes (Core/KingdomMaster.cs). With no
		/// turn spent, realize, the latch's first observation and both controlled transfers share
		/// one tick, so the return leg would read a consumed wake instead of the seat exchange.
		/// The setup verb re-proves the latch with KingdomMaster.AutomaticWorkAllowed; no case
		/// after it spends a turn.
		/// </summary>
		internal const string SettleStep = "advance 1";

		/// <summary>Extra journal rows this family writes beside the automatic verb rows.</summary>
		internal const string SiteRow = "second-city-site";
		internal const string TopologyRow = "second-city-topology";

		internal static readonly string[] Steps = { "stagedigest", "realize", SettleStep,
			SetupVerb, CheckVerb, CheckVerb, CheckVerb, CheckVerb, "stagedigest" };

		/// <summary>How many times the persona calls the check verb; one case per call.</summary>
		internal static int CheckCalls
		{
			get
			{
				int calls = 0;
				for (int i = 0; i < Steps.Length; i++) if (Steps[i] == CheckVerb) calls++;
				return calls;
			}
		}

		internal static bool Matches(IList<string> Script)
		{
			if (Script == null || Script.Count != Steps.Length) return false;
			for (int i = 0; i < Steps.Length; i++) if (Script[i] != Steps[i]) return false;
			return true;
		}
	}
}
