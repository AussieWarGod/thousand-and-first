using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomPorters
	{
		/// <summary>Hands every exact load object to the ground. Protected evidence keeps every
		/// ownership marker; only this porter's ordinary receipt may be retired.</summary>
		private static int Abandon(GameObject body)
		{
			if (!GameObject.Validate(body) || body.Inventory == null || body.CurrentCell == null)
				return 0;
			int jobId = body.GetPart<r_KingdomPorter>()?.JobId ?? 0;
			Cell ground = body.CurrentCell;
			List<GameObject> held = new List<GameObject>(body.Inventory.GetObjects());
			int dropped = 0;
			for (int i = 0; i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!GameObject.Validate(item) || item.InInventory != body) continue;
				bool hadStock = item.HasIntProperty(StockProperty);
				bool hadReceipt = item.HasIntProperty(KingdomOrdinaryFoodAuthority.PorterReceiptProperty)
					&& item.GetIntProperty(KingdomOrdinaryFoodAuthority.PorterReceiptProperty) == jobId;
				string failure;
				bool owned = jobId > 0 && KingdomOrdinaryFoodAuthority.TryObjectNow(item,
					KingdomOrdinaryFoodAuthority.PorterReceiptProperty, jobId, out failure);
				if (owned)
				{
					item.RemoveIntProperty(StockProperty);
					if (hadReceipt) item.RemoveIntProperty(
						KingdomOrdinaryFoodAuthority.PorterReceiptProperty);
				}
				int count = item.Count;
				if (TryDrop(body, ground, item)) dropped += count;
				else if (owned && item.InInventory == body)
				{
					if (hadStock) item.SetIntProperty(StockProperty, 1);
					if (hadReceipt) item.SetIntProperty(
						KingdomOrdinaryFoodAuthority.PorterReceiptProperty, jobId);
				}
			}
			return dropped;
		}

		private static void Spill(GameObject body)
		{
			if (!GameObject.Validate(body) || body.Inventory == null) return;
			int jobId = body.GetPart<r_KingdomPorter>()?.JobId ?? 0;
			Cell ground = body.CurrentCell;
			List<GameObject> held = new List<GameObject>(body.Inventory.GetObjects());
			for (int i = 0; i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!GameObject.Validate(item) || item.InInventory != body) continue;
				string failure;
				bool disposable = jobId > 0 && KingdomOrdinaryFoodAuthority.TryObjectNow(item,
					KingdomOrdinaryFoodAuthority.PorterReceiptProperty, jobId, out failure)
					&& item.HasProperty(StockProperty)
					&& !item.HasPropertyOrTag(NoRestockProperty) && !item.IsImportant();
				if (!disposable) TryDrop(body, ground, item);
			}
		}

		private static bool TryDrop(GameObject body, Cell ground, GameObject item)
		{
			if (!GameObject.Validate(body) || body.Inventory == null || ground == null
				|| !GameObject.Validate(item) || item.InInventory != body) return false;
			string id = item.IDIfAssigned;
			string blueprint = item.Blueprint;
			int count = item.Count;
			try { body.Inventory.RemoveObject(item); }
			catch { }
			if (!GameObject.Validate(item) || item.InInventory != null || item.CurrentCell != null)
				return false;
			try { ground.AddObject(item, Silent: true, NoStack: true); }
			catch { }
			return GameObject.Validate(item) && item.InInventory == null
				&& item.CurrentCell == ground && item.IDIfAssigned == id
				&& item.Blueprint == blueprint && item.Count == count;
		}

		private static bool CanRetireBody(GameObject body, int jobId)
		{
			if (!GameObject.Validate(body) || jobId <= 0) return false;
			List<GameObject> held = body.Inventory == null ? null : body.Inventory.GetObjects();
			for (int i = 0; held != null && i < held.Count; i++)
			{
				GameObject item = held[i];
				string failure;
				if (!GameObject.Validate(item) || item.InInventory != body
					|| !item.HasProperty(StockProperty) || item.HasPropertyOrTag(NoRestockProperty)
					|| item.IsImportant() || !KingdomOrdinaryFoodAuthority.TryObjectNow(item,
						KingdomOrdinaryFoodAuthority.PorterReceiptProperty, jobId, out failure))
					return false;
			}
			string custodyFailure;
			return KingdomOrdinaryFoodAuthority.TryCustodyAvailable(body,
				KingdomOrdinaryFoodAuthority.PorterReceiptProperty, jobId, out custodyFailure);
		}

		private static void Release(KingdomSystem system, int jobId, GameObject body,
			KingdomUnbindCause cause)
		{
			KingdomResidents.Unbind(system, jobId, KingdomBindingKind.Transient, cause);
			if (!GameObject.Validate(body)) return;
			Zone zone = body.CurrentZone;
			Spill(body);
			if (!CanRetireBody(body, jobId))
			{
				KingdomLog.Log("porter: protected or torn custody kept carrier " + jobId + " visible");
				return;
			}
			try { body.Obliterate(); }
			catch { return; }
			if (!GameObject.Validate(body)) KingdomSurvey.ObserveRemovedFromActive(zone, body);
		}

		private static void Handoff(KingdomSystem system, int jobId, GameObject body, string zoneId)
		{
			if (!GameObject.Validate(body) || !KingdomResidents.Unbind(system, jobId,
				KingdomBindingKind.Transient, KingdomUnbindCause.ZoneHandoff)) return;
			Zone zone = body.CurrentZone;
			Spill(body);
			if (!CanRetireBody(body, jobId)) return;
			try { body.Obliterate(); }
			catch { return; }
			if (GameObject.Validate(body)) return;
			KingdomSurvey.ObserveRemovedFromActive(zone, body);
			KingdomLog.Log("porter: job " + jobId + " handed off at the exact exit from " + zoneId);
		}
	}
}
