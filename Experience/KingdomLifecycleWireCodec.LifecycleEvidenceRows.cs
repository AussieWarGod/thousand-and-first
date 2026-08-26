using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteProjection(BinaryWriter w, KingdomLifecycleProjection x)
		{
			if (x == null) throw new InvalidDataException("null projection");
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.ObjectId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.ZoneId, false);
			w.Write((byte)x.Topology); S(w, x.OwnerId, true); w.Write(x.X); w.Write(x.Y);
			w.Write(x.Material); w.Write(x.Count); w.Write(x.NoStack); w.Write((byte)x.State);
			S(w, x.ReceiptId, false); S(w, x.ReceiptTopologyId, false);
			w.Write(x.ReceiptBeforeIdMatches); w.Write(x.ReceiptBeforeMarkerMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterIdMatches);
			w.Write(x.ReceiptAfterMarkerMatches); w.Write(x.ReceiptAfterCount);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState);
		}

		private static KingdomLifecycleProjection ReadProjection(BinaryReader r)
		{
			return new KingdomLifecycleProjection
			{
				OperationId = S(r, true), EventId = S(r, true), ObjectId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				Topology = (KingdomLifecycleTopology)r.ReadByte(), OwnerId = S(r, true),
				X = r.ReadInt32(), Y = r.ReadInt32(), Material = r.ReadInt32(),
					Count = r.ReadInt32(), NoStack = ReadExactBoolean(r),
					State = (KingdomLifecyclePhysicalState)r.ReadByte(),
					ReceiptId = S(r, false), ReceiptTopologyId = S(r, false),
					ReceiptBeforeIdMatches = r.ReadInt32(),
					ReceiptBeforeMarkerMatches = r.ReadInt32(),
					ReceiptBeforeCount = r.ReadInt32(),
					ReceiptAfterIdMatches = r.ReadInt32(),
					ReceiptAfterMarkerMatches = r.ReadInt32(),
					ReceiptAfterCount = r.ReadInt32(),
					ReceiptSameReference = ReadExactBoolean(r),
					ReceiptProofId = S(r, false),
					ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

		private static void WriteLease(BinaryWriter w, KingdomLifecycleResourceLease x)
		{
			if (x == null) throw new InvalidDataException("null lease");
			S(w, x.OperationId, true); w.Write((byte)x.Kind); S(w, x.ScopeId, true);
			S(w, x.SubjectId, true); S(w, x.Key, true); w.Write(x.Before); w.Write(x.Delta);
			w.Write(x.After); w.Write(x.BeforeRevision); w.Write(x.AfterRevision);
			w.Write((byte)x.State);
		}

		private static KingdomLifecycleResourceLease ReadLease(BinaryReader r)
		{
			return ReadLease(r, false);
		}

		private static KingdomLifecycleResourceLease ReadLease(BinaryReader r, bool legacyWire)
		{
			string operationId = S(r, true);
			byte rawKind = r.ReadByte();
			if (legacyWire && rawKind > (byte)KingdomLifecycleResourceKind.Raid)
				throw new InvalidDataException("legacy lifecycle lease kind is unsupported");
			return new KingdomLifecycleResourceLease
			{
				OperationId = operationId, Kind = (KingdomLifecycleResourceKind)rawKind,
				ScopeId = S(r, true), SubjectId = S(r, true), Key = S(r, true),
				Before = r.ReadInt64(), Delta = r.ReadInt64(), After = r.ReadInt64(),
				BeforeRevision = r.ReadInt64(), AfterRevision = r.ReadInt64(),
				State = (KingdomLifecycleLeaseState)r.ReadByte()
			};
		}

		private static void WriteResource(BinaryWriter w, KingdomLifecycleResourceRevision x)
		{
			if (x == null) throw new InvalidDataException("null resource row");
			w.Write((byte)x.Kind); S(w, x.ScopeId, true); S(w, x.SubjectId, true);
			S(w, x.Key, true); w.Write(x.Revision); S(w, x.ActiveOperationId, true);
			S(w, x.LastOperationId, true);
		}

		private static KingdomLifecycleResourceRevision ReadResource(BinaryReader r)
		{
			return ReadResource(r, false);
		}

		private static KingdomLifecycleResourceRevision ReadResource(BinaryReader r, bool legacyWire)
		{
			byte rawKind = r.ReadByte();
			if (legacyWire && rawKind > (byte)KingdomLifecycleResourceKind.Raid)
				throw new InvalidDataException("legacy lifecycle resource kind is unsupported");
			return new KingdomLifecycleResourceRevision
			{
				Kind = (KingdomLifecycleResourceKind)rawKind, ScopeId = S(r, true),
				SubjectId = S(r, true), Key = S(r, true), Revision = r.ReadInt64(),
				ActiveOperationId = S(r, true), LastOperationId = S(r, true)
			};
		}

		private static void WriteOutbox(BinaryWriter w, KingdomLifecycleOutbox x)
		{
			w.Write(x != null);
			if (x == null) return;
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.ChronicleReceiptId, true);
			S(w, x.Chronicle, false, true); w.Write(x.ChronicleAccomplishment);
			w.Write((byte)x.ChronicleDisposition); w.Write((byte)x.ChronicleState);
			S(w, x.Ledger, false, true); w.Write((byte)x.LedgerDisposition);
			w.Write((byte)x.LedgerState); S(w, x.Message, false, true);
			w.Write((byte)x.MessageDisposition); w.Write((byte)x.MessageState);
			S(w, x.Deed, false, true); w.Write((byte)x.DeedDisposition);
			w.Write((byte)x.DeedState); S(w, x.GuestbookLine, false, true);
			w.Write((byte)x.GuestbookDisposition); w.Write((byte)x.GuestbookState);
		}

		private static KingdomLifecycleOutbox ReadOutbox(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomLifecycleOutbox
			{
				OperationId = S(r, true), EventId = S(r, true), ChronicleReceiptId = S(r, true),
				Chronicle = S(r, false, true), ChronicleAccomplishment = ReadExactBoolean(r),
				ChronicleDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				ChronicleState = (KingdomLifecycleSinkState)r.ReadByte(),
				Ledger = S(r, false, true),
				LedgerDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				LedgerState = (KingdomLifecycleSinkState)r.ReadByte(), Message = S(r, false, true),
				MessageDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				MessageState = (KingdomLifecycleSinkState)r.ReadByte(), Deed = S(r, false, true),
				DeedDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				DeedState = (KingdomLifecycleSinkState)r.ReadByte(), GuestbookLine = S(r, false, true),
				GuestbookDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				GuestbookState = (KingdomLifecycleSinkState)r.ReadByte()
			};
		}

		private static void WriteProof(BinaryWriter w, KingdomLifecycleProof x)
		{
			if (x == null) throw new InvalidDataException("null proof");
			w.Write(x.Sequence); S(w, x.Id, true); S(w, x.PlanHash, true);
			w.Write((byte)x.Lane); w.Write((byte)x.Action); w.Write(x.Tick);
		}

		private static KingdomLifecycleProof ReadProof(BinaryReader r)
		{
			return new KingdomLifecycleProof
			{
				Sequence = r.ReadInt64(), Id = S(r, true), PlanHash = S(r, true),
				Lane = (KingdomLifecycleLane)r.ReadByte(),
				Action = (KingdomLifecycleAction)r.ReadByte(), Tick = r.ReadInt64()
			};
		}

	}
}
