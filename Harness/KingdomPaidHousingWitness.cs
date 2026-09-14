using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomPaidHousingWitness
	{
		internal static GameObject Storage;
		internal static Cell StorageCell;
		private static string Contents, SourceId;
		internal static void Capture(GameObject work, Zone zone)
		{
			Require(KingdomArchitectureStamper.TryExactAnchoredComponent(work, zone, "fixture:storage", out Storage,
				out string failure) && Storage.Inventory != null, "home storage absent: " + failure);
			StorageCell = Storage.CurrentCell; SourceId = work.IDIfAssigned;
			var sentinel = GameObject.Create(KingdomMaterials.BlueprintFor(KingdomMaterial.Brush));
			Require(sentinel != null && ReferenceEquals(Storage.Inventory.AddObject(sentinel, null, Silent: true, NoStack: true), sentinel)
				&& sentinel.InInventory == Storage, "synthetic contents sentinel custody failed");
			Contents = ContentDigest(Storage);
			ProvePhysicalRestoration(work, zone);
		}
		private static void ProvePhysicalRestoration(GameObject work, Zone zone)
		{
			Require(KingdomArchitectureStamper.TryVerifyComplete(work, zone, out string failure), failure);
			for (int probe = 0; probe < 3; probe++)
			{
				string token = Storage.GetStringProperty(KingdomArchitectureStamper.ComponentTokenProperty);
				try
				{
					if (probe < 2)
					{
						Storage.CurrentCell.RemoveObject(Storage);
						if (probe == 1) zone.GetCell(StorageCell.X + 1, StorageCell.Y).AddObject(Storage, NoStack: true);
					}
					else Storage.SetStringProperty(KingdomArchitectureStamper.ComponentTokenProperty, "wrong-test-token");
					Require(!KingdomArchitectureStamper.TryVerifyComplete(work, zone, out failure)
						&& !string.IsNullOrEmpty(failure), "physical mismatch did not refuse: " + probe);
					Require(!work.HasStringProperty(KingdomArchitectureStamper.FaultProperty)
						&& !work.HasIntProperty(KingdomArchitectureStamper.FaultProperty), "physical mismatch poisoned intact owner authority");
				}
				finally
				{
					Storage.SetStringProperty(KingdomArchitectureStamper.ComponentTokenProperty, token);
					if (Storage.CurrentCell != StorageCell)
					{
						Storage.CurrentCell?.RemoveObject(Storage);
						StorageCell.AddObject(Storage, NoStack: true);
					}
				}
				Require(KingdomArchitectureStamper.TryVerifyComplete(work, zone, out failure)
					&& ContentDigest(Storage) == Contents, "exact physical restoration failed: " + failure);
			}
			Require(KingdomScenarioJournal.Append("paid-housing-physical-probes", true,
				"missing=refused; moved=refused; wrong-token=refused; restored=exact; owner-authority=unchanged; contents=retained") == null,
				"physical restoration journal unavailable");
		}
		internal static string Complete()
		{
			var game = The.Game;
			var zone = The.Player.CurrentZone;
			Require(ReferenceEquals(game, KingdomPaidHousingNativeProvider.Owner) && KingdomPaidHousingCatalogue.Changed,
				"conversion lost its changed catalogue scope");
			Require(KingdomPaidHousingFault.Injected && KingdomPaidHousingFault.Refused && KingdomPaidHousingFault.Outstanding,
				"actual handover did not witness the controlled storage refusal and Outstanding retry");
			var paid = KingdomPaidHousingNativeProvider.Paid;
			Require(KingdomConstruction.TryFind(KingdomPaidHousingNativeProvider.JobId, out var job) && job != null,
				"paid conversion job absent");
			Require(job.Phase == KingdomConstructionPhase.Complete && job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled,
				"conversion unfinished: phase=" + job.Phase + "; reason=" + job.Failure);
			Require(KingdomConstruction.FindExactId(zone, job.OutputId, out var home) == KingdomPhysicalLookupState.Exact,
				"converted home absent or ambiguous");
			Verify(home, zone, job, paid.WaterDrams, new KingdomMaterialDebitCost(paid.Materials).ToClaimString(),
				KingdomPaidHousingNativeProvider.After.SnapshotHash, Storage.IDIfAssigned, Contents);
			Require(job.SubjectId == SourceId && home.IDIfAssigned != SourceId && Storage.CurrentCell == StorageCell,
				"conversion changed retained storage cell or predecessor identity");
			Require(ObserveCohort(game, zone, game.GetSystem<KingdomSystem>(), "complete",
				out string failure), failure);
			string wire = string.Join("\n", new[] { "taf-paid-housing-v1", game.GameID, zone.ZoneID, job.Id,
				home.IDIfAssigned, SourceId, KingdomPaidHousingNativeProvider.After.SnapshotHash,
				Storage.IDIfAssigned, Contents, job.Claims.MaterialSpent });
			Require(game.GetStringGameState(KingdomPaidHousingNativeProvider.StateKey) == null, "paid housing witness already exists");
			game.SetStringGameState(KingdomPaidHousingNativeProvider.StateKey, wire);
			Require(KingdomScenarioDurableState.ProvesExactText(KingdomPaidHousingNativeProvider.StateKey, wire), "paid housing witness not exact");
			KingdomPaidHousingCatalogue.Restore();
			Require(!KingdomPaidHousingCatalogue.Changed && !KingdomSurvey.HasBoundPass, "catalogue or survey scope leaked");
			return "paid-housing completed; job=" + job.Id + "; water=7; same-material-claim=true"
				+ "; storage-and-contents=retained; controlled-retry=true; founders=retained; catalogue-restored=true";
		}
		internal static void VerifyLoaded(XRLGame game)
		{
			KingdomPaidHousingNativeProvider.RequireScript();
			Require(KingdomScenarioLoadEntry.Armed && KingdomScenarioLoadEntry.LifecycleSnapshot != null,
				"paid housing proof requires separate sealed lifecycle load");
			string wire = game.GetStringGameState(KingdomPaidHousingNativeProvider.StateKey);
			Require(wire != null && wire.Length < 10000
				&& KingdomScenarioDurableState.ProvesExactText(KingdomPaidHousingNativeProvider.StateKey, wire), "saved housing witness absent");
			string[] fields = wire.Split('\n');
			var zone = The.Player.CurrentZone;
			Require(fields.Length == 10 && fields[0] == "taf-paid-housing-v1" && fields[1] == game.GameID
				&& fields[2] == zone.ZoneID, "saved housing witness owner differs");
			Require(KingdomConstruction.TryFind(fields[3], out var job) && job != null && job.SubjectId == fields[5]
				&& job.OutputId == fields[4], "loaded paid housing endpoints differ");
			Require(KingdomConstruction.FindExactId(zone, fields[4], out var home) == KingdomPhysicalLookupState.Exact,
				"loaded converted home absent or ambiguous");
			Verify(home, zone, job, 7, fields[9], fields[6], fields[7], fields[8]);
			Require(!KingdomPaidHousingCatalogue.Changed && KingdomPaidHousingNativeProvider.Owner == null,
				"cold process inherited a warm catalogue fixture");
			Require(KingdomScenarioJournal.Append("paid-housing-loaded", true,
				"same-paid-job=true; same-frozen-home=true; storage-and-contents=retained; cold-process=true; catalogue-scope=absent") == null,
				"housing cold-load journal unavailable");
		}
		private static void Verify(GameObject home, Zone zone, KingdomConstructionJob job, int water,
			string materials, string hash, string storeId, string contents)
		{
			Require(KingdomMaterialDebitCost.TryParseClaim(materials, out var claim)
				&& KingdomQuickstartBuildClaims.CleanFirstPayment(job.Claims, water, claim), "paid conversion was repriced or charged twice");
			Require(job.Route == KingdomConstructionRoute.Improvement && job.TargetKey == "hutyard"
				&& job.Phase == KingdomConstructionPhase.Complete && job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
				&& KingdomConstruction.Owns(The.Game.GetSystem<KingdomSystem>(), zone, job)
				&& KingdomConstruction.HasReceipt(home, job) && KingdomUpgrade.IsFunctionallyBuilt(home)
				&& KingdomUpgrade.DesignKeyOf(home) == "hutyard", "converted home is not the exact paid functional output");
			Require(KingdomArchitectureRuntime.TryRead(home, out var architecture, out string failure)
				&& architecture.SnapshotHash == hash && KingdomArchitectureStamper.TryVerifyComplete(home, zone, out failure), failure);
			Require(KingdomArchitectureStamper.TryExactAnchoredComponent(home, zone, "fixture:storage", out var store, out failure)
				&& store.IDIfAssigned == storeId && ContentDigest(store) == contents, "retained housing contents differ: " + failure);
		}
		internal static string ContentDigest(GameObject store)
		{
			Require(store?.Inventory != null, "storage inventory absent");
			var items = new List<string>();
			foreach (var item in store.Inventory.Objects)
			{
				Require(GameObject.Validate(item) && item.InInventory == store && item.CurrentCell == null, "housing content custody differs");
				items.Add(item.ID + ":" + item.Blueprint + ":" + item.Count);
			}
			items.Sort(StringComparer.Ordinal);
			return KingdomScenarioSaveFiles.HashText(string.Join("\n", items));
		}
		internal static bool ObserveCohort(XRLGame game, Zone zone, KingdomSystem system, string stage, out string failure)
		{
			failure = null;
			int citizens = 0, housed = 0, homes = 0, places = 0, floor = 0;
			try
			{
				KingdomPaidHousingNativeProvider.RequireScript();
				Require(KingdomQuickstartRules.TryDecode(game.GetStringGameState(KingdomQuickstartRules.ReceiptState), out var receipt)
					&& KingdomQuickstartRules.IsTerminal(receipt) && receipt.ZoneId == zone.ZoneID
					&& receipt.FoundersDisposition == KingdomQuickstartFoundersDisposition.Seeded, "founder receipt differs");
				Require(!KingdomSurvey.HasBoundPass, "cohort found a prior survey");
				Require(KingdomSurvey.TryBindLocalOperation(zone, system, out var scope,
					out string reason), "cohort survey unavailable");
				using (scope)
				{
					var survey = KingdomSurvey.ActiveFor(zone);
					Require(survey.TryBenefits(out var benefits, out reason), reason);
					var residents = new HashSet<string>(StringComparer.Ordinal);
					int converted = 0;
					for (int i = 0; i < KingdomQuickstartRules.ShelterLotCount; i++)
					{
						var expected = KingdomQuickstartRules.ShelterLot(i);
						GameObject home = null;
						foreach (var item in zone.GetObjects())
							if (KingdomUpgrade.IsFunctionallyBuilt(item) && KingdomPlots.TryReadRect(item, out var rect)
								&& rect.X1 == expected.X1 && rect.Y1 == expected.Y1 && rect.X2 == expected.X2 && rect.Y2 == expected.Y2)
							{
								Require(home == null, "duplicate home on founder reservation"); home = item;
							}
						Require(home != null, "founder reservation lacks completed home");
						string key = KingdomUpgrade.DesignKeyOf(home);
						Require(key == "tentrow" || key == "hutyard", "unexpected founder home design");
						if (key == "hutyard") converted++;
						var room = benefits.RoomReadingForRoot(home.IDIfAssigned);
						Require(room.SleepingRooms == 1 && room.SleepingPlaces == 3 && room.ExposedPlaces == 0
							&& room.UnusablePlaces == 0 && room.UsableFloorCells >= (key == "tentrow" ? 17 : 16),
							"home quality differs: key=" + key + "; rooms=" + room.SleepingRooms + "; places=" + room.SleepingPlaces
							+ "; exposed=" + room.ExposedPlaces + "; unusable=" + room.UnusablePlaces + "; floor=" + room.UsableFloorCells);
						homes++; places += room.SleepingPlaces; floor += room.UsableFloorCells;
						foreach (var body in KingdomLodging.ResidentsOf(zone, home))
							Require(residents.Add(body.IDIfAssigned), "one resident assigned to multiple homes");
					}
					Require(converted == 1, "expected exactly one completed conversion");
					var founders = new HashSet<string>(StringComparer.Ordinal);
					foreach (string id in receipt.FounderObjectIds)
					{
						Require(founders.Add(id), "founder receipt repeats an identity");
						Require(KingdomConstruction.FindExactId(zone, id, out var body) == KingdomPhysicalLookupState.Exact
							&& body.IsCreature && KingdomCitizenship.BelongsTo(system, body)
							&& KingdomResidents.TryResident(system.City, KingdomResidents.IdOf(body), out var resident)
							&& KingdomResidentRules.OnTheRoll(resident), "original founder lost identity or citizenship");
						citizens++;
						string key = KingdomLodging.HomeDesignKeyOf(zone, body);
						Require(residents.Contains(id) && (key == "tentrow" || key == "hutyard"), "original founder lost usable home");
						housed++;
					}
					Require(citizens == 4 && housed == 4, "original founder cohort differs");
				}
				Require(!KingdomSurvey.HasBoundPass, "cohort survey leaked");
			}
			catch (Exception error) { failure = error.Message; }
			Require(KingdomScenarioJournal.Append("paid-housing-cohort", failure == null, "stage=" + stage
				+ "; citizens=" + citizens + "; housed=" + housed + "; homes=" + homes + "; beds=" + places
				+ "; clear-floor=" + floor + "; synthetic-residents=false; forced-housing=false"
				+ (failure == null ? "" : "; failure=" + failure)) == null, "housing cohort journal unavailable");
			return failure == null;
		}
		private static void Require(bool value, string failure) => KingdomPaidHousingNativeProvider.Require(value, failure);
	}
}
