#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Source contracts only; these do not execute native founding, callbacks or save/load.
	[TestFixture]
	public sealed class KingdomFoundingHeartLifecycleSourceTests
	{
		private const string Provider = "Harness/KingdomFoundingHeartLifecycleProvider.cs";
		private const string World = "Harness/KingdomFoundingHeartLifecycleWorld.cs";
		private const string Checks = "Harness/KingdomFoundingHeartLifecycleChecks.cs";
		private const string Fault = "Harness/KingdomFoundingHeartLifecycleFault.cs";

		[Test]
		public void SourceContract_ProviderRequiresExactFreshScriptBeforeIntentAndPreservesChangedReceipts()
		{
			string source = Read(Provider);
			Ordered(Between(source, "public string RunScenarioVerb(", "private static bool Eligible("),
				"Ok = false;", "!string.IsNullOrEmpty(Argument)", "!Eligible(game, zone, out string failure)",
				"game.StringGameState.Add(Receipt, \"intent\")", "ProvesExactText(Receipt, \"intent\")",
				"KingdomFoundingHeartLifecycleChecks.Run(game, zone, out Ok)", "!ReferenceEquals(The.Game, game)",
				"!KingdomScenarioDurableState.ProvesExactText(Receipt, \"intent\")", "Ok = false;", "return report +",
				"game.SetStringGameState(Receipt, report)", "!KingdomScenarioDurableState.ProvesExactText(Receipt, report)");
			string eligible = source.Substring(source.IndexOf("private static bool Eligible(", StringComparison.Ordinal));
			Contains(eligible, "game == null || zone == null || The.ZoneManager == null",
				"!ReferenceEquals(The.ZoneManager.ActiveZone, zone)", "!MessageQueue.Enabled",
				"!KingdomSubsidence.Enabled", "!KingdomLodging.Enabled", "!KingdomMaster.ConfiguredEnabled",
				"game.GetSystem<KingdomSystem>()?.Founded ?? false", "HasQuickstartState(game)", "HasAnyState(game, Receipt)");
			Ordered(eligible, "HasAnyState(game, Receipt)", "TryBindStampedPlan(out plan, out stamp, out failure)",
				"plan.Key != \"founding-first-city\"", "KingdomScenarioScript.TryRead(out script, out failure)",
				"script.Count != 3", "script[0] != \"stagedigest\"", "script[1] != Verb", "script[2] != \"stagedigest\"",
				"KingdomScenarioTransactionMarker.Observe(out transaction) != KingdomScenarioTransactionShape.None",
				"KingdomQuickstartRules.TryProfile(\"marsh\", out profile)", "zone.ZoneID != profile.ZoneId");
			Contains(source, "ExpectedCases = 15;", "Verb = \"founding-heart-lifecycle\";",
				"Receipt = \"r_TAF_ScenarioFoundingHeartLifecycle_v1\";");
		}

		[Test]
		public void SourceContract_WorldBeginsOnceAndReprovesPendingAuthorityWithoutRepair()
		{
			string source = Read(World);
			Ordered(Between(source, "internal KingdomFoundingHeartLifecycleWorld(", "internal void Current()"),
				"Retained == null", "Retained = this", "TryBindStampedPlan(", "plan.AuthorityClass",
				"KingdomScenarioFoundingStep.TryProvePreconditions(", "KingdomScenarioTransactionMarker.TryBegin(");
			ClassicAssert.AreEqual(1, Regex.Matches(source, @"KingdomScenarioTransactionMarker\.TryBegin\s*\(").Count);
			Contains(Between(source, "internal void Current()", "internal KingdomFoundingHeartPlan Plan()"),
				"ReferenceEquals(The.Game, Game)", "ReferenceEquals(The.Player?.CurrentZone, Zone)",
				"ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)", "Game.TimeTicks == Tick", "ReferenceEquals(Owner, System)");
			Ordered(Between(source, "internal KingdomFoundingHeartPlan Plan()", "internal void Pending("),
				"plan.Copy()", "identity.States = new int[KingdomFoundingHeartRules.SlotCount]",
				"KingdomFoundingHeartRules.Encode(identity)", "FrozenPlan == wire");
			Contains(Between(source, "internal void Pending(", "internal r_KingdomPlotWorks Founded()"),
				"KingdomScenarioTransactionShape.Attempted", "System != null && System.Founded",
				"SiteAuthority == authority", "ReferenceEquals(Realm, faction)", "GetIntProperty(\"TAFFoundingPending\") == 1",
				"PendingFactionAuthorityProperty) == authority", "RealmReservationProperty) == authority",
				"PendingFactionTransactionProperty) == plan.TransactionId", "plan.States[i] == (i < slot ? 2 : 0)",
				"Absent(KingdomPlots.FoundingHeartRootPrefix + id)", "KingdomPhysicalLookupState.Absent",
				"FoundingHeartSealProperty, null) == null");
			StringAssert.DoesNotContain("TryBegin(", Read(Checks));
		}

		[Test]
		public void SourceContract_FourRefusedFoundingsPrecedeOneCommitAndPublicCalendarAdvancement()
		{
			string source = Read(Checks), run = Between(source, "internal static string Run(", "private static void FoundingFault(");
			Ordered(run, "KingdomFoundingHeartProbeBlueprints.Install(true)", "new KingdomFoundingHeartLifecycleWorld(game, zone)",
				"FoundingFault(world, \"r_KingdomFirstBasin\", true, 0)", "Pass(rows, current, ref passed)",
				"FoundingFault(world, \"r_KingdomFirstBasin\", false, 0)", "Pass(rows, current, ref passed)",
				"FoundingFault(world, \"r_KingdomPlotWorks\", true, KingdomFoundingHeartRules.WorksSlot)", "Pass(rows, current, ref passed)",
				"FoundingFault(world, \"r_KingdomPlotWorks\", false, KingdomFoundingHeartRules.WorksSlot)", "Pass(rows, current, ref passed)",
				"KingdomScenarioFoundingStep.TryFound(zone, world.Name", "world.Founded()", "KingdomPlotRules.PlotStage.Staked",
				"KingdomScenarioTransactionMarker.TryCommit(", "world.ClearPlayerFromHeart(plan)", "checked(plan.StartedTick + plan.TotalTicks)",
				"TerminalFault(world, works, finishTick, true)", "Pass(rows, current, ref passed)",
				"TerminalFault(world, works, finishTick, false)", "Pass(rows, current, ref passed)",
				"KingdomPlots.Advance(works, world.System, finishTick)", "world.Completed(predecessor)",
				"targeted == 1 && ReferenceEquals(final, observed)", "NoReplay(world, predecessor, final)", "probes.Check()",
				"Pass(rows, current, ref passed)",
				"KingdomFoundingHeartRetirementChecks.Run(world, predecessor, final, rows, ref passed, ref current)", "probes.Check()");
			ClassicAssert.AreEqual(1, Regex.Matches(source, @"KingdomScenarioTransactionMarker\.TryCommit\s*\(").Count);
			ClassicAssert.IsFalse(Regex.IsMatch(source + Read(World), @"\.TimeTicks\s*=(?!=)"));
			foreach (string forbidden in new[] { "GetMethod(", ".Invoke(", ".SetValue(", "BindingFlags" })
				StringAssert.DoesNotContain(forbidden, source + Read(World));
			Contains(run, "suppliedTick=", "unchangedWorldTick=", "Synthetic faults and future calendar argument",
				"no world-clock edit, ordinary progression, save/load", "root-after-write refusal claim",
				"Six observational native retirement negatives", "null collection is not throwing-reader coverage");
		}

		[Test]
		public void SourceContract_RefusalMustBeMeasuredBeforeOnlyOwnedInjectionRetirement()
		{
			string source = Read(Checks);
			Ordered(Between(source, "private static void FoundingFault(", "private static void TerminalFault("),
				"world.Current()", "fault.Arm()", "KingdomScenarioFoundingStep.TryFound(world.Zone, world.Name",
				"finally { r_TAF_FoundingHeartMintProbe.Callback = null; }", "!founded && !string.IsNullOrEmpty(failure)",
				"fault.VerifyRefusal()", "world.Pending(slot)", "if (typed) fault.RetireInjection()");
			Ordered(Between(source, "private static void TerminalFault(", "private static void NoReplay("),
				"\"r_KingdomRiteGround\", typed", "fault.Arm()", "KingdomPlots.Advance(works, world.System, tick)",
				"finally { r_TAF_FoundingHeartMintProbe.Callback = null; }", "fault.VerifyRefusal()",
				"world.NoTerminal(works)", "if (typed) fault.RetireInjection()");
		}

		[Test]
		public void SourceContract_FaultRetainsOriginalsBeforeCallbacksAndProvesAllSevenReservations()
		{
			string source = Read(Fault);
			Ordered(Between(source, "internal KingdomFoundingHeartLifecycleFault(", "internal void Arm()"),
				"Retained.Add(this)", "GameObject.Create(blueprint, BeforeObjectCreated: body => {", "Bodies.Add(body)",
				"ReferenceEquals(Foreign, captured)", "ForeignBefore = new BodyState(Foreign, Blueprint)");
			Contains(source, "blueprint == \"r_KingdomRiteGround\"", "new string[7]", "SlotCount + 1 == Keys.Length");
			Ordered(Between(source, "private void OnMint(", "internal void VerifyRefusal()"), "Bodies.Add(body); Targets++",
				"Targets == 1 && Armed", "ReferenceEquals(creation.Object, body)", "creation.ReplacementObject == null",
				"OriginalBefore = new BodyState(body, Blueprint)", "KingdomFoundingHeartRules.Encode(plan) == PlanWire",
				"slot < Keys.Length", "Wires[slot] = KingdomFoundingHeartReservationRules.Encode(plan, id, role)",
				"Reservations(false); OriginalBefore.Exact()", "Ints.Add(Keys[0], 701)", "Reservations(true)",
				"ForeignBefore.Exact()", "!ReferenceEquals(body, Foreign)", "creation.ReplacementObject = Foreign");
			string tables = Between(source, "private void Tables()", "private static void Disarmed()");
			foreach (string pair in new[] { "StringGameState, Strings", "IntGameState, Ints", "Int64GameState, Longs",
				"ObjectGameState, Objects", "BooleanGameState, Booleans" }) Contains(tables, "ReferenceEquals(Game." + pair + ")");
			Contains(tables, "ReferenceEquals(The.Game, Game)", "ReferenceEquals(The.ZoneManager.ActiveZone, Zone)");
		}

		[Test]
		public void SourceContract_FaultRetiresOnlyExact701AndLatchesAnyFailedProof()
		{
			string source = Read(Fault);
			Ordered(Between(source, "internal void RetireInjection()", "private void Reservations("),
				"Check(Typed,", "VerifyRefusal()", "Ints.Remove(Keys[0])", "Reservations(false)", "OriginalBefore.Exact()", "Retired = true");
			Contains(Between(source, "internal void VerifyRefusal()", "internal void RetireInjection()"),
				"Armed && Targets == 1 && Injected && !Retired", "Reservations(Typed); OriginalBefore.Exact()", "if (!Typed) ForeignBefore.Exact()");
			string reservations = Between(source, "private void Reservations(", "private void Tables()");
			Ordered(reservations, "Tables()", "FoundingHeartReceiptProperty, null) == PlanWire", "slot < Keys.Length",
				"Strings.TryGetValue(Keys[slot], out string wire) && wire == Wires[slot]", "value == 701",
				"!Longs.ContainsKey(Keys[slot])", "!Objects.ContainsKey(Keys[slot])", "!Booleans.ContainsKey(Keys[slot])",
				"KingdomScenarioDurableState.ProvesExactText(Keys[slot], Wires[slot])", "Tables()");
			Contains(source, "Check(!Failed,", "catch { Failed = true; throw; }");
			foreach (string forbidden in new[] { ".Clear(", ".Destroy(", ".Obliterate(", "Failed = false" })
				StringAssert.DoesNotContain(forbidden, source);
			StringAssert.DoesNotContain(".Remove(", source.Replace("Ints.Remove(Keys[0])", ""));
		}

		[Test]
		public void SourceContract_RetainedBodiesKeepExactIdentityPropertiesAndNoPhysicalCustody()
		{
			string body = Read(Fault).Substring(Read(Fault).IndexOf("private sealed class BodyState", StringComparison.Ordinal));
			Contains(body, "Id = body.IDIfAssigned", "new Dictionary<string, string>(Strings)", "new Dictionary<string, int>(Ints)",
				"Body.IDIfAssigned == Id", "ReferenceEquals(Body.Physics, Physics)", "ReferenceEquals(Body.Property, Strings)",
				"ReferenceEquals(Body.IntProperty, Ints)", "Rows(StringRows, Strings); Rows(IntRows, Ints)",
				"before.Count == after.Count", "EqualityComparer<T>.Default.Equals(row.Value, value)",
				"GameObject.Validate(body) && body.Blueprint == blueprint && body.CurrentCell == null",
				"body.CurrentZone == null && body.InInventory == null && body.Equipped == null",
				"!body.HasStringProperty(KingdomPlots.FoundingHeartOwnerProperty)", "!body.HasIntProperty(KingdomPlots.FoundingHeartOwnerProperty)",
				"!body.HasStringProperty(KingdomPlots.FoundingHeartSlotProperty)", "!body.HasIntProperty(KingdomPlots.FoundingHeartSlotProperty)");
		}

		[Test]
		public void SourceContract_TerminalProofSeparatesLiveAbsenceFromExactRetainedTombstone()
		{
			string source = Read(World);
			Contains(Between(source, "internal void NoTerminal(", "internal GameObject Completed("),
				"GameObject.Validate(works.ParentObject)", "works.StageApplied == (int)KingdomPlotRules.PlotStage.Walls",
				"FoundingHeartTerminalProperty, null) == null", "!works.ParentObject.HasStringProperty(\"r_TAF_PlotFinalOutputId\")",
				"!works.ParentObject.HasIntProperty(\"r_TAF_PlotFinalOutputId\")", "Absent(KingdomPlots.FoundingHeartFinalRootPrefix + id)");
			Contains(Between(source, "internal GameObject Completed(", "private static KingdomPhysicalLookupState Lookup("),
				"terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled", "terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled",
				"terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled", "terminal.TransactionId == plan.TransactionId",
				"terminal.CompletionSeal == KingdomFoundingHeartRules.CompletionSeal(plan)", "final.CurrentCell == Zone.GetCell(terminal.X, terminal.Y)",
				"r_KingdomScaffold.HasRemovalProof(final, terminal.PredecessorId)", "!GameObject.Validate(predecessor)",
				"Lookup(terminal.PredecessorId, out _, out _) == KingdomPhysicalLookupState.Absent",
				"ExactTombstone(terminal.PredecessorId, predecessor)", "Absent(KingdomPlots.FoundingHeartFinalRootPrefix + terminal.FinalId)");
			Ordered(Between(source, "private bool ExactTombstone(", "private void Absent("),
				"Zone.Graveyard?.Objects", "rows.Count > 65536", "predecessor.IDIfAssigned != id",
				"body.IDIfAssigned == id", "!ReferenceEquals(body, predecessor) || GameObject.Validate(body)", "matches++", "return matches == 1");
			Contains(source, "KingdomPlots.FindGlobalFoundingHeartId(id, out body, out graveyard)");
		}

		[Test]
		public void SourceContract_NoReplayMeasuresSameTerminalReferenceNotesBodiesAndPropertyRows()
		{
			string replay = Between(Read(Checks), "private static void NoReplay(", "private static void Pass(");
			Ordered(replay, "FoundingHeartTerminalProperty, null)", "world.System.Ledger.Notes.ToArray()",
				"world.Zone.GetObjects()", "new Dictionary<string, string>(final.Property)", "new Dictionary<string, int>(final.IntProperty)",
				"int count = r_TAF_FoundingHeartMintProbe.Count", "Callback = (body, e) => { }",
				"KingdomPlots.RecoverFoundingHeart(world.System, world.Zone)", "KingdomPlots.AuditFoundingHeartReservations(world.System, world.Zone)",
				"finally { r_TAF_FoundingHeartMintProbe.Callback = null; }", "ReferenceEquals(world.Completed(predecessor), final)",
				"FoundingHeartTerminalProperty, null) == wire", "r_TAF_FoundingHeartMintProbe.Count == count");
			Contains(replay, "notes.Length == world.System.Ledger.Notes.Count", "notes[i] == world.System.Ledger.Notes[i]",
				"after.Length == bodies.Length", "ReferenceEquals(after[i], bodies[i])", "strings.Count == final.Property.Count",
				"ints.Count == final.IntProperty.Count", "final.Property.TryGetValue(row.Key, out string value) && row.Value == value",
				"final.IntProperty.TryGetValue(row.Key, out int value) && row.Value == value");
		}

		[Test]
		public void SourceContract_CleanFoundingRechecksLiveRealmAndExactZeroRatherThanDefault()
		{
			string source = Between(Read(World), "internal r_KingdomPlotWorks Founded()", "internal void ClearPlayerFromHeart(");
			Ordered(source, "Factions.GetIfExists(System.KingdomFactionName)", "ReferenceEquals(currentRealm, Realm)",
				"currentRealm.IntProperties.TryGetValue(\"TAFFoundingPending\", out int pending) && pending == 0",
				"!currentRealm.Properties.ContainsKey(\"TAFFoundingPending\")", "SiteReservationProperty, null) == null");
			StringAssert.DoesNotContain("Realm.GetIntProperty(\"TAFFoundingPending\") == 0", source);
			Contains(Read(Checks), "position is not restored", "bounded native Move");
		}

		[Test]
		public void SourceContract_CompletionNameMatchesPlainFrozenWorkNameNotItsPlotObjectLabel()
		{
			string truth = Read("Growth/KingdomPlot2.07f.FoundingHeartStakeTruth.cs");
			Contains(truth, "part.DisplayName != truth.DisplayName", "bool RequireStaked = true, bool RawDisplayName = false",
				"(RawDisplayName ? Works.Render?.DisplayName : Works.DisplayName) != \"plot: \" + truth.DisplayName",
				"r_KingdomScaffold.CompletionNameProperty, Truth.DisplayName)");
			Contains(Read("Growth/KingdomPlot2.30.Finish.cs"), "string displayName = Works.DisplayName ?? entry.Name");
			Contains(Read("Growth/KingdomPlot2.27.FinalBuilding.cs"), "SetStringProperty(r_KingdomScaffold.CompletionNameProperty, DisplayName)");
			string drive = Read("Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs");
			Contains(drive, "DisplayName ?? Context.Stake.DisplayName, CompleteTick");
			StringAssert.DoesNotContain("DisplayName ?? \"plot: \"", drive);
			StringAssert.DoesNotContain("CompletionNameProperty, \"plot: \"", truth);
		}

		[Test]
		public void SourceContract_FounderWalksClearThroughBoundedNativeMovementBeforeConstruction()
		{
			string source = Between(Read(World), "internal void ClearPlayerFromHeart(", "internal void NoTerminal(");
			Ordered(source, "Current()", "GameObject player = The.Player", "plan.RectX1 > 1",
				"while (player.CurrentCell.X >= plan.RectX1)", "++moves <= 32",
				"player.Move(\"W\", AllowDashing: false, DoConfirmations: false)", "Current()",
				"ReferenceEquals(The.Player, player)", "player.CurrentCell.X == x - 1", "player.CurrentCell.Y == y");
			foreach (string forbidden in new[] { "DirectMoveTo", ".AddObject(", ".RemoveObject(", "Forced: true", ".Destroy(" })
				StringAssert.DoesNotContain(forbidden, source);
		}

		[Test]
		public void SourceContract_PersonaRequiresFifteenGroupsAndKeepsExactInstalledDiagnostics()
		{
			Dictionary<string, string> persona = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (string line in Read("Tools/personas/founding-heart-lifecycle.persona").Split('\n'))
			{
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;
				string[] field = line.TrimEnd('\r').Split(new[] { '=' }, 2);
				ClassicAssert.AreEqual(2, field.Length); ClassicAssert.IsFalse(persona.ContainsKey(field[0])); persona.Add(field[0], field[1]);
			}
			CollectionAssert.AreEquivalent(new[] { "DESCRIPTION", "REQUEST", "START", "SCRIPT", "VERBS", "EXPECT", "LOG_EXPECT", "SET" }, persona.Keys);
			ClassicAssert.AreEqual("founding-first-city", persona["REQUEST"]); ClassicAssert.AreEqual("8.22@40,12", persona["START"]);
			ClassicAssert.AreEqual("stagedigest;founding-heart-lifecycle;stagedigest", persona["SCRIPT"]);
			ClassicAssert.AreEqual("founding-heart-lifecycle", persona["VERBS"]);
			ClassicAssert.AreEqual("stagedigest:OK~founded=false,founding-heart-lifecycle:OK~cases=15 passed=15 failed=0,stagedigest:OK~founded=true,COMPLETE", persona["EXPECT"]);
			ClassicAssert.AreEqual("[\"MODWARN [Pets of Harvest Dawn] - Mod defining manual load order, please convert it to use the Dependencies field.\","
				+ "\"MODWARN [Pets of Harvest Dawn] - XmlDataHelper:: <...>/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/DLC/PetsPack1/Freehold_Pet_Ercolano/PopulationTables.xml line 4 char 6\"]", persona["LOG_EXPECT"]);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		[Test]
		public void SourceContract_DetachedFinalUsesStampedRectAndExplicitOwnerZoneBounds()
		{
			string source = Between(Read("Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs"),
				"private static bool ExactPreparedFoundingHeartFinal(", "private static bool ExactSettledFoundingHeartFinal(");
			Ordered(source, "TryReadStampedRect(Final, out KingdomPlotRules.PlotRect rect)", "SameRect(rect, Context.Rect)",
				"KingdomPlotRules.ValidZoneRect(rect, Z.Width, Z.Height)", "Final.CurrentCell == null",
				"Final.CurrentZone == null && Final.InInventory == null");
			StringAssert.DoesNotContain("TryReadRect(Final,", source);
			Contains(Read("Growth/KingdomPlot2.06.Geometry.cs"), "Zone zone = Object == null ? null : Object.CurrentZone",
				"if (zone == null)", "public static bool TryReadStampedRect(");
		}

		private static string Flat(string source) { return Regex.Replace(source, @"\s+", " "); }
		private static void Contains(string source, params string[] terms)
		{
			source = Flat(source); foreach (string term in terms) StringAssert.Contains(Flat(term), source);
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
