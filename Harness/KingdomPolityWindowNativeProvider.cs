using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// #244/#257 positive native witness. After an ordinary daily pass the persona reads the realm's
	/// polity dispatch receipt and journals it as one polity-dispatch observation row, immediately
	/// before this verb's own row. Read-only: it never reconciles, opens, completes or recovers
	/// dispatch state and never advances time. A failed read still lands as a REFUSED observation,
	/// so the evidence is retained and the auto-runner stops on the refused verb.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomPolityWindowNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "polity-window-check";
		internal const string Refused = "polity window refused: ";

		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;

		public IEnumerable<string> ScenarioVerbs => new[] { Verb };

		public string RunScenarioVerb(string name, string argument, out bool ok)
		{
			ok = false;
			string reading = null;
			try
			{
				Require(name == Verb && string.IsNullOrEmpty(argument),
					"polity-window-check takes no arguments");
				Require(!KingdomScenarioAdvance.Pending && !KingdomSurvey.HasBoundPass,
					"polity-window-check reads only between completed passes");
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				Require(system != null && system.Founded, "no founded realm owns a polity dispatch");
				string failure = null;
				reading = KingdomPolityWindowReading.Describe(system.PolityDispatch, out failure);
				Require(reading != null, failure);
			}
			catch (Exception error)
			{
				string refusal = Refused + KingdomScenarioRules.Bounded(error.Message);
				string lost = KingdomScenarioJournal.Append(KingdomPolityWindowReading.Row, false,
					refusal);
				return lost == null ? refusal : refusal + "; observation row not written: " + lost;
			}
			string note = KingdomScenarioJournal.Append(KingdomPolityWindowReading.Row, true, reading);
			if (note != null) return Refused + "observation row not written: " + note;
			ok = true;
			return Verb + " recorded " + KingdomPolityWindowReading.Row + " " + reading;
		}

		private static void Require(bool value, string failure)
		{
			if (!value) throw new InvalidOperationException(failure);
		}
	}
}
