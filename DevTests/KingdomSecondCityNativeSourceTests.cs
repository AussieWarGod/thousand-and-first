#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// SOURCE PINS ONLY for behavioural coverage row 12 "Multiple cities". These prove the
	/// provider, its checks and the persona agree with each other and with the production
	/// entries and predicates they claim to drive or mirror. They are NOT native acceptance:
	/// row 12's status stays implemented-unexecuted until a typed native evidence id exists.
	/// </summary>
	public class KingdomSecondCityNativeSourceTests
	{
		private const string Provider = "Harness/KingdomSecondCityNativeProvider.cs";
		private const string Checks = "Harness/KingdomSecondCityNativeChecks.cs";
		private const string Cases = "Harness/KingdomSecondCityNativeCases.cs";
		private const string Site = "Harness/KingdomSecondCityNativeSite.cs";
		private const string Persona = "Tools/personas/second-city-native-check.persona";
		private const string Matrix = "Tools/personas/persona_matrix.py";

		/// <summary>The held-ground sentence both production and the cases must carry verbatim.</summary>
		private const string HeldGroundRefusal =
			"This ground already carries a completed second-city publication.";

		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		/// <summary>Source with every space, tab and line break removed, for multi-line pins.</summary>
		private static string Squash(string Source)
		{
			System.Text.StringBuilder kept = new System.Text.StringBuilder(Source.Length);
			foreach (char c in Source)
				if (c != ' ' && c != '\t' && c != '\r' && c != '\n') kept.Append(c);
			return kept.ToString();
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
		public void OneOrdinaryTurnSeparatesRealizeFromSetupBecauseTheWakeIsConsumedOnTheLatchTick()
		{
			// The production law that makes the turn necessary: the wake is refused on the tick
			// the latch changed, and the seat exchange on activation sits behind that wake.
			Assert.That(Read("Core/KingdomMaster.cs"),
				Does.Contain("return decision.AutomaticWorkAllowed && decision.ChangedAtTick != now;"));
			Assert.That(Read("Core/KingdomMaster.cs"),
				Does.Contain("&& (now < 0L || system.MasterOptionTick != now);"));
			string events = Read("Core/KingdomSystem.z20.Events.cs");
			int wake = events.IndexOf("if (!KingdomMaster.ObserveAutomaticWake(this, game.TimeTicks))",
				events.IndexOf("public override bool HandleEvent(ZoneActivatedEvent E)"));
			Assert.That(wake, Is.GreaterThan(0));
			Assert.That(events.IndexOf("if (TrySeat(E.Zone))", wake), Is.GreaterThan(wake));
			Assert.That(KingdomSecondCityScript.Steps[2], Is.EqualTo(KingdomSecondCityScript.SettleStep));
			Assert.That(KingdomSecondCityScript.SettleStep, Is.EqualTo("advance 1"));
			Assert.That(Read(Provider), Does.Contain(
				"Require(KingdomMaster.AutomaticWorkAllowed(game.GetSystem<KingdomSystem>()),"));
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
			// FoundSecond is exactly this delegation, so the cases drive the same transaction
			// while keeping its failure sentence.
			Assert.That(Squash(Read("Core/KingdomFounding.03.SiteJudgmentAndStyle.cs")), Does.Contain(
				Squash("public static bool FoundSecond(string Name, string Vocation, Zone Site, bool Force = false)"
					+ "{ string failure; return KingdomFoundingTransaction.TryFoundSecondWithoutWater("
					+ "Name, Vocation, Site, Force, out failure); }")));
			string source = Read(Cases);
			Assert.That(source, Does.Contain("KingdomFoundingTransaction.TryFoundSecondWithoutWater("));
			Assert.That(source, Does.Contain("Force: false, Failure: out failure"));
			Assert.That(source, Does.Not.Contain("Force: true"));
			// The cases name TrySeat in prose only: none of them may CALL it, or the seat
			// exchange the return leg claims to observe would be the harness's own doing.
			Assert.That(source, Does.Not.Contain("TrySeat("));
			Assert.That(source, Does.Not.Contain("ClaimZone("));
		}

		[Test]
		public void TheHeldGroundRefusalIsBoundToProductionsExactSentence()
		{
			Assert.That(Read("Core/KingdomFoundingTransaction.04DirectSecond.cs"), Does.Contain(
				"Failure = \"" + HeldGroundRefusal + "\";"));
			Assert.That(Read(Cases), Does.Contain("Require(failure == HeldGroundRefusal,"));
			Assert.That(Squash(Read(Cases)), Does.Contain(Squash(
				"internal const string HeldGroundRefusal = \"" + HeldGroundRefusal + "\";")));
		}

		[Test]
		public void EveryTabledVerdictTheCasesBindIsOneProductionDeclares()
		{
			string verdicts = Read("Core/KingdomSettlement.Vocations.cs");
			string source = Read(Cases) + Read(Checks) + Read(Site);
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
			IList<string> steps = PersonaScriptSteps();
			int setup = steps.IndexOf(KingdomSecondCityScript.SetupVerb);
			Assert.That(setup, Is.GreaterThan(0));
			for (int i = setup; i < steps.Count; i++)
				Assert.That(steps[i].StartsWith("advance"), Is.False, steps[i]);
			int advances = 0;
			foreach (string step in steps) if (step.StartsWith("advance")) advances++;
			Assert.That(advances, Is.EqualTo(1));
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
			Assert.That(persona, Does.Contain("one ordinary turn (advance 1)"));
		}

		[Test]
		public void TheProviderAndTheChecksDiscloseThatNothingHasRunNatively()
		{
			Assert.That(Read(Provider), Does.Contain("NOT YET NATIVELY RUN"));
			Assert.That(Read(Checks), Does.Contain("NOT YET NATIVELY RUN"));
		}

		[Test]
		public void TheSiteSearchIsBoundedAndNamesItsProbeLimitWhenItRefuses()
		{
			// Rings start at the nearest world parasang; JudgeSite alone decides closeness
			// (KingdomSecondCitySiteAdjacencyTests).
			Assert.That(KingdomSecondCitySiteRules.MinRing, Is.EqualTo(1));
			Assert.That(KingdomSecondCitySiteRules.MaxProbes, Is.EqualTo(8));
			string source = Read(Site);
			Assert.That(source, Does.Contain("probes < KingdomSecondCitySiteRules.MaxProbes"));
			Assert.That(source, Does.Contain("GroundIsForeignFaction"));
			Assert.That(source, Does.Contain(
				"Refusal = KingdomSecondCitySiteRules.Refusal(probes, candidates.Count);"));
			Assert.That(KingdomSecondCitySiteRules.Refusal(8, 62), Does.StartWith(
				"no eligible second-city site: probed 8 of at most 8 parasangs (62 candidates"));
		}

		[Test]
		public void TheRiteGroundIsJudgedByProductionsOwnFoundingHeartPredicates()
		{
			string site = Read(Site);
			Assert.That(site, Does.Contain(
				"if (!TryRite(System, zone, key, entry.Category, homeCell.X, homeCell.Y, out rite,"));
			Assert.That(site, Does.Contain(
				"string key = KingdomPlotRules.HeartKeyForRung(KingdomSecondCitySiteRules.FoundingRung);"));
			Assert.That(site, Does.Contain("KingdomData.TryGetBuilding(key, out entry)"));
			Assert.That(site, Does.Contain("KingdomSecondCitySiteRules.RiteOrder(PreferredX, PreferredY,"));
			Assert.That(site, Does.Contain("new KingdomPlots.GroundGrid(Zone)"));
			Assert.That(site, Does.Contain("Grid.KindAt(x, y) == KingdomPlotRules.GroundKind.Liquid"));
			// Every predicate is actually applied to each offered rite, not merely declared.
			Assert.That(site, Does.Contain("if (cell == null || cell.Objects.Count != 0) { occupied++; continue; }"));
			Assert.That(site, Does.Contain("if (!KingdomSecondCitySiteRules.TryRiteHeartRect(x, y, Zone.Width, Zone.Height,"));
			Assert.That(site, Does.Contain("if (!Dry(grid, rect)) { wet++; continue; }"));
			Assert.That(site, Does.Contain(
				"if (!KingdomArchitectureRuntime.TryPrepareFoundingHeart(System, Zone, rect, Key,"));
			Assert.That(KingdomSecondCitySiteRules.FoundingRung, Is.EqualTo(1));
			// The production founding draft these mirror, in its own words.
			string heart = Read("Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs");
			Assert.That(heart, Does.Contain("string key = KingdomPlotRules.HeartKeyForRung(1);"));
			Assert.That(heart, Does.Contain("KingdomPlotRules.TrySurveyedHeart(RiteX, RiteY, Z.Width, Z.Height,"));
			Assert.That(heart, Does.Contain("KingdomPlotRules.HeartSizeForRung(1), out KingdomPlotRules.PlotRect rect)"));
			Assert.That(heart, Does.Contain("if (grid.KindAt(x, y) == KingdomPlotRules.GroundKind.Liquid) return false;"));
			Assert.That(Squash(heart), Does.Contain(Squash(
				"KingdomArchitectureRuntime.TryPrepareFoundingHeart(System, Z, rect, key, entry.Category, RiteX, RiteY,")));
			Assert.That(Read("Growth/KingdomArchitectureRuntime.FoundingHeart.cs"),
				Does.Contain("if (basinX != RiteX || basinY != RiteY) continue;"));
			Assert.That(Read("Growth/KingdomArchitectureRuntime.FoundingHeart.cs"),
				Does.Contain("if (!TryVerifyPhysicalIngressRoutes(Z, Rect, snapshot, out Failure)) return false;"));
		}

		[Test]
		public void TheFoundingHeartTierHasOneVariantSoTheCurrentSeatResolvesTheNewSeatsLayout()
		{
			string civic = Read("Architecture/KingdomArchitectures-CivicFaith.xml");
			int start = civic.IndexOf("<tier Key=\"heartbasin\" BuildKey=\"heartbasin\"");
			Assert.That(start, Is.GreaterThan(0));
			int end = civic.IndexOf("</tier>", start);
			Assert.That(end, Is.GreaterThan(start));
			string tier = civic.Substring(start, end - start);
			int variants = 0;
			for (int at = tier.IndexOf("<variant "); at >= 0; at = tier.IndexOf("<variant ", at + 1))
				variants++;
			Assert.That(variants, Is.EqualTo(1));
			Assert.That(tier, Does.Contain("<variant Key=\"fallback\" Priority=\"0\" />"));
			Assert.That(Read(Site), Does.Contain("the founding heart's tier has one fallback variant"));
		}
	}
}
#endif
