using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementCodec
	{
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		private static byte[] WriteState(KingdomRealmRetirementState State)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream, Utf8))
			{
				w.Write(State.Version); w.Write((byte)State.Phase); w.Write(State.Revision);
				S(w, State.ReceiptId); S(w, State.RealmId); S(w, State.FactionId);
				S(w, State.GameId); w.Write(State.RealmIncarnation); w.Write(State.StartedTick);
				w.Write(State.UpdatedTick); S(w, State.AuthorityDigest); S(w, State.Fault ?? "");
				w.Write(State.Locators.Count);
				for (int i = 0; i < State.Locators.Count; i++)
				{
					KingdomRemovalLocator row = State.Locators[i];
					S(w, row.ZoneId); S(w, row.SettlementId); w.Write((byte)row.State);
					w.Write(row.Revision); w.Write(row.CleanedTick); w.Write(row.ObjectCount);
					S(w, row.EvidenceDigest);
				}
				w.Write(State.Records.Count);
				for (int i = 0; i < State.Records.Count; i++)
				{
					KingdomRemovalRecord row = State.Records[i];
					w.Write((byte)row.Kind); S(w, row.Id); w.Write((byte)row.Disposition);
					S(w, row.BeforeDigest); S(w, row.AfterDigest); w.Write(row.Amount);
					S(w, row.Detail ?? "");
				}
				w.Flush(); return stream.ToArray();
			}
		}

		private static KingdomRealmRetirementState ReadState(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream, Utf8))
			{
				KingdomRealmRetirementState state = new KingdomRealmRetirementState
				{
					Version = r.ReadInt32(), Phase = (KingdomRealmRetirementPhase)r.ReadByte(),
					Revision = r.ReadInt32(), ReceiptId = S(r, false), RealmId = S(r, false),
					FactionId = S(r, false), GameId = S(r, false),
					RealmIncarnation = r.ReadInt64(), StartedTick = r.ReadInt64(),
					UpdatedTick = r.ReadInt64(), AuthorityDigest = S(r, false), Fault = S(r, false)
				};
				int locators = Count(r, KingdomRealmRetirementState.MaxLocators);
				for (int i = 0; i < locators; i++)
					state.Locators.Add(new KingdomRemovalLocator
					{
						ZoneId = S(r, false), SettlementId = S(r, true),
						State = (KingdomRemovalLocatorState)r.ReadByte(), Revision = r.ReadInt32(),
						CleanedTick = r.ReadInt64(), ObjectCount = r.ReadInt32(),
						EvidenceDigest = S(r, true)
					});
				int records = Count(r, KingdomRealmRetirementState.MaxRecords);
				for (int i = 0; i < records; i++)
					state.Records.Add(new KingdomRemovalRecord
					{
						Kind = (KingdomRemovalProjectionKind)r.ReadByte(), Id = S(r, false),
						Disposition = (KingdomRemovalDisposition)r.ReadByte(),
						BeforeDigest = S(r, true), AfterDigest = S(r, true),
						Amount = r.ReadInt64(), Detail = S(r, false)
					});
				if (stream.Position != stream.Length) throw new InvalidDataException("trailing payload");
				return state;
			}
		}

		private static byte[] WriteFence(KingdomIdentityFence Fence)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter w = new BinaryWriter(stream, Utf8))
			{
				w.Write(Fence.Version); w.Write(Fence.Revision); S(w, Fence.GameId);
				w.Write(Fence.NextRealmIncarnation); S(w, Fence.LastRealmId);
				S(w, Fence.LastRealmDigest); S(w, Fence.TombstoneChainDigest);
					S(w, Fence.PreparedFromDigest);
					w.Write((byte)Fence.Disposition); S(w, Fence.PendingTransactionId);
					w.Write(Fence.PendingIncarnation); S(w, Fence.PreparedReceiptDigest);
					w.Flush(); return stream.ToArray();
			}
		}

		private static KingdomIdentityFence ReadFence(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader r = new BinaryReader(stream, Utf8))
			{
				KingdomIdentityFence fence = new KingdomIdentityFence
				{
					Version = r.ReadInt32(), Revision = r.ReadInt32(), GameId = S(r, false),
					NextRealmIncarnation = r.ReadInt64(), LastRealmId = S(r, true),
					LastRealmDigest = S(r, true), TombstoneChainDigest = S(r, true),
					PreparedFromDigest = S(r, true),
					Disposition = (KingdomIdentityFenceDisposition)r.ReadByte(),
						PendingTransactionId = S(r, true), PendingIncarnation = r.ReadInt64()
					};
					// Version-2 operational fences written before the C2 terminal binding are additive.
					// A legacy prepared fence remains invalid: it cannot prove which receipt cut it.
					if (stream.Position < stream.Length) fence.PreparedReceiptDigest = S(r, true);
				if (stream.Position != stream.Length) throw new InvalidDataException("trailing payload");
				return fence;
			}
		}

		private static void S(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			byte[] bytes = Utf8.GetBytes(Value);
			if (bytes.Length > 4096) throw new InvalidDataException("string cap");
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static string S(BinaryReader Reader, bool NullAllowed)
		{
			int count = Reader.ReadInt32();
			if (count == -1 && NullAllowed) return null;
			if (count < 0 || count > 4096) throw new InvalidDataException("string length");
			byte[] bytes = Reader.ReadBytes(count);
			if (bytes.Length != count) throw new EndOfStreamException();
			return Utf8.GetString(bytes);
		}

		private static int Count(BinaryReader Reader, int Max)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > Max) throw new InvalidDataException("row count");
			return count;
		}
	}
}
