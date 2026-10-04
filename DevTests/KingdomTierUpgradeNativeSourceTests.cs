#if TAF_TESTS
using System;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Wiring tripwires for the ordinary tier-upgrade seam (behaviour-coverage row 8). The
	/// persona supplies the actual native evidence; these pins only prove the seam is registered,
	/// that it drives nothing itself, that the persona and the provider seal the same script, and
	/// that every synthetic input is disclosed in the report the run publishes. The production
	/// contract preflight pins live in the <c>.Preflight</c> partial.
	/// </summary>
	public partial class KingdomTierUpgradeNativeSourceTests
	{
		private const string Provider = "Harness/KingdomTierUpgradeProvider.cs";
		private const string Checks = "Harness/KingdomTierUpgradeChecks.cs";
		private const string Fixture = "Harness/KingdomTierUpgradeFixture.cs";
		private const string Phases = "Harness/KingdomTierUpgradePhases.cs";
		private const string Shortfall = "Harness/KingdomTierUpgradeShortfall.cs";
		private const string Persona = "Tools/personas/tier-upgrade-native-check.persona";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		private static string[] Script(string name) =>
			TestMain.ReadRepositoryText("Tools/personas/" + name + ".persona")
				.Split('\n').Single(line => line.StartsWith("SCRIPT=", StringComparison.Ordinal))
				.Substring(7).Split(';');

		/// <summary>True when <paramref name="Later"/> follows <paramref name="First"/> within
		/// <paramref name="Window"/> characters of <paramref name="Text"/>.</summary>
		private static bool Follows(string Text, string First, string Later, int Window)
		{
			int at = Text.IndexOf(First, StringComparison.Ordinal);
			int next = at < 0 ? -1 : Text.IndexOf(Later, at, StringComparison.Ordinal);
			return at >= 0 && next > at && next - at <= Window;
		}

		/// <summary>
		/// The persona and the provider seal the SAME script, word for word, and no one-character
		/// edit, deletion or addition survives the comparison.
		/// </summary>
		[Test]
		public void ThePersonaScriptIsExactlyTheSealedScript()
		{
			string[] sealed_ = Script("tier-upgrade-native-check");
			Assert.That(KingdomTierUpgradeScript.Matches(sealed_), Is.True);
			Assert.That(KingdomTierUpgradeScript.Matches(null), Is.False);
			Assert.That(KingdomTierUpgradeScript.Matches(
				sealed_.Concat(new[] { "stagedigest" }).ToArray()), Is.False);
			for (int i = 0; i < sealed_.Length; i++)
			{
				var changed = (string[])sealed_.Clone();
				changed[i] += " ";
				Assert.That(KingdomTierUpgradeScript.Matches(changed), Is.False, "edit at " + i);
				Assert.That(KingdomTierUpgradeScript.Matches(
					sealed_.Where((_, at) => at != i).ToArray()), Is.False, "cut at " + i);
			}
		}

		/// <summary>
		/// The legs are counted in daily settlement passes. Receipt-bearing work advances only
		/// inside the pass, the pass runs once per 1200-tick day, and a freshly staked or
		/// projected raising's first interval is priced at zero because no crew witness exists
		/// until a pass stamps it. So leg one needs three passes (zero, the priced 900-tick tent,
		/// a spare), leg two exactly two (the begin and the improvement's zero-priced first
		/// interval, so the job is still Working when read), and leg three three (the priced
		/// 900-tick improvement and two spares). A production drift in any of these facts, or a
		/// shortened leg, breaks this pin rather than a costly native run.
		/// </summary>
		[Test]
		public void TheLegsCountSettlementPassesWithAZeroPricedFirstInterval()
		{
			string[] script = Script("tier-upgrade-native-check");
			Assert.That(script[2], Is.EqualTo("advance 3600"), "leg one");
			Assert.That(script[5], Is.EqualTo("advance 2400"), "leg two");
			Assert.That(script[7], Is.EqualTo("advance 3600"), "leg three");
			Assert.That(Read("Simulation/City/KingdomSemanticClockRules.cs"), Does.Contain(
				"public const long CadenceTicks = KingdomRules.TicksPerDay;"));
			Assert.That(Read("Core/KingdomRules.Economy.cs"), Does.Contain(
				"public const long TicksPerDay = 1200L;"));
			Assert.That(Read("Growth/KingdomScaffold.cs"), Does.Contain(
				"Receipt-bearing work advances only from KingdomConstruction.OnSettlementPass."));
			Assert.That(Read("Growth/KingdomPlot2.26.Labour.cs"), Does.Contain(
				"witnessed ? prior.LabourPercent : 0,"));
			Assert.That(Read("Growth/KingdomScaffold.LabourWindow.cs"), Does.Contain(
				"else if (witnessed) pricedEffectiveness = prior.EffectivenessPercent;"));
			foreach (string window in new[] { "Growth/KingdomPlot2.26b.LabourWindow.cs",
				"Growth/KingdomScaffold.LabourWindow.cs" })
				Assert.That(Follows(Read(window), "!= KingdomConstructionPresenceRules.Schema)",
					"effectiveness = 0;", 80), Is.True, window);
		}

		/// <summary>The provider is registered, names its three verbs, seals the script through
		/// the engine-free shard, and dispatches into the checks shard.</summary>
		[Test]
		public void ProviderIsRegisteredWithAllThreeVerbsAndTheSealedScript()
		{
			string provider = Read(Provider);
			Assert.That(provider, Does.Contain("[KingdomScenarioVerbProvider]"));
			Assert.That(provider, Does.Contain(
				"internal const string SetupVerb = \"tier-upgrade-setup\";"));
			Assert.That(provider, Does.Contain(
				"internal const string CheckVerb = \"tier-upgrade-check\";"));
			Assert.That(provider, Does.Contain(
				"internal const string ShortVerb = \"tier-upgrade-short\";"));
			Assert.That(provider, Does.Contain(
				"public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }"));
			Assert.That(provider, Does.Contain("KingdomTierUpgradeScript.Matches(script)"));
			Assert.That(provider, Does.Contain(
				"KingdomTierUpgradeChecks.Run(Verb, game, zone, out complete)"));
			Assert.That(provider, Does.Not.Contain("Popup.Show"));
		}

		/// <summary>
		/// The seam OBSERVES. Behaviour-coverage row 8 is about what the real settlement pass
		/// does, so a shard that called the upgrade or funding entry points itself would be
		/// proving its own arithmetic. None of them may appear anywhere in the seam's own shards;
		/// the setup's rung-one heart completion goes through the shared, disclosed
		/// completed-heart helper and never reaches the tent (pinned in the preflight partial).
		/// </summary>
		[Test]
		public void TheSeamNeverDrivesTheUpgradeItself()
		{
			foreach (string path in new[] { Provider, Checks, Fixture, Phases, Shortfall })
			{
				string text = Read(path);
				foreach (string forbidden in new[] { "KingdomUpgrade.Begin(",
					"KingdomUpgrade.BeginCore(", "KingdomUpgrade.BeginPrepared(",
					"KingdomUpgrade.BeginPreparedPlanChange(", "KingdomUpgrade.TryPreparePlanChange(",
					"KingdomUpgrade.HandOver(", "TryApplyUpgrade(", "KingdomPlots.Advance(",
					"KingdomConstruction.TryFundNew(", "KingdomPlots.GrowInPlace(" })
					Assert.That(text, Does.Not.Contain(forbidden), path + " / " + forbidden);
			}
		}

		/// <summary>The ordinary lane never reaches for heart machinery: an ordinary climb that
		/// settled a rung would be a defect, and a seam that called the rung settler itself could
		/// not notice. Setup completes rung one only through the shared completed-heart helper;
		/// no rung is climbed or settled by the seam.</summary>
		[Test]
		public void TheSeamNeverTouchesHeartRungMachinery()
		{
			foreach (string path in new[] { Provider, Checks, Fixture, Phases, Shortfall })
			{
				string text = Read(path);
				foreach (string forbidden in new[] { "TrySettleHeartRung",
					"TrySettleImprovementHeartRung", "ClearClimbHold", "HeartGrowRefused",
					"HeartAccretion" })
					Assert.That(text, Does.Not.Contain(forbidden), path + " / " + forbidden);
			}
			// The successor is asserted to be OFF the heart ladder, which is the positive half
			// of the same claim.
			Assert.That(Read(Phases), Does.Contain("KingdomPlotRules.HeartRungOf(ToKey) == 0"));
		}

		/// <summary>The frozen quote for tent -> tentrow is pinned as a value contract, so a
		/// catalogue edit cannot quietly change what the native run accepts.</summary>
		[Test]
		public void TheFrozenQuoteIsPinnedToTheCatalogue()
		{
			string phases = Read(Phases);
			Assert.That(phases, Does.Contain("internal const int QuoteDrams = 2;"));
			Assert.That(phases, Does.Contain("internal const long QuoteTicks = 900L;"));
			Assert.That(phases, Does.Contain("internal const int QuoteCrew = 1;"));
			Assert.That(phases, Does.Contain("internal const int QuoteBrush = 2;"));
			string buildings = Read("RuntimeData/KingdomBuildings.xml");
			Assert.That(buildings, Does.Contain("UpgradesTo=\"tentrow\" UpgradeMaterials=\"canvas:2\""));
			Assert.That(Read(Checks), Does.Contain("internal const string FromKey = \"tent\";"));
			Assert.That(Read(Checks), Does.Contain("internal const string ToKey = \"tentrow\";"));
		}

		/// <summary>
		/// The shortage is HELD FROM SETUP, so no real pass can begin the climb while it stands:
		/// the store is minted one canvas unit short of the tent's bill plus the upgrade's, the
		/// shortfall is proved exact right after the commission pays, the first check proves the
		/// tent still carries only its commission receipt, and nothing is withheld across turns.
		/// </summary>
		[Test]
		public void TheShortageIsHeldFromSetupAndNothingBeginsWhileShort()
		{
			string fixture = Read(Fixture);
			Assert.That(fixture, Does.Contain("internal const int UpgradeShortBy = 1;"));
			Assert.That(fixture, Does.Contain(
				"MintedBrushUnits = tentBrush + upgradeBrush - UpgradeShortBy;"));
			Assert.That(Follows(fixture, "CommissionTent();", "RequireExactShortfall();", 40),
				Is.True, "the exact shortfall is proved right after the commission pays");
			string phases = Read(Phases);
			Assert.That(phases, Does.Contain(
				"tent.GetStringProperty(KingdomConstruction.ReceiptProperty) == TentJobId"));
			Assert.That(phases, Does.Contain(
				"\"an improvement began on the tent while its bill was short\""));
			foreach (string path in new[] { Fixture, Phases, Shortfall })
				Assert.That(Read(path), Does.Not.Contain("RemoveFromContext"), path);
		}

		/// <summary>The negative leg asserts the NAMED reason and a zero debit, not merely that
		/// something refused, and exactly the missing unit admits the bill.</summary>
		[Test]
		public void TheNegativeLegBindsTheNamedMaterialRefusal()
		{
			string shortfall = Read(Shortfall);
			Assert.That(shortfall, Does.Contain(
				"== KingdomUpgradeRules.UpgradeVerdict.NotEnoughMaterial"));
			Assert.That(shortfall, Does.Contain("KingdomUpgradeRules.ReasonLine("));
			Assert.That(shortfall, Does.Contain("shortAssessment.Reason == expected"));
			Assert.That(shortfall, Does.Contain(
				"\"the refused assessment moved stored water\""));
			Assert.That(shortfall, Does.Contain("supplied == UpgradeShortBy"));
			Assert.That(shortfall, Does.Contain("KingdomUpgradeRules.UpgradeVerdict.Ready"));
		}

		/// <summary>The completion proof is ordered: the predecessor's identity is captured while
		/// it still stands, and its absence is proved by the typed physical lookup rather than by
		/// a census of what happens to be alive. The receipt is read where production settles
		/// it: TellCompletion moves FinalRemoved to EffectsSettled inside the same recovery, the
		/// terminal state the natively-run paid-housing witness reads.</summary>
		[Test]
		public void RemovalProofIsBoundToTheCapturedPredecessorIdentity()
		{
			string phases = Read(Phases);
			int captured = phases.IndexOf("PredecessorId = tent.IDIfAssigned;",
				StringComparison.Ordinal);
			int proof = phases.IndexOf("r_KingdomScaffold.RemovalProofProperty)",
				StringComparison.Ordinal);
			Assert.That(captured, Is.GreaterThan(-1), "the predecessor identity is never captured");
			Assert.That(proof, Is.GreaterThan(captured),
				"the removal proof must be read after the predecessor identity is captured");
			Assert.That(phases, Does.Contain("== PredecessorId,"),
				"the removal proof must be compared against the captured predecessor identity");
			Assert.That(phases, Does.Contain("KingdomConstruction.FindExactId(Zone, PredecessorId, out absent)"));
			Assert.That(phases, Does.Contain("== KingdomPhysicalLookupState.Absent"));
			Assert.That(phases, Does.Contain("job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled"));
			Assert.That(phases, Does.Contain("job.SubjectId == PredecessorId && job.OutputId == SuccessorId"));
			Assert.That(phases, Does.Not.Contain("KingdomPhysicalPhase.FinalRemoved"));
			Assert.That(Read("Growth/KingdomUpgrade.25.HandoverRemoval.cs"), Does.Contain(
				"if (!r_KingdomScaffold.TellCompletion(System, Successor, Job))"));
			Assert.That(Read("Growth/KingdomScaffold.CompletionAndLegacy.cs"), Does.Contain(
				"KingdomPhysicalPhase.EffectsSettled, telling.PhysicalIndex,"));
			Assert.That(Read("Harness/KingdomPaidHousingWitness.cs"), Does.Contain(
				"job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled"));
		}

		/// <summary>The persona brackets the exact phases, and every synthetic input is named in
		/// the report the run publishes.</summary>
		[Test]
		public void PersonaBracketsTheExactPhasesAndDisclosesEverySyntheticInput()
		{
			string persona = Read(Persona);
			Assert.That(persona, Does.Contain("REQUEST=founding-first-city"));
			Assert.That(persona, Does.Contain(
				"VERBS=tier-upgrade-setup,tier-upgrade-check,tier-upgrade-short"));
			Assert.That(persona, Does.Contain("SCRIPT=stagedigest;tier-upgrade-setup;advance 3600;"
				+ "tier-upgrade-check;tier-upgrade-short;advance 2400;tier-upgrade-check;"
				+ "advance 3600;tier-upgrade-check;stagedigest"));
			Assert.That(persona, Does.Contain("EXPECT=stagedigest:OK~founded=false,"
				+ "tier-upgrade-setup:OK~native-tier-upgrade phase=1,advance:OK,"
				+ "tier-upgrade-check:OK~native-tier-upgrade phase=2,"
				+ "tier-upgrade-short:OK~native-tier-upgrade phase=3,advance:OK,"
				+ "tier-upgrade-check:OK~native-tier-upgrade phase=4,advance:OK,"
				+ "tier-upgrade-check:OK~native-tier-upgrade cases=1 passed=1 failed=0,"
				+ "stagedigest:OK~founded=true,COMPLETE"));
			Assert.That(persona, Does.Contain("LOG_FORBID=[\"improvement refused cleanly:\","
				+ "\"construction: improvement projection waits:\","
				+ "\"seal: settlement pass was not staged\","
				+ "\"construction: founding heart recovery requires inspection\"]"));
			Assert.That(persona, Does.Contain("TIMEOUT=3600"));
			string checks = Read(Checks);
			Assert.That(checks, Does.Contain("synthetic-camp=true; synthetic-residents=true; "
				+ "synthetic-drams=true"));
			Assert.That(checks, Does.Contain("synthetic-heart-calendar=true; "
				+ "synthetic-founder-walk=true"));
			Assert.That(checks, Does.Contain("synthetic-born-provenance=true; "
				+ "synthetic-store-contents=true"));
			Assert.That(checks, Does.Contain("synthetic-first-notice=true; synthetic-tent=false"));
			Assert.That(checks, Does.Contain("ordinary-reachability=untested; charter=untested; "
				+ "save-load=untested"));
			// The rung-one completion's two synthetic parts - the explicit future calendar
			// frontier the shared completed-heart helper hands the production labour driver, and
			// the founder's westward walk - are disclosed wherever the run is described.
			foreach (string path in new[] { Persona, Provider, Fixture,
				"Tools/coverage/matrix.json" })
			{
				string text = Read(path);
				Assert.That(text, Does.Contain("explicit future calendar frontier"), path);
				Assert.That(text, Does.Contain("westward walk"), path);
			}
		}
	}
}
#endif
