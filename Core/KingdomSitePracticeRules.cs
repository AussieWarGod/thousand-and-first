using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace ThousandAndFirst
{
	/// <summary>Pure D1 snapshot, reading, retry, capacity, and presentation law.</summary>
	public static class KingdomSitePracticeRules
	{
		public const int MaxRows = 8;
		public const int MaxIdBytes = 128;
		public const int MaxTextBytes = 512;
		public static string SnapshotDigest(KingdomSiteEvidenceSnapshot snapshot)
		{
			return Hash(snapshot?.SettlementId, snapshot?.Vocation, snapshot?.Style,
				snapshot?.Terrain, snapshot?.Region, snapshot?.Creed,
				snapshot?.WorkReceiptId, snapshot?.DeedReceiptId, snapshot?.WorkText,
				snapshot?.DeedText, Number(snapshot?.FoundedTick),
				Number(snapshot?.ObservedTick));
		}
		/// <summary>Selects the ordinal-first exact work and freezes a stable two-reading view.</summary>
		public static bool TryBuildPreview(KingdomSiteFoundingEvidence founding,
			IList<KingdomSiteBuiltWorkEvidence> works,
			out KingdomSitePracticePreview preview, out string failure)
		{
			preview = null;
			failure = null;
			if (!ValidFounding(founding) || !TrySelectWork(
				founding.SettlementId, works, out KingdomSiteBuiltWorkEvidence work,
				out failure))
				return Fail(failure ?? "site founding evidence is incomplete", out failure);
			string workText = work.DisplayName + " (" + work.DesignKey + ") in " + work.ZoneId;
			if (!Text(workText)) return Fail("site work description exceeds its bound", out failure);
			KingdomSiteEvidenceSnapshot snapshot = new KingdomSiteEvidenceSnapshot
			{
				SettlementId = founding.SettlementId,
				Vocation = founding.Vocation,
				Style = founding.Style,
				Terrain = founding.Terrain,
				Region = founding.Region,
				Creed = founding.Creed,
				WorkReceiptId = work.WorkReceiptId,
				DeedReceiptId = founding.DeedReceiptId,
				WorkText = workText,
				DeedText = founding.DeedText,
				FoundedTick = founding.FoundedTick,
				ObservedTick = Math.Max(founding.FoundedTick, work.CompletedTick)
			};
			snapshot.Digest = SnapshotDigest(snapshot);
			if (!TryPreview(snapshot, out string first, out string second, out failure)) return false;
			preview = new KingdomSitePracticePreview
			{
				Snapshot = snapshot,
				SourceSummary = "Source: " + snapshot.Terrain + " in " + snapshot.Region +
					"; " + snapshot.WorkText + "; " + snapshot.DeedText + ".",
				FirstTitle = Title(1),
				FirstReading = first,
				SecondTitle = Title(2),
				SecondReading = second,
				VocationNotice = "This signature complements the explicit " +
					snapshot.Vocation + " vocation; it never changes it."
			};
			return Text(preview.SourceSummary) && Text(preview.VocationNotice)
				|| Fail("site preview prose exceeds its bound", out failure);
		}
		public static bool TryRead(KingdomSitePracticeBook book, long expectedRevision,
			KingdomSiteEvidenceSnapshot snapshot, int reading, long tick,
			out KingdomSitePracticeReceipt receipt, out string failure)
		{
			receipt = null;
			failure = null;
			if (!TryValidate(book, out failure) || !ValidSnapshot(snapshot) || reading < 1 ||
				reading > 2 || tick < snapshot.ObservedTick)
				return Fail(failure ?? "site evidence or reading is invalid", out failure);
			string id = PracticeId(snapshot, reading);
			for (int i = 0; i < book.Rows.Count; i++)
			{
				if (SameSnapshot(book.Rows[i].Source, snapshot) && book.Rows[i].Reading != reading)
					return Fail("site evidence already has its chosen reading", out failure);
				if (book.Rows[i].PracticeId != id) continue;
				receipt = book.Rows[i];
				return SameSnapshot(receipt.Source, snapshot) && receipt.Reading == reading
					|| Fail("site practice retry changed its evidence", out failure);
			}
			if (book.Revision != expectedRevision)
				return Fail("stale site practice revision", out failure);
			if (book.Rows.Count >= MaxRows)
				return Fail("site practice capacity is full", out failure);
			KingdomSitePracticeBook candidate = Copy(book);
			string tagA = Tag(snapshot.Terrain, snapshot.Region);
			string tagB = Tag(snapshot.Style, snapshot.Creed ?? snapshot.Vocation);
			KingdomSitePracticeReceipt created = new KingdomSitePracticeReceipt
			{
				PracticeId = id,
				Source = Copy(snapshot),
				Reading = reading,
				EvidenceTagA = tagA,
				EvidenceTagB = tagB,
				Title = Title(reading),
				Description = Render(snapshot, reading, tagA, tagB),
				ChosenTick = tick
			};
			candidate.Rows.Add(created);
			candidate.Rows.Sort((left, right) => string.CompareOrdinal(
				left.PracticeId, right.PracticeId));
			candidate.Revision++;
			if (!TryValidate(candidate, out failure)) return false;
			book.Revision = candidate.Revision;
			book.Rows = candidate.Rows;
			receipt = Find(book, id);
			return receipt != null;
		}
		public static bool TryPreview(KingdomSiteEvidenceSnapshot snapshot,
			out string first, out string second, out string failure)
		{
			first = null;
			second = null;
			failure = null;
			if (!ValidSnapshot(snapshot)) return Fail("site evidence is incomplete", out failure);
			string tagA = Tag(snapshot.Terrain, snapshot.Region);
			string tagB = Tag(snapshot.Style, snapshot.Creed ?? snapshot.Vocation);
			first = Render(snapshot, 1, tagA, tagB);
			second = Render(snapshot, 2, tagA, tagB);
			return true;
		}
		public static bool TryValidate(KingdomSitePracticeBook book, out string failure)
		{
			failure = null;
			if (book == null || book.Revision < 0 || book.Rows == null || book.Rows.Count > MaxRows)
				return Fail("site practice book is invalid", out failure);
			string prior = null;
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomSitePracticeReceipt row = book.Rows[i];
				if (row == null || row.Version != 1 || !ValidSnapshot(row.Source) ||
					row.Reading < 1 || row.Reading > 2 ||
					row.PracticeId != PracticeId(row.Source, row.Reading) ||
					prior != null && string.CompareOrdinal(prior, row.PracticeId) >= 0 ||
					row.EvidenceTagA != Tag(row.Source.Terrain, row.Source.Region) ||
					row.EvidenceTagB != Tag(row.Source.Style, row.Source.Creed ?? row.Source.Vocation) ||
					row.Title != Title(row.Reading) || row.Description != Render(row.Source,
						row.Reading, row.EvidenceTagA, row.EvidenceTagB) ||
					row.ChosenTick < row.Source.ObservedTick)
					return Fail("site practice row is invalid", out failure);
				prior = row.PracticeId;
			}
			return true;
		}
		private static bool TrySelectWork(string settlementId,
			IList<KingdomSiteBuiltWorkEvidence> works,
			out KingdomSiteBuiltWorkEvidence selected, out string failure)
		{
			selected = null;
			failure = null;
			if (works == null || works.Count == 0)
				return Fail("no exact completed city work is available", out failure);
			List<KingdomSiteBuiltWorkEvidence> ordered = new List<KingdomSiteBuiltWorkEvidence>();
			for (int i = 0; i < works.Count; i++)
			{
				if (!ValidWork(works[i]) || works[i].SettlementId != settlementId)
					return Fail("site work evidence is invalid or belongs to another city", out failure);
				ordered.Add(works[i]);
			}
			ordered.Sort(CompareWork);
			for (int i = 1; i < ordered.Count; i++)
				if (ordered[i - 1].WorkReceiptId == ordered[i].WorkReceiptId)
					return Fail("one work receipt identifies multiple candidates", out failure);
			selected = ordered[0];
			return true;
		}
		private static int CompareWork(KingdomSiteBuiltWorkEvidence left,
			KingdomSiteBuiltWorkEvidence right)
		{
			int order = string.CompareOrdinal(left.WorkReceiptId, right.WorkReceiptId);
			if (order != 0) return order;
			order = string.CompareOrdinal(left.ObjectId, right.ObjectId);
			return order != 0 ? order : string.CompareOrdinal(left.ZoneId, right.ZoneId);
		}
		private static bool ValidFounding(KingdomSiteFoundingEvidence value)
		{
			return value != null && Id(value.SettlementId) && Text(value.Vocation) &&
				Text(value.Style) && Text(value.Terrain) && Text(value.Region) &&
				Optional(value.Creed) && Id(value.DeedReceiptId) && Text(value.DeedText) &&
				value.FoundedTick >= 0;
		}
		private static bool ValidWork(KingdomSiteBuiltWorkEvidence value)
		{
			return value != null && Id(value.SettlementId) && Text(value.ZoneId) &&
				Id(value.ObjectId) && Text(value.DesignKey) && Id(value.WorkReceiptId) &&
				Text(value.DisplayName) && value.CompletedTick >= 0;
		}
		private static bool ValidSnapshot(KingdomSiteEvidenceSnapshot value)
		{
			return value != null && Id(value.SettlementId) && Text(value.Vocation) &&
				Text(value.Style) && Text(value.Terrain) && Text(value.Region) &&
				Optional(value.Creed) && Id(value.WorkReceiptId) && Id(value.DeedReceiptId) &&
				Text(value.WorkText) && Text(value.DeedText) && value.FoundedTick >= 0 &&
				value.ObservedTick >= value.FoundedTick && Digest(value.Digest) &&
				value.Digest == SnapshotDigest(value);
		}
		private static bool SameSnapshot(KingdomSiteEvidenceSnapshot left,
			KingdomSiteEvidenceSnapshot right)
		{
			return left != null && right != null && left.SettlementId == right.SettlementId &&
				left.Vocation == right.Vocation && left.Style == right.Style &&
				left.Terrain == right.Terrain && left.Region == right.Region &&
				left.Creed == right.Creed && left.WorkReceiptId == right.WorkReceiptId &&
				left.DeedReceiptId == right.DeedReceiptId && left.WorkText == right.WorkText &&
				left.DeedText == right.DeedText && left.FoundedTick == right.FoundedTick &&
				left.ObservedTick == right.ObservedTick && left.Digest == right.Digest;
		}
		private static string PracticeId(KingdomSiteEvidenceSnapshot snapshot, int reading)
		{
			return "taf:site-practice:" + Hash(snapshot.Digest, Number(reading));
		}
		private static string Title(int reading)
		{
			return reading == 1 ? "Keep the local account" :
				"Set the founding beside later work";
		}
		private static string Render(KingdomSiteEvidenceSnapshot snapshot, int reading,
			string tagA, string tagB)
		{
			return reading == 1
				? snapshot.SettlementId + " keeps a visible practice of " + tagA +
					" beside " + tagB + "."
				: snapshot.WorkText + " is read beside " + snapshot.DeedText +
					"; the chosen vocation remains " + snapshot.Vocation + ".";
		}
		private static string Tag(string first, string second) { return first + " / " + second; }
		private static KingdomSitePracticeBook Copy(KingdomSitePracticeBook source)
		{
			KingdomSitePracticeBook copy = new KingdomSitePracticeBook { Revision = source.Revision };
			for (int i = 0; i < source.Rows.Count; i++)
			{
				KingdomSitePracticeReceipt row = source.Rows[i];
				copy.Rows.Add(new KingdomSitePracticeReceipt { Version = row.Version,
					PracticeId = row.PracticeId, Source = Copy(row.Source), Reading = row.Reading,
					EvidenceTagA = row.EvidenceTagA, EvidenceTagB = row.EvidenceTagB,
					Title = row.Title, Description = row.Description, ChosenTick = row.ChosenTick });
			}
			return copy;
		}
		private static KingdomSiteEvidenceSnapshot Copy(KingdomSiteEvidenceSnapshot source)
		{
			return new KingdomSiteEvidenceSnapshot { SettlementId = source.SettlementId,
				Vocation = source.Vocation, Style = source.Style, Terrain = source.Terrain,
				Region = source.Region, Creed = source.Creed, WorkReceiptId = source.WorkReceiptId,
				DeedReceiptId = source.DeedReceiptId, WorkText = source.WorkText,
				DeedText = source.DeedText, FoundedTick = source.FoundedTick,
				ObservedTick = source.ObservedTick, Digest = source.Digest };
		}
		private static KingdomSitePracticeReceipt Find(KingdomSitePracticeBook book, string id)
		{
			for (int i = 0; i < book.Rows.Count; i++) if (book.Rows[i].PracticeId == id) return book.Rows[i];
			return null;
		}
		private static string Number(long? value)
		{
			return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "";
		}
		private static string Hash(params string[] parts)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{
					for (int i = 0; i < parts.Length; i++) writer.Write(parts[i] ?? "");
					writer.Flush();
					using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(
						sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
				}
			}
			catch (EncoderFallbackException) { return null; }
		}
		private static bool Utf8(string value, int maxBytes)
		{
			try { return value != null && value.IndexOf('\0') < 0 &&
				new UTF8Encoding(false, true).GetByteCount(value) <= maxBytes; }
			catch (EncoderFallbackException) { return false; }
		}
		private static bool Id(string value) { return value != null &&
			value.StartsWith("taf:", StringComparison.Ordinal) && Utf8(value, MaxIdBytes); }
		private static bool Text(string value) { return !string.IsNullOrWhiteSpace(value) &&
			Utf8(value, MaxTextBytes); }
		private static bool Optional(string value) { return value == null || Text(value); }
		private static bool Digest(string value) { return value != null && value.Length == 64 &&
			Array.TrueForAll(value.ToCharArray(), c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'); }
		private static bool Fail(string value, out string failure) { failure = value; return false; }
	}
}
