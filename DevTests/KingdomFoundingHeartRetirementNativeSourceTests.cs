#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	// Source contracts only; these do not execute engine faults, reflection or restoration.
	[TestFixture]
	public sealed class KingdomFoundingHeartRetirementNativeSourceTests
	{
		private const string Checks = "Harness/KingdomFoundingHeartRetirementChecks.cs";
		private const string Fault = "Harness/KingdomFoundingHeartRetirementFault.cs";
		private const string Snapshot = "Harness/KingdomFoundingHeartRetirementSnapshot.cs";

		[Test]
		public void SourceContract_OnlyFourExactObservationalSignaturesAreReflectedAndExceptionsAreNotFalseVerdicts()
		{
			string source = Read(Checks);
			string run = Between(source, "internal static void Run(", "private static MethodInfo Method(");
			Contains(source, "BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly",
				"typeof(KingdomPlots).GetNestedType(\"FoundingHeartContext\", BindingFlags.NonPublic)",
				"Check(contextType != null", "Method(\"ExactFoundingHeartRetiredAuthority\", typeof(bool), typeof(Zone), typeof(string), contextType.MakeByRefType())",
				"Method(\"ExactFoundingHeartRetirementProof\", typeof(bool), typeof(Zone), contextType, typeof(string))",
				"Method(\"FindGraveyardTombstone\", typeof(KingdomPhysicalLookupState), typeof(string), typeof(GameObject).MakeByRefType())",
				"Method(\"TryLoadedPlotTombstones\", typeof(bool), typeof(List<GameObject>).MakeByRefType())");
			Assert.AreEqual(4, Regex.Matches(run, @"\bMethod\s*\(").Count);
			Ordered(Between(source, "private static MethodInfo Method(", "private static void Check("),
				"typeof(KingdomPlots).GetMethod(name, Hidden, null, arguments, null)",
				"Check(method != null && method.ReturnType == result", "return method");
			Assert.IsFalse(Regex.IsMatch(source, @"\bcatch\s*(?:\(|\{)|\bfinally\s*\{"));
			Assert.IsFalse(Regex.IsMatch(source,
				@"\b(?:Recover\w*|Audit\w*|Advance|Ensure\w*|Destroy\w*|Obliterate\w*|Pool\w*|Release\w*|GetZone|LoadZone|SetValue)\s*\("));
		}

		[Test]
		public void SourceContract_CompletedAuthorityContextAndLoadedCollectorAreProvedBeforeSixNamedFaults()
		{
			string source = Read(Checks);
			Ordered(source, "collector.Invoke(null, new object[] { null })",
				"ReferenceEquals(world.Completed(predecessor), final)", "string id = predecessor.IDIfAssigned",
				"object[] baseline = { world.Zone, id, null }",
				"authority.Invoke(null, baseline) && baseline[2] != null", "object context = baseline[2]",
				"proof.Invoke(null, new object[] { world.Zone, context, id })",
				"string[] kinds = { \"duplicate\", \"null-collection\", \"overbound\", \"wrong-owner\", \"wrong-slot\", \"live-conflict\" }",
				"foreach (string kind in kinds)", "current = \"retirement-\" + kind + \"-refused\"",
				"new KingdomFoundingHeartRetirementFault(world, predecessor, final)",
				"fault.VerifyBaseline()", "fault.Install(kind)", "fault.VerifyInstalled()");
			Assert.AreEqual(1, Regex.Matches(source, @"\bpassed\s*\+\+").Count);
		}

		[Test]
		public void SourceContract_CollectionShapeIdentityAndOuterCustodyHaveDistinctNativeVerdicts()
		{
			string source = Read(Checks);
			Ordered(source, "fault.VerifyInstalled()", "bool readable = (bool)collector.Invoke(null, new object[] { null })",
				"Check(readable == (kind != \"null-collection\" && kind != \"overbound\")",
				"object[] found = { id, null }", "lookup.Invoke(null, found)",
				"bool ambiguous = kind == \"duplicate\" || kind == \"null-collection\" || kind == \"overbound\"",
				"state == (ambiguous ? KingdomPhysicalLookupState.Ambiguous : KingdomPhysicalLookupState.Exact)",
				"ambiguous ? found[1] == null : ReferenceEquals(found[1], predecessor)",
				"bool inner = (bool)proof.Invoke(null, new object[] { world.Zone, context, id })",
				"Check(inner == (kind == \"live-conflict\")",
				"Check(!(bool)authority.Invoke(null, new object[] { world.Zone, id, null })");
			Contains(Read("Harness/KingdomFoundingHeartLifecycleChecks.cs"),
				"null collection is not throwing-reader coverage");
		}

		[Test]
		public void SourceContract_PassFollowsInstalledReproofConditionalRestoreAndPositiveBaselineRecheck()
		{
			Ordered(Read(Checks), "Check(!(bool)authority.Invoke(null, new object[] { world.Zone, id, null })",
				"fault.VerifyInstalled()", "fault.VerifyAndRestore()", "fault.VerifyBaseline()",
				"Check((bool)authority.Invoke(null, new object[] { world.Zone, id, null })",
				"ReferenceEquals(world.Completed(predecessor), final)", "passed++",
				".Append(current).Append(\"=PASS\")");
			StringAssert.DoesNotContain("finally {", Read(Checks));
		}

		[Test]
		public void SourceContract_LifecycleFailureAfterFifteenthPassStillLatchesOverallFailure()
		{
			string run = Between(Read("Harness/KingdomFoundingHeartLifecycleChecks.cs"),
				"internal static string Run(", "private static void FoundingFault(");
			Ordered(run, "bool failed = false", "try",
				"KingdomFoundingHeartRetirementChecks.Run(world, predecessor, final, rows, ref passed, ref current)",
				"probes.Check()", "catch (Exception error)", "failed = true",
				".Append(current).Append(\"=FAIL \")", "finally { r_TAF_FoundingHeartMintProbe.Callback = null; }",
				"ok = !failed && passed == KingdomFoundingHeartLifecycleProvider.ExpectedCases",
				"(ok ? 0 : 1)");
			Assert.AreEqual(1, Regex.Matches(run, @"\bfailed\s*=\s*false\b").Count);
		}

		[Test]
		public void SourceContract_QueueFaultsReplaceOnlyOwnedCollectionAndActuallyPopulateTheOverboundCount()
		{
			string source = Read(Fault);
			Ordered(Between(source, "internal void Install(", "private void InstallQueue("),
				"Guard(() =>", "!Installed && !Restored && Kind == null", "Snapshot.VerifyBaseline()",
				"Kind = kind", "InstallQueue()", "InstallMaps()", "InstallLiveConflict()", "Installed = true", "VerifyInstalled()");
			Ordered(Between(source, "private void InstallQueue(", "private void InstallMaps("),
				"if (Kind != \"null-collection\")", "int count = Kind == \"overbound\" ? 65537 : Snapshot.OriginalQueue.Count + 1",
				"Queue = new RingDeque<GameObject>(count)", "QueueRows = new GameObject[count]", "for (int i = 0; i < count; i++)",
				"Kind == \"duplicate\" && i < Snapshot.OriginalQueue.Count ? Snapshot.OriginalQueue[i] : Snapshot.Predecessor.Body",
				"QueueRows[i] = body; Queue.Enqueue(body)", "Check(Queue.Count == count", "Snapshot.VerifyBaseline()",
				"Snapshot.Graveyard.Objects = Queue");
			Ordered(Between(source, "internal void VerifyInstalled(", "internal void VerifyAndRestore("),
				"Snapshot.VerifyInjection(IsQueue ? Queue : Snapshot.OriginalQueue", "if (IsQueue && Queue != null)",
				"Queue.Count == QueueRows.Length", "ReferenceEquals(Queue[i], QueueRows[i])");
			Assert.IsFalse(Regex.IsMatch(source, @"\.Graveyard\.Add\s*\(|\b(?:Clear|Destroy\w*|Obliterate\w*|Pool\w*|Release\w*)\s*\("));
		}

		[Test]
		public void SourceContract_MapFaultsRetainOriginalMapsAndLiveConflictUsesOnlyCapturedFreshOwnedBody()
		{
			string source = Read(Fault);
			Ordered(Between(source, "private void InstallMaps(", "private void InstallLiveConflict("),
				"new Dictionary<string, string>(Snapshot.Predecessor.Strings.Source, Snapshot.Predecessor.Strings.Source.Comparer)",
				"new Dictionary<string, int>(Snapshot.Predecessor.Ints.Source, Snapshot.Predecessor.Ints.Source.Comparer)",
				"strings[KingdomPlots.FoundingHeartOwnerProperty] = \"retirement-fixture-foreign-owner\"",
				"ints[KingdomPlots.FoundingHeartSlotProperty] = 0", "Snapshot.VerifyBaseline()",
				"Snapshot.Predecessor.Body.Property = strings", "Snapshot.Predecessor.Body.IntProperty = ints");
			Ordered(Between(source, "private void InstallLiveConflict(", "internal void VerifyInstalled("),
				"!KingdomNativeRegressionContext.HasAnyState(Snapshot.World.Game, ScratchKey)",
				"GameObject.Create(\"Chest\", BeforeObjectCreated: body =>", "Bodies.Add(body); captured = body; captures++",
				"if (returned != null) Bodies.Add(returned)", "captures == 1 && ReferenceEquals(returned, captured)",
				"GameObject.Validate(returned)", "returned.CurrentCell == null && returned.CurrentZone == null",
				"returned.InInventory == null && returned.Equipped == null && returned.Count == 1",
				"Snapshot.VerifyBaseline()", "LiveBody = returned", "LiveBody.IDIfAssigned = Snapshot.Predecessor.Id",
				"new KingdomFoundingHeartRetirementSnapshot.BodyState(LiveBody)", "Snapshot.VerifyBaseline()",
				"!KingdomNativeRegressionContext.HasAnyState(Snapshot.World.Game, ScratchKey)",
				"Snapshot.World.Game.ObjectGameState.Add(ScratchKey, LiveBody)");
			Contains(source, "Retained.Add(this)", "if (LiveState != null) LiveState.Verify()");
			Assert.IsFalse(Regex.IsMatch(source, @"\bPredecessor(?:\.Body)?\.(?:IDIfAssigned|Live|Flags|_BaseID)\s*=(?!=)"));
		}

		[Test]
		public void SourceContract_RestoreRequiresExactInstalledProofAndFailedProofPermanentlyRetainsEvidence()
		{
			string source = Read(Fault);
			Ordered(Between(source, "internal void VerifyAndRestore(", "internal void VerifyBaseline("),
				"Guard(() =>", "VerifyInstalled()", "if (IsQueue) Snapshot.Graveyard.Objects = Snapshot.OriginalQueue",
				"Snapshot.Predecessor.Body.Property = Snapshot.Predecessor.Strings.Source",
				"Snapshot.Predecessor.Body.IntProperty = Snapshot.Predecessor.Ints.Source",
				"Snapshot.World.Game.ObjectGameState.Remove(ScratchKey)", "Snapshot.VerifyBaseline()",
				"if (LiveState != null) LiveState.Verify()", "Restored = true");
			Contains(Between(source, "private void Guard(", "private static void Check("),
				"Check(!Failed", "try { action(); } catch { Failed = true; throw; }");
			Contains(Between(source, "internal void VerifyInstalled(", "internal void VerifyAndRestore("),
				"Installed && !Restored", "Strings.Verify(Snapshot.Predecessor.Body.Property)",
				"Ints.Verify(Snapshot.Predecessor.Body.IntProperty)");
			Assert.IsFalse(Regex.IsMatch(source, @"\bfinally\s*\{|\bFailed\s*=\s*false\b|\b(?:Clear|Destroy|Obliterate|Pool|Release)\s*\("));
			StringAssert.DoesNotContain(".Remove(", source.Replace("ObjectGameState.Remove(ScratchKey)", ""));
		}

		[Test]
		public void SourceContract_SnapshotRequiresGenuineUniqueTombstoneAndPreservesAllLoadedQueues()
		{
			string source = Read(Snapshot);
			string capture = Between(source, "internal KingdomFoundingHeartRetirementSnapshot(", "internal void VerifyBaseline(");
			Ordered(capture, "Retained.Add(this)", "world.Current()", "Cached = new Table<Zone>(Manager.CachedZones)",
				"HashSet<Zone> zones = new HashSet<Zone> { world.Zone }", "foreach (Zone zone in Cached.Source.Values)",
				"GlobalGraveyard = Manager.Graveyard", "ZoneGraveyards.Add(zone, zone.Graveyard)",
				"OriginalQueue = Graveyard.Objects", "!ReferenceEquals(Graveyard, GlobalGraveyard)",
				"ReferenceEquals(row.Key, world.Zone) || !ReferenceEquals(row.Value, Graveyard)",
				"Queues.Add(GlobalGraveyard, new QueueState(GlobalGraveyard))", "entries <= 65536",
				"!GameObject.Validate(predecessor) && GameObject.Validate(final)",
				"predecessor.IDIfAssigned == KingdomFoundingHeartRules.SlotId(plan, KingdomFoundingHeartRules.WorksSlot)",
				"int exact = 0", "foreach (QueueState queue in Queues.Values)", "foreach (GameObject body in queue.Rows)",
				"body.IDIfAssigned == Predecessor.Id", "ReferenceEquals(queue.Owner, Graveyard) && ReferenceEquals(body, predecessor)",
				"exact++", "Check(exact == 1");
			Contains(capture, "predecessor.HasStringProperty(KingdomPlots.FoundingHeartOwnerProperty) && !predecessor.HasIntProperty(KingdomPlots.FoundingHeartOwnerProperty)",
				"predecessor.HasIntProperty(KingdomPlots.FoundingHeartSlotProperty) && !predecessor.HasStringProperty(KingdomPlots.FoundingHeartSlotProperty)",
				"terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled",
				"terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled && terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled",
				"terminal.PredecessorId == Predecessor.Id", "terminal.FinalId == final.IDIfAssigned",
				"terminal.CompletionSeal == KingdomFoundingHeartRules.CompletionSeal(plan)",
				"final.HasStringProperty(KingdomPlots.FoundingHeartTerminalProperty) && !final.HasIntProperty(KingdomPlots.FoundingHeartTerminalProperty)",
				"final.GetStringProperty(KingdomPlots.FoundingHeartTerminalProperty) == terminalWire", "r_KingdomScaffold.HasRemovalProof(final, Predecessor.Id)");
			Contains(Between(source, "internal void VerifyInjection(", "internal sealed class Table<T>"),
				"ReferenceEquals(Manager.Graveyard, GlobalGraveyard)", "ReferenceEquals(row.Key.Graveyard, row.Value)",
				"row.Value.Verify(ReferenceEquals(row.Key, Graveyard) ? queue : row.Value.Queue)");
			Contains(Between(source, "private sealed class QueueState", "private static GameObject[] BoundedRoster("),
				"owner.Objects.Count <= 65536", "MaxCount = owner.MaxCount", "ReferenceEquals(Owner.Objects, expected)",
				"Owner.MaxCount == MaxCount && Queue.Count == Rows.Length", "ReferenceEquals(Queue[i], Rows[i])");
			Assert.IsFalse(Regex.IsMatch(source, @"\b(?:GetZone|LoadZone|FetchZone|Pool\w*|Release\w*|Clear|Destroy\w*)\s*\("));
		}

		[Test]
		public void SourceContract_SnapshotReprovesWorldAllFiveTablesBodiesAndOutsideRowsBeforeRestoration()
		{
			string source = Read(Snapshot);
			string verify = Between(source, "internal void VerifyInjection(", "internal sealed class Table<T>");
			Contains(verify, "ReferenceEquals(The.Game, Game)", "ReferenceEquals(The.ZoneManager, Manager)",
				"ReferenceEquals(The.Player, Player)", "ReferenceEquals(System.City, City)", "Game.TimeTicks == Tick",
				"!string.IsNullOrEmpty(GameId) && Game.GameID == GameId",
				"System.Founded && System.CurrentRealmId == Realm && System.CurrentSettlementId == Settlement",
				"System.SettlementIdentityTransactionId == Transaction", "System.Population == Population",
				"Strings.Verify(Game.StringGameState)", "Ints.Verify(Game.IntGameState)", "Longs.Verify(Game.Int64GameState)",
				"Objects.Verify(Game.ObjectGameState, scratchKey, scratchBody)", "Booleans.Verify(Game.BooleanGameState)",
				"Cached.Verify(Manager.CachedZones)", "ZoneProperties.Verify(Manager.ZoneProperties)", "ZoneValues.Verify(values)",
				"Predecessor.Verify(predecessorStrings, predecessorInts); Final.Verify(); PlayerState.Verify()",
				"SameReferences(Roster, BoundedRoster(World.Zone)", "ReferenceEquals(System.Ledger, Ledger)",
				"News[i][j] == NewsRows[i][j]", "counters[i] == Counters[i]", "r_TAF_FoundingHeartMintProbe.Count == ProbeCount");
			Contains(Between(source, "internal sealed class Table<T>", "internal sealed class BodyState"),
				"ReferenceEquals(current, Source) && current.Count == Rows.Count + (extraKey == null ? 0 : 1)",
				"current.TryGetValue(row.Key, out T value) && Equal(row.Value, value)",
				"!Rows.ContainsKey(extraKey) && current.TryGetValue(extraKey, out T extraValue) && Equal(extra, extraValue)",
				"Rows = new Dictionary<string, T>(source, source.Comparer)",
				"typeof(T) == typeof(object) ? ReferenceEquals(a, b)");
			Contains(Between(source, "internal sealed class BodyState", "private sealed class QueueState"),
				"Body.IDIfAssigned == Id", "GameObject.Validate(Body) == Valid", "Body.Live == Live && Body.Flags == Flags",
				"Body._BaseID == BaseId && Body.Count == Count", "ReferenceEquals(Body.Property, strings)",
				"ReferenceEquals(Body.IntProperty, ints)", "Strings.Verify(Strings.Source); Ints.Verify(Ints.Source)",
				"SameReferences(Custody, ReadCustody(Body)", "ReferenceEquals(Parts[i], Body.PartsList[i])");
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Compact(string value) { return Regex.Replace(value, @"\s+", ""); }
		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal); Assert.GreaterOrEqual(first, 0, start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			Assert.Greater(last, first, end); return source.Substring(first, last - first);
		}
		private static void Contains(string source, params string[] tokens)
		{
			foreach (string token in tokens) StringAssert.Contains(Compact(token), Compact(source), token);
		}
		private static void Ordered(string source, params string[] tokens)
		{
			string compact = Compact(source); int cursor = 0;
			foreach (string token in tokens)
			{
				string expected = Compact(token); int at = compact.IndexOf(expected, cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(at, cursor, token); cursor = at + expected.Length;
			}
		}
	}
}
#endif
