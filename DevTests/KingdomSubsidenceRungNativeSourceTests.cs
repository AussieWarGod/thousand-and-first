#if TAF_TESTS
using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	// Source contracts only. Native journal and strict compiler evidence are separate gates.
	[TestFixture]
	public sealed class KingdomSubsidenceRungNativeSourceTests
	{
		private const string Checks = "Harness/KingdomSubsidenceRungNativeChecks.cs";
		private const string Provider = "Harness/KingdomSubsidenceRungNativeProvider.cs";
		private const string Fault = "Harness/KingdomSubsidenceRungNativeFault.cs";
		private const string Fixture = "Harness/KingdomSubsidenceRungNativeFixture.cs";
		private const string Death = "Harness/KingdomResidentDeathNativeChecks.cs";

		[Test]
		public void CompletedSummaryIncludesTheFinalNamedDepartureNotOnlyRetiredSteps()
		{
			Assert.AreEqual(2, Enumerable.Range(0, 10).Count(i => KingdomSubsidenceRules.TellsDeparture(i, 15)));
			Assert.AreEqual(3, Enumerable.Range(0, 15).Count(i => KingdomSubsidenceRules.TellsDeparture(i, 15)));
			Assert.AreEqual(3, KingdomSubsidenceRules.NamedDepartures(15));
			Contains(Read(Checks), "KingdomSubsidenceRules.NamedDepartures(15)");
		}

		[Test]
		public void FixtureCandidateStreamsVaryBeforeLongOwnerPrefixTruncation()
		{
			string owner = new string('a', 200);
			Assert.AreEqual(KingdomSubsidenceRules.WorkStream(owner + ":0"),
				KingdomSubsidenceRules.WorkStream(owner + ":1"), "negative control must share the truncated prefix");
			Assert.AreNotEqual(KingdomSubsidenceRules.WorkStream("native-rung-hut:0:" + owner),
				KingdomSubsidenceRules.WorkStream("native-rung-hut:1:" + owner));
			Contains(Read(Fixture), "\"native-rung-hut:\" + i.ToString(CultureInfo.InvariantCulture) + \":\" + Realm");
		}

		[Test]
		public void SourceContract_SixNamedCasesUseTheirOwnExactFreshProfileVerb()
		{
			string persona = Read("Tools/personas/subsidence-rung-native-checks.persona");
			Contains(persona, "REQUEST=founding-first-city", "SCRIPT=stagedigest;subsidence-rung-check;stagedigest",
				"VERBS=subsidence-rung-check", "subsidence-rung-check:OK~cases=6 passed=6 failed=0");
			string source = Read(Checks);
			int branch = source.IndexOf("if (DeathPrepared)", StringComparison.Ordinal);
			Assert.Greater(branch, 0);
			string[] shared = PassedCases(source.Substring(0, branch));
			string[] wear = PassedCases(Between(source, "current = \"actual-wear-after-synthetic-authority-cut\";", "catch (Exception error)"));
			string[] death = PassedCases(Read(Death));
			CollectionAssert.AreEqual(new[] { "synthetic-home-and-work-fixture", "fifteen-departures-before-rung-capture",
				"exact-rung-and-roof-capture" }, shared);
			CollectionAssert.AreEqual(new[] { "actual-wear-after-synthetic-authority-cut",
				"production-rung-recovery-and-retirement", "same-tick-no-rung-replay" }, wear);
			CollectionAssert.AreEqual(new[] { "unrelated-engine-death", "selected-engine-death",
				"prepared-death-recovery-no-replay" }, death);
			Assert.AreEqual(6, shared.Concat(wear).Distinct().Count());
			Assert.AreEqual(6, shared.Concat(death).Distinct().Count());
			string deathBranch = Between(source, "if (DeathPrepared)", "current = \"actual-wear-after-synthetic-authority-cut\";");
			Ordered(deathBranch, "current = \"prepared-roof-native-deaths\";",
				"KingdomResidentDeathNativeChecks.Run(fixture, plan, results, ref passed);", "else");
			StringAssert.DoesNotContain("passed++", deathBranch);
			Contains(Read(Provider), "ExpectedCases = 6", "r_TAF_ScenarioSubsidenceRungChecks_v1",
				"!KingdomLodging.Enabled", "Game.TimeTicks <= 3L * KingdomSubsidenceStepRules.StepTicks",
				"script.Count != 3", "script[1] != RequestedVerb", "KingdomScenarioTransactionShape.None",
				"HasAnyState(Game, Receipt)", "ProvesExactText(Receipt, \"intent\")");
		}

		[Test]
		public void SourceContract_ActualDeparturePathPrecedesPlanCaptureAndWearCut()
		{
			Ordered(Read(Checks), "KingdomSubsidenceRungNativeFixture.TryCreate(",
				"now - 3L * KingdomSubsidenceStepRules.StepTicks", "KingdomSubsidenceNativeClockSeed.TrySeed(",
				"KingdomSubsidenceNativeClockChecks.Verify(", "fixture.Work.AddPart(fault)",
				"KingdomSubsidence.TryReckon(system, Zone, fixture.Survey, now, out failure)",
				"fault.Throws == 1 && fault.CommittedBeforeFault", "KingdomSubsidenceStepRuntime.TryPrepareRung(",
				"KingdomSubsidenceStepRules.TryArmRungWear(", "system.City.SubsidenceModel = intentWire",
				"KingdomSubsidenceWearRuntime.TryApply(", "KingdomSubsidenceStepRuntime.TryBeforePass(");
			Contains(Read(Checks), "absent == 15", "KingdomResidents.DepartureCarriersAbsent(",
				"system.Population == 35", "system.Stage == GrowthStage.Town");
		}

		[Test]
		public void SourceContract_NativeCallbackArmsOnExactUnplannedRungNotEventCount()
		{
			Contains(Read(Fault), "GetDisplayNameEvent.ID", "ReferenceEquals(E.Object, ParentObject)",
				"Throws == 0", "book.Active.Phase == KingdomSubsidenceStepPhase.Settling",
				"book.Active.Completed == 5", "book.Active.DueTick == DueTick",
				"book.Active.RungModel == KingdomSubsidenceStepRules.UnplannedRungs",
				"book.Sequence == 3", "KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture)");
			Ordered(Read(Fault), "Throws++;", "CommittedBeforeFault =", "throw new InvalidOperationException(");
		}

		[Test]
		public void SourceContract_ActualWearAfterCutIsNotMislabelledAsEngineCallback()
		{
			Contains(Read(Checks), "if (fixture.Wear.Wear == fixture.AfterWear) { afterCuts++; return false; }",
				"failure == KingdomSubsidenceWearRuntime.AuthorityLost && afterCuts == 1",
				"fixture.Wear.IncidentPhase == (int)KingdomWearIncidentPhase.MutationIntent",
				"fixture.Wear.IncidentId == stepId && !Roof(fixture).RoofStanding",
				"parent-intent=seeded; wear-write=production", "construction-and-assignment=seeded");
			Assert.IsFalse(Regex.IsMatch(Read(Checks), @"\bfixture\.Wear\.Wear\s*=(?!=)"));
		}

		[Test]
		public void SourceContract_RecoveryChecksActualRoofDateReceiptReleaseTellingAndNoReplay()
		{
			Contains(Read(Checks), "retired.Active == null && retired.Sequence == 3", "retired.LastRetiredTick == now",
				"retired.BatchModel == KingdomSubsidenceBatchRules.None", "fixture.Wear.LastCompletedIncidentId == stepId",
				"roof.RoofStanding && roof.Reached == now && roof.Warned == KingdomBrinkRules.Unwarned",
				"Count(system.ChronicleEntries, stageLine) == 1", "Count(system.ChronicleEntries, ruinLine) == 1",
				"Same(system.ChronicleEntries, official)", "Same(system.OutsiderEntries, outsider)",
				"Same(system.Ledger.Notes, notes)", "system.City.SubsidenceModel == wire",
				"Count(system.ChronicleEntries, summaryLine) == 1", "Prefix(system.ChronicleEntries, officialPrefix)",
				"Prefix(system.OutsiderEntries, outsiderPrefix)", "copies == 1", "Fixture.Wear.IncidentId == null",
				"Fixture.Wear.LastCompletedIncidentId == Plan.StepId", "ReferenceEquals(Fixture.Wear.ParentObject, work)",
				"roof.RoofStanding && roof.Reached == Plan.DueTick");
			Assert.AreEqual(2, Regex.Matches(Read(Checks), @"VerifyEffects\(fixture, plan\);").Count);
		}

		[Test]
		public void SourceContract_RetainsEffectsAndLimitsAcceptance()
		{
			Contains(Read(Checks), "fixture.TryReleaseHeld(", "fixture.Work.RemovePart(fault)",
				"handler-cleanup=FAIL unknown state retained", "ordinary-acceptance=false; save-load=untested",
				"elapsed=seeded-checkpoint", "profile-and-effects-retained=true");
			StringAssert.DoesNotContain("LastAttempt = null", Read(Fixture));
			foreach (string path in new[] { Checks, Provider, Fault, Fixture, Death })
			{
				string source = Read(path);
				Assert.Less(source.Split('\n').Length - (source.EndsWith("\n", StringComparison.Ordinal) ? 1 : 0), 300, path);
				StringAssert.DoesNotContain(".Destroy(", source);
				StringAssert.DoesNotContain(".Obliterate(", source);
				Assert.IsFalse(Regex.IsMatch(source, @"\bGame\.TimeTicks\s*=(?!=)"));
			}
		}

		[Test]
		public void SourceContract_PreparedDeathVerbHasExactFreshAdmissionWithoutArguments()
		{
			string source = Read(Provider);
			Contains(source, "DeathVerb = \"subsidence-rung-death-check\"",
				"return script == Verb || script == DeathVerb", "return new[] { Verb, DeathVerb }",
				"(Verb != KingdomSubsidenceRungNativeProvider.Verb && Verb != DeathVerb) || !string.IsNullOrEmpty(Argument)",
				"RequestedVerb == DeathVerb && !KingdomOffices.Enabled", "script[1] != RequestedVerb");
			Ordered(source, "!string.IsNullOrEmpty(Argument)", "!Eligible(game, zone, Verb, out failure)",
				"game.SetStringGameState(Receipt, \"intent\")", "ProvesExactText(Receipt, \"intent\")",
				"Run(game, zone, out Ok, Verb == DeathVerb)");
			StringAssert.DoesNotContain("DeathArgument", source);
			Contains(Read(Death), "script[1] == KingdomSubsidenceRungNativeProvider.DeathVerb");
			Contains(Read(Checks), "out bool Ok, bool DeathPrepared = false", "if (DeathPrepared)");
			Contains(Read("Harness/KingdomSubsidenceNativeClockSeed.cs"),
				"KingdomSubsidenceRungNativeProvider.ExactScript(script[1])",
				"receipt = KingdomSubsidenceRungNativeProvider.Receipt");
			Contains(Read("Harness/KingdomSubsidenceNativeClockChecks.cs"),
				"script[1] == verbs[i] || i == 1 && KingdomSubsidenceRungNativeProvider.ExactScript(script[1])",
				"HasAnyState(game, receipts[i])", "Require(matches == 1");
			string persona = Read("Tools/personas/subsidence-rung-death-native-checks.persona");
			Contains(persona, "REQUEST=founding-first-city", "SCRIPT=stagedigest;subsidence-rung-death-check;stagedigest",
				"VERBS=subsidence-rung-death-check", "subsidence-rung-death-check:OK~cases=6 passed=6 failed=0");
			string original = Read("Tools/personas/subsidence-rung-native-checks.persona");
			string diagnostics = persona.Split('\n').Single(line => line.StartsWith("LOG_EXPECT=", StringComparison.Ordinal));
			Assert.AreEqual(original.Split('\n').Single(line => line.StartsWith("LOG_EXPECT=", StringComparison.Ordinal)), diagnostics);
			StringAssert.DoesNotContain("TAF_LOG_ALLOW", persona);
		}

		[Test]
		public void SourceContract_RealDieMustProduceExactSettledWitnessAndMeasuredRemoval()
		{
			string source = Read(Death);
			Ordered(source, "Journal(f) == null", "Kill(f, unrelated, false);", "Kill(f, fixture.HomeResident, true);");
			string kill = Between(source, "private static void Kill(", "private static void Verify(");
			Ordered(kill, "KingdomCitizenship.BelongsTo(f.System, body)", "death target binding changed",
				"death target row missing", "string[] accounts = Accounts(f.System);", "Exact(f);", "body.Die(Force: true);",
				"!GameObject.Validate(body)", "cell.GetObjects()", "TryCaptureGlobalLiveIds(",
				"!live.ContainsKey(objectId)", "receipt.Phase == KingdomResidentDeathPhase.Settled", "Verify(f);");
			Contains(kill, "journal.Entries.Count == f.Killed.Count + 1", "receipt.Before.ResidentId == id",
				"receipt.Body == objectId", "receipt.Tick == f.Now", "receipt.MintedTick == bound.MintedTick",
				"KingdomResidentDeathCodec.Row(receipt.Before) == KingdomResidentDeathCodec.Row(before)",
				"receipt.StepWire == f.Wire", "receipt.Memory", "receipt.RoleFault == \"\"",
				"receipt.Cause == KingdomStandingCause.Unwitnessed", "receipt.RoofBlocked == selected",
				"receipt.RoofIndex == (selected ? 0 : -1)", "receipt.RoofWork == (selected ? f.Plan.Works[0].ObjectId : \"\")");
			Contains(source, "!g.HasIntGameState(f.Key)", "!g.HasInt64GameState(f.Key)",
				"!g.HasObjectGameState(f.Key)", "!g.HasBooleanGameState(f.Key)", "!g.HasStringGameState(f.Key)",
				"KingdomResidentDeathCodec.TryDecode(wire", "encoded == wire",
				"journal.Realm == f.Realm && journal.Settlement == f.Settlement");
			Assert.AreEqual(1, Regex.Matches(source, @"\bbody\.Die\(").Count);
			StringAssert.DoesNotContain("KingdomResidentDeathRuntime.Record(", source);
		}

		[Test]
		public void SourceContract_DeathKeepsPreparedRoofAndChecksAccountingWithoutReplayingRung()
		{
			string source = Read(Death);
			Contains(source, "f.City.SubsidenceModel == f.Wire", "s.LastSubsidenceTick == f.Checkpoint",
				"s.Ledger.Departures == f.Departures", "KingdomResidentDepartureRules.IsEmpty(s.ResidentDeparture)",
				"f.Plan.Works[0].WearPhase == KingdomSubsidenceEffectPhase.Prepared",
				"f.Plan.Works[0].Roofs[0].Phase == KingdomSubsidenceEffectPhase.Prepared",
				"ReferenceEquals(carriers[i], f.RoofCarriers[i])", "Equals(carriers[i][j], f.RoofValues[i][j])",
				"f.System.Population == 35 - f.Killed.Count", "f.System.Dead == f.Dead + f.Killed.Count",
				"bindings.Count == 35 - f.Killed.Count", "state.ResidentCount == 35",
				"before.WithStanding(KingdomResidentStanding.Dead, KingdomStandingCause.Unwitnessed)",
				"binding.MintedTick == priorBinding.MintedTick", "living == 35 - f.Killed.Count",
				"KingdomResidentDeathRules.TryPrepareAccounts(oracle, accounts, out var expected)",
				"expected.AccountProof == receipt.AccountProof", "Same(Accounts(f.System), expected.AfterAccounts)");
			string recovery = Between(source, "string journal = f.Fixture.Base.Game.GetStringGameState(f.Key);", "private static void Kill(");
			Ordered(recovery, "for (int i = 0; i < 2; i++)", "Exact(f);",
				"KingdomResidentDeathRuntime.TryRecoverPending(s, out failure)", "Verify(f);",
				"GetStringGameState(f.Key) == journal", "Same(Accounts(s), accounts)",
				"Same(s.ChronicleEntries.ToArray(), official)", "Same(s.OutsiderEntries.ToArray(), outsider)",
				"Same(s.Ledger.Notes.ToArray(), notes)");
			StringAssert.DoesNotContain("TryResumeRung(", source);
			StringAssert.DoesNotContain("TryPublishWitnessedDeath(", source);
			Assert.IsFalse(Regex.IsMatch(source, @"\.SubsidenceModel\s*=(?!=)|\.Population\s*=(?!=)|\.TimeTicks\s*=(?!=)"));
		}

		[Test]
		public void SourceContract_SelectedDeathReleasesOnlyItsHeldPartyAndRetainsFailureEvidence()
		{
			string source = Read(Death);
			Ordered(source, "Kill(f, unrelated, false);", "Exact(f);",
				"Check(fixture.TryReleaseHeld(out string failure), failure);", "Exact(f);", "Kill(f, fixture.HomeResident, true);");
			Contains(source, "ReferenceEquals(KingdomSubsidenceRungNativeFixture.LastAttempt, f.Fixture)",
				"ReferenceEquals(KingdomSubsidenceNativeFixture.LastAttempt, f.Fixture.Base)",
				"ReferenceEquals(The.Player, f.Player)", "ReferenceEquals(s.City, f.City)",
				"ReferenceEquals(s.Bindings, f.Bindings)", "ReferenceEquals(s.Ledger, f.Ledger)",
				"ReferenceEquals(tables[i], f.Tables[i])", "telling-delivery=not-claimed");
			Ordered(Read(Checks), "finally", "fixture.Work.RemovePart(fault)", "if (!clean)",
				"fixture.TryReleaseHeld(out string cleanup)", "Math.Min(passed, 5)", "scope?.Dispose();",
				"Ok = passed == KingdomSubsidenceRungNativeProvider.ExpectedCases");
			string release = Between(Read(Fixture), "internal bool TryReleaseHeld(", "private void RequireWorld(");
			Ordered(release, "if (!HoldAttempted)", "GameObject.Validate(HomeResident)",
				"ReferenceEquals(HeldBrain.PartyLeader, Player)", "HeldBrain.PartyLeader = null", "HoldAttempted = false");
			StringAssert.DoesNotContain(".PartyLeader =", source);
			StringAssert.DoesNotContain(".Clear(", source);
			StringAssert.DoesNotContain("LastAttempt = null", source);
			Contains(Read(Checks), "ordinary-acceptance=false; save-load=untested", "profile-and-effects-retained=true");
		}

		private static string[] PassedCases(string Source)
		{
			return Regex.Matches(Source, @"passed\+\+;\s*results\.Append\(""\\n([^""=]+)=PASS")
				.Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
		}
		private static string Between(string Source, string Start, string End)
		{
			int first = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, Start);
			int last = Source.IndexOf(End, first + Start.Length, StringComparison.Ordinal);
			Assert.Greater(last, first, End);
			return Source.Substring(first, last - first);
		}

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }
		private static void Contains(string Source, params string[] Tokens)
		{
			foreach (string token in Tokens) StringAssert.Contains(token, Source);
		}
		private static void Ordered(string Source, params string[] Tokens)
		{
			int cursor = 0;
			foreach (string token in Tokens)
			{
				int at = Source.IndexOf(token, cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(at, cursor, token);
				cursor = at + token.Length;
			}
		}
	}
}
#endif
