using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		public static bool Bind(KingdomSystem System, int bindingKey, KingdomBindingKind kind, string zoneId, GameObject Body, long TimeTicks)
		{
			KingdomBindingTable table;
			if (!TryTable(System, out table) || bindingKey == 0)
			{
				return false;
			}
			string objectId = GameObject.Validate(Body) ? Body.ID : null;
			KingdomBinding standing;
			bool held = table.TryGet(bindingKey, kind, out standing);
			if (!held && kind == KingdomBindingKind.Transient
				&& !KingdomExperienceRuntime.FoundationOwnsCarrierClaim(System, bindingKey)
				&& !KingdomExperienceRuntime.TryAdmitFoundationTransientClaim(System,
					bindingKey, out KingdomExperienceCapacityFault _, out string capacityFailure))
			{
				KingdomLog.Log("binding: shared body capacity refused transient " + bindingKey
					+ " (" + (capacityFailure ?? "invalid authority") + ")");
				return false;
			}
			// A settler who has not moved since the last pass costs nothing. Without this, every
			// check-in would republish the whole registry once per person on the ground, to write
			// down where each of them already was.
			if (held
				&& string.Equals(standing.ZoneId ?? "", zoneId ?? "", StringComparison.Ordinal)
				&& string.Equals(standing.ObjectId ?? "", objectId ?? "", StringComparison.Ordinal))
			{
				return true;
			}
			KingdomBindingTable next;
			KingdomCityFault fault;
			bool written = held
				? table.TryRebind(bindingKey, kind, zoneId, objectId, out next, out fault)
				: table.TryBind(bindingKey, kind, zoneId, objectId, (TimeTicks > 0L) ? TimeTicks : 0L, out next, out fault);
			if (!written)
			{
				Refuse("bind", fault);
				return false;
			}
			return Publish(System, next, "bind");
		}

		/// <summary>
		/// Evicts a binding and says why. The cause reaches the log line; the row it belongs to
		/// carries it durably, which is the division &sect;3.8 sets up by keeping no second list of
		/// closed bindings.
		/// </summary>
		public static bool Unbind(KingdomSystem System, int bindingKey, KingdomBindingKind kind, KingdomUnbindCause cause)
		{
			KingdomBindingTable table;
			if (!TryTable(System, out table))
			{
				return false;
			}
			KingdomBindingTable next;
			KingdomBinding evicted;
			KingdomCityFault fault;
			if (!table.TryUnbind(bindingKey, kind, cause, out next, out evicted, out fault))
			{
				// An unknown key is not an error worth a line: a body unbound twice in one pass is
				// the ordinary shape of a death that two consumers both noticed.
				if (fault != KingdomCityFault.UnknownBinding)
				{
					Refuse("unbind", fault);
				}
				return false;
			}
			if (!Publish(System, next, "unbind"))
			{
				return false;
			}
			KingdomLog.Log("binding: released " + kind + " " + bindingKey + " from "
				+ (string.IsNullOrEmpty(evicted.ZoneId) ? "-" : evicted.ZoneId) + " (" + cause + ")");
			return true;
		}

		/// <summary>
		/// What the stale-transient sweep would say about one object in a thawed zone.
		/// <para>
		/// &sect;3.8's t3, and the whole of it that W2 owns: <b>the detection verdict ships now, the
		/// despawn is W3.</b> A body carrying a job id whose binding the model already evicted is
		/// holding goods the stores were credited for at the dated tick — the one instant they
		/// could exist twice.
		/// </para>
		/// </summary>
		public static KingdomSweepVerdict SweepVerdict(KingdomSystem System, GameObject Body)
		{
			if (!GameObject.Validate(Body))
			{
				return KingdomSweepVerdict.NotTransient;
			}
			int jobId = Body.GetIntProperty(JobIdProperty);
			KingdomBindingTable table;
			if (jobId == 0 || !TryTable(System, out table))
			{
				// A registry that would not read cannot prove a body stale, and the sweep's licence
				// is deduplication rather than destruction: without the proof, nothing is touched.
				return KingdomBindingRules.JudgeStale(jobId, true);
			}
			return KingdomBindingRules.JudgeStale(jobId, table.Holds(jobId, KingdomBindingKind.Transient));
		}

		/// <summary>
		/// Invariant I3 over the whole realm, asserted rather than inferred: no binding key ever
		/// resolves to two living bodies. Returns null when the registry is clean, and the fault's
		/// own name when it is not.
		/// </summary>
		public static string AuditLine(KingdomSystem System)
		{
			KingdomBindingTable table;
			if (!TryTable(System, out table))
			{
				return "bindings unreadable";
			}
			KingdomCityFault fault;
			if (!table.TryAudit(out fault))
			{
				return "bindings " + fault;
			}
			return null;
		}
	}
}
