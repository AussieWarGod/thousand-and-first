#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Production-contract preflight for the ordinary tier-upgrade seam (behaviour-coverage row
	/// 8), checked before any costly native run (docs/DEVELOPMENT.md): the setup builds the state
	/// its first read needs, the readiness observation asks production's own inputs and keeps the
	/// refusal reason, every forbidden log line is one production writes to Player.log on this
	/// path, and the coverage note claims only the one chain the persona drives.
	/// </summary>
	public partial class KingdomTierUpgradeNativeSourceTests
	{
		/// <summary>
		/// Straight after the founding transaction the founding heart is only a Staked works: the
		/// survey lists as built only objects carrying KingdomBuilt, which the final building
		/// receives when its plot completes, and the authored fixture:storage stockpile exists
		/// only on that final layout. So the setup completes rung one through the shared
		/// completed-heart helper (as the natively-run camp-heart fixture does) BEFORE it
		/// dedicates water or binds the heart and its store, and refuses unless rung one stands.
		/// </summary>
		[Test]
		public void TheSetupCompletesTheFoundingHeartBeforeBindingItsStore()
		{
			string fixture = Read(Fixture);
			int found = fixture.IndexOf("KingdomNativeCampFounding.Found(Game, Zone, Require);",
				StringComparison.Ordinal);
			int complete = fixture.IndexOf(
				"KingdomScenarioCompletedHeart.Complete(Game, System, Zone);",
				StringComparison.Ordinal);
			int rung = fixture.IndexOf("KingdomPlots.HeartRung(Zone) == 1", StringComparison.Ordinal);
			int dedicate = fixture.IndexOf("KingdomNativeCampFounding.Dedicate(",
				StringComparison.Ordinal);
			int bind = fixture.IndexOf("BindHeartAndStore();", StringComparison.Ordinal);
			Assert.That(found, Is.GreaterThan(-1), "the real founding");
			Assert.That(complete, Is.GreaterThan(found), "rung one completes after the founding");
			Assert.That(rung, Is.GreaterThan(complete), "rung one is proved after completion");
			Assert.That(dedicate, Is.GreaterThan(rung), "water is dedicated after rung one stands");
			Assert.That(bind, Is.GreaterThan(dedicate), "the heart and its store are bound last");
			Assert.That(fixture, Does.Contain("KingdomUpgrade.DesignKeyOf(Heart) == FoundingHeartKey"));
			Assert.That(Read(Checks), Does.Contain(
				"internal const string FoundingHeartKey = \"heartbasin\";"));
			// The production contract the order answers to.
			Assert.That(Read("Growth/KingdomSurvey.01.Capture.cs"), Does.Contain(
				"row.Built = !pendingImprovement && Item.GetIntProperty(\"KingdomBuilt\") == 1;"));
			string final = Read("Growth/KingdomPlot2.27.FinalBuilding.cs");
			Assert.That(final, Does.Contain("Building.SetIntProperty(\"KingdomBuilt\", 1);"));
			Assert.That(final, Does.Contain("if (Heart) Building.SetIntProperty(HeartPlotProperty, 1);"));
			// The helper drives only the founding heart's own fresh rung-one works.
			string helper = Read("Harness/KingdomScenarioCompletedHeart.cs");
			Assert.That(helper, Does.Contain("Works.DesignKey == \"heartbasin\""));
			Assert.That(helper, Does.Contain(
				"KingdomPlots.Advance(witness.Works, system, completionTick);"));
		}

		/// <summary>
		/// The readiness observation asks production's assessment with the inputs the settlement
		/// pass itself computes - free hands from the population less the assigned crew, and
		/// competing work from every working improvement plus every built work whose receipt
		/// still blocks - never an assumed empty queue, keeps the verdict's reason and those
		/// inputs, and asks Begin's read-only zoning gate before the long leg rather than after
		/// it. When the ready-looking begin never happens, the second check journals a fresh
		/// verdict and reason, the inputs, the tent's announced verdict and the ledger tail
		/// before it refuses: Begin's zoning and outstanding-funding outcomes reach only the
		/// in-memory ledger, never Player.log.
		/// </summary>
		[Test]
		public void TheReadinessObservationUsesProductionInputsAndKeepsTheReason()
		{
			string shortfall = Read(Shortfall);
			Assert.That(shortfall, Does.Not.Contain("survey, hands, false)"), "assumed empty queue");
			Assert.That(shortfall, Does.Contain(
				"root.GetPart<r_KingdomImprovement>()?.Working == true"));
			Assert.That(shortfall, Does.Contain("KingdomConstruction.ReceiptBlocksCurrent(root)"));
			Assert.That(shortfall, Does.Contain(
				"int free = Math.Max(0, System.Population - System.AssignedCrew);"));
			Assert.That(shortfall, Does.Contain("survey, free, competing.Count > 0);"));
			foreach (string input in new[] { "\"; competing=\"", "\"; announced=\"",
				"KingdomUpgrade.Enabled", "KingdomMaster.AutomaticWorkAllowed(System)",
				"KingdomZoning.Judge(System, Zone.ZoneID, assessment.Successor)",
				"\"; ledger-notes=\"" })
				Assert.That(shortfall, Does.Contain(input), input);
			Assert.That(shortfall, Does.Contain(
				"ready.Verdict + \" / \" + KingdomScenarioRules.Bounded(ready.Reason)"));
			Assert.That(Follows(shortfall, "Assess(tent, out readyContext);",
				".Append(readyContext).Append(LedgerTail());", 120), Is.True,
				"the ready inputs are journaled before the verdict is required");
			// Begin's read-only zoning gate is asked before the long leg, not discovered after it.
			Assert.That(shortfall, Does.Contain(
				"KingdomZoning.Permits(System, Zone.ZoneID, ready.Successor, out zoning)"));
			Assert.That(Read("Growth/KingdomUpgrade.14.Begin.cs"), Does.Contain(
				"if (!KingdomZoning.Permits(System, Z.ZoneID, A.Successor,"));
			Assert.That(Read("Growth/KingdomUpgradeRules.Assessment.cs"), Does.Contain(
				"public static bool CraftGateAdmits(ZoningVerdict Verdict)"));
			string phases = Read(Phases);
			Assert.That(Follows(phases, "if (!bound) RecordUnbegun(tent);",
				"\"the settlement pass bound no improvement receipt to the tent\"", 160), Is.True);
			Assert.That(phases, Does.Contain(".Append(KingdomScenarioRules.Bounded(fresh.Reason))"));
			// The production inputs it mirrors, and the ledger-only Begin refusal it must journal.
			string resolve = Read("Growth/KingdomUpgrade.13.Resolve.cs");
			Assert.That(resolve, Does.Contain("if (improvement != null && improvement.Working)"));
			Assert.That(resolve, Does.Contain(
				"if (HasActiveConstruction(works[i])) otherWorkUnderway = true;"));
			Assert.That(resolve, Does.Contain("Assessment assessment = Assess(System, Z, works[i], "
				+ "Survey, freeHands, otherWorkUnderway);"));
			Assert.That(Read("Growth/KingdomUpgrade.08.ConstructionInspect.cs"), Does.Contain(
				"return KingdomConstruction.ReceiptBlocksCurrent(Work);"));
			Assert.That(Read("Growth/KingdomUpgrade.14.Begin.cs"), Does.Contain(
				"System.Ledger.Note(\"{{r|The improvement waits. \" + zoningFailure + \"}}\");"));
		}

		/// <summary>When no completed tent stands after leg one, the first check journals the
		/// commission receipt's phase, due tick and recorded failure, the works' stage and the
		/// crew inputs before it refuses (docs/DEVELOPMENT.md: inspect the recorded construction
		/// reason before any longer wait).</summary>
		[Test]
		public void AnUnbuiltTentJournalsItsConstructionReceiptBeforeRefusing()
		{
			string phases = Read(Phases);
			Assert.That(Follows(phases, "if (tent == null) RecordUnbuilt();",
				"\"no completed settlement tent stands after the build wait\"", 120), Is.True);
			foreach (string field in new[] { "\"; phase=\"", "\"; due=\"", "\"; failure=\"",
				"\"; works-stage=\"", "\"; assigned=\"" })
				Assert.That(phases, Does.Contain(field), field);
		}

		/// <summary>
		/// Every LOG_FORBID entry is a line production writes to Player.log through KingdomLog.Log
		/// on this path. The forbidden-log scan reads Player.log only (Tools/run-personas.sh), so
		/// a ledger-only sentence, or a refusal from a branch this fixed-envelope climb never
		/// enters, would be a guard that can never fire.
		/// </summary>
		[Test]
		public void EveryForbiddenLogLineIsOneProductionWritesToPlayerLog()
		{
			var emitters = new Dictionary<string, string>
			{
				{ "improvement refused cleanly:", "Growth/KingdomUpgrade.14.Begin.cs" },
				{ "construction: improvement projection waits:", "Growth/KingdomUpgrade.14.Begin.cs" },
				{ "seal: settlement pass was not staged", "Core/KingdomSystem.z21.SemanticPass.cs" },
				{ "construction: founding heart recovery requires inspection",
					"Growth/KingdomConstruction.Settlement.cs" },
			};
			string line = Read(Persona).Split('\n').Single(
				row => row.StartsWith("LOG_FORBID=", StringComparison.Ordinal)).Substring(11).Trim();
			string[] forbidden = JsonSerializer.Deserialize<string[]>(line);
			Assert.That(forbidden, Is.Not.Null.And.Not.Empty);
			foreach (string literal in forbidden)
			{
				Assert.That(emitters.ContainsKey(literal), Is.True, "no proved emitter: " + literal);
				Assert.That(Read(emitters[literal]), Does.Contain("KingdomLog.Log(\"" + literal),
					literal);
			}
			// The outstanding-funding sentence is ledger-only: KingdomLedger.Note keeps it in memory.
			Assert.That(Read("Growth/KingdomUpgrade.14.Begin.cs"), Does.Contain(
				"System.Ledger.Note(\"{{r|The improvement receipt remains outstanding."));
			Assert.That(forbidden.Any(literal => literal.Contains("remains outstanding")), Is.False);
		}

		/// <summary>
		/// The coverage note scopes this driver to the one chain it drives. The catalogue ships
		/// many ordinary (non-heart) upgrade chains, so the note names tent -> tentrow as one of
		/// exactly that many and never as the only one: a native PASS on this persona covers this
		/// chain, not ordinary tier replacement at large.
		/// </summary>
		[Test]
		public void TheCoverageNoteScopesTheDriverToOneOfTheShippedOrdinaryChains()
		{
			List<string> chains = XDocument.Parse(Read("RuntimeData/KingdomBuildings.xml"))
				.Descendants("building").Where(b => b.Attribute("UpgradesTo") != null)
				.Select(b => (string)b.Attribute("Key")).ToList();
			int ordinary = chains.Count(key => KingdomPlotRules.HeartRungOf(key) == 0);
			Assert.That(chains, Does.Contain("tent"));
			Assert.That(ordinary, Is.GreaterThan(1), "the catalogue ships one ordinary chain only");
			string scope = "tent -> tentrow only, one of " + ordinary + " shipped non-heart chains";
			foreach (string path in new[] { "Tools/coverage/matrix.json",
				"docs/BEHAVIOUR_COVERAGE.md" })
			{
				string text = Read(path);
				Assert.That(text, Does.Contain(scope), path);
				Assert.That(text, Does.Not.Contain("the only shipped ordinary chain"), path);
			}
		}
	}
}
#endif
