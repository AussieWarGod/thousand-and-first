using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomPhysicalHappenings
	{
		private static string PostReceipt(GameObject body)
		{
			return KingdomStations.PostOf(body).ToString(CultureInfo.InvariantCulture) + "/"
				+ body.GetIntProperty(KingdomStations.PostKindProperty).ToString(
					CultureInfo.InvariantCulture);
		}

		private static string PostReceipt(KingdomHappeningParticipant row)
		{
			return row.PostWorkId.ToString(CultureInfo.InvariantCulture) + "/"
				+ row.PostKind.ToString(CultureInfo.InvariantCulture);
		}

		private static string NameOf(GameObject body)
		{
			return body.GetStringProperty("KingdomName") ?? body.BaseDisplayNameStripped ?? "";
		}

		private static string[] Names(KingdomHappeningOperation operation)
		{
			if (operation == null) return new string[0];
			string[] names = new string[operation.Participants.Length];
			for (int i = 0; i < names.Length; i++) names[i] = operation.Participants[i].Name;
			return names;
		}

		private static GameObject FindById(Zone zone, string objectId)
		{
			if (zone == null || string.IsNullOrEmpty(objectId)) return null;
			GameObject found = GameObject.FindByID(objectId);
			return GameObject.Validate(found) && found.CurrentCell != null
				&& ReferenceEquals(found.CurrentZone, zone)
				&& string.Equals(found.IDIfAssigned, objectId, StringComparison.Ordinal)
				? found : null;
		}

		private static bool TryFindById(Zone zone, string objectId, out GameObject found,
			out bool absent)
		{
			found = null;
			absent = false;
			if (zone == null || string.IsNullOrEmpty(objectId)) return false;
			GameObject exact = GameObject.FindByID(objectId);
			if (!GameObject.Validate(exact))
			{
				absent = true;
				return true;
			}
			// An exact fixture id resolving elsewhere is conflicting evidence, not absence. Keep the
			// durable restoration receipt open rather than clearing another zone's authority.
			if (exact.CurrentCell == null || !ReferenceEquals(exact.CurrentZone, zone)
				|| !string.Equals(exact.IDIfAssigned, objectId, StringComparison.Ordinal)) return false;
			found = exact;
			return true;
		}

		private static Zone ExactLoadedZone(string zoneId)
		{
			Zone zone = null;
			if (string.IsNullOrEmpty(zoneId) || The.ZoneManager?.CachedZones == null
				|| !The.ZoneManager.CachedZones.TryGetValue(zoneId, out zone)) return null;
			return zone;
		}

		private static bool StandsIn(string zoneId)
		{
			return The.Player?.CurrentZone != null
				&& string.Equals(The.Player.CurrentZone.ZoneID, zoneId, StringComparison.Ordinal);
		}

		private static bool OwnedGround(KingdomSystem system, string zoneId)
		{
			return system != null && system.OwnedZone(zoneId);
		}

		private static bool TryRead(KingdomCityBook book, long nowTick,
			out KingdomHappeningLifecycleBook lifecycle)
		{
			if (!TryReadRaw(book, out lifecycle)) return false;
			KingdomHappeningLifecycleBook recovered =
				KingdomHappeningLifecycleRules.RecoverInterruptedSinks(lifecycle, nowTick);
			if (!ReferenceEquals(recovered, lifecycle) && !Write(book, recovered)) return false;
			lifecycle = recovered;
			return true;
		}

		private static bool TryReadRaw(KingdomCityBook book,
			out KingdomHappeningLifecycleBook lifecycle)
		{
			lifecycle = null;
			KingdomHappeningLifecycleFault fault = KingdomHappeningLifecycleFault.Malformed;
			if (book == null || !KingdomHappeningLifecycleRules.TryDecode(
				book.HappeningModel, out lifecycle, out fault))
			{
				KingdomLog.Log("happening physical: sidecar refused (" + fault + ")");
				return false;
			}
			return true;
		}

		private static bool Write(KingdomCityBook book, KingdomHappeningLifecycleBook lifecycle)
		{
			if (book == null || !KingdomHappeningLifecycleRules.TryEncode(lifecycle,
				out string wire)) return false;
			book.HappeningModel = wire;
			return true;
		}

		private static bool SetPhase(KingdomCityBook book,
			KingdomHappeningLifecycleBook lifecycle, KingdomHappeningLifecyclePhase expected,
			KingdomHappeningLifecyclePhase phase, bool attended, long holdUntil, long nowTick)
		{
			return lifecycle.Active != null && KingdomHappeningLifecycleRules.TrySetPhase(
				lifecycle, lifecycle.Active.EventId, expected, phase, attended, holdUntil,
				nowTick, out KingdomHappeningLifecycleBook changed,
				out KingdomHappeningLifecycleFault fault) && Write(book, changed);
		}

		private static bool Clear(KingdomCityBook book,
			KingdomHappeningLifecycleBook lifecycle, string eventId)
		{
			return KingdomHappeningLifecycleRules.TryClear(lifecycle, eventId,
				out KingdomHappeningLifecycleBook changed,
				out KingdomHappeningLifecycleFault fault) && Write(book, changed);
		}

		private enum SinkLane : byte
		{
			Chronicle,
			Told,
			Effect,
			Ledger,
			Message
		}

		private sealed class Evidence
		{
			internal readonly Zone Zone;
			internal readonly GameObject Fixture;
			internal readonly List<GameObject> Bodies;
			internal readonly bool FixtureExact;
			internal readonly bool ParticipantsExact;
			internal readonly bool AllArrived;
			internal readonly bool UseReceiptExact;

			internal Evidence(Zone zone, GameObject fixture, List<GameObject> bodies,
				bool fixtureExact, bool participantsExact, bool allArrived, bool useReceiptExact)
			{
				Zone = zone;
				Fixture = fixture;
				Bodies = bodies;
				FixtureExact = fixtureExact;
				ParticipantsExact = participantsExact;
				AllArrived = allArrived;
				UseReceiptExact = useReceiptExact;
			}
		}
	}
}
