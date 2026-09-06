using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	// LoadGame fires the player's GameRestored immediately before AfterGameLoadedEvent.Send.
	// Observe here without rewriting Send's accesses to generic singleton static fields.
	[HarmonyPatch(typeof(GameObject), "FireEvent", new Type[] { typeof(string) })]
	internal static class KingdomScenarioLoadWitness
	{
		private static int Attempts;
		private static XRLGame WitnessedGame;
		private static readonly List<GameObject> Bodies = new List<GameObject>();
		private static string Failure;

		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static void Prefix(GameObject __instance, string __0)
		{
			if (!KingdomScenarioLoadEntry.Armed || __0 != "GameRestored") return;
			if (KingdomScenarioLoadEntry.QuickstartSnapshot != null)
			{
				if (ReferenceEquals(__instance, The.Player)) KingdomQuickstartLoadTest.BeforeActivation();
				return;
			}
			if (KingdomScenarioLoadEntry.RungSnapshot != null)
			{
				if (ReferenceEquals(__instance, The.Player)) KingdomSubsidenceRungLoadWitness.Prefix();
				return;
			}
			try
			{
				if (!ReferenceEquals(__instance, The.Player)) return;
				Attempts++;
				Check(Attempts == 1 && WitnessedGame == null, "saved-state witness fired more than once");
				Check(KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors,
					"primary reader did not complete exactly once without errors");
				KingdomScenarioSaveSnapshot snapshot = KingdomScenarioLoadEntry.Snapshot;
				XRLGame game = The.Game;
				Check(game != null && ReferenceEquals(The.Game, game) && game.GameID == snapshot.GameId
					&& game.TimeTicks == snapshot.Now && game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey,
						KingdomScenarioLoadEntry.SnapshotWire), "loaded game, clock, snapshot or autoonce field changed");
				KingdomSystem system = game.GetSystem<KingdomSystem>();
				Check(system != null && system.City.SubsidenceModel == snapshot.StepWire
					&& snapshot.StepWire.StartsWith("ss5:", StringComparison.Ordinal)
					&& system.City.HasValidSubsidenceStorage() && system.City.TryReadExact(out _, out _)
					&& system.Population == 49 && KingdomResidents.OnRollCount(system) == 49
					&& system.Stage == GrowthStage.City && system.Ledger.Departures == snapshot.LedgerDepartures
					&& KingdomSubsidenceStepCodec.TryDecode(snapshot.StepWire, out KingdomSubsidenceStepBook book)
					&& book.Sequence == 1 && book.Active != null && book.Active.Completed == 1 && book.Active.Quota == 5
					&& book.Active.DueTick == snapshot.Now && system.LastSubsidenceTick == book.Active.AnchorTick
					&& book.Active.PendingDepartureId == "" && KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture),
					"pre-activation loaded partial step differs from the exact saved before-state");
				Zone zone = game.ZoneManager.ActiveZone;
				Check(zone != null && zone.ZoneID == snapshot.ZoneId && ReferenceEquals(The.Player?.CurrentZone, zone)
					&& KingdomScenarioSaveFiles.Same(System.IO.Path.GetFullPath(game.GetCacheDirectory()),
						KingdomScenarioSaveFiles.SaveDirectory(KingdomScenarioSaveFiles.Root(), snapshot.GameId)),
					"loaded ground or rebased cache directory is not exact");
				KingdomScenarioSaveAuthorityChecks.VerifyExact(system, zone);
				HashSet<string> ids = new HashSet<string>(snapshot.ObjectIds, StringComparer.Ordinal) { snapshot.MissingObjectId };
				Check(KingdomPlots.TryCaptureGlobalLiveIds(ids, out Dictionary<string, GameObject> live)
					&& live.Count == 49 && !live.ContainsKey(snapshot.MissingObjectId), "loaded live-body census disagrees");
				for (int i = 0; i < 49; i++)
				{
					Check(live.TryGetValue(snapshot.ObjectIds[i], out GameObject body) && ExactBody(system, zone, body,
						snapshot.ResidentIds[i], snapshot.ObjectIds[i]), "loaded resident row/body/binding changed");
					Bodies.Add(body);
				}
				Check(KingdomResidents.DepartureCarriersAbsent(system, system.City, snapshot.MissingResidentId),
					"the already-departed resident returned in the saved authority");
				WitnessedGame = game;
				Check(KingdomScenarioJournal.Append("LOAD-PREACTIVATION", true,
					"exact ss5 bytes and 49 loaded bodies; one credit of five; original anchor unpaid; autoonce=true"
					+ "; before-AfterGameLoaded-handlers-and-zone-activation=true") == null, "pre-activation journal unavailable");
			}
			catch (Exception error)
			{
				Failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
				KingdomScenarioJournal.Append("LOAD-PREACTIVATION", false, Failure);
			}
		}

		internal static string VerifyRecovered(XRLGame Game, KingdomScenarioSaveSnapshot Snapshot)
		{
			Check(Failure == null && Attempts == 1 && ReferenceEquals(WitnessedGame, Game) && Bodies.Count == 49,
				"no explicit exact-save pre-activation witness: " + Failure);
			KingdomSystem system = Game.GetSystem<KingdomSystem>();
			Zone zone = Game.ZoneManager.ActiveZone;
			KingdomScenarioSaveAuthorityChecks.VerifyReconciled(system, zone);
			Check(Game.TimeTicks == Snapshot.Now && (system.Population == 49 || system.Population == 45),
				"unexpected post-load clock or population");
			bool activationRecovered = system.Population == 45;
			Check(activationRecovered || system.City.SubsidenceModel == Snapshot.StepWire,
				"activation changed the pending step without completing its remaining credits");
			KingdomSurvey survey = KingdomSurvey.Take(zone, system);
			using (survey.BindPass())
			{
				Check(KingdomSubsidenceStepRuntime.TryBeforePass(system, zone, survey, out string refusal), refusal);
				VerifyAfter(system, zone, Snapshot);
				string wire = system.City.SubsidenceModel;
				string[] official = system.ChronicleEntries.ToArray(), outsider = system.OutsiderEntries.ToArray();
				Check(KingdomSubsidenceStepRuntime.TryBeforePass(system, zone, survey, out refusal), refusal);
				VerifyAfter(system, zone, Snapshot);
				Check(system.City.SubsidenceModel == wire && Same(system.ChronicleEntries, official)
					&& Same(system.OutsiderEntries, outsider) && Game.TimeTicks == Snapshot.Now,
					"post-load same-tick retry replayed state or telling");
			}
			Check(KingdomScenarioJournal.Append("LOAD-RECOVERY", true,
				"remaining-four=proved; population=45; original-step-retired=true; replay=false; status="
				+ (KingdomSubsidenceStepRuntime.Status(system) == "" ? "clear" : "pending")
				+ "; activation-already-recovered=" + activationRecovered.ToString().ToLowerInvariant()) == null,
				"post-load recovery journal unavailable");
			return activationRecovered ? "native-zone-activation" : "explicit-production-prepass";
		}

		private static void VerifyAfter(KingdomSystem System, Zone Zone, KingdomScenarioSaveSnapshot Snapshot)
		{
			Check(System.Founded && System.Population == 45 && KingdomResidents.OnRollCount(System) == 45 && System.Stage == GrowthStage.City
				&& System.Ledger.Departures == Snapshot.LedgerDepartures + 4 && System.LastSubsidenceTick == Snapshot.Now
				&& KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture)
				&& KingdomSubsidenceStepCodec.TryDecode(System.City.SubsidenceModel, out KingdomSubsidenceStepBook book)
				&& book.Active == null && book.Sequence == 1 && book.LastRetiredTick == Snapshot.Now
				&& book.BatchModel == KingdomSubsidenceBatchRules.None && book.FailureModel == KingdomSubsidenceReportArchive.None,
				"loaded step did not retire its original remaining four credits exactly");
			int gone = 0;
			for (int i = 0; i < Bodies.Count; i++)
			{
				if (!GameObject.Validate(Bodies[i]))
				{
					Check(KingdomResidents.DepartureCarriersAbsent(System, System.City, Snapshot.ResidentIds[i]),
						"loaded departure left a resident row/binding");
					gone++;
				}
				else Check(ExactBody(System, Zone, Bodies[i], Snapshot.ResidentIds[i], Snapshot.ObjectIds[i]),
					"loaded survivor lost exact row/body/binding");
			}
			Check(gone == 4 && KingdomSubsidenceStepRuntime.Status(System) == "", "four physical removals or clear status unproved");
		}

		private static bool ExactBody(KingdomSystem System, Zone Zone, GameObject Body, int Id, string ObjectId)
		{
			return GameObject.Validate(Body) && Body.IsAlive && Body.CurrentZone == Zone && Body.IDIfAssigned == ObjectId
				&& !Body.IsPlayerLed() && KingdomCitizenship.BelongsTo(System, Body) && KingdomResidents.IdOf(Body) == Id
				&& System.City.TryResidentRow(Id, out _) && System.Bindings.TryReadExact(out KingdomBindingTable bindings, out _)
				&& bindings.TryGet(Id, KingdomBindingKind.Resident, out KingdomBinding binding)
				&& binding.ObjectId == ObjectId && binding.ZoneId == Zone.ZoneID;
		}

		private static bool Same(List<string> Current, string[] Before)
		{
			if (Current.Count != Before.Length) return false;
			for (int i = 0; i < Before.Length; i++) if (Current[i] != Before[i]) return false;
			return true;
		}

		private static void Check(bool Condition, string Detail)
		{
			KingdomScenarioSaveFiles.Require(Condition, Detail ?? "native load witness refused");
		}
	}
}
