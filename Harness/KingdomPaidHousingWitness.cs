using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomPaidHousingWitness
	{
		internal static GameObject Storage;
		internal static Cell StorageCell;
		private static string Contents, SourceId;
		internal static void Capture(GameObject work, Zone zone)
		{
			Require(KingdomArchitectureStamper.TryExactAnchoredComponent(work, zone, "storage", out Storage,
				out string failure) && Storage.Inventory != null, "home storage absent: " + failure);
			StorageCell = Storage.CurrentCell; SourceId = work.IDIfAssigned;
			var sentinel = GameObject.Create(KingdomMaterials.BlueprintFor(KingdomMaterial.Brush));
			Require(sentinel != null && ReferenceEquals(Storage.Inventory.AddObject(sentinel, null, Silent: true, NoStack: true), sentinel)
				&& sentinel.InInventory == Storage, "synthetic contents sentinel custody failed");
			Contents = ContentDigest(Storage);
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
			Require(KingdomQuickstartSettlementChecks.Observe(game, zone, game.GetSystem<KingdomSystem>(), "paid-housing-complete",
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
			Require(KingdomArchitectureStamper.TryExactAnchoredComponent(home, zone, "storage", out var store, out failure)
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
		private static void Require(bool value, string failure) => KingdomPaidHousingNativeProvider.Require(value, failure);
	}
}
