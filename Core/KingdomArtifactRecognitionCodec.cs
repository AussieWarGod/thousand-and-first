using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static class KingdomArtifactRecognitionCodec
	{
		public const int WireVersion = 1;
		public const int MaxRowEncodedBytes = 4096;
		public const int BookHeaderBytes = 20;
		public const int MaxBookEncodedBytes = BookHeaderBytes +
			KingdomArtifactRecognitionRules.MaxRows * (4 + MaxRowEncodedBytes);

		public static byte[] Encode(KingdomArtifactRecognitionBook Book)
		{
			if (!KingdomArtifactRecognitionRules.TryValidate(Book, out string failure))
				throw new InvalidDataException(failure);
			using (MemoryStream m = new MemoryStream()) using (BinaryWriter w =
				new BinaryWriter(m, new UTF8Encoding(false, true), true))
			{
				w.Write(0x36524B54); w.Write(WireVersion); w.Write(Book.Revision);
				w.Write(Book.Rows.Count);
				for (int i = 0; i < Book.Rows.Count; i++)
				{
					byte[] row = EncodeRow(Book.Rows[i]); w.Write(row.Length); w.Write(row);
				}
				w.Flush(); byte[] bytes = m.ToArray();
				if (bytes.Length > MaxBookEncodedBytes) throw new InvalidDataException(
					"artifact recognition book exceeds its byte budget");
				return bytes;
			}
		}

		public static KingdomArtifactRecognitionBook Decode(byte[] Bytes)
		{
			try
			{
				if (Bytes == null || Bytes.Length > MaxBookEncodedBytes)
					throw new InvalidDataException("artifact recognition book exceeds its byte budget");
				using (MemoryStream m = new MemoryStream(Bytes, false))
				using (BinaryReader r = new BinaryReader(m, new UTF8Encoding(false, true), true))
				{
					if (r.ReadInt32() != 0x36524B54 || r.ReadInt32() != WireVersion)
						throw new InvalidDataException("unknown artifact recognition wire");
					KingdomArtifactRecognitionBook b = new KingdomArtifactRecognitionBook
						{ Revision = r.ReadInt64() };
					int count = r.ReadInt32(); if (count < 0 || count >
						KingdomArtifactRecognitionRules.MaxRows)
						throw new InvalidDataException("artifact recognition row cap exceeded");
					for (int i = 0; i < count; i++)
					{
						int size = r.ReadInt32(); if (size < 1 || size > MaxRowEncodedBytes)
							throw new InvalidDataException("artifact recognition row exceeds its byte budget");
						byte[] row = r.ReadBytes(size); if (row.Length != size)
							throw new EndOfStreamException();
						using (MemoryStream rm = new MemoryStream(row, false))
						using (BinaryReader rr = new BinaryReader(rm, new UTF8Encoding(false, true), true))
						{
							b.Rows.Add(Read(rr)); if (rm.Position != rm.Length)
								throw new InvalidDataException("trailing recognition row bytes");
						}
					}
					if (m.Position != m.Length)
						throw new InvalidDataException("trailing artifact recognition bytes");
					if (!KingdomArtifactRecognitionRules.TryValidate(b, out string failure))
						throw new InvalidDataException(failure);
					return b;
				}
			}
			catch (EndOfStreamException e) { throw new InvalidDataException("truncated recognition wire", e); }
			catch (DecoderFallbackException e) { throw new InvalidDataException("recognition wire is not strict UTF-8", e); }
		}

		private static byte[] EncodeRow(KingdomArtifactRecognitionReceipt Row)
		{
			using (MemoryStream m = new MemoryStream()) using (BinaryWriter w =
				new BinaryWriter(m, new UTF8Encoding(false, true), true))
			{
				Write(w, Row); w.Flush(); byte[] bytes = m.ToArray();
				if (bytes.Length > MaxRowEncodedBytes) throw new InvalidDataException(
					"artifact recognition row exceeds its byte budget");
				return bytes;
			}
		}

		private static void Write(BinaryWriter W, KingdomArtifactRecognitionReceipt X)
		{
			W.Write(X.Version); S(W, X.RecognitionId); W.Write((byte)X.Kind);
			S(W, X.Source.ObjectId); S(W, X.Source.Blueprint); S(W, X.Source.DisplayName);
			S(W, X.Source.OwnerId); S(W, X.Source.LocationId); S(W, X.Source.DeedId);
			S(W, X.Source.DeedText); W.Write(X.Source.ObservedTick); S(W, X.Source.SnapshotDigest);
			W.Write(X.AttributedResidentId); S(W, X.AttributionName); S(W, X.Text);
			W.Write(X.CommerceValue); W.Write(X.CustodyClaimed); W.Write(X.RecognizedTick);
		}

		private static KingdomArtifactRecognitionReceipt Read(BinaryReader R)
		{
			return new KingdomArtifactRecognitionReceipt { Version = R.ReadInt32(),
				RecognitionId = S(R), Kind = (KingdomArtifactRecognitionKind)R.ReadByte(),
				Source = new KingdomArtifactSnapshot { ObjectId = S(R), Blueprint = S(R),
					DisplayName = S(R), OwnerId = S(R), LocationId = S(R), DeedId = S(R),
					DeedText = S(R), ObservedTick = R.ReadInt64(), SnapshotDigest = S(R) },
				AttributedResidentId = R.ReadInt32(), AttributionName = S(R), Text = S(R),
				CommerceValue = R.ReadInt32(), CustodyClaimed = R.ReadBoolean(),
				RecognizedTick = R.ReadInt64() };
		}

		private static void S(BinaryWriter W, string V)
		{
			if (V == null) { W.Write(-1); return; }
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(V);
			if (bytes.Length > KingdomArtifactRecognitionRules.MaxDerivedTextBytes)
				throw new InvalidDataException("recognition string exceeds its cap");
			W.Write(bytes.Length); W.Write(bytes);
		}
		private static string S(BinaryReader R)
		{
			int size = R.ReadInt32(); if (size == -1) return null;
			if (size < 0 || size > KingdomArtifactRecognitionRules.MaxDerivedTextBytes)
				throw new InvalidDataException("recognition string exceeds its cap");
			byte[] bytes = R.ReadBytes(size); if (bytes.Length != size) throw new EndOfStreamException();
			return new UTF8Encoding(false, true).GetString(bytes);
		}
	}
}
