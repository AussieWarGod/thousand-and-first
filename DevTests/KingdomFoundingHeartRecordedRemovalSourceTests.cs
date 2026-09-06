#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	// Source contracts only; no destruction, pooling, save/load or native recovery is executed.
	[TestFixture]
	public sealed class KingdomFoundingHeartRecordedRemovalSourceTests
	{
		private const string Seal = "Growth/KingdomPlot2.07h.FoundingHeartSeal.cs";
		private const string Custody = "Growth/KingdomPlot2.07g.FoundingHeartCustody.cs";
		private const string Recorded = "Growth/KingdomPlot2.07q.FoundingHeartRecordedRemoval.cs";
		private const string Authority = "Growth/KingdomPlot2.07i.FoundingHeartTerminalAuthority.cs";
		private const string Drive = "Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs";
		private const string Settlement = "Growth/KingdomPlot2.07k.FoundingHeartTerminalSettlement.cs";
		private const string Rules = "Growth/KingdomFoundingHeartTerminalRules.cs";
		private const string Tombstones = "Growth/KingdomPlot2.07m.FoundingHeartTombstones.cs";

		[Test]
		public void SourceContract_RecordedRetirementRemainsBehindFullAuthorityAndRetiredCustody()
		{
			Ordered(Tail(Read(Seal), "private static bool ExactFoundingHeartRetiredAuthority("),
				"Context = null", "KingdomFoundingHeartRules.TryDecode(raw, out KingdomFoundingHeartPlan plan)",
				"KingdomFoundingHeartRules.Complete(plan) && plan.ZoneId == Z.ZoneID",
				"KingdomFoundingHeartRules.SlotId(plan, KingdomFoundingHeartRules.WorksSlot) == PredecessorId",
				"ExactFoundingHeartSeal(Z, plan)", "ExactFoundingHeartReservations(plan)",
				"TryReadFoundingHeartContext(Z, plan, out Context)", "ExactFoundingHeartMarkerRoster(Z, plan, false)",
				"ExactFoundingHeartRetiredCustody(plan)", "ExactFoundingHeartRetirementProof(Z, Context, PredecessorId)");
			string custody = Between(Read(Custody), "private static bool ExactFoundingHeartRetiredCustody(",
				"private static bool HasGlobalFoundingHeartTransactionEvidence(");
			Ordered(custody, "if (!ExactFoundingHeartOwnedRoster(Plan)) return false",
				"slot < KingdomFoundingHeartRules.WorksSlot", "FindGlobalFoundingHeartId(",
				"!= KingdomPhysicalLookupState.Exact", "graveyard || FoundingHeartLoadedReferenceCount(exact) != 1",
				"!ExactFoundingHeartObjectGameState(Plan, slot, exact, false)",
				"return ExactFoundingHeartLiveAbsence(worksId)",
				"&& ExactFoundingHeartObjectGameState(Plan, works, null, false)");
			StringAssert.DoesNotContain("ExactFoundingHeartGraveyardTombstone", custody);
			Ordered(Between(Read(Drive), "private static bool RepairFoundingHeartFinalIntent(",
				"private static bool BeginFoundingHeartTerminal("),
				"Terminal.Phase >= KingdomFoundingHeartTerminalPhase.Removed",
				"return ExactFoundingHeartRetiredAuthority(Z, Terminal.PredecessorId, out _)");
		}

		[Test]
		public void SourceContract_ExactAndConflictingTombstonesCannotFallThroughToRecordedAbsence()
		{
			string source = Between(Read(Recorded), "private static bool ExactFoundingHeartRetirementProof(",
				"private static bool ExactFoundingHeartRecordedFinal(");
			Ordered(source, "FindGraveyardTombstone(PredecessorId, out GameObject tombstone)",
				"if (state == KingdomPhysicalLookupState.Exact) return FoundingHeartTombstoneIdentity(tombstone, Context.Plan, KingdomFoundingHeartRules.WorksSlot)",
				"if (state != KingdomPhysicalLookupState.Absent) return false",
				"Z?.GetZoneProperty(FoundingHeartTerminalProperty, null)",
				"!KingdomFoundingHeartTerminalRules.TryDecode(raw, out var terminal)",
				"!FoundingHeartTerminalBinding(Context, terminal)", "terminal.PredecessorId != PredecessorId",
				"!string.IsNullOrEmpty(Z.GetZoneProperty(FoundingHeartTerminalFailureProperty, null))", "return false",
				"bool exactFinal = ExactFoundingHeartRecordedFinal(Z, Context, terminal, raw)",
				"return KingdomFoundingHeartTerminalRules.CanUseRecordedRemoval(terminal, exactFinal, ExactFoundingHeartLiveAbsence(PredecessorId), state)");
			Ordered(Between(Read(Tombstones), "private static bool FoundingHeartTombstoneIdentity(",
				"private static bool TryClassifyFoundingHeartTombstone("),
				"if (Object == null || GameObject.Validate(Object)) return false", "try",
				"Object.IDIfAssigned == KingdomFoundingHeartRules.SlotId(Plan, Slot)",
				"Object.GetStringProperty(FoundingHeartOwnerProperty) == Plan.TransactionId",
				"Object.GetIntProperty(FoundingHeartSlotProperty) == Slot + 1", "catch", "return false");
		}

		[Test]
		public void SourceContract_RecordedPhaseRequiresCanonicalWireExactTypedMirrorAndRemovalMarker()
		{
			Contains(Between(Read(Rules), "public static bool CanUseRecordedRemoval(", "public static bool ExactAddCut("),
				"return Valid(Plan) && Plan.Phase >= KingdomFoundingHeartTerminalPhase.Removed && ExactFinalWitness && LivePredecessorAbsent && TombstoneState == KingdomPhysicalLookupState.Absent");
			Contains(Between(Read(Rules), "public static bool TryDecode(", "private static bool Sink("),
				"if (!Valid(Plan) || Encode(Plan) != Encoded) Plan = null", "catch { Plan = null; }", "return Plan != null");
			Ordered(Read(Recorded), "!ExactFoundingHeartString(final, FoundingHeartTerminalProperty, Raw)",
				"final.HasIntProperty(FoundingHeartTerminalFailureProperty)",
				"!string.IsNullOrEmpty(final.GetStringProperty(FoundingHeartTerminalFailureProperty))",
				"!r_KingdomScaffold.HasRemovalProof(final, Terminal.PredecessorId)");
			Contains(Between(Read("Growth/KingdomPlot2.07f.FoundingHeartStakeTruth.cs"),
				"private static bool ExactFoundingHeartString(", "private static bool FoundingHeartPropertyAbsent("),
				"return Object.HasStringProperty(Key) && !Object.HasIntProperty(Key) && Object.GetStringProperty(Key) == Expected");
			Contains(Between(Read("Growth/KingdomScaffold.SuccessorProof.cs"), "public static bool HasRemovalProof(",
				"public static bool HasPendingImprovementSuccessorEvidence("),
				"GameObject.Validate(Successor) && !string.IsNullOrEmpty(PredecessorId)",
				"Successor.HasStringProperty(RemovalProofProperty) && !Successor.HasIntProperty(RemovalProofProperty)",
				"Successor.GetStringProperty(RemovalProofProperty) == PredecessorId");
		}

		[Test]
		public void SourceContract_RecordedFinalUsesObservationalBindingEndpointAndTruthBeforeDriverAudit()
		{
			Ordered(Tail(Read(Recorded), "private static bool ExactFoundingHeartRecordedFinal("),
				"FindGlobalFoundingHeartId(Terminal.FinalId, out GameObject final, out bool graveyard)",
				"!= KingdomPhysicalLookupState.Exact || graveyard", "!ExactPreparedFoundingHeartFinal(final, Z, Context, Terminal)",
				"final.CurrentCell == null || final.CurrentCell != Z.GetCell(Terminal.X, Terminal.Y)",
				"!ExactFoundingHeartFinalTruth(final, Context.Stake)",
				"!ExactFoundingHeartString(final, KingdomUpgrade.BuildKeyProperty, Terminal.BuildKey)",
				"!ExactFoundingHeartString(final, PlotIdProperty, Terminal.PlotId)");
			Contains(Between(Read(Authority), "private static bool FoundingHeartTerminalBinding(",
				"private static bool PublishFoundingHeartTerminal("),
				"KingdomFoundingHeartRules.Complete(plan)", "KingdomFoundingHeartTerminalRules.Valid(Terminal)",
				"Terminal.TransactionId == plan.TransactionId", "Terminal.CompletionSeal == KingdomFoundingHeartRules.CompletionSeal(plan)",
				"Terminal.ZoneId == plan.ZoneId", "Terminal.PredecessorId == KingdomFoundingHeartRules.SlotId(plan, KingdomFoundingHeartRules.WorksSlot)",
				"Terminal.FinalId == FoundingHeartFinalId(plan)", "Terminal.Blueprint == Context.Stake.Blueprint",
				"Terminal.BuildKey == Context.Stake.BuildKey && Terminal.PlotId == plan.PlotId",
				"Terminal.X == Context.Architecture.MainWorldX", "Terminal.Y == Context.Architecture.MainWorldY");
			Contains(Between(Read(Drive), "private static bool ExactPreparedFoundingHeartFinal(",
				"private static bool ExactSettledFoundingHeartFinal("),
				"!GameObject.Validate(Final) || !FoundingHeartTerminalBinding(Context, Terminal)",
				"Final.IDIfAssigned != Terminal.FinalId || Final.Blueprint != Terminal.Blueprint",
				"!SameIntent(intent, Context.Architecture)", "Final.CurrentCell == Z.GetCell(Terminal.X, Terminal.Y)",
				"Final.CurrentZone == Z && Final.InInventory == null");
			Ordered(Between(Read(Drive), "private static bool ExactSettledFoundingHeartFinal(",
				"private static bool AdvanceFoundingHeartTerminal("), "ExactPreparedFoundingHeartFinal(Final, Z, Context, Terminal)",
				"ExactFinalBuilding(Final, Z, Z.GetCell(Terminal.X, Terminal.Y)", "ExactFoundingHeartFinalTruth(Final, Context.Stake)");
			Ordered(Between(Read(Drive), "private static bool DriveFoundingHeartTerminal(",
				"private static bool RepairFoundingHeartFinalIntent("),
				"!RepairFoundingHeartFinalIntent(Z, final, Context, terminal)",
				"terminal.Phase != KingdomFoundingHeartTerminalPhase.EffectsSettled",
				"!ExactSettledFoundingHeartFinal(final, Z, Context, terminal)",
				"!ExactFoundingHeartRetiredAuthority(Z, terminal.PredecessorId, out _)",
				"return RetireFoundingHeartFinalRoot(plan, final)");
			Contains(Between(Read("Growth/KingdomPlot2.07f.FoundingHeartStakeTruth.cs"),
				"private static bool ExactFoundingHeartFinalTruth(", "private static bool ExactFoundingHeartFinalShape("),
				"!ExactFoundingHeartFinalShape(Building, Truth)", "!graveyard && object.ReferenceEquals(exact, Building)",
				"FoundingHeartLoadedReferenceCount(Building) == 1");
		}

		[Test]
		public void SourceContract_AllRootTablesAreObservedWithPhaseSensitiveExactFinalCustody()
		{
			Ordered(Tail(Read(Recorded), "private static bool ExactFoundingHeartRecordedFinal("),
				"FoundingHeartReservationStore store = new FoundingHeartReservationStore()",
				"for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)",
				"KingdomScenarioStateShape.Classify(store.Observe(FoundingHeartRootKey(Context.Plan, slot)), out _) != KingdomDurableKeyShape.Absent",
				"return false", "var root = store.Observe(FoundingHeartFinalRootKey(Context.Plan))",
				"if (root == null || root.HasString || root.HasInt || root.HasInt64 || root.HasBoolean) return false",
				"bool rooted = ExactFoundingHeartFinalObjectGameState(Context.Plan, final, true)",
				"return store.Current && (Terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled ? rooted || !root.HasObject && ExactFoundingHeartFinalObjectGameState(Context.Plan, final, false) : rooted)");
			Contains(Between(Read("Growth/KingdomPlot2.07o.FoundingHeartReservationStore.cs"),
				"internal KingdomDurableKeyObservation Observe(", "internal bool Ensure("),
				"if (!Current || string.IsNullOrEmpty(Key)) return null", "Game.HasStringGameState(Key)",
				"Game.HasIntGameState(Key)", "Game.HasInt64GameState(Key)", "Game.HasObjectGameState(Key)",
				"Game.HasBooleanGameState(Key)", "return Current ? row : null", "catch { return null; }");
			Contains(Between(Read(Authority), "private static bool ExactFoundingHeartFinalObjectGameState(",
				"private static bool FoundingHeartTerminalBinding("), "foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)",
				"if (++visited > MaximumFoundingHeartCustodyObjects) return false", "row.Key != key",
				"!object.ReferenceEquals(root, Expected)", "!object.ReferenceEquals(item, Expected)",
				"matches == 1 && The.Game.ObjectGameState.TryGetValue(key, out object exact)",
				"matches == 0 && !The.Game.ObjectGameState.ContainsKey(key)", "catch { return false; }");
		}

		[Test]
		public void SourceContract_RecordedProofAndLiveAbsenceObserveLoadedStateWithoutRepairOrRemoteWarming()
		{
			string absent = Tail(Read(Tombstones), "private static bool ExactFoundingHeartLiveAbsence(");
			Ordered(absent, "if (string.IsNullOrEmpty(Id)) return false",
				"if (!TryFoundingHeartCustodyRoots(pending, graveyard)) return false",
				"item == null || !expanded.Add(item) || graveyard.Contains(item)",
				"expanded.Count > MaximumFoundingHeartCustodyObjects || item.IDIfAssigned == Id", "return false",
				"item.GetInventoryDirectAndEquipment()", "catch { return false; }", "return true");
			string proof = Read(Recorded) + Tail(Read(Seal), "private static bool ExactFoundingHeartRetiredAuthority(")
				+ Between(Read(Custody), "private static bool ExactFoundingHeartRetiredCustody(",
					"private static bool HasGlobalFoundingHeartTransactionEvidence(")
				+ Between(Read(Drive), "private static bool ExactPreparedFoundingHeartFinal(",
					"private static bool ExactSettledFoundingHeartFinal(")
				+ Tail(Read("Growth/KingdomPlot2.07f.FoundingHeartStakeTruth.cs"), "private static bool ExactFoundingHeartFinalTruth(");
			Assert.IsFalse(Regex.IsMatch(proof,
				@"\b(?:Set\w*Property|Set\w*GameState|Ensure\w*|Repair\w*|Recover\w*|Publish\w*|Destroy\w*|Obliterate\w*|Remove\w*|Clear|Pool\w*|Release\w*|Create\w*|TryReadFoundingHeartTerminal|ExactSettledFoundingHeartFinal|ExactFinalBuilding|TryVerifyComplete|TryExactOutput|Quarantine\w*)\s*\("));
			Assert.IsFalse(Regex.IsMatch(proof + absent + Read("Growth/KingdomPlot2.07b.FoundingHeartIdentity.cs")
				+ Read("Growth/KingdomPlot2.32c.LoadedGraveyards.cs"),
				@"\b(?:GetZone|LoadZone|FetchZone|ThawZone|Warm\w*|Pool\w*|Release\w*)\s*\("));
		}

		[Test]
		public void SourceContract_FreshDestroyStillMeasuresReturnInvalidityLiveAbsenceAndExactReferenceBeforeMarker()
		{
			string fresh = Between(Read(Settlement), "KingdomPhysicalLookupState before =",
				"private static bool SettleFoundingHeartEffects(");
			Ordered(fresh, "FindGlobalFoundingHeartId(Terminal.PredecessorId", "before != KingdomPhysicalLookupState.Exact || graveyard",
				"!TryReadFoundingHeartWorkAuthority(Z, predecessor, out _)", "bool returned = false", "bool removed = false",
				"try { removed = predecessor.Destroy(null, Silent: true); returned = true; }", "catch { }",
				"finally { KingdomSurvey.ObserveCurrentTopologyInActive(Z, predecessor); }",
				"!ExactFoundingHeartFinalObjectGameState(Context.Plan, Final, true)",
				"!KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(returned, removed, GameObject.Validate(predecessor), KingdomConstruction.FindExactId(Z, Terminal.PredecessorId, out _) == KingdomPhysicalLookupState.Absent, ExactGraveyardTombstone(Terminal.PredecessorId, predecessor))",
				"return QuarantineFoundingHeartTerminal(", "KingdomSurvey.ObserveRemovedFromActive(Z, predecessor)",
				"Final.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, Terminal.PredecessorId)",
				"!r_KingdomScaffold.HasRemovalProof(Final, Terminal.PredecessorId)",
				"!ExactFoundingHeartRetiredAuthority(Z, Terminal.PredecessorId, out _)",
				"return AdvanceFoundingHeartTerminal(", "KingdomFoundingHeartTerminalPhase.Removed");
			Contains(Between(Read(Rules), "public static bool ExactRemovalTombstone(", "public static bool CanUseRecordedRemoval("),
				"return CallbackReturned && CallbackResult && !PredecessorValid && ActiveIdAbsent && ExactIdentityTombstone");
			StringAssert.DoesNotContain("CanUseRecordedRemoval", fresh);
			Assert.AreEqual(1, Regex.Matches(Read(Settlement), @"\.Destroy\s*\(").Count);
		}

		[Test]
		public void SourceContract_ExistingSameBindingMirrorRecoveryRemainsSeparateFromObservationalProof()
		{
			string read = Between(Read(Authority), "private static bool TryReadFoundingHeartTerminal(",
				"private static bool QuarantineFoundingHeartTerminal(");
			Ordered(read, "if (string.IsNullOrEmpty(raw))", "!TryFoundingHeartFinalRoot(plan, out Final)",
				"!FoundingHeartTerminalBinding(Context, Terminal)", "!ExactFoundingHeartFinalObjectGameState(plan, Final, true)",
				"Z.SetZoneProperty(FoundingHeartTerminalProperty, raw)");
			Ordered(read, "string mirror = Final.GetStringProperty(FoundingHeartTerminalProperty)", "if (mirror != raw)",
				"!KingdomFoundingHeartTerminalRules.TryDecode(mirror, out var prior)",
				"!KingdomFoundingHeartTerminalRules.SameBinding(prior, Terminal)", "return false",
				"Final.SetStringProperty(FoundingHeartTerminalProperty, raw)");
			Ordered(Between(Read(Drive), "private static bool DriveFoundingHeartTerminal(",
				"private static bool RepairFoundingHeartFinalIntent("),
				"TryReadFoundingHeartTerminal(Z, Context, out terminal, out final)",
				"RepairFoundingHeartFinalIntent(Z, final, Context, terminal)");
			StringAssert.DoesNotContain("TryReadFoundingHeartTerminal", Read(Recorded));
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Compact(string value) { return Regex.Replace(value, @"\s+", ""); }
		private static string Tail(string source, string start) { return Between(source, start, null); }
		private static string Between(string source, string start, string end)
		{
			source = Compact(source); start = Compact(start);
			int first = source.IndexOf(start, StringComparison.Ordinal); Assert.GreaterOrEqual(first, 0, start);
			if (end == null) return source.Substring(first);
			int last = source.IndexOf(Compact(end), first + start.Length, StringComparison.Ordinal);
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
