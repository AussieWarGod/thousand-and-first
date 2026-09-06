#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source and XML contracts for the native raid-launch harness. These do NOT execute
	/// the fixture: the six Harness shards are XRL-coupled and compile only inside a dev scenario
	/// profile, so this suite reads them as text exactly like
	/// KingdomRaidOutboxNativeHarnessSourceTests.cs. Runtime proof is the two personas.</summary>
	[TestFixture]
	public sealed class KingdomRaidLaunchNativeContractTests
	{
		private const string Overlay = "Harness/ObjectBlueprints.xml";
		private const string ShippedObjects = "RuntimeData/ObjectBlueprints.xml";
		private const string Profiles = "RuntimeData/KingdomRaidProfiles.xml";
		private const string Probe = "Harness/r_TAF_RaidMintProbe.cs";
		private const string Fixture = "Harness/KingdomRaidLaunchNativeFixture.cs";
		private const string Checks = "Harness/KingdomRaidLaunchNativeChecks.cs";
		private const string Quarantine = "Harness/KingdomRaidLaunchNativeQuarantineChecks.cs";
		private const string Provider = "Harness/KingdomRaidLaunchNativeProvider.cs";
		private const string Snapshots = "Harness/KingdomRaidLaunchNativeSnapshots.cs";
		/// <summary>The single caller that fixes the mint-loop fault literal; also the caller
		/// KingdomLifecycleRules.Quarantine writes no diagnostic of its own for.</summary>
		private const string AttackProjection =
			"Raids/KingdomRaids.09.AttackProjectionAndHelpers.cs";
		private const string PartName = "r_TAF_RaidMintProbe";
		/// <summary>The pinned roster stage. The same literal is a const in the fixture.</summary>
		private const string PinnedStage = "GrowthStage.Steading";
		private static readonly string[] Shards =
			new[] { Probe, Fixture, Checks, Quarantine, Provider, Snapshots };
		/// <summary>The one reset the harness performs, and the proof it erases nothing.</summary>
		private const string EntryGuard =
			"if (!r_TAF_RaidMintProbe.Vacant || KingdomRaidLaunchNativeFixture.LastAttempt != null) "
			+ "return \"native raid launch refused: retained native raid evidence must not be cleared\";";
		/// <summary>The two SPAWNABLE Snapjaw variants. The un-numbered archetypes carry
		/// &lt;tag Name="BaseObject" Value="*noinherit" /&gt; in the installed core, which
		/// EligibleBlueprint (Raids/KingdomRaidProfiles.cs:241-252) refuses.</summary>
		private static readonly string[] Probed =
			new[] { "Snapjaw Scavenger 0", "Snapjaw Hunter 0" };
		/// <summary>Names this repository has itself shipped in a raid band and the installed
		/// core refused: the six un-numbered Snapjaw archetypes (BaseObject) and the one
		/// ExcludeFromDynamicEncounters cannibal. A known list, never a census.</summary>
		private static readonly string[] KnownRefused = new[] {
			"Snapjaw Scavenger", "Snapjaw Hunter", "Snapjaw Warrior", "Snapjaw Shotgunner",
			"Snapjaw Brute", "Snapjaw Warlord", "Missile Cannibal" };

		[Test]
		public void OverlayMergesTheProbeIntoExactlyTheTwoShippedSnapjawBlueprints()
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(Read(Overlay));
			Assert.AreEqual("objects", document.DocumentElement.Name, "overlay root element");
			XmlNodeList objects = document.DocumentElement.SelectNodes("object[part[@Name='r_TAF_RaidMintProbe']]");
			Assert.AreEqual(2, objects.Count, "the raid probe must merge exactly two blueprints");
			List<string> names = new List<string>();
			for (int i = 0; i < objects.Count; i++)
			{
				XmlElement row = (XmlElement)objects[i];
				names.Add(row.GetAttribute("Name"));
				Assert.AreEqual("Merge", row.GetAttribute("Load"),
					"a dev overlay row must merge into the shipped blueprint, never redefine it");
				Assert.AreEqual("", row.GetAttribute("Inherits"), "overlay row must not inherit");
				XmlNodeList parts = row.SelectNodes("part");
				Assert.AreEqual(1, parts.Count, "each overlay row carries exactly one part row");
				Assert.AreEqual(PartName, ((XmlElement)parts[0]).GetAttribute("Name"));
				Assert.AreEqual(1, row.SelectNodes("*").Count,
					"an overlay row may carry nothing but its one part row");
			}
			CollectionAssert.AreEquivalent(Probed, names);
			// The part name must resolve as XRL.World.Parts.<name>, the shipped-part idiom.
			StringAssert.Contains("namespace XRL.World.Parts", Read(Probe));
			StringAssert.Contains("public sealed class " + PartName + " : IPart", Read(Probe));
			// The overlay's own header must carry no provenance comment: it is byte-identical to
			// the shipped file through the root element. The provenance sentence lives on
			// KingdomRaidLaunchNativeProvider's class comment instead.
			string[] overlayLines = Read(Overlay).Replace("\r\n", "\n").Split('\n');
			string[] shippedLines = Read(ShippedObjects).Replace("\r\n", "\n").Split('\n');
			Assert.AreEqual(shippedLines[0], overlayLines[0],
				"the overlay's xml declaration must match the shipped file exactly");
			Assert.AreEqual(shippedLines[1], overlayLines[1],
				"the overlay's root element line must match the shipped file, byte-identical");
			StringAssert.Contains("Harness/ObjectBlueprints.xml is a DEV-ONLY overlay",
				Read(Provider), "the provenance sentence moved to the provider's class comment");
		}

		[Test]
		public void ProfileStillListsExactlyTheTwoProbedBlueprintsAtThePinnedStage()
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(Read(Profiles));
			XmlElement snapjaws = null;
			XmlNodeList rows = document.DocumentElement.SelectNodes("profile");
			for (int i = 0; i < rows.Count; i++)
				if (((XmlElement)rows[i]).GetAttribute("Faction") == "Snapjaws")
					snapjaws = (XmlElement)rows[i];
			Assert.NotNull(snapjaws, "the shipped Snapjaws raid profile");
			// The fixture pins Steading, and Members() returns Steading for every stage below
			// Village (Raids/KingdomRaidProfiles.cs:28-34), so this attribute is the whole roster
			// the launcher can request.
			string[] steading = snapjaws.GetAttribute("Steading").Split(',');
			Assert.AreEqual(3, steading.Length,
				"the Steading band keeps its three slots, so its duplicate weight is preserved");
			CollectionAssert.AreEqual(new[] { "Snapjaw Scavenger 0", "Snapjaw Scavenger 0", "Snapjaw Hunter 0" },
				new List<string>(Trim(steading)), "the exact 2:1 weight and frozen roster order must remain stable");
			CollectionAssert.AreEquivalent(Probed,
				new List<string>(new HashSet<string>(Trim(steading), StringComparer.Ordinal)),
				"the Steading band names exactly the two probed variants and no third body");
			foreach (string member in steading)
				CollectionAssert.Contains(Probed, member.Trim(),
					"every pinned-stage roster slot must be a probed blueprint");
			CollectionAssert.IsSubsetOf(Probed, new List<string>(Trim(steading)));
			StringAssert.Contains("internal const GrowthStage FrozenStage = " + PinnedStage + ";",
				Read(Fixture));
			// The pin has to matter: the later tiers deliberately name unprobed bodies.
			bool unprobed = false;
			foreach (string tier in new[] { "Village", "Town", "City" })
				foreach (string member in Trim(snapjaws.GetAttribute(tier).Split(',')))
					if (Array.IndexOf(Probed, member) < 0) unprobed = true;
			Assert.IsTrue(unprobed,
				"a later roster tier must name an unprobed body, or pinning the stage proves nothing");
		}

		[Test]
		public void CompileRouteCopiesOnlyCSharpSoTheOverlayIsNeverStagedOrRegistered()
		{
			// gate.sh:131-133 copies exactly the rows of the shared inventory helper...
			StringAssert.Contains(
				"python3 \"$REPO/Tools/dev-harness-inventory.py\" --list-harness | while IFS= read -r shard; do",
				Read("Tools/gate.sh"));
			StringAssert.Contains("cp -- \"$REPO/Harness/$shard\" \"$DEV/Harness/$shard\"",
				Read("Tools/gate.sh"));
			// ...and that helper skips every non-.cs entry, so the XML meets no compiler.
			StringAssert.Contains("if not path.is_file() or path.suffix.casefold() != \".cs\":",
				Read("Tools/dev-harness-inventory.py"));
			string taf = Read("DevTests/TafTests.csproj");
			string portable = Read("DevTests/PortableTests.csproj");
			foreach (string shard in Shards)
			{
				string include = "..\\" + shard.Replace('/', '\\');
				StringAssert.DoesNotContain(include, taf,
					"an engine-touching Harness shard must not be in a Qud-free project");
				StringAssert.DoesNotContain(include, portable,
					"an engine-touching Harness shard must not be in a Qud-free project");
				StringAssert.Contains("using XRL", Read(shard),
					"each shard is engine-touching, which is why neither project may hold it");
			}
			StringAssert.DoesNotContain("ObjectBlueprints.xml", taf);
			StringAssert.DoesNotContain("ObjectBlueprints.xml", portable);
		}

		[Test]
		public void RunRouteCopiesHarnessXmlAndSealsByWalkingEveryFile()
		{
			string prepare = Read("Tools/prepare-scenario.sh");
			// prepare-scenario.sh:101-102 copies BOTH suffixes by glob, with no per-file list, so
			// the new overlay reaches the throwaway profile with no tool edit.
			StringAssert.Contains(
				"find \"$REPO/Harness\" -mindepth 1 -maxdepth 1 -type f \\( -name '*.cs' -o -name '*.xml' \\) \\",
				prepare);
			StringAssert.Contains("-exec cp -- {} \"$MOD/Harness/\" \\;", prepare);
			// ...and the seal is derived by walking the finished profile, not enumerated, so the
			// overlay is sealed with everything else.
			StringAssert.Contains("python3 \"$PROFILE_TOOL\" seal \"$LOCAL\" \"$SEAL_DIR/profile.sha256\"",
				prepare);
			string profile = Read("Tools/scenario_profile.py");
			StringAssert.Contains("\"\"\"Every regular file under root, keyed by normalized relative path.",
				profile);
			StringAssert.Contains("for current, directories, files in os.walk(root, followlinks=False):",
				profile);
		}

		[Test]
		public void ProbeHandlersOnlyRecordAndNeverThrowIntoTheFactory()
		{
			string source = Read(Probe);
			foreach (string signature in new[] {
				"public override bool HandleEvent(BeforeObjectCreatedEvent E)",
				"private void Observe(BeforeObjectCreatedEvent E)",
				"private static string Substitute(BeforeObjectCreatedEvent E, out GameObject Made," })
				StringAssert.DoesNotContain("throw", Method(source, signature),
					"a probe handler must never throw: the factory swallows callback exceptions");
			StringAssert.Contains(
				"catch (Exception error) { try { entry = Fault(entry, sequence, E.Object, "
					+ "Describe(error)); } catch (Exception) { } }",
				Flat(source));
			StringAssert.Contains("catch (Exception) { }", source);
			StringAssert.Contains("internal static volatile bool Armed;", source);
			ContainsAll(Flat(Method(source, "private void Observe(BeforeObjectCreatedEvent E)")),
				"if (!Armed || Substituting || E == null || E.Object == null) return;",
				"Book == null ? null : Book.Raid, r_TAF_RaidMintSnapshots.Priors(Entries));",
				"if (!ReferenceEquals(E.Object, ParentObject))");
			StringAssert.Contains("E.ReplacementObject = Made;",
				Method(source, "private static string Substitute(BeforeObjectCreatedEvent E, out GameObject Made,"));
		}

		[Test]
		public void NoShardPerformsDestructiveTeardown()
		{
			foreach (string shard in Shards)
			{
				string source = Read(shard);
				foreach (string forbidden in new[] {
					"Obliterate(", "Destroy(", "RemoveObject(", "RemovePart(", "Despawn(" })
					StringAssert.DoesNotContain(forbidden, source,
						shard + " must retain every body, substitute and abandoned original");
				foreach (Match match in Regex.Matches(source, @"[A-Za-z_][A-Za-z0-9_]*\.Clear\(\)"))
					CollectionAssert.Contains(new[] { "Ledger.Clear()", "Entries.Clear()" },
						match.Value,
						shard + " may clear only the probe's own retained observation lists");
			}
		}

		[Test]
		public void ProviderArmsThenDisarmsInsideAFinallyAndProvesItsReceipts()
		{
			string source = Read(Provider);
			string body = Flat(Method(source, "public string RunScenarioVerb("));
			Ordered(body, "Ok = false;", "bool quarantine = Verb == VerbB1;",
				"if (!Eligible(game, zone, Verb, out failure)) return",
				"game.SetStringGameState(receipt, \"intent\");",
				"KingdomScenarioDurableState.ProvesExactText(receipt, \"intent\")",
				EntryGuard, "r_TAF_RaidMintProbe.ResetProbe();", "r_TAF_RaidMintProbe.Arm(",
				"KingdomRaidLaunchNativeChecks.SubstituteAtSequence : 0,",
				"report = KingdomRaidLaunchNativeChecks.Run(game, zone, quarantine, out Ok);",
				"finally", "r_TAF_RaidMintProbe.Armed = false;",
				"game.SetStringGameState(receipt, report);", "return report;");
			int at = source.IndexOf("finally", StringComparison.Ordinal);
			Assert.Greater(at, 0, "the provider must disarm in a finally block");
			Assert.Greater(source.IndexOf("r_TAF_RaidMintProbe.Armed = false;", at,
				StringComparison.Ordinal), at, "Armed = false must sit inside that finally");
			ContainsAll(source, "internal const string VerbA = \"raid-launch-native-a\";",
				"internal const string VerbB1 = \"raid-launch-native-b1\";",
				"internal const int ExpectedCases = 1;", "[KingdomScenarioVerbProvider]",
				"return KingdomScenarioVerbApi.Version;", "return new[] { VerbA, VerbB1 };");
		}

		[Test]
		public void ChecksAssertOutsideDispatchAndDeclareTheFixedQuarantineFault()
		{
			string checks = Read(Checks);
			Ordered(Flat(Method(checks, "internal static string Run(")),
				"KingdomRaidLaunchNativeFixture.TryCreate(Zone, out fixture, out failure)",
				"r_TAF_RaidMintProbe.Snapshot().Length == 0", "fixture.Activate();",
				"if (Quarantine) fixture.Activate();", "r_TAF_RaidMintProbe.Armed = false;",
				"observations = r_TAF_RaidMintProbe.Snapshot();",
				"op = observations[0].Raid;", "Check(op.PartySize >= 2,",
				"a second real activation minted again after quarantine",
				"KingdomRaidLaunchNativeQuarantineChecks.Verify(fixture, op, observations);",
				"VerifyHappyPath(fixture, op, observations);", "finally",
				// Evidence and the report are only ever built AFTER this point, inside the
				// finally, each guarded by its own try/catch so neither loses the run's evidence.
				"r_TAF_RaidMintProbe.Armed = false;",
				"try { Evidence(results, fixture, op, observations); }", "report-fault=",
				"try { report = Report(Game, now, Quarantine, passed, ok, fixture); }",
				"report-fault=");
			StringAssert.Contains("\"raid projection body or frozen entry cell could not be recreated\"",
				checks);
			ContainsAll(checks, "internal const int SubstituteAtSequence = 2;",
				"internal const string SubstituteBlueprint = \"Chest\";",
				"save-load=untested", "b2-swallowed-throw=unimplemented",
				"same-blueprint-custody=open", "substitute-custody=unproved",
				"scene-preserved=true");
			// The synthetic-seed clause must be composed from the fixture's own named constants
			// (and the exact enum members SeedIncident assigns), never a second hardcoded copy.
			ContainsAll(Flat(Method(checks, "private static string Report(")),
				"KingdomRaidLaunchNativeFixture Fixture)",
				"Fixture == null || Fixture.System == null",
				"synthetic-seed=store:\" + KingdomRaidLaunchNativeFixture.StoreBlueprint",
				"KingdomRaidLaunchNativeFixture.StoredDrams + \"dr+KingdomStores=1,incident:\"",
				"KingdomRaidIncidentState.FightCommitted + \"/\" + KingdomRaidResponse.Fight",
				"KingdomRaidLaunchNativeFixture.FrozenStage",
				"KingdomRaidLaunchNativeFixture.PartySize",
				"raid-open=\" + (raid == null ? \"false\" : \"true:\" + raid.Phase)");
			ContainsAll(Flat(Method(checks, "internal static void VerifyMint(")),
				"ReferenceEquals(O.Raid, Op)", "O.Phase == KingdomLifecyclePhase.ProjectionIntent",
				"O.MintIndex == K && O.ProvedBefore == K",
				"O.MintState == KingdomLifecyclePhysicalState.Prepared",
				"KingdomRaidLaunchNativeFixture.Probed(O.RequestedBlueprint)",
				"KingdomLifecycleRules.ChildId(Op.Id, \"raider\", K)");
			string quarantine = Read(Quarantine);
			ContainsAll(Flat(Method(quarantine, "internal static void Verify(")),
				"Observations.Length == Checks.SubstituteAtSequence",
				"GameObject.Validate(second.Original)",
				"second.Substitute != null", "the abandoned correct-blueprint original was not recorded",
				"VerifyQuarantine(Fixture, Op);", "VerifyFirstRaider(Fixture, Op);",
				"VerifySubstitute(Fixture, refused, second.Substitute);", "VerifyWire(Fixture, Op);");
			// The ID clause is explicit about which of the two claims it makes (see item 4): the
			// weaker always-sound one, with the actual value recorded rather than asserted absent.
			ContainsAll(Flat(Method(quarantine, "private static void VerifySubstitute(")),
				"!string.Equals(Substitute.IDIfAssigned, Refused.ObjectId,",
				"the substitute was stamped with the refused projection identity: \"",
				"+ (Substitute.IDIfAssigned ?? \"(null)\")",
				"Substitute.GetPart<NoXPGain>() == null");
		}

		[Test]
		public void CleanupIsADisarmAndTheOnlyResetProbeIsGuardedAtVerbEntry()
		{
			// Exactly one ResetProbe call site in the whole harness, and it is the verb's entry.
			int calls = 0;
			foreach (string shard in Shards)
			{
				string source = Read(shard);
				calls += Regex.Matches(source, @"r_TAF_RaidMintProbe\.ResetProbe\(\)").Count;
				int activate = source.LastIndexOf("Activate()", StringComparison.Ordinal);
				if (activate < 0) continue;
				StringAssert.DoesNotContain("ResetProbe()", source.Substring(activate),
					shard + " must never reset the probe after activation: evidence outlives the run");
			}
			Assert.AreEqual(1, calls, "the probe may be reset at verb entry and nowhere else");
			// An unqualified in-shard call (no "r_TAF_RaidMintProbe." prefix) would not match the
			// count above, so it is counted separately here, outside ResetProbe()'s own definition.
			string probeSource = Read(Probe);
			string resetMethod = Method(probeSource, "internal static void ResetProbe()");
			int unqualified = Regex.Matches(probeSource.Replace(resetMethod, ""),
				@"(?<!\.)\bResetProbe\(\);").Count;
			Assert.AreEqual(0, unqualified,
				"an unqualified ResetProbe(); call in the probe would evade the exactly-one-reset pin");
			string provider = Read(Provider);
			int run = provider.IndexOf("KingdomRaidLaunchNativeChecks.Run(", StringComparison.Ordinal);
			Assert.Greater(run, 0, "the provider must drive the checks");
			StringAssert.DoesNotContain("ResetProbe()", provider.Substring(run),
				"the provider's finally is a disarm and nothing else");
			StringAssert.Contains(EntryGuard, Flat(Method(provider, "public string RunScenarioVerb(")),
				"the entry reset must be refused unless the probe is provably empty");
			string checks = Read(Checks);
			StringAssert.DoesNotContain("ResetProbe()",
				checks.Substring(checks.IndexOf("fixture.Activate();", StringComparison.Ordinal)),
				"Checks.Run's finally is a disarm and nothing else");
			ContainsAll(Flat(Read(Probe)),
				"internal static bool Vacant { get { return Ledger.Count == 0 && Entries.Count == 0; } }",
				"internal static int Retained { get { return Entries.Count; } }");
			// The two retained lists and the Book reference are what keeps the scene reachable.
			ContainsAll(Read(Probe),
				"private static readonly List<r_TAF_RaidMintEntry> Entries = new List<r_TAF_RaidMintEntry>();",
				"internal static KingdomLifecycleBook Book;");
		}

		[Test]
		public void ProbeFreezesPriorActorPlacementAtCallbackEntryBeforeAnySubstitution()
		{
			string source = Read(Probe);
			string handler = Flat(source.Substring(
				source.IndexOf("private void Observe(", StringComparison.Ordinal)));
			// Order: the frozen entry snapshot (authority + every prior actor's callback-time
			// placement) is taken and retained BEFORE the substitution writes the event.
			Ordered(handler,
				"entry = new r_TAF_RaidMintEntry(sequence, E.Object, E.Context, "
					+ "Book == null ? null : Book.Raid, r_TAF_RaidMintSnapshots.Priors(Entries));",
				"Entries.Add(entry);", "Substitute(E, out made, out blueprint)",
				"entry.Substitute = made;", "Ledger.Add(new r_TAF_RaidMintObservation(entry));",
				"E.ReplacementObject = Made;");
			// A per-callback zone census is exactly what the prior-actor snapshot exists to avoid.
			StringAssert.DoesNotContain("KingdomSurvey", source,
				"the probe must not survey the zone inside a creation callback");
			ContainsAll(Read(Snapshots), "public readonly GameObject Body;",
				"ObjectId = Body.IDIfAssigned;", "Zone zone = Body.CurrentZone;",
				"ZoneId = zone == null ? null : zone.ZoneID;", "Cell cell = Body.CurrentCell;",
				"if (cell != null) { CellX = cell.X; CellY = cell.Y; }",
				"public readonly int? CellX;", "public readonly int? CellY;",
				"public readonly int MarkerParts;", "public readonly string MarkerOperationId;",
				"r_KingdomRaiderObjective marker = Body.PartsList[i] as r_KingdomRaiderObjective;",
				"if (found == 0) OperationId = marker.OperationId;",
				"entry == null ? null : (entry.Substitute ?? entry.Original));");
			// ...and the checks read that physical record, not the projection's Proved bookkeeping.
			StringAssert.Contains("VerifyPriors(O, Op, K);",
				Flat(Method(Read(Checks), "internal static void VerifyMint(")));
			ContainsAll(Flat(Method(Read(Checks), "private static void VerifyPriors(")),
				"O.Priors != null && O.Priors.Length == K",
				"r_TAF_RaidMintPriorActor prior = O.Priors[j];",
				"prior != null && prior.Sequence == j + 1 && prior.Body != null "
					+ "&& GameObject.Validate(prior.Body)",
				"string.Equals(prior.ObjectId, Op.Projections[j].ObjectId, StringComparison.Ordinal)",
				"string.Equals(prior.ZoneId, Op.ZoneId, StringComparison.Ordinal) "
					+ "&& prior.CellX.HasValue && prior.CellY.HasValue",
				"prior.MarkerParts == 1 && string.Equals(prior.MarkerOperationId, Op.Id,");
			StringAssert.Contains("Check(r_TAF_RaidMintProbe.Retained == observations.Length,",
				Read(Checks), "a faulted observation must not leave the ledger short in silence");
		}

		[Test]
		public void QuarantineFaultLiteralNeverDriftsFromItsCallSite()
		{
			// A single source of truth for the literal: if either side changes by one character,
			// one of the two Contains assertions below fails.
			const string fault = "raid projection body or frozen entry cell could not be recreated";
			string quoted = "\"" + fault + "\"";
			StringAssert.Contains(quoted, Read(AttackProjection),
				"the fixed mint-loop fault literal must still exist verbatim at its documented call site");
			StringAssert.Contains("internal const string QuarantineFault =", Read(Checks),
				"Checks.QuarantineFault must remain the named constant the report and quarantine checks share");
			StringAssert.Contains(quoted, Read(Checks),
				"Checks.QuarantineFault must equal the exact call-site fault literal, verbatim");
		}

		[Test]
		public void FixtureFreezesTheRosterAndDrivesTheRealActivationEntry()
		{
			string source = Read(Fixture);
			ContainsAll(Flat(Method(source, "internal void Activate()")),
				"KingdomRaids.OnZoneActivated(System, Zone);");
			StringAssert.DoesNotContain("LaunchRaid(", source,
				"the fixture must never call the launcher directly");
			ContainsAll(Flat(Method(source, "private void FreezeRoster(")),
				"KingdomRaidProfiles.TryResolveFrozen(", "stage == FrozenStage",
				"i < KingdomRaidIncidentRules.MaxParty",
				"KingdomRaidProfiles.Blueprint(Profile, FrozenStage, Incident.Seed, i)",
				"Require(Probed(blueprint),");
			ContainsAll(source, "internal const int PartySize = 3;",
				"new[] { \"Snapjaw Scavenger 0\", \"Snapjaw Hunter 0\" }",
				"incident.State = KingdomRaidIncidentState.FightCommitted;",
				"incident.Response = KingdomRaidResponse.Fight;",
				"KingdomRaidIncidentRules.ValidLedger(book.RaidLedger)");
		}

		[Test]
		public void PersonasSealOneCaseEachAroundTheRealFoundingWithNoTeardownVerb()
		{
			foreach (string verb in new[] { "raid-launch-native-a", "raid-launch-native-b1" })
			{
				string source = Read("Tools/personas/" + verb + ".persona");
				Assert.AreEqual("founding-first-city", Setting(source, "REQUEST"));
				Assert.AreEqual("8.22@40,12", Setting(source, "START"));
				Assert.AreEqual(verb, Setting(source, "VERBS"));
				Assert.AreEqual("raids,native-regression", Setting(source, "SET"));
				CollectionAssert.AreEqual(new[] { "stagedigest", verb, "stagedigest" },
					Setting(source, "SCRIPT").Split(';'));
				CollectionAssert.AreEqual(new[] { "stagedigest:OK~founded=false",
					verb + ":OK~cases=1 passed=1 failed=0", "stagedigest:OK~founded=true",
					"COMPLETE" }, Setting(source, "EXPECT").Split(','));
			}
			// The quarantine persona declares no LOG_EXPECT on purpose: the fixed fault text is
			// emitted by the caller at Raids/KingdomRaids.09.AttackProjectionAndHelpers.cs;
			// KingdomLifecycleRules.Quarantine itself
			// (Experience/KingdomLifecycleLeaseRules.cs:196-202) writes no diagnostic, so the
			// fixed fault is journal and durable-receipt evidence, never a Player.log line.
			foreach (string raw in Read("Tools/personas/raid-launch-native-b1.persona").Split('\n'))
				Assert.IsFalse(raw.Trim().StartsWith("LOG_EXPECT=", StringComparison.Ordinal),
					"an expectation the run cannot produce would fail the persona by construction");
		}

		/// <summary>SOURCE CONTRACT, NOT INSTALLED-ELIGIBILITY PROOF. This reads only this
		/// repository - the mod-authored blueprints in RuntimeData/ObjectBlueprints.xml plus the
		/// KnownRefused list above. It never opens the installed StreamingAssets table, so it can
		/// prove a band names something known-bad; it can NOT prove the rest are spawnable. Only
		/// the all-five native gate (Harness/KingdomRaidLaunchNativeChecks.cs) proves that.
		/// </summary>
		[Test]
		public void NoShippedProfileBandNamesABlueprintKnownToBeRefused()
		{
			HashSet<string> refused = new HashSet<string>(KnownRefused, StringComparer.Ordinal);
			XmlDocument objects = new XmlDocument();
			objects.LoadXml(Read(ShippedObjects));
			XmlNodeList authored = objects.DocumentElement.SelectNodes("object");
			for (int i = 0; i < authored.Count; i++)
			{
				XmlElement row = (XmlElement)authored[i];
				XmlNodeList tags = row.SelectNodes("tag");
				for (int t = 0; t < tags.Count; t++)
				{
					string tag = ((XmlElement)tags[t]).GetAttribute("Name");
					if (tag == "BaseObject" || tag == "ExcludeFromDynamicEncounters")
						refused.Add(row.GetAttribute("Name"));
				}
			}
			XmlDocument document = new XmlDocument();
			document.LoadXml(Read(Profiles));
			XmlNodeList rows = document.DocumentElement.SelectNodes("profile");
			Assert.AreEqual(5, rows.Count, "the five shipped faction profiles");
			for (int i = 0; i < rows.Count; i++)
			{
				XmlElement row = (XmlElement)rows[i];
				foreach (string band in new[] { "Steading", "Village", "Town", "City" })
					foreach (string member in Trim(row.GetAttribute(band).Split(',')))
						Assert.IsFalse(refused.Contains(member),
							row.GetAttribute("Key") + " " + band
								+ " names a blueprint known to be refused at load: " + member);
			}
			StringAssert.DoesNotContain("Missile Cannibal", Read(Profiles),
				"the ExcludeFromDynamicEncounters cannibal must not return to any band");
		}

		/// <summary>The gate that makes the four non-Snapjaw bands visible: both verbs demand all
		/// five shipped profiles from the real KingdomRaidProfiles.TryGet surface before any
		/// Snapjaws-only assertion runs, and journal the count. The key list is derived here from
		/// the shipped XML, so adding a profile without extending the gate fails this test.
		/// </summary>
		[Test]
		public void NativeChecksGateOnAllFiveShippedProfilesBeforeAnySnapjawAssertion()
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(Read(Profiles));
			XmlNodeList rows = document.DocumentElement.SelectNodes("profile");
			List<string> keys = new List<string>();
			for (int i = 0; i < rows.Count; i++)
				keys.Add(((XmlElement)rows[i]).GetAttribute("Faction"));
			Assert.AreEqual(5, keys.Count, "the five shipped faction keys");
			string provider = Read(Provider);
			ContainsAll(Flat(provider),
				"internal static readonly string[] ShippedFactions = new[] { \""
					+ string.Join("\", \"", keys.ToArray()) + "\" };",
				"internal static string ProfilesLoaded(out int Loaded, out string Missing)",
				"KingdomRaidProfiles.TryGet(ShippedFactions[i], out profile)",
				"return \"profiles-loaded=\" + loaded + \"/\" + ShippedFactions.Length + \" \" "
					+ "+ (keys.Length == 0 ? \"-\" : keys.ToString()) + \" missing=\" + Missing;");
			// The gate runs BEFORE the fixture, so a refused band cannot hide behind a passing
			// Snapjaws run, and its row is appended to results, so it survives a failed case.
			Ordered(Flat(Method(Read(Checks), "internal static string Run(")),
				"results.Append('\\n').Append( KingdomRaidLaunchNativeProvider.ProfilesLoaded("
					+ "out loaded, out missing));",
				"Check(loaded == KingdomRaidLaunchNativeProvider.ShippedFactions.Length, "
					+ "\"shipped raid profiles refused at load, missing: \" + missing);",
				"KingdomRaidLaunchNativeFixture.TryCreate(Zone, out fixture, out failure)");
		}

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }
		private static string Flat(string Source) { return Regex.Replace(Source, @"\s+", " "); }

		private static string[] Trim(string[] Values)
		{
			string[] trimmed = new string[Values.Length];
			for (int i = 0; i < Values.Length; i++) trimmed[i] = Values[i].Trim();
			return trimmed;
		}

		private static void ContainsAll(string Source, params string[] Tokens)
		{
			foreach (string token in Tokens) StringAssert.Contains(token, Source);
		}

		private static string Method(string Source, string Signature)
		{
			int start = Source.IndexOf(Signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, Signature);
			int open = Source.IndexOf('{', start), depth = 0;
			Assert.GreaterOrEqual(open, 0, Signature);
			for (int i = open; i < Source.Length; i++)
			{
				if (Source[i] == '{') depth++;
				else if (Source[i] == '}' && --depth == 0) return Source.Substring(start, i - start + 1);
			}
			Assert.Fail("Unclosed source method: " + Signature);
			return null;
		}

		private static void Ordered(string Source, params string[] Tokens)
		{
			int cursor = 0;
			foreach (string token in Tokens)
			{
				int at = Source.IndexOf(token, cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(at, cursor, "Missing or reordered source contract: " + token);
				cursor = at + token.Length;
			}
		}

		private static string Setting(string Source, string Key)
		{
			string[] rows = Source.Split('\n');
			string found = null;
			foreach (string raw in rows)
			{
				string line = raw.Trim();
				if (!line.StartsWith(Key + "=", StringComparison.Ordinal)) continue;
				Assert.IsNull(found, Key);
				found = line.Substring(Key.Length + 1);
			}
			Assert.NotNull(found, Key);
			return found;
		}
	}
}
#endif
