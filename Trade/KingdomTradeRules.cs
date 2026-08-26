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
	/// <summary>Engine-free bounds, lifecycle repair, replay, option, and conservation laws.</summary>
	public static partial class KingdomTradeRules
	{
		private const int MaxReferenceSealDepth = 12;
		private const int MaxReferenceSealRows = 32768;

		private sealed class ExactReferenceComparer : IEqualityComparer<object>
		{
			public new bool Equals(object Left, object Right)
			{
				return ReferenceEquals(Left, Right);
			}

			public int GetHashCode(object Value)
			{
				return RuntimeHelpers.GetHashCode(Value);
			}
		}

		public const int CurrentFormatVersion = 5;
		private const string IdentityNamespace = "taf.trade.identity.v2";
		public const int MaxCharters = 8;
		public const int MaxWaterLegs = 24;
		public const int MaxMaterialOutputs = 9;
		public const int MaxProjectionRows = 8;
		public const int MaxRecentProofs = 64;
		public const int MaxCompactedProofs = 16;
		public const int MaxArchives = 16;
		public const int MaxIncidents = 16;
		// Mirrors current identity-wave product cap without coupling pure Trade rules to Core.
		public const int MaxSettlementIds = 4;
		public const int MaxIdChars = 256;
		public const int MaxNameChars = 512;
		public const int MaxTextChars = 4096;
		public const int MaxClaimChars = 8192;
		public const int MaxOperationWater = 1000000;

		public static bool BookUsable(KingdomTradeBook Book)
		{
			return Book != null && Book.FormatVersion == CurrentFormatVersion
				&& Book.SchemaState == KingdomTradeSchemaState.Compatible
				&& Book.IdentityBound && ValidId(Book.RealmId)
				&& ValidSettlementSet(Book.SettlementIds);
		}

		public static KingdomTradeExactLookup ResolveExactUnique<T>(IList<T> Rows,
			string ExactId, Func<T, string> Id, out T Exact) where T : class
		{
			Exact = null;
			if (Rows == null || Id == null || !ValidId(ExactId))
				return KingdomTradeExactLookup.Incomplete;
			int matches = 0;
			try
			{
				for (int i = 0; i < Rows.Count; i++)
				{
					T row = Rows[i];
					if (row == null) return KingdomTradeExactLookup.Incomplete;
					if (!string.Equals(Id(row), ExactId, StringComparison.Ordinal)) continue;
					matches++;
					Exact = row;
				}
			}
			catch { Exact = null; return KingdomTradeExactLookup.Incomplete; }
			if (matches == 0) return KingdomTradeExactLookup.Missing;
			if (matches == 1) return KingdomTradeExactLookup.ExactUnique;
			Exact = null;
			return KingdomTradeExactLookup.Ambiguous;
		}

		public static bool HasActiveAuthority(KingdomTradeBook Book)
		{
			return Book != null && ((Book.Charters != null && Book.Charters.Count > 0)
				|| Book.Manifest != null || Book.OpenOperation != null || Book.PendingRetirement != null
				|| !string.IsNullOrEmpty(Book.ActiveProjectionId)
				|| !string.IsNullOrEmpty(Book.ActiveProjectionObjectId)
				|| (Book.Projections != null && Book.Projections.Count > 0)
				|| Book.RetainedEscrowDrams > 0L);
		}

		public static bool CanBindRealm(KingdomTradeBook Book)
		{
			return Book != null && Book.FormatVersion == CurrentFormatVersion
				&& Book.SchemaState == KingdomTradeSchemaState.Compatible
				&& !Book.IdentityBound && string.IsNullOrEmpty(Book.RealmId)
				&& !HasActiveAuthority(Book);
		}

		public static bool BindExactIdentity(KingdomTradeBook Book, string RealmId,
			IEnumerable<string> SettlementIds, out string Failure)
		{
			Failure = null;
			if (Book == null || Book.FormatVersion != CurrentFormatVersion
				|| Book.SchemaState != KingdomTradeSchemaState.Compatible || !ValidId(RealmId))
			{
				Failure = "Trade identity bind requires a compatible book and exact immutable realm id.";
				return false;
			}
			List<string> exact;
			if (!TryExactSettlementSet(SettlementIds, out exact))
			{
				Failure = "Trade identity bind requires exact settlement ids within product cap.";
				return false;
			}
			if (Book.IdentityBound)
			{
				if (string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal)
					&& ExactStringSet(Book.SettlementIds, exact)) return true;
				QuarantineBook(Book, "immutable trade identity changed after binding");
				Failure = Book.SchemaFault;
				return false;
			}
			if (HasActiveAuthority(Book))
			{
				QuarantineBook(Book, "unbound trade evidence cannot become live authority");
				Failure = Book.SchemaFault;
				return false;
			}
			Book.RealmId = RealmId;
			Book.SettlementIds = exact;
			Book.IdentityBound = true;
			return true;
		}

		/// <summary>Atomically expands a bound exact identity set; settled authority stays valid.</summary>
		public static bool ExpandExactIdentity(KingdomTradeBook Book, string RealmId,
			IEnumerable<string> SettlementIds, out string Failure)
		{
			Failure = null;
			if (Book == null || Book.FormatVersion != CurrentFormatVersion
				|| Book.SchemaState != KingdomTradeSchemaState.Compatible || !Book.IdentityBound
				|| !ValidId(Book.RealmId) || !ValidSettlementSet(Book.SettlementIds))
			{
				Failure = "Trade identity expansion requires compatible bound exact authority.";
				return false;
			}
			List<string> exact;
			if (!TryExactSettlementSet(SettlementIds, out exact))
			{
				Failure = "Trade identity expansion candidate is malformed or exceeds product cap.";
				return false;
			}
			if (!string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal))
			{
				QuarantineBook(Book, "immutable trade realm changed during identity expansion");
				Failure = Book.SchemaFault;
				return false;
			}
			if (ExactStringSet(Book.SettlementIds, exact)) return true;
			for (int i = 0; i < Book.SettlementIds.Count; i++)
				if (!exact.Contains(Book.SettlementIds[i]))
				{
					QuarantineBook(Book, "exact trade settlement identity was removed or replaced");
					Failure = Book.SchemaFault;
					return false;
				}
			if (Book.OpenOperation != null || Book.PendingRetirement != null)
			{
				Failure = "Trade identity expansion deferred while an operation receipt is open.";
				return false;
			}
			Book.SettlementIds = exact;
			return true;
		}

		/// <summary>
		/// Builds an exile replacement without mutating Source. The archive digest commits every
		/// active row; explicit counters and escrow fields keep value/effect disposition inspectable.
		/// </summary>
	}
}
