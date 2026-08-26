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
	internal static partial class KingdomGuestLifecycle
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

	}
}
