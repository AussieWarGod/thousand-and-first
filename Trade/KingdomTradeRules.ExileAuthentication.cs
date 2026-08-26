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
		public static bool TryAuthenticateExactExileClosedTick(KingdomTradeBook Book,
			string RealmId, List<string> ExactSettlementIds,
			out long ClosedTick, out string Failure)
		{
			ClosedTick = -1L;
			Failure = null;
			List<string> exact;
			if (!ValidId(RealmId) || !TryExactSettlementSet(ExactSettlementIds, out exact))
			{
				Failure = "Trade exile proof requires exact bounded realm and settlement identity.";
				return false;
			}
			bool bound = Book != null && Book.IdentityBound;
			bool exactIdentity = bound
				? BookUsable(Book) && string.Equals(Book.RealmId, RealmId,
					StringComparison.Ordinal) && ExactStringSet(Book.SettlementIds, exact)
				: Book != null && string.IsNullOrEmpty(Book.RealmId)
					&& Book.SettlementIds != null && Book.SettlementIds.Count == 0;
			if (Book == null || Book.FormatVersion != CurrentFormatVersion
				|| Book.SchemaState != KingdomTradeSchemaState.Compatible || !exactIdentity
				|| HasActiveAuthority(Book)
				|| Book.Charters == null || Book.Charters.Count != 0 || Book.Manifest != null
				|| Book.OpenOperation != null || Book.PendingRetirement != null
				|| Book.RecentProofs == null || Book.RecentProofs.Count != 0
				|| Book.CompactedProofs == null || Book.CompactedProofs.Count != 0
				|| !string.IsNullOrEmpty(Book.ActiveProjectionId)
				|| !string.IsNullOrEmpty(Book.ActiveProjectionObjectId)
				|| Book.Projections == null || Book.Projections.Count != 0
				|| Book.RetainedEscrowDrams != 0L || Book.UnattributedArchivedEscrowDrams != 0L
				|| Book.OptionState != KingdomTradeOptionState.Unknown || Book.RestampPending
				|| Book.NextCharterSequence != 1L || Book.NextOperationSequence != 1L
				|| Book.RetiredThrough != 0L || Book.Archives == null
				|| Book.Archives.Count < 1 || Book.Archives.Count > MaxArchives
				|| Book.Incidents == null || Book.Incidents.Count > MaxIncidents)
			{
				Failure = "Trade exile proof book is not one exact pristine settled authority graph.";
				return false;
			}
			for (int i = 0; i < Book.Incidents.Count; i++)
				if (!ValidIncidentEvidence(Book.Incidents[i]))
				{
					Failure = "Trade exile proof incident evidence is malformed.";
					return false;
				}
			KingdomTradeArchive target = null;
			int targetIndex = -1;
			for (int i = 0; i < Book.Archives.Count; i++)
			{
				KingdomTradeArchive row = Book.Archives[i];
				if (!ValidArchiveEvidence(row))
				{
					Failure = "Trade exile proof archive evidence is malformed.";
					return false;
				}
				for (int j = 0; j < i; j++)
					if (string.Equals(Book.Archives[j].RealmId, row.RealmId,
						StringComparison.Ordinal))
					{
						Failure = "Trade exile proof archive realm evidence collides.";
						return false;
					}
				if (!string.Equals(row.RealmId, RealmId, StringComparison.Ordinal)) continue;
				if (target != null || !ExactStringSet(row.SettlementIds, exact))
				{
					Failure = "Trade exile proof receipt collides with different topology evidence.";
					return false;
				}
				target = row;
				targetIndex = i;
			}
			if (target == null || targetIndex != Book.Archives.Count - 1
				|| Book.OptionObservedTick != target.ClosedTick)
			{
				Failure = "Trade exile has no unique authenticated receipt for the exact realm topology.";
				return false;
			}
			ClosedTick = target.ClosedTick;
			return true;
		}

		/// <summary>Strict compatibility wrapper for callers that require pre-bind proof.</summary>
		public static bool TryGetExactExileClosedTick(KingdomTradeBook Book, string RealmId,
			List<string> ExactSettlementIds, out long ClosedTick, out string Failure)
		{
			ClosedTick = -1L;
			Failure = null;
			if (Book != null && Book.IdentityBound)
			{
				Failure = "Trade exile retry requires the pristine unbound settled receipt.";
				return false;
			}
			return TryAuthenticateExactExileClosedTick(Book, RealmId, ExactSettlementIds,
				out ClosedTick, out Failure);
		}

	}
}
