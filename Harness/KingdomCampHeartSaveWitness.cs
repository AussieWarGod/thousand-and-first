using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal static KingdomCampHeartSaveSnapshot CaptureSaveWitness(XRLGame Game, Zone Zone)
		{
			Frame frame = ObserveCamp(Game, Zone);
			var units = frame.ContentUnits(out _);
			var brush = new List<KingdomCampHeartNativeCensus.Unit>();
			string timber = null;
			foreach (var unit in units)
			{
				Require(unit.RawCount == 1, "saved camp materials are not individual physical units");
				if (unit.Blueprint == KingdomMaterials.BlueprintFor(KingdomMaterial.Brush)) brush.Add(unit);
				else
				{
					Require(timber == null && unit.Blueprint == KingdomMaterials.BlueprintFor(KingdomMaterial.Timber),
						"saved camp contains an unexpected material or another timber unit");
					timber = unit.Id;
				}
			}
			Require(brush.Count == SavedBrushUnits && timber != null, "saved camp lacks 21 brush and one timber: store="
				+ frame.StoreId + "; brush=" + brush.Count + "; timber=" + (timber ?? "absent")
				+ "; raw=" + KingdomCampHeartNativeCensus.Describe(units));
			return new KingdomCampHeartSaveSnapshot(Game.GameID, frame.System.RealmId,
				KingdomConstruction.OwnerOf(frame.System), Zone.ZoneID, frame.HeartId, frame.JobId,
				frame.StoreId, frame.FireId, timber, KingdomCampHeartSaveSnapshotCodec.CustodyDigest(units),
				KingdomCampHeartSaveSnapshotCodec.CustodyDigest(brush), frame.TentJobId, frame.Heart.CurrentCell.X,
				frame.Heart.CurrentCell.Y, frame.StoreCell.X, frame.StoreCell.Y, frame.FireCell.X,
				frame.FireCell.Y, frame.Census().StoredWater, Game.Turns);
		}

		private static Frame ObserveCamp(XRLGame Game, Zone Zone)
		{
			Require(Game != null && ReferenceEquals(Game, The.Game) && Zone != null
				&& ReferenceEquals(Zone, The.ZoneManager?.ActiveZone), "camp is not the active game and zone");
			var frame = new Frame(Game, Zone) { System = Game.GetSystem<KingdomSystem>() };
			Require(frame.System != null && frame.System.Founded && frame.System.ClaimedZones.Contains(Zone.ZoneID),
				"camp has no founded settlement claiming this zone");
			frame.Heart = frame.StandingHeart();
			frame.HeartId = frame.Heart.IDIfAssigned;
			Require(KingdomPlots.HeartRung(Zone) == 2 && KingdomUpgrade.DesignKeyOf(frame.Heart) == SecondRungKey
				&& KingdomUpgrade.IsFunctionallyBuilt(frame.Heart) && frame.BasinCapacity(frame.Heart) == "48",
				"camp no longer has a functional rung-2 heart and 48-dram basin");
			Require(KingdomArchitectureStamper.TryExactAnchoredComponent(frame.Heart, Zone,
				StorageRole, out frame.Store, out string failure), failure ?? "camp store is not anchored");
			frame.StoreId = frame.Store.IDIfAssigned;
			frame.StoreCell = frame.Store.CurrentCell;
			frame.RequireStoreIdentity();
			frame.Fire = frame.FireIn(frame.Heart);
			Require(GameObject.Validate(frame.Fire), "camp fire is absent");
			frame.FireId = frame.Fire.IDIfAssigned;
			frame.FireCell = frame.Fire.CurrentCell;
			foreach (var item in new[] { frame.Heart, frame.Store, frame.Fire }) ExactGround(Zone, item);
			frame.JobId = frame.Heart.GetStringProperty(KingdomConstruction.ReceiptProperty);
			Require(!string.IsNullOrEmpty(frame.JobId), "standing heart has no construction receipt");
			Require(KingdomConstruction.TryRead(out var jobs, out failure), failure ?? "construction registry unreadable");
			int matching = 0;
			foreach (var job in jobs)
			{
				if (job?.Id != frame.JobId) continue;
				Require(++matching == 1 && job.OwnerKey == KingdomConstruction.OwnerOf(frame.System)
					&& job.ZoneId == Zone.ZoneID && job.TargetKey == SecondRungKey
					&& job.OutputId == frame.HeartId && job.Phase == KingdomConstructionPhase.Complete
					&& job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled && KingdomConstruction.HasReceipt(frame.Heart, job),
					"retained heart job is ambiguous, unfinished or detached from its standing output");
			}
			frame.TentJobId = PaidTent(frame, jobs);
			RequireBookRow(frame);
			return frame;
		}

		private static void ExactGround(Zone Zone, GameObject Item)
		{
			Require(GameObject.Validate(Item) && !string.IsNullOrEmpty(Item.IDIfAssigned)
				&& KingdomConstruction.FindExactId(Zone, Item.IDIfAssigned, out var exact) == KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exact, Item) && Item.Physics?._CurrentCell != null
				&& ReferenceEquals(Item.Physics._CurrentCell.ParentZone, Zone)
				&& Item.Physics._InInventory == null && Item.Physics._Equipped == null,
				"camp object lacks unique identity and exclusive physical ground custody");
		}

		private static void RequireBookRow(Frame Frame)
		{
			var book = Frame.System.City;
			Require(book?.WorkIds != null && book.WorkZoneIds != null && book.WorkDesignKeys != null
				&& book.WorkAnchorsX != null && book.WorkAnchorsY != null
				&& book.WorkZoneIds.Count == book.WorkIds.Count && book.WorkDesignKeys.Count == book.WorkIds.Count
				&& book.WorkAnchorsX.Count == book.WorkIds.Count && book.WorkAnchorsY.Count == book.WorkIds.Count,
				"camp city work columns are absent or torn");
			int wanted = Simulation.City.KingdomCityRules.StableId(Frame.HeartId), matching = 0;
			for (int i = 0; i < book.WorkIds.Count; i++)
			{
				if (book.WorkIds[i] != wanted) continue;
				Require(++matching == 1 && book.WorkZoneIds[i] == Frame.Zone.ZoneID
					&& book.WorkDesignKeys[i] == Frame.Heart.Blueprint
					&& book.WorkAnchorsX[i] == Frame.Heart.CurrentCell.X && book.WorkAnchorsY[i] == Frame.Heart.CurrentCell.Y,
					"camp city book names a different heart, design or anchor");
			}
			Require(matching == 1, "camp city book does not name the standing heart");
		}
	}
}
