using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static class KingdomWitnessWorkRules
	{
		public const string RaisingAdapterKind = "taf:adapter:witness-raising:v1";
		public const int MaxRows = 8;
		public const int MaxTextBytes = 512;
		public const int MaxIdBytes = 128;
		public const int MaxDerivedTextBytes = 1200;

		public static bool TryCapture(KingdomWitnessWorkBook Book, long ExpectedRevision,
			KingdomWitnessWorkSource Source, out KingdomWitnessWorkReceipt Receipt,
			out string Failure)
		{
			Receipt = null; Failure = null;
			if (!TryValidate(Book, out Failure) || !ValidSource(Source)) return false;
			string workId = Id("taf:experience:witness-work:", Source.EventId,
				Source.EventKind);
			for (int i = 0; i < Book.Rows.Count; i++) if (Book.Rows[i].WorkId == workId)
			{
				Receipt = Book.Rows[i]; return ExactSource(Receipt.Source, Source) ||
					Fail("witness work id collides with another source", out Failure);
			}
			if (Book.Revision != ExpectedRevision) return Fail("stale witness work revision", out Failure);
			if (Book.Rows.Count >= MaxRows) return Fail("witness work capacity is full", out Failure);
			KingdomWitnessWorkBook candidate = Clone(Book);
			Receipt = new KingdomWitnessWorkReceipt { Phase = KingdomWitnessWorkPhase.Captured,
				WorkId = workId, Source = Copy(Source), Description = Description(Source),
				Fixed = true, Portable = false, CommerceValue = 0,
				ChangedTick = Source.ClosedTick };
			candidate.Rows.Add(Receipt); candidate.Rows.Sort((a, b) => string.CompareOrdinal(a.WorkId, b.WorkId));
			candidate.Revision++; return Publish(Book, candidate, Receipt.WorkId, out Receipt, out Failure);
		}

		public static bool TryPrepareCarrier(KingdomWitnessWorkBook Book, long ExpectedRevision,
			string WorkId, string ObjectId, string ZoneId, string ConstructionReceiptId,
			int X, int Y, long Tick, out string Failure)
		{
			Failure = null; KingdomWitnessWorkReceipt row = Find(Book, WorkId);
			if (!TryValidate(Book, out Failure) || row == null || !IdText(ObjectId) ||
				!IdText(ZoneId) || !IdText(ConstructionReceiptId) || X < 0 || Y < 0
				|| Tick < row.Source.ClosedTick) return Fail(Failure ??
				"carrier preparation is invalid", out Failure);
			string receipt = CarrierReceiptId(WorkId, ObjectId, ZoneId,
				ConstructionReceiptId, X, Y);
			if (receipt == null) return Fail("carrier receipt identity is invalid", out Failure);
			if (row.Phase == KingdomWitnessWorkPhase.CarrierPrepared ||
				row.Phase == KingdomWitnessWorkPhase.Projected)
				return row.CarrierReceiptId == receipt && row.CarrierObjectId == ObjectId &&
					row.CarrierZoneId == ZoneId
					&& row.CarrierConstructionReceiptId == ConstructionReceiptId
					&& row.CarrierX == X && row.CarrierY == Y
					|| Fail("carrier retry changed identity", out Failure);
			if (row.Phase != KingdomWitnessWorkPhase.Captured || Book.Revision != ExpectedRevision)
				return Fail("witness work is not carrier-preparable", out Failure);
			KingdomWitnessWorkBook candidate = Clone(Book); row = Find(candidate, WorkId);
			row.Phase = KingdomWitnessWorkPhase.CarrierPrepared; row.CarrierReceiptId = receipt;
			row.CarrierObjectId = ObjectId; row.CarrierZoneId = ZoneId;
			row.CarrierConstructionReceiptId = ConstructionReceiptId;
			row.CarrierX = X; row.CarrierY = Y; row.ChangedTick = Tick;
			candidate.Revision++; return Publish(Book, candidate, null, out _, out Failure);
		}

		public static bool TryDecline(KingdomWitnessWorkBook Book, long ExpectedRevision,
			string WorkId, long Tick, out string Failure)
		{
			Failure = null; KingdomWitnessWorkReceipt row = Find(Book, WorkId);
			if (!TryValidate(Book, out Failure) || row == null)
				return Fail(Failure ?? "witness work is absent", out Failure);
			if (row.Phase == KingdomWitnessWorkPhase.Declined) return true;
			if (row.Phase != KingdomWitnessWorkPhase.Captured
				|| Book.Revision != ExpectedRevision || Tick < row.ChangedTick)
				return Fail("witness work decline is invalid", out Failure);
			KingdomWitnessWorkBook candidate = Clone(Book); row = Find(candidate, WorkId);
			row.Phase = KingdomWitnessWorkPhase.Declined; row.ChangedTick = Tick;
			candidate.Revision++;
			return Publish(Book, candidate, null, out _, out Failure);
		}

		public static bool TryCommitCarrier(KingdomWitnessWorkBook Book, long ExpectedRevision,
			string WorkId, string ReceiptId, long Tick, out string Failure)
		{
			Failure = null; KingdomWitnessWorkReceipt row = Find(Book, WorkId);
			if (!TryValidate(Book, out Failure) || row == null || row.CarrierReceiptId != ReceiptId)
				return Fail(Failure ?? "carrier receipt is foreign", out Failure);
			if (row.Phase == KingdomWitnessWorkPhase.Projected) return true;
			if (row.Phase != KingdomWitnessWorkPhase.CarrierPrepared ||
				Book.Revision != ExpectedRevision || Tick < row.ChangedTick)
				return Fail("carrier commit is invalid", out Failure);
			KingdomWitnessWorkBook candidate = Clone(Book); row = Find(candidate, WorkId);
			row.Phase = KingdomWitnessWorkPhase.Projected; row.ChangedTick = Tick;
			candidate.Revision++; return Publish(Book, candidate, null, out _, out Failure);
		}

		public static bool TryReconcileCarrier(KingdomWitnessWorkBook Book, long ExpectedRevision,
			string WorkId, bool ExactCarrierPresent, bool Teardown, long Tick, out string Failure)
		{
			Failure = null; KingdomWitnessWorkReceipt row = Find(Book, WorkId);
			if (!TryValidate(Book, out Failure) || row == null) return false;
			KingdomWitnessWorkPhase next = Teardown ? KingdomWitnessWorkPhase.Removed :
				ExactCarrierPresent ? row.Phase : KingdomWitnessWorkPhase.Lost;
			if (row.Phase == next || row.Phase == KingdomWitnessWorkPhase.Removed) return true;
			if ((row.Phase != KingdomWitnessWorkPhase.CarrierPrepared &&
				row.Phase != KingdomWitnessWorkPhase.Projected) || Book.Revision != ExpectedRevision ||
				Tick < row.ChangedTick) return Fail("carrier reconciliation is invalid", out Failure);
			KingdomWitnessWorkBook candidate = Clone(Book); row = Find(candidate, WorkId);
			row.Phase = next; row.ChangedTick = Tick; candidate.Revision++;
			return Publish(Book, candidate, null, out _, out Failure);
		}

		public static bool TryValidate(KingdomWitnessWorkBook Book, out string Failure)
		{
			Failure = null;
			if (Book == null || Book.Revision < 0 || Book.Rows == null || Book.Rows.Count > MaxRows)
				return Fail("witness work book is invalid", out Failure);
			string prior = null;
			for (int i = 0; i < Book.Rows.Count; i++)
			{
				KingdomWitnessWorkReceipt r = Book.Rows[i];
				if (r == null || r.Version != 1 || !ValidSource(r.Source) || r.WorkId !=
					Id("taf:experience:witness-work:", r.Source.EventId, r.Source.EventKind) ||
					(prior != null && string.CompareOrdinal(prior, r.WorkId) >= 0) ||
					r.Description != Description(r.Source) || !Utf8(r.Description,
						MaxDerivedTextBytes) || !r.Fixed || r.Portable ||
					r.CommerceValue != 0 || r.ChangedTick < r.Source.ClosedTick ||
					!ValidPhase(r)) return Fail("witness work row is invalid", out Failure);
				prior = r.WorkId;
			}
			return true;
		}

		private static bool ValidPhase(KingdomWitnessWorkReceipt R)
		{
			bool carrier = IdText(R.CarrierReceiptId) && IdText(R.CarrierObjectId) &&
				IdText(R.CarrierZoneId) && IdText(R.CarrierConstructionReceiptId)
				&& R.CarrierX >= 0 && R.CarrierY >= 0
				&& R.CarrierReceiptId == CarrierReceiptId(R.WorkId, R.CarrierObjectId,
					R.CarrierZoneId, R.CarrierConstructionReceiptId, R.CarrierX, R.CarrierY);
			if (R.Phase == KingdomWitnessWorkPhase.Captured
				|| R.Phase == KingdomWitnessWorkPhase.Declined) return !carrier &&
				R.CarrierReceiptId == null && R.CarrierObjectId == null && R.CarrierZoneId == null
				&& R.CarrierConstructionReceiptId == null && R.CarrierX == -1 && R.CarrierY == -1
				&& string.IsNullOrEmpty(R.Fault);
			return R.Phase >= KingdomWitnessWorkPhase.CarrierPrepared &&
				R.Phase <= KingdomWitnessWorkPhase.Lost && carrier && string.IsNullOrEmpty(R.Fault);
		}

		private static bool ValidSource(KingdomWitnessWorkSource S)
		{
			return S != null && IdText(S.EventId) && IdText(S.SettlementId) &&
				Text(S.EventKind) && S.EventKind.IndexOf("death", StringComparison.OrdinalIgnoreCase) < 0 &&
				Text(S.EventText) && S.ClosedTick >= 0 && S.MakerResidentId > 0 &&
				Text(S.MakerName) && Digest(S.SnapshotDigest) &&
				S.SnapshotDigest == SnapshotDigest(S);
		}

		public static string SnapshotDigest(KingdomWitnessWorkSource S)
		{
			return Hash(S?.EventId, S?.SettlementId, S?.EventKind, S?.EventText,
				S == null ? null : S.ClosedTick.ToString(CultureInfo.InvariantCulture),
				S == null ? null : S.MakerResidentId.ToString(CultureInfo.InvariantCulture), S?.MakerName);
		}

		private static string Description(KingdomWitnessWorkSource S)
		{
			return "On civic tick " + S.ClosedTick.ToString(CultureInfo.InvariantCulture)
				+ ", " + S.MakerName + " made this fixed account: " + S.EventText + ".";
		}

		private static KingdomWitnessWorkReceipt Find(KingdomWitnessWorkBook B, string IdValue)
		{
			if (B?.Rows == null) return null;
			for (int i = 0; i < B.Rows.Count; i++) if (B.Rows[i]?.WorkId == IdValue) return B.Rows[i];
			return null;
		}
		private static KingdomWitnessWorkBook Clone(KingdomWitnessWorkBook B) =>
			KingdomWitnessWorkCodec.Decode(KingdomWitnessWorkCodec.Encode(B));
		private static bool Publish(KingdomWitnessWorkBook Target, KingdomWitnessWorkBook Candidate,
			string ReceiptId, out KingdomWitnessWorkReceipt Receipt, out string Failure)
		{
			Receipt = null; if (!TryValidate(Candidate, out Failure)) return false;
			Target.Revision = Candidate.Revision; Target.Rows = Candidate.Rows;
			Receipt = Find(Target, ReceiptId); return true;
		}

		private static KingdomWitnessWorkSource Copy(KingdomWitnessWorkSource S)
		{
			return new KingdomWitnessWorkSource { EventId = S.EventId, SettlementId = S.SettlementId,
				EventKind = S.EventKind, EventText = S.EventText, ClosedTick = S.ClosedTick,
				MakerResidentId = S.MakerResidentId, MakerName = S.MakerName,
				SnapshotDigest = S.SnapshotDigest };
		}

		private static bool ExactSource(KingdomWitnessWorkSource A, KingdomWitnessWorkSource B)
		{
			return A != null && B != null && A.SnapshotDigest == B.SnapshotDigest
				&& A.EventId == B.EventId && A.SettlementId == B.SettlementId
				&& A.EventKind == B.EventKind && A.EventText == B.EventText
				&& A.ClosedTick == B.ClosedTick && A.MakerResidentId == B.MakerResidentId
				&& A.MakerName == B.MakerName;
		}

		public static KingdomWitnessWorkReceipt FindExact(KingdomWitnessWorkBook Book,
			string WorkId)
		{
			KingdomWitnessWorkReceipt row = Find(Book, WorkId);
			return row == null ? null : KingdomWitnessWorkCodec.Decode(
				KingdomWitnessWorkCodec.Encode(new KingdomWitnessWorkBook
					{ Rows = new List<KingdomWitnessWorkReceipt> { row } })).Rows[0];
		}

		internal static string CarrierReceiptId(string WorkId, string ObjectId,
			string ZoneId, string ConstructionReceiptId, int X, int Y)
		{
			if (!IdText(WorkId) || !IdText(ObjectId) || !IdText(ZoneId)
				|| !IdText(ConstructionReceiptId) || X < 0 || Y < 0) return null;
			return Id("taf:experience:witness-carrier:", WorkId, ObjectId, ZoneId,
				ConstructionReceiptId, X.ToString(CultureInfo.InvariantCulture),
				Y.ToString(CultureInfo.InvariantCulture));
		}

		internal static string ProjectionProof(int Version, string RealmId, string SettlementId,
			string WorkId, string SourceDigest, string CarrierReceiptIdValue,
			string CarrierObjectId, string CarrierEngineId, string CarrierZoneId,
			string ConstructionReceiptId, int X, int Y, string Description)
		{
			return Id("taf:experience:witness-marker-proof:",
				Version.ToString(CultureInfo.InvariantCulture), RealmId, SettlementId, WorkId,
				SourceDigest, CarrierReceiptIdValue, CarrierObjectId, CarrierEngineId,
				CarrierZoneId, ConstructionReceiptId, X.ToString(CultureInfo.InvariantCulture),
				Y.ToString(CultureInfo.InvariantCulture), Description);
		}

		private static string Id(string Prefix, params string[] Parts)
		{
			string digest = Hash(Parts);
			return digest == null ? null : Prefix + digest;
		}
		private static string Hash(params string[] Parts)
		{
			try
			{
				using (MemoryStream m = new MemoryStream()) using (BinaryWriter w =
					new BinaryWriter(m, new UTF8Encoding(false, true), true))
				{
					for (int i = 0; i < Parts.Length; i++) w.Write(Parts[i] ?? ""); w.Flush();
					using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(m.ToArray()));
				}
			}
			catch (EncoderFallbackException) { return null; }
		}
		private static string Hex(byte[] B) { return BitConverter.ToString(B).Replace("-", "").ToLowerInvariant(); }
		private static bool Digest(string V) { return V != null && V.Length == 64 && V == V.ToLowerInvariant() && Array.TrueForAll(V.ToCharArray(), c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'); }
		private static bool IdText(string V) { return V != null && V.Length > 4 && V.StartsWith("taf:", StringComparison.Ordinal) && Utf8(V, MaxIdBytes); }
		private static bool Text(string V) { return !string.IsNullOrWhiteSpace(V) && Utf8(V, MaxTextBytes); }
		private static bool Utf8(string V, int MaxBytes) { try { return V != null && V.IndexOf('\0') < 0 && new UTF8Encoding(false, true).GetByteCount(V) <= MaxBytes; } catch (EncoderFallbackException) { return false; } }
		private static bool Fail(string TextValue, out string Failure) { Failure = TextValue; return false; }
	}
}
