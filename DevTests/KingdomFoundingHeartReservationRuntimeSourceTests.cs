#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// These contracts inspect wiring, not execution of native callbacks, custody, or serialization.
	[TestFixture]
	public sealed class KingdomFoundingHeartReservationRuntimeSourceTests
	{
		private const string StorePath = "Growth/KingdomPlot2.07o.FoundingHeartReservationStore.cs";
		private const string FencePath = "Growth/KingdomPlot2.07p.FoundingHeartAllocationFence.cs";
		private const string ReservationsPath = "Growth/KingdomPlot2.07l.FoundingHeartReservations.cs";

		[Test]
		public void StorePinsTheGameAndAllFiveExactOrdinalTableReferences()
		{
			string source = Source(StorePath);
			string capture = Slice(source, "internal FoundingHeartReservationStore()", "internal bool Current");
			string current = Slice(source, "internal bool Current", "private static bool Ordinal<T>");
			StringAssert.Contains("Game = The.Game;", capture);
			StringAssert.Contains("ReferenceEquals(The.Game, Game)", current);
			string[] tables = { "StringGameState", "IntGameState", "Int64GameState", "ObjectGameState", "BooleanGameState" };
			string[] refs = { "Strings", "Ints", "Longs", "Objects", "Booleans" };
			for (int i = 0; i < tables.Length; i++)
			{
				StringAssert.Contains(refs[i] + " = Game?." + tables[i], capture);
				StringAssert.Contains("ReferenceEquals(Game." + tables[i] + ", " + refs[i] + ") && Ordinal(" + refs[i] + ")", current);
			}
			string ordinal = Slice(source, "private static bool Ordinal<T>", "internal KingdomDurableKeyObservation Observe(");
			StringAssert.Contains("Table != null", ordinal);
			StringAssert.Contains("ReferenceEquals(Table.Comparer, EqualityComparer<string>.Default)", ordinal);
			StringAssert.Contains("ReferenceEquals(Table.Comparer, StringComparer.Ordinal)", ordinal);
			StringAssert.DoesNotContain("OrdinalIgnoreCase", source);
		}

		[Test]
		public void ObservationUsesFiveTypedPresenceChecksAndReprovesTheSameStore()
		{
			string observe = Slice(Source(StorePath), "internal KingdomDurableKeyObservation Observe(", "internal bool Ensure(");
			Ordered(observe, "if (!Current || string.IsNullOrEmpty(Key)) return null;",
				"bool present = Game.HasStringGameState(Key);",
				"String = present ? Game.GetStringGameState(Key, null) : null",
				"HasInt = Game.HasIntGameState(Key)", "HasInt64 = Game.HasInt64GameState(Key)",
				"HasObject = Game.HasObjectGameState(Key)", "HasBoolean = Game.HasBooleanGameState(Key)",
				"return Current ? row : null;");
			StringAssert.Contains("catch { return null; }", observe);
			StringAssert.DoesNotContain("IsNullOrEmpty(current)", observe);
		}

		[Test]
		public void EveryOneOfTheSevenReservationsIsPreflightedBeforeAnyPublication()
		{
			string ensure = Slice(Source(ReservationsPath), "private static bool EnsureFoundingHeartReservations(",
				"private static bool EnsureFoundingHeartReservation(");
			Ordered(ensure, "new FoundingHeartReservationStore()", "store.CheckPlan(Plan, AllowAbsent: true)",
				"slot < KingdomFoundingHeartRules.SlotCount", "EnsureFoundingHeartReservation(store, Plan,",
				"FoundingHeartFinalId(Plan), \"final\")", "store.CheckPlan(Plan)");
			string check = Slice(Source(StorePath), "internal bool CheckPlan(", "internal bool TryAudit(");
			Ordered(check, "if (!Current || !KingdomFoundingHeartRules.Valid(Plan)) return false;",
				"string receipt = KingdomFoundingHeartRules.Encode(Plan);",
				"slot <= KingdomFoundingHeartRules.SlotCount", "? \"final\" : \"slot-\" + slot",
				"KingdomFoundingHeartReservationState.TryExpected", "Observe(key), out bool absent)",
				"|| absent && !AllowAbsent", "KingdomFoundingHeartRules.Encode(Plan) == receipt");
			StringAssert.Contains("SlotCount = 6;", Source("Growth/KingdomFoundingHeartRules.cs"));
			StringAssert.DoesNotContain(".Ensure(", check);
			StringAssert.DoesNotContain(".Add(", check);
		}

		[Test]
		public void PublicationReobservesAbsenceUsesNonOverwritingAddAndChecksExactAfterState()
		{
			string source = Source(StorePath);
			string ensure = Slice(source, "internal bool Ensure(", "internal bool CheckPlan(");
			Ordered(ensure, "TryExpected(Key, Expected, Observe(Key), out bool absent)", "if (absent)",
				"if (!Current || !KingdomFoundingHeartReservationState.TryExpected(Key, Expected,",
				"Observe(Key), out absent) || !absent) return false;", "Strings.Add(Key, Expected);",
				"return Current && KingdomFoundingHeartReservationState.TryExpected(Key, Expected,",
				"Observe(Key), out absent) && !absent;");
			StringAssert.DoesNotContain("SetStringGameState", source + Source(ReservationsPath));
			StringAssert.DoesNotContain("Strings[", source);
			StringAssert.DoesNotContain(".Remove(", ensure);
			StringAssert.Contains("return Store.Ensure(key, expected);", Source(ReservationsPath));
			StringAssert.Contains("return new FoundingHeartReservationStore().CheckPlan(Plan);", Source(ReservationsPath));
		}

		[Test]
		public void AuditIncludesEveryTypedTableAndBracketsRecoveryWithExactRetention()
		{
			string source = Source(StorePath);
			string audit = Slice(source, "internal bool TryAudit(", "private static bool AddKeys<T>");
			foreach (string table in new[] { "Strings", "Ints", "Longs", "Objects", "Booleans" })
				StringAssert.Contains("AddKeys(" + table + ", keys, ref scanned)", audit);
			Ordered(audit, "new HashSet<string>(StringComparer.Ordinal)", "AddKeys(Strings",
				"key, Observe(key)", "KingdomFoundingHeartReservationState.TryAudit(rows, MaximumFoundingHeartCustodyObjects,",
				"|| !Current) return false;", "Reservations = exact;");
			StringAssert.Contains("Table.Count > MaximumFoundingHeartCustodyObjects - Scanned", source);
			StringAssert.Contains("key.StartsWith(FoundingHeartReservationPrefix, StringComparison.Ordinal)", source);
			string reservations = Source(ReservationsPath);
			Ordered(reservations, "store.TryAudit(out Dictionary<string, string> reservations)", "Z.GetObjects()",
				"store.Retains(reservations, AllowAdditional: false)", "RecoverFoundingHeart(System, Z)",
				"store.Retains(reservations, AllowAdditional: true)");
			string retained = Slice(source, "internal bool Retains(", "private static bool HasAnyFoundingHeartReservation(");
			StringAssert.Contains("!AllowAdditional && current.Count != Before.Count", retained);
			StringAssert.Contains("!current.TryGetValue(row.Key, out string value) || value != row.Value", retained);
			StringAssert.Contains("return Current;", retained);
			StringAssert.Contains("!= KingdomDurableKeyShape.Absent", source);
		}

		[Test]
		public void AllocationFenceReprovesFrozenOwnerReceiptZoneAndAllReservations()
		{
			string source = Source(FencePath);
			string current = Slice(source, "internal bool Current", "internal GameObject Create(");
			foreach (string proof in new[] { "Store.Current", "ReferenceEquals(Context.Plan, Plan)",
				"Context.Receipt == Wire", "KingdomFoundingHeartRules.Encode(Plan) == Wire",
				"Zone.ZoneID == Plan.ZoneId", "ReferenceEquals(The.ZoneManager, Manager)",
				"ReferenceEquals(Store.Game.ZoneManager, Manager)",
				"ReferenceEquals(Store.Game.GetSystem<KingdomSystem>(), System)",
				"System.CurrentRealmId == Realm", "System.CurrentSettlementId == Settlement",
				"transaction == Plan.TransactionId", "Zone.GetZoneProperty(FoundingHeartReceiptProperty, null) == Wire",
				"ExactFoundingHeartZoneTruth(Zone, Plan) && Store.CheckPlan(Plan)" })
				StringAssert.Contains(proof, current);
			StringAssert.Contains("catch { return false; }", current);
		}

		[Test]
		public void FactoryAcceptsOnlyItsSinglePreEventReferenceWithUnchangedOriginalIdentity()
		{
			string create = Slice(Source(FencePath), "internal GameObject Create(", "private static bool UnplacedFoundingHeartOutput(");
			Ordered(create, "if (!Current) return null;", "GameObject observed = null;", "string originalId = null;",
				"int observations = 0;", "bool owned = false;",
				"GameObject.Create(Blueprint, BeforeObjectCreated: candidate => {", "observations++;",
				"observed = candidate;", "originalId = candidate?.IDIfAssigned;",
				"owned = observations == 1 && UnplacedFoundingHeartOutput(candidate, Blueprint) && Current;",
				"return owned && observations == 1 && ReferenceEquals(result, observed)",
				"UnplacedFoundingHeartOutput(result, Blueprint) && result.IDIfAssigned == originalId",
				"&& Current ? result : null;");
			StringAssert.Contains("catch { return null; }", create);
			foreach (string forbidden in new[] { "IDIfAssigned = ", "SetIntProperty", "SetStringProperty", "RemoveCreatedWorks" })
				StringAssert.DoesNotContain(forbidden, create);
		}

		[Test]
		public void FactoryCustodyProofRequiresValidUnplacedUnloadedOutput()
		{
			string source = Source(FencePath);
			string custody = source.Substring(source.IndexOf("private static bool UnplacedFoundingHeartOutput(", StringComparison.Ordinal));
			foreach (string proof in new[] { "GameObject.Validate(Output)", "Output.Blueprint == Blueprint",
				"Output.CurrentCell == null", "Output.CurrentZone == null", "Output.InInventory == null",
				"Output.Physics.InInventory == null", "FoundingHeartLoadedReferenceCount(Output) == 0" })
				StringAssert.Contains(proof, custody);
		}

		[Test]
		public void MarkCreationRechecksTheFenceBeforeStampingIdentityAndRooting()
		{
			string marks = Slice(Source("Growth/KingdomPlot2.07c.FoundingHeartMarks.cs"),
				"private static bool DriveFoundingHeartMark(", "private static bool PlaceOrSettleFoundingHeartMark(");
			Ordered(marks, "new FoundingHeartAllocationFence(Z, Context)", "if (!fence.Current) return false;",
				"created = fence.Create(FoundingHeartSlotBlueprint(Slot))", "UnplacedFoundingHeartOutput(created,",
				"|| !fence.Current) return false;", "created.SetIntProperty(FoundingHeartSlotMark(Slot), 1)",
				"if (!fence.Current || !StageFoundingHeartIdentity(created, plan, Slot)", "RootFoundingHeartOutput(plan, Slot, created)");
			StringAssert.DoesNotContain("GameObject.Create(", marks);
		}

		[Test]
		public void HeartStakeUsesCheckedFactoryAndCarriesItsFenceThroughIdentityPublication()
		{
			string stake = Source("Growth/KingdomPlot2.11.Stake.cs");
			Ordered(stake, "FoundingHeartAllocationFence heartFence = Heart == null ? null",
				"new FoundingHeartAllocationFence(Z, Heart.Context)", "Heart != null && !heartFence.Current",
				"Heart == null ? GameObject.Create(WorksBlueprint) : heartFence.Create(WorksBlueprint)",
				"!UnplacedFoundingHeartOutput(works, WorksBlueprint) || !heartFence.Current",
				"works.GetPart<r_KingdomPlotWorks>()", "ref Job, Heart, heartFence)");
			string add = Source("Growth/KingdomPlot2.11a.StakeAdd.cs");
			Ordered(add, "FoundingHeartPlacement Heart, FoundingHeartAllocationFence HeartFence)",
				"if (HeartFence == null || !HeartFence.Current)", "PreparedFoundingHeartWorksShape(works, Heart.Context)",
				"StageFoundingHeartIdentity(works, Heart.Context.Plan, Heart.Slot)", "PrepareFoundingHeartWorksAdd(Heart, works)",
				"cell.AddObject(works, NoStack: Heart != null)", "ObserveAddResultInActive",
				"SettleFoundingHeartWorksAdd(Heart, works, accepted, callbackThrew)");
		}

		[Test]
		public void FinalCreationRechecksFenceBeforeIdentityAndRetainsRootAfterPublicationCut()
		{
			string begin = FinalBegin();
			Ordered(begin, "new FoundingHeartAllocationFence(Z, Context)", "if (!fence.Current) return HeartRefused(\"terminal: allocation authority\")",
				"Final = fence.Create(Context.Stake.Blueprint)", "UnplacedFoundingHeartOutput(Final, Context.Stake.Blueprint) || !fence.Current",
				"Final.IDIfAssigned = finalId;", "Final.SetStringProperty(FoundingHeartTerminalProperty, encoded)",
				"if (!fence.Current) return HeartRefused(\"terminal: prepared allocation authority\")",
				"if (!ExactPreparedFoundingHeartFinal(Final, Z, Context, Terminal))",
				"RootFoundingHeartFinal(plan, Final)", "published = true;", "PublishFoundingHeartTerminal(");
			StringAssert.DoesNotContain("GameObject.Create(", begin);
		}

		[Test]
		public void FinalCleanupRequiresUnpublishedExactOwnedUnplacedPreparedOutput()
		{
			string begin = FinalBegin();
			string cleanup = begin.Substring(begin.IndexOf("finally", StringComparison.Ordinal));
			Ordered(cleanup, "if (!published && fence.Current && UnplacedFoundingHeartOutput(Final, Context.Stake.Blueprint)",
				"&& ExactFoundingHeartFinalObjectGameState(plan, Final, false)",
				"&& Terminal != null && ExactPreparedFoundingHeartFinal(Final, Z, Context, Terminal))",
				"RemoveCreatedWorks(Final, Z);");
			ClassicAssert.AreEqual(1, begin.Split(new[] { "RemoveCreatedWorks(" }, StringSplitOptions.None).Length - 1);
			StringAssert.DoesNotContain("if (!published && GameObject.Validate(Final))", begin);
		}

		private static string FinalBegin()
		{
			return Slice(Source("Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs"),
				"private static bool BeginFoundingHeartTerminal(", "private static bool ExactPreparedFoundingHeartFinal(");
		}

		private static string Source(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Slice(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(last, first, end);
			return source.Substring(first, last - first);
		}
		private static void Ordered(string source, params string[] terms)
		{
			int cursor = 0;
			foreach (string term in terms)
			{
				int found = source.IndexOf(term, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, term);
				cursor = found + term.Length;
			}
		}
	}
}
#endif
