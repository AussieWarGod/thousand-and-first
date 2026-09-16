#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source wiring only; the sealed real-engine run owns behavioral evidence.</summary>
	[TestFixture]
	public sealed class KingdomWaterMaintenanceNativeSourceTests
	{
		private const string Provider = "Harness/KingdomWaterMaintenanceNativeProvider.cs";
		private const string Checks = "Harness/KingdomWaterMaintenanceNativeChecks.cs";
		[Test]
		public void DepartureProvesDurableDeliverySeparatelyFromTheCappedSummary()
		{
			string source = Read("Harness/KingdomWaterMaintenanceDepartureEvidence.cs");
			foreach (string token in new[] { "operation + \":chronicle\"", "TryFingerprint(eventId, text, false, null,",
				"TryParseRegistry(raw,", "out bool migrated, out _) && !migrated", "TryWriteRegistry(rows,",
				"string.Equals(raw, canonical, StringComparison.Ordinal)", "matches == 1", "!receipt.LegacyBlocked",
				"receipt.OfficialState == KingdomChronicleSinkDisposition.Delivered",
				"receipt.OutsiderState == KingdomChronicleSinkDisposition.Delivered",
				"receipt.JournalState == KingdomChronicleSinkDisposition.Skipped",
				"game.HasStringGameState(RegistryKey) && !game.HasIntGameState(RegistryKey)",
				"!game.HasInt64GameState(RegistryKey)", "!game.HasObjectGameState(RegistryKey)",
				"!game.HasBooleanGameState(RegistryKey)", "string.Equals(raw, Read(game, tables), StringComparison.Ordinal)",
				"exactNotes == 1 || exactNotes == 0 && summary.Length == SummaryLimit",
				"private const int SummaryLimit = 12", "omitted-at-cap", "founder-notification-verified=false",
				"current-list-membership-verified=false" }) StringAssert.Contains(token, source);
			foreach (string token in new[] { "RecordOnce(", "RecordOnceAt(", "TryProveOnceAt(",
				"SetStringGameState(", "SetIntGameState(", ".Note(", "notes.Add(", "notes.Clear(",
				"KingdomChronicleSinkDisposition.Lost ||" }) StringAssert.DoesNotContain(token, source);
			StringAssert.Contains("KingdomWaterMaintenanceDepartureEvidence.Verify(System, Game, Ledger, Departure,", Read(Checks));
		}
		[Test]
		public void EmptyCampReadsActualAutomaticStageWithoutFlushingOrInventingBodies()
		{
			string source = Read("Harness/KingdomWaterMaintenanceSealEvidence.cs");
			// The roadless testground never restages after founding (#135 typed Pending), so the
			// fixture accepts the untouched founding stage and asserts the live ledger side instead.
			foreach (string token in new[] { "store.ReadStage(game.GameID)", "staged.Population == 0",
				"staged.Revision == 1 && staged.WrittenTick == foundedTick", "KingdomSealRecord.TryParse(",
				"roundtrip.Compose() == wire", "KingdomPolityProfileRules.IsUnresolvedBodyPool(profile.BodyKeys)",
				"p.Source == KingdomPolitySource.CurrentRealm", "coordinator.NativeSpatialCaptureWaits(out string failure)",
				"coordinator.NativePendingStageEvidence() == before", "stage-revision=", " stage-schema=",
				" spatial=pending restage=none" }) StringAssert.Contains(token, source);
			foreach (string token in new[] { "TryStage(", "TryStageSemanticSnapshot(", "TryReconcile(",
				"TryCapture(", "SetOption(", "BodyKeys =", "staged.WrittenTick > foundedTick",
				"CommittedUnresolvedLegacyProfileSchema", "IsUnresolvedBodyPool(staged.CanonicalBodyKeys)",
				"StillMatches(" }) StringAssert.DoesNotContain(token, source);
			StringAssert.Contains("KingdomWaterMaintenanceSealEvidence.Verify(System, Game, LastTick", Read(Checks));
		}
		[Test]
		public void NativeDedicationCompletesBeforeInitialVesselBaselineWithoutBillingOrClockChanges()
		{
			string source = Read(Provider);
			ClassicAssert.Less(source.IndexOf("Dedicate(system, prove, proveBound);", StringComparison.Ordinal),
				source.IndexOf("VesselProofs[i] = new KingdomRaidContactBody", StringComparison.Ordinal));
			foreach (string token in new[] { "KingdomCity.CheckIn(system, Zone, survey, tick)",
				"system.LastHeartbeatTick == heartbeat", "system.DedicationCounter == counter + 2",
				"system.Ledger.UpkeepDrawn == 0", "system.Population == 0", "survey.Larders.Count == 0",
				"!Vessels[i].HasIntProperty(KingdomCity.DedicationOrderProperty)",
				"KingdomWaterMaintenanceHeartStores.Water(survey, Zone, Liquids, out int heartStores, out string heart)",
				"survey.Stores.Count == 2 + heartStores", "survey.StoredWater == 1 + heartWater" }) StringAssert.Contains(token, source);
			StringAssert.DoesNotContain("SetIntProperty(KingdomCity.DedicationOrderProperty", source);
			// The heart's first basin is a civic store; the fixture partitions, never assumes exactly two.
			foreach (string token in new[] { "survey.Stores.Count == 2 &&", "ReferenceEquals(survey.Stores[0]" })
				StringAssert.DoesNotContain(token, source + Read(Checks));
			string heart = Read("Harness/KingdomWaterMaintenanceHeartStores.cs");
			foreach (string token in new[] { "KingdomPlots.TrySurveyedHeart(zone, out KingdomPlotRules.PlotRect heart)",
				"owner.GetIntProperty(\"KingdomStores\") == 1 && heart.Contains(cell.X, cell.Y)",
				"KingdomLiquids.HasFreshWater(store) ? store.Volume : 0" }) StringAssert.Contains(token, heart);
			foreach (string token in new[] { "SetIntProperty(", "AddDrams(", "RemoveObject(", "Fill(" }) StringAssert.DoesNotContain(token, heart);
			StringAssert.Contains("Evidence.Append(\"\\ndedication-survey \").Append(Setup.DedicationSurvey)", Read(Checks));
		}
		[Test]
		public void FreshScenarioSealsWarmupEnrollmentAndFourRealWaterIntervals()
		{
			string source = Read(Provider), persona = Read("Tools/personas/water-maintenance-native-check.persona");
			foreach (string token in new[] { "script.Count == Script.Length", "script[i] == Script[i]",
				"HasAnyState(game, Receipt)", "HasQuickstartState(game)", "KingdomScenarioTransactionShape.None",
				"KingdomScenarioDurableState.ProvesExactText(Receipt, result)" }) StringAssert.Contains(token, source);
			foreach (string row in new[] { "REQUEST=founding-first-city", "START=8.22@40,12",
				"SCRIPT=stagedigest;water-maintenance-setup;advance 2400;water-maintenance-enroll;advance 1200;water-maintenance-check;advance 1200;water-maintenance-check;advance 1200;water-maintenance-refill;advance 1200;water-maintenance-check;stagedigest",
				"VERBS=water-maintenance-setup,water-maintenance-enroll,water-maintenance-check,water-maintenance-refill" })
				ClassicAssert.AreEqual(1, Regex.Matches(persona, "(?m)^" + Regex.Escape(row) + "$" ).Count);
		}
		[Test]
		public void ObserversRetainProductionControlFlowAndActualElapsedTime()
		{
			string provider = Read(Provider), checks = Read(Checks), all = provider + checks;
			foreach (string token in new[] { "typeof(EndTurnEvent)", "\"ResolveHeartbeat\"", "\"ConsumeUpkeep\"",
				"\"TryDestroyBody\"", "internal static void Prefix", "internal static void Postfix", "bool __result", "int __result" })
				StringAssert.Contains(token, provider);
			foreach (string token in new[] { "static bool Prefix", "ref bool __result", "ref int __result", "__result =",
				"KingdomGrowth.OnZoneActivated(", "KingdomGrowth.Emigrate(", ".HandleEvent(", ".ConsumeUpkeep(" })
				StringAssert.DoesNotContain(token, all);
			ClassicAssert.IsFalse(Regex.IsMatch(all, @"\.(Turns|TimeTicks|Energy|Population|LastHeartbeatTick|DryStreak|Withered|Departures)\s*=(?!=)"));
			foreach (string token in new[] { "Game.Turns == EnrollTurns + EndTurns", "Game.TimeTicks == EnrollTick + EndTurns",
				"(tick - Checkpoint) / 1200", "ReferenceEquals(survey, KingdomSurvey.ActiveFor(Zone))", "Fault == null" })
				StringAssert.Contains(token, checks);
		}
		[Test]
		public void ActualDebitsAndDepartureReproveOriginalPhysicalAndCanonicalOwners()
		{
			string checks = Read(Checks);
			foreach (string token in new[] { "amount == Need", "amount == Math.Min(StoreWater, Need)",
				"Setup.Liquids[0].Volume == StoreWater - amount", "Ledger.UpkeepDrawn == PaidTotal",
				"KingdomResidentDeparturePhase.EffectsPublished", "operation.PreparedTick == DispatchTick",
				"operation.ResidentId == Setup.Ids[index]", "operation.BodyObjectId == Setup.BodyIds[index]",
				"!row && !bound", "body.IsInGraveyard()", "Count(body, true) == 1", "DepartureMarker.TalliesClosed",
				"KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture)", "KingdomWaterMaintenanceDepartureEvidence.Verify(" })
				StringAssert.Contains(token, checks);
			StringAssert.Contains("KingdomCitizenship.TryEnroll(", Read(Provider));
			StringAssert.Contains("KingdomResidents.TryEnsureRow(", Read(Provider));
			StringAssert.Contains("id = body.ID; baseId = body._BaseID;", Read(Provider));
			StringAssert.Contains("AllocationIds.Add(baseId)", Read(Provider));
		}
		[Test]
		public void RefillTransfersRealDonorWaterAndKeepsScarcityControls()
		{
			string source = Read(Checks);
			foreach (string token in new[] { "MixWith(Setup.Liquids[3], PouredFrom: Setup.Vessels[3], Amount: 16)",
				"Setup.Liquids[0].Volume == 16 && Setup.Liquids[3].Volume == 0", "DryBills >= 3 && Departures == 1",
				"PaidTotal == 1 + HealthyDays * 2", "System.DryStreak == 0 && !System.Withered",
				"Setup.Liquids[1].IsFreshWater() && !Setup.Liquids[2].IsFreshWater()",
				"value == row.Value", "ordinary-acceptance=false; save-load=untested", "Retained.Armed = false" })
				StringAssert.Contains(token, source);
			StringAssert.DoesNotContain("KingdomLiquids.Fill(", source);
			StringAssert.Contains("Options.SetOption(\"r_TAF_OptionGrowth\", \"No\")", source);
			StringAssert.Contains("KingdomGrowth.ScarcityEnabled && !KingdomGrowth.Enabled", source);
		}
		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
	}
}
#endif
