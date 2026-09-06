using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomChronicle
	{
		internal static bool RecordDeclaredOnce(KingdomSystem System,
			KingdomChronicleDeclaration Declaration)
		{
			return Declaration != null && RecordOnceCore(System, Declaration.EventId,
				Declaration.Text, Declaration.Accomplishment, Declaration.MuralText, Declaration);
		}

		private static bool RecordOnceCore(KingdomSystem System, string EventId, string Text,
			bool Accomplishment, string MuralText, KingdomChronicleDeclaration Declaration,
			long? AtTick = null, Func<bool> OwnerExact = null)
		{
			if (!PublicationAllowed(OwnerExact)
				|| AtTick.HasValue && (Accomplishment || MuralText != null || Declaration != null)) return false;
			string fingerprint;
			bool fingerprinted = AtTick.HasValue
				? TryAtFingerprint(System, EventId, Text, AtTick.Value, out fingerprint)
				: Declaration != null &&
				Declaration.AuthoredOutsiderText != null
				? KingdomChronicleReceiptRules.TryDisputedFingerprint(EventId,
					Declaration.Official, Declaration.Outsider, Accomplishment, MuralText,
					out fingerprint)
				: KingdomChronicleReceiptRules.TryFingerprint(EventId, Text, Accomplishment,
					MuralText, out fingerprint);
			if (System == null || The.Game == null || !fingerprinted || (Declaration != null &&
					(!string.Equals(Declaration.EventId, EventId, StringComparison.Ordinal) ||
					 !string.Equals(Declaration.Text, Text, StringComparison.Ordinal) ||
					 Declaration.Accomplishment != Accomplishment ||
					 !string.Equals(Declaration.MuralText, MuralText, StringComparison.Ordinal) ||
					 !string.Equals(Declaration.Fingerprint, fingerprint,
						 StringComparison.Ordinal)))) return false;
			if (!PublicationAllowed(OwnerExact)) return false;
			if (AtTick.HasValue && (System.ChronicleEntries == null || System.OutsiderEntries == null)) return false;
			System.ChronicleEntries = System.ChronicleEntries ?? new List<string>();
			System.OutsiderEntries = System.OutsiderEntries ?? new List<string>();
			if (System.ChronicleEntries.Count > MaxEntries || System.OutsiderEntries.Count > MaxEntries)
			{
				PublicationFault(KingdomChronicleRegistryFault.MalformedRow, "list-bound", true, OwnerExact);
				return false;
			}
			string raw;
			try
			{
				if (AtTick.HasValue)
				{
					if (!TryReadAtRegistry(out raw)) return false;
				}
				else raw = The.Game.GetStringGameState(EventRegistryState, "");
			}
			catch
			{
				PublicationFault(KingdomChronicleRegistryFault.MalformedRow, "registry-read", true, OwnerExact);
				return false;
			}
			List<KingdomChronicleReceipt> rows;
			bool migratedLegacy;
			KingdomChronicleRegistryFault fault;
			if (!KingdomChronicleReceiptRules.TryParseRegistry(raw, out rows,
				out migratedLegacy, out fault))
			{
				PublicationFault(fault, "registry-parse", true, OwnerExact);
				return false;
			}
			if (AtTick.HasValue && (migratedLegacy || !AtRegistryCanonical(raw, rows))) return false;
			if (migratedLegacy && (!PublicationAllowed(OwnerExact)
				|| !WriteEventReceipts(rows, "legacy-migration"))) return false;

			KingdomChronicleReceipt receipt = null;
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].EventId, EventId, StringComparison.Ordinal)) receipt = rows[i];
			if (receipt != null && receipt.LegacyBlocked)
			{
				string ignoredJob;
				string ignoredCoordinate;
				if (KingdomChronicleReceiptRules.TryConstructionIdentity(EventId,
					out ignoredJob, out ignoredCoordinate))
				{
					// v1 FNV data cannot authorize another append. Construction callers need
					// a terminal answer so an old ceremony job cannot remain pinned forever.
					PublicationFault(KingdomChronicleRegistryFault.None, "legacy-construction-lost", false, OwnerExact);
					return true;
				}
				PublicationFault(KingdomChronicleRegistryFault.None, "legacy-replay-blocked", true, OwnerExact);
				return false;
			}
			if (receipt != null && !string.Equals(receipt.Fingerprint, fingerprint,
				StringComparison.Ordinal))
			{
				PublicationFault(KingdomChronicleRegistryFault.DuplicateIdentity,
					"fingerprint-mismatch", true, OwnerExact);
				return false;
			}
			if (receipt != null && Declaration != null && !receipt.Compact &&
				(!string.Equals(receipt.Official, Declaration.Official, StringComparison.Ordinal) ||
				 !string.Equals(receipt.Outsider, Declaration.Outsider, StringComparison.Ordinal) ||
				 !string.Equals(receipt.OfficialBefore, Declaration.OfficialBefore,
					 StringComparison.Ordinal) ||
				 !string.Equals(receipt.OfficialAfter, Declaration.OfficialAfter,
					 StringComparison.Ordinal) ||
				 !string.Equals(receipt.OutsiderBefore, Declaration.OutsiderBefore,
					 StringComparison.Ordinal) ||
				 !string.Equals(receipt.OutsiderAfter, Declaration.OutsiderAfter,
					 StringComparison.Ordinal)))
			{
				PublicationFault(KingdomChronicleRegistryFault.DuplicateIdentity,
					"declaration-mismatch", true, OwnerExact);
				return false;
			}
			if (receipt != null && receipt.Compact)
				return PublicationAllowed(OwnerExact) && KingdomChronicleReceiptRules.IsTerminal(receipt);
			if (receipt != null && KingdomChronicleReceiptRules.IsTerminal(receipt))
				return PublicationAllowed(OwnerExact) && WriteEventReceipts(rows, "terminal-compaction");
			if (receipt == null)
			{
				// No receipt is ever evicted: terminal identity is permanent replay proof.
				if (rows.Count >= KingdomChronicleReceiptRules.MaxReceipts)
				{
					PublicationFault(KingdomChronicleRegistryFault.TooManyRows, "capacity", true, OwnerExact);
					return false;
				}
				KingdomChronicleDeclaration declaration = Declaration;
				if (declaration == null && !(AtTick.HasValue
					? TryDeclareCore(System, EventId, Text, null, false, null, out declaration, AtTick)
					: TryDeclareOnce(System, EventId, Text, Accomplishment, MuralText, out declaration)))
				{
					PublicationFault(KingdomChronicleRegistryFault.CryptoUnavailable,
						"receipt-declaration", true, OwnerExact);
					return false;
				}
				if (!PublicationAllowed(OwnerExact) || AtTick.HasValue
					&& !string.Equals(declaration.Fingerprint, fingerprint, StringComparison.Ordinal)) return false;
				if (!KingdomChronicleReceiptRules.TryHashList("official",
						System.ChronicleEntries, out string declaredOfficialBefore) ||
					!KingdomChronicleReceiptRules.TryHashAfter("official",
						System.ChronicleEntries, declaration.Official,
						out string declaredOfficialAfter) ||
					!KingdomChronicleReceiptRules.TryHashList("outsider",
						System.OutsiderEntries, out string declaredOutsiderBefore) ||
					!KingdomChronicleReceiptRules.TryHashAfter("outsider",
						System.OutsiderEntries, declaration.Outsider,
						out string declaredOutsiderAfter) ||
					!string.Equals(declaredOfficialBefore, declaration.OfficialBefore,
						StringComparison.Ordinal) ||
					!string.Equals(declaredOfficialAfter, declaration.OfficialAfter,
						StringComparison.Ordinal) ||
					!string.Equals(declaredOutsiderBefore, declaration.OutsiderBefore,
						StringComparison.Ordinal) ||
					!string.Equals(declaredOutsiderAfter, declaration.OutsiderAfter,
						StringComparison.Ordinal))
				{
					PublicationFault(KingdomChronicleRegistryFault.DuplicateIdentity,
						"declaration-list-mismatch", true, OwnerExact);
					return false;
				}
				receipt = new KingdomChronicleReceipt
				{
					EventId = EventId,
					Fingerprint = fingerprint,
					Official = declaration.Official,
					Outsider = declaration.Outsider,
					OfficialBefore = declaration.OfficialBefore,
					OfficialAfter = declaration.OfficialAfter,
					OutsiderBefore = declaration.OutsiderBefore,
					OutsiderAfter = declaration.OutsiderAfter,
					OfficialState = KingdomChronicleSinkDisposition.Pending,
					OutsiderState = KingdomChronicleSinkDisposition.Pending,
					JournalState = Accomplishment
						? KingdomChronicleSinkDisposition.Pending
						: KingdomChronicleSinkDisposition.Skipped,
					Updated = Now()
				};
				rows.Add(receipt);
				// Rendering invokes display-name callbacks; another event may have published meanwhile.
				if (!PublicationAllowed(OwnerExact) || AtTick.HasValue
					&& (!TryReadAtRegistry(out string currentRegistry)
						|| !string.Equals(currentRegistry, raw, StringComparison.Ordinal))
					|| !WriteEventReceipts(rows, "receipt-create")) return false;
			}
			if (!PublicationAllowed(OwnerExact) || !DeliverList(rows, receipt, System.ChronicleEntries, true)) return false;
			if (!PublicationAllowed(OwnerExact) || !DeliverList(rows, receipt, System.OutsiderEntries, false)) return false;
			if (!PublicationAllowed(OwnerExact) || !DeliverJournal(rows, receipt, Accomplishment, Text, MuralText)) return false;
			return PublicationAllowed(OwnerExact) && KingdomChronicleReceiptRules.IsTerminal(receipt);
		}

	}
}
