#if TAF_TESTS
using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Wiring tripwires only; the persona supplies actual native contact evidence.</summary>
	[TestFixture]
	public sealed class KingdomRaidContactNativeSourceTests
	{
		private const string Provider = "Harness/KingdomRaidContactNativeProvider.cs";
		private const string Checks = "Harness/KingdomRaidContactNativeChecks.cs";

		[Test]
		public void ContactProviderRequiresFreshScriptBeforeIntentAndExecution()
		{
			string provider = Read(Provider);
			StringAssert.Contains("[KingdomScenarioVerbProvider]", provider);
			StringAssert.Contains("internal const string Verb = \"raid-contact-native-check\";", provider);
			string run = Flat(Method(provider, "public string RunScenarioVerb("));
			Ordered(run, "if (!Eligible(game, zone, out string failure))", "game.SetStringGameState(Receipt, \"intent\")",
				"ProvesExactText(Receipt, \"intent\")", "KingdomRaidContactNativeChecks.Run(game, zone, out Ok)",
				"game.SetStringGameState(Receipt, report)", "ProvesExactText(Receipt, report)");
			string gate = Flat(Method(provider, "private static bool Eligible("));
			foreach (string token in new[] { "HasQuickstartState(game)", "HasAnyState(game, Receipt)",
				"KingdomRaidLaunchNativeFixture.LastAttempt != null", "!r_TAF_RaidMintProbe.Vacant",
				"script.Count != 3", "script[0] != \"stagedigest\"", "script[1] != Verb",
				"script[2] != \"stagedigest\"", "KingdomScenarioTransactionShape.None" })
				StringAssert.Contains(token, gate);
		}

		[Test]
		public void ActualContactRunsAfterPreloadAndBeforeRestoredSingleDebit()
		{
			string source = Read(Checks), run = Flat(Method(source, "internal void Run()"));
			Ordered(run, "Foreign = Manager.GetZone(foreignId)",
				"KingdomRaidLaunchNativeFixture.TryCreate(Target, out Fixture", "Fixture.Activate()",
				"PendingWire = Wire()", "KingdomRaids.StepRaider(Actor.Body, Actor.Objective, Tick); Pending()",
				"foreign-contact water=240 plunder=0 phase=EffectIntent wire=unchanged",
				"Move(Store, Store.OriginalCell)", "int debit = Math.Min(Operation.PlunderRequested, 240)",
				"KingdomRaids.StepRaider(Actor.Body, Actor.Objective, Tick)",
				"KingdomRaidIncidentRules.Incident(Book.RaidLedger, Fixture.Incident.Id)",
				"KingdomRaidResolution.StoresPlundered", "byte[] settled = Wire()",
				"KingdomRaids.StepRaider(Actor.Body, Actor.Objective, Tick)", "Same(settled, Wire())");
			ClassicAssert.AreEqual(3, Regex.Matches(run, @"KingdomRaids\.StepRaider\(").Count);
			StringAssert.DoesNotContain("OnWorldWake(", source);
			StringAssert.DoesNotContain("ResumeOpen(", source);
			StringAssert.Contains("SystemMoveTo(destination, energyCost: 0", Method(source, "private void Move("));
			string physical = Method(source, "private void Physical(");
			StringAssert.Contains("TargetRows.Exact(", physical);
			StringAssert.Contains("ForeignRows.Exact(", physical);
			StringAssert.Contains("Same(PendingWire, Wire())", Method(source, "private void Pending("));
		}

		[Test]
		public void PersonaBracketsExactContactCaseAndLabelsSyntheticEvidence()
		{
			string persona = Read("Tools/personas/raid-contact-native-check.persona");
			ClassicAssert.AreEqual("founding-first-city", Setting(persona, "REQUEST"));
			ClassicAssert.AreEqual("raid-contact-native-check", Setting(persona, "VERBS"));
			ClassicAssert.AreEqual("stagedigest;raid-contact-native-check;stagedigest", Setting(persona, "SCRIPT"));
			ClassicAssert.AreEqual("stagedigest:OK~founded=false,raid-contact-native-check:OK~cases=1 passed=1 failed=0,"
				+ "stagedigest:OK~founded=true,COMPLETE", Setting(persona, "EXPECT"));
			StringAssert.Contains("synthetic=true; ordinary-acceptance=false; save-load=untested", Read(Checks));
		}

		[Test]
		public void LocalWaterAllowanceKeepsActiveSurveyIdentityAndSettlementFloor()
		{
			string allowance = Flat(Method(Read("Growth/KingdomConstructionInputLeaseAuthority.cs"),
				"internal static bool TryWaterAllowance("));
			foreach (string token in new[] { "survey.Ground == null", "The.ZoneManager == null",
				"!ReferenceEquals(The.ZoneManager.ActiveZone, survey.Ground)",
				"KingdomSurvey.ActiveFor(survey.Ground) != survey" }) StringAssert.Contains(token, allowance);
			Ordered(allowance, "KingdomSurvey.ActiveFor(survey.Ground) != survey", "return false;",
				"snapshot.TryWaterHold(settlementId, out ignoredReserved, out floor)",
				"survey.Stores.Count", "survey.StoredWater - exactLeased",
				"TryAvailableWater(spendable, floor, preserveFloor, out available)");
			string commitGuard = Flat(Method(Read("Growth/KingdomWaterDebit.ReservationVerification.cs"),
				"private bool CurrentLeaseAuthorityAllowsDebit("));
			Ordered(commitGuard, "TryCapture(out leases, out failure)",
				"TryWaterAllowance( leases, Survey, true, out available, out failure)", "available < Amount");
		}

		[Test]
		public void ContactBindsRealSurveyOnlyAfterExactActiveZoneAdmission()
		{
			string contact = Flat(Method(Read("Raids/KingdomRaids.06.AttackResolutionAndOutbox.cs"),
				"private static void ProveObjectiveContact("));
			Ordered(contact, "!string.Equals(zone.ZoneID, op.ZoneId, StringComparison.Ordinal)",
				"op.Target != x || op.Count != y", "!ReferenceEquals(The.ZoneManager?.ActiveZone, zone)",
				"KingdomLifecycleBook book = system.LifecycleBook", "!ReferenceEquals(book?.Raid, op)",
				"KingdomSurvey survey = KingdomSurvey.Take(zone, system)", "using (survey.BindPass())",
				"!ReferenceEquals(The.Game, game)", "!ReferenceEquals(system.LifecycleBook, book)",
				"!ReferenceEquals(book.Raid, op)", "!ReferenceEquals(The.ZoneManager?.ActiveZone, zone)",
				"!KingdomMaster.AutomaticWorkAllowed(system)", "!KingdomLifecycleRules.CanOwnAuthority(book)",
				"ProveObjectiveContact(system, zone, op, targetId, x, y, survey)");
			StringAssert.DoesNotContain("new KingdomSurvey(", contact);
		}

		[Test]
		public void ExactStoreSelectorRetainsWholeSurveyAndCannotChooseAnotherVessel()
		{
			string source = Read("Growth/KingdomWaterDebit.cs");
			string selector = Flat(Method(source, "internal static KingdomWaterDebit ReserveExactStore("));
			Ordered(selector, "survey != null && survey.Ground != null && vessel != null",
				"ReferenceEquals(KingdomSurvey.ActiveFor(survey.Ground), survey)",
				"ReferenceEquals(XRL.The.ZoneManager?.ActiveZone, survey.Ground)",
				"OwnsVessel(vessel.ParentObject, vessel)",
				"ReferenceEquals(vessel.ParentObject.Physics?._CurrentCell?.ParentZone, survey.Ground)",
				"ReferenceEquals(survey.Stores[i], vessel)", "if (matches != 1)", "FailReservation(",
				"return Reserve(survey, amount, null, vessel)");
			StringAssert.DoesNotContain("new KingdomSurvey(", selector);
			string reserve = Flat(Method(source, "private static KingdomWaterDebit Reserve("));
			Ordered(reserve, "LiquidVolume ExactVessel = null", "new KingdomWaterDebit(Survey, Amount)",
				"TryWaterAllowance( leases, Survey, true, out available, out leaseFailure)",
				"available < Amount", "int count = Survey.Stores.Count",
				"dedicated[i] = (ExactVessel == null || ReferenceEquals(vessel, ExactVessel))",
				"!KingdomConstructionInputLeaseAuthority.IsLeased(leases, owner)",
				"KingdomWaterDebitRules.TryPlan(Amount, volumes, pure, dedicated,",
				"ExactVessel != null && (!ReferenceEquals(vessel, ExactVessel)",
				"!ReferenceEquals(owner.Physics?._CurrentCell?.ParentZone, Survey.Ground)",
				"debit.Entries.Add(new Entry", "OriginalZone = ExactVessel != null ? Survey.Ground");
			foreach (string token in new[] { "Stores.Add(", "Stores.Clear(", "StoredWater =", "StorageSpace =",
				"StorageCapacity =", "new KingdomSurvey(" }) StringAssert.DoesNotContain(token, selector + reserve);
		}

		[Test]
		public void NonReservedContactReturnsBeforeCommitCompensationOrEffectIntent()
		{
			string source = Flat(Read("Raids/KingdomRaids.06.AttackResolutionAndOutbox.cs"));
			string contact = Method(source, "private static void ProveObjectiveContact(KingdomSystem system, Zone zone, "
				+ "KingdomLifecycleOperation op, string targetId, int x, int y, KingdomSurvey survey)");
			Ordered(contact, "op.Phase != KingdomLifecyclePhase.EffectIntent", "op.Origin != targetId",
				"GameObject target = FindExact(zone, targetId)", "LiquidVolume liquid = target?.GetPart<LiquidVolume>()",
				"target.GetIntProperty(\"KingdomStores\") != 1", "if (amount > 0)",
				"debit = KingdomWaterDebit.ReserveExactStore(survey, liquid, amount)",
				"if (debit == null || debit.State != KingdomWaterDebitState.Reserved) return;",
				"if (!debit.Commit())", "RestoreDebitOrQuarantine(system, op, debit,",
				"debit.Spent != amount || debit.Outstanding != 0 || !debit.MeasurementExact",
				"RaidRuntimeAdapter.BeginEffect", "RaidRuntimeAdapter.CommitEffect");
			StringAssert.DoesNotContain("new KingdomSurvey(", contact);
			StringAssert.DoesNotContain(".ReserveExactWater(", contact);
		}

		[Test]
		public void NativeSelectorWitnessReservesNonfirstStoreWithoutFundingActualContact()
		{
			string selector = Flat(Method(Read("Harness/KingdomRaidContactWaterChecks.cs"),
				"internal static void Prepare("));
			Ordered(selector, "survey.Stores.Count == 2", "ReferenceEquals(survey.Stores[0], named)",
				"ReferenceEquals(survey.Stores[1], other)", "survey.StoredWater == 336", "using (survey.BindPass())",
				"ReserveExactStore(survey, null, 1)", "ReserveExactStore(survey, named, 241)",
				"ReserveExactStore(survey, other, 1)", "exact.State == KingdomWaterDebitState.Reserved",
				"legs.Length == 1", "ReferenceEquals(legs[0].Owner, Other)", "legs[0].BeforeVolume == 96",
				"legs[0].AfterVolume == 95", "Refused(copy.ReserveExactWater(1)",
				"survey.StoredWater == stored", "namedBody.Exact(namedBody.OriginalCell)",
				"otherBody.Exact(otherBody.OriginalCell)", "KingdomSurvey.ActiveFor(fixture.Zone) == null");
			StringAssert.DoesNotContain(".Commit(", selector);
			StringAssert.DoesNotContain(".Rollback(", selector);
			string run = Flat(Method(Read(Checks), "internal void Run()"));
			Ordered(run, "KingdomRaidContactWaterChecks.Prepare(Fixture, Clear, Evidence)",
				"PendingWire = Wire()", "Require(KingdomSurvey.ActiveFor(Target) == null,",
				"KingdomRaids.StepRaider(Actor.Body, Actor.Objective, Tick)");
			StringAssert.DoesNotContain("BindPass(", run);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Flat(string source) { return Regex.Replace(source, @"\s+", " "); }
		private static string Setting(string source, string key)
		{
			string[] rows = source.Split('\n').Select(row => row.Trim())
				.Where(row => row.StartsWith(key + "=", StringComparison.Ordinal)).ToArray();
			ClassicAssert.AreEqual(1, rows.Length); return rows[0].Substring(key.Length + 1);
		}
		private static string Method(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, signature);
			int open = source.IndexOf('{', start), depth = 0;
			ClassicAssert.GreaterOrEqual(open, 0, signature);
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
			}
			Assert.Fail("Unclosed method: " + signature); return null;
		}
		private static void Ordered(string source, params string[] tokens)
		{
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(token, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, "Missing or reordered: " + token); cursor = at + token.Length;
			}
		}
	}
}
#endif
