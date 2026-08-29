using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	/// <summary>The bounded facts persisted when one site reading is adopted.</summary>
	[Serializable]
	public sealed class KingdomSiteEvidenceSnapshot
	{
		public string SettlementId;
		public string Vocation;
		public string Style;
		public string Terrain;
		public string Region;
		public string Creed;
		public string WorkReceiptId;
		public string DeedReceiptId;
		public string WorkText;
		public string DeedText;
		public string Digest;
		public long FoundedTick;
		public long ObservedTick;
	}

	[Serializable]
	public sealed class KingdomSitePracticeReceipt
	{
		public int Version = 1;
		public string PracticeId;
		public KingdomSiteEvidenceSnapshot Source;
		public int Reading;
		public string EvidenceTagA;
		public string EvidenceTagB;
		public string Title;
		public string Description;
		public long ChosenTick;
	}

	[Serializable]
	public sealed class KingdomSitePracticeBook
	{
		public long Revision;
		public List<KingdomSitePracticeReceipt> Rows =
			new List<KingdomSitePracticeReceipt>();
	}

	/// <summary>Validated immutable-city facts used only to construct a preview.</summary>
	public sealed class KingdomSiteFoundingEvidence
	{
		public string SettlementId;
		public string Vocation;
		public string Style;
		public string Terrain;
		public string Region;
		public string Creed;
		public string DeedReceiptId;
		public string DeedText;
		public long FoundedTick;
	}

	/// <summary>One exact, current-city, completed physical work candidate.</summary>
	public sealed class KingdomSiteBuiltWorkEvidence
	{
		public string SettlementId;
		public string ZoneId;
		public string ObjectId;
		public string DesignKey;
		public string WorkReceiptId;
		public string DisplayName;
		public long CompletedTick;
	}

	/// <summary>Read-only D1 choice. It owns no practice row and changes no vocation.</summary>
	public sealed class KingdomSitePracticePreview
	{
		public KingdomSiteEvidenceSnapshot Snapshot;
		public string SourceSummary;
		public string FirstTitle;
		public string FirstReading;
		public string SecondTitle;
		public string SecondReading;
		public string VocationNotice;
	}

	/// <summary>Wire-v1 site-book rows, kept beside their exact model.</summary>
	public static partial class KingdomCivicPracticeCodec
	{
		private static byte[] EncodeSites(KingdomSitePracticeBook book)
		{
			string failure;
			if (!KingdomSitePracticeRules.TryValidate(book, out failure))
				throw new InvalidDataException(failure);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				WriteHeader(writer, book.Revision, book.Rows.Count);
				for (int i = 0; i < book.Rows.Count; i++)
					WriteRow(writer, book.Rows[i], WriteSite);
				writer.Flush();
				return Cap(stream.ToArray(), MaxSiteBookBytes);
			}
		}

		private static KingdomSitePracticeBook DecodeSites(byte[] bytes)
		{
			using (MemoryStream stream = new MemoryStream(bytes, false))
			using (BinaryReader reader = Reader(stream))
			{
				ReadHeader(reader, out long revision, out int count,
					KingdomSitePracticeRules.MaxRows);
				KingdomSitePracticeBook book = new KingdomSitePracticeBook
					{ Revision = revision };
				for (int i = 0; i < count; i++) book.Rows.Add(ReadRow(reader, ReadSite));
				string failure = null;
				bool valid = KingdomSitePracticeRules.TryValidate(book, out failure);
				if (stream.Position != stream.Length || !valid)
					throw new InvalidDataException(failure ?? "trailing site bytes");
				return book;
			}
		}

		private static void WriteSite(BinaryWriter writer, KingdomSitePracticeReceipt row)
		{
			writer.Write(row.Version); WriteString(writer, row.PracticeId);
			KingdomSiteEvidenceSnapshot source = row.Source;
			WriteString(writer, source.SettlementId); WriteString(writer, source.Vocation);
			WriteString(writer, source.Style); WriteString(writer, source.Terrain);
			WriteString(writer, source.Region); WriteString(writer, source.Creed);
			WriteString(writer, source.WorkReceiptId); WriteString(writer, source.DeedReceiptId);
			WriteString(writer, source.WorkText); WriteString(writer, source.DeedText);
			writer.Write(source.FoundedTick); writer.Write(source.ObservedTick);
			WriteString(writer, source.Digest); writer.Write(row.Reading);
			WriteString(writer, row.EvidenceTagA); WriteString(writer, row.EvidenceTagB);
			WriteString(writer, row.Title); WriteString(writer, row.Description);
			writer.Write(row.ChosenTick);
		}

		private static KingdomSitePracticeReceipt ReadSite(BinaryReader reader)
		{
			KingdomSitePracticeReceipt row = new KingdomSitePracticeReceipt
				{ Version = reader.ReadInt32(), PracticeId = ReadString(reader),
				Source = new KingdomSiteEvidenceSnapshot() };
			KingdomSiteEvidenceSnapshot source = row.Source;
			source.SettlementId = ReadString(reader); source.Vocation = ReadString(reader);
			source.Style = ReadString(reader); source.Terrain = ReadString(reader);
			source.Region = ReadString(reader); source.Creed = ReadString(reader);
			source.WorkReceiptId = ReadString(reader);
			source.DeedReceiptId = ReadString(reader);
			source.WorkText = ReadString(reader); source.DeedText = ReadString(reader);
			source.FoundedTick = reader.ReadInt64(); source.ObservedTick = reader.ReadInt64();
			source.Digest = ReadString(reader); row.Reading = reader.ReadInt32();
			row.EvidenceTagA = ReadString(reader); row.EvidenceTagB = ReadString(reader);
			row.Title = ReadString(reader); row.Description = ReadString(reader);
			row.ChosenTick = reader.ReadInt64();
			return row;
		}
	}
}
