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
		private static void SetMaterial(KingdomCarryOperation op, int material, int group, int value)
		{
			if (group == 1)
			{
				switch (material) { case 0: op.EscrowMud = value; break; case 1: op.EscrowBrush = value; break;
				case 2: op.EscrowTimber = value; break; case 3: op.EscrowStone = value; break;
				case 4: op.EscrowMarble = value; break; default: op.EscrowScrap = value; break; }
			}
			else if (group == 2)
			{
				switch (material) { case 0: op.DeliveredMud = value; break; case 1: op.DeliveredBrush = value; break;
				case 2: op.DeliveredTimber = value; break; case 3: op.DeliveredStone = value; break;
				case 4: op.DeliveredMarble = value; break; default: op.DeliveredScrap = value; break; }
			}
			else if (group == 3)
			{
				switch (material) { case 0: op.LostMud = value; break; case 1: op.LostBrush = value; break;
				case 2: op.LostTimber = value; break; case 3: op.LostStone = value; break;
				case 4: op.LostMarble = value; break; default: op.LostScrap = value; break; }
			}
		}

		public static bool TryPlanHash(KingdomLifecycleOperation op, out string Hash)
		{
			Hash = null;
			if (op == null) return false;
			try
			{
				Hash = HashId("plan", delegate(BinaryWriter w)
				{
					w.Write(op.Sequence); CanonicalString(w, op.Id); w.Write((byte)op.Lane);
					w.Write((byte)op.Action); w.Write(op.CreatedTick);
					CanonicalString(w, op.SettlementId); CanonicalString(w, op.ZoneId);
					CanonicalString(w, op.ObjectId); CanonicalString(w, op.ObjectMarker);
					CanonicalString(w, op.Blueprint); w.Write((byte)op.ObjectTopology);
					CanonicalString(w, op.ObjectOwnerId); w.Write(op.ObjectX); w.Write(op.ObjectY);
					CanonicalString(w, op.ObjectName);
					CanonicalString(w, op.Origin); CanonicalString(w, op.Faction);
					CanonicalString(w, op.DisplayFaction); CanonicalString(w, op.Detail);
					CanonicalString(w, op.Creed); w.Write(op.Kind); w.Write(op.Target);
					w.Write(op.Count); w.Write(op.DueBefore);
					w.Write(op.DueAfter); w.Write(op.DepartTick); w.Write(op.WaterRequested);
					w.Write(op.Defence); w.Write(op.PartySize);
					w.Write(op.PlunderRequested); CanonicalString(w, op.ArrivalText);
					w.Write(op.WaterLegs == null ? -1 : op.WaterLegs.Count);
					if (op.WaterLegs != null) for (int i = 0; i < op.WaterLegs.Count; i++)
					{
						KingdomLifecycleWaterLeg x = op.WaterLegs[i];
						CanonicalString(w, x.OperationId); CanonicalString(w, x.LeaseKey);
						CanonicalString(w, x.OwnerId); CanonicalString(w, x.Blueprint);
						CanonicalString(w, x.ZoneId);
						w.Write(x.Capacity); w.Write(x.Before); w.Write(x.Delta); w.Write(x.After);
						CanonicalString(w, x.Composition); CanonicalString(w, x.ReceiptId);
					}
					w.Write(op.Projections == null ? -1 : op.Projections.Count);
					if (op.Projections != null) for (int i = 0; i < op.Projections.Count; i++)
						WriteProjectionPlan(w, op.Projections[i]);
					w.Write(op.ResourceLeases == null ? -1 : op.ResourceLeases.Count);
					if (op.ResourceLeases != null) for (int i = 0; i < op.ResourceLeases.Count; i++)
						WriteLeasePlan(w, op.ResourceLeases[i]);
					WriteOutboxPlan(w, op.Outbox);
				});
				return ValidHashNamespace(Hash, "plan");
			}
			catch (Exception)
			{
				Hash = null;
				return false;
			}
		}

		public static bool TryCarryPlanHash(KingdomCarryOperation op, out string Hash)
		{
			Hash = null;
			if (op == null) return false;
			try
			{
				Hash = HashId("carry-plan", delegate(BinaryWriter w)
				{
					w.Write(op.Sequence); CanonicalString(w, op.Id); w.Write(op.CreatedTick);
					w.Write(op.SettlementIds == null ? -1 : op.SettlementIds.Count);
					if (op.SettlementIds != null) for (int i = 0; i < op.SettlementIds.Count; i++)
						CanonicalString(w, op.SettlementIds[i]);
					CanonicalString(w, op.RealmTopologyHash);
					CanonicalString(w, op.OriginSettlementId);
					CanonicalString(w, op.OriginZoneId); w.Write(op.OriginX); w.Write(op.OriginY);
					CanonicalString(w, op.DestinationSettlementId);
					CanonicalString(w, op.DestinationSettlementName);
					CanonicalString(w, op.DestinationZoneId); w.Write((byte)op.DestinationTopology);
					CanonicalString(w, op.DestinationOwnerId); w.Write(op.DestinationX);
					w.Write(op.DestinationY); w.Write(op.DueTick);
					w.Write(op.RiskFrozen);
					w.Write(op.AuthorityKind == KingdomCarryAuthorityKind.ExactManifest
						? false : op.LostOnRoad);
					WriteLeasePlan(w, op.ScheduleLease);
					CanonicalString(w, op.ScheduleReceiptId);
					CanonicalString(w, op.ScheduleTopologyId);
					w.Write(op.Sources == null ? -1 : op.Sources.Count);
					if (op.Sources != null) for (int i = 0; i < op.Sources.Count; i++)
					{
						KingdomCarrySource x = op.Sources[i];
						CanonicalString(w, x.OperationId); CanonicalString(w, x.SourceEventId);
						CanonicalString(w, x.ObjectId); CanonicalString(w, x.Blueprint);
						w.Write((byte)x.Topology); CanonicalString(w, x.OwnerId);
						CanonicalString(w, x.ZoneId); w.Write(x.X); w.Write(x.Y);
						w.Write(x.Material); w.Write(x.OriginalCount); w.Write(x.PlannedCount);
					}
					w.Write(op.Outputs == null ? -1 : op.Outputs.Count);
					if (op.Outputs != null) for (int i = 0; i < op.Outputs.Count; i++)
						WriteProjectionPlan(w, op.Outputs[i]);
					for (int material = 0; material < 6; material++)
						w.Write(MaterialValue(op, material, 0));
					WriteOutboxPlan(w, op.Outbox);
					if (op.AuthorityKind == KingdomCarryAuthorityKind.ExactManifest)
					{
						w.Write((byte)op.AuthorityKind); w.Write(op.ManifestVersion);
						CanonicalString(w, op.ManifestDigest);
						CanonicalString(w, op.SignObjectId); CanonicalString(w, op.SignBlueprint);
						w.Write((byte)op.SignTopology); CanonicalString(w, op.SignOwnerId);
						CanonicalString(w, op.SignZoneId); w.Write(op.SignX); w.Write(op.SignY);
						w.Write(op.SignCount); CanonicalString(w, op.SignReceiptId);
						w.Write(op.JobIds == null ? -1 : op.JobIds.Count);
						if (op.JobIds != null) for (int i = 0; i < op.JobIds.Count; i++)
							w.Write(op.JobIds[i]);
						w.Write(op.TripIds == null ? -1 : op.TripIds.Count);
						if (op.TripIds != null) for (int i = 0; i < op.TripIds.Count; i++)
							w.Write(op.TripIds[i]);
						CanonicalString(w, op.SpillZoneId); w.Write(op.SpillX); w.Write(op.SpillY);
					}
				});
				return ValidHashNamespace(Hash, "carry-plan");
			}
			catch (Exception)
			{
				Hash = null;
				return false;
			}
		}

		/// <summary>Canonical immutable digest consumed by the central logistics rows. It freezes
		/// ordered whole source objects only; callback progress and carrier assignment cannot change
		/// it.</summary>
		public static bool TryCarryManifestDigest(KingdomCarryOperation op, out string Hash)
		{
			Hash = null;
			if (op == null || op.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
				|| op.ManifestVersion != CurrentCarryManifestVersion || op.Sources == null
				|| op.Sources.Count == 0 || op.Sources.Count > MaxCarrySources) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < op.Sources.Count; i++)
			{
				KingdomCarrySource source = op.Sources[i];
				if (source == null || !string.Equals(source.OperationId, op.Id,
						StringComparison.Ordinal)
					|| !string.Equals(source.SourceEventId, ChildId(op.Id, "source", i),
						StringComparison.Ordinal)
					|| !ValidRootId(source.ObjectId) || !ids.Add(source.ObjectId)
					|| !ValidName(source.Blueprint)
					|| !TopologyValid(source.Topology, source.OwnerId, source.ZoneId,
						source.X, source.Y)
					|| source.Material != -1
					|| source.OriginalCount <= 0 || source.OriginalCount > MaxPhysicalCount
					|| source.PlannedCount != source.OriginalCount) return false;
			}
			try
			{
				Hash = HashId("carry-manifest", delegate(BinaryWriter w)
				{
					w.Write(op.ManifestVersion); CanonicalString(w, op.Id);
					w.Write(op.Sources.Count);
					for (int i = 0; i < op.Sources.Count; i++)
					{
						KingdomCarrySource source = op.Sources[i];
						w.Write(i); CanonicalString(w, source.SourceEventId);
						CanonicalString(w, source.ObjectId); CanonicalString(w, source.Blueprint);
						w.Write((byte)source.Topology); CanonicalString(w, source.OwnerId);
						CanonicalString(w, source.ZoneId); w.Write(source.X); w.Write(source.Y);
						w.Write(source.Material); w.Write(source.OriginalCount);
						w.Write(source.PlannedCount);
					}
				});
				return ValidHashNamespace(Hash, "carry-manifest");
			}
			catch (Exception)
			{
				Hash = null;
				return false;
			}
		}

		private static void WriteProjectionPlan(BinaryWriter w, KingdomLifecycleProjection x)
		{
			CanonicalString(w, x.OperationId); CanonicalString(w, x.EventId);
			CanonicalString(w, x.ObjectId); CanonicalString(w, x.Marker);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId);
			w.Write((byte)x.Topology); CanonicalString(w, x.OwnerId); w.Write(x.X); w.Write(x.Y);
			w.Write(x.Material); w.Write(x.Count); w.Write(x.NoStack);
			CanonicalString(w, x.ReceiptId); CanonicalString(w, x.ReceiptTopologyId);
		}

		private static void WriteLeasePlan(BinaryWriter w, KingdomLifecycleResourceLease x)
		{
			CanonicalString(w, x.OperationId); w.Write((byte)x.Kind);
			CanonicalString(w, x.ScopeId); CanonicalString(w, x.SubjectId);
			CanonicalString(w, x.Key); w.Write(x.Before); w.Write(x.Delta); w.Write(x.After);
			w.Write(x.BeforeRevision); w.Write(x.AfterRevision);
		}

		private static void WriteOutboxPlan(BinaryWriter w, KingdomLifecycleOutbox x)
		{
			if (x == null) { w.Write(false); return; }
			w.Write(true); CanonicalString(w, x.OperationId); CanonicalString(w, x.EventId);
			CanonicalString(w, x.ChronicleReceiptId); CanonicalString(w, x.Chronicle);
			w.Write(x.ChronicleAccomplishment); w.Write((byte)x.ChronicleDisposition);
			CanonicalString(w, x.Ledger); w.Write((byte)x.LedgerDisposition);
			CanonicalString(w, x.Message); w.Write((byte)x.MessageDisposition);
			CanonicalString(w, x.Deed); w.Write((byte)x.DeedDisposition);
			CanonicalString(w, x.GuestbookLine); w.Write((byte)x.GuestbookDisposition);
		}

		private static bool ProofListValid(KingdomLifecycleBook Book)
		{
			if (Book == null || Book.RecentProofs == null
				|| Book.RecentProofs.Count > MaxRecentProofs) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> coordinates = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Book.RecentProofs.Count; i++)
			{
				KingdomLifecycleProof p = Book.RecentProofs[i];
				if (p == null || p.Sequence <= 0L || !ActionAllowedInLane(p.Action, p.Lane)
					|| p.Sequence > GetRetiredThrough(Book, p.Lane) || p.Tick < 0L
					|| !string.Equals(p.Id, OperationId(Book.SettlementId, p.Lane, p.Sequence),
						StringComparison.Ordinal)
					|| !ValidHashNamespace(p.PlanHash, "plan") || !ids.Add(p.Id)
					|| !coordinates.Add(((byte)p.Lane).ToString(CultureInfo.InvariantCulture)
						+ ":" + p.Sequence.ToString(CultureInfo.InvariantCulture))) return false;
			}
			return true;
		}

		private static bool CarryProofListValid(KingdomCarryBook Book)
		{
			if (Book == null || Book.RecentProofs == null
				|| Book.RecentProofs.Count > MaxRecentProofs) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			HashSet<long> sequences = new HashSet<long>();
			for (int i = 0; i < Book.RecentProofs.Count; i++)
			{
				KingdomLifecycleProof p = Book.RecentProofs[i];
				if (p == null || p.Sequence <= 0L || p.Sequence > Book.RetiredThrough
					|| p.Lane != KingdomLifecycleLane.None || p.Action != KingdomLifecycleAction.None
					|| p.Tick < 0L || !string.Equals(p.Id, CarryId(Book.RealmId, p.Sequence),
						StringComparison.Ordinal) || !ValidHashNamespace(p.PlanHash, "carry-plan")
					|| !ids.Add(p.Id) || !sequences.Add(p.Sequence)) return false;
			}
			return true;
		}

		private static bool ResourceRegistryValid(KingdomLifecycleBook Book)
		{
			if (Book == null || Book.Resources == null || Book.Resources.Count > MaxResourceRows)
				return false;
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = Book.Resources[i];
				if (!ResourceShape(row) || !keys.Add(row.Key)) return false;
			}
			return true;
		}

	}
}
