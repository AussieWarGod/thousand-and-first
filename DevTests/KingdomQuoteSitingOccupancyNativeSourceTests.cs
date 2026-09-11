#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Wiring tripwires only. The persona supplies the actual native evidence; these assert
	/// that the seam is registered, that the provider's sealed script and the persona's own
	/// SCRIPT are the SAME sequence of steps (found in review-0192a82-siting-findings.md:
	/// a provider whose sealed array is shorter than its persona's SCRIPT refuses every verb,
	/// including setup, so this is a real cross-check between the two files rather than two
	/// independently pinned literals that could drift together undetected), and that the
	/// setup/check completion ordering matches the pattern the other native providers use.
	/// </summary>
	public class KingdomQuoteSitingOccupancyNativeSourceTests
	{
		private const string Provider = "Harness/KingdomQuoteSitingOccupancyNativeProvider.cs";
		private const string Checks = "Harness/KingdomQuoteSitingOccupancyNativeChecks.cs";
		private const string Persona = "Tools/personas/quote-occupancy-native-check.persona";

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }

		private static string Between(string Source, string Start, string End)
		{
			int from = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.That(from, Is.GreaterThanOrEqualTo(0), "missing source boundary: " + Start);
			from += Start.Length;
			int to = Source.IndexOf(End, from, StringComparison.Ordinal);
			Assert.That(to, Is.GreaterThan(from), "missing source boundary: " + End);
			return Source.Substring(from, to - from);
		}

		private static string ConstValue(string Source, string Name)
		{
			string marker = "internal const string " + Name + " = \"";
			int from = Source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(from, Is.GreaterThanOrEqualTo(0), "missing const: " + Name);
			from += marker.Length;
			int to = Source.IndexOf("\";", from, StringComparison.Ordinal);
			Assert.That(to, Is.GreaterThan(from), "unterminated const: " + Name);
			return Source.Substring(from, to - from);
		}

		/// <summary>Parses the provider's own <c>Script</c> array literal into the exact ordered
		/// step names it seals, resolving the SetupVerb/CheckVerb identifiers against their own
		/// const declarations in the SAME file rather than hardcoding "quote-occupancy-setup"/
		/// "quote-occupancy-check" a second time here.</summary>
		private static List<string> ProviderScriptSteps(string ProviderSource)
		{
			string setupVerb = ConstValue(ProviderSource, "SetupVerb");
			string checkVerb = ConstValue(ProviderSource, "CheckVerb");
			string body = Between(ProviderSource,
				"private static readonly string[] Script = {", "};");
			List<string> steps = new List<string>();
			foreach (string rawToken in body.Split(','))
			{
				string token = rawToken.Trim().Replace("\r", "").Replace("\n", "").Trim();
				if (token.Length == 0) continue;
				if (token.StartsWith("\"", StringComparison.Ordinal)
					&& token.EndsWith("\"", StringComparison.Ordinal))
				{
					steps.Add(token.Substring(1, token.Length - 2));
				}
				else if (token == "SetupVerb") steps.Add(setupVerb);
				else if (token == "CheckVerb") steps.Add(checkVerb);
				else Assert.Fail("unrecognised Script token: " + token);
			}
			return steps;
		}

		private static List<string> PersonaScriptSteps(string PersonaSource)
		{
			foreach (string line in PersonaSource.Split('\n'))
			{
				string trimmed = line.Trim();
				if (trimmed.StartsWith("SCRIPT=", StringComparison.Ordinal))
					return trimmed.Substring("SCRIPT=".Length).Split(';').ToList();
			}
			Assert.Fail("persona has no SCRIPT= line");
			return null;
		}

		[Test]
		public void ProviderIsRegisteredWithBothVerbs()
		{
			string provider = Read(Provider);
			Assert.That(provider, Does.Contain("[KingdomScenarioVerbProvider]"));
			Assert.That(provider, Does.Contain(
				"internal const string SetupVerb = \"quote-occupancy-setup\";"));
			Assert.That(provider, Does.Contain(
				"internal const string CheckVerb = \"quote-occupancy-check\";"));
			Assert.That(provider, Does.Contain(
				"KingdomQuoteSitingOccupancyNativeChecks.Run(Verb, game, zone, out complete)"));
		}

		/// <summary>The exact bug from review-0192a82-siting-findings.md finding 3(a): the
		/// persona's SCRIPT and the provider's sealed array must be the identical sequence, or
		/// EVERY verb -- including setup -- refuses "the exact sealed quote-occupancy script is
		/// absent". This parses both files and compares the real step lists, so a future edit to
		/// either one alone, without the other, fails here instead of only natively.</summary>
		[Test]
		public void TheSealedProviderScriptIsExactlyThePersonaScript()
		{
			List<string> providerSteps = ProviderScriptSteps(Read(Provider));
			List<string> personaSteps = PersonaScriptSteps(Read(Persona));
			Assert.That(providerSteps, Is.EqualTo(personaSteps),
				"provider Script = [" + string.Join(";", providerSteps) + "] but persona SCRIPT = ["
				+ string.Join(";", personaSteps) + "]");
			// The persona calls the check verb five times in a row (idempotent, per Checks.cs);
			// pinning the count here means a future edit to either the persona's repeat count or
			// the provider's array length alone is caught even before the list comparison above.
			Assert.That(personaSteps.Count(step => step == "quote-occupancy-check"), Is.EqualTo(5));
			Assert.That(providerSteps.Count(step => step == "quote-occupancy-check"), Is.EqualTo(5));
		}

		/// <summary>The other half of finding 3: <c>Done</c> must be set only when a check
		/// verb actually runs, not synchronously inside Start(), matching
		/// KingdomDepositOverflowNativeChecks (Done only on its own last check) -- otherwise
		/// setup's own "complete" reading overwrites the receipt before the first check verb can
		/// read the plain "intent" text back.</summary>
		[Test]
		public void CompletionIsSetOnlyWhenACheckVerbRuns()
		{
			string checks = Read(Checks);
			string start = Between(checks, "internal void Start()", "internal void Check()");
			Assert.That(start, Does.Not.Contain("Done = true"),
				"Start() must not itself flip Done -- that overwrites the setup receipt");
			string check = checks.Substring(checks.IndexOf("internal void Check()",
				StringComparison.Ordinal));
			Assert.That(check, Does.Contain("Done = true;"));
			Assert.That(checks, Does.Contain("Retained.Check();"),
				"Run() must dispatch to Check() on the check verb, not inline the flag flip");
		}

		[Test]
		public void PersonaDisclosesSyntheticSetupAndNativeNonExecution()
		{
			string persona = Read(Persona);
			Assert.That(persona, Does.Contain("REQUEST=founding-first-city"));
			Assert.That(persona, Does.Contain(
				"VERBS=quote-occupancy-setup,quote-occupancy-check"));
			Assert.That(persona, Does.Contain("SYNTHETIC SETUP, DISCLOSED"));
			Assert.That(persona, Does.Contain("NOT YET NATIVELY RUN"));
			Assert.That(persona, Does.Contain("case=occupied-first-clear-alternate"));
			Assert.That(persona, Does.Contain("alternate-chosen=True"));
			Assert.That(persona, Does.Contain("case=all-occupied-no-mutation"));
			Assert.That(persona, Does.Contain("case=drift-after-quote-preflight-refused"));
		}
	}
}
#endif
