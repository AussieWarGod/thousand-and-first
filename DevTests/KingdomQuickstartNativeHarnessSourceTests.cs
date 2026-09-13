#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source contracts for harness wiring; these do not execute native fixtures.</summary>
	[TestFixture]
	public sealed class KingdomQuickstartNativeHarnessSourceTests
	{
		private const string Creators = "Harness/KingdomQuickstartBootstrap.NativeCreators.cs";
		private const string Faults = "Harness/KingdomQuickstartBootstrap.NativeFaults.cs";
		private const string Provider = "Harness/KingdomQuickstartNativeProvider.cs";
		private const string Context = "Harness/KingdomNativeRegressionContext.cs";
		private const string CaskFault = "Harness/KingdomQuickstartCaskFault.cs";

		[Test]
		public void WaterFaultUsesRealCreatorBeforeCleanupRetryAndRecovery()
		{
			string body = Flat(Method(Read(Creators), "private static void NativeWaterCreator("));
			Ordered(body, "new NativeWaterGround(Context)", "new KingdomQuickstartCaskFault(Context, receipt)",
				"CreateWater(Context.Game, Context.Zone, receipt, out string refusal)",
				"refused == null", "fault.Check(1, 1)", "NativeAbsent(Context, fault.First)",
				"before.Check(null)", "!GrantQuarantined(Context.Game)",
				"CreateWater(Context.Game, Context.Zone, receipt, out string failure)",
				"fault.Check(2, 2)", "ReferenceEquals(water, fault.Second)",
				"MaxVolume == 64", "before.Check(water)",
				"ReferenceEquals(water, CreateWater(Context.Game, Context.Zone, receipt, out failure))",
				"fault.Check(2, 2)", "ExactGrantMarker(water, receipt, KingdomQuickstartPhase.WaterStocked)",
				"before.Check(water)");
			StringAssert.DoesNotContain("TryCreateFreshGrant(", body);
		}

		[Test]
		public void WaterProbeCapturesOriginalThenMutatesOnlyFirstEnteredCapacity()
		{
			string source = Read(CaskFault);
			StringAssert.Contains("BeforeObjectCreatedEvent.ID", source);
			StringAssert.Contains("EnteredCellEvent.ID", source);
			Ordered(Flat(Method(source, "internal void Mint(")), "Owner(); BlueprintExact(true)",
				"new Witness(body, part)", "Originals.Add(witness); Context.Track(body)");
			Ordered(Flat(Method(source, "internal void Enter(")), "Owner(); BlueprintExact(true)",
				"ReferenceEquals(entered.Object, body)", "ReferenceEquals(entered.Cell, cell)",
				"witness.Exact()", "Count(cell.Objects, body) == 1", "witness.Volume.MaxVolume == 64",
				"witness.Volume.Volume == KingdomQuickstartRules.StarterWaterDrams", "Owner(); BlueprintExact(true); witness.Exact()",
				"Entries++", "if (Entries == 1)", "Faults++; witness.Volume.MaxVolume = 32");
			Ordered(Flat(Method(source, "public void Dispose(")), "Owner(); BlueprintExact(true)",
				"catch (Exception error)", "ReferenceEquals(added, Probe)", "Parts.Remove(ProbeName)",
				"BlueprintExact(false)", "finally", "Active = null");
			StringAssert.DoesNotContain("GameObject.Create(", source);
		}

		[Test]
		public void SeventeenUniqueCaseIdsBindExactCreatorAndFaultCallbacks()
		{
			string registrations = Method(Read(Creators), "internal static void NativeQuickstartChecks(")
				+ Method(Read(Faults), "private static void NativeQuickstartFaults(");
			MatchCollection matches = Regex.Matches(registrations,
				@"Context\.Case\(""([^""]+)"",\s*\(\) => ([^;\r\n]+)\);");
			string[] expected =
			{
				"creator-water-recovery=NativeWaterCreator(Context)",
				"creator-larder-recovery=NativeLarderCreator(Context)",
				"creator-materials-recovery=NativeMaterialsCreator(Context)",
				"creator-advisor-recovery=NativeAdvisorCreator(Context)",
				"creator-founders-cohort=NativeFoundersCohort(Context)",
				"creator-foreign-obstruction=NativeForeignObstruction(Context)",
				"adapter-water-verification-refusal=NativeWaterFault(Context, \"verify\")",
				"adapter-throw-before-placement=NativeWaterFault(Context, \"before\")",
				"adapter-throw-after-placement=NativeWaterFault(Context, \"after\")",
				"adapter-child-refusal-before-transfer=NativeChildFault(Context, \"before\")",
				"adapter-child-throw-after-transfer=NativeChildFault(Context, \"after\")",
				"adapter-moved-child=NativeChildFault(Context, \"moved\")",
				"adapter-moved-root=NativeWaterFault(Context, \"moved\")",
				"adapter-foreign-return=NativeChildFault(Context, \"foreign\")",
				"adapter-foreign-contents-fence=NativeForeignContents(Context)",
				"adapter-interrupted-factory-fence=NativeInterruptedFactory(Context)",
				"adapter-quarantine-five-tables=NativeQuarantineTables(Context)"
			};
			CollectionAssert.AreEqual(expected, matches.Cast<Match>().Select(m =>
				m.Groups[1].Value + "=" + Flat(m.Groups[2].Value)).ToArray());
			ClassicAssert.AreEqual(17, new HashSet<string>(matches.Cast<Match>()
				.Select(m => m.Groups[1].Value), StringComparer.Ordinal).Count);
			StringAssert.Contains("NativeQuickstartFaults(Context);", registrations);
			StringAssert.Contains("internal const int ExpectedCases = 17;", Read(Provider));
		}

		[TestCase("Water", "water", "VerifyWaterGrant", "Volume == KingdomQuickstartRules.StarterWaterDrams")]
		[TestCase("Larder", "larder", "VerifyLarderGrant", "Inventory.Objects.Count == 12")]
		[TestCase("Materials", "stockpile", "VerifyMaterialsGrant", "Inventory.Objects.Count == 3")]
		public void ProductionCreatorsVerifyPhysicalGrantsAndRecoverSameReferences(
			string Role, string Variable, string Verify, string Quantity)
		{
			string body = Flat(Method(Read(Creators), "private static void Native" + Role + "Creator("));
			Ordered(body, "GameObject " + Variable + " = NativeTrackFresh(Context, Create" + Role
				+ "(Context.Game, Context.Zone, receipt, out string failure))",
				"Context.Check(" + Verify + "(Context.Zone, " + Variable + ", receipt, true, out failure)",
				"ReferenceEquals(" + Variable + ", Create" + Role
					+ "(Context.Game, Context.Zone, receipt, out failure))");
			StringAssert.Contains(Quantity, body);
			StringAssert.DoesNotContain("Publish(", body);
		}

		[Test]
		public void AdvisorRecoveryCallsProductionResolverAndChecksNoLoot()
		{
			string body = Flat(Method(Read(Creators), "private static void NativeAdvisorCreator("));
			Ordered(body, "NativeTrackFresh(Context, CreateAdvisor(Context.Game, Context.Zone, profile, receipt",
				"VerifyAdvisor(Context.Zone, advisor, receipt, out failure)",
				"advisor.GetIntProperty(\"NoLoot\") == 1 && advisor.Inventory.Objects.Count == 0",
				"TryResolveAdvisor(Context.Game, Context.Zone, profile, receipt",
				"ReferenceEquals(advisor, recovered)",
				"disposition == KingdomQuickstartAdvisorDisposition.Included");
			StringAssert.DoesNotContain("Publish(", body);
		}

		[Test]
		public void FaultCasesExerciseProductionAdapterAndExactPhysicalCleanup()
		{
			StringAssert.Contains("return RemoveFreshGrantObject(Object);",
				Method(Read(Creators), "internal static bool NativeQuickstartCleanup("));
			foreach (string name in new[] { "NativeWaterFault", "NativeChildFault",
				"NativeForeignContents", "NativeInterruptedFactory" })
				StringAssert.Contains("TryCreateFreshGrant(Context.Game, scope =>",
					Method(Read(Faults), "private static void " + name + "("), name);
			string water = Flat(Method(Read(Faults), "private static void NativeWaterFault("));
			Ordered(water, "if (Cut == \"before\") throw", "TryPlaceGrant(Context.Zone, water",
				"if (Cut == \"after\") throw", "VerifyWaterGrant(Context.Zone, item, receipt, true",
				"NativeAbsent(Context, water)", "CreateWater(Context.Game, Context.Zone, receipt");
			string child = Flat(Method(Read(Faults), "private static void NativeChildFault("));
			Ordered(child, "if (Cut == \"before\") return null;", "if (Cut == \"foreign\") return foreign;",
				"NativeInsert(Context, root, child)", "if (Cut == \"after\") throw",
				"child.RemoveFromContext()", "NativeAbsent(Context, root)", "NativeAbsent(Context, child)");
			string absent = Flat(Method(Read(Creators), "private static void NativeAbsent("));
			StringAssert.Contains("Object != null && !GameObject.Validate(Object)", absent);
			StringAssert.Contains("Object.CurrentCell == null && Object.InInventory == null && Object.Equipped == null",
				absent);
			StringAssert.Contains("!ContainsExact(Context.Zone.GetObjects(), Object)", absent);
		}

		[Test]
		public void FixtureDisposalUsesReverseExactReferencesAndPoisonsLaterCasesOnFailure()
		{
			string source = Read(Context);
			Ordered(Method(source, "internal GameObject Track("),
				"ReferenceEquals(Owned[i], Exact)", "Owned.Add(Exact)");
			string body = Flat(Method(source, "internal void Case("));
			Ordered(body, "!Cases.Add(Id)", "string failure = Poisoned ?",
				"if (failure == null) Body();", "finally",
				"for (int i = Owned.Count - 1; i >= 0; i--)", "RemoveFixture(Owned[i])",
				"if (!removed) { Poisoned = true;", "Owned.Clear();",
				"HasQuickstartState(Game)", "if (failure == null) Passed++;");
			StringAssert.Contains("Game.GetSystem<KingdomSystem>()?.Founded ?? false", body);
			string tracking = Flat(Method(Read(Creators), "private static GameObject NativeTrackFresh("));
			Ordered(tracking, "Context.Track(Grant);", "Context.Track(child);");
		}

		[Test]
		public void AllFiveDurableTablesRefuseBeforeAllocationAndPreserveExactValues()
		{
			string tables = Flat(Method(Read(Faults), "private static void NativeQuarantineTables("));
			string presence = Method(Read(Context), "internal static bool HasAnyState(");
			string[] types = { "String", "Int", "Int64", "Boolean", "Object" };
			string[] values = { "\"\"", "0", "0L", "false", "(object)null" };
			for (int i = 0; i < types.Length; i++)
			{
				StringAssert.Contains("NativeQuarantineTable(Context, Context.Game." + types[i]
					+ "GameState, " + values[i] + ", \"" + types[i].ToLowerInvariant() + "\");", tables);
				StringAssert.Contains("Game." + types[i] + "GameState?.ContainsKey(Key)", presence);
			}
			string body = Flat(Method(Read(Faults), "private static void NativeQuarantineTable<T>("));
			Ordered(body, "!GrantQuarantined(Context.Game)", "States.Add(key, Value);", "try",
				"!TryCreateFreshGrant(Context.Game, scope =>", "calls++;", "scope.Create(",
				"calls == 0", "States.TryGetValue(key, out T observed)", "finally");
			StringAssert.Contains("if (States.TryGetValue(key, out T observed) && EqualityComparer<T>.Default.Equals(observed, Value)) States.Remove(key);",
				body);
		}

		[Test]
		public void ForeignAndUnreturnedFixturesRetainFenceAndForbidRetryAllocations()
		{
			string foreign = Flat(Method(Read(Faults), "private static void NativeForeignContents("));
			Ordered(foreign, "NativePair(Context, scope, out child)",
				"foreign = Context.Track(GameObject.Create(\"Vinewafer\"))", "NativeInsert(Context, root, foreign)",
				"NativeAbsent(Context, child)", "GameObject.Validate(root) && GameObject.Validate(foreign)",
				"finally { NativeAssertFence(Context); }");
			string interrupted = Method(Read(Faults), "private static void NativeInterruptedFactory(");
			Ordered(interrupted, "return scope.Create(() =>", "unreturned = Context.Track(",
				"throw new InvalidOperationException(NativeFaultMessage)", "GameObject.Validate(unreturned)",
				"finally { NativeAssertFence(Context); }");
			string fence = Flat(Method(Read(Creators), "private static void NativeAssertFence("));
			Ordered(fence, "!string.IsNullOrEmpty(token) && GrantQuarantined(Context.Game)",
				"!TryCreateFreshGrant(Context.Game, scope =>", "allocations++;", "allocations == 0", "finally");
			StringAssert.Contains("if (string.Equals(Context.Game.GetStringGameState(key, null), token, StringComparison.Ordinal)) Context.Game.StringGameState.Remove(key);",
				fence);
		}

		[Test]
		public void StampedActiveUnfoundedProfileIsRequiredBeforeIntentAndFixtureMutation()
		{
			string provider = Read(Provider);
			StringAssert.Contains("internal const string Verb = \"quickstart-check\";", provider);
			string eligible = Flat(Method(provider, "private static bool Eligible("));
			StringAssert.Contains("if (Game == null || Zone == null || The.ZoneManager == null || !ReferenceEquals(The.ZoneManager.ActiveZone, Zone) || (Game.GetSystem<KingdomSystem>()?.Founded ?? false) || KingdomNativeRegressionContext.HasQuickstartState(Game) || KingdomNativeRegressionContext.HasAnyState(Game, Receipt)) return false;",
				eligible);
			Ordered(eligible, "HasAnyState(Game, Receipt)",
				"if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out Failure)) return false;",
				"if (!string.Equals(plan.Key, \"founding-first-city\", StringComparison.Ordinal)"
					+ " || !KingdomScenarioScript.TryRead(out script, out Failure) || script.Count != 3"
					+ " || script[0] != \"stagedigest\" || script[1] != Verb || script[2] != \"stagedigest\")"
					+ " { Failure = \"native fixture checks require their exact fresh-profile script and stamped plan\"; return false; }",
				"if (KingdomScenarioTransactionMarker.Observe(out transaction) != KingdomScenarioTransactionShape.None)"
					+ " { Failure = \"native fixture checks require an unspent scenario transaction\"; return false; }",
				"KingdomQuickstartRules.TryProfile(\"marsh\", out profile)",
				"string.Equals(Zone.ZoneID, profile.ZoneId, StringComparison.Ordinal)",
				"for (int x = 28; x <= 30; x++)", "for (int y = 10; y <= 16; y++)", "return true;");
			StringAssert.DoesNotContain("plan.Synthetic", eligible);
			string state = Method(Read(Context), "internal static bool HasQuickstartState(");
			foreach (string key in new[] { "ProfileState", "ReceiptState", "WorldReservationState", "QuarantineState" })
				StringAssert.Contains("KingdomQuickstartRules." + key, state);
			StringAssert.Contains("if (HasAnyState(Game, key)) return true;", state);
			string run = Flat(Method(provider, "public string RunScenarioVerb("));
			Ordered(run, "if (!Eligible(game, zone, out reason)) return",
				"game.SetStringGameState(Receipt, \"intent\");",
				"if (!KingdomScenarioDurableState.ProvesExactText(Receipt, \"intent\")) return",
				"KingdomQuickstartBootstrap.NativeQuickstartCleanup", "KingdomQuickstartBootstrap.NativeQuickstartChecks(context)",
				"Ok = context.Count == ExpectedCases && context.Passed == ExpectedCases && context.Failed == 0;",
				"game.SetStringGameState(Receipt, report)");
			StringAssert.Contains("if (!KingdomScenarioDurableState.ProvesExactText(Receipt, report)) { Ok = false; return",
				run);
		}

		[Test]
		public void PersonaBracketsNativeChecksWithUnfoundedObservationsAndLabelsEvidenceLimits()
		{
			string persona = Read("Tools/personas/quickstart-native-checks.persona");
			CollectionAssert.AreEqual(new[] { "stagedigest", "quickstart-check", "stagedigest" },
				Setting(persona, "SCRIPT").Split(';'));
			CollectionAssert.AreEqual(new[] { "stagedigest:OK~founded=false",
				"quickstart-check:OK~cases=16 passed=16 failed=0", "stagedigest:OK~founded=false", "COMPLETE" },
				Setting(persona, "EXPECT").Split(','));
			ClassicAssert.AreEqual("quickstart-check", Setting(persona, "VERBS"));
			ClassicAssert.AreEqual("founding-first-city", Setting(persona, "REQUEST"));
			StringAssert.DoesNotContain("realize", Setting(persona, "SCRIPT"));
			StringAssert.Contains("fresh synthetic ground", persona);
			StringAssert.Contains("does not sign Quickstart embark or save/load", persona);
			StringAssert.Contains("synthetic=true; ordinary-acceptance=false; save-load=untested",
				Method(Read(Context), "internal string Report("));
		}

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }
		private static string Flat(string Source) { return Regex.Replace(Source, @"\s+", " "); }

		private static string Method(string Source, string Signature)
		{
			int start = Source.IndexOf(Signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, Signature);
			int open = Source.IndexOf('{', start), depth = 0;
			ClassicAssert.GreaterOrEqual(open, 0, Signature);
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
				ClassicAssert.GreaterOrEqual(at, cursor, "Missing or reordered source contract: " + token);
				cursor = at + token.Length;
			}
		}

		private static string Setting(string Source, string Key)
		{
			string[] rows = Source.Split('\n').Select(line => line.Trim())
				.Where(line => line.StartsWith(Key + "=", StringComparison.Ordinal)).ToArray();
			ClassicAssert.AreEqual(1, rows.Length, Key);
			return rows[0].Substring(Key.Length + 1);
		}
	}
}
#endif
