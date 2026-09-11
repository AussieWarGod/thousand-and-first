#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The rung 2 -> 3 leg of the sealed camp-heart run (issue #159). Two kinds of case live here
	/// and they are not equals: VALUE tests, which execute production's own rules and the authored
	/// catalogue arithmetic the fixture is sized from, and WIRING pins, which only prove the seam
	/// still asks for what it claims to ask for. Neither is native evidence: the rung itself is
	/// proved only by the persona run, and that run is owed.
	/// </summary>
	public class KingdomCampHeartRung3NativeSourceTests
	{
		private const string Provider = "Harness/KingdomCampHeartNativeProvider.cs";
		private const string Checks = "Harness/KingdomCampHeartNativeChecks.cs";
		private const string Fixture = "Harness/KingdomCampHeartNativeFixture.cs";
		private const string Phases = "Harness/KingdomCampHeartNativeChecksPhases.cs";
		private const string Rung3 = "Harness/KingdomCampHeartNativeRung3.cs";
		private const string Stock = "Harness/KingdomCampHeartNativeRung3Stock.cs";
		private const string Diagnostics = "Harness/KingdomCampHeartNativeRung3Diagnostics.cs";
		private const string Bill = "Harness/KingdomCampHeartNativeBill.cs";
		private const string TownSeed = "Harness/KingdomCampHeartNativeTownSeed.cs";
		private const string Persona = "Tools/personas/camp-heart-rung3-native-check.persona";
		private const string Buildings = "RuntimeData/KingdomBuildings.xml";
		private const string Blueprints = "RuntimeData/ObjectBlueprints.xml";

		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		/// <summary>The value of one attribute of one authored building, read out of the catalogue
		/// rather than restated here.</summary>
		private static string Authored(string Key, string Attribute)
		{
			string xml = Read(Buildings);
			int at = xml.IndexOf("Key=\"" + Key + "\"", StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThan(-1), Key + " is not an authored building");
			int end = xml.IndexOf("</building>", at, StringComparison.Ordinal);
			int selfClosing = xml.IndexOf("/>", at, StringComparison.Ordinal);
			if (end < 0 || (selfClosing > -1 && selfClosing < end)) end = selfClosing;
			Assert.That(end, Is.GreaterThan(at), Key + " has no closing tag");
			string element = xml.Substring(at, end - at);
			int name = element.IndexOf(Attribute + "=\"", StringComparison.Ordinal);
			Assert.That(name, Is.GreaterThan(-1), Key + " has no " + Attribute);
			int open = element.IndexOf('"', name) + 1;
			int close = element.IndexOf('"', open);
			return element.Substring(open, close - open);
		}

		/// <summary>The stockpile capacity one named blueprint declares, read inside that
		/// blueprint's own element. Several blueprints declare the tag and they are not all the
		/// same size, so the element is bounded before the tag is read.</summary>
		private static int DeclaredCapacity(string Blueprint)
		{
			string xml = Read(Blueprints);
			int at = xml.IndexOf("<object Name=\"" + Blueprint + "\"", StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThan(-1), Blueprint + " is not an authored blueprint");
			int end = xml.IndexOf("</object>", at, StringComparison.Ordinal);
			Assert.That(end, Is.GreaterThan(at), Blueprint + " has no closing tag");
			string element = xml.Substring(at, end - at);
			int tag = element.IndexOf("<tag Name=\"r_KingdomStockpileCapacity\" Value=\"",
				StringComparison.Ordinal);
			Assert.That(tag, Is.GreaterThan(-1), Blueprint + " declares no stockpile capacity");
			int open = element.IndexOf("Value=\"", tag, StringComparison.Ordinal) + 7;
			return int.Parse(element.Substring(open, element.IndexOf('"', open) - open));
		}

		/// <summary>A <c>kind:units</c> bill as a dictionary, in the catalogue's own spelling.</summary>
		private static Dictionary<string, int> Bill(string Authored)
		{
			Dictionary<string, int> bill = new Dictionary<string, int>();
			foreach (string term in Authored.Split(','))
			{
				string[] parts = term.Split(':');
				Assert.That(parts.Length, Is.EqualTo(2), "malformed bill term '" + term + "'");
				bill[parts[0].Trim()] = int.Parse(parts[1].Trim());
			}
			return bill;
		}

		/// <summary>An <c>internal const int</c> the harness declares, read off its own source so
		/// the arithmetic below is checked against what the fixture will really mint.</summary>
		private static int Constant(string Source, string Name)
		{
			string text = Read(Source);
			int at = text.IndexOf("internal const int " + Name + " = ", StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThan(-1), Name + " is not declared in " + Source);
			int open = text.IndexOf("= ", at, StringComparison.Ordinal) + 2;
			int close = text.IndexOf(';', open);
			return int.Parse(text.Substring(open, close - open).Trim());
		}

		/// <summary>
		/// VALUE. The rung-3 bill the fixture mints is the authored rung 2 -> 3 bill, term for
		/// term, and what the fixture is accountable for at the rung-3 boundary fits the store's
		/// OWN declared capacity. The store is not the fixture's to predict -- the settlement
		/// deposits its own yard work into it while a rung is built -- so this is an upper bound
		/// the fixture must leave room inside, never an equality it asserts about the world.
		/// </summary>
		[Test]
		public void TheMintedRungThreeBillIsTheAuthoredBillAndFitsTheDeclaredCapacity()
		{
			Dictionary<string, int> authored = Bill(Authored("heartwaterstone", "UpgradeMaterials"));
			Assert.That(authored.Count, Is.EqualTo(3));
			Assert.That(authored["timber"], Is.EqualTo(Constant(Checks, "Rung3TimberUnits")));
			Assert.That(authored["stone"], Is.EqualTo(Constant(Checks, "Rung3StoneUnits")));
			Assert.That(authored["shapedtimber"],
				Is.EqualTo(Constant(Checks, "Rung3ShapedTimberUnits")));

			int rung3 = authored["timber"] + authored["stone"] + authored["shapedtimber"];
			Assert.That(rung3, Is.EqualTo(25));
			int capacity = DeclaredCapacity("r_KingdomHeartStockpile");
			// The fixture's own store, not the first stockpile tag in the file: two blueprints
			// declare 48 today, so reading the wrong one would prove nothing about this store.
			Assert.That(capacity, Is.EqualTo(48));

			// The rung-2 fill, and the room the rung-3 run deliberately leaves. Native run 5
			// found 46 of 48 units standing at the phase-2 boundary, because the settlement's own
			// keepers deposit what its yard work makes into this same store while the rung is
			// built: the fixture's fill must leave room for that AND for the next authored bill.
			int rung2Fill = Constant(Checks, "MintedStoneUnits") + Constant(Checks, "MintedTimberUnits");
			int unasked = Constant(Checks, "Rung3UnaskedBrushUnits");
			Assert.That(rung2Fill + unasked, Is.LessThanOrEqualTo(capacity));
			// What the fixture is accountable for at the rung-3 boundary -- its own unasked units
			// plus the whole next bill -- must fit the declared capacity with room to spare.
			Assert.That(unasked + rung3, Is.LessThanOrEqualTo(capacity));
			Assert.That(unasked, Is.GreaterThan(0),
				"the unasked units are the witness that the bill took only what it asked for");

			// The rung-2 bill the fixture mints is still the authored rung 1 -> 2 bill, and the
			// rung-2 run's own fill still fills the declared capacity exactly.
			Dictionary<string, int> rung2 = Bill(Authored("heartbasin", "UpgradeMaterials"));
			Assert.That(rung2["stone"], Is.EqualTo(Constant(Checks, "MintedStoneUnits")));
			Assert.That(rung2["timber"], Is.EqualTo(Constant(Checks, "MintedTimberUnits")));
			Assert.That(rung2["stone"] + rung2["timber"] + Constant(Checks, "MintedBrushUnits"),
				Is.EqualTo(capacity));
		}

		/// <summary>
		/// VALUE. The phase-3 predicate, asked of production's own rules: rung three is worth 160
		/// drams, the moot yard asks for a Town and the workshop craft level, and the fixture's
		/// own population, dedicated capacity and taught-design count are exactly what those two
		/// gates need. A threshold that moves fails here.
		/// </summary>
		[Test]
		public void ThePhaseThreePredicateMatchesProductionsOwnRungGates()
		{
			Assert.That(KingdomPlotRules.HeartBasinCapacityForRung(3), Is.EqualTo(160));
			Assert.That(KingdomPlotRules.HeartBasinCapacityForRung(2), Is.EqualTo(48));
			Assert.That(Read(Rung3), Does.Contain("BasinCapacity(standing) == \"160\""));

			Assert.That(Authored("heartmoot", "MinStage"), Is.EqualTo("Town"));
			Assert.That(Authored("heartmoot", "MinTech"), Is.EqualTo("workshop"));
			Assert.That(Authored("heartmoot", "Footprint"), Is.EqualTo("12x10"));
			Assert.That(Authored("heartwaterstone", "Footprint"), Is.EqualTo("8x6"));
			Assert.That(Read(Rung3), Does.Contain("width == 12 && height == 10"));

			int residents = Constant(Checks, "TownResidentCount");
			int drams = Constant(Checks, "TownDedicatedDrams");
			Assert.That(KingdomRules.StageFor(residents, drams), Is.EqualTo(GrowthStage.Town));
			// And the fixture is not merely over the line by accident: one person fewer, or a
			// dedication under the authored capacity gate, is not a Town.
			Assert.That(KingdomRules.StageFor(residents - 1, drams),
				Is.Not.EqualTo(GrowthStage.Town));
			Assert.That(KingdomRules.StageFor(residents, 255), Is.Not.EqualTo(GrowthStage.Town));

			int disks = Constant(Checks, "WorkshopDisks");
			Assert.That(KingdomZoningRules.LevelForPoints(
				disks * KingdomZoningRules.TechPointsPerDisk),
				Is.EqualTo(TechLevel.Workshop));
			Assert.That(KingdomZoningRules.LevelForPoints(
				(disks - 1) * KingdomZoningRules.TechPointsPerDisk),
				Is.LessThan(TechLevel.Workshop));
		}

		/// <summary>
		/// The persona and the provider name the SAME sealed script, word for word. The provider
		/// refuses any other script outright, so a persona that drifted from it could only ever
		/// fail in a native run; this compares the two texts instead.
		/// </summary>
		[Test]
		public void ThePersonaScriptIsExactlyTheProvidersSealedRungThreeScript()
		{
			string persona = Read(Persona);
			int at = persona.IndexOf("\nSCRIPT=", StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThan(-1), "the persona declares no script");
			int end = persona.IndexOf('\n', at + 1);
			string[] words = persona.Substring(at + 8, end - at - 8).Split(';');

			string provider = Read(Provider);
			int array = provider.IndexOf("private static readonly string[] Rung3Script = {",
				StringComparison.Ordinal);
			Assert.That(array, Is.GreaterThan(-1), "the provider seals no rung-3 script");
			int open = provider.IndexOf('{', array) + 1;
			int close = provider.IndexOf("};", open, StringComparison.Ordinal);
			string[] sealedWords = provider.Substring(open, close - open).Split(',');
			Assert.That(sealedWords.Length, Is.EqualTo(words.Length),
				"the persona and the provider seal different script lengths");
			for (int i = 0; i < words.Length; i++)
			{
				string word = sealedWords[i].Trim().Replace("\n", "").Replace("\t", "").Trim();
				if (word == "SetupVerb") word = "\"camp-heart-setup\"";
				if (word == "CheckVerb") word = "\"camp-heart-check\"";
				Assert.That(word, Is.EqualTo("\"" + words[i].Trim() + "\""),
					"script word " + i + " differs between persona and provider");
			}
			// The rung-2 script is untouched, and the provider still accepts exactly two.
			Assert.That(provider, Does.Contain("private static readonly string[] Rung2Script = {"));
			Assert.That(provider, Does.Contain("if (SameScript(script, Rung2Script)) return 2;"));
			Assert.That(provider, Does.Contain("if (SameScript(script, Rung3Script)) return 3;"));
			Assert.That(provider, Does.Contain(
				"Require(false, \"the sealed camp heart script differs\");"));
		}

		/// <summary>
		/// WIRING. The seam still drives nothing: the rung-3 phase reads, and the rung-3 bill is
		/// minted through the same disclosed path as every other fixture unit. Every synthetic
		/// input this leg adds is disclosed in the report line, and the open CASE 3 question is
		/// carried in the persona header and in the shard that exists to answer it.
		/// </summary>
		[Test]
		public void TheRungThreeLegDrivesNothingAndDisclosesEverythingItAdds()
		{
			string rung3 = Read(Rung3);
			foreach (string driver in new[] { "KingdomUpgrade.Begin(", "KingdomPlots.Advance(",
				"TryApplyUpgrade(", "SetIntProperty(", "SetStringProperty(", "HeartRung =" })
				Assert.That(rung3, Does.Not.Contain(driver),
					"the rung-3 phase must read, never drive: " + driver);
			foreach (string read in new[] { "KingdomPlots.HeartRung(Zone) == 3",
				"KingdomUpgrade.IsFunctionallyBuilt(standing)",
				"KingdomConstructionPhase.Complete",
				"KingdomArchitectureStamper.UpgradeFaultProperty",
				"RequireSettledOnceEachClimb(standing);",
				"int settled = Standing.GetIntProperty(HeartEffectProperty);",
				"int first = SecondStanding.GetIntProperty(HeartEffectProperty);" })
				Assert.That(rung3, Does.Contain(read));
			Assert.That(rung3, Does.Contain("TrySettleImprovementHeartRung"));
			Assert.That(rung3, Does.Contain("settlement helper TWICE"));
			Assert.That(rung3, Does.Contain("SECOND consecutive climb"));

			string stock = Read(Stock);
			foreach (string driver in new[] { "KingdomUpgrade.Begin(", "KingdomPlots.Advance(",
				"TryApplyUpgrade(", "Destroy(", "Obliterate(" })
				Assert.That(stock, Does.Not.Contain(driver),
					"the rung-3 stock shard must never drive or dispose: " + driver);
			Assert.That(stock, Does.Contain("Mint(KingdomMaterial.ShapedTimber, shaped, "
				+ "MintedRung3);"));
			Assert.That(stock, Does.Contain("KingdomZoning.Learn(System, \"disk\","));
			Assert.That(stock, Does.Contain("Require(after >= TechLevel.Workshop,"));
			// Observed-relative, capacity-bounded, and diagnosable: the store is read before and
			// after, the declared capacity is production's own, and every refusal carries the
			// per-material census.
			Assert.That(stock, Does.Contain("int capacity = KingdomSurvey.StockCapacityOf(Store);"));
			Assert.That(stock, Does.Contain("int before = KingdomSurvey.StockHeldIn(Store);"));
			Assert.That(stock, Does.Contain("Require(before + minting <= capacity,"));
			Assert.That(stock, Does.Contain("Require(after == before + minting,"));
			Assert.That(stock, Does.Contain("taf-camp-rung3-bill-short: the store holds "));
			foreach (string diagnostic in new[] { "capacity. Observed: \" + census",
				"unit(s) minted. Before: \" + census + \". After: \" + held",
				"bill asks for \" + Units + \". Observed: \" + Census" })
				Assert.That(stock, Does.Contain(diagnostic));
			Assert.That(stock, Does.Contain("RecordAnnexGround()"));
			Assert.That(stock, Does.Contain("annex-ground rect="));
			Assert.That(Read(Phases), Does.Contain("MintRung3Bill();"));
			Assert.That(Read(Phases), Does.Contain("RecordAnnexGround();"));
			// The third check of either ladder is dispatched by the sealed target rung: the rung-2
			// run keeps its #162 day-after gate, the rung-3 run reads the second climb, and both
			// ask production's own founding-heart recovery predicate (PR #164) on their ground.
			string afterRaise = Read("Harness/KingdomCampHeartNativeAfterRaise.cs");
			Assert.That(Read(Phases), Does.Contain("case 3: Phase3(); Done = true;"));
			Assert.That(afterRaise, Does.Contain("if (TargetRung >= 3) { ClimbRung3(); return; }"));
			Assert.That(afterRaise, Does.Contain("AfterRaise();"));
			Assert.That(rung3, Does.Contain("private void ClimbRung3()"));
			Assert.That(rung3, Does.Contain("RequireRecoveredAfterSecondClimb();"));
			Assert.That(rung3, Does.Contain(
				"bool recovered = KingdomPlots.RecoverFoundingHeart(System, Zone);"));
			Assert.That(rung3, Does.Contain("taf-camp-rung3-heart-unrecovered:"));
		}

		/// <summary>
		/// WIRING (native runs 7 and 33). The rung-3 boundary reads the settlement's own verdict,
		/// stage, population and the moot yard's job BY TARGET before its first Require, journals
		/// the blocked path exactly as the rung-2 leg does, keys job progress on the rung-3 job
		/// and the waterstone rather than the retained rung-2 pair, and proves the Town held both
		/// when the bill was minted and at the boundary. The diagnostic shard drives nothing.
		/// </summary>
		[Test]
		public void TheRungThreeBoundaryReadsTheVerdictAndTheTownBeforeItAsserts()
		{
			string rung3 = Read(Rung3);
			int standing = rung3.IndexOf("RecordRung3Standing(standing);", StringComparison.Ordinal);
			int firstRequire = rung3.IndexOf("Require(KingdomUpgrade.DesignKeyOf(standing) == ThirdRungKey,",
				StringComparison.Ordinal);
			Assert.That(standing, Is.GreaterThan(-1));
			Assert.That(firstRequire, Is.GreaterThan(standing), "the verdict is read before the key is asserted");
			foreach (string wiring in new[] { "KingdomConstructionJob rung3Job = FindRung3Job();",
				"RecordRung3Blocked(rung3Job);",
				"RecordJobProgress(rung3Job == null ? \"(no moot-yard job)\" : rung3Job.Id,",
				"SecondStanding);", "RequireTownHeld(\"at the rung-3 boundary\");",
				"KingdomConstructionJob found = FindRung3Job();" })
				Assert.That(rung3, Does.Contain(wiring), wiring);
			Assert.That(rung3.IndexOf("RequireTownHeld(\"at the rung-3 boundary\");", StringComparison.Ordinal),
				Is.LessThan(firstRequire));

			string diagnostics = Read(Diagnostics);
			foreach (string read in new[] {
				"KingdomUpgrade.Assessment assessment = KingdomUpgrade.Assess(System, Zone,",
				"int freeHands = System.Population - System.AssignedCrew;",
				"\"; verdict=\").Append(assessment.Verdict)",
				"\"; stage-needed=\").Append(assessment.StageNeeded)",
				"((KingdomUpgradeRules.UpgradeVerdict)improvement.AnnouncedReason)",
				"|| job.TargetKey != ThirdRungKey) continue;",
				"RecordBlockedMessages();",
				"\"taf-camp-rung3-town-held: the settlement did not hold the Town the moot yard \"",
				"System.Stage >= GrowthStage.Town",
				"&& System.Population >= TownResidentCount," })
				Assert.That(diagnostics, Does.Contain(read), read);
			foreach (string driver in new[] { "KingdomUpgrade.Begin(", "KingdomPlots.Advance(",
				"TryApplyUpgrade(", "SetIntProperty(", "SetStringProperty(", "Destroy(", "Obliterate(" })
				Assert.That(diagnostics, Does.Not.Contain(driver),
					"the rung-3 diagnostics must read, never drive: " + driver);
			Assert.That(Read(Bill), Does.Contain("private void RecordJobProgress(string Id, GameObject Root)"));
			Assert.That(Read(Bill), Does.Contain("var improvement = Root?.GetPart<XRL.World.Parts.r_KingdomImprovement>();"));
			Assert.That(Read(Phases), Does.Contain("RequireTownHeld(\"when the rung-3 bill was minted\");"));
			Assert.That(diagnostics, Does.Contain("KingdomSubsidence.ScopedSupports(System, Zone, Census());"));
			Assert.That(diagnostics, Does.Contain("\"; supported level=\").Append(KingdomSubsidenceRules.SupportedLevel("));
		}

		/// <summary>
		/// WIRING (native run 33). The Town the moot yard is gated on is held by REAL finished
		/// works seeded through production's own receiptless stake and schema-zero calendar, never
		/// hand-stamped, never crewed, never by writing a home id; subsidence and lodging stay on
		/// and the run refuses unless production's own bed count and water level carry 25. The seed
		/// runs after the reservoir is dedicated and before the before-snapshot, and the persona
		/// discloses all of it.
		/// </summary>
		[Test]
		public void TheTownIsSeededAsRealFinishedWorksAndNeverHandStamped()
		{
			string seed = Read(TownSeed);
			foreach (string production in new[] {
				"KingdomPlots.Stake(System, Zone, lot, entry, spec, grid,",
				"KingdomPlots.Advance(part, System, checked(part.StartTick + part.TotalTicks));",
				"!works.HasIntProperty(KingdomPlots.PlotWorkSchemaProperty)",
				"KingdomGrowth.TryCountBeds(Zone, out beds, out failure)",
				"KingdomSubsidenceRules.LevelFromWater(water, stage) < Residents",
				"KingdomSubsidenceRules.SupportedLevel(tally, stage, System.Shade)",
				"KingdomLodging.OnSettlementPass(System, Zone, survey);",
				"KingdomUpgrade.IsFunctionallyBuilt(final)",
				"final.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1",
				"System.Stage = stage;",
				"internal const string LodgingKey = \"tentrow\";",
				"taf-camp-town-seed-roof:", "taf-camp-town-seed-water:", "taf-camp-town-seed-level:",
				"taf-camp-town-seed-unhoused:", "crewed=false; hand-stamped=false" })
				Assert.That(seed, Does.Contain(production), production);
			foreach (string forbidden in new[] { "SetIntProperty(\"KingdomBuilt\"",
				"KingdomUpgrade.BuildKeyProperty,", "SetStringProperty(KingdomLodging.HomePlotIdProperty",
				"KingdomPlots.Commission(", "KingdomUpgrade.Begin(", "TryApplyUpgrade(",
				"KingdomSubsidence.Enabled = ", "KingdomLodging.Enabled = ", "Destroy(", "Obliterate(" })
				Assert.That(seed, Does.Not.Contain(forbidden),
					"the town seed must go through production, never around it: " + forbidden);

			string checks = Read(Checks);
			int dedicate = checks.IndexOf("KingdomNativeCampFounding.Dedicate(Game, Zone, System, Drams,", StringComparison.Ordinal);
			int seedAt = checks.IndexOf("SeedTownIfOwed();", StringComparison.Ordinal);
			int before = checks.IndexOf("RecordBefore();", StringComparison.Ordinal);
			Assert.That(dedicate, Is.GreaterThan(-1));
			Assert.That(seedAt, Is.GreaterThan(dedicate), "the town is seeded after the reservoir is dedicated");
			Assert.That(before, Is.GreaterThan(seedAt), "the town is seeded before the before-snapshot");
			Assert.That(checks, Does.Contain("\"; synthetic-town-works=\""));
			Assert.That(checks, Does.Contain("\"; synthetic-stage-derived=\""));
			Assert.That(seed, Does.Contain("if (TargetRung < 3) return;"));

			string persona = Read(Persona);
			Assert.That(persona, Does.Contain("Subsidence and lodging departures stay ON."));
			Assert.That(persona, Does.Contain("nothing is hand-stamped and no crew built them"));
			Assert.That(persona, Does.Contain("never by writing a home id"));
			Assert.That(persona, Does.Contain("taf-camp-rung3-town-held"));

			// The marker the rung-3 phase reads is the one production writes.
			Assert.That(Read(Checks), Does.Contain("internal const string HeartEffectProperty = "
				+ "\"r_TAF_ConstructionHeartEffect\";"));
			Assert.That(Read("Growth/KingdomPlot2.03.RegistryAndDeclarations.cs"),
				Does.Contain("private const string HeartEffectProperty = "
					+ "\"r_TAF_ConstructionHeartEffect\";"));
			Assert.That(Read("Growth/KingdomPlotHeartRules.Settle.cs"),
				Does.Contain("Building.SetIntProperty(HeartEffectProperty, 2);"));

			string checks = Read(Checks);
			Assert.That(checks, Does.Contain("\"; target-rung=\" + Retained.TargetRung"));
			Assert.That(checks, Does.Contain("\"; synthetic-rung3-bill=\""));
			Assert.That(checks, Does.Contain("\"; synthetic-craft-disks=\""));

			string persona = Read(Persona);
			Assert.That(persona, Does.Contain("camp-builder CASE 3"));
			Assert.That(persona, Does.Contain("founded, not commissioned"));
			Assert.That(persona, Does.Contain("TWICE IN A ROW"));
			Assert.That(persona, Does.Contain("That is a reading of the source, not a result."));
			Assert.That(persona, Does.Contain("LOG_FORBID=[\"construction: founding heart "
				+ "recovery requires inspection\",\"seal: settlement pass was not staged\"]"));
			Assert.That(persona, Does.Contain("TIMEOUT=3600"));
			Assert.That(persona, Does.Contain("SET=camp,native-regression,test-only"));
		}
	}
}
#endif
