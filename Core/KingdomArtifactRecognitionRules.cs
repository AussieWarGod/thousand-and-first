using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static class KingdomArtifactRecognitionRules
	{
		public const int MaxRows = 8;
		public const int MaxTextBytes = 512;
		public const int MaxIdBytes = 128;
		public const int MaxDerivedTextBytes = 1200;

		public static bool TryRecognize(KingdomArtifactRecognitionBook Book,
			long ExpectedRevision, KingdomArtifactSnapshot Snapshot,
			KingdomArtifactRecognitionKind Kind, int AttributedResidentId,
			string AttributionName, long Tick,
			out KingdomArtifactRecognitionReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!TryValidate(Book, out Failure) || !ValidSnapshot(Snapshot) ||
				Kind < KingdomArtifactRecognitionKind.Remark ||
				Kind > KingdomArtifactRecognitionKind.Representation ||
				AttributedResidentId < 0 || AttributedResidentId == 0 !=
				string.IsNullOrEmpty(AttributionName) || AttributionName != null &&
				!Text(AttributionName) || Tick < Snapshot.ObservedTick)
				return Fail(Failure ?? "artifact recognition input is invalid", out Failure);
			string id = Id("taf:artifact-recognition:", Snapshot.SnapshotDigest,
				((byte)Kind).ToString(CultureInfo.InvariantCulture));
			// One subject, one recognition. The subject is the exact object identity, so an
			// original that is later moved, sold, renamed, or recognized in another form cannot
			// produce a second row about the same thing, and cannot rewrite the row it already
			// has. Two objects that merely share a display name keep distinct identities and
			// therefore remain distinct subjects.
			//
			// The retry is compared on what the row means, not on its digest. A digest covers the
			// tick it was read at, so a founder who opens the same unchanged object an hour later
			// produces a different digest and a different id for a thing that has not changed at
			// all; matching on the digest would call that a rewrite and refuse it. So every stable
			// field is compared and only a later reading of the same facts is tolerated. The row
			// handed back is the one already kept, at its own tick and its own id.
			for (int i = 0; i < Book.Rows.Count; i++)
			{
				if (Book.Rows[i].Source.ObjectId != Snapshot.ObjectId) continue;
				if (SameSubject(Book.Rows[i], Snapshot, Kind, AttributedResidentId, AttributionName)
					&& Snapshot.ObservedTick >= Book.Rows[i].Source.ObservedTick)
				{
					Receipt = Book.Rows[i]; return true;
				}
				return Fail("that exact object is already recognized and its record cannot be "
					+ "rewritten", out Failure);
			}
			if (Book.Revision != ExpectedRevision) return Fail("stale recognition revision", out Failure);
			if (Book.Rows.Count >= MaxRows) return Fail("recognition capacity is full", out Failure);
			KingdomArtifactRecognitionBook candidate = Clone(Book);
			Receipt = new KingdomArtifactRecognitionReceipt { RecognitionId = id, Kind = Kind,
				Source = Copy(Snapshot), AttributedResidentId = AttributedResidentId,
				AttributionName = AttributionName, Text = RecognitionText(Snapshot, Kind,
					AttributionName), CommerceValue = 0, CustodyClaimed = false,
				RecognizedTick = Tick };
			candidate.Rows.Add(Receipt); candidate.Rows.Sort((a, b) => string.CompareOrdinal(
				a.RecognitionId, b.RecognitionId)); candidate.Revision++;
			// A refused candidate leaves no receipt behind: an out parameter that survives a false
			// return is how a caller ends up reporting a row the book never took.
			if (!TryValidate(candidate, out Failure)) { Receipt = null; return false; }
			Book.Revision = candidate.Revision; Book.Rows = candidate.Rows;
			for (int i = 0; i < Book.Rows.Count; i++) if (Book.Rows[i].RecognitionId == id)
				Receipt = Book.Rows[i]; return true;
		}

		public static bool TryDescribe(KingdomArtifactRecognitionBook Book,
			string RecognitionId, out string Description, out string Failure)
		{
			Description = null;
			if (!TryValidate(Book, out Failure)) return false;
			for (int i = 0; i < Book.Rows.Count; i++)
				if (Book.Rows[i].RecognitionId == RecognitionId)
				{
					Description = Book.Rows[i].Text; return true;
				}
			return Fail("recognition receipt is absent", out Failure);
		}

		public static bool TryValidate(KingdomArtifactRecognitionBook Book, out string Failure)
		{
			Failure = null;
			if (Book == null || Book.Revision < 0 || Book.Rows == null || Book.Rows.Count > MaxRows)
				return Fail("recognition book is invalid", out Failure);
			string prior = null;
			for (int i = 0; i < Book.Rows.Count; i++)
			{
				KingdomArtifactRecognitionReceipt r = Book.Rows[i];
				if (r == null || r.Version != 1 || !ValidSnapshot(r.Source) || r.Kind <
					KingdomArtifactRecognitionKind.Remark || r.Kind >
					KingdomArtifactRecognitionKind.Representation || r.RecognitionId != Id(
					"taf:artifact-recognition:", r.Source.SnapshotDigest,
					((byte)r.Kind).ToString(CultureInfo.InvariantCulture)) || prior != null &&
					string.CompareOrdinal(prior, r.RecognitionId) >= 0 ||
					r.AttributedResidentId < 0 || r.AttributedResidentId == 0 !=
					string.IsNullOrEmpty(r.AttributionName) || r.AttributionName != null &&
					!Text(r.AttributionName) || r.Text != RecognitionText(r.Source, r.Kind,
						r.AttributionName) || !Utf8(r.Text, MaxDerivedTextBytes) ||
					r.CommerceValue != 0 || r.CustodyClaimed ||
					r.RecognizedTick < r.Source.ObservedTick)
					return Fail("recognition row is invalid", out Failure);
				prior = r.RecognitionId;
			}
			return true;
		}

		public static string SnapshotDigest(KingdomArtifactSnapshot S)
		{
			return Hash(S?.ObjectId, S?.Blueprint, S?.DisplayName, S?.OwnerId,
				S?.LocationId, S?.DeedId, S?.DeedText, S == null ? null :
				S.ObservedTick.ToString(CultureInfo.InvariantCulture));
		}

		private static bool ValidSnapshot(KingdomArtifactSnapshot S)
		{
			return S != null && IdText(S.ObjectId) && Text(S.Blueprint) && Text(S.DisplayName) &&
				Optional(S.OwnerId) && IdText(S.LocationId) && Optional(S.DeedId) &&
				OptionalText(S.DeedText) && (S.DeedId == null) == (S.DeedText == null) &&
				S.ObservedTick >= 0 && Digest(S.SnapshotDigest) &&
				S.SnapshotDigest == SnapshotDigest(S);
		}

		private static string RecognitionText(KingdomArtifactSnapshot S,
			KingdomArtifactRecognitionKind K, string Attribution)
		{
			string lead = string.IsNullOrEmpty(Attribution) ? "The city records" :
				Attribution + " records";
			string form = K == KingdomArtifactRecognitionKind.Remark ? "a remark on" :
				K == KingdomArtifactRecognitionKind.Inscription ? "an inscription for" :
				"a fixed representation of";
			return lead + " " + form + " " + S.DisplayName + (S.DeedText == null ? "." :
				", remembered for " + S.DeedText + ".");
		}

		/// <summary>
		/// Whether a request says the same thing as a row already kept.
		/// <para>
		/// Every field that carries meaning is compared. The two deliberately left out are the
		/// observation tick and the digest computed over it, because those differ between two
		/// honest readings of an object that has not moved, changed hands, or been renamed. The
		/// caller decides what to do about the tick; this only answers whether the facts agree.
		/// </para>
		/// </summary>
		private static bool SameSubject(KingdomArtifactRecognitionReceipt R,
			KingdomArtifactSnapshot S, KingdomArtifactRecognitionKind K, int Resident,
			string Name)
		{
			return R.Kind == K && R.Source.ObjectId == S.ObjectId &&
				R.Source.Blueprint == S.Blueprint && R.Source.DisplayName == S.DisplayName &&
				R.Source.OwnerId == S.OwnerId && R.Source.LocationId == S.LocationId &&
				R.Source.DeedId == S.DeedId && R.Source.DeedText == S.DeedText &&
				R.AttributedResidentId == Resident && R.AttributionName == Name;
		}

		private static KingdomArtifactSnapshot Copy(KingdomArtifactSnapshot S)
		{
			return new KingdomArtifactSnapshot { ObjectId = S.ObjectId, Blueprint = S.Blueprint,
				DisplayName = S.DisplayName, OwnerId = S.OwnerId, LocationId = S.LocationId,
				DeedId = S.DeedId, DeedText = S.DeedText, ObservedTick = S.ObservedTick,
				SnapshotDigest = S.SnapshotDigest };
		}
		private static KingdomArtifactRecognitionBook Clone(KingdomArtifactRecognitionBook B) =>
			KingdomArtifactRecognitionCodec.Decode(KingdomArtifactRecognitionCodec.Encode(B));

		private static string Id(string Prefix, params string[] P) { return Prefix + Hash(P); }
		private static string Hash(params string[] P)
		{
			try
			{
				using (MemoryStream m = new MemoryStream()) using (BinaryWriter w =
					new BinaryWriter(m, new UTF8Encoding(false, true), true))
				{
					for (int i = 0; i < P.Length; i++) w.Write(P[i] ?? ""); w.Flush();
					using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(
						sha.ComputeHash(m.ToArray())).Replace("-", "").ToLowerInvariant();
				}
			}
			catch (EncoderFallbackException) { return null; }
		}
		private static bool Digest(string V) { return V != null && V.Length == 64 && V == V.ToLowerInvariant() && Array.TrueForAll(V.ToCharArray(), c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'); }
		private static bool IdText(string V) { return V != null && V.Length > 4 && V.StartsWith("taf:", StringComparison.Ordinal) && Utf8(V, MaxIdBytes); }
		private static bool Optional(string V) { return V == null || IdText(V); }
		private static bool Text(string V) { return !string.IsNullOrWhiteSpace(V) && Utf8(V, MaxTextBytes); }
		private static bool OptionalText(string V) { return V == null || Text(V); }
		private static bool Utf8(string V, int MaxBytes) { try { return V != null && V.IndexOf('\0') < 0 && new UTF8Encoding(false, true).GetByteCount(V) <= MaxBytes; } catch (EncoderFallbackException) { return false; } }
		private static bool Fail(string T, out string F) { F = T; return false; }
	}
}
