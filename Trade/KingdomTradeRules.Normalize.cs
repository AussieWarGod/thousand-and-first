using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		public static void Normalize(KingdomTradeBook Book)
		{
			if (Book == null) return;
			if (Book.FormatVersion != CurrentFormatVersion) return;
			if (!Enum.IsDefined(typeof(KingdomTradeSchemaState), Book.SchemaState))
			{
				Book.SchemaState = KingdomTradeSchemaState.Quarantined;
				Book.SchemaFault = AppendFault(Book.SchemaFault, "unknown trade schema disposition");
			}
			if (Book.SchemaState == KingdomTradeSchemaState.Unknown
				|| Book.SchemaState == KingdomTradeSchemaState.Quarantined) return;
			if (TooLong(Book.SchemaFault, MaxTextChars))
			{
				QuarantineBook(Book, "oversized trade schema evidence");
				return;
			}
			if (!Book.IdentityBound)
			{
				if (HasActiveAuthority(Book)) QuarantineBook(Book,
					"unbound trade evidence is non-authoritative until exact identity is supplied");
				return;
			}
			if (!ValidId(Book.RealmId) || !ValidSettlementSet(Book.SettlementIds))
			{
				QuarantineBook(Book, "bound trade identity is malformed");
				return;
			}
			bool malformedRealm = false;
			if (Book.NextCharterSequence <= 0L || Book.NextOperationSequence <= 0L
				|| Book.RetiredThrough < 0L || Book.OptionObservedTick < 0L || Book.OptionEpoch < 0L)
			{
				QuarantineBook(Book, "negative or zero trade counters are preserved as malformed evidence");
				return;
			}
			if (Book.NextOperationSequence <= Book.RetiredThrough)
			{
				QuarantineBook(Book, "operation counter overlaps permanent retirement authority");
				return;
			}
			if (!Enum.IsDefined(typeof(KingdomTradeOptionState), Book.OptionState))
			{
				QuarantineBook(Book, "invalid trade option evidence");
				return;
			}
			if (Book.RetainedEscrowDrams < 0L || Book.UnattributedArchivedEscrowDrams < 0L)
			{
				QuarantineBook(Book, "negative escrow evidence was preserved");
				return;
			}
			if (TooLong(Book.ActiveProjectionId, MaxIdChars)
				|| TooLong(Book.ActiveProjectionObjectId, MaxIdChars))
			{
				QuarantineBook(Book, "oversized legacy projection evidence");
				return;
			}
			if (!string.IsNullOrEmpty(Book.ActiveProjectionId)
				|| !string.IsNullOrEmpty(Book.ActiveProjectionObjectId))
			{
				Book.SchemaState = KingdomTradeSchemaState.Quarantined;
				Book.SchemaFault = AppendFault(Book.SchemaFault,
					"legacy realm-global projection authority cannot be assigned to an exact city");
				return;
			}

			if (Book.Charters == null || Book.Projections == null || Book.RecentProofs == null
				|| Book.CompactedProofs == null
				|| Book.Archives == null || Book.Incidents == null)
			{
				QuarantineBook(Book, "missing Trade evidence list");
				return;
			}
			if (Book.Charters.Count > MaxCharters)
			{
				Book.SchemaState = KingdomTradeSchemaState.Quarantined;
				Book.SchemaFault = AppendFault(Book.SchemaFault,
					"active charter row cap exceeded; no authority rows were discarded");
				return;
			}
			for (int i = Book.Charters.Count - 1; i >= 0; i--)
			{
				KingdomTradeCharter row = Book.Charters[i];
				if (row == null)
				{
					QuarantineBook(Book, "null charter evidence row was preserved");
					return;
				}
				bool oversized = TooLong(row.Id, MaxIdChars)
					|| TooLong(row.DealKey, MaxNameChars) || TooLong(row.Faction, MaxNameChars)
					|| TooLong(row.Fault, MaxTextChars);
				if (malformedRealm || oversized || row.Sequence <= 0L
					|| !string.Equals(row.Id, CharterId(Book.RealmId, row.Sequence), StringComparison.Ordinal)
					|| !ValidName(row.DealKey) || !ValidName(row.Faction)
					|| row.NextTick < 0L || row.CreatedTick < 0L)
				{
					row.Quarantined = true;
					row.Fault = AppendFault(row.Fault, "malformed charter row");
				}
			}
			for (int i = 0; i < Book.Charters.Count; i++)
			{
				KingdomTradeCharter left = Book.Charters[i];
				if (left == null) continue;
				for (int j = i + 1; j < Book.Charters.Count; j++)
				{
					KingdomTradeCharter right = Book.Charters[j];
					if (right == null || !(string.Equals(left.Id, right.Id, StringComparison.Ordinal)
						|| (string.Equals(left.DealKey, right.DealKey, StringComparison.Ordinal)
							&& string.Equals(left.Faction, right.Faction, StringComparison.Ordinal)))) continue;
					left.Quarantined = true;
					right.Quarantined = true;
					left.Fault = AppendFault(left.Fault, "duplicate charter authority");
					right.Fault = AppendFault(right.Fault, "duplicate charter authority");
				}
			}
			NormalizeProjections(Book);
			if (Book.SchemaState == KingdomTradeSchemaState.Quarantined) return;

			NormalizeManifest(Book, Book.Manifest, malformedRealm);
			NormalizeOperation(Book);
			if (malformedRealm && Book.OpenOperation != null)
			{
				Book.OpenOperation.Phase = KingdomTradePhase.Quarantined;
				Book.OpenOperation.Fault = AppendFault(Book.OpenOperation.Fault,
					"malformed realm identity");
			}
			NormalizeProofs(Book);
			NormalizeProofCompactions(Book);
			NormalizeArchives(Book);
			NormalizeIncidents(Book);
			NormalizePendingRetirement(Book);
		}

	}
}
