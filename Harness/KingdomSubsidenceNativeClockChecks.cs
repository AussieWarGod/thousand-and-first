using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Native refusal probe of captured settlement/body/receipt/key fields only.
	/// Not a proof of arbitrary world effects. No engine serialization or normalization.</summary>
	internal static class KingdomSubsidenceNativeClockChecks
	{
		private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private sealed class Capture
		{
			internal readonly List<Action> Checks = new List<Action>();
			internal void Check() { foreach (Action check in Checks) check(); }
			internal void Watch(Func<object> read, string label, bool descend = false)
			{
				object before = read();
				Checks.Add(() => Require(Same(before, read()), "changed " + label));
				if (descend && before != null) Data(before, label);
			}
			internal void Named(object owner, string names, bool descend = false)
			{
				Require(owner != null, "missing field owner");
				foreach (string name in names.Split(' '))
				{
					FieldInfo field = Find(owner.GetType(), name);
					Watch(() => field.GetValue(owner), owner.GetType().Name + "." + name, descend);
				}
			}
			internal void Data(object value, string label)
			{
				Type type = value.GetType();
				if (Atom(type)) return;
				if (type == typeof(List<int>) || type == typeof(List<long>) || type == typeof(List<string>) || type == typeof(List<GameObject>))
				{
					IList list = (IList)value; int count = list.Count;
					Require(count <= 4096, "list bound"); Checks.Add(() => Require(list.Count == count, "list count " + label));
					for (int i = 0; i < count; i++) { int index = i; Watch(() => list[index], label); }
					return;
				}
				if (type == typeof(Dictionary<string, string>) || type == typeof(Dictionary<string, int>))
				{
					IDictionary map = (IDictionary)value; int count = map.Count;
					Require(count <= 4096, "dictionary bound"); Checks.Add(() => Require(map.Count == count, "map count " + label));
					foreach (DictionaryEntry entry in map)
					{
						object key = entry.Key, item = entry.Value;
						Checks.Add(() => Require(map.Contains(key) && Same(item, map[key]), "map entry " + label));
					}
					return;
				}
				Require(type == typeof(KingdomCityBook) || type == typeof(KingdomBindingRegistry)
					|| type == typeof(KingdomLedger) || type == typeof(KingdomResidentDepartureOperation)
					|| type == typeof(KingdomNamedCookReceipt) || type == typeof(KingdomAssentingMootReceipt)
					|| type == typeof(r_KingdomCitizenship), "unknown record type " + type.Name);
				// Every declared field must belong to the closed raw-record schema.
				string schema = " " + Schema(type) + " ";
				Require(type.GetFields(Fields | BindingFlags.DeclaredOnly).Length == Schema(type).Split(' ').Length,
					"record field count " + type.Name);
				foreach (FieldInfo field in type.GetFields(Fields | BindingFlags.DeclaredOnly))
				{
					Require(schema.Contains(" " + field.Name + " "),
						"unknown field " + type.Name + "." + field.Name);
					Watch(() => field.GetValue(value), type.Name + "." + field.Name, field.Name != "DistanceCache");
				}
			}
			internal void Rack(object rack)
			{
				Named(rack, "Items Size Length Variant");
				Array items = (Array)Find(rack.GetType(), "Items").GetValue(rack);
				Require(items != null && items.Length <= 4096, "rack bound");
				for (int i = 0; i < items.Length; i++) { int index = i; Watch(() => items.GetValue(index), "rack item"); }
			}
			internal void Body(GameObject body, Zone zone)
			{
				Require(body != null && body.Physics != null && body.Brain != null, "missing resident body");
				Named(body, "_BaseID Flags Live Blueprint Physics Brain Body Inventory Stacker PartsList");
				Named(body, "Property IntProperty", true); Rack(body.PartsList);
				Require(body.Inventory != null && body.Inventory.Objects != null && body.Inventory.Objects.Count == 0, "resident inventory");
				Named(body.Inventory, "Objects", true);
				Named(body.Physics, "_ParentObject _CurrentCell _InInventory _Equipped");
				Cell cell = body.Physics._CurrentCell;
				Require(cell != null && ReferenceEquals(cell.ParentZone, zone)
					&& body.Physics._InInventory == null && body.Physics._Equipped == null, "resident custody");
				Named(cell, "X Y ParentZone Objects"); Rack(cell.Objects);
				int copies = 0, receipts = 0;
				foreach (GameObject item in cell.Objects) if (ReferenceEquals(item, body)) copies++;
				Require(copies == 1, "resident cell multiplicity");
				foreach (IPart part in body.PartsList)
				{
					Require(part != null && ReferenceEquals(part._ParentObject, body), "part ownership");
					Named(part, "_ParentObject");
					if (part is r_KingdomCitizenship) { Data(part, "citizenship"); receipts++; }
				}
				Require(receipts == 1, "citizenship multiplicity");
				Named(body.Brain, "Flags LeaderReference Allegiance");
				object leader = Find(body.Brain.GetType(), "LeaderReference").GetValue(body.Brain);
				if (leader != null) Named(leader, "ID Object");
				object allegiance = body.Brain.Allegiance; int depth = 0;
				while (allegiance != null)
				{
					Require(++depth <= 16, "allegiance chain bound");
					Named(allegiance, "SourceID Previous Reason Flags Buckets Slots Size Length Amount Next Version _Seed");
					foreach (string name in new[] { "Buckets", "Slots" })
					{
						Array array = (Array)Find(allegiance.GetType(), name).GetValue(allegiance);
						Require(array != null && array.Length <= 4096, "allegiance array bound");
						for (int i = 0; i < array.Length; i++) { int index = i; Watch(() => array.GetValue(index), "allegiance slot"); }
					}
					allegiance = Find(allegiance.GetType(), "Previous").GetValue(allegiance);
				}
			}
		}

		internal static void Verify(KingdomSubsidenceNativeFixture fixture, Zone zone, KingdomSurvey survey, long now)
		{
			Require(fixture != null && fixture.System != null && now > 0 && now < long.MaxValue, "probe arguments");
			XRLGame game = fixture.Game; KingdomSystem system = fixture.System;
			long original = system.LastSubsidenceTick;
			Owner(fixture, zone, survey, game, now);
			Require(original >= 0 && original < now && system.Population == 50 && system.Stage == GrowthStage.City
				&& !system.SubsidenceAnnounced && KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture)
				&& system.ResidentDeparture.PriorCook == null && system.ResidentDeparture.PriorOffice == null
				&& system.ResidentDeparture.PriorPolity == null, "not before real departures");
			Require(KingdomSubsidenceStepCodec.TryDecode(system.City.SubsidenceModel, out var book)
				&& book.Admission == KingdomSubsidenceAdmission.Admitted && book.RealmId == system.CurrentRealmId
				&& book.SettlementId == system.CurrentSettlementId
				&& book.Sequence == 0 && book.Active == null && book.LastRetiredTick == 0
				&& book.BatchModel == KingdomSubsidenceBatchRules.None && book.OptionModel == KingdomSubsidenceStepRules.NoOption
				&& book.FailureModel == KingdomSubsidenceReportArchive.None, "not exact seeded book");
			string key = KingdomSubsidence.OptionStatePrefix + system.CurrentSettlementId;
			Require(game.StringGameState.TryGetValue(key, out string option)
				&& KingdomElapsedOptionRules.TryDecode(option, out var row) && row.State == KingdomElapsedOptionState.Enabled
				&& row.ObservedTick == original && row.MasterResumeToken == system.MasterAppliedResumeToken
				&& KingdomElapsedOptionRules.Encode(row) == option, "not exact seeded option");
			Capture capture = new Capture();
			capture.Named(system, "Population Stage SupportedLevel SubsidenceBinding SubsidenceAnnounced RealmId SettlementName "
				+ "KingdomFactionName MasterOption MasterOptionTick MasterResumeToken MasterAppliedResumeToken ResidentCounter "
				+ "City Bindings Ledger ResidentDeparture ChronicleEntries OutsiderEntries ClaimedZones", true);
			capture.Named(fixture, "Residents Ids Owned", true);
			capture.Named(survey, "Settlers CitizenBodies Objects LoadedObjects Works Built Defences", true);
			capture.Named(survey, "StoredWater OpenWater StorageSpace StorageCapacity Citizens DistrictDefenceBonus FoodStored FoodCapacity FoodAbundance");
			GameObject[] bodies = new GameObject[fixture.Bodies.Count];
			Require(bodies.Length == 50 && fixture.ResidentIds.Count == 50 && survey.Settlers.Count == 50, "fixture graph size");
			for (int i = 0; i < bodies.Length; i++)
			{
				int index = i; bodies[i] = fixture.Bodies[i];
				capture.Watch(() => fixture.Bodies[index], "fixture body");
				capture.Watch(() => fixture.ResidentIds[index], "fixture resident id");
				capture.Watch(() => survey.Settlers[index], "survey body");
				capture.Body(bodies[i], zone);
			}
			capture.Checks.Add(() => Require(fixture.Bodies.Count == 50 && fixture.ResidentIds.Count == 50
				&& survey.Settlers.Count == 50, "resident graph count"));
			capture.Named(game, "StringGameState IntGameState Int64GameState BooleanGameState ObjectGameState");
			foreach (string stateKey in new[] { key, "r_TAF_ChronicleEventRegistry_v1", KingdomScenarioProvenanceRules.ProvenanceState,
				KingdomScenarioRealizer.StampedState, KingdomScenarioRealizer.EngineSeedState, KingdomScenarioNewGameGate.RequestState,
				KingdomScenarioTransactionMarker.TransactionState, KingdomScenarioTransactionMarker.RealizedState }) Keys(capture, game, stateKey);
			Probe(fixture, zone, survey, game, now, now - 1L, null, original, capture);
			Probe(fixture, zone, survey, game, now, now + 1L, null, original, capture);
			Probe(fixture, zone, survey, game, now, now, -1L, original, capture);
			Probe(fixture, zone, survey, game, now, now, now + 1L, original, capture);
		}
		private static void Probe(KingdomSubsidenceNativeFixture fixture, Zone zone, KingdomSurvey survey,
			XRLGame game, long now, long supplied, long? injected, long original, Capture capture)
		{
			KingdomSystem system = fixture.System;
			Owner(fixture, zone, survey, game, now); capture.Check();
			Require(system.LastSubsidenceTick == original, "before raw clock");
			Healthy(system, zone, survey, now); Owner(fixture, zone, survey, game, now); capture.Check();
			if (injected.HasValue) system.LastSubsidenceTick = injected.Value;
			bool accepted = KingdomSubsidence.TryReckon(system, zone, survey, supplied, out string refusal);
			Owner(fixture, zone, survey, game, now); capture.Check();
			Require(system.LastSubsidenceTick == (injected ?? original), "after raw clock");
			Require(!accepted && !string.IsNullOrEmpty(refusal), "clock call did not refuse with reason");
			// Restoration is deliberately not in finally: failed proof retains injected evidence.
			if (injected.HasValue) system.LastSubsidenceTick = original;
			Owner(fixture, zone, survey, game, now); capture.Check();
			Require(system.LastSubsidenceTick == original, "restored raw clock");
			Healthy(system, zone, survey, now); Owner(fixture, zone, survey, game, now); capture.Check();
		}
		private static void Healthy(KingdomSystem system, Zone zone, KingdomSurvey survey, long now)
		{
			Require(KingdomSubsidenceStepRuntime.TryPassGuard(system, zone, survey, out Func<bool> exact, out Func<bool> sameSeat, out _)
				&& exact != null && sameSeat != null && exact() && sameSeat(), "baseline pass authority");
			Require(KingdomSubsidenceOptionRuntime.TryObserve(KingdomSubsidence.Enabled, now, out var observed, out _)
				&& observed != null && ReferenceEquals(observed.Game, The.Game) && ReferenceEquals(observed.System, system)
				&& ReferenceEquals(observed.City, system.City) && observed.Snapshot != null
				&& observed.Snapshot.Decision.Valid && observed.Snapshot.Decision.Action == KingdomElapsedOptionAction.Run,
				"baseline five-table option must run");
		}
		private static void Owner(KingdomSubsidenceNativeFixture fixture, Zone zone, KingdomSurvey survey, XRLGame game, long now)
		{
			Require(ReferenceEquals(The.Game, game) && game != null && game.TimeTicks == now
				&& ReferenceEquals(KingdomSubsidenceNativeFixture.LastAttempt, fixture)
				&& ReferenceEquals(fixture.Game, game) && ReferenceEquals(fixture.Zone, zone)
				&& ReferenceEquals(game.GetSystem<KingdomSystem>(), fixture.System)
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, zone) && The.Player?.Physics != null
				&& ReferenceEquals(The.Player.Physics._CurrentCell?.ParentZone, zone)
				&& ReferenceEquals(KingdomSurvey.ActiveFor(zone), survey) && ReferenceEquals(survey.Ground, zone), "world/fixture owner");
			Require(fixture.System.Founded && fixture.System.OwnedZone(zone.ZoneID)
				&& KingdomMaster.NewWorkAllowed(fixture.System) && KingdomSubsidence.Enabled, "settlement owner");
			var rung = KingdomSubsidenceRungNativeFixture.LastAttempt;
			Require(ReferenceEquals(fixture.Survey, survey) || rung != null && ReferenceEquals(rung.Base, fixture)
				&& ReferenceEquals(rung.Survey, survey) && rung.Now == now, "fixture survey owner");
			FieldInfo attempt = typeof(KingdomSubsidenceNativeClockSeed).GetField("Attempt", BindingFlags.NonPublic | BindingFlags.Static);
			object seed = attempt?.GetValue(null); Require(seed != null, "no retained seed attempt");
			foreach (var pair in new[] { new KeyValuePair<string, object>("Game", game),
				new KeyValuePair<string, object>("System", fixture.System), new KeyValuePair<string, object>("Fixture", fixture),
				new KeyValuePair<string, object>("City", fixture.System.City), new KeyValuePair<string, object>("Zone", zone),
				new KeyValuePair<string, object>("Survey", survey), new KeyValuePair<string, object>("Now", now),
				new KeyValuePair<string, object>("Player", The.Player), new KeyValuePair<string, object>("Realm", fixture.System.CurrentRealmId),
				new KeyValuePair<string, object>("Settlement", fixture.System.CurrentSettlementId) })
				Require(Same(Find(seed.GetType(), pair.Key).GetValue(seed), pair.Value), "seed owner " + pair.Key);
			IList<string> script = null;
			Require(KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out _)
				&& plan.Key == "founding-first-city" && plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.Committed
				&& KingdomScenarioScript.TryRead(out script, out _) && script.Count == 3
				&& script[0] == "stagedigest" && script[2] == "stagedigest", "stamped committed scenario");
			string[] verbs = { KingdomSubsidenceNativeProvider.Verb, KingdomSubsidenceRungNativeProvider.Verb,
				KingdomScenarioSaveProvider.Verb, KingdomSubsidenceRungSaveProvider.Verb };
			string[] receipts = { KingdomSubsidenceNativeProvider.Receipt, KingdomSubsidenceRungNativeProvider.Receipt,
				KingdomScenarioSaveProvider.Receipt, KingdomSubsidenceRungSaveProvider.Receipt };
			int matches = 0;
			for (int i = 0; i < verbs.Length; i++)
				if (script[1] == verbs[i] || i == 1 && KingdomSubsidenceRungNativeProvider.ExactScript(script[1])) { Require(KingdomScenarioDurableState.ProvesExactText(receipts[i], "intent"), "provider intent"); matches++; }
				else Require(!KingdomNativeRegressionContext.HasAnyState(game, receipts[i]), "foreign provider receipt");
			Require(matches == 1, "exactly one provider");
		}
		private static void Keys(Capture capture, XRLGame game, string key)
		{
			foreach (IDictionary table in new IDictionary[] { game.StringGameState, game.IntGameState,
				game.Int64GameState, game.BooleanGameState, game.ObjectGameState })
			{
				Require(table != null, "state table absent"); bool present = table.Contains(key);
				object value = present ? table[key] : null;
				Require(value == null || Atom(value.GetType()), "unknown key value type");
				capture.Checks.Add(() => Require(table.Contains(key) == present && (!present || Same(table[key], value)), "five-table key"));
			}
		}
		private static FieldInfo Find(Type type, string name)
		{
			for (Type current = type; current != null; current = current.BaseType)
			{
				FieldInfo field = current.GetField(name, Fields | BindingFlags.DeclaredOnly);
				if (field != null) return field;
			}
			throw new InvalidOperationException("Native clock probe missing field " + type.Name + "." + name);
		}
		private static bool Atom(Type type) { return type == typeof(string) || type == typeof(int)
			|| type == typeof(long) || type == typeof(bool) || type == typeof(ulong) || type.IsEnum; }
		private static bool Same(object a, object b)
		{
			if (a == null || b == null) return ReferenceEquals(a, b);
			Type type = a.GetType(); if (type != b.GetType()) return false;
			if (Atom(type)) return a.Equals(b);
			if (!type.IsValueType) return ReferenceEquals(a, b);
			Require(type.Name == "Slot" && type.Namespace == "XRL.Collections"
				&& type.GetFields(Fields | BindingFlags.DeclaredOnly).Length == 5, "unknown raw value type");
			foreach (string name in new[] { "Hash", "Next", "Key", "Value", "Strict" })
				if (!Same(Find(type, name).GetValue(a), Find(type, name).GetValue(b))) return false;
			return true;
		}
		private static string Schema(Type type)
		{
			if (type == typeof(KingdomCityBook)) return "DistanceCache SchemaVersion RulesVersion SettlementId ProcessedThroughTick "
				+ "WaterLevel WaterCapacity FoodLevel FoodCapacity MaterialsLevel MaterialsCapacity ZoneIds ZoneDistrictCodes ZoneLastReadTicks "
				+ "ZoneWaterLevels ZoneWaterCapacities ZoneFoodLevels ZoneFoodCapacities ZoneMaterialsLevels ZoneMaterialsCapacities ZoneRoofs "
				+ "ZoneDefences ZoneWaterCarries ZoneFoodCarries ZoneOwedWater ZoneOwedFood ZoneOwedMaterials WorkIds WorkZoneIds WorkAnchorsX "
				+ "WorkAnchorsY WorkDesignKeys WorkConditions WorkCrews WorkRanThroughTicks WorkKinds WorkStages WorkProgress WorkNextTicks "
				+ "ResidentIds ResidentNames ResidentOrigins ResidentOriginCodes ResidentCreedCodes ResidentKeptCreeds ResidentArrivedTicks "
				+ "ResidentArrived ResidentHomeWorkIds ResidentJobWorkIds ResidentJobRoles ResidentDayShapes ResidentStandings ResidentCauses "
				+ "ResidentBoundZoneIds ResidentRoofStanding ResidentRoofTicks ResidentRoofWarnedTicks ResidentCreedStanding ResidentCreedTicks "
				+ "ResidentCreedWarnedTicks ResidentCreedToward ResidentCreedChannels ClockKinds ClockNextDueTicks ClockOrdinals ToldKinds ToldTicks "
				+ "ToldSubjectsA ToldSubjectsB ToldPlaceZoneIds ToldOutcomes AmbientKey AmbientDayOrdinal RegardKey LastFestivalTick PilgrimLoudness "
				+ "PilgrimState PilgrimSequence PilgrimCauseTick PilgrimCause PilgrimObjectId PilgrimName PilgrimPlaceName PilgrimGreeted OfficeEpithet "
				+ "LastExtensionTick ExtensionHappeningCursors ExtensionModel HappeningModel SubsidenceModel SubsidenceReadFailed RiteBlocked NamedCook AssentingMoot";
			if (type == typeof(KingdomBindingRegistry)) return "Keys Kinds ZoneIds ObjectIds MintedTicks";
			if (type == typeof(KingdomLedger)) return "Fetched UpkeepDrawn ArrivalCost Delivered Harvested Foraged RationsDrawn Milled HarvestLost Plundered Arrivals Departures Notes BrinkLines ExpeditionLines";
			if (type == typeof(KingdomResidentDepartureOperation)) return "Version Phase Revision OperationId RealmId SettlementId ResidentId BodyObjectId ZoneId ResidentName Origin PreparedTick DeparturesBefore Chronicled ChronicleLine LedgerLine Cause PriorCook PriorOffice PriorPolity PolityConclusionRef AuthorizationKind AuthorizationEventId AuthorizationOwnerObjectId AuthorizationCauseDigest";
			if (type == typeof(KingdomNamedCookReceipt)) return "Version Phase Generation RealmId SettlementId SettlementName ResidentId ResidentName BodyObjectId RecipeId RecipeDisplayName EffectId GraphFingerprint DesignatedTick ReleasedTick Fault";
			if (type == typeof(KingdomAssentingMootReceipt)) return "Version Phase Generation RealmId SettlementId SettlementName ZoneId BuildingObjectId LotId AuthorityId MembershipFingerprint BaselineHitpoints Strength AssentResidentIds AssentResidentNames AssentBodyObjectIds ExemptResidentIds ExemptResidentNames ExemptBodyObjectIds PreparedTick AppliedTick SuspendedTick SuspendedReason Fault";
			if (type == typeof(r_KingdomCitizenship)) return "ReceiptVersion Phase PriorKind PriorValue AppliedValue OwnerRealmId OwnerSettlementId FactionId BodyObjectId EnrollmentReason RemovalReason AppliedTick RemovedTick NoticePublished Fault";
			throw new InvalidOperationException("Native clock probe unknown record schema");
		}
		private static void Require(bool condition, string label)
		{
			if (!condition) throw new InvalidOperationException("Native clock captured-state proof failed: " + label);
		}
	}
}
