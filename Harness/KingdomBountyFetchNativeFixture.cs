using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	// Synthetic ground and stock; the posting, the reading, the acceptance, the carry and the
	// payment are all the shipped settlement pass running on real engine turns. Nothing here
	// writes a worker, a due tick, a transfer phase or a credit.
	internal sealed class KingdomBountyFetchNativeFixture
	{
		internal const string Sentinel = "Vinewafer";
		internal const string StoreBlueprint = "r_KingdomReservoir";
		/// <summary>Real drams in a real dedicated vessel; the shipped payout draws the price
		/// from these and nothing here writes a payment phase, a proved amount or a Paid value.</summary>
		internal const int StoredDrams = 240;
		internal static readonly string[] Carried = { "r_KingdomTimber", "r_KingdomCutStone" };
		internal readonly XRLGame Game;
		internal readonly Zone Zone;
		internal readonly List<GameObject> Owned = new List<GameObject>();
		internal readonly GameObject[] Residents = new GameObject[2];
		internal readonly GameObject[] Loads = new GameObject[2], Sentinels = new GameObject[2];
		internal readonly string[] LoadIds = new string[2], SentinelIds = new string[2];
		internal readonly int[] LoadCounts = new int[2], SentinelCounts = new int[2];
		internal GameObject Pile, Destination, Notice, Store;
		internal LiquidVolume StoreWater;
		internal KingdomSystem System;
		internal r_KingdomNotice Data;
		internal string PileId, DestinationId, NoticeId;
		internal long PostedTick;
		internal int Price;

		internal KingdomBountyFetchNativeFixture(XRLGame game, Zone zone)
		{ Game = game; Zone = zone; }

		internal KingdomSystem Build()
		{
			KingdomSystem system = System = Found();
			long tick = Game.TimeTicks;
			Enroll(system, tick);
			Pile = Create("Chest"); PlaceObject(Pile);
			Destination = Create("Chest"); PlaceObject(Destination);
			Require(Pile.Inventory != null && Destination.Inventory != null
				&& Pile.Inventory.Objects.Count == 0 && Destination.Inventory.Objects.Count == 0,
				"fresh fixture containers are not empty inventories");
			// Explicit synthetic dedication, exactly as a founder's own mark would leave it.
			Destination.SetIntProperty(KingdomMaterials.StockpileProperty, 1);
			Require(KingdomMaterials.IsStockpile(Destination) && !KingdomMaterials.IsStockpile(Pile),
				"dedication did not separate destination from pile");
			for (int i = 0; i < 2; i++)
			{
				Loads[i] = Stow(Pile, Carried[i], 3, out LoadIds[i], out LoadCounts[i]);
				Require(KingdomMaterials.TryOrdinaryMaterialOf(Loads[i], out _),
					"a carried stack is not ordinary settlement material");
				Sentinels[i] = Stow(Pile, Sentinel, 1, out SentinelIds[i], out SentinelCounts[i]);
				Require(!KingdomMaterials.TryOrdinaryMaterialOf(Sentinels[i], out _),
					"a sentinel classified as settlement material");
			}
			Require(Pile.Inventory.Objects.Count == 4, "fixture pile is not two loads and two sentinels");
			Fund();
			Stake(system);
			Require(Game.TimeTicks == tick, "fixture construction advanced the world clock");
			return system;
		}

		private KingdomSystem Found()
		{
			Require(KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out string failure), failure);
			string name = null;
			foreach (var step in plan.Steps) if (step.Verb == KingdomScenarioVerb.FoundFirstCity)
				Require(name == null && step.Arguments.TryGetValue("CityName", out name)
					&& !string.IsNullOrEmpty(name), "founding name missing or repeated");
			Require(name != null && KingdomScenarioFoundingStep.TryProvePreconditions(Zone, name, out failure), failure);
			Require(KingdomScenarioTransactionMarker.TryBegin(out failure), failure);
			Require(KingdomScenarioFoundingStep.TryFound(Zone, name, out _, out failure), failure);
			Require(KingdomScenarioTransactionMarker.TryCommit(out failure), failure);
			KingdomSystem system = Game.GetSystem<KingdomSystem>();
			Require(system != null && system.Founded, "founding did not publish a founded realm");
			return system;
		}

		// The realm's own funded store. This founding runs KingdomFoundingTransaction
		// .TryFoundFirstWithoutWater (KingdomScenarioFoundingStep.TryFound), and the founding basin
		// blueprint r_KingdomFirstBasin is stocked Volume="0", so the realm owns no drams at all.
		// Without this a posted price has nothing to be drawn from: the carry would complete and
		// then stall unpaid on the board, and the notice would never be retired.
		private void Fund()
		{
			Store = Create(StoreBlueprint);
			StoreWater = Store.GetPart<LiquidVolume>();
			Require(StoreWater != null && ReferenceEquals(StoreWater.ParentObject, Store)
				&& StoreWater.Volume == 0 && StoreWater.MaxVolume >= StoredDrams
				&& KingdomLiquids.CanReceiveFreshWater(StoreWater),
				"the fixture store is not an empty receivable vessel");
			Store.SetIntProperty("KingdomStores", 1);
			PlaceObject(Store);
			Require(KingdomLiquids.Fill(StoreWater, "water", StoredDrams) == StoredDrams
				&& StoreWater.Volume == StoredDrams && KingdomLiquids.HasFreshWater(StoreWater),
				"the fixture store did not receive its exact fresh water");
			Require(Store.GetIntProperty("KingdomStores") == 1
				&& ReferenceEquals(Store.CurrentZone, Zone)
				&& !KingdomMaterials.IsStockpile(Store) && StoredDrams > KingdomBountyRules.MaxPrice,
				"the funded store is not a dedicated vessel that can cover the posted price");
		}

		private void Enroll(KingdomSystem system, long tick)
		{
			for (int i = 0; i < Residents.Length; i++)
			{
				GameObject body = Residents[i] = Create("NPC");
				Require(body.Brain != null && body.Body != null && body.IsAlive && !body.IsPlayer()
					&& body.GetPart<r_KingdomCitizenship>() == null, "NPC lacks fresh citizenship shape");
				body.SetStringProperty("Species", "human");
				Require(KingdomCitizenship.TryEnroll(system, body,
					KingdomCitizenshipEnrollmentReason.Arrival, tick, out string failure), failure);
				Require(KingdomCitizenship.BelongsTo(system, body), "enrollment lost citizenship authority");
				string name = "bounty fixture settler " + (i + 1);
				body.GiveProperName(name, Force: true);
				body.SetStringProperty("KingdomName", name);
				body.SetStringProperty("KingdomOrigin", "native bounty fetch fixture");
				PlaceObject(body);
				Require(KingdomResidents.TryEnsureRow(system, body, "native bounty fetch fixture",
					null, tick, out var city, out int id) && ReferenceEquals(city, system.City) && id > 0,
					"real enrollment did not publish a row");
			}
			Require(KingdomResidents.OnRollCount(system) >= Residents.Length,
				"the fixture roster cannot read a notice");
		}

		// The one synthetic step: the staked notice's own durable posting fields, written exactly
		// as the founder's posting transaction writes them. Everything downstream of the stake is
		// the shipped pass.
		private void Stake(KingdomSystem system)
		{
			Cell cell = Clear();
			Notice = Create(KingdomBounty.NoticeBlueprint);
			r_KingdomNotice data = Data = Notice.GetPart<r_KingdomNotice>();
			Require(data != null, "the notice blueprint carries no notice part");
			// The loudest lawful price. The reader roll is a real per-day draw shaded by the
			// price, so a minimum-price notice can stand unread for many in-game days and the
			// sealed advance budget would decide the verdict instead of the carry.
			Price = KingdomBountyRules.MaxPrice;
			data.TaskCode = (int)BountyTask.Fetch;
			data.Price = Price;
			data.PostedTick = PostedTick = Game.TimeTicks;
			data.ScheduleVersion = 2;
			data.EventStreamId = KingdomBountyRules.NoticeEventStream(Notice.ID);
			data.LifecycleId = KingdomBountyRules.NoticeEventId(Notice.ID);
			data.AttemptScheduleExhausted = !KingdomBountyRules.TryFirstAttemptTick(
				data.PostedTick, out data.NextAttemptTick);
			data.Magnitude = LoadCounts[0] + LoadCounts[1];
			data.PostChronicleLine = KingdomBountyRules.PostedChronicle(
				KingdomPresentation.Rich(system.SeatName), BountyTask.Fetch, Price);
			data.PostMessageLine = "{{G|The notice is up.}} "
				+ KingdomBountyRules.NoticeText(BountyTask.Fetch, Price, null);
			data.PostZoneId = Zone.ZoneID;
			data.PostCellX = cell.X; data.PostCellY = cell.Y;
			data.PostPileCellX = Pile.CurrentCell.X; data.PostPileCellY = Pile.CurrentCell.Y;
			data.PostMessageState = (int)BountySinkDisposition.Pending;
			data.PostPhase = (int)BountyPostPhase.Bound;
			Require(ReferenceEquals(cell.AddObject(Notice), Notice), "notice placement substituted the object");
			Notice.MakeActive();
			NoticeId = Notice.IDIfAssigned;
			PileId = Pile.IDIfAssigned; DestinationId = Destination.IDIfAssigned;
			data.PileId = Pile.ID;
			Pile.SetStringProperty(KingdomBounty.FetchMarkProperty, Notice.ID);
			Require(Pile.GetStringProperty(KingdomBounty.FetchMarkProperty) == NoticeId
				&& data.PileId == PileId && !string.IsNullOrEmpty(NoticeId),
				"the fetch mark did not bind the exact notice");
			Require(!data.LifecycleQuarantined && data.TransferPhase == 0 && data.TransferredUnits == 0
				&& string.IsNullOrEmpty(data.WorkerName) && data.DueTick == 0L && !data.Done
				&& data.Paid == 0 && data.PaymentPhase == 0 && data.CompletionPhase == 0
				&& data.TerminalPhase == 0 && data.TakePhase == 0,
				"the staked notice already carries worker, transfer, payment or completion state");
		}

		private GameObject Stow(GameObject container, string blueprint, int wanted,
			out string id, out int count)
		{
			GameObject item = Create(blueprint);
			if (wanted > 1 && item.GetPart<Stacker>() != null) item.Count = wanted;
			Require(ReferenceEquals(container.Inventory.AddObject(item, Silent: true, NoStack: true), item),
				"fixture stow substituted the stowed object");
			id = item.IDIfAssigned; count = item.Count;
			Require(!string.IsNullOrEmpty(id) && count > 0 && item.InInventory == container
				&& item.CurrentCell == null, "fixture stow lacks exact container custody");
			return item;
		}

		private GameObject Create(string blueprint)
		{
			GameObject result = GameObject.Create(blueprint);
			Require(GameObject.Validate(result) && result.Blueprint == blueprint
				&& result.CurrentCell == null && result.InInventory == null && Owned.Count < 16,
				"factory returned foreign identity or custody: " + blueprint);
			Owned.Add(result);
			return result;
		}

		private void PlaceObject(GameObject body)
		{
			Cell target = Clear();
			Require(ReferenceEquals(target.AddObject(body, NoStack: true), body),
				"native placement substituted the object");
			Require(ReferenceEquals(body.CurrentCell, target) && body.InInventory == null,
				"placement lacks exclusive original custody");
		}

		private Cell Clear()
		{
			for (int y = 1; y < Zone.Height - 1; y++) for (int x = 1; x < Zone.Width - 1; x++)
			{
				Cell cell = Zone.GetCell(x, y);
				bool clear = cell.IsEmpty() && cell.IsPassable() && !cell.HasOpenLiquidVolume();
				foreach (GameObject row in cell.Objects) if (!GameObject.Validate(row) || row.IsCreature
					|| KingdomPlots.ReadObject(row) != KingdomPlotRules.GroundKind.Bare) clear = false;
				if (clear) return cell;
			}
			Require(false, "bounded empty native placement cell unavailable");
			return null;
		}

		private static void Require(bool value, string failure)
		{ KingdomBountyFetchNativeProvider.Require(value, failure); }
	}
}
