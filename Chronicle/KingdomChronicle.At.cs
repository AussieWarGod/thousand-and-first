using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomChronicle
	{
		/// <summary>Dates a keyed telling at its frozen event tick. Existing receipts retain their
		/// first rendered declaration. True proves both registers delivered, never merely settled Lost.
		/// OwnerExact brackets declaration and core publication boundaries, not shared sink internals.</summary>
		internal static bool RecordOnceAt(KingdomSystem System, string EventId, string Text,
			long AtTick, Func<bool> OwnerExact = null)
		{
			try
			{
				var game = The.Game;
				if (game == null || System == null || AtTick < 0 || AtTick > game.TimeTicks
					|| !System.TryGetCurrentIdentity(out string realm, out string settlement)) return false;
				Func<bool> exact = () => ReferenceEquals(The.Game, game)
					&& System.TryGetCurrentIdentity(out string currentRealm, out string currentSettlement)
					&& realm == currentRealm && settlement == currentSettlement && PublicationAllowed(OwnerExact);
				return RecordOnceCore(System, EventId, Text, false, null, null, AtTick, exact)
					&& PublicationAllowed(exact) && TryProveOnceAt(System, EventId, Text, AtTick);
			}
			catch { return false; }
		}

		/// <summary>Read-only proof from an exact current receipt. Compaction may have rotated the
		/// original list entry; Delivered is durable publication evidence, not current list membership.</summary>
		internal static bool TryProveOnceAt(KingdomSystem System, string EventId, string Text, long AtTick)
		{
			try
			{
				if (!TryAtFingerprint(System, EventId, Text, AtTick, out string fingerprint)
					|| !TryReadAtRegistry(out string raw)
					|| !KingdomChronicleReceiptRules.TryParseRegistry(raw, out List<KingdomChronicleReceipt> rows,
						out bool migrated, out _) || migrated || !AtRegistryCanonical(raw, rows)) return false;
				foreach (KingdomChronicleReceipt receipt in rows)
					if (string.Equals(receipt.EventId, EventId, StringComparison.Ordinal))
						return !receipt.LegacyBlocked && receipt.Fingerprint == fingerprint
							&& receipt.OfficialState == KingdomChronicleSinkDisposition.Delivered
							&& receipt.OutsiderState == KingdomChronicleSinkDisposition.Delivered
							&& receipt.JournalState == KingdomChronicleSinkDisposition.Skipped;
				return false;
			}
			catch { return false; }
		}

		/// <summary>Loss is terminal evidence, never inferred from a failed publication call.
		/// Foreign fingerprints, malformed authority and still-pending sinks remain refusals.</summary>
		internal static bool TryProveLostOnceAt(KingdomSystem System, string EventId, string Text, long AtTick)
		{
			try
			{
				if (!TryAtFingerprint(System, EventId, Text, AtTick, out string fingerprint)
					|| !TryReadAtRegistry(out string raw)
					|| !KingdomChronicleReceiptRules.TryParseRegistry(raw, out List<KingdomChronicleReceipt> rows,
						out bool migrated, out _) || migrated || !AtRegistryCanonical(raw, rows)) return false;
				foreach (KingdomChronicleReceipt receipt in rows)
					if (string.Equals(receipt.EventId, EventId, StringComparison.Ordinal))
						return !receipt.LegacyBlocked && receipt.Fingerprint == fingerprint
							&& KingdomChronicleReceiptRules.IsTerminal(receipt)
							&& receipt.JournalState == KingdomChronicleSinkDisposition.Skipped
							&& (receipt.OfficialState == KingdomChronicleSinkDisposition.Lost
								|| receipt.OutsiderState == KingdomChronicleSinkDisposition.Lost);
				return false;
			}
			catch { return false; }
		}

		private static bool TryAtFingerprint(KingdomSystem System, string EventId, string Text,
			long AtTick, out string Fingerprint)
		{
			Fingerprint = null;
			if (System == null || The.Game == null || AtTick < 0 || AtTick > The.Game.TimeTicks
				|| string.IsNullOrEmpty(EventId) || !AtText(EventId, KingdomChronicleReceiptRules.MaxEventIdChars)
				|| !AtText(Text, KingdomChronicleReceiptRules.MaxEventTextChars)
				|| !System.TryGetCurrentIdentity(out string realm, out string settlement)) return false;
			return KingdomChronicleReceiptRules.TryCanonicalHash("taf-chronicle-at-v1",
				new[] { realm, settlement, EventId, Text, AtTick.ToString(CultureInfo.InvariantCulture) }, out Fingerprint);
		}

		private static bool AtText(string Value, int Limit)
		{
			if (Value == null || Value.Length > Limit) return false;
			for (int i = 0; i < Value.Length; i++)
				if (char.IsControl(Value[i])) return false;
			// TryCanonicalHash's strict UTF-8 encoder refuses unpaired surrogates.
			return true;
		}

		private static bool TryReadAtRegistry(out string Raw)
		{
			Raw = null;
			var game = The.Game;
			if (game == null) return false;
			KingdomDurableKeyObservation observed = new KingdomDurableKeyObservation
			{
				HasString = game.HasStringGameState(EventRegistryState),
				HasInt = game.HasIntGameState(EventRegistryState),
				HasInt64 = game.HasInt64GameState(EventRegistryState),
				HasObject = game.HasObjectGameState(EventRegistryState),
				HasBoolean = game.HasBooleanGameState(EventRegistryState)
			};
			if (observed.HasString) observed.String = game.GetStringGameState(EventRegistryState);
			if (!KingdomScenarioStateShape.TryAuthorityText(observed, out string value, out bool present, out _)) return false;
			Raw = present ? value : "";
			return ReferenceEquals(game, The.Game);
		}

		private static bool AtRegistryCanonical(string Raw, List<KingdomChronicleReceipt> Rows)
		{
			return Raw == "" && Rows.Count == 0 || KingdomChronicleReceiptRules.TryWriteRegistry(Rows,
				out string canonical, out _) && string.Equals(Raw, canonical, StringComparison.Ordinal);
		}

		private static bool PublicationAllowed(Func<bool> OwnerExact)
		{
			try { return OwnerExact == null || OwnerExact(); }
			catch { return false; }
		}

		private static void PublicationFault(KingdomChronicleRegistryFault Fault, string Context,
			bool PlayerVisible, Func<bool> OwnerExact)
		{
			if (PublicationAllowed(OwnerExact)) ReportFault(Fault, Context, PlayerVisible);
		}
	}
}
