using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Only production authority for plain/notable guest mutations. Legacy clock fields
	/// are engine projections; every change is leased by the settlement lifecycle book.</summary>
	internal static class KingdomGuestLifecycle
	{
		internal const string MarkerProperty = "r_TAF_GuestLifecycleMarker";
		internal const string OperationProperty = "r_TAF_GuestLifecycleOperation";

		internal static KingdomLifecycleOperation Open(KingdomSystem system,
			KingdomLifecycleLane lane)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null) return null;
			return lane == KingdomLifecycleLane.PlainGuest ? book.PlainGuest
				: lane == KingdomLifecycleLane.NotableGuest ? book.NotableGuest : null;
		}

		internal static bool TryPrepareSpawnPlan(KingdomSystem system,
			KingdomLifecycleLane lane, string table, string fallbackBlueprint,
			out KingdomSemanticPersonPlan plan, out string failure)
		{
			plan = null;
			failure = null;
			KingdomLifecycleBook book = Authority(system);
			if (book == null || Open(system, lane) != null)
			{
				failure = "guest lifecycle authority is absent or already occupied";
				return false;
			}
			long sequence;
			string stream;
			bool title;
			if (lane == KingdomLifecycleLane.PlainGuest)
			{
				sequence = book.PlainGuestNextSequence;
				stream = KingdomSemanticSelection.PlainGuestStream;
				title = false;
			}
			else if (lane == KingdomLifecycleLane.NotableGuest)
			{
				sequence = book.NotableGuestNextSequence;
				stream = KingdomSemanticSelection.NotableGuestStream;
				title = true;
			}
			else
			{
				failure = "guest semantic lane is unsupported";
				return false;
			}
			return KingdomSemanticSelection.TryPreparePerson(system, table,
				fallbackBlueprint, stream, KingdomSemanticSelection.PersonEventKind,
				sequence, title, out plan, out failure);
		}

		internal static bool ObserveOption(KingdomSystem system, KingdomLifecycleLane lane,
			bool enabled, long now, out bool allowNew)
		{
			allowNew = false;
			KingdomLifecycleBook book = Authority(system);
			if (book == null || now < 0L) return false;
			KingdomLifecycleOptionState prior;
			long tick;
			KingdomLifecycleOperation open;
			if (lane == KingdomLifecycleLane.PlainGuest)
			{
				prior = book.LocusOption; tick = book.LocusOptionTick; open = book.PlainGuest;
			}
			else if (lane == KingdomLifecycleLane.NotableGuest)
			{
				prior = book.NotableOption; tick = book.NotableOptionTick; open = book.NotableGuest;
			}
			else return false;
			KingdomLifecycleOptionDecision decision = KingdomLifecycleRules.ObserveOption(
				prior, tick, enabled, now, open != null);
			if (!decision.Valid)
			{
				book.Quarantined = true;
				book.Fault = "guest option evidence moved backwards or was malformed";
				return false;
			}
			if (lane == KingdomLifecycleLane.PlainGuest)
			{
				book.LocusOption = decision.State; book.LocusOptionTick = decision.Tick;
			}
			else
			{
				book.NotableOption = decision.State; book.NotableOptionTick = decision.Tick;
			}
			allowNew = decision.AllowNewWork;
			return true;
		}

		internal static long EffectiveDue(KingdomSystem system, KingdomLifecycleLane lane,
			long interval)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || interval <= 0L) return 0L;
			long legacy = lane == KingdomLifecycleLane.PlainGuest
				? system.NextGuestTick : system.NextNotableGuestTick;
			long optionTick = lane == KingdomLifecycleLane.PlainGuest
				? book.LocusOptionTick : book.NotableOptionTick;
			long restamped = optionTick >= 0L && optionTick <= long.MaxValue - interval
				? optionTick + interval : 0L;
			return legacy > restamped ? legacy : restamped;
		}

		internal static void QuarantineLegacyEvidence(KingdomSystem system, string reason)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null) return;
			book.Quarantined = true;
			book.Fault = string.IsNullOrEmpty(reason)
				? "malformed legacy guest evidence retained" : reason;
		}

		internal static bool PublishPassages(KingdomSystem system, Zone zone,
			KingdomLifecycleLane lane, long now, long before, long after, int departed,
			long lastDeparted, long standingSince, string chronicle, string ledger,
			string guestbook)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || zone == null || before < 0L || after < 0L || before == after
				|| departed < 0 || lastDeparted < 0L || standingSince < 0L) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book, lane,
				KingdomLifecycleAction.Passages, now);
			if (op == null) return false;
			op.Count = departed;
			op.DepartTick = lastDeparted;
			op.Target = standingSince > 0L ? 1 : 0;
			op.ArrivalText = standingSince.ToString(CultureInfo.InvariantCulture);
			if (!KingdomLifecycleRules.GuestRuntimeAdapter.PrepareSchedule(book, op,
				zone.ZoneID, before, after)) return false;
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, chronicle, ledger, null,
				null, lane == KingdomLifecycleLane.NotableGuest ? guestbook : null);
			return op.Outbox != null && KingdomLifecycleRules.TryPublish(book, op)
				&& Drive(system, zone, lane);
		}

		internal static bool PublishSpawn(KingdomSystem system, Zone zone,
			KingdomLifecycleLane lane, Cell cell, long now, long departTick, string blueprint,
			string name, string origin, int kind, int target, string detail, string creed,
			string arrivalText, string chronicle, string ledger, string message, string guestbook,
			bool accomplishment = false, KingdomSemanticPersonPlan semanticPlan = null)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || zone == null || cell == null || departTick <= 0L
				|| string.IsNullOrEmpty(blueprint) || string.IsNullOrEmpty(name)) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book, lane,
				KingdomLifecycleAction.Spawn, now);
			if (op == null) return false;
			if (semanticPlan != null && (semanticPlan.RulesVersion !=
					Simulation.Kernel.KingdomSemanticSelectionRules.RulesVersion
				|| semanticPlan.Sequence != op.Sequence || semanticPlan.EventKind !=
					KingdomSemanticSelection.PersonEventKind
				|| !string.Equals(semanticPlan.Blueprint, blueprint, StringComparison.Ordinal)
				|| !string.Equals(semanticPlan.Name, name, StringComparison.Ordinal)
				|| !string.Equals(semanticPlan.Origin, origin, StringComparison.Ordinal)))
				return false;
			op.ObjectName = name;
			op.Origin = origin;
			op.Faction = semanticPlan == null
				? (string.Equals(creed, "causal-pilgrim", StringComparison.Ordinal)
					? KingdomSemanticSelection.CausalPilgrimStream : null)
				: semanticPlan.StreamId;
			op.DisplayFaction = semanticPlan?.Title;
			op.Kind = kind;
			op.Target = target;
			op.Detail = detail;
			op.Creed = creed;
			op.ArrivalText = arrivalText;
			op.DepartTick = departTick;
			string objectId = KingdomLifecycleRules.ChildId(op.Id, "guest", 0);
			if (KingdomLifecycleRules.GuestRuntimeAdapter.PrepareProjection(book, op, objectId,
				blueprint, zone.ZoneID, cell.X, cell.Y) == null
				|| !KingdomLifecycleRules.GuestRuntimeAdapter.PrepareDomain(book, op, 0L)
				|| !KingdomLifecycleRules.GuestRuntimeAdapter.PrepareSchedule(book, op,
					zone.ZoneID, 0L, departTick)) return false;
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, chronicle, ledger, message,
				null, lane == KingdomLifecycleLane.NotableGuest ? guestbook : null);
			if (op.Outbox == null) return false;
			op.Outbox.ChronicleAccomplishment = accomplishment;
			return KingdomLifecycleRules.TryPublish(book, op) && Drive(system, zone, lane);
		}

		internal static bool PublishMissedCausal(KingdomSystem system, Zone zone, long now,
			long before, long after, int sequence, string name, string cause, string place,
			string chronicle, string ledger)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || zone == null || before < 0L || after <= 0L || before == after
				|| sequence <= 0 || string.IsNullOrEmpty(cause)) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Passages, now);
			if (op == null) return false;
			op.ObjectName = name;
			op.Origin = cause;
			op.Detail = cause;
			op.ArrivalText = place;
			op.Kind = sequence;
			op.Creed = "causal-pilgrim";
			op.Count = 1;
			op.DepartTick = now;
			if (!KingdomLifecycleRules.GuestRuntimeAdapter.PrepareSchedule(book, op,
				zone.ZoneID, before, after)) return false;
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, chronicle, ledger, null,
				null, null);
			return op.Outbox != null && KingdomLifecycleRules.TryPublish(book, op)
				&& Drive(system, zone, KingdomLifecycleLane.PlainGuest);
		}

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

		internal static bool Drive(KingdomSystem system, Zone zone, KingdomLifecycleLane lane)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || zone == null) return false;
			for (int guard = 0; guard < 32; guard++)
			{
				KingdomLifecycleOperation op = lane == KingdomLifecycleLane.PlainGuest
					? book.PlainGuest : book.NotableGuest;
				if (op == null) return true;
				if (op.Phase == KingdomLifecyclePhase.Quarantined) return false;
				try
				{
					if (!SettlePhase(system, zone, book, op)) return false;
					if (op.Phase == KingdomLifecyclePhase.Terminal)
					{
						if (!KingdomLifecycleRules.Retire(book, op, The.Game.TimeTicks)) return false;
						return true;
					}
					KingdomLifecyclePhase next = Next(op);
					if (next == KingdomLifecyclePhase.Invalid
						|| !KingdomLifecycleRules.AdvancePhase(book, op, next, The.Game.TimeTicks))
						return false;
				}
				catch (Exception error)
				{
					MetricsManager.LogError("ThousandAndFirst guest lifecycle", error);
					return false;
				}
			}
			return false;
		}

		private static bool SettlePhase(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			switch (op.Phase)
			{
			case KingdomLifecyclePhase.ProjectionIntent:
				return SettleProjection(system, zone, book, op);
			case KingdomLifecyclePhase.WaterIntent:
				return SettleWater(system, zone, book, op);
			case KingdomLifecyclePhase.RemovalIntent:
				return SettleRemoval(system, zone, book, op);
			case KingdomLifecyclePhase.DomainIntent:
				return SettleDomain(system, zone, book, op);
			case KingdomLifecyclePhase.Sinks:
				return SettleSinks(system, book, op);
			case KingdomLifecyclePhase.ScheduleIntent:
				return SettleSchedule(system, zone, book, op);
			default:
				return true;
			}
		}

		private static bool SettleProjection(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			KingdomLifecycleProjection projection = op.Projections.Count == 1
				? op.Projections[0] : null;
			if (projection == null) return false;
			GameObject existing = FindExact(projection.ObjectId);
			bool exact = ExactProjection(existing, projection);
			if (projection.State == KingdomLifecyclePhysicalState.Intent)
			{
				if (!KingdomLifecycleRules.GuestRuntimeAdapter.RecoverProjectionIntent(book,
					op, projection, exact, !GameObject.Validate(existing))) return false;
				if (projection.State == KingdomLifecyclePhysicalState.Proved) return true;
			}
			GuestWorld world = new GuestWorld(system, zone, book, op);
			return KingdomLifecycleRules.TrustedAdapter.ProveLifecycleProjection(book, op,
				projection, world);
		}

		private static bool SettleWater(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			GuestWorld world = new GuestWorld(system, zone, book, op);
			for (int i = 0; i < op.WaterLegs.Count; i++)
			{
				KingdomLifecycleWaterLeg leg = op.WaterLegs[i];
				KingdomLifecycleResourceLease lease = FindLease(op, leg.LeaseKey);
				GameObject owner = FindExact(leg.OwnerId);
				LiquidVolume liquid = owner == null ? null : owner.GetPart<LiquidVolume>();
				if (leg.State == KingdomLifecyclePhysicalState.Proved) continue;
				if (leg.State == KingdomLifecyclePhysicalState.Intent)
				{
					if (liquid != null && liquid.Volume == leg.After)
					{
						if (!KingdomLifecycleRules.GuestRuntimeAdapter.RecoverWaterIntent(book,
							op, lease, leg, liquid.Volume)) return false;
						continue;
					}
					if (liquid == null || !KingdomLifecycleRules.GuestRuntimeAdapter.ResetWaterIntent(
						book, op, leg, liquid.Volume)) return false;
				}
				if (!KingdomLifecycleRules.TrustedAdapter.ProveWater(book, lease, leg, world))
					return false;
			}
			return true;
		}

		private static bool SettleRemoval(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			GameObject exact = FindExact(op.ObjectId);
			if (op.RemovalState == KingdomLifecyclePhysicalState.Intent)
				return KingdomLifecycleRules.GuestRuntimeAdapter.RecoverRemovalIntent(book, op,
					!GameObject.Validate(exact));
			return KingdomLifecycleRules.TrustedAdapter.ProveLifecycleRemoval(book, op,
				new GuestWorld(system, zone, book, op));
		}

		private static bool SettleDomain(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			KingdomLifecycleResourceLease lease = DomainLease(op);
			if (lease == null) return false;
			if (op.Action == KingdomLifecycleAction.Lodge)
			{
				long current = Simulation.City.KingdomResidents.OnRollCount(system);
				GameObject guest = FindExact(op.ObjectId);
				if (lease.State == KingdomLifecycleLeaseState.Intent)
				{
					if (current == lease.After)
					{
						if (!KingdomGuestbook.ApplyLifecycleLodge(system, guest, op)
							|| !KingdomGuestbook.LifecycleLodgeComplete(system, guest, op)) return false;
						KingdomSurvey.ObserveChangedInActive(zone, guest);
						return KingdomLifecycleRules.GuestRuntimeAdapter.RecoverDomainIntent(book,
							op, current);
					}
					if (current != lease.Before || !KingdomLifecycleRules.GuestRuntimeAdapter
						.ResetDomainIntent(book, op, current)) return false;
				}
				if (!KingdomLifecycleRules.GuestRuntimeAdapter.BeginDomain(book, op, current))
					return false;
				if (!KingdomGuestbook.ApplyLifecycleLodge(system, guest, op)) return false;
				KingdomSurvey.ObserveChangedInActive(zone, guest);
				long after = Simulation.City.KingdomResidents.OnRollCount(system);
				return KingdomGuestbook.LifecycleLodgeComplete(system, guest, op)
					&& KingdomLifecycleRules.GuestRuntimeAdapter.CommitDomain(book, op, after);
			}
			if (op.Creed == "causal-pilgrim") ApplyCausalDomain(system, op);
			if (op.Target == 1 && (op.Action == KingdomLifecycleAction.OfferWater
				|| op.Action == KingdomLifecycleAction.Depart)) system.FirstGuestGreeted = true;
			return KingdomLifecycleRules.GuestRuntimeAdapter.ProvePhysicalDomain(book, op);
		}

		private static void ApplyCausalDomain(KingdomSystem system,
			KingdomLifecycleOperation op)
		{
			if (system.City == null || op.Kind != system.City.PilgrimSequence) return;
			if (op.Action == KingdomLifecycleAction.Spawn)
			{
				system.City.PilgrimState = (int)KingdomLocusRules.PilgrimState.Standing;
				system.City.PilgrimObjectId = op.Projections[0].ObjectId;
				system.City.PilgrimName = op.ObjectName;
				return;
			}
			system.City.PilgrimState = (int)KingdomLocusRules.PilgrimState.None;
			system.City.PilgrimCauseTick = 0L;
			system.City.PilgrimCause = "";
			system.City.PilgrimObjectId = "";
			system.City.PilgrimName = "";
			system.City.PilgrimPlaceName = "";
			system.City.PilgrimGreeted = 0;
		}

		private static bool SettleSinks(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (op.Creed == "causal-pilgrim"
				&& op.Action == KingdomLifecycleAction.Passages) ApplyCausalDomain(system, op);
			if (!KingdomLifecycleRules.RecoverOutbox(book, op)) return false;
			KingdomLifecycleSinkMask[] sinks =
			{
				KingdomLifecycleSinkMask.Chronicle, KingdomLifecycleSinkMask.Ledger,
				KingdomLifecycleSinkMask.Message, KingdomLifecycleSinkMask.Guestbook
			};
			for (int i = 0; i < sinks.Length; i++)
			{
				KingdomLifecycleSinkMask sink = sinks[i];
				if (SinkState(op.Outbox, sink) != KingdomLifecycleSinkState.Pending) continue;
				if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginSink(book, op, sink)) return false;
				bool delivered = DeliverSink(system, op, sink);
				if (!delivered) return false;
				if (!KingdomLifecycleRules.RaidRuntimeAdapter.CommitSink(book, op, sink)) return false;
			}
			return true;
		}

		private static bool DeliverSink(KingdomSystem system, KingdomLifecycleOperation op,
			KingdomLifecycleSinkMask sink)
		{
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle:
				return KingdomChronicle.RecordOnce(system, op.Outbox.ChronicleReceiptId,
					op.Outbox.Chronicle, op.Outbox.ChronicleAccomplishment);
			case KingdomLifecycleSinkMask.Ledger:
				system.Ledger.Note(op.Outbox.Ledger); return true;
			case KingdomLifecycleSinkMask.Message:
				MessageQueue.AddPlayerMessage(op.Outbox.Message); return true;
			case KingdomLifecycleSinkMask.Guestbook:
				KingdomGuestbook.AppendLifecycleLine(system, op.Outbox.GuestbookLine); return true;
			default:
				return false;
			}
		}

		private static bool SettleSchedule(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			long current = CurrentSchedule(system, op);
			KingdomLifecycleResourceLease lease = ScheduleLease(op);
			if (lease == null) return false;
			if (lease.State == KingdomLifecycleLeaseState.Intent)
				return current == lease.After
					&& KingdomLifecycleRules.GuestRuntimeAdapter.RecoverScheduleIntent(book, op,
						current);
			return KingdomLifecycleRules.TrustedAdapter.ProveLifecycleSchedule(book, op,
				new GuestWorld(system, zone, book, op));
		}

		private static long CurrentSchedule(KingdomSystem system, KingdomLifecycleOperation op)
		{
			if (op.Action == KingdomLifecycleAction.Passages)
				return op.Lane == KingdomLifecycleLane.PlainGuest
					? system.NextGuestTick : system.NextNotableGuestTick;
			long depart = op.Lane == KingdomLifecycleLane.PlainGuest
				? system.GuestDepartTick : system.NotableGuestDepartTick;
			if (op.Action == KingdomLifecycleAction.Spawn) return depart;
			KingdomLifecycleResourceLease schedule = ScheduleLease(op);
			if (schedule != null && schedule.State == KingdomLifecycleLeaseState.Prepared)
				return depart;
			if (depart > 0L) return depart;
			return op.Lane == KingdomLifecycleLane.PlainGuest
				? system.NextGuestTick : system.NextNotableGuestTick;
		}

		private static void SetSchedule(KingdomSystem system, KingdomLifecycleOperation op,
			long value)
		{
			if (op.Action == KingdomLifecycleAction.Passages)
			{
				if (op.Lane == KingdomLifecycleLane.PlainGuest) system.NextGuestTick = value;
				else system.NextNotableGuestTick = value;
			}
			else if (op.Action == KingdomLifecycleAction.Spawn)
			{
				if (op.Lane == KingdomLifecycleLane.PlainGuest) system.GuestDepartTick = value;
				else system.NotableGuestDepartTick = value;
			}
			else
			{
				if (op.Lane == KingdomLifecycleLane.PlainGuest)
				{
					system.GuestDepartTick = 0L; system.NextGuestTick = value;
				}
				else
				{
					system.NotableGuestDepartTick = 0L; system.NextNotableGuestTick = value;
				}
			}
		}

		private static bool PrepareWater(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, KingdomSurvey survey, int amount)
		{
			if (survey == null || amount <= 0) return false;
			int remaining = amount;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < survey.Stores.Count && remaining > 0; i++)
			{
				LiquidVolume liquid = survey.Stores[i];
				GameObject owner = liquid == null ? null : liquid.ParentObject;
				if (!GameObject.Validate(owner) || !ids.Add(owner.ID)
					|| owner.GetIntProperty("KingdomStores") != 1
					|| !KingdomLiquids.HasFreshWater(liquid) || liquid.MaxVolume < 0) continue;
				int take = Math.Min(remaining, liquid.Volume);
				if (KingdomLifecycleRules.GuestRuntimeAdapter.PrepareWater(book, op,
					op.WaterLegs.Count, owner.ID, owner.Blueprint, owner.CurrentZone.ZoneID,
					liquid.MaxVolume, liquid.Volume, take, "water") == null) return false;
				remaining -= take;
			}
			return remaining == 0 && op.WaterRequested == amount;
		}

		private static KingdomLifecycleBook Authority(KingdomSystem system)
		{
			if (system == null || system.LifecycleBook == null || system.City == null
				|| !string.Equals(system.LifecycleBook.SettlementId,
					system.City.SettlementId, StringComparison.Ordinal)) return null;
			KingdomLifecycleRules.Normalize(system.LifecycleBook);
			return KingdomLifecycleRules.CanOwnAuthority(system.LifecycleBook)
				? system.LifecycleBook : null;
		}

		private static KingdomLifecyclePhase Next(KingdomLifecycleOperation op)
		{
			foreach (KingdomLifecyclePhase phase in Enum.GetValues(typeof(KingdomLifecyclePhase)))
				if (phase != KingdomLifecyclePhase.Quarantined
					&& KingdomLifecycleRules.CanTransition(op.Action, op.Phase, phase)) return phase;
			return KingdomLifecyclePhase.Invalid;
		}

		private static KingdomLifecycleResourceLease FindLease(KingdomLifecycleOperation op,
			string key)
		{
			if (op == null || op.ResourceLeases == null) return null;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i] != null && op.ResourceLeases[i].Key == key)
					return op.ResourceLeases[i];
			return null;
		}

		private static KingdomLifecycleResourceLease DomainLease(KingdomLifecycleOperation op)
		{
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceKind kind = op.ResourceLeases[i].Kind;
				if (kind != KingdomLifecycleResourceKind.Schedule
					&& kind != KingdomLifecycleResourceKind.WaterVessel
					&& kind != KingdomLifecycleResourceKind.Projection
					&& kind != KingdomLifecycleResourceKind.Object) return op.ResourceLeases[i];
			}
			return null;
		}

		private static KingdomLifecycleResourceLease ScheduleLease(KingdomLifecycleOperation op)
		{
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i].Kind == KingdomLifecycleResourceKind.Schedule)
					return op.ResourceLeases[i];
			return null;
		}

		private static KingdomLifecycleSinkState SinkState(KingdomLifecycleOutbox box,
			KingdomLifecycleSinkMask sink)
		{
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: return box.ChronicleState;
			case KingdomLifecycleSinkMask.Ledger: return box.LedgerState;
			case KingdomLifecycleSinkMask.Message: return box.MessageState;
			case KingdomLifecycleSinkMask.Guestbook: return box.GuestbookState;
			default: return KingdomLifecycleSinkState.Lost;
			}
		}

		private static GameObject FindExact(string id)
		{
			return string.IsNullOrEmpty(id) ? null : GameObject.FindByID(id);
		}

		private static bool ExactProjection(GameObject item, KingdomLifecycleProjection p)
		{
			return GameObject.Validate(item) && item.ID == p.ObjectId
				&& item.Blueprint == p.Blueprint && item.CurrentZone != null
				&& item.CurrentZone.ZoneID == p.ZoneId && item.CurrentCell != null
				&& item.CurrentCell.X == p.X && item.CurrentCell.Y == p.Y
				&& item.GetStringProperty(MarkerProperty) == p.Marker;
		}

		private sealed class GuestWorld : IKingdomLifecycleTrustedWorld
		{
			private readonly KingdomSystem System;
			private readonly Zone Zone;
			private readonly KingdomLifecycleBook Book;
			private readonly KingdomLifecycleOperation Operation;
			private readonly ScheduleReference Schedule = new ScheduleReference();
			private List<IKingdomLifecycleTrustedObservation> Cached;
			private GameObject Tombstone;

			internal GuestWorld(KingdomSystem system, Zone zone, KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				System = system; Zone = zone; Book = book; Operation = operation;
				Schedule.Value = CurrentSchedule(system, operation);
				KingdomLifecycleResourceLease lease = ScheduleLease(operation);
				Schedule.Revision = lease == null ? 0L : lease.BeforeRevision;
				for (int i = Book.RecentProofs.Count - 1; i >= 0; i--)
					if (Book.RecentProofs[i] != null && Book.RecentProofs[i].Lane == Operation.Lane)
					{
						Schedule.LastOperationId = Book.RecentProofs[i].Id;
						break;
					}
			}

			public int ObservationCount { get { Cached = Build(); return Cached.Count; } }

			public IKingdomLifecycleTrustedObservation Observe(int index)
			{
				return Cached[index];
			}

			public object InvokeCarryOutput(KingdomLifecycleProjection output) { return null; }

			public object InvokeWater(object vesselReference, int amount)
			{
				GameObject owner = vesselReference as GameObject;
				LiquidVolume liquid = owner == null ? null : owner.GetPart<LiquidVolume>();
				int drained;
				try { drained = KingdomLiquids.Drain(liquid, amount); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(Zone, owner); }
				if (drained != amount) return null;
				return owner;
			}

			public object InvokeSchedule(object scheduleReference, long dueTick, string operationId)
			{
				if (!ReferenceEquals(scheduleReference, Schedule) || Operation.Id != operationId)
					return null;
				SetSchedule(System, Operation, dueTick);
				Schedule.Value = dueTick;
				Schedule.Revision++;
				Schedule.LastOperationId = operationId;
				return Schedule;
			}

			public object InvokeCarryRemoval(object sourceReference, int count, string eventId)
			{
				return null;
			}

			public object InvokeCarrySignRemoval(object signReference, int count, string receiptId)
			{
				return null;
			}

			public object InvokeCarryMove(object sourceReference, int tripId,
				KingdomLifecycleTopology targetTopology, string targetOwnerId,
				string targetZoneId, int targetX, int targetY, string receiptId)
			{
				return null;
			}

			public object InvokeLifecycleProjection(KingdomLifecycleProjection projection)
			{
				GameObject body = Operation.Lane == KingdomLifecycleLane.PlainGuest
					? KingdomLocus.CreateLifecycleGuest(Operation, projection)
					: KingdomGuestbook.CreateLifecycleNotable(Operation, projection);
				if (!GameObject.Validate(body)) return null;
				Cell cell = Zone.GetCell(projection.X, projection.Y);
				if (cell == null) { body.Obliterate(); return null; }
				body.ID = projection.ObjectId;
				body.SetStringProperty(MarkerProperty, projection.Marker);
				body.SetStringProperty(OperationProperty, Operation.Id);
				GameObject accepted = null;
				try { accepted = cell.AddObject(body); }
				finally { KingdomSurvey.ObserveAddResultInActive(Zone, body, accepted); }
				if (!ReferenceEquals(accepted, body) || body.CurrentCell != cell)
					return null;
				body.MakeActive();
				return body;
			}

			public object InvokeLifecycleRemoval(object objectReference, int count,
				string operationId)
			{
				GameObject body = objectReference as GameObject;
				if (!GameObject.Validate(body) || count != 1 || operationId != Operation.Id)
					return null;
				bool removed;
				try { removed = body.Obliterate(); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(Zone, body); }
				if (!removed || GameObject.Validate(body)) return null;
				Tombstone = body;
				return body;
			}

			private List<IKingdomLifecycleTrustedObservation> Build()
			{
				List<IKingdomLifecycleTrustedObservation> rows =
					new List<IKingdomLifecycleTrustedObservation>();
				KingdomLifecycleResourceLease scheduleLease = ScheduleLease(Operation);
				if (scheduleLease != null)
				{
					rows.Add(new Observation(Schedule, scheduleLease.Key, null, "Schedule",
						Book.SettlementId, null, Operation.ZoneId,
						KingdomLifecycleTopology.Cell, 0, 0, 0, 0, null,
						Schedule.Value, Schedule.Revision, Schedule.LastOperationId));
				}
				foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
				{
					if (!GameObject.Validate(item) || item.CurrentCell == null) continue;
					LiquidVolume liquid = item.GetPart<LiquidVolume>();
					rows.Add(new Observation(item, item.ID,
						item.GetStringProperty(MarkerProperty), item.Blueprint, Book.SettlementId,
						null, Zone.ZoneID, KingdomLifecycleTopology.Cell,
						item.CurrentCell.X, item.CurrentCell.Y, 1,
						liquid == null || liquid.MaxVolume < 0 ? 0 : liquid.MaxVolume,
						liquid == null ? null : "water", liquid == null ? 0L : liquid.Volume,
						0L, null));
				}
				if (Tombstone != null)
					rows.Add(new Observation(Tombstone, Operation.ObjectId,
						Tombstone.GetStringProperty(MarkerProperty), Operation.Blueprint,
						Book.SettlementId, null, Operation.ZoneId, Operation.ObjectTopology,
						Operation.ObjectX, Operation.ObjectY, 0, 0, null, 0L, 0L, null));
				return rows;
			}
		}

		/// <summary>Lifecycle identity stores plain prose; Qud formatting never enters authority.</summary>
		private static string PlainObjectName(GameObject guest)
		{
			if (!GameObject.Validate(guest)) return "";
			string named = guest.GetStringProperty("KingdomName");
			return string.IsNullOrEmpty(named) ? (guest.BaseDisplayNameStripped ?? "") : named;
		}

		private sealed class ScheduleReference
		{
			internal long Value;
			internal long Revision;
			internal string LastOperationId;
		}

		private sealed class Observation : IKingdomLifecycleTrustedObservation
		{
			public object Reference { get; private set; }
			public string ObjectId { get; private set; }
			public string Marker { get; private set; }
			public string Blueprint { get; private set; }
			public string SettlementId { get; private set; }
			public string OwnerId { get; private set; }
			public string ZoneId { get; private set; }
			public KingdomLifecycleTopology Topology { get; private set; }
			public int X { get; private set; }
			public int Y { get; private set; }
			public int Count { get; private set; }
			public int Capacity { get; private set; }
			public string Composition { get; private set; }
			public long Value { get; private set; }
			public long Revision { get; private set; }
			public string LastOperationId { get; private set; }

			internal Observation(object reference, string objectId, string marker,
				string blueprint, string settlementId, string ownerId, string zoneId,
				KingdomLifecycleTopology topology, int x, int y, int count, int capacity,
				string composition, long value, long revision, string lastOperationId)
			{
				Reference = reference; ObjectId = objectId; Marker = marker; Blueprint = blueprint;
				SettlementId = settlementId; OwnerId = ownerId; ZoneId = zoneId;
				Topology = topology; X = x; Y = y; Count = count; Capacity = capacity;
				Composition = composition; Value = value; Revision = revision;
				LastOperationId = lastOperationId;
			}
		}
	}
}
