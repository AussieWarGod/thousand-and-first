using System;
using System.Collections.Generic;

using Qud.API;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomExpeditions
	{
		/// <summary>Builds a read-only physical receipt. IDs may be assigned here, but no water or
		/// food moves until the prepared realm row and this encoded receipt are both durable.</summary>
		private static bool TryPrepareDebitReceipt(KingdomSurvey Survey, KingdomWaterDebit Water,
			int JobId, string SourceZoneId, int WaterCost, int ProvisionCost,
			out KingdomExpeditionDebitReceipt Receipt, out string Encoded, out string Failure)
		{
			Receipt = null;
			Encoded = null;
			Failure = null;
			if (Survey == null || Water == null || JobId <= 0 || WaterCost <= 0
				|| ProvisionCost <= 0 || Survey.FoodStored < ProvisionCost)
				return Refuse("Dedicated stores no longer cover the exact quote.", out Failure);
			KingdomWaterDebitLeg[] described;
			if (!Water.TryDescribe(out described) || described.Length <= 0)
				return Refuse("The exact water reservation could not expose a bounded receipt.", out Failure);
			KingdomExpeditionWaterLeg[] water = new KingdomExpeditionWaterLeg[described.Length];
			for (int i = 0; i < described.Length; i++)
			{
				GameObject owner = described[i].Owner;
				string ownerId = GameObject.Validate(owner) ? owner.ID : null;
				if (string.IsNullOrEmpty(ownerId)
					|| ownerId.Length > KingdomExpeditionDebitReceipt.MaxIdentityChars)
					return Refuse("A dedicated water vessel lacks a bounded persistent identity.", out Failure);
				water[i] = new KingdomExpeditionWaterLeg(ownerId, described[i].BeforeVolume,
					described[i].AfterVolume, described[i].MaxVolume);
			}

			List<KingdomExpeditionProvisionLeg> provisions =
				new List<KingdomExpeditionProvisionLeg>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
			int remaining = ProvisionCost;
			for (int i = 0; i < Survey.Larders.Count && remaining > 0; i++)
			{
				GameObject larder = Survey.Larders[i];
				if (!GameObject.Validate(larder) || larder.Inventory == null
					|| larder.GetIntProperty("KingdomLarder") != 1) continue;
				string larderId = larder.ID;
				if (string.IsNullOrEmpty(larderId)
					|| larderId.Length > KingdomExpeditionDebitReceipt.MaxIdentityChars) continue;
				List<GameObject> items = new List<GameObject>(larder.Inventory.GetObjects());
				for (int j = 0; j < items.Count && remaining > 0; j++)
				{
					GameObject item = items[j];
					if (!GameObject.Validate(item) || !seen.Add(item) || item.InInventory != larder
						|| item.Count <= 0 || item.GetIntProperty(ProvisionJobProperty) != 0
						|| (!item.HasPart("Food")
							&& !item.HasPart("PreparedCookingIngredient"))) continue;
					string itemId = item.ID;
					if (string.IsNullOrEmpty(itemId)
						|| itemId.Length > KingdomExpeditionDebitReceipt.MaxIdentityChars
						|| !seenIds.Add(itemId)) continue;
					int take = (item.Count < remaining) ? item.Count : remaining;
					provisions.Add(new KingdomExpeditionProvisionLeg(larderId, itemId,
						item.Count, item.Count - take));
					remaining -= take;
				}
			}
			if (remaining != 0)
				return Refuse("The larders cannot bind every quoted provision to one exact stack.", out Failure);
			if (!KingdomExpeditionDebitReceipt.TryCreate(JobId, SourceZoneId, WaterCost,
				ProvisionCost, water, provisions.ToArray(), out Receipt)
				|| !Receipt.TryEncode(out Encoded))
				return Refuse("The exact debit receipt exceeds its fixed identity or size bounds.", out Failure);
			return true;
		}

		private static bool TryApplyPreparedDebit(KingdomSystem System, KingdomJobRow Row,
			GameObject Body, KingdomExpeditionDebitReceipt Receipt, KingdomWaterDebit ReservedWater,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Body)
				|| !KingdomExpeditionDebitReceipt.TryDecode(
					Body.GetStringProperty(DebitReceiptProperty), out KingdomExpeditionDebitReceipt held)
				|| held.JobId != Row.JobId)
				return Refuse("The exact body no longer holds this job's debit receipt.", out Failure);
			Zone source;
			try { source = The.ZoneManager.GetZone(Receipt.SourceZoneId); }
			catch { return Refuse("The debit receipt's source ground cannot be thawed.", out Failure); }
			if (!TryApplyProvisionReceipt(source, Row.JobId, Receipt, out Failure)) return false;
			if (ReservedWater != null && WaterAllBefore(source, Receipt))
			{
				if (!MarkWaterReceipt(source, Row.JobId, Receipt, out Failure)) return false;
				if (!ReservedWater.Commit())
					return Refuse("The exact water callback did not complete; its durable receipt remains open for CAS recovery.", out Failure);
			}
			if (!TryApplyWaterReceipt(source, Row.JobId, Receipt, out Failure)) return false;
			return true;
		}

		private static bool TryApplyProvisionReceipt(Zone Source, int JobId,
			KingdomExpeditionDebitReceipt Receipt, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Receipt.ProvisionLegCount; i++)
			{
				KingdomExpeditionProvisionLeg leg;
				if (!Receipt.TryProvisionLeg(i, out leg))
					return Refuse("A provision receipt leg is absent.", out Failure);
				GameObject larder = FindZoneObject(Source, leg.LarderId);
				if (!GameObject.Validate(larder) || larder.Inventory == null
					|| larder.GetIntProperty("KingdomLarder") != 1)
					return Refuse("A receipt-bound larder is missing; no replacement stack was charged.", out Failure);
				GameObject item = FindInventoryObject(larder, leg.ItemId);
				bool present = GameObject.Validate(item) && item.InInventory == larder;
				int current = present ? item.Count : 0;
				int remaining;
				if (!KingdomExpeditionRules.TryDebitProgress(leg.BeforeCount, leg.AfterCount,
					present, current, out remaining))
					return Refuse("A receipt-bound provision stack left its exact before/after range; it was not charged again.", out Failure);
				if (!present) continue;
				int marker = item.GetIntProperty(ProvisionJobProperty);
				if (marker != 0 && marker != JobId)
					return Refuse("A provision stack belongs to another durable receipt.", out Failure);
				if (remaining > 0)
				{
					item.SetIntProperty(ProvisionJobProperty, JobId);
					while (remaining > 0)
					{
						int before = item.Count;
						try { item.Destroy(null, Silent: true); }
						catch
						{
							KingdomSurvey.ObserveCurrentTopologyInActive(Source, larder);
							return Refuse("A provision callback stopped; the exact partial count remains recoverable.", out Failure);
						}
						KingdomSurvey.ObserveChangedInActive(Source, larder);
						present = GameObject.Validate(item) && item.InInventory == larder;
						current = present ? item.Count : 0;
						if (current != before - 1
							|| !KingdomExpeditionRules.TryDebitProgress(leg.BeforeCount,
								leg.AfterCount, present, current, out remaining))
							return Refuse("A provision callback left an unexpected count; no second stack was touched.", out Failure);
					}
				}
			}
			return true;
		}

		private static bool MarkWaterReceipt(Zone Source, int JobId,
			KingdomExpeditionDebitReceipt Receipt, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Receipt.WaterLegCount; i++)
			{
				KingdomExpeditionWaterLeg leg;
				if (!Receipt.TryWaterLeg(i, out leg)) return false;
				GameObject owner = FindZoneObject(Source, leg.OwnerId);
				LiquidVolume vessel = owner?.GetPart<LiquidVolume>();
				if (!GameObject.Validate(owner) || vessel == null
					|| owner.GetIntProperty("KingdomStores") != 1
					|| vessel.MaxVolume != leg.MaxVolume)
					return Refuse("A receipt-bound water vessel is missing or changed.", out Failure);
				int marker = owner.GetIntProperty(WaterJobProperty);
				if (marker != 0 && marker != JobId)
					return Refuse("A water vessel belongs to another durable receipt.", out Failure);
				owner.SetIntProperty(WaterJobProperty, JobId);
				owner.SetIntProperty(WaterAfterProperty, leg.AfterVolume);
			}
			return true;
		}

		private static bool WaterAllBefore(Zone Source, KingdomExpeditionDebitReceipt Receipt)
		{
			for (int i = 0; i < Receipt.WaterLegCount; i++)
			{
				KingdomExpeditionWaterLeg leg;
				if (!Receipt.TryWaterLeg(i, out leg)) return false;
				LiquidVolume vessel = FindZoneObject(Source, leg.OwnerId)?.GetPart<LiquidVolume>();
				if (vessel == null || vessel.Volume != leg.BeforeVolume) return false;
			}
			return true;
		}

		private static bool TryApplyWaterReceipt(Zone Source, int JobId,
			KingdomExpeditionDebitReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!MarkWaterReceipt(Source, JobId, Receipt, out Failure)) return false;
			for (int i = 0; i < Receipt.WaterLegCount; i++)
			{
				KingdomExpeditionWaterLeg leg;
				Receipt.TryWaterLeg(i, out leg);
				GameObject owner = FindZoneObject(Source, leg.OwnerId);
				LiquidVolume vessel = owner.GetPart<LiquidVolume>();
				bool present = vessel != null;
				int current = present ? vessel.Volume : 0;
				int remaining;
				if (!KingdomExpeditionRules.TryDebitProgress(leg.BeforeVolume, leg.AfterVolume,
					present, current, out remaining))
					return Refuse("A receipt-bound water volume left its exact before/after range; it was not charged again.", out Failure);
				while (remaining > 0)
				{
					if (!KingdomLiquids.HasFreshWater(vessel))
						return Refuse("A receipt-bound vessel no longer contains pure fresh water.", out Failure);
					int before = vessel.Volume;
					try { KingdomLiquids.Drain(vessel, remaining); }
					catch
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(Source, owner);
						return Refuse("A water callback stopped; the exact partial volume remains recoverable.", out Failure);
					}
					KingdomSurvey.ObserveChangedInActive(Source, owner);
					current = vessel.Volume;
					if (current >= before
						|| !KingdomExpeditionRules.TryDebitProgress(leg.BeforeVolume,
							leg.AfterVolume, true, current, out remaining))
						return Refuse("A water callback left an unexpected volume; no second vessel was touched.", out Failure);
				}
			}
			return true;
		}

		private static GameObject FindZoneObject(Zone Zone, string ObjectId)
		{
			if (Zone == null || string.IsNullOrEmpty(ObjectId)) return null;
			GameObject found = null;
			foreach (GameObject candidate in KingdomSurvey.ObjectsFor(Zone))
			{
				if (!string.Equals(candidate.IDIfAssigned, ObjectId, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = candidate;
			}
			return found;
		}

		private static GameObject FindInventoryObject(GameObject Owner, string ObjectId)
		{
			if (!GameObject.Validate(Owner) || Owner.Inventory == null
				|| string.IsNullOrEmpty(ObjectId)) return null;
			GameObject found = null;
			foreach (GameObject item in Owner.Inventory.GetObjects())
			{
				if (!string.Equals(item.IDIfAssigned, ObjectId, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = item;
			}
			return found;
		}

		private static bool HasDebitMarker(Zone Zone, int JobId)
		{
			if (Zone == null || JobId <= 0) return false;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
			{
				if (item.GetIntProperty(WaterJobProperty) == JobId) return true;
				if (item.Inventory == null) continue;
				foreach (GameObject held in item.Inventory.GetObjects())
					if (held.GetIntProperty(ProvisionJobProperty) == JobId) return true;
			}
			return false;
		}

		private static void ClearDebitMarkers(KingdomJobRow Row)
		{
			Zone zone;
			try { zone = The.ZoneManager.GetZone(Row.SourceZoneId); }
			catch { return; }
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
			{
				if (item.GetIntProperty(WaterJobProperty) == Row.JobId)
				{
					item.RemoveIntProperty(WaterJobProperty);
					item.RemoveIntProperty(WaterAfterProperty);
				}
				if (item.Inventory == null) continue;
				foreach (GameObject held in item.Inventory.GetObjects())
					if (held.GetIntProperty(ProvisionJobProperty) == Row.JobId)
						held.RemoveIntProperty(ProvisionJobProperty);
			}
		}
	}
}
