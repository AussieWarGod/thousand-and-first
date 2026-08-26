using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomGuestLifecycle
	{
		internal static bool PublishDeparture(KingdomSystem system, GameObject guest,
			KingdomLifecycleLane lane, long now, long nextDue, bool greeted,
			string chronicle, string ledger, string message, string guestbook,
			bool accomplishment = false)
		{
			return PublishRemoval(system, guest, lane, KingdomLifecycleAction.Depart, now,
				nextDue, greeted, 0, chronicle, ledger, message, guestbook, accomplishment);
		}

		internal static bool PublishOfferWater(KingdomSystem system, GameObject guest,
			long now, long nextDue, string chronicle, string ledger, string message,
			bool accomplishment)
		{
			return PublishRemoval(system, guest, KingdomLifecycleLane.PlainGuest,
				KingdomLifecycleAction.OfferWater, now, nextDue, true,
				KingdomLocusRules.GuestWaterCostDrams, chronicle, ledger, message, null,
				accomplishment);
		}

		private static bool PublishRemoval(KingdomSystem system, GameObject guest,
			KingdomLifecycleLane lane, KingdomLifecycleAction action, long now, long nextDue,
			bool greeted, int waterCost, string chronicle, string ledger, string message,
			string guestbook, bool accomplishment)
		{
			KingdomLifecycleBook book = Authority(system);
			Cell cell = guest == null ? null : guest.CurrentCell;
			Zone zone = guest == null ? null : guest.CurrentZone;
			if (book == null || !GameObject.Validate(guest) || cell == null || zone == null
				|| nextDue <= 0L) return false;
			long scheduleBefore = lane == KingdomLifecycleLane.PlainGuest
				? system.GuestDepartTick : system.NotableGuestDepartTick;
			bool causal = lane == KingdomLifecycleLane.PlainGuest
				&& guest.GetIntProperty(KingdomLocus.CausalPilgrimProperty) == 1;
			long semanticDepart = scheduleBefore;
			if (causal && system.City != null
				&& guest.GetIntProperty(KingdomLocus.PilgrimSequenceProperty)
					== system.City.PilgrimSequence
				&& KingdomLocusRules.TryPilgrimWindow(system.City.PilgrimCauseTick,
					out _, out long causalDepart)) semanticDepart = causalDepart;
			if (scheduleBefore < 0L || semanticDepart <= 0L
				|| (!causal && scheduleBefore == 0L)) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book, lane,
				action, now);
			if (op == null) return false;
			op.ObjectName = PlainObjectName(guest);
			op.Origin = guest.GetStringProperty("KingdomOrigin");
			op.Kind = lane == KingdomLifecycleLane.NotableGuest
				? guest.GetIntProperty("KingdomGuestHookKind")
				: guest.GetIntProperty(KingdomLocus.PilgrimSequenceProperty);
			op.Target = greeted ? 1 : 0;
			op.Detail = lane == KingdomLifecycleLane.NotableGuest
				? guest.GetStringProperty("KingdomGuestHookText")
				: guest.GetStringProperty(KingdomLocus.PilgrimCauseProperty);
			op.Creed = causal
				? "causal-pilgrim" : null;
			op.ArrivalText = op.Creed == null ? null : system.City.PilgrimPlaceName;
			op.DepartTick = semanticDepart;
			if (!KingdomLifecycleRules.GuestRuntimeAdapter.PrepareRemoval(book, op,
				guest.ID, guest.Blueprint, zone.ZoneID, cell.X, cell.Y)) return false;
			if (waterCost > 0 && !PrepareWater(book, op, KingdomSurvey.Take(zone), waterCost))
				return false;
			long domainBefore = action == KingdomLifecycleAction.OfferWater ? 0L : 1L;
			if (!KingdomLifecycleRules.GuestRuntimeAdapter.PrepareDomain(book, op, domainBefore)
				|| !KingdomLifecycleRules.GuestRuntimeAdapter.PrepareSchedule(book, op,
					zone.ZoneID, scheduleBefore, nextDue)) return false;
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, chronicle, ledger, message,
				null, lane == KingdomLifecycleLane.NotableGuest ? guestbook : null);
			if (op.Outbox == null) return false;
			op.Outbox.ChronicleAccomplishment = accomplishment;
			return KingdomLifecycleRules.TryPublish(book, op) && Drive(system, zone, lane);
		}

		internal static bool PublishLodge(KingdomSystem system, GameObject guest,
			GameObject fineHouse, long now, long nextDue, int waterCost, string chronicle,
			string ledger, string message, string guestbook, bool accomplishment)
		{
			KingdomLifecycleBook book = Authority(system);
			Zone zone = guest == null ? null : guest.CurrentZone;
			if (book == null || !GameObject.Validate(guest) || zone == null || nextDue <= 0L
				|| system.NotableGuestDepartTick <= 0L) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.NotableGuest, KingdomLifecycleAction.Lodge, now);
			if (op == null) return false;
			op.ObjectId = guest.ID;
			op.ObjectMarker = GameObject.Validate(fineHouse) ? fineHouse.ID : null;
			op.Blueprint = guest.Blueprint;
			op.ObjectName = PlainObjectName(guest);
			op.Origin = guest.GetStringProperty("KingdomOrigin");
			op.Kind = guest.GetIntProperty("KingdomGuestHookKind");
			op.Detail = guest.GetStringProperty("KingdomGuestHookText");
			op.Target = guest.HasTag(KingdomGuestbook.LegendaryTraderTag) ? 1 : 0;
			op.PlunderRequested = op.Target == 1 ? system.ShopTier : 0;
			op.DisplayFaction = op.Target == 1 ? "finehouse" : null;
			op.Faction = XRL.World.Calendar.GetDay() + " of "
				+ XRL.World.Calendar.GetMonth() + ", " + XRL.World.Calendar.GetYear() + " AR";
			Simulation.Kernel.SemanticEventKey creedKey;
			Simulation.Kernel.KernelFaultCode creedFault;
			if (!Simulation.Kernel.SemanticEventKey.TryCreate(
				Simulation.Kernel.KingdomSemanticSelectionRules.RulesVersion,
				system.CurrentSettlementId, KingdomSemanticSelection.NotableLodgeStream,
				KingdomSemanticSelection.LodgeEventKind, (ulong)op.Sequence,
				out creedKey, out creedFault)
				|| !KingdomCreed.TryDraw(system, system.SimulationSeed, creedKey, 0U,
					out op.Creed)) return false;
			system.CreedCounts.TryGetValue(op.Creed ?? "", out int creedBefore);
			op.Count = string.IsNullOrEmpty(op.Creed) ? 0 : creedBefore;
			op.Defence = Simulation.City.KingdomResidents.OnRollCount(system);
			op.DepartTick = system.NotableGuestDepartTick;
			if (!PrepareWater(book, op, KingdomSurvey.Take(zone), waterCost)
				|| !KingdomLifecycleRules.GuestRuntimeAdapter.PrepareDomain(book, op,
					Simulation.City.KingdomResidents.OnRollCount(system))
				|| !KingdomLifecycleRules.GuestRuntimeAdapter.PrepareSchedule(book, op,
					zone.ZoneID, system.NotableGuestDepartTick, nextDue)) return false;
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, chronicle, ledger, message,
				null, guestbook);
			if (op.Outbox == null) return false;
			op.Outbox.ChronicleAccomplishment = accomplishment;
			return KingdomLifecycleRules.TryPublish(book, op)
				&& Drive(system, zone, KingdomLifecycleLane.NotableGuest);
		}

	}
}
