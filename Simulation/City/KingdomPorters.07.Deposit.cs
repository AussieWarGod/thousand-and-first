using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomPorters
	{
		/// <summary>Resumes one exact receipt-bound deposit. Landed units remain unavailable until
		/// the cargo-zero row is durable; retry counts the same objects instead of minting substitutes.</summary>
		private static void Deposit(KingdomSystem system, Zone zone, GameObject body,
			r_KingdomPorter part, KingdomJobRow row, long timeTick)
		{
			Cell at = zone.GetCell(part.DestX, part.DestY);
			GameObject store = LarderAt(at);
			if (!GameObject.Validate(body) || body.Inventory == null
				|| !GameObject.Validate(store) || store.Inventory == null) return;
			List<GameObject> carried;
			List<GameObject> standing;
			int carriedCount;
			int standingCount;
			if (!TryPorterReceipts(body, row.JobId, out carried, out carriedCount)
				|| !TryPorterReceipts(store, row.JobId, out standing, out standingCount)
				|| carriedCount + standingCount != row.CargoAmount) return;
			for (int i = 0; i < carried.Count; i++)
			{
				GameObject item = carried[i];
				string id = item.IDIfAssigned;
				string blueprint = item.Blueprint;
				string failure;
				if (!KingdomOrdinaryFoodAuthority.TrySpendNow(item,
					KingdomOrdinaryFoodAuthority.PorterReceiptProperty, row.JobId, out failure)) return;
				try { body.Inventory.RemoveObject(item); }
				catch { }
				if (!GameObject.Validate(item) || item.InInventory != null || item.CurrentCell != null
					|| item.IDIfAssigned != id || item.Blueprint != blueprint || item.Count != 1
					|| !KingdomOrdinaryFoodAuthority.TrySpendNow(item,
						KingdomOrdinaryFoodAuthority.PorterReceiptProperty, row.JobId, out failure))
					return;
				try { store.Inventory.AddObject(item, Silent: true, NoStack: true); }
				catch { }
				KingdomSurvey.ObserveCurrentTopologyInActive(zone, store);
				if (!GameObject.Validate(item) || item.InInventory != store || item.CurrentCell != null
					|| item.IDIfAssigned != id || item.Blueprint != blueprint || item.Count != 1
					|| !KingdomOrdinaryFoodAuthority.TrySpendNow(item,
						KingdomOrdinaryFoodAuthority.PorterReceiptProperty, row.JobId, out failure))
					return;
			}
			if (!TryPorterReceipts(store, row.JobId, out standing, out standingCount)
				|| standingCount != row.CargoAmount) return;
			KingdomJobTable table;
			KingdomJobTable next;
			KingdomJobRow current;
			KingdomCityFault fault;
			if (!system.Jobs.TryRead(out table, out fault) || !table.TryGet(row.JobId, out current)
				|| current.CargoAmount != row.CargoAmount
				|| !table.TryReplace(current.WithCargoLanded(), out next, out fault)
				|| !system.Jobs.TryPublish(next, out fault)) return;
			for (int i = 0; i < standing.Count; i++)
			{
				string failure;
				if (KingdomOrdinaryFoodAuthority.TrySpendNow(standing[i],
					KingdomOrdinaryFoodAuthority.PorterReceiptProperty, row.JobId, out failure))
					standing[i].RemoveIntProperty(KingdomOrdinaryFoodAuthority.PorterReceiptProperty);
			}
			KingdomSurvey.ObserveCurrentTopologyInActive(zone, store);
			system.Ledger.Note("{{G|" + KingdomCityRules.PorterNote(
				row.CargoAmount, store.ShortDisplayName) + "}}");
			XRL.Messages.MessageQueue.AddPlayerMessage("{{G|" + KingdomCityRules.PorterNote(
				row.CargoAmount, store.ShortDisplayName) + "}}");
			KingdomLog.Log("porter: job " + row.JobId + " deposited " + row.CargoAmount
				+ " into " + store.ShortDisplayName);
			Walk(body, zone, part.ExitX, part.ExitY);
		}

		private static bool TryPorterReceipts(GameObject owner, int jobId,
			out List<GameObject> receipts, out int count)
		{
			receipts = new List<GameObject>();
			count = 0;
			List<GameObject> held = !GameObject.Validate(owner) || owner.Inventory == null
				? null : new List<GameObject>(owner.Inventory.GetObjects());
			for (int i = 0; held != null && i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!GameObject.Validate(item) || item.InInventory != owner) return false;
				if (!item.HasIntProperty(KingdomOrdinaryFoodAuthority.PorterReceiptProperty)
					|| item.GetIntProperty(KingdomOrdinaryFoodAuthority.PorterReceiptProperty) != jobId)
					continue;
				string failure;
				if (item.HasStringProperty(KingdomOrdinaryFoodAuthority.PorterReceiptProperty)
					|| item.Count != 1 || item.GetIntProperty(StockProperty) != 1
					|| !KingdomOrdinaryFoodAuthority.TrySpendNow(item,
						KingdomOrdinaryFoodAuthority.PorterReceiptProperty, jobId, out failure)) return false;
				receipts.Add(item);
				count++;
			}
			return true;
		}
	}
}
