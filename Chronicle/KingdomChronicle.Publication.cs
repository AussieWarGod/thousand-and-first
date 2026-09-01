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
			bool Accomplishment, string MuralText, KingdomChronicleDeclaration Declaration)
		{
			string fingerprint;
			bool fingerprinted = Declaration != null &&
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
			System.ChronicleEntries = System.ChronicleEntries ?? new List<string>();
			System.OutsiderEntries = System.OutsiderEntries ?? new List<string>();
			if (System.ChronicleEntries.Count > MaxEntries || System.OutsiderEntries.Count > MaxEntries)
			{
				ReportFault(KingdomChronicleRegistryFault.MalformedRow, "list-bound", true);
				return false;
			}
			string raw;
			try { raw = The.Game.GetStringGameState(EventRegistryState, ""); }
			catch
			{
				ReportFault(KingdomChronicleRegistryFault.MalformedRow, "registry-read", true);
				return false;
			}
			List<KingdomChronicleReceipt> rows;
			bool migratedLegacy;
			KingdomChronicleRegistryFault fault;
			if (!KingdomChronicleReceiptRules.TryParseRegistry(raw, out rows,
				out migratedLegacy, out fault))
			{
				ReportFault(fault, "registry-parse", true);
				return false;
			}
			if (migratedLegacy && !WriteEventReceipts(rows, "legacy-migration")) return false;

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
					ReportFault(KingdomChronicleRegistryFault.None, "legacy-construction-lost", false);
					return true;
				}
				ReportFault(KingdomChronicleRegistryFault.None, "legacy-replay-blocked", true);
				return false;
			}
			if (receipt != null && !string.Equals(receipt.Fingerprint, fingerprint,
				StringComparison.Ordinal))
			{
				ReportFault(KingdomChronicleRegistryFault.DuplicateIdentity,
					"fingerprint-mismatch", true);
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
				ReportFault(KingdomChronicleRegistryFault.DuplicateIdentity,
					"declaration-mismatch", true);
				return false;
			}
			if (receipt != null && receipt.Compact)
				return KingdomChronicleReceiptRules.IsTerminal(receipt);
			if (receipt != null && KingdomChronicleReceiptRules.IsTerminal(receipt))
				return WriteEventReceipts(rows, "terminal-compaction");
			if (receipt == null)
			{
				// No receipt is ever evicted: terminal identity is permanent replay proof.
				if (rows.Count >= KingdomChronicleReceiptRules.MaxReceipts)
				{
					ReportFault(KingdomChronicleRegistryFault.TooManyRows, "capacity", true);
					return false;
				}
				KingdomChronicleDeclaration declaration = Declaration;
				if (declaration == null && !TryDeclareOnce(System, EventId, Text,
					Accomplishment, MuralText, out declaration))
				{
					ReportFault(KingdomChronicleRegistryFault.CryptoUnavailable,
						"receipt-declaration", true);
					return false;
				}
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
					ReportFault(KingdomChronicleRegistryFault.DuplicateIdentity,
						"declaration-list-mismatch", true);
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
				if (!WriteEventReceipts(rows, "receipt-create")) return false;
			}
			if (!DeliverList(rows, receipt, System.ChronicleEntries, true)) return false;
			if (!DeliverList(rows, receipt, System.OutsiderEntries, false)) return false;
			if (!DeliverJournal(rows, receipt, Accomplishment, Text, MuralText)) return false;
			return KingdomChronicleReceiptRules.IsTerminal(receipt);
		}

	}
}
