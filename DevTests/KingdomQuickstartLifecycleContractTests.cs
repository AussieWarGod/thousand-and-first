#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// SOURCE-ONLY fixture. Every case here reads repository text; none of them runs the game, so
	/// nothing in this file proves that a lifecycle link actually works. The behavioural (native)
	/// coverage of this chain lives in the harness and its journals, judged by
	/// Tools/check-quickstart-lifecycle.py -- see Harness/KingdomQuickstartLifecycleRows.cs for
	/// which links have a producer today and which are owed.
	/// <para>
	/// What these cases DO protect: the contract cannot drift apart silently. The harness declares
	/// the six links' row names in C#; the checker declares them again in Python; a native journal
	/// is judged against the Python list. If the two lists diverge, a link could be renamed on one
	/// side and quietly stop being demanded on the other, and a chain that was never driven to the
	/// end would stop being reported as BLOCKER.
	/// </para>
	/// </summary>
	public class KingdomQuickstartLifecycleContractTests
	{
		private const string Rows = "Harness/KingdomQuickstartLifecycleRows.cs";
		private const string Checker = "Tools/check-quickstart-lifecycle.py";
		private const string BuildTest = "Harness/KingdomQuickstartBuildTest.cs";
		private const string LoadTest = "Harness/KingdomQuickstartLoadTest.cs";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		private static List<string> RowNames(string source)
		{
			List<string> names = new List<string>();
			foreach (Match match in Regex.Matches(source, "\"(QUICKSTART-[A-Z-]+)\""))
				if (!names.Contains(match.Groups[1].Value)) names.Add(match.Groups[1].Value);
			return names;
		}

		/// <summary>Both sides declare the same six links in the same order, so neither can drop a
		/// demand the other still believes is being made.</summary>
		[Test]
		public void TheHarnessAndTheCheckerDeclareTheSameLifecycleRowsInTheSameOrder()
		{
			List<string> harness = RowNames(Read(Rows));
			List<string> checker = RowNames(Read(Checker));
			CollectionAssert.AreEqual(harness, checker);
			ClassicAssert.AreEqual(18, harness.Count);
		}

		/// <summary>The two links with no producer are named as owed in the harness, in the same
		/// words the checker uses, so "not driven" can never read as "driven and fine".</summary>
		[Test]
		public void TheUndrivenLinksAreDeclaredOwedRatherThanQuietlyOmitted()
		{
			string rows = Read(Rows);
			StringAssert.Contains("QUICKSTART-NEXT-BEGIN", rows);
			StringAssert.Contains("Owed: a second quote and commission attempt", rows);
			StringAssert.Contains("THE SECOND SESSION", rows);
			StringAssert.Contains("\"lifecycle-loaded\", \"lifecycle-next\"", rows);
			StringAssert.Contains("with NO scenario auto-runner", rows);
			// The turn-driven half is declared as driven on the founded road, by the verbs the
			// persona seals -- so "owed" shrinks only when a producer actually exists.
			StringAssert.Contains("\"lifecycle-grown\"", rows);
		}

		/// <summary>The founded-road verbs the checker judges are the verbs the provider
		/// registers and the persona seals: one vocabulary, three files.</summary>
		[Test]
		public void TheLifecycleVerbsAgreeAcrossProviderCheckerAndPersona()
		{
			string provider = Read("Harness/KingdomQuickstartLifecycleProvider.cs");
			string checker = Read(Checker);
			// The Quickstart-lifecycle persona (ZAP-034 root ruling): the settlement's boot phase
			// already ran the exact quote/CanPay/commission sequence, so this persona's own
			// SCRIPT/VERBS deliberately omit lifecycle-build -- running it again would only refuse
			// "this lifecycle already commissioned its one job". The founding-road persona below
			// still seals all four verbs; only the vocabulary each persona SEALS differs.
			string quickstartPersona = Read("Tools/personas/lifecycle-stockpile-native-check.persona");
			string foundingPersona = Read("Tools/personas/lifecycle-founding-road-refusal.persona");
			foreach (string verb in new[] { "lifecycle-open", "lifecycle-build", "lifecycle-grown",
				"lifecycle-save" })
			{
				StringAssert.Contains("\"" + verb + "\"", provider);
				StringAssert.Contains("\"" + verb + "\"", checker);
				StringAssert.Contains(verb, quickstartPersona);
				StringAssert.Contains(verb, foundingPersona);
			}
			StringAssert.Contains("VERBS=lifecycle-open,lifecycle-grown,lifecycle-save", quickstartPersona);
			StringAssert.Contains("VERBS=lifecycle-open,lifecycle-build,lifecycle-grown,lifecycle-save", foundingPersona);
			StringAssert.Contains("SCRIPT=quickstart-lifecycle marsh yes;", quickstartPersona);
			StringAssert.DoesNotContain(";lifecycle-build;", quickstartPersona);
		}

		/// <summary>Sealed-script equality (ZAP-034): each lifecycle persona's SCRIPT= line, pinned
		/// exactly, so a future edit to either changes a test rather than silently drifting from
		/// what the harness actually expects.</summary>
		[Test]
		public void SealedScriptsAreExactlyPinnedForBothLifecycleRoads()
		{
			string quickstartPersona = Read("Tools/personas/lifecycle-stockpile-native-check.persona");
			string foundingPersona = Read("Tools/personas/lifecycle-founding-road-refusal.persona");
			StringAssert.Contains(
				"SCRIPT=quickstart-lifecycle marsh yes;stagedigest;lifecycle-open;advance 7200;"
					+ "lifecycle-grown;lifecycle-save;stagedigest",
				quickstartPersona);
			StringAssert.Contains(
				"SCRIPT=stagedigest;realize;lifecycle-open;advance 100;lifecycle-open;advance 100;"
					+ "lifecycle-open;advance 100;lifecycle-open;advance 100;lifecycle-open;advance 100;"
					+ "lifecycle-open;lifecycle-build;advance 2400;lifecycle-grown;lifecycle-save;stagedigest",
				foundingPersona);
		}

		/// <summary>The Quickstart road (ZAP-034): the runner is added, but only under the
		/// lifecycle command, and the runner never re-strips or skips a real Quickstart camp's
		/// own boot line.</summary>
		[Test]
		public void TheQuickstartRoadWiresTheRunnerWithoutTouchingProductionQuickstart()
		{
			string patch = Read("Harness/KingdomQuickstartLifecycleRunnerPatch.cs");
			StringAssert.Contains("[HarmonyPatch(typeof(QudGamemodeModule), \"bootGame\")]", patch);
			StringAssert.Contains("KingdomQuickstartBootTest.LifecycleRequested", patch);
			StringAssert.Contains("game.RequireSystem<KingdomScenarioAutoRunner>()", patch);
			string runner = Read("Harness/KingdomScenarioAutoRunner.cs");
			StringAssert.Contains("KingdomQuickstartBootTest.LifecycleRequested", runner);
			StringAssert.Contains("quickstartLifecycle", runner);
			// No re-strip and no line-0 dispatch under the lifecycle command; every other profile
			// keeps its unconditional re-strip and its Cursor = 0.
			StringAssert.Contains("if (!quickstartLifecycle)", runner);
			StringAssert.Contains("Cursor = quickstartLifecycle ? 1 : 0;", runner);
			// TheQuickstartLifecycleAuthorityIsScopedAndLeavesTheOldProfilesAlone (above) already
			// pins that boot/build/save never construct or require the runner themselves; this
			// new patch file is the one and only place that does, and only under Lifecycle.
		}

		/// <summary>Native run 13 (529a2aa) diagnosis: KingdomQuickstartBootTest.Begin is a
		/// HarmonyPrefix on the OUTER EmbarkInfo.bootGame, so it claims Popup.Suppress BEFORE the
		/// runner patch's postfix (on the INNER QudGamemodeModule.bootGame) ever runs -- backwards
		/// from what the original patch docstring assumed. Begin's own `finally` then dropped the
		/// flag unconditionally once its own OwnSuppression was true, with no idea the runner had
		/// also claimed it, stalling the very next unattended popup. This pins both the diagnostic
		/// row and the fix at that finally.</summary>
		[Test]
		public void TheDiagnosedSuppressionRaceIsBothLoggedAndFixed()
		{
			string patch = Read("Harness/KingdomQuickstartLifecycleRunnerPatch.cs");
			StringAssert.Contains("KingdomScenarioJournal.Append(\"LIFECYCLE-RUNNER\"", patch);
			StringAssert.Contains("LIFECYCLE-RUNNER patched=true added=", patch);
			StringAssert.Contains("lifecycleRequested=", patch);
			StringAssert.Contains("MetricsManager.LogInfo(\"[TAF] \" + line)", patch);
			string runner = Read("Harness/KingdomScenarioAutoRunner.cs");
			StringAssert.Contains("internal static bool Suppressing(XRLGame Game)", runner);
			StringAssert.Contains(
				"return Game?.GetSystem<KingdomScenarioAutoRunner>()?.SuppressedPopups == true;",
				runner);
			string boot = Read("Harness/KingdomQuickstartBootTest.cs");
			StringAssert.Contains(
				"if (OwnSuppression && !KingdomScenarioAutoRunner.Suppressing(Game)) Popup.Suppress = false;",
				boot);
		}

		/// <summary>Review-required fix on a16359f: LIFECYCLE-RUNNER was journaled for EVERY
		/// Quickstart-mode boot, breaking quickstart-boot/-save/-build's byte-identical journal
		/// (Tools/check-quickstart-results.py's exact positional boot-row equality) and the
		/// lifecycle persona's own strictly positional EXPECT (persona_matrix.match). Fixed by
		/// folding the LifecycleRequested check into the single early return -- the row can now
		/// exist only when that guard already passed -- and by registering it as bookkeeping so
		/// persona_matrix.significant() drops it before any positional comparison runs.</summary>
		[Test]
		public void TheLifecycleRunnerRowNeverReachesNonLifecycleQuickstartBoots()
		{
			string patch = Read("Harness/KingdomQuickstartLifecycleRunnerPatch.cs");
			StringAssert.Contains(
				"if (game == null || !KingdomQuickstartRules.IsMode(game.gameMode)", patch);
			StringAssert.Contains(
				"|| !KingdomQuickstartBootTest.LifecycleRequested) return;", patch);
			// Nothing between the guard and the Append call may itself return early on a
			// non-lifecycle path -- the guard above is the ONLY gate, so no separate check could
			// let the row through for boot/save/build.
			string guardText = "LifecycleRequested) return;";
			int guardEnd = patch.IndexOf(guardText, StringComparison.Ordinal) + guardText.Length;
			int appendAt = patch.IndexOf("KingdomScenarioJournal.Append(\"LIFECYCLE-RUNNER\"",
				StringComparison.Ordinal);
			ClassicAssert.IsTrue(guardEnd > guardText.Length && appendAt > guardEnd);
			StringAssert.DoesNotContain("return;", patch.Substring(guardEnd, appendAt - guardEnd));
			string matrix = Read("Tools/personas/persona_matrix.py");
			StringAssert.Contains("\"LIFECYCLE-RUNNER\",", matrix);
		}

		/// <summary>The turn-driven step refuses rather than passes when the job has not
		/// completed, and the refusal names the budget as the reason.</summary>
		[Test]
		public void AnExpiredTurnBudgetIsARefusalNotAPass()
		{
			string finish = Read("Harness/KingdomQuickstartLifecycleFinish.cs");
			StringAssert.Contains("job.Phase != KingdomConstructionPhase.Complete", finish);
			// The refusal now names WHY, from read state, instead of only saying time ran out.
			StringAssert.Contains("the job has not completed; turns=", finish);
			StringAssert.Contains("KingdomQuickstartLifecycleStall.Describe(", finish);
			StringAssert.Contains("KingdomConstruction.HasReceipt(Building, Job)", finish);
			// The completed work reports both its own receipt identity and the paid job it
			// fulfils, so the finished building is linkable back to what was paid for.
			StringAssert.Contains("completedReceiptId=", finish);
			StringAssert.Contains("forJobId=", finish);
			StringAssert.Contains("GetIntProperty(\"KingdomBuilt\") != 1", finish);
			StringAssert.Contains("GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Job.TargetKey", finish);
		}

		/// <summary>Native run 17 (f691ab4) fix: a plot-backed job (Job.Projection == PlotWorks,
		/// e.g. the "fire" commission) must read its progress off KingdomPlots.PlotWork* string
		/// properties, never off r_KingdomScaffold -- that read was always absent for a plot root,
		/// so every plot-backed refusal misread as "no-labour-ever" regardless of whether labour
		/// had actually run. The untruncated reading goes to its own bookkeeping row so the
		/// stamped, 300-char-bounded refusal row never has to carry it.</summary>
		[Test]
		public void StallClassificationReadsThePlotLaneForPlotBackedJobs()
		{
			string finish = Read("Harness/KingdomQuickstartLifecycleFinish.cs");
			StringAssert.Contains("KingdomQuickstartLifecycleStall.DetailMessage(Game, Zone, System, job)",
				finish);
			StringAssert.Contains("KingdomScenarioJournal.Append(KingdomQuickstartLifecycleStall.DetailRow",
				finish);
			string stall = Read("Harness/KingdomQuickstartLifecycleStall.cs");
			StringAssert.Contains("internal const string DetailRow = \"lifecycle-grown-detail\";", stall);
			// The Projection branch and the works-root read live in the split partial shard now
			// (KingdomQuickstartLifecycleStall.Detail.cs), kept under the house line cap.
			string detail = Read("Harness/KingdomQuickstartLifecycleStall.Detail.cs");
			StringAssert.Contains("internal static partial class KingdomQuickstartLifecycleStall", detail);
			StringAssert.Contains("if (Job.Projection == KingdomConstructionProjection.PlotWorks)", detail);
			StringAssert.Contains("KingdomPlots.PlotWorkRemainingProperty", detail);
			StringAssert.Contains("KingdomPlots.PlotWorkLastTickProperty", detail);
			StringAssert.Contains("KingdomPlots.PlotWorkRequiredProperty", detail);
			StringAssert.Contains("KingdomPlots.PlotWorkSchemaProperty", detail);
			StringAssert.Contains("KingdomPlots.PlotWorkWindowProperty", detail);
			// The scaffold lane is still read, but only for the non-plot branch -- neither lane
			// is dropped, only correctly chosen.
			StringAssert.Contains("r_KingdomScaffold scaffold = root.GetPart<r_KingdomScaffold>();", detail);
			// The detail row's key set, exactly as the diagnosis asked for.
			foreach (string key in new[] { "selectedId=", "candidates=", "roots=", "free=",
				"plotRemaining=", "lastSemanticTick=", "schema=" })
				StringAssert.Contains(key, detail);
			string matrix = Read("Tools/personas/persona_matrix.py");
			StringAssert.Contains("\"lifecycle-grown-detail\",", matrix);
		}

		/// <summary>Native run 23 (3e3ff75), #163: a paid, fully-laboured job whose plot stage
		/// never advances (a living occupant on the footprint refuses the apply every pass) must
		/// classify as stage-not-applied/apply-blocked-occupant, never fall through to the wrong
		/// insufficient-turns default. The occupant read is the same test production's own
		/// CanInsert refuses on, and the detail row now also carries stage-applied=/physical=/
		/// occupants= so a native run can bind the classification to what was actually found.
		/// </summary>
		[Test]
		public void StallClassificationNamesAnApplyBlockedOccupantBeforeInsufficientTurns()
		{
			// The pure classification half lives in its own engine-free shard so it can be value
			// tested, not only source-pinned -- see DevTests/
			// KingdomQuickstartLifecycleStallClassifyTests.cs.
			string classify = Read("Harness/KingdomQuickstartLifecycleStall.Classify.cs");
			StringAssert.Contains("internal const string StageNotApplied = \"stage-not-applied\";",
				classify);
			StringAssert.Contains(
				"internal const string ApplyBlockedOccupant = \"apply-blocked-occupant\";", classify);
			StringAssert.Contains(
				"if (RemainingTicks <= 0L && StageApplied < StageTarget)", classify);
			StringAssert.Contains(
				"return OccupantCount > 0 ? ApplyBlockedOccupant : StageNotApplied;", classify);
			// The new branch must land BEFORE the insufficient-turns default, not after.
			int branchAt = classify.IndexOf("StageApplied < StageTarget", StringComparison.Ordinal);
			int defaultAt = classify.IndexOf("return InsufficientTurns;", StringComparison.Ordinal);
			ClassicAssert.IsTrue(branchAt >= 0 && defaultAt > branchAt);
			string detail = Read("Harness/KingdomQuickstartLifecycleStall.Detail.cs");
			StringAssert.Contains("private static List<string> OccupantsOn(", detail);
			StringAssert.Contains("item.IsCreature || item.IsPlayer()", detail);
			StringAssert.Contains("stage-applied=", detail);
			StringAssert.Contains("occupants=", detail);
		}

		/// <summary>The cold-load session compares what it reads against the witness; it never
		/// restores from it, and a difference refuses rather than repairs.</summary>
		[Test]
		public void TheColdLoadStepReProvesByReferenceAndNeverRestores()
		{
			string load = Read("Harness/KingdomQuickstartLifecycleLoad.cs");
			foreach (string term in new[] {
				"Game.GameID != Witness.GameId",
				"system.RealmId != Witness.RealmId",
				"cityId != Witness.CityId",
				"item.IDIfAssigned != Witness.BuildingId",
				"building.GetIntProperty(\"KingdomBuilt\") != 1",
				"KingdomConstruction.HasReceipt(building, job)",
				"timber != Witness.Timber",
				"water != Witness.StoredWater" })
				StringAssert.Contains(term, load);
			// Restoring would mean writing state from the witness; comparing against it is what
			// this step does, so only the writing verbs are forbidden here.
			foreach (string forbidden in new[] { "SetIntProperty", "SetStringProperty",
				"CreateObject", "RequirePart", "AddObject" })
				StringAssert.DoesNotContain(forbidden, load);
		}

		/// <summary>No observed field may be an echo of the witness: each is read from the loaded
		/// game, and the plot identity is read from the standing building's own recorded rect.</summary>
		[Test]
		public void EveryObservedFieldOnTheLoadSideIsReadFromTheLoadedGame()
		{
			string load = Read("Harness/KingdomQuickstartLifecycleLoad.cs");
			StringAssert.DoesNotContain("Describe(Witness.", load);
			StringAssert.DoesNotContain("plotId=\" + Witness", load);
			StringAssert.Contains("string plot = KingdomQuickstartLifecycleSteps.Observed(building)", load);
			StringAssert.Contains("\"; plotId=\" + plot", load);
			StringAssert.Contains("building.Physics._CurrentCell.X", load);
			StringAssert.Contains("building.Physics._CurrentCell.ParentZone.ZoneID", load);
			// The next action re-reads the standing building rather than repeating the witness.
			StringAssert.Contains("Standing(zone, Witness, out string plotId, out string buildingId)", load);
			string finish = Read("Harness/KingdomQuickstartLifecycleFinish.cs");
			StringAssert.Contains("plotId=\" + Observed(building)", finish);
			StringAssert.Contains("KingdomPlots.TryReadRect(building, out KingdomPlotRules.PlotRect rect)", finish);
			StringAssert.DoesNotContain("Describe(job.SubjectId)", finish);
		}

		/// <summary>Functional completion is the production predicate, not a bare flag, and
		/// custody is proved by reference on both sides.</summary>
		[Test]
		public void CompletionAndCustodyUseTheProductionPredicateAndReferenceIdentity()
		{
			foreach (string path in new[] { "Harness/KingdomQuickstartLifecycleFinish.cs",
				"Harness/KingdomQuickstartLifecycleLoad.cs" })
			{
				string source = Read(path);
				StringAssert.Contains("KingdomUpgrade.IsFunctionallyBuilt(", source);
				// Called as-is: no local re-implementation and no broader registry sweep.
				StringAssert.DoesNotContain("BuiltProperty", source);
				StringAssert.DoesNotContain("HasPendingImprovementSuccessorAuthority", source);
				StringAssert.Contains("Physics._CurrentCell", source);
				StringAssert.Contains("ParentZone", source);
			}
			string load = Read("Harness/KingdomQuickstartLifecycleLoad.cs");
			// The receipt is read from the building itself, so a compacted row cannot skip it.
			StringAssert.Contains("building.GetStringProperty(KingdomConstruction.ReceiptProperty)", load);
			StringAssert.Contains("receipt != Witness.JobId", load);
			StringAssert.Contains("more than one retained registry row claims the saved job identity", load);
		}

		/// <summary>The next action mints its own job and may never report the completed one.</summary>
		[Test]
		public void TheNextActionMustMintItsOwnJob()
		{
			string load = Read("Harness/KingdomQuickstartLifecycleLoad.cs");
			StringAssert.Contains("KingdomPlots.TryQuoteCommission(system, zone, entry, null,", load);
			StringAssert.Contains("KingdomMaterials.CanPay(zone, KingdomQuickstartLifecycleSteps.BuildKey", load);
			StringAssert.Contains("KingdomCommission.Commission(system, KingdomQuickstartLifecycleSteps.BuildKey", load);
			StringAssert.Contains("ExactSingleDebit(before, after", load);
			StringAssert.Contains("job.Id == Witness.JobId", load);
			StringAssert.Contains("must mint its own job", load);
		}

		/// <summary>The lifecycle load branch is reached only through its own snapshot prefix, and
		/// no other profile's load path is altered by it.</summary>
		[Test]
		public void TheLoadBranchIsGatedByTheLifecycleAuthorityMarker()
		{
			string entry = Read("Harness/KingdomScenarioLoadEntry.cs");
			StringAssert.Contains("KingdomQuickstartLifecycleSnapshotCodec.MatchesPrefix(SnapshotWire)", entry);
			StringAssert.Contains("LifecycleSnapshot.GameId == Request.GameId", entry);
			StringAssert.Contains("KingdomQuickstartLifecycleLoad.VerifyLoaded(loaded, LifecycleSnapshot)", entry);
			StringAssert.Contains("KingdomQuickstartLifecycleLoad.Next(loaded, LifecycleSnapshot)", entry);
			// The older routes still stand exactly as they were.
			StringAssert.Contains("KingdomQuickstartLoadTest.VerifyLoaded(loaded)", entry);
			StringAssert.Contains("KingdomUpgradeLoad.VerifyLoaded(loaded)", entry);
			StringAssert.Contains("taf-lifecycle-save-v1:", Read("Harness/KingdomQuickstartLifecycleSnapshot.cs"));
		}

		/// <summary>
		/// The Quickstart lifecycle variant is a SEPARATE command with its own authority, and the
		/// three old commands keep exactly the contract they had: one line, and no auto-runner.
		/// </summary>
		[Test]
		public void TheQuickstartLifecycleAuthorityIsScopedAndLeavesTheOldProfilesAlone()
		{
			string request = Read("Harness/KingdomQuickstartBootRequest.cs");
			StringAssert.Contains("LifecycleVerb = \"quickstart-lifecycle\"", request);
			StringAssert.Contains("if (!lifecycle && Script.Count != 1) return false;", request);
			string boot = Read("Harness/KingdomQuickstartBootTest.cs");
			StringAssert.Contains("Request.Lifecycle || Game.GetSystem<KingdomScenarioAutoRunner>() == null",
				boot);
			// The runner is never CREATED by a Quickstart shard, whichever command is running.
			foreach (string path in new[] { "Harness/KingdomQuickstartBootTest.cs",
				"Harness/KingdomQuickstartBuildTest.cs", "Harness/KingdomQuickstartSaveState.cs" })
				foreach (string forbidden in new[] { "new KingdomScenarioAutoRunner",
					"RequireSystem<KingdomScenarioAutoRunner" })
					StringAssert.DoesNotContain(forbidden, Read(path));
			// The save path's own exclusion is untouched.
			StringAssert.Contains("GetSystem<KingdomScenarioAutoRunner>() == null",
				Read("Harness/KingdomQuickstartSaveState.cs"));
			string profile = Read("Tools/scenario_profile.py");
			StringAssert.Contains("QUICKSTART_LIFECYCLE_VERB = \"quickstart-lifecycle\"", profile);
			StringAssert.Contains("QUICKSTART_VERBS = (QUICKSTART_BOOT_VERB, QUICKSTART_SAVE_VERB, QUICKSTART_BUILD_VERB)",
				profile);
			StringAssert.Contains("if tokens[:1] == [QUICKSTART_LIFECYCLE_VERB]:", profile);
		}

		/// <summary>The starter-chest commission hands its exact job to the lifecycle verbs, and
		/// only under the lifecycle command.</summary>
		[Test]
		public void TheQuickstartBuildPhaseCarriesItsJobOnlyForTheLifecycleCommand()
		{
			string build = Read("Harness/KingdomQuickstartBuildTest.cs");
			StringAssert.Contains("if (KingdomQuickstartBootTest.LifecycleRequested)", build);
			StringAssert.Contains("KingdomQuickstartLifecycleSteps.JobKey, job.Id", build);
			StringAssert.Contains("TakeStock(Zone, stockpile, true,", build);
			string boot = Read("Harness/KingdomQuickstartBootTest.cs");
			StringAssert.Contains("Request.Build || Request.Lifecycle", boot);
		}

		/// <summary>Every lifecycle row names the profile it was launched from, read from that
		/// profile's own sealed root, and a run that cannot name it refuses -- including a
		/// refusal row: Refuse(...) and every hand-built refusal string route through Stamped(...)
		/// too, so no lifecycle-* row (success or refusal) can be written unstamped.</summary>
		[Test]
		public void EveryLifecycleRowIncludingRefusalsIsStampedWithItsLaunchedProfile()
		{
			string stamp = Read("Harness/KingdomQuickstartLifecycleStamp.cs");
			StringAssert.Contains("internal static string Text(string Root)", stamp);
			StringAssert.Contains("KingdomScenarioJournal.ProfileRoot()",
				Read("Harness/KingdomQuickstartLifecycleSteps.cs"));
			StringAssert.Contains("\"profile.sha256\"", stamp);
			StringAssert.Contains("taf-scenario-profile-seal-v1", stamp);
			StringAssert.Contains("\"profile=\" + name + \" seal=\" + digest", stamp);
			string steps = Read("Harness/KingdomQuickstartLifecycleSteps.cs");
			StringAssert.Contains("internal static string Stamped(string Report)", steps);
			StringAssert.Contains("the launched profile could not be named from its ", steps);
			// Refuse(...) itself routes through Stamped(...): the two success rows in Steps.cs
			// (Open, Build), the open-wait "still waiting" row Open() journals while its bounded
			// stockpile-dedication wait is unresolved, and Refuse's own return make four; Finish.cs
			// has no refusal helper of its own and calls the shared Refuse(...), so its count stays
			// at its two success rows only.
			StringAssert.Contains(
				"return Stamped(\"native-lifecycle refused at \" + Step + \": \" + Bounded(Reason));",
				steps);
			StringAssert.Contains("return Stamped(\"native-lifecycle step=open-wait; ", steps);
			ClassicAssert.AreEqual(4, Occurrences(steps, "return Stamped("), "Steps.cs");
			ClassicAssert.AreEqual(2,
				Occurrences(Read("Harness/KingdomQuickstartLifecycleFinish.cs"), "return Stamped("),
				"Finish.cs");
			string load = Read("Harness/KingdomQuickstartLifecycleLoad.cs");
			ClassicAssert.AreEqual(2,
				Occurrences(load, "KingdomQuickstartLifecycleSteps.Stamped("), "load success rows");
			// The two load refusal rows carry no hand-built literal any more: they route through
			// the shared Refuse(...) helper, which is itself stamped.
			ClassicAssert.AreEqual(2,
				Occurrences(load, "KingdomQuickstartLifecycleSteps.Refuse("), "load refusal rows");
			// No lifecycle shard may return a bare, unstamped refusal literal: every such return
			// must go through Refuse(...) or Stamped(...) instead.
			foreach (string path in new[] { "Harness/KingdomQuickstartLifecycleSteps.cs",
				"Harness/KingdomQuickstartLifecycleFinish.cs", "Harness/KingdomQuickstartLifecycleLoad.cs" })
				ClassicAssert.AreEqual(0,
					Occurrences(Read(path), "return \"native-lifecycle refused"),
					path + " must carry no unstamped refusal literal");
			// The Quickstart lifecycle variant stamps its own row, and only that variant does.
			string build = Read("Harness/KingdomQuickstartBuildTest.cs");
			StringAssert.Contains("QUICKSTART-LIFECYCLE-PROFILE", build);
			StringAssert.Contains("if (KingdomQuickstartBootTest.LifecycleRequested)", build);
		}

		/// <summary>The checker demands the stamp agree with that session's own run record.</summary>
		[Test]
		public void TheCheckerBindsEachRowToItsSessionsRecordedProfile()
		{
			string checker = Read("Tools/check-quickstart-lifecycle.py");
			StringAssert.Contains("def check_stamps(", checker);
			StringAssert.Contains("carries no profile stamp", checker);
			StringAssert.Contains("record.get(\"profileName\") != name", checker);
			StringAssert.Contains("record.get(\"profileSeal\") != seal", checker);
		}

		private static int Occurrences(string Source, string Token)
		{
			int total = 0;
			for (int at = Source.IndexOf(Token, StringComparison.Ordinal); at >= 0;
				at = Source.IndexOf(Token, at + Token.Length, StringComparison.Ordinal)) total++;
			return total;
		}

		/// <summary>An unfinished job is diagnosed, not merely declared over budget: the four
		/// cases are named, every field they rest on is read, and none of it repairs anything.</summary>
		[Test]
		public void AnUnfinishedJobIsClassifiedFromReadProductionState()
		{
			// KingdomQuickstartLifecycleStall is now split across three partial shards to stay
			// under the house line cap: .cs (Describe + shared helpers), .Classify.cs (the pure,
			// engine-free classification), .Detail.cs (the untruncated DetailRow reading).
			string stall = Read("Harness/KingdomQuickstartLifecycleStall.cs");
			string classify = Read("Harness/KingdomQuickstartLifecycleStall.Classify.cs");
			string detail = Read("Harness/KingdomQuickstartLifecycleStall.Detail.cs");
			foreach (string token in new[] { "\"pass-never-ran\"", "\"no-labour-ever\"",
				"\"labour-stalled\"", "\"insufficient-turns\"", "\"stage-not-applied\"",
				"\"apply-blocked-occupant\"" })
				StringAssert.Contains(token, classify);
			foreach (string field in new[] { "scaffold.RemainingTicks", "scaffold.LastWorkedTick",
				"r_KingdomScaffold.WorkWindowProperty", "KingdomConstructionPresence.SelectedProperty",
				"KingdomConstructionPresence.HandsProperty",
				"KingdomConstructionPresence.EffectivenessProperty",
				"KingdomConstructionPresence.SchemaProperty" })
				StringAssert.Contains(field, detail);
			foreach (string field in new[] { "System.LastSemanticTick",
				"Job.StartedTick", "Job.DueTick", "Job.UpdatedTick", "Job.InputReceipt" })
				StringAssert.Contains(field, stall);
			// Reads only: nothing here writes state or advances a clock, across all three shards.
			foreach (string source in new[] { stall, classify, detail })
				foreach (string forbidden in new[] { "SetIntProperty", "SetStringProperty",
					"AdvanceDurable", "RetryDurable", "= now;" })
					StringAssert.DoesNotContain(forbidden, source);
			string finish = Read("Harness/KingdomQuickstartLifecycleFinish.cs");
			StringAssert.Contains("KingdomQuickstartLifecycleStall.Describe(Game, Zone, System, job)", finish);
			StringAssert.DoesNotContain("the turn budget expired before this building stood", finish);
			string checker = Read("Tools/check-quickstart-lifecycle.py");
			StringAssert.Contains("STALL_CLASSES = (", checker);
			StringAssert.Contains("never a pass or a waiver", checker);
		}

		/// <summary>The lifecycle verbs mint no stock and force no phase.</summary>
		[Test]
		public void TheLifecycleDriverFabricatesNothing()
		{
			foreach (string path in new[] { "Harness/KingdomQuickstartLifecycleSteps.cs",
				"Harness/KingdomQuickstartLifecycleFinish.cs",
				"Harness/KingdomQuickstartLifecycleLoad.cs" })
			{
				string source = Read(path);
				foreach (string forbidden in new[] { "CreateObject", "CreateUnmodifiedObject",
					"Phase = KingdomConstructionPhase", "SetIntProperty(\"KingdomBuilt\"",
					"UpdatePhysical(" })
					StringAssert.DoesNotContain(forbidden, source);
			}
		}

		/// <summary>A missing link must reach BLOCKER, and BLOCKER must not share PASS's exit
		/// code: a chain that stopped early can then never be read as a success by a caller that
		/// only checks the process status.</summary>
		[Test]
		public void TheCheckerTreatsAnUndrivenLinkAsBlockerAndNeverAsPass()
		{
			string checker = Read(Checker);
			StringAssert.Contains("no rows for this link; it was never driven", checker);
			StringAssert.Contains("EXITS = {PASS: 0, BLOCKER: 3, FAIL: 4}", checker);
			StringAssert.DoesNotContain("verdict = PASS if", checker);
			// The artefact never carries its own excuses, so a reader of the file alone cannot
			// mistake an unfinished chain for a complete one that merely explains itself.
			StringAssert.Contains("Blockers are NOT smuggled into it", checker);
		}

		/// <summary>The executable links this chain already owns keep their physical assertions:
		/// the commission's exact timber and water debit, the new paid job, and the restored
		/// identities after a cold load. A weakening here would empty the chain from the middle.</summary>
		[Test]
		public void TheAlreadyDrivenLinksKeepTheirPhysicalAndIdentityAssertions()
		{
			string build = Read(BuildTest);
			StringAssert.Contains("ExactSingleDebit(stockBefore, stockAfter", build);
			StringAssert.Contains("KingdomMaterial.Timber, 1", build);
			StringAssert.Contains("ExactWaterDebit(waterBefore, waterAfter, entry.CostDrams", build);
			StringAssert.Contains("TryNewPaidJob(jobsBefore, jobsAfter, Zone, system, BuildKey", build);
			StringAssert.Contains("SameStockpile(stockBefore, stockAfter", build);
			string load = Read(LoadTest);
			StringAssert.Contains("KingdomQuickstartSaveState.Verify(Game, KingdomScenarioLoadEntry.QuickstartSnapshot)", load);
			StringAssert.Contains("exact-saved-heart-stock-and-IDs=true", load);
			StringAssert.Contains("bootstrap-replay=false", load);
		}

		/// <summary>The commission is still reached through the production three-call sequence the
		/// Charter UI itself drives, not through a shortcut the harness invented.</summary>
		[Test]
		public void ThePaidCommissionLinkStillDrivesTheProductionSequence()
		{
			string build = Read(BuildTest);
			int quote = build.IndexOf("KingdomPlots.TryQuoteCommission(system, Zone, entry, null,", StringComparison.Ordinal);
			int canPay = build.IndexOf("KingdomMaterials.CanPay(Zone, BuildKey, out string materialBlocker)", StringComparison.Ordinal);
			int commission = build.IndexOf("KingdomCommission.Commission(system, BuildKey, null,", StringComparison.Ordinal);
			ClassicAssert.IsTrue(quote >= 0 && canPay > quote && commission > canPay,
				"quote, CanPay and Commission must still run in that order");
		}
	}
}
#endif
