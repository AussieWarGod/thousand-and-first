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
			string persona = Read("Tools/personas/lifecycle-stockpile-native-check.persona");
			foreach (string verb in new[] { "lifecycle-open", "lifecycle-build", "lifecycle-grown",
				"lifecycle-save" })
			{
				StringAssert.Contains("\"" + verb + "\"", provider);
				StringAssert.Contains("\"" + verb + "\"", checker);
				StringAssert.Contains(verb, persona);
			}
			StringAssert.Contains("VERBS=lifecycle-open,lifecycle-build,lifecycle-grown,lifecycle-save", persona);
		}

		/// <summary>The turn-driven step refuses rather than passes when the job has not
		/// completed, and the refusal names the budget as the reason.</summary>
		[Test]
		public void AnExpiredTurnBudgetIsARefusalNotAPass()
		{
			string finish = Read("Harness/KingdomQuickstartLifecycleFinish.cs");
			StringAssert.Contains("job.Phase != KingdomConstructionPhase.Complete", finish);
			StringAssert.Contains("the turn budget expired before this building stood", finish);
			StringAssert.Contains("KingdomConstruction.HasReceipt(Building, Job)", finish);
			// The completed work reports both its own receipt identity and the paid job it
			// fulfils, so the finished building is linkable back to what was paid for.
			StringAssert.Contains("completedReceiptId=", finish);
			StringAssert.Contains("forJobId=", finish);
			StringAssert.Contains("GetIntProperty(\"KingdomBuilt\") != 1", finish);
			StringAssert.Contains("GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Job.TargetKey", finish);
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
		/// profile's own sealed root, and a run that cannot name it refuses.</summary>
		[Test]
		public void EveryLifecycleRowIsStampedWithItsLaunchedProfile()
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
			foreach (string path in new[] { "Harness/KingdomQuickstartLifecycleSteps.cs",
				"Harness/KingdomQuickstartLifecycleFinish.cs" })
				ClassicAssert.AreEqual(2, Occurrences(Read(path), "return Stamped("), path);
			string load = Read("Harness/KingdomQuickstartLifecycleLoad.cs");
			ClassicAssert.AreEqual(2,
				Occurrences(load, "KingdomQuickstartLifecycleSteps.Stamped("), "load rows");
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
