using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>One copy-on-write durable construction job.</summary>
	public sealed class KingdomConstructionJob
	{
		public string Id;
		public string OwnerKey;
		public string ZoneId;
		public KingdomConstructionRoute Route;
		public KingdomConstructionPhase Phase;
		public KingdomConstructionProjection Projection;
		public int X;
		public int Y;
		public string SubjectId;
		/// <summary>Immutable exact predecessor captured when intent is first published.</summary>
		public string SourceId;
		/// <summary>Exact generated object ID, published before its first AddObject callback.</summary>
		public string OutputId;
		public KingdomPhysicalPhase PhysicalPhase;
		public int PhysicalIndex;
		public int PhysicalAmount;
		public int PhysicalSpilled;
		public string PhysicalItemId;
		public string PhysicalDestinationId;
		public string PhysicalReceipt;
		public string TargetKey;
		public string Payload;
		/// <summary>Version of immutable paid build-effect truth. Zero is a legacy receipt;
		/// it must never be completed from live catalogue data.</summary>
		public int BuildTruthSchema;
		public bool BuildHasPlot;
		public bool BuildFrontier;
		/// <summary>Final defence, including every bonus known at receipt publication.</summary>
		public int BuildDefence;
		public long CreatedTick;
		public long StartedTick;
		public long DueTick;
		public long UpdatedTick;
		public int Revision;
		public KingdomConstructionClaims Claims;
		public string Failure;
		public KingdomConstructionOutbox Outbox;
		/// <summary>A settled terminal row reduced to an immutable replay proof.</summary>
		public bool Compacted;
		/// <summary>SHA-256 of the canonical compact identity/counter record. It proves that
		/// retained replay membership was not edited; it deliberately does not claim to hash
		/// payload/outbox bytes discarded during compaction.</summary>
		public string CompactHash;

		public KingdomConstructionJob Copy()
		{
			return new KingdomConstructionJob
			{
				Id = Id,
				OwnerKey = OwnerKey,
				ZoneId = ZoneId,
				Route = Route,
				Phase = Phase,
				Projection = Projection,
				X = X,
				Y = Y,
				SubjectId = SubjectId,
				SourceId = SourceId,
				OutputId = OutputId,
				PhysicalPhase = PhysicalPhase,
				PhysicalIndex = PhysicalIndex,
				PhysicalAmount = PhysicalAmount,
				PhysicalSpilled = PhysicalSpilled,
				PhysicalItemId = PhysicalItemId,
				PhysicalDestinationId = PhysicalDestinationId,
				PhysicalReceipt = PhysicalReceipt,
				TargetKey = TargetKey,
				Payload = Payload,
				BuildTruthSchema = BuildTruthSchema,
				BuildHasPlot = BuildHasPlot,
				BuildFrontier = BuildFrontier,
				BuildDefence = BuildDefence,
				CreatedTick = CreatedTick,
				StartedTick = StartedTick,
				DueTick = DueTick,
				UpdatedTick = UpdatedTick,
				Revision = Revision,
				Claims = Claims == null ? null : Claims.Copy(),
				Failure = Failure,
				Outbox = Outbox == null ? null : Outbox.Copy(),
				Compacted = Compacted,
				CompactHash = CompactHash
			};
		}
	}
}
