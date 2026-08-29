using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static class KingdomWitnessWorkCodec
	{
		public const int WireVersion = 1;
		public const int MaxRowEncodedBytes = 4096;
		public const int BookHeaderBytes = 20;
		public const int MaxBookEncodedBytes = BookHeaderBytes +
			KingdomWitnessWorkRules.MaxRows * (4 + MaxRowEncodedBytes);

		public static byte[] Encode(KingdomWitnessWorkBook Book)
		{
			if (!KingdomWitnessWorkRules.TryValidate(Book, out string failure))
				throw new InvalidDataException(failure);
			using (MemoryStream m = new MemoryStream()) using (BinaryWriter w =
				new BinaryWriter(m, new UTF8Encoding(false, true), true))
			{
				w.Write(0x35574B54); w.Write(WireVersion); w.Write(Book.Revision);
				w.Write(Book.Rows.Count);
				for (int i = 0; i < Book.Rows.Count; i++)
				{
					byte[] row = EncodeRow(Book.Rows[i]); w.Write(row.Length); w.Write(row);
				}
				w.Flush(); byte[] bytes = m.ToArray();
				if (bytes.Length > MaxBookEncodedBytes) throw new InvalidDataException(
					"witness work book exceeds its byte budget");
				return bytes;
			}
		}

		public static KingdomWitnessWorkBook Decode(byte[] Bytes)
		{
			try
			{
				if (Bytes == null || Bytes.Length > MaxBookEncodedBytes)
					throw new InvalidDataException("witness work book exceeds its byte budget");
				using (MemoryStream m = new MemoryStream(Bytes, false))
				using (BinaryReader r = new BinaryReader(m, new UTF8Encoding(false, true), true))
				{
					if (r.ReadInt32() != 0x35574B54 || r.ReadInt32() != WireVersion)
						throw new InvalidDataException("unknown witness work wire");
					KingdomWitnessWorkBook b = new KingdomWitnessWorkBook { Revision = r.ReadInt64() };
					int count = r.ReadInt32(); if (count < 0 || count > KingdomWitnessWorkRules.MaxRows)
						throw new InvalidDataException("witness work row cap exceeded");
					for (int i = 0; i < count; i++)
					{
						int size = r.ReadInt32(); if (size < 1 || size > MaxRowEncodedBytes)
							throw new InvalidDataException("witness work row exceeds its byte budget");
						byte[] row = r.ReadBytes(size); if (row.Length != size)
							throw new EndOfStreamException();
						using (MemoryStream rm = new MemoryStream(row, false))
						using (BinaryReader rr = new BinaryReader(rm, new UTF8Encoding(false, true), true))
						{
							b.Rows.Add(Read(rr)); if (rm.Position != rm.Length)
								throw new InvalidDataException("trailing witness row bytes");
						}
					}
					if (m.Position != m.Length)
						throw new InvalidDataException("trailing witness work bytes");
					if (!KingdomWitnessWorkRules.TryValidate(b, out string failure))
						throw new InvalidDataException(failure);
					return b;
				}
			}
			catch (EndOfStreamException e) { throw new InvalidDataException("truncated witness work wire", e); }
			catch (DecoderFallbackException e) { throw new InvalidDataException("witness work wire is not strict UTF-8", e); }
		}

		private static byte[] EncodeRow(KingdomWitnessWorkReceipt Row)
		{
			using (MemoryStream m = new MemoryStream()) using (BinaryWriter w =
				new BinaryWriter(m, new UTF8Encoding(false, true), true))
			{
				Write(w, Row); w.Flush(); byte[] bytes = m.ToArray();
				if (bytes.Length > MaxRowEncodedBytes) throw new InvalidDataException(
					"witness work row exceeds its byte budget");
				return bytes;
			}
		}

		private static void Write(BinaryWriter W, KingdomWitnessWorkReceipt X)
		{
			W.Write(X.Version); W.Write((byte)X.Phase); S(W, X.WorkId); S(W, X.Source.EventId);
			S(W, X.Source.SettlementId); S(W, X.Source.EventKind); S(W, X.Source.EventText);
			W.Write(X.Source.ClosedTick); W.Write(X.Source.MakerResidentId); S(W, X.Source.MakerName);
			S(W, X.Source.SnapshotDigest); S(W, X.Description); S(W, X.CarrierReceiptId);
			S(W, X.CarrierObjectId); S(W, X.CarrierZoneId);
			S(W, X.CarrierConstructionReceiptId); W.Write(X.CarrierX); W.Write(X.CarrierY);
			W.Write(X.Fixed); W.Write(X.Portable);
			W.Write(X.CommerceValue); W.Write(X.ChangedTick); S(W, X.Fault);
		}

		private static KingdomWitnessWorkReceipt Read(BinaryReader R)
		{
			return new KingdomWitnessWorkReceipt { Version = R.ReadInt32(),
				Phase = (KingdomWitnessWorkPhase)R.ReadByte(), WorkId = S(R),
				Source = new KingdomWitnessWorkSource { EventId = S(R), SettlementId = S(R),
					EventKind = S(R), EventText = S(R), ClosedTick = R.ReadInt64(),
					MakerResidentId = R.ReadInt32(), MakerName = S(R), SnapshotDigest = S(R) },
				Description = S(R), CarrierReceiptId = S(R), CarrierObjectId = S(R),
				CarrierZoneId = S(R), CarrierConstructionReceiptId = S(R),
				CarrierX = R.ReadInt32(), CarrierY = R.ReadInt32(),
				Fixed = R.ReadBoolean(), Portable = R.ReadBoolean(),
				CommerceValue = R.ReadInt32(), ChangedTick = R.ReadInt64(), Fault = S(R) };
		}

		private static void S(BinaryWriter W, string V)
		{
			if (V == null) { W.Write(-1); return; }
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(V);
			if (bytes.Length > KingdomWitnessWorkRules.MaxDerivedTextBytes)
				throw new InvalidDataException("witness work string exceeds its cap");
			W.Write(bytes.Length); W.Write(bytes);
		}
		private static string S(BinaryReader R)
		{
			int size = R.ReadInt32(); if (size == -1) return null;
			if (size < 0 || size > KingdomWitnessWorkRules.MaxDerivedTextBytes)
				throw new InvalidDataException("witness work string exceeds its cap");
			byte[] bytes = R.ReadBytes(size); if (bytes.Length != size) throw new EndOfStreamException();
			return new UTF8Encoding(false, true).GetString(bytes);
		}
	}
}
