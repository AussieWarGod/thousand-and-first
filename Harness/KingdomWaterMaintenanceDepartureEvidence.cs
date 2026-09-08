using System;
using System.Collections.Generic;
using System.Text;
using XRL;

namespace ThousandAndFirst.Harness
{
	// Durable publication proof, not founder notification or current Chronicle-list membership.
	internal static class KingdomWaterMaintenanceDepartureEvidence
	{
		private const string RegistryKey = "r_TAF_ChronicleEventRegistry_v1";
		private const int SummaryLimit = 12;

		internal static void Verify(KingdomSystem system, XRLGame game, KingdomLedger ledger,
			KingdomResidentDepartureOperation departure, Action prove, StringBuilder evidence, ref bool reported)
		{
			Require(system != null && game != null && ledger != null && prove != null && evidence != null,
				"departure publication observation lacks its retained owner");
			prove();
			Require(ReferenceEquals(The.Game, game) && ReferenceEquals(game.GetSystem<KingdomSystem>(), system)
				&& ReferenceEquals(system.Ledger, ledger) && KingdomResidentDepartureRules.Valid(departure)
				&& departure.Chronicled && departure.Phase == (int)KingdomResidentDeparturePhase.EffectsPublished,
				"departure publication observation lacks the captured completed operation");
			Require(system.TryGetCurrentIdentity(out string realm, out string settlement)
				&& realm == departure.RealmId && settlement == departure.SettlementId,
				"departure publication observation belongs to another realm or settlement");
			long tick = game.TimeTicks, turns = game.Turns, actions = game.ActionTicks, playerActions = game.PlayerActionTicks;
			string operation = departure.OperationId, text = departure.ChronicleLine, note = departure.LedgerLine;
			long prepared = departure.PreparedTick;
			Require(prepared <= tick, "departure publication observation precedes the event");
			string eventId = operation + ":chronicle";
			Require(KingdomChronicleReceiptRules.TryFingerprint(eventId, text, false, null, out string fingerprint),
				"departure RecordOnce fingerprint is not canonical");
			object[] tables = Tables(game);
			string raw = Read(game, tables);
			Require(KingdomChronicleReceiptRules.TryParseRegistry(raw, out List<KingdomChronicleReceipt> rows,
				out bool migrated, out _) && !migrated
				&& KingdomChronicleReceiptRules.TryWriteRegistry(rows, out string canonical, out _)
				&& string.Equals(raw, canonical, StringComparison.Ordinal),
				"departure Chronicle registry is malformed, migrated, or noncanonical");
			KingdomChronicleReceipt receipt = null;
			int matches = 0;
			foreach (KingdomChronicleReceipt row in rows)
				if (string.Equals(row.EventId, eventId, StringComparison.Ordinal)) { receipt = row; matches++; }
			Require(matches == 1 && receipt != null && !receipt.LegacyBlocked
				&& string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
				&& receipt.OfficialState == KingdomChronicleSinkDisposition.Delivered
				&& receipt.OutsiderState == KingdomChronicleSinkDisposition.Delivered
				&& receipt.JournalState == KingdomChronicleSinkDisposition.Skipped,
				"departure lacks one exact durable delivered Chronicle receipt; pending, Lost, and capacity refusal are not delivery");

			List<string> notes = ledger.Notes;
			Require(notes != null && notes.Count <= SummaryLimit, "ordinary ledger notes exceed their bounded summary contract");
			string[] summary = notes.ToArray();
			int exactNotes = 0;
			foreach (string line in summary)
			{
				Require(!string.IsNullOrEmpty(line), "ordinary ledger summary contains a malformed note");
				if (string.Equals(line, note, StringComparison.Ordinal)) exactNotes++;
			}
			Require(exactNotes == 1 || exactNotes == 0 && summary.Length == SummaryLimit,
				"departure ordinary summary is duplicated or absent without a saturated twelve-note lane");
			int departures = ledger.Departures, upkeep = ledger.UpkeepDrawn;
			prove();
			Require(ReferenceEquals(The.Game, game) && ReferenceEquals(game.GetSystem<KingdomSystem>(), system)
				&& ReferenceEquals(system.Ledger, ledger) && system.TryGetCurrentIdentity(out string currentRealm, out string currentSettlement)
				&& currentRealm == realm && currentSettlement == settlement
				&& game.TimeTicks == tick && game.Turns == turns && game.ActionTicks == actions && game.PlayerActionTicks == playerActions
				&& KingdomResidentDepartureRules.Valid(departure) && departure.Chronicled
				&& departure.Phase == (int)KingdomResidentDeparturePhase.EffectsPublished
				&& departure.OperationId == operation && departure.ChronicleLine == text && departure.LedgerLine == note
				&& departure.PreparedTick == prepared && departure.RealmId == realm && departure.SettlementId == settlement
				&& ledger.Departures == departures && ledger.UpkeepDrawn == upkeep && ReferenceEquals(ledger.Notes, notes)
				&& notes.Count == summary.Length, "departure read-only observation changed authority, accounting, clock, or note custody");
			for (int i = 0; i < summary.Length; i++)
				Require(string.Equals(notes[i], summary[i], StringComparison.Ordinal), "ordinary summary changed during departure observation");
			Require(string.Equals(raw, Read(game, tables), StringComparison.Ordinal), "departure registry changed during read-only proof");
			if (!reported)
			{
				evidence.Append("\ndeparture-Chronicle event=").Append(eventId).Append(" fingerprint=").Append(fingerprint)
					.Append("; exact-receipt=true; official=Delivered; outsider=Delivered; journal=Skipped; ordinary-summary-count=")
					.Append(summary.Length).Append(" exact-departure-notes=").Append(exactNotes)
					.Append(" summary-note=").Append(exactNotes == 0 ? "omitted-at-cap" : "present-once")
					.Append("; read-only=true; founder-notification-verified=false; current-list-membership-verified=false");
				reported = true;
			}
		}

		private static object[] Tables(XRLGame game)
		{
			object[] tables = { game.StringGameState, game.IntGameState, game.Int64GameState, game.ObjectGameState, game.BooleanGameState };
			foreach (object table in tables) Require(table != null, "Chronicle registry has an absent game-state table");
			return tables;
		}

		private static bool SameTables(XRLGame game, object[] tables)
		{
			return ReferenceEquals(The.Game, game) && ReferenceEquals(tables[0], game.StringGameState)
				&& ReferenceEquals(tables[1], game.IntGameState) && ReferenceEquals(tables[2], game.Int64GameState)
				&& ReferenceEquals(tables[3], game.ObjectGameState) && ReferenceEquals(tables[4], game.BooleanGameState);
		}

		private static string Read(XRLGame game, object[] tables)
		{
			Require(SameTables(game, tables), "Chronicle registry table authority changed");
			Require(game.HasStringGameState(RegistryKey) && !game.HasIntGameState(RegistryKey)
				&& !game.HasInt64GameState(RegistryKey) && !game.HasObjectGameState(RegistryKey) && !game.HasBooleanGameState(RegistryKey),
				"Chronicle registry is missing or occupies a foreign/duplicate game-state table");
			string raw = game.GetStringGameState(RegistryKey);
			Require(raw != null && raw.Length > 0 && raw.Length <= KingdomChronicleReceiptRules.MaxRegistryChars
				&& SameTables(game, tables), "Chronicle registry text is absent, oversized, or changed owners");
			return raw;
		}

		private static void Require(bool value, string failure)
		{ KingdomWaterMaintenanceNativeProvider.Require(value, failure); }
	}
}
