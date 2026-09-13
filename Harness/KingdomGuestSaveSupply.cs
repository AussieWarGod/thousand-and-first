using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Recover a real exhausted Quickstart store using only existing carried water.</summary>
	internal static class KingdomGuestSaveSupply
	{
		private const int Drams = 8;
		private static bool Attempted;
		private static void Require(bool value, string reason) => KingdomGuestActionsNativeProvider.Require(value, reason);

		internal static string Run(XRLGame game)
		{
			Require(!Attempted && !KingdomScenarioSaveFiles.LoadPresent(), "guest supply repeated or entered on load");
			Attempted = true;
			var player = The.Player;
			var zone = player.CurrentZone;
			var system = game.GetSystem<KingdomSystem>();
			Require(KingdomQuickstartRules.TryDecode(game.GetStringGameState(KingdomQuickstartRules.ReceiptState),
				out var receipt), "genuine Quickstart receipt absent");
			GameObject cask = null;
			foreach (GameObject item in zone.GetObjects())
				if (item.IDIfAssigned == receipt.WaterObjectId)
				{ Require(cask == null, "duplicate receipted cask"); cask = item; }
			var water = cask?.GetPart<LiquidVolume>();
			Require(GameObject.Validate(cask) && cask.Blueprint == "r_KingdomCaskRack"
				&& cask.CurrentCell?.X == KingdomQuickstartRules.WaterCellX
				&& cask.CurrentCell?.Y == KingdomQuickstartRules.WaterCellY
				&& cask.GetIntProperty("KingdomStores") == 1 && !cask.HasPart("LiquidProducer")
				&& water != null && water.Volume == 0 && water.MaxVolume == 64
				&& KingdomGrowth.CountStoredWater(zone) == 0, "actual starter store has not exhausted its water");
			GameObject donor = null;
			foreach (GameObject item in player.Inventory.Objects)
				if (GameObject.Validate(item) && item.InInventory == player
					&& KingdomLiquids.HasFreshWater(item.GetPart<LiquidVolume>()) && item.GetPart<LiquidVolume>().Volume >= Drams)
				{ donor = item; break; }
			Require(donor != null, "founder lacks eight carried drams; no supplies will be created");
			var liquid = donor.GetPart<LiquidVolume>();
			int carriedBefore = liquid.Volume;
			string authority = KingdomGuestSaveWitness.CaptureTransferAuthority(game);
			int moves = Walk(player, cask.CurrentCell);
			Require(KingdomGuestSaveWitness.CaptureTransferAuthority(game) == authority
				&& liquid.Volume == carriedBefore && water.Volume == 0, "walk changed guest authority or supplies");
			var invariant = new Invariant(game, zone, system);
			Require(KingdomData.TryGetBuilding(KingdomQuickstartLifecycleSteps.BuildKey, out var entry), "fire design absent");
			Require(KingdomPlots.TryQuoteCommission(system, zone, entry, null, KingdomPlotRules.PlotSize.None,
				out var quote, out string failure) && quote.WaterDrams == 2, "shortage quote differs: " + failure);
			Require(KingdomMaterials.CanPay(zone, entry.Key, out failure), "shortage must have payable materials: " + failure);
			Require(!KingdomCommission.Commission(system, entry.Key, null, KingdomPlotRules.PlotSize.None, quote,
				out failure) && !string.IsNullOrEmpty(failure), "unfunded commission did not refuse");
			invariant.Verify();
			Require(liquid.Volume == carriedBefore && water.Volume == 0 && KingdomGrowth.CountStoredWater(zone) == 0,
				"refused commission debited water");
			Require(KingdomScenarioJournal.Append("guest-save-shortage", true,
				"stored=0; required=2; commission-refused=true; authority-unchanged=true; materials-unchanged=true; water-debit=0") == null,
				"shortage journal unavailable");
			Require(player.CurrentZone == zone && Adjacent(player.CurrentCell, cask.CurrentCell)
				&& donor.InInventory == player && player.Inventory.Objects.Contains(donor)
				&& KingdomLiquids.HasFreshWater(liquid), "physical pour context changed");
			water.MixWith(liquid, PouredFrom: donor, Amount: Drams);
			Require(liquid.Volume == carriedBefore - Drams && water.Volume == Drams
				&& (liquid.Volume == 0 || KingdomLiquids.HasFreshWater(liquid)) && KingdomLiquids.HasFreshWater(water)
				&& liquid.Volume + water.Volume == carriedBefore && KingdomGrowth.CountStoredWater(zone) == Drams
				&& donor.InInventory == player && ReferenceEquals(donor.GetPart<LiquidVolume>(), liquid)
				&& ReferenceEquals(cask.GetPart<LiquidVolume>(), water), "physical water transfer did not conserve exact volumes and owners");
			invariant.Verify();
			return "native-guest-save supply=carried-water; amount=" + Drams + "; donor=" + donor.IDIfAssigned
				+ "; cask=" + cask.IDIfAssigned + "; donor-before=" + carriedBefore + "; donor-after=" + liquid.Volume
				+ "; store-before=0; store-after=" + water.Volume + "; moves=" + moves
				+ "; adjacent=true; conserved=true; authority-unchanged=true; materials-unchanged=true; world-repair=false";
		}

		private static bool Adjacent(Cell a, Cell b) => a != null && b != null && a.ParentZone == b.ParentZone
			&& Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) == 1;

		private static int Walk(GameObject player, Cell target)
		{
			Cell start = player.CurrentCell, end = null;
			var previous = new Dictionary<Cell, Cell> { [start] = null };
			var queue = new Queue<Cell>(); queue.Enqueue(start);
			int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
			while (queue.Count > 0)
			{
				Cell cell = queue.Dequeue();
				if (Adjacent(cell, target)) { end = cell; break; }
				for (int i = 0; i < 4; i++)
				{
					int x = cell.X + dx[i], y = cell.Y + dy[i];
					if (x < 1 || x > 78 || y < 1 || y > 23) continue;
					Cell next = start.ParentZone.GetCell(x, y);
					if (previous.ContainsKey(next) || next.HasOpenLiquidVolume() || !next.IsPassable(player)) continue;
					previous.Add(next, cell); queue.Enqueue(next);
				}
			}
			Require(end != null, "no walkable route to receipted cask");
			var path = new Stack<Cell>();
			for (Cell cell = end; cell != start; cell = previous[cell]) path.Push(cell);
			int moves = path.Count;
			Require(moves <= 80, "cask walk exceeds bounded scenario route");
			while (path.Count > 0)
			{
				Cell next = path.Pop(), current = player.CurrentCell;
				string direction = next.X < current.X ? "W" : next.X > current.X ? "E" : next.Y < current.Y ? "N" : "S";
				Require(player.Move(direction, AllowDashing: false, DoConfirmations: false)
					&& ReferenceEquals(player.CurrentCell, next) && ReferenceEquals(The.Player, player), "ordinary cask walk refused or changed owner");
			}
			return moves;
		}

		private sealed class Invariant
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly KingdomSystem System;
			private readonly string Authority, Registry;
			private readonly long Tick, Turns, Actions, PlayerActions, Heartbeat;
			private readonly int Dry, Upkeep, Departures;
			private readonly Dictionary<GameObject, KingdomQuickstartBuildCensus.StockSnapshot> Stores =
				new Dictionary<GameObject, KingdomQuickstartBuildCensus.StockSnapshot>();
			internal Invariant(XRLGame game, Zone zone, KingdomSystem system)
			{
				Game = game; Zone = zone; System = system;
				Authority = KingdomGuestSaveWitness.CaptureTransferAuthority(game);
				Registry = game.GetStringGameState(KingdomConstruction.RegistryStateKey);
				Tick = game.TimeTicks; Turns = game.Turns; Actions = game.ActionTicks; PlayerActions = game.PlayerActionTicks;
				Heartbeat = system.LastHeartbeatTick; Dry = system.DryStreak; Upkeep = system.Ledger.UpkeepDrawn; Departures = system.Ledger.Departures;
				foreach (GameObject store in zone.GetObjects())
					if (store.Inventory != null && KingdomMaterials.IsStockpile(store))
					{
						Require(KingdomQuickstartBuildCensus.TakeStock(zone, store, false, out var stock, out string failure), failure);
						Stores.Add(store, stock);
					}
				Require(Stores.Count > 0, "dedicated material stockpile absent");
				Require(Registry != null, "construction authority absent");
			}
			internal void Verify()
			{
				Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(The.Player.CurrentZone, Zone)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && !KingdomSurvey.HasBoundPass
					&& KingdomGuestSaveWitness.CaptureTransferAuthority(Game) == Authority
					&& Game.GetStringGameState(KingdomConstruction.RegistryStateKey) == Registry
					&& Game.TimeTicks == Tick && Game.Turns == Turns && Game.ActionTicks == Actions && Game.PlayerActionTicks == PlayerActions
					&& System.LastHeartbeatTick == Heartbeat && System.DryStreak == Dry
					&& System.Ledger.UpkeepDrawn == Upkeep && System.Ledger.Departures == Departures, "shortage or refill changed authority/clocks/accounting");
				int count = 0;
				foreach (GameObject store in Zone.GetObjects())
					if (store.Inventory != null && KingdomMaterials.IsStockpile(store))
					{
						count++;
						Require(Stores.TryGetValue(store, out var before), "dedicated material store added");
						Require(KingdomQuickstartBuildCensus.TakeStock(Zone, store, false, out var after, out string failure), failure);
						Require(KingdomQuickstartBuildCensus.SameStockpile(before, after, out failure) && before.Rows.Length == after.Rows.Length, failure);
						for (int i = 0; i < before.Rows.Length; i++)
							Require(ReferenceEquals(before.Rows[i].Object, after.Rows[i].Object) && before.Rows[i].Count == after.Rows[i].Count,
								"refused commission or refill changed material rows");
					}
				Require(count == Stores.Count, "dedicated material store removed");
			}
		}
	}
}
