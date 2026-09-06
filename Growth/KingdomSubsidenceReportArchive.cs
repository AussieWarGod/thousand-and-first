using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Failed telling is not physical debt. Keep its full frozen evidence until the
	/// founder reads and acknowledges it; never evict an unread failure to make room.</summary>
	internal static class KingdomSubsidenceReportArchive
	{
		internal const string None = "sf1:none";
		internal const int MaxReports = 8;
		internal const int MaxWireChars = 262144;
		private const int Magic = 0x31465253;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool Valid(KingdomSubsidenceStepBook book)
		{
			if (book == null || !TryRead(book.FailureModel, out List<string> rows)) return false;
			if (book.Admission != KingdomSubsidenceAdmission.Admitted) return rows.Count == 0;
			foreach (string wire in rows)
				if (!KingdomSubsidenceReportCodec.TryDecode(wire, out KingdomSubsidenceReportPlan report)
					|| report.RealmId != book.RealmId || report.SettlementId != book.SettlementId) return false;
			return true;
		}

		internal static bool TryRetain(KingdomSubsidenceStepBook book, string reportWire,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Valid(book)) return false;
			if (reportWire == KingdomSubsidenceBatchRules.NoReport) { next = book; return true; }
			if (!KingdomSubsidenceReportCodec.TryDecode(reportWire, out KingdomSubsidenceReportPlan report)
				|| report.RealmId != book.RealmId || report.SettlementId != book.SettlementId
				|| !KingdomSubsidenceReportRules.Settled(report)) return false;
			if (!KingdomSubsidenceReportRules.HasLoss(report)) { next = book; return true; }
			if (!TryRead(book.FailureModel, out List<string> rows)) return false;
			foreach (string row in rows)
			{
				if (!KingdomSubsidenceReportCodec.TryDecode(row, out KingdomSubsidenceReportPlan prior)) return false;
				if (prior.OwnerId != report.OwnerId) continue;
				if (row != reportWire) return false;
				next = book; return true;
			}
			rows.Add(reportWire);
			if (!TryWrite(rows, out string wire)) return false;
			next = book.WithFailures(wire);
			return true;
		}

		internal static string Digest(KingdomSubsidenceStepBook book)
		{
			if (!Valid(book) || !TryRead(book.FailureModel, out List<string> rows)) return null;
			if (rows.Count == 0) return "";
			StringBuilder text = new StringBuilder("\n\n{{W|Unconfirmed subsidence reports}}\n"
				+ "Reading acknowledges this saved warning; delivery is not claimed. "
				+ "Completed departures and damage remain accounted for.\n");
			foreach (string wire in rows)
			{
				if (!KingdomSubsidenceReportCodec.TryDecode(wire, out KingdomSubsidenceReportPlan report)) return null;
				for (int i = 0; i < report.Entries.Count; i++)
				{
					KingdomSubsidenceReportEntry entry = report.Entries[i];
					if (entry.LedgerPhase != ReportLedgerPhase.Lost && !entry.ChronicleLost && !entry.CapacityRefused) continue;
					text.Append("\n").Append(entry.Text).Append("\nUnconfirmed: ");
					if (entry.LedgerPhase == ReportLedgerPhase.Lost) text.Append("homecoming ledger");
					if (entry.LedgerPhase == ReportLedgerPhase.Lost && (entry.ChronicleLost || entry.CapacityRefused)) text.Append(" and ");
					if (entry.ChronicleLost) text.Append("Chronicle");
					if (entry.CapacityRefused)
						text.Append("Chronicle not published: registry full; all ").Append(entry.CapacityCount)
							.Append(" replay receipts retained (registry ").Append(entry.CapacityHash).Append(")");
					text.Append(". Evidence: ").Append(KingdomSubsidenceReportRules.EventId(report, i)).Append("\n");
				}
			}
			return text.ToString();
		}

		internal static bool TryRead(string wire, out List<string> rows)
		{
			rows = null;
			if (wire == None) { rows = new List<string>(); return true; }
			if (string.IsNullOrEmpty(wire) || wire.Length > MaxWireChars
				|| !wire.StartsWith("sf1:", StringComparison.Ordinal)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(wire.Substring(4)), false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic) return false;
					int count = reader.ReadInt32();
					if (count < 1 || count > MaxReports) return false;
					List<string> value = new List<string>();
					for (int i = 0; i < count; i++) value.Add(reader.ReadString());
					if (stream.Position != stream.Length || !TryWrite(value, out string canonical)
						|| canonical != wire) return false;
					rows = value; return true;
				}
			}
			catch { return false; }
		}

		private static bool TryWrite(List<string> rows, out string wire)
		{
			wire = null;
			if (rows == null || rows.Count > MaxReports) return false;
			if (rows.Count == 0) { wire = None; return true; }
			HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal);
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic); writer.Write(rows.Count);
					foreach (string row in rows)
					{
						if (!KingdomSubsidenceReportCodec.TryDecode(row, out KingdomSubsidenceReportPlan report)
							|| !KingdomSubsidenceReportRules.Settled(report) || !KingdomSubsidenceReportRules.HasLoss(report)
							|| !owners.Add(report.OwnerId)) return false;
						writer.Write(row);
						if (stream.Length > MaxWireChars / 4 * 3 - 3) return false;
					}
					writer.Flush();
					string value = "sf1:" + Convert.ToBase64String(stream.ToArray());
					if (value.Length > MaxWireChars) return false;
					wire = value; return true;
				}
			}
			catch { return false; }
		}
	}
}
