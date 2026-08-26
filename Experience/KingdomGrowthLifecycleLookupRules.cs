using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static KingdomGrowthSlotKind SlotForGrowthAction(KingdomGrowthAction action)
		{
			switch (action)
			{
			case KingdomGrowthAction.Heartbeat: return KingdomGrowthSlotKind.Heartbeat;
			case KingdomGrowthAction.Arrival: return KingdomGrowthSlotKind.Arrival;
			case KingdomGrowthAction.Departure: return KingdomGrowthSlotKind.Departure;
			case KingdomGrowthAction.Delivery: return KingdomGrowthSlotKind.Delivery;
			case KingdomGrowthAction.Fetch: return KingdomGrowthSlotKind.Fetch;
			case KingdomGrowthAction.Mill: return KingdomGrowthSlotKind.Mill;
			case KingdomGrowthAction.Sow:
			case KingdomGrowthAction.Withdraw:
			case KingdomGrowthAction.Ripen:
			case KingdomGrowthAction.Harvest:
			case KingdomGrowthAction.Irrigate: return KingdomGrowthSlotKind.Field;
			default: return KingdomGrowthSlotKind.None;
			}
		}

		private static KingdomGrowthFieldSlot FindGrowthField(KingdomGrowthBook book,
			string fieldId)
		{
			if (book == null || book.FieldOps == null || fieldId == null) return null;
			KingdomGrowthFieldSlot found = null;
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field == null || !string.Equals(field.FieldId, fieldId,
					StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = field;
			}
			return found;
		}

		private static KingdomGrowthOperation GetGrowthOperation(KingdomGrowthBook book,
			KingdomGrowthSlotKind slot, string fieldId)
		{
			if (book == null) return null;
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: return book.HeartbeatOp;
			case KingdomGrowthSlotKind.Arrival: return book.ArrivalOp;
			case KingdomGrowthSlotKind.Departure: return book.DepartureOp;
			case KingdomGrowthSlotKind.Delivery: return book.DeliveryOp;
			case KingdomGrowthSlotKind.Fetch: return book.FetchOp;
			case KingdomGrowthSlotKind.Mill: return book.MillOp;
			case KingdomGrowthSlotKind.Field:
				KingdomGrowthFieldSlot field = FindGrowthField(book, fieldId);
				return field == null ? null : field.Operation;
			default: return null;
			}
		}

		private static void SetGrowthOperation(KingdomGrowthBook book,
			KingdomGrowthSlotKind slot, KingdomGrowthFieldSlot field,
			KingdomGrowthOperation operation)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: book.HeartbeatOp = operation; break;
			case KingdomGrowthSlotKind.Arrival: book.ArrivalOp = operation; break;
			case KingdomGrowthSlotKind.Departure: book.DepartureOp = operation; break;
			case KingdomGrowthSlotKind.Delivery: book.DeliveryOp = operation; break;
			case KingdomGrowthSlotKind.Fetch: book.FetchOp = operation; break;
			case KingdomGrowthSlotKind.Mill: book.MillOp = operation; break;
			case KingdomGrowthSlotKind.Field: field.Operation = operation; break;
			}
		}

		private static long GetGrowthNext(KingdomGrowthBook book, KingdomGrowthSlotKind slot,
			KingdomGrowthFieldSlot field)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: return book.HeartbeatNextSequence;
			case KingdomGrowthSlotKind.Arrival: return book.ArrivalNextSequence;
			case KingdomGrowthSlotKind.Departure: return book.DepartureNextSequence;
			case KingdomGrowthSlotKind.Delivery: return book.DeliveryNextSequence;
			case KingdomGrowthSlotKind.Fetch: return book.FetchNextSequence;
			case KingdomGrowthSlotKind.Mill: return book.MillNextSequence;
			case KingdomGrowthSlotKind.Field: return field == null ? long.MaxValue : field.NextSequence;
			default: return long.MaxValue;
			}
		}

		private static void SetGrowthNext(KingdomGrowthBook book, KingdomGrowthSlotKind slot,
			KingdomGrowthFieldSlot field, long value)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: book.HeartbeatNextSequence = value; break;
			case KingdomGrowthSlotKind.Arrival: book.ArrivalNextSequence = value; break;
			case KingdomGrowthSlotKind.Departure: book.DepartureNextSequence = value; break;
			case KingdomGrowthSlotKind.Delivery: book.DeliveryNextSequence = value; break;
			case KingdomGrowthSlotKind.Fetch: book.FetchNextSequence = value; break;
			case KingdomGrowthSlotKind.Mill: book.MillNextSequence = value; break;
			case KingdomGrowthSlotKind.Field: field.NextSequence = value; break;
			}
		}

		private static long GetGrowthRetired(KingdomGrowthBook book, KingdomGrowthSlotKind slot,
			KingdomGrowthFieldSlot field)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: return book.HeartbeatRetiredThrough;
			case KingdomGrowthSlotKind.Arrival: return book.ArrivalRetiredThrough;
			case KingdomGrowthSlotKind.Departure: return book.DepartureRetiredThrough;
			case KingdomGrowthSlotKind.Delivery: return book.DeliveryRetiredThrough;
			case KingdomGrowthSlotKind.Fetch: return book.FetchRetiredThrough;
			case KingdomGrowthSlotKind.Mill: return book.MillRetiredThrough;
			case KingdomGrowthSlotKind.Field: return field == null ? long.MaxValue : field.RetiredThrough;
			default: return long.MaxValue;
			}
		}

		private static void SetGrowthRetired(KingdomGrowthBook book, KingdomGrowthSlotKind slot,
			KingdomGrowthFieldSlot field, long value)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: book.HeartbeatRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Arrival: book.ArrivalRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Departure: book.DepartureRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Delivery: book.DeliveryRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Fetch: book.FetchRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Mill: book.MillRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Field: field.RetiredThrough = value; break;
			}
		}

		private static long GrowthClockValue(KingdomGrowthBook book,
			KingdomGrowthAction action, KingdomGrowthFieldSlot field)
		{
			switch (action)
			{
			case KingdomGrowthAction.Heartbeat: return book.LastHeartbeatTick;
			case KingdomGrowthAction.Arrival: return book.NextArrivalTick;
			case KingdomGrowthAction.Departure: return book.LastDepartureTick;
			case KingdomGrowthAction.Delivery: return book.LastDeliveryTick;
			case KingdomGrowthAction.Fetch: return book.LastFetchTick;
			case KingdomGrowthAction.Mill: return book.LastMillTick;
			case KingdomGrowthAction.Sow:
			case KingdomGrowthAction.Withdraw:
			case KingdomGrowthAction.Ripen:
			case KingdomGrowthAction.Harvest:
			case KingdomGrowthAction.Irrigate: return field == null ? -1L : field.CommitRevision;
			default: return -1L;
			}
		}

		private static string GrowthClockSubject(string settlementId,
			KingdomGrowthSlotKind slot, string fieldId)
		{
			return HashId("growth-clock-subject", delegate(BinaryWriter w)
			{
				CanonicalString(w, settlementId); w.Write((byte)slot); CanonicalString(w, fieldId);
			});
		}

		private static KingdomLifecycleResourceRevision FindGrowthResource(KingdomGrowthBook book,
			string key)
		{
			if (book == null || book.Resources == null || key == null) return null;
			KingdomLifecycleResourceRevision found = null;
			for (int i = 0; i < book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = book.Resources[i];
				if (row == null || !string.Equals(row.Key, key, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = row;
			}
			return found;
		}

		private static bool IsPhysicalResourceKind(KingdomLifecycleResourceKind Kind)
		{
			return Kind == KingdomLifecycleResourceKind.Schedule
				|| Kind == KingdomLifecycleResourceKind.WaterVessel
				|| Kind == KingdomLifecycleResourceKind.Object
				|| Kind == KingdomLifecycleResourceKind.Projection;
		}

	}
}
