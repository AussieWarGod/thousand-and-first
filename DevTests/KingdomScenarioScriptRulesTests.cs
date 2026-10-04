#if TAF_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The sealed scenario script's line rules and verb bound (Harness/KingdomScenarioScriptRules.cs),
	/// executed rather than read. Native run 4ce2a6a1 sealed the heart's rung-5 persona at 35 verbs
	/// and the runner refused it against the old bound of 32 before its first verb, after every
	/// offline check had passed. The bound is now 48 and both Python tools that build a sealed
	/// profile restate it, so this pins the runtime, its two mirrors and the persona that needed it.
	/// </summary>
	public class KingdomScenarioScriptRulesTests
	{
		private const string Rung5Persona = "Tools/personas/camp-heart-rung5-native-check.persona";

		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		private static List<string> Repeat(int Count, string Verb)
		{
			return Enumerable.Repeat(Verb, Count).ToList();
		}

		[Test]
		public void TheBoundIsFortyEight()
		{
			Assert.That(KingdomScenarioScriptRules.MaxVerbs, Is.EqualTo(48));
		}

		[TestCase(1)]
		[TestCase(47)]
		[TestCase(48)]
		public void UpToTheBoundEveryVerbIsKeptInOrder(int Count)
		{
			List<string> lines = Repeat(Count - 1, "status");
			lines.Add("advance 1200");
			bool ok = KingdomScenarioScriptRules.TryParse(lines, out IList<string> verbs,
				out string failure);
			Assert.That(ok, Is.True, "refused: " + failure);
			Assert.That(failure, Is.Null);
			Assert.That(verbs, Is.EqualTo(lines));
		}

		[TestCase(49)]
		[TestCase(64)]
		public void OneVerbPastTheBoundRefusesTheWholeScript(int Count)
		{
			bool ok = KingdomScenarioScriptRules.TryParse(Repeat(Count, "status"),
				out IList<string> verbs, out string failure);
			Assert.That(ok, Is.False);
			Assert.That(verbs, Is.Null);
			Assert.That(failure, Is.EqualTo("the script declares more than 48 verbs"));
		}

		[Test]
		public void ACountedVerbAndItsArgumentAreOneVerb()
		{
			// Ninety-six shell words on forty-eight lines: the runner counts lines, not words.
			bool ok = KingdomScenarioScriptRules.TryParse(Repeat(48, "advance 1200"),
				out IList<string> verbs, out _);
			Assert.That(ok, Is.True);
			Assert.That(verbs.Count, Is.EqualTo(48));
			Assert.That(KingdomScenarioScriptRules.TryParse(Repeat(49, "advance 1200"), out _,
				out _), Is.False);
		}

		[Test]
		public void CommentsBlankLinesAndWhiteSpaceAreNotVerbs()
		{
			List<string> lines = new List<string>
			{
				"# Sealed developer scenario script.", "", "   ", "\t# indented comment", null,
			};
			for (int i = 0; i < 48; i++)
				lines.Add(i % 2 == 0 ? "  status  " : "\u00a0advance 1200\u2028");
			lines.Add("");
			bool ok = KingdomScenarioScriptRules.TryParse(lines, out IList<string> verbs,
				out string failure);
			Assert.That(ok, Is.True, "refused: " + failure);
			Assert.That(verbs.Count, Is.EqualTo(48));
			Assert.That(verbs[0], Is.EqualTo("status"));
			Assert.That(verbs[1], Is.EqualTo("advance 1200"));
			// String.Trim keeps U+001C although Python's str.isspace calls it white space; the
			// offline mirrors count it as a verb for exactly this reason.
			Assert.That(KingdomScenarioScriptRules.TryParse(new[] { "\u001c" }, out verbs, out _),
				Is.True);
			Assert.That(verbs.Count, Is.EqualTo(1));
		}

		[Test]
		public void AnOverLongLineAndAnEmptyScriptRefuseByName()
		{
			string longest = new string('a', KingdomScenarioScriptRules.MaxVerbChars);
			Assert.That(KingdomScenarioScriptRules.TryParse(new[] { longest }, out _, out _),
				Is.True);
			bool ok = KingdomScenarioScriptRules.TryParse(new[] { "# c", longest + "a" },
				out IList<string> verbs, out string failure);
			Assert.That(ok, Is.False);
			Assert.That(verbs, Is.Null);
			Assert.That(failure,
				Is.EqualTo("script line 2 is 301 characters, over the 300-character bound"));
			Assert.That(KingdomScenarioScriptRules.TryParse(new[] { "# only a comment", "" },
				out _, out failure), Is.False);
			Assert.That(failure, Is.EqualTo("the script file declares no verbs"));
			Assert.That(KingdomScenarioScriptRules.TryParse(null, out _, out failure), Is.False);
			Assert.That(failure, Is.EqualTo("the script file declares no verbs"));
		}

		/// <summary>
		/// End to end without the engine: the persona's steps, laid out one a line under a comment
		/// header exactly as Tools/scenario_profile.py seals them, pass the runner's own parser and
		/// are then recognised as the five-rung form. The rung-5 fixture feeds the matcher the steps
		/// directly, which is how a 35-verb script stayed green offline under a bound of 32.
		/// </summary>
		[Test]
		public void TheRungFivePersonaSealsThirtyFiveVerbsTheRunnerReadsAsTheFiveRungForm()
		{
			string script = null;
			foreach (string line in Read(Rung5Persona).Split('\n'))
				if (line.StartsWith("SCRIPT=")) script = line.Substring("SCRIPT=".Length).Trim();
			Assert.That(script, Is.Not.Null);
			string sealedText = "# Sealed developer scenario script.\n"
				+ string.Join("\n", script.Split(';').Select(step => step.Trim())) + "\n";
			bool ok = KingdomScenarioScriptRules.TryParse(sealedText.Split('\n'),
				out IList<string> verbs, out string failure);
			Assert.That(ok, Is.True, "refused: " + failure);
			Assert.That(verbs.Count, Is.EqualTo(35));
			Assert.That(KingdomCampHeartChainScript.MatchesRung5(verbs), Is.True);
			Assert.That(KingdomCampHeartChainScript.SealedTargetRung(verbs), Is.EqualTo(5));
		}

		[Test]
		public void BothPythonToolsRestateTheRuntimeBound()
		{
			foreach (string tool in new[] { "Tools/scenario_profile.py",
				"Tools/personas/persona_matrix.py" })
			{
				MatchCollection declared = Regex.Matches(Read(tool),
					@"^MAX_SCRIPT_VERBS = (\d+)\r?$", RegexOptions.Multiline);
				Assert.That(declared.Count, Is.EqualTo(1), tool);
				Assert.That(int.Parse(declared[0].Groups[1].Value),
					Is.EqualTo(KingdomScenarioScriptRules.MaxVerbs), tool);
			}
		}

		[Test]
		public void TheReaderDelegatesSoThereIsOneCount()
		{
			string reader = Read("Harness/KingdomScenarioScript.cs");
			Assert.That(reader, Does.Contain(
				"return KingdomScenarioScriptRules.TryParse(lines, out Verbs, out Failure);"));
			Assert.That(reader, Does.Not.Contain("const int MaxVerbs"));
			Assert.That(reader, Does.Not.Contain("found.Add("));
		}
	}
}
#endif
