#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Source contracts only: no engine execution, ordinary progression or independent save commitment proof.
	[TestFixture]
	public sealed class KingdomScenarioCompletedHeartSourceTests
	{
		private const string Helper = "Harness/KingdomScenarioCompletedHeart.cs";
		private const string Fixture = "Harness/KingdomSubsidenceNativeFixture.cs";

		[Test]
		public void SourceContract_OptInCompletionFollowsFoundingCommitBeforeUnchangedEmptyResidentSetup()
		{
			string source = Read(Fixture);
			Contains(source, "out string Failure, bool CompleteFoundingHeart = false");
			Ordered(source, "LastAttempt = Fixture", "KingdomScenarioTransactionMarker.TryBegin(",
				"KingdomScenarioFoundingStep.TryFound(", "KingdomScenarioTransactionMarker.TryCommit(",
				"Fixture.System = game.GetSystem<KingdomSystem>()",
				"if (CompleteFoundingHeart) KingdomScenarioCompletedHeart.Complete(game, Fixture.System, Zone)",
				"Fixture.Build()", "return true", "catch (Exception error)", "return false");
			string build = Between(source, "private void Build()", "private GameObject Create(");
			Ordered(build, "RequireWorld()", "System.City.ResidentCount == 0", "KingdomResidents.OnRollCount(System) == 0",
				"System.Population == 0", "System.Bindings.Count == 0", "KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture)",
				"Store = Create(StoreBlueprint)", "GameObject body = Create(\"NPC\")", "KingdomCitizenship.TryEnroll(");
			Contains(Read("Harness/KingdomScenarioSaveChecks.cs"),
				"KingdomSubsidenceNativeFixture.TryCreate(Zone, out fixture, out string failure, CompleteFoundingHeart: true)");
		}

		[Test]
		public void SourceContract_RetainedFreshOwnerProofDoesNotReuseThePendingLifecycleRealm()
		{
			string source = Read(Helper);
			Ordered(source, "Retained.Add(this)", "Game = game; System = system; Zone = zone; Player = The.Player",
				"Tick = game.TimeTicks", "RealmId = system.CurrentRealmId", "SettlementId = system.CurrentSettlementId");
			string current = Between(source, "private void Current()", "private void Prepare()");
			Contains(current, "ReferenceEquals(The.Game, Game)", "ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)",
				"ReferenceEquals(The.Player, Player)", "GameObject.Validate(Player)", "ReferenceEquals(Player.CurrentZone, Zone)",
				"ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)", "Game.TimeTicks == Tick", "System.OwnedZone(ZoneId)",
				"System.CurrentRealmId == RealmId", "System.CurrentSettlementId == SettlementId",
				"Game.StringGameState != null", "Game.IntGameState != null", "Game.Int64GameState != null",
				"Game.BooleanGameState != null", "Game.ObjectGameState != null",
				"Factions.GetIfExists(FactionName)", "realm.IntProperties.TryGetValue(\"TAFFoundingPending\", out int pending) && pending == 0",
				"!realm.Properties.ContainsKey(\"TAFFoundingPending\")", "KingdomScenarioTransactionShape.Committed");
			StringAssert.DoesNotContain("KingdomFoundingHeartLifecycleWorld", source);
			StringAssert.DoesNotContain("MintProbe", source);
		}

		[Test]
		public void SourceContract_CompleteCanonicalPlanAndSevenExactReservationsRequireAllTypedRootsAbsent()
		{
			string source = Read(Helper);
			string authority = Between(source, "private void Authority()", "private void WalkWest()");
			Contains(authority, "KingdomFoundingHeartRules.Complete(Plan)", "Plan.ZoneId == ZoneId",
				"KingdomFoundingHeartRules.Encode(Plan) == PlanWire", "FoundingHeartReceiptProperty, null) == PlanWire",
				"FoundingHeartSealProperty, null) == KingdomFoundingHeartRules.CompletionSeal(Plan)",
				"slot <= KingdomFoundingHeartRules.SlotCount", "? \"final\" : \"slot-\"",
				"KingdomScenarioDurableState.ProvesExactText(", "KingdomFoundingHeartReservationRules.Encode(Plan, id, role)",
				"Absent((slot == KingdomFoundingHeartRules.SlotCount ? KingdomPlots.FoundingHeartFinalRootPrefix : KingdomPlots.FoundingHeartRootPrefix) + id)");
			Contains(source, "!KingdomNativeRegressionContext.HasAnyState(Game, key)",
				"!Predecessor.HasIntProperty(KingdomPlots.PlotWorkSchemaProperty)",
				"!Predecessor.HasStringProperty(KingdomPlots.PlotWorkSchemaProperty)",
				"Works.StartTick == Plan.StartedTick", "Works.TotalTicks == Plan.TotalTicks");
			Contains(Read("Harness/KingdomNativeRegressionContext.cs"), "Game.StringGameState?.ContainsKey(Key)",
				"Game.IntGameState?.ContainsKey(Key)", "Game.Int64GameState?.ContainsKey(Key)",
				"Game.BooleanGameState?.ContainsKey(Key)", "Game.ObjectGameState?.ContainsKey(Key)");
		}

		[Test]
		public void SourceContract_BoundedNativeMovementPrecedesCheckedFutureAdvanceWithoutClockOrPositionRestore()
		{
			string source = Read(Helper);
			Ordered(Between(source, "internal static void Complete(", "private void Current()"),
				"witness.Prepare()", "witness.WalkWest()", "checked(witness.Plan.StartedTick + witness.Plan.TotalTicks)",
				"completionTick > witness.Tick", "witness.Current(); witness.Authority()", "witness.Works.StageApplied",
				"KingdomPlots.Advance(witness.Works, system, completionTick)", "witness.Final = witness.Completed()",
				"witness.RecoverWithoutReplay()");
			Ordered(Between(source, "private void WalkWest()", "private GameObject Completed()"),
				"Current()", "Plan.RectX1 > 1", "while (Player.CurrentCell.X >= Plan.RectX1)", "++moves <= 32",
				"Player.Move(\"W\", AllowDashing: false, DoConfirmations: false)", "Current(); Authority()",
				"Player.CurrentCell.X == x - 1", "Player.CurrentCell.Y == y", "Player.CurrentCell.X < Plan.RectX1");
			ClassicAssert.IsFalse(Regex.IsMatch(source, @"\.(?:TimeTicks|CurrentCell|CurrentZone|X|Y)\s*=(?!=)"));
			foreach (string forbidden in new[] { "DirectMoveTo", ".AddObject(", ".RemoveObject(", "Forced: true", ".Destroy(", ".Obliterate(" })
				StringAssert.DoesNotContain(forbidden, source);
			Contains(source, "future calendar argument is synthetic", "clock, position and failed effects are never reset");
		}

		[Test]
		public void SourceContract_CompletedProofRequiresBothSettledEffectsAndExactTypedFinalBinding()
		{
			string source = Read(Helper);
			Contains(Between(source, "private GameObject Completed()", "private bool ExactTombstone("),
				"KingdomFoundingHeartTerminalRules.TryDecode(wire", "terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled",
				"terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled", "terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled",
				"terminal.TransactionId == Plan.TransactionId", "terminal.ZoneId == Plan.ZoneId",
				"terminal.CompletionSeal == KingdomFoundingHeartRules.CompletionSeal(Plan)",
				"terminal.PredecessorId == KingdomFoundingHeartRules.SlotId(Plan, KingdomFoundingHeartRules.WorksSlot)",
				"terminal.FinalId == KingdomFoundingHeartRules.StableId(Plan.TransactionId, Plan.ZoneId, \"final\")",
				"terminal.Blueprint == \"r_KingdomRiteGround\"", "terminal.BuildKey == \"heartbasin\"", "terminal.PlotId == Plan.PlotId",
				"terminal.X == WorkCell.X && terminal.Y == WorkCell.Y", "GameObject final = Live(terminal.FinalId)",
				"ReferenceEquals(final.CurrentCell, WorkCell)", "ReferenceEquals(final.CurrentCell, Zone.GetCell(terminal.X, terminal.Y))",
				"Text(final, KingdomPlots.FoundingHeartTerminalProperty, wire)", "r_KingdomScaffold.HasRemovalProof(final, terminal.PredecessorId)");
			Contains(source, "body.HasStringProperty(key) && !body.HasIntProperty(key)", "body.GetStringProperty(key) == value");
			Contains(Read("Growth/KingdomScaffold.SuccessorProof.cs"), "Successor.HasStringProperty(RemovalProofProperty)",
				"!Successor.HasIntProperty(RemovalProofProperty)", "Successor.GetStringProperty(RemovalProofProperty) == PredecessorId");
		}

		[Test]
		public void SourceContract_AbsenceAloneCannotReplaceAnExactOriginatingZoneDestructionTombstone()
		{
			string source = Read(Helper);
			Ordered(Between(source, "private GameObject Completed()", "private bool ExactTombstone("),
				"!GameObject.Validate(Predecessor)", "Predecessor.IDIfAssigned == terminal.PredecessorId",
				"KingdomPlots.FindGlobalFoundingHeartId(terminal.PredecessorId, out _, out _)",
				"KingdomPhysicalLookupState.Absent && ExactTombstone(terminal.PredecessorId)");
			Contains(Between(source, "private bool ExactTombstone(", "private GameObject Live("),
				"Zone.Graveyard?.Objects", "rows == null || rows.Count > 65536", "body.IDIfAssigned == id",
				"!ReferenceEquals(body, Predecessor) || GameObject.Validate(body)", "matches++", "return matches == 1");
			Contains(Between(source, "private GameObject Live(", "private void RecoverWithoutReplay()"),
				"KingdomPhysicalLookupState.Exact && !graveyard && GameObject.Validate(body)", "body.IDIfAssigned == id",
				"ReferenceEquals(body.CurrentZone, Zone)", "body.InInventory == null && body.Equipped == null && body.Count == 1",
				"ReferenceEquals(item, body)", "count == 1");
		}

		[Test]
		public void SourceContract_RealRecoveryAndAuditMustPreserveTerminalLedgerRosterAndFinalPropertyMaps()
		{
			string recovery = Between(Read(Helper), "private void RecoverWithoutReplay()", "private static void Same<");
			Ordered(recovery, "string wire = Zone.GetZoneProperty(", "KingdomLedger ledger = System.Ledger",
				"List<string> notes = ledger.Notes", "string[] beforeNotes = notes.ToArray()", "Zone.GetObjects()",
				"new Dictionary<string, string>(strings)", "new Dictionary<string, int>(ints)", "Current(); Authority()",
				"KingdomPlots.RecoverFoundingHeart(System, Zone)", "Current(); Authority()",
				"KingdomPlots.AuditFoundingHeartReservations(System, Zone)", "ReferenceEquals(Completed(), Final)");
			Contains(recovery, "FoundingHeartTerminalProperty, null) == wire", "ReferenceEquals(System.Ledger, ledger)",
				"ReferenceEquals(ledger.Notes, notes)", "beforeNotes.Length == notes.Count", "beforeNotes[i] == notes[i]",
				"after.Length == bodies.Length", "ReferenceEquals(after[i], bodies[i])", "ReferenceEquals(Final.Property, strings)",
				"ReferenceEquals(Final.IntProperty, ints)", "Same(beforeStrings, strings); Same(beforeInts, ints); Current()");
		}

		[Test]
		public void SourceContract_CleanSetupCannotInventOrRepairGameObjectOrReceiptAuthority()
		{
			string source = Read(Helper);
			ClassicAssert.IsFalse(Regex.IsMatch(source, @"\b(?:Set\w*GameState|Remove\w*GameState|Set\w*Property|Remove\w*Property|Create|TryFound|TryCommit|TryBegin)\s*\("));
			ClassicAssert.IsFalse(Regex.IsMatch(source, @"\.(?:Clear|Reset|Destroy|Obliterate|AddPart|RemovePart|RequirePart)\s*\("));
			ClassicAssert.IsFalse(Regex.IsMatch(source, @"\b(?:Game|System|Zone|Player|Predecessor|Final|Plan|Works)\.[\w.]+\s*=(?!=)"));
			ClassicAssert.IsFalse(Regex.IsMatch(source, @"\.(?:ID|IDIfAssigned)\s*=(?!=)"));
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Compact(string value) { return Regex.Replace(value, @"\s+", ""); }
		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(last, first, end);
			return source.Substring(first, last - first);
		}
		private static void Contains(string source, params string[] tokens)
		{
			foreach (string token in tokens) StringAssert.Contains(Compact(token), Compact(source), token);
		}
		private static void Ordered(string source, params string[] tokens)
		{
			source = Compact(source);
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(Compact(token), cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, token);
				cursor = at + Compact(token).Length;
			}
		}
	}
}
#endif
