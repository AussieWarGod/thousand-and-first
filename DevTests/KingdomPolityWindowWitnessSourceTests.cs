#if TAF_TESTS
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>#244/#257 wiring pins for the engine-bound witness provider, which TafTests does not
	/// compile, and for the persona and host grammar that consume its rows. The reading itself is
	/// executed engine-free in KingdomPolityWindowWitnessTests; these strings close no native gate.</summary>
	[TestFixture]
	public sealed class KingdomPolityWindowWitnessSourceTests
	{
		private const string Provider = "Harness/KingdomPolityWindowNativeProvider.cs";
		private const string Persona = "Tools/personas/polity-window-native-check.persona";
		private const string Host = "Tools/personas/persona_polity.py";
		private const string DriftText =
			" continues with endpoint facts changed since it opened; no new dispatch until window ";

		[Test]
		public void ProviderJournalsTheObservationBeforeItsOwnRowAndNeverWrites()
		{
			string source = Read(Provider);
			foreach (string token in new[] { "[KingdomScenarioVerbProvider]",
				"internal const string Verb = \"polity-window-check\";",
				"KingdomPolityWindowReading.Describe(system.PolityDispatch, out failure)",
				"KingdomScenarioJournal.Append(KingdomPolityWindowReading.Row, false,",
				"KingdomScenarioJournal.Append(KingdomPolityWindowReading.Row, true, reading)",
				"return Verb + \" recorded \" + KingdomPolityWindowReading.Row + \" \" + reading;",
				"!KingdomScenarioAdvance.Pending && !KingdomSurvey.HasBoundPass" })
				StringAssert.Contains(token, source);
			foreach (string token in new[] { "TryOpen(", "TryReconcile(", "TryComplete(",
				"TryRecover(", "PolityDispatch =", "SetStringGameState", "TimeTicks", "Harmony",
				"Popup" })
				StringAssert.DoesNotContain(token, source);
			ClassicAssert.Less(source.IndexOf("Append(KingdomPolityWindowReading.Row, true, reading)",
				System.StringComparison.Ordinal), source.IndexOf("ok = true;",
				System.StringComparison.Ordinal), "the observation lands before the verb reports OK");
		}

		[Test]
		public void PersonaWatchesEveryDailyPassAndBindsTheProductionDriftLine()
		{
			string persona = Read(Persona);
			foreach (string row in new[] { "REQUEST=founding-first-city", "START=8.22@40,12",
				"VERBS=polity-window-check", "CHECK=polity-window", "TIMEOUT=1200",
				"LOG_FORBID=[\"polity: daily reconciliation refused\",\"polity: zone reconciliation "
					+ "refused\",\"polity: active load reconciliation refused\",\" withdrawn: \"]",
				"LOG_REQUIRE=[\"" + DriftText + "\"]" })
				ClassicAssert.AreEqual(1, Regex.Matches(persona, "(?m)^" + Regex.Escape(row) + "$").Count,
					row);
			Match script = Regex.Match(persona, "(?m)^SCRIPT=(.*)$");
			ClassicAssert.IsTrue(script.Success);
			ClassicAssert.AreEqual("stagedigest;realize;" + Repeat("advance 1200;polity-window-check;", 11)
				+ "stagedigest", script.Groups[1].Value);
			string notes = Read("Polity/KingdomPolitySchedulerRuntime.DispatchNotes.cs");
			StringAssert.Contains("\"polity: dispatch window \" + Window.ToString(CultureInfo.InvariantCulture)",
				notes);
			StringAssert.Contains("+ \"" + DriftText + "\"", notes);
		}

		[Test]
		public void HostGrammarNamesTheHarnessRowAndVerb()
		{
			string host = Read(Host);
			StringAssert.Contains("OBSERVATION = \"polity-dispatch\"", host);
			StringAssert.Contains("VERB = \"polity-window-check\"", host);
			StringAssert.Contains("window=(0|[1-9][0-9]{0,19}) revision=(0|[1-9][0-9]{0,18}) ", host);
			StringAssert.Contains("count=([1-3]) mask=([0-7]) intents=([0-3])", host);
			StringAssert.Contains("POLITY_EVIDENCE_ROWS = (\"polity-dispatch\",)",
				Read("Tools/personas/persona_matrix.py"));
		}

		private static string Repeat(string value, int count)
		{
			System.Text.StringBuilder builder = new System.Text.StringBuilder();
			for (int i = 0; i < count; i++) builder.Append(value);
			return builder.ToString();
		}

		private static string Read(string relative)
		{
			return File.ReadAllText(Path.Combine(TestMain.RepositoryRoot, relative));
		}
	}
}
#endif
