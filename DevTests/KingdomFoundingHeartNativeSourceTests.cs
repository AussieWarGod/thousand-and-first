#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Source contracts only; these do not execute native factories, callbacks, or save/load.
	[TestFixture]
	public sealed class KingdomFoundingHeartNativeSourceTests
	{
		private const string Provider = "Harness/KingdomFoundingHeartNativeProvider.cs";
		private const string Probe = "Harness/r_TAF_FoundingHeartMintProbe.cs";
		private const string Checks = "Harness/KingdomFoundingHeartNativeChecks.cs";
		private const string Allocation = "Harness/KingdomFoundingHeartAllocationNativeCases.cs";
		private const string Cases = "Harness/KingdomFoundingHeartReservationNativeCases.cs";
		private const string Frame = "Harness/KingdomFoundingHeartReservationNativeCases.Frame.cs";

		[Test]
		public void SourceContract_ProviderRequiresFreshStampedUnusedProfileBeforeIntent()
		{
			string source = Read(Provider);
			string run = Between(source, "public string RunScenarioVerb(", "private static bool Eligible(");
			Ordered(run, "Ok = false;", "!string.IsNullOrEmpty(Argument)", "!Eligible(game, zone, out string failure)",
				"game.StringGameState.Add(Receipt, \"intent\")", "ProvesExactText(Receipt, \"intent\")",
				"KingdomFoundingHeartNativeChecks.Run(game, zone, out Ok)", "!ReferenceEquals(The.Game, game)",
				"!KingdomScenarioDurableState.ProvesExactText(Receipt, \"intent\")", "Ok = false;",
				"game.SetStringGameState(Receipt, report)", "!KingdomScenarioDurableState.ProvesExactText(Receipt, report)",
				"Ok = false;");
			string eligible = source.Substring(source.IndexOf("private static bool Eligible(", StringComparison.Ordinal));
			Contains(eligible, "game == null || zone == null || The.ZoneManager == null",
				"!ReferenceEquals(The.ZoneManager.ActiveZone, zone)", "!MessageQueue.Enabled",
				"!KingdomSubsidence.Enabled", "!KingdomLodging.Enabled", "!KingdomMaster.ConfiguredEnabled",
				"game.GetSystem<KingdomSystem>()?.Founded ?? false", "HasQuickstartState(game)", "HasAnyState(game, Receipt)",
				"plan.Key != \"founding-first-city\"", "script.Count != 3", "script[0] != \"stagedigest\"",
				"script[1] != Verb", "script[2] != \"stagedigest\"", "zone.ZoneID != profile.ZoneId");
			Ordered(eligible, "HasAnyState(game, Receipt)", "TryBindStampedPlan(out plan, out stamp, out failure)",
				"KingdomScenarioTransactionMarker.Observe(out transaction) != KingdomScenarioTransactionShape.None",
				"KingdomQuickstartRules.TryProfile(\"marsh\", out profile)");
			Contains(source, "ExpectedCases = 7;", "Verb = \"founding-heart-check\";");
		}

		[Test]
		public void SourceContract_ProbeIsInertUnarmedAndRetainsOriginalReferencesWithLatchedErrors()
		{
			XmlDocument overlay = new XmlDocument();
			overlay.LoadXml(Read("Harness/ObjectBlueprints.xml"));
			ClassicAssert.AreEqual(2, overlay.DocumentElement.SelectNodes("object").Count);
			XmlNodeList hearts = overlay.DocumentElement.SelectNodes("object[part[@Name='r_TAF_FoundingHeartMintProbe']]");
			ClassicAssert.AreEqual(0, hearts.Count, "own blueprints are not defined when the Harness XML loads");
			Ordered(Read(Checks), "KingdomFoundingHeartProbeBlueprints.Install()", "KingdomSubsidenceNativeFixture.TryCreate(",
				"KingdomFoundingHeartAllocationNativeCases.ForeignReplacement(", "probes.Check()");
			string binding = Read("Harness/KingdomFoundingHeartProbeBlueprints.cs");
			Contains(binding, "new Binding[names.Length]", "MaximumOriginalParts = 512", "!Parts.ContainsKey(ProbeName)",
				"ReferenceEquals(The.Game, Game)", "ReferenceEquals(Factory.Blueprints, Blueprints)",
				"ReferenceEquals(current.Parts, row.Parts)", "ReferenceEquals(current, part.Value)",
				"Probe.T == typeof(r_TAF_FoundingHeartMintProbe)", "ProvesExactText(KingdomFoundingHeartNativeProvider.Receipt, \"intent\")");
			Ordered(binding, "Require(lifecycle", "? KingdomScenarioDurableState.ProvesExactText(KingdomFoundingHeartLifecycleProvider.Receipt, \"intent\")",
				": KingdomScenarioDurableState.ProvesExactText(KingdomFoundingHeartNativeProvider.Receipt, \"intent\")",
				"new KingdomFoundingHeartProbeBlueprints(lifecycle)", "Retained = binding;", "binding.CheckState(i)",
				"Parts.Add(ProbeName, binding.Bindings[i].Probe)", "binding.CheckState(i + 1)");
			Contains(binding, "private KingdomFoundingHeartProbeBlueprints(bool lifecycle)",
				"string[] names = lifecycle ? new[] { \"r_KingdomHeartStake\", \"r_KingdomFirstBasin\", \"r_KingdomPlotWorks\", \"r_KingdomRiteGround\" }"
				+ " : new[] { \"r_KingdomHeartStake\", \"r_KingdomFirstBasin\", \"r_KingdomPlotWorks\" }");
			foreach (string forbidden in new[] { ".Clear(", ".Remove(", "HarmonyPatch", "GameObject.Create(" })
				StringAssert.DoesNotContain(forbidden, binding);
			string source = Read(Probe);
			string observe = Between(source, "private void Observe(", "private static void RecordError(");
			Ordered(observe, "callback = Callback;", "if (callback == null) return;", "Observed.Count >= MaximumObserved",
				"RecordError(", "GameObject original = ParentObject;", "Observed.Add(original)",
				"!ReferenceEquals(E.Object, original)", "RecordError(", "if (Error == null) callback(original, E)");
			Contains(source, "Observed.ToArray()", "ID == BeforeObjectCreatedEvent.ID", "catch (Exception error)",
				"error.GetType().Name", "if (Error == null) Error =", "detail.Length <= 512");
			foreach (string forbidden in new[] { "Observed.Clear(", "Observed.Remove", "Observed.Add(E)", "Error = null;",
				".Destroy(", ".Obliterate(" }) StringAssert.DoesNotContain(forbidden, source);
			Contains(Read(Checks), "r_TAF_FoundingHeartMintProbe.Count == 0", "r_TAF_FoundingHeartMintProbe.Error == null",
				"finally { r_TAF_FoundingHeartMintProbe.Callback = null; }");
		}

		[Test]
		public void SourceContract_RealFoundingChecksAllReservationsInsideCreationBeforeHeartIdentity()
		{
			string source = Read(Checks);
			string callback = Between(source, "r_TAF_FoundingHeartMintProbe.Callback = (body, e) => {",
				"KingdomSubsidenceNativeFixture fixture;");
			Ordered(callback, "!(body.IDIfAssigned ?? \"\").StartsWith(\"taf-heart-v1-\", StringComparison.Ordinal)",
				"KingdomFoundingHeartRules.TryDecode(zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null)",
				"Reservations(atCallback)", "created.Add(body)");
			Ordered(source, "KingdomSubsidenceNativeFixture.TryCreate(zone, out fixture, out string failure)",
				"KingdomFoundingHeartRules.Complete(plan)", "created.Count == 6", "r_TAF_FoundingHeartMintProbe.Count == 6",
				"created[slot].IDIfAssigned == KingdomFoundingHeartRules.SlotId(plan, slot)", "Reservations(plan)",
				"KingdomPlots.AuditFoundingHeartReservations(fixture.System, zone)");
			Contains(source, "ReferenceEquals(created[slot].CurrentZone, zone)");
			string reservations = Between(source, "private static void Reservations(", "private static void Pass(");
			Contains(reservations, "slot <= KingdomFoundingHeartRules.SlotCount", "? \"final\" : \"slot-\" + slot",
				"KingdomScenarioDurableState.ProvesExactText(KingdomPlots.FoundingHeartReservationPrefix + id,",
				"KingdomFoundingHeartReservationRules.Encode(plan, id, role)");
			Ordered(Read("Harness/KingdomSubsidenceNativeFixture.cs"), "KingdomScenarioFoundingStep.TryProvePreconditions(",
				"KingdomScenarioTransactionMarker.TryBegin(", "KingdomScenarioFoundingStep.TryFound(",
				"KingdomScenarioTransactionMarker.TryCommit(");
		}

		[Test]
		public void SourceContract_DirectGuardProbesRetainForeignObjectsAndDiscloseSyntheticScope()
		{
			string source = Read(Allocation);
			Contains(source, "private guard on a completed fixture is synthetic", "does not complete a terminal work",
				"\"r_KingdomFirstBasin\", \"r_KingdomHeartStake\", \"r_KingdomPlotWorks\"",
				"GetMethod(\"TryReadFoundingHeartContext\"", "GetNestedType(\"FoundingHeartAllocationFence\"",
				"Activator.CreateInstance(type", "GetMethod(\"Create\"", "method.Invoke(fence, new object[] { blueprint })");
			Ordered(source, "GameObject.Create(blueprint, BeforeObjectCreated: Retained.Add)",
				"e.ReplacementObject = foreign", "made = Create(fence, blueprint)", "made == null",
				"!ReferenceEquals(observed, foreign)", "foreign.IDIfAssigned == id",
				"Exact(strings, foreign.Property); Exact(ints, foreign.IntProperty);", "Unassigned(observed); Unassigned(foreign);");
			Contains(source, "ReferenceEquals(made, observed)", "observed.IDIfAssigned == originalId",
				"r_TAF_FoundingHeartMintProbe.Count == count + 1", "r_TAF_FoundingHeartMintProbe.Error == null",
				"ReferenceEquals(game.IntGameState, ints)", "ReferenceEquals(game.StringGameState, strings)",
				"current == wire", "value == 701", "ints.Remove(key) && KingdomScenarioDurableState.ProvesExactText(key, wire)",
				"!body.HasStringProperty(KingdomPlots.FoundingHeartOwnerProperty)",
				"!body.HasIntProperty(KingdomPlots.FoundingHeartSlotProperty)");
			foreach (string forbidden in new[] { "Retained.Clear(", ".Destroy(", ".Obliterate(",
				"BeginFoundingHeartTerminal(", "DriveFoundingHeartTerminal(" }) StringAssert.DoesNotContain(forbidden, source);
			Contains(Read(Checks), "synthetic state faults and direct shared-guard calls", "No terminal completion",
				"ordinary-save compatibility", "all-callback-cut acceptance", "Evidence retained");
		}

		[Test]
		public void SourceContract_TypedMatricesReproveExactStateBeforeRestoringOnlyTheirOwnedKeys()
		{
			string cases = Read(Cases), frame = Read(Frame), checks = Read(Checks);
			Contains(cases, "slot < 7", "mask < 32", "accepted == (mask == 0 || mask == 1)",
				"new[] { null, \"\", \"hr1|native-synthetic-malformed\" }", "frame.AcceptCreated(slot)",
				"frame.Inject(0, 0, frame.Canonical[0])", "frame.Inject(6, 1, \"hr1|native-synthetic-malformed-final\")",
				"BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly",
				"new[] { typeof(KingdomFoundingHeartPlan) }", "method.IsPrivate", "method.ReturnType == typeof(bool)");
			ClassicAssert.AreEqual(3, Regex.Matches(cases, @"finally\s*\{\s*frame\.Restore\(\);\s*\}").Count);
			Ordered(checks, "r_TAF_FoundingHeartMintProbe.Callback = (body, e) => { };",
				"KingdomFoundingHeartReservationNativeCases.TypedStates(", "KingdomFoundingHeartReservationNativeCases.MalformedStrings(",
				"KingdomFoundingHeartReservationNativeCases.PreflightAllSeven(", "r_TAF_FoundingHeartMintProbe.Callback = null;");
			Contains(frame, "Callback != null && r_TAF_FoundingHeartMintProbe.Error == null",
				"ReferenceEquals(r_TAF_FoundingHeartMintProbe.Callback, Callback)", "r_TAF_FoundingHeartMintProbe.Count == Mints",
				"ReferenceEquals(The.Game, Game)", "Game.TimeTicks == Tick", "ReferenceEquals(System.City, City)",
				"ReferenceEquals(System.PolityLedger, Ledger)", "ReferenceEquals(System.Bindings, Bindings)",
				"KingdomFoundingHeartRules.Encode(Plan) == Wire", "ReferenceEquals(current[i], Bodies[i])",
				"Tables[table].Contains(Keys[slot]) == present", "Tables[table].Count == count");
			string[] tables = { "StringGameState", "IntGameState", "Int64GameState", "ObjectGameState", "BooleanGameState" };
			for (int i = 0; i < tables.Length; i++)
				StringAssert.Contains("ReferenceEquals(Game." + tables[i] + ", Tables[" + i + "])", frame);
			Ordered(Between(frame, "internal void Restore()", "private void SetRow("), "Check();",
				"for (int slot = 0; slot < 7; slot++) SetRow(slot, 1, Canonical[slot]);", "Check();");
			Ordered(Between(frame, "private void SetRow(", "private object Value("), "Check();", "table < 5", "Check();",
				"Tables[table].Remove(Keys[slot])", "Check();", "Tables[table].Add(Keys[slot], desired)", "Check();");
			Contains(frame, "table == 3 ? ReferenceEquals(left, right) : Equals(left, right)",
				"EnsureMethod.Invoke(null, new object[] { Plan })", "Same(table, Tables[table][row.Key], row.Value)");
			StringAssert.DoesNotContain(".Clear(", frame);
		}

		[Test]
		public void SourceContract_PersonaPinsSevenGroupsAndOnlyTwoExactInstalledDiagnostics()
		{
			string source = Read(Checks);
			string[] groups = Regex.Matches(source, @"\bcurrent\s*=\s*""([^""]+)""")
				.Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
			CollectionAssert.AreEqual(new[] { "real-founding-allocation-and-seven-reservations", "typed-state-matrix-224-shapes",
				"malformed-string-matrix-21-shapes", "all-seven-preflight-before-write", "shared-guard-genuine-factory-three-blueprints",
				"shared-guard-native-typed-callback-three-blueprints", "shared-guard-native-foreign-replacement-three-blueprints" }, groups);
			ClassicAssert.AreEqual(7, Regex.Matches(source, @"Pass\(rows,\s*current,\s*ref passed\);").Count);
			Contains(source, "ok = passed == KingdomFoundingHeartNativeProvider.ExpectedCases");
			Dictionary<string, string> persona = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (string line in Read("Tools/personas/founding-heart-native-checks.persona").Split('\n'))
			{
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;
				string[] field = line.TrimEnd('\r').Split(new[] { '=' }, 2);
				ClassicAssert.AreEqual(2, field.Length); ClassicAssert.IsFalse(persona.ContainsKey(field[0])); persona.Add(field[0], field[1]);
			}
			CollectionAssert.AreEquivalent(new[] { "DESCRIPTION", "REQUEST", "START", "SCRIPT", "VERBS", "EXPECT", "LOG_EXPECT", "SET" }, persona.Keys);
			ClassicAssert.AreEqual("founding-first-city", persona["REQUEST"]);
			ClassicAssert.AreEqual("8.22@40,12", persona["START"]);
			ClassicAssert.AreEqual("stagedigest;founding-heart-check;stagedigest", persona["SCRIPT"]);
			ClassicAssert.AreEqual("founding-heart-check", persona["VERBS"]);
			ClassicAssert.AreEqual("stagedigest:OK~founded=false,founding-heart-check:OK~cases=7 passed=7 failed=0,stagedigest:OK~founded=true,COMPLETE", persona["EXPECT"]);
			ClassicAssert.AreEqual("[\"MODWARN [Pets of Harvest Dawn] - Mod defining manual load order, please convert it to use the Dependencies field.\","
				+ "\"MODWARN [Pets of Harvest Dawn] - XmlDataHelper:: <...>/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/DLC/PetsPack1/Freehold_Pet_Ercolano/PopulationTables.xml line 4 char 6\"]", persona["LOG_EXPECT"]);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Flat(string source) { return Regex.Replace(source, @"\s+", " "); }
		private static void Contains(string source, params string[] terms)
		{
			source = Flat(source);
			foreach (string term in terms) StringAssert.Contains(Flat(term), source);
		}
		private static void Ordered(string source, params string[] terms)
		{
			source = Flat(source); int cursor = 0;
			foreach (string term in terms)
			{
				string needle = Flat(term); int found = source.IndexOf(needle, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, term); cursor = found + needle.Length;
			}
		}
		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal); ClassicAssert.GreaterOrEqual(first, 0, start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal); ClassicAssert.Greater(last, first, end);
			return source.Substring(first, last - first);
		}
	}
}
#endif
