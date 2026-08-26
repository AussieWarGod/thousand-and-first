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
