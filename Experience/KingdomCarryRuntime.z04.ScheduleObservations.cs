using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomCarryRuntime
	{

		private static KingdomLifecycleResourceRevision ScheduleRow(KingdomCarryBook book,
			KingdomCarryOperation op)
		{
			if (book == null || op == null || book.Resources == null) return null;
			string key = KingdomLifecycleRules.ResourceKey(KingdomLifecycleResourceKind.Schedule,
				book.RealmId, op.DestinationSettlementId);
			for (int i = 0; i < book.Resources.Count; i++)
				if (book.Resources[i] != null && book.Resources[i].Key == key) return book.Resources[i];
			return null;
		}

		private static long PriorScheduleValue(KingdomCarryHaul haul)
		{
			return haul != null && LegacyMaterialUnits(haul) == 0 && haul.DueTick >= 0L
				? haul.DueTick : 0L;
		}

		private static bool MatchesScheduleProjection(KingdomCarryHaul haul,
			KingdomCarryOperation op)
		{
			return haul != null && op != null && LegacyMaterialUnits(haul) == 0
				&& haul.OriginZoneID == op.OriginZoneId && haul.OriginX == op.OriginX
				&& haul.OriginY == op.OriginY
				&& haul.DestinationSettlementId == op.DestinationSettlementId
				&& haul.PlantedTick == op.CreatedTick && haul.DueTick == op.DueTick;
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
			public string Marker { get { return null; } }
			public string Blueprint { get; private set; }
			public string SettlementId { get; private set; }
			public string OwnerId { get; private set; }
			public string ZoneId { get; private set; }
			public KingdomLifecycleTopology Topology { get; private set; }
			public int X { get; private set; }
			public int Y { get; private set; }
			public int Count { get; private set; }
			public int Capacity { get { return 0; } }
			public string Composition { get { return null; } }
			public long Value { get; private set; }
			public long Revision { get; private set; }
			public string LastOperationId { get; private set; }

			internal Observation(object reference, string objectId, string blueprint,
				string settlementId, string ownerId, string zoneId,
				KingdomLifecycleTopology topology, int x, int y, int count,
				long value, long revision, string lastOperationId)
			{
				Reference = reference; ObjectId = objectId; Blueprint = blueprint;
				SettlementId = settlementId; OwnerId = ownerId; ZoneId = zoneId;
				Topology = topology; X = x; Y = y; Count = count; Value = value;
				Revision = revision; LastOperationId = lastOperationId;
			}
		}
	}
}
