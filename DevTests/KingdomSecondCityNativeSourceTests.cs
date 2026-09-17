#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// SOURCE PINS ONLY for behavioural coverage row 12 "Multiple cities". These prove the
	/// provider, its checks and the persona agree with each other and with the production
	/// entries they claim to drive. They are NOT native acceptance: row 12's status stays
	/// implemented-unexecuted until a typed native evidence id exists.
	/// </summary>
	public class KingdomSecondCityNativeSourceTests
	{
		private const string Provider = "Harness/KingdomSecondCityNativeProvider.cs";
		private const string Checks = "Harness/KingdomSecondCityNativeChecks.cs";
		private const string Cases = "Harness/KingdomSecondCityNativeCases.cs";
		private const string Script = "Harness/KingdomSecondCityScript.cs";
		private const string SiteRules = "Harness/KingdomSecondCitySiteRules.cs";
		private const string Persona = "Tools/personas/second-city-native-check.persona";
		private const string Matrix = "Tools/personas/persona_matrix.py";

		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		private static IList<string> PersonaScriptSteps()
		{
			foreach (string line in Read(Persona).Split('\n'))
			{
				string trimmed = line.TrimEnd('\r');
				if (!trimmed.StartsWith("SCRIPT=")) continue;
				return new List<string>(trimmed.Substring("SCRIPT=".Length).Split(';'));
			}
			Assert.Fail("the second-city persona declares no SCRIPT= line");
			return null;
		}

		[Test]
		public void TheSealedProviderScriptIsExactlyThePersonaScript()
		{
			Assert.That(PersonaScriptSteps(), Is.EqualTo(
				new List<string>(KingdomSecondCityScript.Steps)));
		}

		[Test]
		public void TheProviderIsRegisteredAndClaimsBothScriptVerbs()
		{
			string source = Read(Provider);
			Assert.That(source, Does.Contain("[KingdomScenarioVerbProvider]"));
			Assert.That(source, Does.Contain("IKingdomScenarioVerbProvider"));
			Assert.That(source, Does.Contain("KingdomScenarioVerbApi.Version"));
			Assert.That(source, Does.Contain("new[] { SetupVerb, CheckVerb }"));
			Assert.That(KingdomSecondCityScript.SetupVerb, Is.EqualTo("second-city-setup"));
			Assert.That(KingdomSecondCityScript.CheckVerb, Is.EqualTo("second-city-check"));
		}

		[Test]
		public void TheProviderRefusesAnythingButItsOwnSealedScript()
		{
			Assert.That(Read(Provider), Does.Contain("KingdomSecondCityScript.Matches(script)"));
		}

		[Test]
		public void ThePersonaRunsAfterRealizeAndTheEligibilityWallRequiresOneFoundedCity()
		{
			Assert.That(KingdomSecondCityScript.Steps[1], Is.EqualTo("realize"));
			string source = Read(Provider);
			Assert.That(source, Does.Contain("system.Founded"));
			Assert.That(source, Does.Contain("system.SettlementCount == 1"));
			Assert.That(source, Does.Contain("system.NonSeatSettlementCount == 0"));
			Assert.That(source, Does.Contain("plan.Key == \"founding-first-city\""));
		}

		[Test]
		public void APerCaseFailureRefusesTheVerbInsteadOfReportingGreen()
		{
			Assert.That(Read(Provider),
				Does.Contain("Ok = KingdomSecondCityNativeChecks.Ok;"));
			Assert.That(Read(Checks), Does.Contain("internal static bool Ok { get { return Failed == 0; } }"));
		}

		[Test]
		public void TheSecondFoundingDrivesTheProductionTransactionAndNeverForcesIt()
		{
			string source = Read(Cases);
			Assert.That(source, Does.Contain("KingdomFounding.FoundSecond"));
			Assert.That(source, Does.Contain("Force: false"));
			Assert.That(source, Does.Not.Contain("Force: true"));
			Assert.That(source, Does.Not.Contain("System.TrySeat"));
			Assert.That(source, Does.Not.Contain("ClaimZone"));
		}

		[Test]
		public void EveryTabledVerdictTheCasesBindIsOneProductionDeclares()
		{
			string verdicts = Read("Core/KingdomSettlement.Vocations.cs");
			string source = Read(Cases) + Read(Checks);
			foreach (string verdict in new[] { "Allowed", "GroundIsAlreadyOurs",
				"GroundIsTooClose" })
			{
				Assert.That(verdicts, Does.Contain("SecondFoundingVerdict." + verdict));
				Assert.That(source, Does.Contain("SecondFoundingVerdict." + verdict));
			}
		}

		[Test]
		public void TheReturnLegAssertsProductionsOwnSeatExchange()
		{
			string source = Read(Cases);
			Assert.That(source, Does.Contain("returning to the first city did not bring its seat back"));
			Assert.That(Read("Core/KingdomSystem.z20.Events.cs"), Does.Contain("TrySeat(E.Zone)"));
		}

		[Test]
		public void NoCaseMaySpendAGameTurn()
		{
			string source = Read(Cases);
			Assert.That(source, Does.Contain("a second-city case advanced world time"));
			Assert.That(source, Does.Contain("Game.Turns == KingdomSecondCityNativeChecks.Turns"));
			Assert.That(source, Does.Contain("Game.TimeTicks == KingdomSecondCityNativeChecks.Ticks"));
			Assert.That(PersonaScriptSteps(), Has.No.Member("advance"));
		}

		[Test]
		public void BothEvidenceRowsAreJournalledAndAllowlisted()
		{
			string source = Read(Checks) + Read(Cases);
			Assert.That(source, Does.Contain("KingdomScenarioJournal.Append(KingdomSecondCityScript.SiteRow"));
			Assert.That(source, Does.Contain("KingdomScenarioJournal.Append(KingdomSecondCityScript.TopologyRow"));
			string matrix = Read(Matrix);
			Assert.That(matrix, Does.Contain("SECOND_CITY_EVIDENCE_ROWS"));
			Assert.That(matrix, Does.Contain("\"" + KingdomSecondCityScript.SiteRow + "\""));
			Assert.That(matrix, Does.Contain("\"" + KingdomSecondCityScript.TopologyRow + "\""));
			Assert.That(matrix, Does.Contain("and verb not in SECOND_CITY_EVIDENCE_ROWS"));
		}

		[Test]
		public void ThePersonaDisclosesItsSyntheticSetupAndItsUncoveredScope()
		{
			string persona = Read(Persona);
			Assert.That(persona, Does.Contain("SYNTHETIC SETUP, DISCLOSED"));
			Assert.That(persona, Does.Contain("NOT COVERED, DISCLOSED"));
			Assert.That(persona, Does.Contain("save -> cold load"));
			Assert.That(persona, Does.Contain("Force FALSE"));
			Assert.That(persona, Does.Contain("zero-energy SystemMoveTo"));
		}

		[Test]
		public void TheProviderAndTheChecksDiscloseThatNothingHasRunNatively()
		{
			Assert.That(Read(Provider), Does.Contain("NOT YET NATIVELY RUN"));
			Assert.That(Read(Checks), Does.Contain("NOT YET NATIVELY RUN"));
		}

		[Test]
		public void TheSiteSearchIsBoundedAndStartsOutsideTheBorderingBand()
		{
			Assert.That(KingdomSecondCitySiteRules.MinRing, Is.EqualTo(2));
			string source = Read(Checks);
			Assert.That(source, Does.Contain("internal const int MaxProbes = 8;"));
			Assert.That(source, Does.Contain("probes < MaxProbes"));
			Assert.That(source, Does.Contain("GroundIsForeignFaction"));
			Assert.That(source, Does.Contain("no eligible second-city site within"));
		}
	}
}
#endif
