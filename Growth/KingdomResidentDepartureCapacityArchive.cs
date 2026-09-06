using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal enum KingdomResidentDepartureChronicleDisposition : byte { CapacityRefused = 1 }

	/// <summary>Unread named-departure capacity evidence outlives its destructive journal.
	/// Rows retain their realm and settlement; only an exact attended read acknowledges them.</summary>
	internal static partial class KingdomResidentDepartureCapacityArchive
	{
		internal const string None = "dc1:none";
		internal const int MaximumWarnings = 8;
		internal const int MaximumWireChars = 524288;

		private sealed class Row
		{
			internal string Realm, Settlement, OperationId, Body, Zone, Name, Text, LedgerText, Fingerprint, Hash;
			internal int Resident, DeparturesBefore, Count;
			internal long Tick;
		}

		internal static bool CanAdmit(string wire)
		{
			return TryRead(wire, out List<Row> rows) && rows.Count < MaximumWarnings;
		}

		internal static bool CanAdmit(string wire, KingdomResidentDepartureOperation operation)
		{
			return CanAdmit(wire) && KingdomResidentDepartureRules.Valid(operation)
				&& operation.Phase == (int)KingdomResidentDeparturePhase.Prepared && operation.Chronicled
				&& Text(operation.BodyObjectId, 512) && Text(operation.ZoneId, 512) && Text(operation.ResidentName, 512)
				&& Text(operation.ChronicleLine, KingdomResidentDepartureRules.MaximumLineChars)
				&& Text(operation.LedgerLine, KingdomResidentDepartureRules.MaximumLineChars)
				&& Fingerprint(operation, out _);
		}

		internal static bool TryMatch(string wire, KingdomResidentDepartureOperation operation, out bool retained)
		{
			retained = false;
			if (!KingdomResidentDepartureRules.Valid(operation) || !TryRead(wire, out List<Row> rows)) return false;
			foreach (Row row in rows)
				if (row.OperationId == operation.OperationId)
				{
					if (!Matches(row, operation)) return false;
					retained = true;
				}
			return true;
		}

		/// <summary>The runtime re-proves registry and journal authority around this publication.
		/// A retained row settles only Chronicle non-publication; it grants no body authority.</summary>
		internal static bool TryRetain(string wire, KingdomResidentDepartureOperation operation,
			KingdomChronicleCapacityWitness witness, out string next)
		{
			next = null;
			if (!KingdomResidentDepartureRules.Valid(operation) || !operation.Chronicled
				|| operation.Phase != (int)KingdomResidentDeparturePhase.RolesClosed
				|| !Fingerprint(operation, out string fingerprint)
				|| !KingdomChronicleCapacityRules.Valid(witness, EventId(operation), fingerprint)
				|| !TryRead(wire, out List<Row> rows)) return false;
			foreach (Row row in rows)
				if (row.OperationId == operation.OperationId)
				{
					if (!Matches(row, operation) || row.Count != witness.RegistryCount || row.Hash != witness.RegistryHash) return false;
					next = wire; return true;
				}
			rows.Add(new Row
			{
				Realm = operation.RealmId, Settlement = operation.SettlementId, OperationId = operation.OperationId,
				Resident = operation.ResidentId, Body = operation.BodyObjectId, Zone = operation.ZoneId,
				Name = operation.ResidentName, Tick = operation.PreparedTick, DeparturesBefore = operation.DeparturesBefore,
				Text = operation.ChronicleLine, LedgerText = operation.LedgerLine,
				Fingerprint = fingerprint, Hash = witness.RegistryHash, Count = witness.RegistryCount
			});
			return TryWrite(rows, out next);
		}

		/// <summary>Read-only projection. The caller must show this digest under exact owner and
		/// wire reproof before publishing acknowledged; rows belonging elsewhere remain unchanged.</summary>
		internal static bool TryPrepareRead(string wire, string realm, string settlement,
			out string digest, out string acknowledged)
		{
			digest = null; acknowledged = null;
			if (!KingdomIdentityRules.IsRealmId(realm) || !KingdomIdentityRules.IsSettlementId(settlement)
				|| !TryRead(wire, out List<Row> rows)) return false;
			List<Row> kept = new List<Row>();
			StringBuilder text = new StringBuilder();
			foreach (Row row in rows)
			{
				if (row.Realm != realm || row.Settlement != settlement) { kept.Add(row); continue; }
				if (text.Length == 0) text.Append("\n\n{{W|Departures missing from the Chronicle}}\n"
					+ "The Chronicle's record registry is full. These departures and their population changes remain accounted for. "
					+ "Reading acknowledges these saved warnings; Chronicle delivery is not claimed.\n");
				text.Append("\n").Append(row.Text).Append(".\nChronicle not published; all ")
					.Append(row.Count.ToString(CultureInfo.InvariantCulture)).Append(" replay receipts retained.\n");
			}
			if (!TryWrite(kept, out acknowledged)) return false;
			digest = text.ToString(); return true;
		}

		internal static string EventId(KingdomResidentDepartureOperation operation)
		{ return operation.OperationId + ":chronicle"; }

		internal static bool Fingerprint(KingdomResidentDepartureOperation operation, out string fingerprint)
		{
			fingerprint = null;
			return operation != null && KingdomChronicleReceiptRules.TryFingerprint(EventId(operation),
				operation.ChronicleLine, false, null, out fingerprint);
		}

		private static bool Matches(Row row, KingdomResidentDepartureOperation operation)
		{
			return operation.Chronicled && row.Realm == operation.RealmId && row.Settlement == operation.SettlementId
				&& row.OperationId == operation.OperationId && row.Resident == operation.ResidentId
				&& row.Body == operation.BodyObjectId && row.Zone == operation.ZoneId && row.Name == operation.ResidentName
				&& row.Tick == operation.PreparedTick && row.DeparturesBefore == operation.DeparturesBefore
				&& row.Text == operation.ChronicleLine && row.LedgerText == operation.LedgerLine
				&& Fingerprint(operation, out string fingerprint) && fingerprint == row.Fingerprint;
		}

		private static bool Valid(Row row)
		{
			return row != null && KingdomIdentityRules.IsRealmId(row.Realm)
				&& KingdomIdentityRules.IsSettlementId(row.Settlement) && row.Resident > 0 && row.Tick >= 0
				&& row.DeparturesBefore >= 0 && Text(row.Body, 512) && Text(row.Zone, 512) && Text(row.Name, 512)
				&& Text(row.Text, KingdomResidentDepartureRules.MaximumLineChars)
				&& Text(row.LedgerText, KingdomResidentDepartureRules.MaximumLineChars)
				&& row.OperationId == KingdomResidentDepartureRules.Id(row.Realm, row.Settlement, row.Resident, row.Body, row.Tick)
				&& row.Count == KingdomChronicleReceiptRules.MaxReceipts && KingdomChronicleReceiptRules.IsSha256(row.Hash)
				&& KingdomChronicleReceiptRules.TryFingerprint(row.OperationId + ":chronicle", row.Text, false, null,
					out string fingerprint) && fingerprint == row.Fingerprint;
		}

		private static bool Text(string value, int maximum)
		{
			if (string.IsNullOrEmpty(value) || value.Length > maximum) return false;
			foreach (char character in value) if (char.IsControl(character)) return false;
			try { Utf8.GetByteCount(value); return true; } catch { return false; }
		}
	}
}
