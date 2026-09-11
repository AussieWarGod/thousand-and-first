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
		// The three case bodies (and TryFindManagedCell/RawTimber) moved to their own partial-
		// class file to keep KingdomQuoteSitingOccupancyNativeChecks.cs under the harness line
		// cap once case 3 grew to trace and name two possible refusals.
		private const string Cases = "Harness/KingdomQuoteSitingOccupancyNativeChecks.Cases.cs";
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

		/// <summary>Native run of ad75a8a: setup and the FIRST check passed 3/3, but the SECOND
		/// check refused "the quote-occupancy owner intent is absent or torn" and the script
		/// stopped -- the provider unconditionally required the receipt to still read "intent"
		/// before every Run() call, but the first completing check had already overwritten it
		/// with the report. Fixed: the provider now reads Completed BEFORE calling Run() and
		/// checks the receipt against "intent" only pre-completion; once already complete, it
		/// re-verifies the receipt against the (byte-identical, nothing-mutated) recomputed
		/// report instead, and never writes the receipt again. Because Check() only ever sets a
		/// bool and no case re-runs, the recomputed report is provably the same string every
		/// time, so two consecutive checks are guaranteed to emit an identical journal line by
		/// construction -- pinned here as a source/value fact rather than executed (DevTests has
		/// no real Zone/GameObject to run a genuine Frame).</summary>
		[Test]
		public void RepeatCheckCallsReVerifyTheReportInsteadOfIntentAndNeverRewriteTheReceipt()
		{
			string checks = Read(Checks);
			Assert.That(checks, Does.Contain(
				"internal static bool Completed { get { return Retained != null && Retained.Done; } }"));
			// Check() has exactly one statement -- setting the flag -- so a repeat call mutates
			// nothing and Passed/Failed/Evidence (and therefore the recomputed report string)
			// cannot differ between the first and any later completing call.
			string check = Between(checks, "internal void Check()\n\t\t\t{", "}");
			Assert.That(check.Trim(), Is.EqualTo("Done = true;"),
				"Check() must do nothing but flip the flag, or repeat calls are no longer provably identical");

			string provider = Read(Provider);
			Assert.That(provider, Does.Contain(
				"bool alreadyComplete = KingdomQuoteSitingOccupancyNativeChecks.Completed;"));
			Assert.That(provider, Does.Contain("else if (!alreadyComplete)"));
			Assert.That(provider, Does.Contain(
				"the quote-occupancy owner intent is absent or torn"));
			Assert.That(provider, Does.Contain("if (alreadyComplete)"));
			Assert.That(provider, Does.Contain(
				"Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, result),\n"
				+ "\t\t\t\t\t\t\"the quote-occupancy report was torn between idempotent checks\");"));
			// The idempotent branch must never call SetStringGameState -- a repeat check proves
			// continuity, it does not re-write.
			string idempotentBranch = Between(provider, "if (alreadyComplete)\n\t\t\t\t{",
				"else if (complete)");
			Assert.That(idempotentBranch, Does.Not.Contain("SetStringGameState"),
				"an idempotent repeat check must never re-write the durable receipt");
			// Two DISTINCT named refusals, one per regime, so a genuinely torn receipt still
			// refuses by name whether it tears before completion (against "intent") or after
			// (against the frozen report) -- neither text is a substring of the other.
			Assert.That("the quote-occupancy owner intent is absent or torn",
				Does.Not.Contain("the quote-occupancy report was torn between idempotent checks"));
			Assert.That("the quote-occupancy report was torn between idempotent checks",
				Does.Not.Contain("the quote-occupancy owner intent is absent or torn"));
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

		/// <summary>Native run of bd9bbcd threw instead of journaling: production DID refuse (the
		/// plan-changed guard, nothing spent) but earlier than the living-occupant preflight text
		/// the case demanded by exact substring, and the exception escaped the whole verb. Fixed
		/// per review: this pins that case 3 now accepts EITHER named text (never a substring
		/// wildcard), proves nothing spent AND nothing stamped, and journals its outcome instead
		/// of letting a Require escape.</summary>
		[Test]
		public void DriftCaseAcceptsEitherNamedRefusalTextAndProvesNothingSpentOrStamped()
		{
			string cases = Read(Cases);
			Assert.That(cases, Does.Contain("private const string PlanChangedText = "
				+ "\"The ground or production plan changed after \""));
			Assert.That(cases, Does.Contain("\"its preview. Review the exact plan again; "
				+ "nothing was spent.\";"));
			Assert.That(cases, Does.Contain("private const string LivingOccupantText = "
				+ "\"a living occupant stands on authored ground at \";"));
			Assert.That(cases, Does.Contain(
				"commitFailure == PlanChangedText"));
			Assert.That(cases, Does.Contain(
				"commitFailure.StartsWith(LivingOccupantText, StringComparison.Ordinal)"));
			Assert.That(cases, Does.Contain(
				"Require(!committed && (isPlanChanged || isLivingOccupant),"));
			Assert.That(cases, Does.Contain("int builtBefore = KingdomPlots.CountBuilt(Zone);"));
			Assert.That(cases, Does.Contain("int builtAfter = KingdomPlots.CountBuilt(Zone);"));
			Assert.That(cases, Does.Contain(
				"a refused drift-after-quote commission still stamped a component"));
			Assert.That(cases, Does.Contain(
				"a refused drift-after-quote commission spent timber or water"));
			Assert.That(cases, Does.Contain(
				"; case=drift-after-quote-preflight-refused refused=true refusal=\")"));
			// The exact bug: an unconditional single-substring StartsWith against only the
			// living-occupant text must never return.
			Assert.That(cases, Does.Not.Contain(
				"commitFailure.StartsWith(\"a living occupant stands on authored ground at \","));
		}

		/// <summary>The other half of the same bug: a Require failure inside one case escaped as
		/// an exception and aborted the whole setup verb (native run bd9bbcd, verb REFUSED with
		/// "InvalidOperationException:..."). Every case now runs through RunCase, which catches
		/// and counts rather than propagating, so the setup verb's own cases=/passed=/failed=
		/// line reflects real per-case outcomes instead of a hardcoded "3 failed=0".</summary>
		[Test]
		public void EveryCaseIsCaughtByRunCaseSoOneFailureNeverAbortsTheOthers()
		{
			string checks = Read(Checks);
			Assert.That(checks, Does.Contain("private void RunCase(string Name, Action Body)"));
			Assert.That(checks, Does.Contain("catch (Exception error)"));
			Assert.That(checks, Does.Contain("Failed++;"));
			Assert.That(checks, Does.Contain("Passed++;"));
			Assert.That(checks, Does.Contain(
				"RunCase(\"occupied-first-clear-alternate\","));
			Assert.That(checks, Does.Contain(
				"RunCase(\"all-occupied-no-mutation\", () => AllOccupiedNoMutation(system, entry));"));
			Assert.That(checks, Does.Contain(
				"RunCase(\"drift-after-quote-preflight-refused\","));
			Assert.That(checks, Does.Contain(
				"cases=3 passed=\" + Retained.Passed"));
			Assert.That(checks, Does.Contain("+ \" failed=\" + Retained.Failed + Retained.Evidence;"));
			Assert.That(checks, Does.Not.Contain("passed=\" + (Complete ? \"3 failed=0\""),
				"the verb's own summary line must report real counts, never a hardcoded 3/0");
		}
	}
}
#endif
