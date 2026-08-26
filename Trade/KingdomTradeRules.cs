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
	public enum KingdomTradeOptionAction : byte
	{
		None = 0,
		StayDisabled = 1,
		Disable = 2,
		EnableAndRestamp = 3
	}

	public enum KingdomTradeExactLookup : byte
	{
		Incomplete = 0,
		Missing = 1,
		ExactUnique = 2,
		Ambiguous = 3
	}

	/// <summary>Engine-free bounds, lifecycle repair, replay, option, and conservation laws.</summary>
	public static class KingdomTradeRules
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
		public static bool TryPrepareExile(KingdomTradeBook Source, long Tick,
			string ExactRealmId, List<string> ExactSettlementIds,
			out KingdomTradeBook Replacement, out long SettledTick, out string Failure)
		{
			Replacement = null;
			SettledTick = -1L;
			Failure = null;
			if (Source == null || Tick < 0L || !ValidId(ExactRealmId))
			{
				Failure = "Trade exile requires an exact realm id and nonnegative tick.";
				return false;
			}
			List<string> exact;
			if (!TryExactSettlementSet(ExactSettlementIds, out exact))
			{
				Failure = "Trade exile requires the complete exact settlement topology.";
				return false;
			}
			if (!Source.IdentityBound)
			{
				long closedTick;
				if (TryGetExactExileClosedTick(Source, ExactRealmId, exact,
					out closedTick, out Failure))
				{
					Replacement = Source;
					SettledTick = closedTick;
					return true;
				}
				return false;
			}
			KingdomTradeBook copy;
			string originalEvidence = EvidenceDigest(Source);
			try
			{
				KingdomTradeCodec.EncodePayload(Source);
				copy = KingdomTradeCodec.DecodeEnvelopeRaw(
					KingdomTradeCodec.EncodeEnvelope(Source));
				// Detached exile preparation is an explicit semantic authority caller. Raw save decode
				// itself never repairs; Core first gets to reject coexisting legacy rows.
				Normalize(copy);
			}
			catch (Exception error)
			{
				Failure = "Trade exile could not freeze bounded authority: " + Bound(error.Message, 256);
				return false;
			}
			if (copy.Archives == null || copy.Archives.Count >= MaxArchives)
			{
				Failure = "Trade exile archive capacity is full.";
				return false;
			}
			if (!BookUsable(copy) || !string.Equals(copy.RealmId, ExactRealmId,
					StringComparison.Ordinal) || !ExactStringSet(copy.SettlementIds, exact))
			{
				Failure = "Trade exile exact realm or settlement topology does not match live authority.";
				return false;
			}
			for (int i = 0; i < copy.Archives.Count; i++)
				if (string.Equals(copy.Archives[i].RealmId, ExactRealmId,
					StringComparison.Ordinal))
				{
					Failure = "Trade exile collides with existing archive evidence for this realm.";
					return false;
				}
			int proofCount = copy.RecentProofs.Count;
			for (int i = 0; i < copy.CompactedProofs.Count; i++)
			{
				if (!ValidProofCompaction(copy.CompactedProofs[i])
					|| copy.CompactedProofs[i].ProofCount > int.MaxValue - proofCount)
				{
					Failure = "Trade exile proof accounting is malformed or overflowing.";
					return false;
				}
				proofCount += copy.CompactedProofs[i].ProofCount;
			}
			long archived;
			if (!TryAddEscrow(copy.RetainedEscrowDrams,
				copy.UnattributedArchivedEscrowDrams, out archived))
			{
				Failure = "Trade exile unattributed escrow accounting overflowed.";
				return false;
			}
			int manifestEscrow = copy.Manifest?.EscrowDrams ?? 0;
			KingdomTradeOperation open = copy.OpenOperation;
			bool manifestAlreadyRetained = open != null
				&& open.Kind == KingdomTradeOperationKind.ManifestLapse
				&& open.RetainedState == KingdomTradePhysicalState.Proved
				&& copy.Manifest != null && open.RetainedDelta == manifestEscrow
				&& copy.RetainedEscrowDrams == open.RetainedAfter;
			if (!manifestAlreadyRetained
				&& !TryAddEscrow(archived, manifestEscrow, out archived))
			{
				Failure = "Trade exile escrow accounting overflowed.";
				return false;
			}
			int orphanedLoad = open != null && open.Kind == KingdomTradeOperationKind.ManifestLoad
				&& (copy.Manifest == null || !string.Equals(copy.Manifest.OperationId,
					open.Id, StringComparison.Ordinal)) ? open.ProvedWater : 0;
			if (!TryAddEscrow(archived, orphanedLoad, out archived))
			{
				Failure = "Trade exile orphaned manifest accounting overflowed.";
				return false;
			}
			string evidence = originalEvidence;
			if (!ValidId(evidence))
			{
				Failure = "Trade exile could not authenticate its complete authority graph.";
				return false;
			}
			KingdomTradeArchive archive = new KingdomTradeArchive
			{
				RealmId = ExactRealmId,
				SettlementIds = new List<string>(exact),
				RetainedEscrowDrams = archived,
				ManifestEscrowDrams = manifestEscrow,
				ManifestId = copy.Manifest?.Id,
				ManifestStatus = copy.Manifest?.Status ?? KingdomTradeManifestStatus.None,
				CharterCount = copy.Charters.Count,
				ProjectionCount = copy.Projections.Count,
				ProofCount = proofCount,
				OpenOperationId = open?.Id,
				PendingRetirementId = copy.PendingRetirement?.Id,
				OpenRequestedWater = open?.RequestedWater ?? 0,
				OpenProvedWater = open?.ProvedWater ?? 0,
				OpenAmbiguousWater = open?.AmbiguousWater ?? 0,
				RetiredThrough = copy.RetiredThrough,
				AuthorityEvidenceHash = evidence,
				ClosedTick = Tick
			};
			archive.ReceiptEvidenceHash = ArchiveReceiptDigest(archive);
			if (!CanonicalSha256(archive.AuthorityEvidenceHash)
				|| !CanonicalSha256(archive.ReceiptEvidenceHash))
			{
				Failure = "Trade exile could not authenticate its canonical archive receipt.";
				return false;
			}
			copy.Archives.Add(archive);
			copy.Charters = new List<KingdomTradeCharter>();
			copy.Manifest = null;
			copy.OpenOperation = null;
			copy.PendingRetirement = null;
			copy.RecentProofs = new List<KingdomTradeProof>();
			copy.CompactedProofs = new List<KingdomTradeProofCompaction>();
			copy.ActiveProjectionId = null;
			copy.ActiveProjectionObjectId = null;
			copy.Projections = new List<KingdomTradeProjectionRow>();
			copy.RetainedEscrowDrams = 0L;
			copy.UnattributedArchivedEscrowDrams = 0L;
			copy.RealmId = null;
			copy.IdentityBound = false;
			copy.SettlementIds = new List<string>();
			copy.OptionState = KingdomTradeOptionState.Unknown;
			copy.OptionObservedTick = Tick;
			copy.RestampPending = false;
			copy.NextCharterSequence = 1L;
			copy.NextOperationSequence = 1L;
			copy.RetiredThrough = 0L;
			try { KingdomTradeCodec.EncodePayload(copy); }
			catch
			{
				Failure = "Trade exile replacement exceeded bounded persistence capacity.";
				return false;
			}
			long authenticatedTick;
			string receiptFailure;
			if (!TryGetExactExileClosedTick(copy, ExactRealmId, exact,
				out authenticatedTick, out receiptFailure) || authenticatedTick != Tick)
			{
				Failure = "Trade exile replacement did not authenticate its exact durable receipt: "
					+ receiptFailure;
				return false;
			}
			Replacement = copy;
			SettledTick = authenticatedTick;
			return true;
		}

		/// <summary>Compatibility seam for engine-free callers using a concrete array.</summary>
		public static bool TryPrepareExile(KingdomTradeBook Source, long Tick,
			string ExactRealmId, string[] ExactSettlementIds,
			out KingdomTradeBook Replacement, out string Failure)
		{
			List<string> exact;
			if (!TryExactSettlementSet(ExactSettlementIds, out exact))
			{
				Replacement = null;
				Failure = "Trade exile requires the complete exact settlement topology.";
				return false;
			}
			long ignoredTick;
			return TryPrepareExile(Source, Tick, ExactRealmId, exact,
				out Replacement, out ignoredTick, out Failure);
		}

		/// <summary>
		/// Authenticates one exact settled exile receipt without mutating Book. The receipt may be
		/// observed either before return binding or immediately after the same exact identity was
		/// rebound. No active Trade authority or changed close clock may coexist with this proof.
		/// </summary>
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

		public static bool IdentityContainsSettlement(KingdomTradeBook Book, string SettlementId)
		{
			return BookUsable(Book) && Book.SettlementIds.Contains(SettlementId);
		}

		/// <summary>Captures immutable authority and topology immediately before a hostile callback.</summary>
		public static KingdomTradeAuthoritySeal CaptureAuthoritySeal(KingdomTradeBook Book,
			IList<string> ClaimedZones, IList<string> CityZones)
		{
			if (Book == null || ClaimedZones == null || CityZones == null) return null;
			try
			{
				return new KingdomTradeAuthoritySeal
				{
					BookBytes = KingdomTradeCodec.EncodePayload(Book),
					ClaimedZones = ClaimedZones,
					ClaimedRows = CopyStrings(ClaimedZones),
					CityZones = CityZones,
					CityRows = CopyStrings(CityZones)
				};
			}
			catch { return null; }
		}

		/// <summary>No callback may alter authority or topology beyond its one declared mutation.</summary>
		public static bool ExactAuthoritySeal(KingdomTradeBook Book,
			IList<string> ClaimedZones, IList<string> CityZones, KingdomTradeAuthoritySeal Seal)
		{
			if (Book == null || Seal == null || Seal.BookBytes == null
				|| !ReferenceEquals(ClaimedZones, Seal.ClaimedZones)
				|| !ReferenceEquals(CityZones, Seal.CityZones)
				|| !ExactStrings(ClaimedZones, Seal.ClaimedRows)
				|| !ExactStrings(CityZones, Seal.CityRows)) return false;
			byte[] current;
			try { current = KingdomTradeCodec.EncodePayload(Book); }
			catch { return false; }
			if (current.Length != Seal.BookBytes.Length) return false;
			for (int i = 0; i < current.Length; i++)
				if (current[i] != Seal.BookBytes[i]) return false;
			return true;
		}

		/// <summary>
		/// Captures every bounded mutable reference reachable through concrete lists, maps, arrays,
		/// and public persisted TAF fields. Values are proved separately by canonical graph bytes.
		/// </summary>
		public static bool TryCaptureExactReferenceSeal(IList<object> Roots,
			out KingdomTradeReferenceSeal Seal)
		{
			Seal = null;
			if (Roots == null || Roots.Count > 256) return false;
			try
			{
				List<object> rows = new List<object>();
				HashSet<object> expanded = new HashSet<object>(new ExactReferenceComparer());
				for (int i = 0; i < Roots.Count; i++)
					if (!CollectExactReferences(Roots[i], 0, rows, expanded)) return false;
				Seal = new KingdomTradeReferenceSeal { Rows = rows.ToArray() };
				return true;
			}
			catch { Seal = null; return false; }
		}

		public static bool ExactReferenceSeal(IList<object> Roots,
			KingdomTradeReferenceSeal Seal)
		{
			if (Seal?.Rows == null || !TryCaptureExactReferenceSeal(Roots,
				out KingdomTradeReferenceSeal current) || current.Rows.Length != Seal.Rows.Length)
				return false;
			for (int i = 0; i < Seal.Rows.Length; i++)
				if (!ReferenceEquals(Seal.Rows[i], current.Rows[i])) return false;
			return true;
		}

		private static bool CollectExactReferences(object Value, int Depth,
			List<object> Rows, HashSet<object> Expanded)
		{
			if (Rows == null || Expanded == null || Depth > MaxReferenceSealDepth
				|| Rows.Count >= MaxReferenceSealRows) return false;
			Rows.Add(Value);
			if (Value == null) return true;
			Type type = Value.GetType();
			if (type.IsValueType || Value is string) return false;
			if (!Expanded.Add(Value)) return true;

			if (Value is Array array)
			{
				if (array.Length > 1024) return false;
				Type element = type.GetElementType();
				if (element == null || element.IsValueType || element == typeof(string)) return true;
				for (int i = 0; i < array.Length; i++)
					if (!CollectExactReferences(array.GetValue(i), Depth + 1, Rows, Expanded))
						return false;
				return true;
			}
			if (Value is IDictionary dictionary)
			{
				if (dictionary.Count > 1024) return false;
				foreach (DictionaryEntry row in dictionary)
				{
					if (row.Key != null && !(row.Key is string) && !row.Key.GetType().IsValueType
						&& !CollectExactReferences(row.Key, Depth + 1, Rows, Expanded)) return false;
					if (row.Value != null && !(row.Value is string) && !row.Value.GetType().IsValueType
						&& !CollectExactReferences(row.Value, Depth + 1, Rows, Expanded)) return false;
				}
				return true;
			}
			if (Value is IList list)
			{
				if (list.Count > 1024) return false;
				for (int i = 0; i < list.Count; i++)
				{
					object row = list[i];
					if (row != null && !(row is string) && !row.GetType().IsValueType
						&& !CollectExactReferences(row, Depth + 1, Rows, Expanded)) return false;
				}
				return true;
			}
			if (type.Namespace == null
				|| !type.Namespace.StartsWith("ThousandAndFirst", StringComparison.Ordinal))
				return true;

			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
			Array.Sort(fields, (left, right) => string.CompareOrdinal(left.Name, right.Name));
			for (int i = 0; i < fields.Length; i++)
			{
				FieldInfo field = fields[i];
				if (field.IsStatic || field.FieldType.IsValueType || field.FieldType == typeof(string)
					|| field.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
				if (!CollectExactReferences(field.GetValue(Value), Depth + 1, Rows, Expanded))
					return false;
			}
			return true;
		}

		/// <summary>Callers never receive a mutable alias to persisted manifest authority.</summary>
		public static KingdomTradeManifestState SnapshotManifest(KingdomTradeManifestState Manifest)
		{
			if (Manifest == null) return null;
			return new KingdomTradeManifestState
			{
				OperationSequence = Manifest.OperationSequence,
				OperationId = Manifest.OperationId,
				Id = Manifest.Id,
				OriginId = Manifest.OriginId,
				OriginName = Manifest.OriginName,
				DestinationId = Manifest.DestinationId,
				DestinationName = Manifest.DestinationName,
				OriginalDrams = Manifest.OriginalDrams,
				EscrowDrams = Manifest.EscrowDrams,
				LoadedTick = Manifest.LoadedTick,
				DeadlineTick = Manifest.DeadlineTick,
				TurnedBack = Manifest.TurnedBack,
				Status = Manifest.Status,
				Fault = Manifest.Fault
			};
		}

		private static string[] CopyStrings(IList<string> Values)
		{
			string[] copy = new string[Values.Count];
			for (int i = 0; i < copy.Length; i++) copy[i] = Values[i];
			return copy;
		}

		private static bool ExactStrings(IList<string> Current, string[] Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Length) return false;
			for (int i = 0; i < Expected.Length; i++)
				if (!string.Equals(Current[i], Expected[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ValidSettlementSet(List<string> Values)
		{
			if (Values == null || Values.Count < 1 || Values.Count > MaxSettlementIds) return false;
			for (int i = 0; i < Values.Count; i++)
				if (!ValidId(Values[i]) || (i > 0 && string.CompareOrdinal(Values[i - 1], Values[i]) >= 0))
					return false;
			return true;
		}

		private static bool TryExactSettlementSet(IEnumerable<string> Values,
			out List<string> Exact)
		{
			Exact = new List<string>();
			if (Values == null) return false;
			try
			{
				foreach (string id in Values)
				{
					if (!ValidId(id) || Exact.Contains(id) || Exact.Count >= MaxSettlementIds)
						return false;
					Exact.Add(id);
				}
				Exact.Sort(StringComparer.Ordinal);
				return ValidSettlementSet(Exact);
			}
			catch
			{
				Exact = null;
				return false;
			}
		}

		private static bool TryExactSettlementSet(List<string> Values,
			out List<string> Exact)
		{
			Exact = new List<string>();
			if (Values == null || Values.Count < 1 || Values.Count > MaxSettlementIds)
				return false;
			for (int i = 0; i < Values.Count; i++)
			{
				string id = Values[i];
				if (!ValidId(id) || Exact.Contains(id)) return false;
				Exact.Add(id);
			}
			Exact.Sort(StringComparer.Ordinal);
			return ValidSettlementSet(Exact);
		}

		private static bool TryExactSettlementSet(string[] Values,
			out List<string> Exact)
		{
			Exact = new List<string>();
			if (Values == null || Values.Length < 1 || Values.Length > MaxSettlementIds)
				return false;
			for (int i = 0; i < Values.Length; i++)
			{
				string id = Values[i];
				if (!ValidId(id) || Exact.Contains(id)) return false;
				Exact.Add(id);
			}
			Exact.Sort(StringComparer.Ordinal);
			return ValidSettlementSet(Exact);
		}

		private static bool ExactStringSet(List<string> Left, List<string> Right)
		{
			if (Left == null || Right == null || Left.Count != Right.Count) return false;
			for (int i = 0; i < Left.Count; i++)
				if (!string.Equals(Left[i], Right[i], StringComparison.Ordinal)) return false;
			return true;
		}

		public static void QuarantineBook(KingdomTradeBook Book, string Fault)
		{
			if (Book == null || Book.FormatVersion != CurrentFormatVersion) return;
			Book.SchemaState = KingdomTradeSchemaState.Quarantined;
			Book.SchemaFault = AppendFault(Book.SchemaFault, Fault);
			if (Book.Charters != null)
			{
				for (int i = 0; i < Book.Charters.Count; i++)
				{
					KingdomTradeCharter row = Book.Charters[i];
					if (row == null) continue;
					row.Quarantined = true;
					row.Fault = AppendFault(row.Fault, Fault);
				}
			}
			if (Book.Manifest != null)
			{
				Book.Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Book.Manifest.Fault = AppendFault(Book.Manifest.Fault, Fault);
			}
			if (Book.OpenOperation != null)
			{
				Book.OpenOperation.Phase = KingdomTradePhase.Quarantined;
				Book.OpenOperation.Fault = AppendFault(Book.OpenOperation.Fault, Fault);
			}
		}

		public static bool SinkSettled(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Delivered
				|| State == KingdomTradeSinkState.Skipped
				|| State == KingdomTradeSinkState.Lost;
		}

		public static KingdomTradeSinkState ResumeSink(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Intent ? KingdomTradeSinkState.Lost : State;
		}

		public static KingdomTradeOptionAction ObserveOption(
			KingdomTradeOptionState Prior, bool Enabled)
		{
			if (!Enabled)
			{
				return Prior == KingdomTradeOptionState.Disabled
					? KingdomTradeOptionAction.StayDisabled : KingdomTradeOptionAction.Disable;
			}
			return Prior == KingdomTradeOptionState.Enabled
				? KingdomTradeOptionAction.None : KingdomTradeOptionAction.EnableAndRestamp;
		}

		public static long SaturatingAdd(long Left, long Right)
		{
			if (Right > 0L && Left > long.MaxValue - Right) return long.MaxValue;
			if (Right < 0L && Left < long.MinValue - Right) return long.MinValue;
			return Left + Right;
		}

		public static int SaturatingMultiply(int Left, int Right)
		{
			if (Left <= 0 || Right <= 0) return 0;
			long value = (long)Left * Right;
			return value >= int.MaxValue ? int.MaxValue : (int)value;
		}

		public static int SaturatingAdd(int Left, int Right)
		{
			long value = (long)Left + Right;
			if (value <= 0L) return 0;
			return value >= int.MaxValue ? int.MaxValue : (int)value;
		}

		public static bool RecordIncident(KingdomTradeBook Book, long Tick, string Fault,
			KingdomTradeBook Evidence = null)
		{
			if (Book == null || Book.Incidents == null || Book.Incidents.Count >= MaxIncidents) return false;
			KingdomTradeOperation operation = Evidence?.OpenOperation;
			Book.Incidents.Add(new KingdomTradeIncident
			{
				RealmId = ValidId(Evidence?.RealmId) ? Evidence.RealmId
					: (ValidId(Book.RealmId) ? Book.RealmId : "unbound-trade-incident"),
				Sequence = operation?.Sequence ?? 0L,
				OperationId = operation?.Id,
				EvidenceHash = EvidenceDigest(Evidence ?? Book),
				Tick = Tick < 0L ? 0L : Tick,
				Fault = Bound(Fault, MaxTextChars)
			});
			return true;
		}

		public static string EvidenceDigest(KingdomTradeBook Book)
		{
			try
			{
				byte[] bytes = KingdomTradeCodec.EncodePayload(Book);
				using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
			}
			catch
			{
				using (SHA256 sha = SHA256.Create())
				{
					byte[] bytes = Encoding.UTF8.GetBytes(DigestField(Book?.RealmId) + "\n"
						+ DigestField(Book?.OpenOperation?.Id) + "\n"
						+ (Book?.OpenOperation?.Sequence ?? 0L).ToString(CultureInfo.InvariantCulture));
					return Hex(sha.ComputeHash(bytes));
				}
			}
		}

		/// <summary>Only canonical lowercase SHA-256 text may carry authority evidence.</summary>
		public static bool CanonicalSha256(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if ((Value[i] < '0' || Value[i] > '9')
					&& (Value[i] < 'a' || Value[i] > 'f')) return false;
			return true;
		}

		/// <summary>
		/// Recomputable receipt commitment. ReceiptEvidenceHash itself is excluded to avoid
		/// recursion; every other persisted archive field is length- or width-delimited.
		/// </summary>
		private static string ArchiveReceiptDigest(KingdomTradeArchive Row)
		{
			try
			{
				if (Row == null || Row.SettlementIds == null
					|| Row.SettlementIds.Count > MaxSettlementIds) return null;
				using (MemoryStream canonical = new MemoryStream())
				{
					if (!WriteCanonicalField(canonical, "taf.trade.archive-receipt.v1")
						|| !WriteCanonicalNullableField(canonical, Row.RealmId)) return null;
					WriteInt32(canonical, Row.SettlementIds.Count);
					for (int i = 0; i < Row.SettlementIds.Count; i++)
						if (!WriteCanonicalNullableField(canonical, Row.SettlementIds[i])) return null;
					WriteInt64(canonical, Row.RetainedEscrowDrams);
					WriteInt32(canonical, Row.ManifestEscrowDrams);
					if (!WriteCanonicalNullableField(canonical, Row.ManifestId)) return null;
					WriteInt32(canonical, (int)Row.ManifestStatus);
					WriteInt32(canonical, Row.CharterCount);
					WriteInt32(canonical, Row.ProjectionCount);
					WriteInt32(canonical, Row.ProofCount);
					if (!WriteCanonicalNullableField(canonical, Row.OpenOperationId)
						|| !WriteCanonicalNullableField(canonical, Row.PendingRetirementId)) return null;
					WriteInt32(canonical, Row.OpenRequestedWater);
					WriteInt32(canonical, Row.OpenProvedWater);
					WriteInt32(canonical, Row.OpenAmbiguousWater);
					WriteInt64(canonical, Row.RetiredThrough);
					if (!WriteCanonicalNullableField(canonical, Row.AuthorityEvidenceHash)) return null;
					WriteInt64(canonical, Row.ClosedTick);
					using (SHA256 sha = SHA256.Create())
						return sha == null ? null : Hex(sha.ComputeHash(canonical.ToArray()));
				}
			}
			catch { return null; }
		}

		private static string DigestField(string Value)
		{
			if (Value == null) return "-1:";
			int take = Math.Min(Value.Length, MaxIdChars);
			return Value.Length.ToString(CultureInfo.InvariantCulture) + ":"
				+ (take == Value.Length ? Value : Value.Substring(0, take));
		}

		private static string Hex(byte[] Digest)
		{
			char[] hex = new char[Digest.Length * 2];
			const string alphabet = "0123456789abcdef";
			for (int i = 0; i < Digest.Length; i++)
			{
				hex[i * 2] = alphabet[Digest[i] >> 4];
				hex[i * 2 + 1] = alphabet[Digest[i] & 15];
			}
			return new string(hex);
		}

		/// <summary>Name-derived identity exists only to preserve legacy positional rows.
		/// Live trade must bind the city's already-minted SettlementId.</summary>
		public static string LegacySettlementId(string RealmId, string Name)
		{
			return CanonicalId("settlement", 0L, RealmId, Name);
		}

		public static string LegacyRealmId(string FactionName, string DisplayName)
		{
			return CanonicalId("realm", 0L, FactionName, DisplayName);
		}

		public static string LegacyCharterId(string RealmId, string Deal, string Faction, int Row)
		{
			return CanonicalId("legacy-charter", Row + 1L, RealmId, Deal, Faction);
		}

		public static string LegacyManifestId(string RealmId, string Origin, string Destination,
			long LoadedTick)
		{
			return CanonicalId("legacy-manifest", LoadedTick, RealmId, Origin, Destination);
		}

		public static string CharterId(string RealmId, long Sequence)
		{
			return CanonicalId("charter", Sequence, RealmId);
		}

		public static string OperationId(string RealmId, long Sequence)
		{
			return CanonicalId("operation", Sequence, RealmId);
		}

		public static string ProjectionId(string OperationId)
		{
			return CanonicalId("projection", 0L, OperationId);
		}

		public static string ManifestId(string OperationId)
		{
			return CanonicalId("manifest", 0L, OperationId);
		}

		public static string MaterialMarker(string OperationId, int Kind)
		{
			return CanonicalId("material", Kind, OperationId);
		}

		private static string CanonicalId(string Lane, long Number, params string[] Fields)
		{
			try
			{
				if (string.IsNullOrEmpty(Lane) || Lane.Length > 64
					|| Fields == null || Fields.Length > 8) return null;
				using (MemoryStream canonical = new MemoryStream())
				{
					if (!WriteCanonicalField(canonical, IdentityNamespace)
						|| !WriteCanonicalField(canonical, Lane)) return null;
					WriteInt64(canonical, Number);
					WriteInt32(canonical, Fields.Length);
					for (int i = 0; i < Fields.Length; i++)
						if (!WriteCanonicalField(canonical, Fields[i] ?? "")) return null;
					byte[] digest;
					using (SHA256 sha = SHA256.Create())
					{
						if (sha == null) return null;
						digest = sha.ComputeHash(canonical.ToArray());
					}
					char[] hex = new char[digest.Length * 2];
					const string alphabet = "0123456789abcdef";
					for (int i = 0; i < digest.Length; i++)
					{
						hex[i * 2] = alphabet[digest[i] >> 4];
						hex[i * 2 + 1] = alphabet[digest[i] & 15];
					}
					return new string(hex);
				}
			}
			catch
			{
				return null;
			}
		}

		private static bool WriteCanonicalField(Stream Stream, string Value)
		{
			if (Stream == null || Value == null || Value.Length > MaxTextChars) return false;
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			WriteInt32(Stream, bytes.Length);
			Stream.Write(bytes, 0, bytes.Length);
			return true;
		}

		private static bool WriteCanonicalNullableField(Stream Stream, string Value)
		{
			if (Stream == null) return false;
			if (Value == null)
			{
				WriteInt32(Stream, -1);
				return true;
			}
			return WriteCanonicalField(Stream, Value);
		}

		private static void WriteInt32(Stream Stream, int Value)
		{
			Stream.WriteByte((byte)(Value >> 24));
			Stream.WriteByte((byte)(Value >> 16));
			Stream.WriteByte((byte)(Value >> 8));
			Stream.WriteByte((byte)Value);
		}

		private static void WriteInt64(Stream Stream, long Value)
		{
			ulong bits = unchecked((ulong)Value);
			for (int shift = 56; shift >= 0; shift -= 8)
				Stream.WriteByte((byte)(bits >> shift));
		}

		public static KingdomTradeOperation NewOperation(KingdomTradeBook Book,
			KingdomTradeOperationKind Kind, long Tick)
		{
			if (!BookUsable(Book) || !ValidId(Book.RealmId) || Book.OpenOperation != null
				|| Book.NextOperationSequence <= Book.RetiredThrough
				|| Book.NextOperationSequence == long.MaxValue || Tick < 0L
				|| Kind == KingdomTradeOperationKind.None
				|| !Enum.IsDefined(typeof(KingdomTradeOperationKind), Kind)) return null;
			// Reserve a durable retirement slot before publishing any operation authority.
			if (!EnsureRetirementCapacity(Book)) return null;
			long sequence = Book.NextOperationSequence;
			string id = OperationId(Book.RealmId, sequence);
			if (!ValidId(id)) return null;
			Book.NextOperationSequence++;
			KingdomTradeOperation operation = new KingdomTradeOperation
			{
				Sequence = sequence,
				Id = id,
				Kind = Kind,
				Phase = KingdomTradePhase.Prepared,
				CreatedTick = Tick,
				UpdatedTick = Tick,
					ProjectionState = KingdomTradePhysicalState.None,
					PriorCleanupState = KingdomTradePhysicalState.None,
					WaterLegs = new List<KingdomTradeWaterLeg>(),
					MaterialOutputs = new List<KingdomTradeMaterialOutput>(),
					Pattern = Kind == KingdomTradeOperationKind.CharterDelivery
						? KingdomTradePatternRules.PriorWireDefault() : null
			};
			Book.OpenOperation = operation;
			return operation;
		}

		/// <summary>Reserves one exact proof slot before an operation can publish effects.</summary>
		public static bool EnsureRetirementCapacity(KingdomTradeBook Book)
		{
			if (Book == null || Book.RecentProofs == null || Book.CompactedProofs == null
				|| Book.RecentProofs.Count > MaxRecentProofs
				|| Book.CompactedProofs.Count > MaxCompactedProofs) return false;
			if (Book.RecentProofs.Count < MaxRecentProofs) return true;
			const int compactCount = MaxRecentProofs / 2;
			List<KingdomTradeProof> batch = Book.RecentProofs.GetRange(0, compactCount);
			for (int i = 0; i < batch.Count; i++)
				if (!ValidProof(Book, batch[i], true)
					|| !string.Equals(batch[i].RealmId, Book.RealmId,
						StringComparison.Ordinal)) return false;
			string digest = ProofCompactionDigest(batch, Book.CompactedProofs);
			if (!ValidId(digest)) return false;
			long first = batch[0].Sequence;
			long last = batch[0].Sequence;
			for (int i = 1; i < batch.Count; i++)
			{
				first = Math.Min(first, batch[i].Sequence);
				last = Math.Max(last, batch[i].Sequence);
			}
			int total = batch.Count;
			if (Book.CompactedProofs.Count >= MaxCompactedProofs)
			{
				for (int i = 0; i < Book.CompactedProofs.Count; i++)
				{
					KingdomTradeProofCompaction prior = Book.CompactedProofs[i];
					if (!ValidProofCompaction(prior) || prior.ProofCount > int.MaxValue - total)
						return false;
					total += prior.ProofCount;
					first = Math.Min(first, prior.FirstSequence);
					last = Math.Max(last, prior.LastSequence);
				}
			}
			KingdomTradeProofCompaction compact = new KingdomTradeProofCompaction
			{
				RealmId = Book.RealmId,
				FirstSequence = first,
				LastSequence = last,
				ProofCount = total,
				EvidenceHash = digest
			};
			// All validation and hashing precede this bounded atomic in-memory replacement.
			Book.RecentProofs.RemoveRange(0, compactCount);
			if (Book.CompactedProofs.Count >= MaxCompactedProofs)
			{
				Book.CompactedProofs.Clear();
				Book.CompactedProofs.Add(compact);
			}
			else Book.CompactedProofs.Add(compact);
			return true;
		}

		private static string ProofCompactionDigest(List<KingdomTradeProof> Proofs,
			List<KingdomTradeProofCompaction> Prior)
		{
			try
			{
				KingdomTradeBook evidence = new KingdomTradeBook
				{
					RecentProofs = new List<KingdomTradeProof>(Proofs),
					CompactedProofs = new List<KingdomTradeProofCompaction>(Prior)
				};
				byte[] encoded = KingdomTradeCodec.EncodePayload(evidence);
				using (MemoryStream canonical = new MemoryStream())
				{
					if (!WriteCanonicalField(canonical, IdentityNamespace)
						|| !WriteCanonicalField(canonical, "proof-compaction")) return null;
					WriteInt32(canonical, encoded.Length);
					canonical.Write(encoded, 0, encoded.Length);
					using (SHA256 sha = SHA256.Create())
						return Hex(sha.ComputeHash(canonical.ToArray()));
				}
			}
			catch { return null; }
		}

		public static bool Retire(KingdomTradeBook Book, KingdomTradeOperation Operation,
			KingdomTradePhase Disposition, long Tick, string Fault)
		{
			if (Book == null || Operation == null || Book.OpenOperation != Operation
				|| Operation.Sequence <= Book.RetiredThrough
				|| (Disposition != KingdomTradePhase.Terminal
					&& Disposition != KingdomTradePhase.Quarantined)
				|| (Disposition == KingdomTradePhase.Terminal
					&& Operation.Phase != KingdomTradePhase.RetirementReady)
				|| (Operation.Phase != KingdomTradePhase.RetirementReady
					&& Operation.Phase != KingdomTradePhase.Quarantined)) return false;
			if (HasUnresolvedEffects(Operation) || !DurableDomainSettled(Book, Operation))
			{
				Operation.Phase = KingdomTradePhase.Quarantined;
				Operation.Fault = AppendFault(Operation.Fault,
					"unresolved trade value or effects remain under this open receipt");
				return false;
			}
			if (Book.PendingRetirement != null || Book.RecentProofs == null
				|| Book.RecentProofs.Count >= MaxRecentProofs) return false;
			Operation.Phase = Disposition;
			Operation.UpdatedTick = Tick;
			Operation.Fault = Bound(Fault, MaxTextChars);
			Book.PendingRetirement = ProofFor(Book, Operation, Disposition, Tick, Fault);
			return CompletePendingRetirement(Book);
		}

		public static bool HasUnresolvedEffects(KingdomTradeOperation Operation)
		{
			if (Operation == null || Operation.AmbiguousWater > 0
				|| Operation.MaterialProved != Operation.MaterialRequested
				|| !ValidAccountingEvidence(Operation)
				|| Operation.ManifestEscrowState == KingdomTradePhysicalState.Lost
				|| Operation.RetainedState == KingdomTradePhysicalState.Lost) return true;
			if (Operation.Kind == KingdomTradeOperationKind.ManifestDelivery
				&& Operation.ManifestEscrowState != KingdomTradePhysicalState.Proved) return true;
			if (Operation.Kind == KingdomTradeOperationKind.ManifestLapse
				&& Operation.RetainedState != KingdomTradePhysicalState.Proved) return true;
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& (Operation.ProvedWater != Operation.RequestedWater
					|| !KingdomTradePatternRules.Terminal(Operation.Pattern))) return true;
			if (Operation.Kind == KingdomTradeOperationKind.ManifestLoad
				&& Operation.ProvedWater != Operation.RequestedWater) return true;
			if (Operation.WaterLegs == null) return true;
			long provedWater = 0L;
			for (int i = 0; i < Operation.WaterLegs.Count; i++)
			{
				KingdomTradeWaterLeg leg = Operation.WaterLegs[i];
					if (leg == null || (leg.State != KingdomTradePhysicalState.Proved
						&& leg.State != KingdomTradePhysicalState.Skipped)) return true;
				if (leg.State == KingdomTradePhysicalState.Proved) provedWater += leg.Delta;
			}
			if (provedWater != Operation.ProvedWater) return true;
			if (Operation.MaterialOutputs == null) return true;
			long provedMaterial = 0L;
			for (int i = 0; i < Operation.MaterialOutputs.Count; i++)
			{
				KingdomTradeMaterialOutput output = Operation.MaterialOutputs[i];
					if (output == null || output.State != KingdomTradePhysicalState.Proved
						|| (output.CleanupState != KingdomTradePhysicalState.None
							&& output.CleanupState != KingdomTradePhysicalState.Skipped
							&& output.CleanupState != KingdomTradePhysicalState.Proved)) return true;
				if (output.State == KingdomTradePhysicalState.Proved) provedMaterial += output.Count;
			}
			if (provedMaterial != Operation.MaterialProved) return true;
			if (!TerminalPhysical(Operation.ProjectionState)
				|| !TerminalPhysical(Operation.PriorCleanupState)
				|| (Operation.Standing != null
					&& !TerminalPhysical(Operation.Standing.State))) return true;
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery)
			{
				if ((Operation.Phase == KingdomTradePhase.RetirementReady
						|| Operation.Phase == KingdomTradePhase.Terminal)
					&& !TerminalCharterOutboxExact(Operation)) return true;
				if (Operation.Phase == KingdomTradePhase.Quarantined
					&& !TerminalCharterOutboxExact(Operation)
					&& !QuarantineCharterOutboxExact(Operation)) return true;
			}
			KingdomTradeOutbox box = Operation.Outbox;
			return box == null || !string.Equals(box.EventId, Operation.Id,
				StringComparison.Ordinal) || box.ChronicleState == KingdomTradeSinkState.Lost
				|| box.LedgerState == KingdomTradeSinkState.Lost
				|| box.MessageState == KingdomTradeSinkState.Lost
				|| box.DeedState == KingdomTradeSinkState.Lost
				|| !SinkSettled(box.ChronicleState) || !SinkSettled(box.LedgerState)
				|| !SinkSettled(box.MessageState) || !SinkSettled(box.DeedState);
		}

		/// <summary>Only a complete bounded Charter payload may reach external callbacks.</summary>
		public static bool CharterOutboxReadyForDispatch(KingdomTradeOperation Operation)
		{
			if (!CharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return DispatchableSink(box.ChronicleState) && DispatchableSink(box.LedgerState)
				&& DispatchableSink(box.MessageState) && DispatchableSink(box.DeedState);
		}

		/// <summary>Quarantine may dispatch either exact normal payload or its distinct alert payload.</summary>
		public static bool CharterOutboxSafeForQuarantineDispatch(KingdomTradeOperation Operation)
		{
			return CharterOutboxLaneShape(Operation) || QuarantineCharterOutboxLaneShape(Operation);
		}

		private static bool TerminalCharterOutboxExact(KingdomTradeOperation Operation)
		{
			if (!CharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return box.ChronicleState == KingdomTradeSinkState.Delivered
				&& box.LedgerState == KingdomTradeSinkState.Delivered
				&& box.MessageState == KingdomTradeSinkState.Delivered
				&& box.DeedState == KingdomTradeSinkState.Delivered;
		}

		private static bool QuarantineCharterOutboxExact(KingdomTradeOperation Operation)
		{
			if (!QuarantineCharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return box.ChronicleState == KingdomTradeSinkState.Delivered
				&& box.LedgerState == KingdomTradeSinkState.Delivered
				&& box.MessageState == KingdomTradeSinkState.Delivered
				&& box.DeedState == KingdomTradeSinkState.Skipped;
		}

		private static bool CharterOutboxLaneShape(KingdomTradeOperation Operation)
		{
			if (!CharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return MandatorySink(box.ChronicleState) && MandatorySink(box.LedgerState)
				&& MandatorySink(box.MessageState) && MandatorySink(box.DeedState);
		}

		private static bool QuarantineCharterOutboxLaneShape(KingdomTradeOperation Operation)
		{
			if (!QuarantineCharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return MandatorySink(box.ChronicleState) && MandatorySink(box.LedgerState)
				&& MandatorySink(box.MessageState) && box.DeedState == KingdomTradeSinkState.Skipped;
		}

		private static bool CharterOutboxPayloadExact(KingdomTradeOperation Operation)
		{
			KingdomTradeOutbox box = Operation?.Outbox;
			return Operation != null && Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& box != null && string.Equals(box.EventId, Operation.Id, StringComparison.Ordinal)
				&& ValidOutboxPayload(box.Chronicle) && ValidOutboxPayload(box.LedgerNote)
				&& ValidOutboxPayload(box.Message) && ValidOutboxPayload(box.Deed)
				&& box.LedgerDeliveredDelta == Operation.ProvedWater;
		}

		private static bool QuarantineCharterOutboxPayloadExact(KingdomTradeOperation Operation)
		{
			KingdomTradeOutbox box = Operation?.Outbox;
			return Operation != null && Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& box != null && string.Equals(box.EventId, Operation.Id, StringComparison.Ordinal)
				&& ValidOutboxPayload(box.Chronicle) && ValidOutboxPayload(box.LedgerNote)
				&& ValidOutboxPayload(box.Message) && string.IsNullOrEmpty(box.Deed)
				&& box.LedgerDeliveredDelta == 0;
		}

		private static bool ValidOutboxPayload(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Value.Length <= MaxTextChars;
		}

		private static bool MandatorySink(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Pending || State == KingdomTradeSinkState.Intent
				|| State == KingdomTradeSinkState.Delivered || State == KingdomTradeSinkState.Lost;
		}

		private static bool DispatchableSink(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Pending || State == KingdomTradeSinkState.Delivered;
		}

		private static bool TerminalPhysical(KingdomTradePhysicalState State)
		{
			return State == KingdomTradePhysicalState.None
				|| State == KingdomTradePhysicalState.Proved
				|| State == KingdomTradePhysicalState.Skipped;
		}

		private static bool DurableDomainSettled(KingdomTradeBook Book,
			KingdomTradeOperation Operation)
		{
			if (Book == null || Operation == null) return false;
			if (Operation.ProjectionState == KingdomTradePhysicalState.Proved)
			{
				int matches = 0;
				for (int i = 0; i < (Book.Projections?.Count ?? 0); i++)
				{
					KingdomTradeProjectionRow row = Book.Projections[i];
					if (row != null && !row.Quarantined
						&& row.OperationSequence == Operation.Sequence
						&& string.Equals(row.OperationId, Operation.Id, StringComparison.Ordinal)
						&& string.Equals(row.SettlementId, Operation.SettlementId, StringComparison.Ordinal)
						&& string.Equals(row.ZoneId, Operation.ZoneId, StringComparison.Ordinal)
						&& string.Equals(row.ProjectionId, Operation.ProjectionId, StringComparison.Ordinal)
						&& string.Equals(row.ObjectId, Operation.ProjectionObjectId, StringComparison.Ordinal))
						matches++;
				}
				if (matches != 1) return false;
			}
			KingdomTradeManifestState manifest = Book.Manifest;
			switch (Operation.Kind)
			{
			case KingdomTradeOperationKind.CharterDelivery:
				int schedules = 0;
				KingdomTradeCharter charter = null;
				for (int i = 0; i < (Book.Charters?.Count ?? 0); i++)
				{
					KingdomTradeCharter row = Book.Charters[i];
					if (row == null || !(string.Equals(row.Id, Operation.CharterId,
							StringComparison.Ordinal)
						|| (string.Equals(row.DealKey, Operation.DealKey,
								StringComparison.Ordinal)
							&& string.Equals(row.Faction, Operation.Faction,
								StringComparison.Ordinal)))) continue;
					schedules++;
					charter = row;
				}
				if (schedules != 1 || charter == null
					|| !string.Equals(charter.Id, Operation.CharterId, StringComparison.Ordinal)
					|| !string.Equals(charter.DealKey, Operation.DealKey, StringComparison.Ordinal)
					|| !string.Equals(charter.Faction, Operation.Faction, StringComparison.Ordinal)
					|| charter.Sequence <= 0L || charter.Sequence >= Book.NextCharterSequence
					|| !string.Equals(charter.Id, CharterId(Book.RealmId, charter.Sequence),
						StringComparison.Ordinal)
					|| charter.CreatedTick < 0L || charter.CreatedTick > Operation.CreatedTick)
					return false;
				if (Operation.Phase == KingdomTradePhase.RetirementReady
					|| Operation.Phase == KingdomTradePhase.Terminal)
					return !charter.Quarantined && charter.NextTick == Operation.DueAfter;
				if (Operation.Phase != KingdomTradePhase.Quarantined) return false;
				return charter.NextTick == Operation.DueAfter
					|| (charter.Quarantined && charter.NextTick == Operation.DueBefore);
			case KingdomTradeOperationKind.ManifestLoad:
				return manifest != null
					&& manifest.OperationSequence == Operation.Sequence
					&& string.Equals(manifest.OperationId, Operation.Id, StringComparison.Ordinal)
					&& string.Equals(manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
					&& manifest.EscrowDrams == Operation.ProvedWater;
			case KingdomTradeOperationKind.ManifestDelivery:
				return manifest != null
					&& string.Equals(manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
					&& manifest.EscrowDrams == Operation.ManifestEscrowAfter;
			case KingdomTradeOperationKind.ManifestTurnback:
				return manifest != null && manifest.TurnedBack
					&& string.Equals(manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
					&& string.Equals(manifest.OriginId, Operation.DestinationId, StringComparison.Ordinal)
					&& string.Equals(manifest.DestinationId, Operation.OriginId, StringComparison.Ordinal);
			case KingdomTradeOperationKind.ManifestLapse:
				return manifest != null
					&& string.Equals(manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
					&& Book.RetainedEscrowDrams == Operation.RetainedAfter;
			default:
				return true;
			}
		}

		private static KingdomTradeProof ProofFor(KingdomTradeBook Book, KingdomTradeOperation Operation,
			KingdomTradePhase Disposition, long Tick, string Fault)
		{
			KingdomTradeOutbox box = Operation.Outbox;
			return new KingdomTradeProof
				{
					RealmId = Book.RealmId, Sequence = Operation.Sequence, Id = Operation.Id, Kind = Operation.Kind,
					OperationEvidenceHash = OperationEvidenceDigest(Operation),
				Disposition = Disposition, RequestedWater = Operation.RequestedWater,
				ProvedWater = Operation.ProvedWater, AmbiguousWater = Operation.AmbiguousWater,
				SettlementId = Operation.SettlementId, ManifestId = Operation.ManifestId,
				ManifestEscrowBefore = Operation.ManifestEscrowBefore,
				ManifestEscrowDebit = Operation.ManifestEscrowDebit,
				ManifestEscrowAfter = Operation.ManifestEscrowAfter,
				ManifestEscrowState = Operation.ManifestEscrowState,
				RetainedBefore = Operation.RetainedBefore, RetainedDelta = Operation.RetainedDelta,
				RetainedAfter = Operation.RetainedAfter, RetainedState = Operation.RetainedState,
				MaterialRequested = Operation.MaterialRequested, MaterialProved = Operation.MaterialProved,
				ChronicleState = box == null ? KingdomTradeSinkState.Skipped : box.ChronicleState,
				LedgerState = box == null ? KingdomTradeSinkState.Skipped : box.LedgerState,
					MessageState = box == null ? KingdomTradeSinkState.Skipped : box.MessageState,
					DeedState = box == null ? KingdomTradeSinkState.Skipped : box.DeedState,
					ManifestCleanup = (Operation.Kind == KingdomTradeOperationKind.ManifestDelivery
						&& Operation.ManifestEscrowAfter == 0)
						|| Operation.Kind == KingdomTradeOperationKind.ManifestLapse,
					Tick = Tick < 0L ? 0L : Tick, Fault = Bound(Fault, MaxTextChars)
			};
		}

		private static string OperationEvidenceDigest(KingdomTradeOperation Operation)
		{
			try
			{
				KingdomTradeBook evidence = new KingdomTradeBook { OpenOperation = Operation };
				byte[] bytes = KingdomTradeCodec.EncodePayload(evidence);
				string inner;
				using (SHA256 sha = SHA256.Create()) inner = Hex(sha.ComputeHash(bytes));
				return CanonicalId("operation-proof", Operation.Sequence, Operation.Id, inner);
			}
			catch { return null; }
		}

		private static string OperationEvidenceDigestV3(KingdomTradeOperation Operation)
		{
			try
			{
				KingdomTradeBook evidence = new KingdomTradeBook
				{
					FormatVersion = 4,
					OpenOperation = Operation
				};
				byte[] bytes = KingdomTradeCodec.EncodePayloadV3ForMigration(evidence);
				string inner;
				using (SHA256 sha = SHA256.Create()) inner = Hex(sha.ComputeHash(bytes));
				return CanonicalId("operation-proof", Operation.Sequence, Operation.Id, inner);
			}
			catch { return null; }
		}

		/// <summary>Exact wire-v3/format-4 adoption; no current writer calls this seam.</summary>
		internal static void MigrateWireV3(KingdomTradeBook Book)
		{
			if (Book == null || Book.FormatVersion != 4) return;
			KingdomTradeOperation operation = Book.OpenOperation;
			KingdomTradeProof pending = Book.PendingRetirement;
			// The frozen v3 writer ignores this additive field, so establish a terminal
			// migration value before either success or quarantine can expose the book.
			if (operation != null && operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& operation.Pattern == null)
				operation.Pattern = KingdomTradePatternRules.PriorWireDefault();
			bool migratePending = operation != null && pending != null;
			if (migratePending)
			{
				string oldDigest = OperationEvidenceDigestV3(operation);
				if (!ValidId(oldDigest) || !string.Equals(pending.OperationEvidenceHash,
					oldDigest, StringComparison.Ordinal))
				{
					Book.FormatVersion = CurrentFormatVersion;
					QuarantineBook(Book,
						"wire-v3 pending retirement did not authenticate its exact prior operation");
					return;
				}
			}
			Book.FormatVersion = CurrentFormatVersion;
			if (migratePending)
				pending.OperationEvidenceHash = OperationEvidenceDigest(operation);
		}

		private static bool CompletePendingRetirement(KingdomTradeBook Book)
		{
			KingdomTradeProof proof = Book?.PendingRetirement;
			if (proof == null || Book.RecentProofs == null || !ValidProof(Book, proof, false))
				return false;
			int matches = 0;
			KingdomTradeProof existing = null;
			for (int i = 0; i < Book.RecentProofs.Count; i++)
				if (Book.RecentProofs[i] != null
					&& (Book.RecentProofs[i].Sequence == proof.Sequence
						|| string.Equals(Book.RecentProofs[i].Id, proof.Id, StringComparison.Ordinal)))
				{
					matches++;
					existing = Book.RecentProofs[i];
				}
			if (matches > 1) { QuarantineBook(Book, "duplicate retirement receipt"); return false; }
			if (matches == 1 && !ExactProof(existing, proof))
			{
				QuarantineBook(Book, "colliding retirement receipt differs from pending evidence");
				return false;
			}
			if (Book.OpenOperation != null)
			{
				if (!ProofMatchesOperation(Book, proof, Book.OpenOperation)
					|| HasUnresolvedEffects(Book.OpenOperation)
					|| !DurableDomainSettled(Book, Book.OpenOperation)) return false;
			}
			else if (matches != 1 || Book.RetiredThrough < proof.Sequence) return false;
			if (!ManifestCleanupExactOrDone(Book, proof)) return false;
			if (matches == 0)
			{
				if (Book.RecentProofs.Count >= MaxRecentProofs) return false;
				Book.RecentProofs.Add(proof);
			}
			Book.RetiredThrough = Math.Max(Book.RetiredThrough, proof.Sequence);
			if (Book.OpenOperation != null) Book.OpenOperation = null;
			if (!CompleteManifestCleanup(Book, proof)) return false;
			Book.PendingRetirement = null;
			return true;
		}

		private static bool CompleteManifestCleanup(KingdomTradeBook Book,
			KingdomTradeProof Proof)
		{
			if (!ManifestCleanupExactOrDone(Book, Proof)) return false;
			if (!Proof.ManifestCleanup || Book.Manifest == null) return true;
			Book.Manifest = null;
			return true;
		}

		private static bool ManifestCleanupExactOrDone(KingdomTradeBook Book,
			KingdomTradeProof Proof)
		{
			if (!Proof.ManifestCleanup) return true;
			KingdomTradeManifestState manifest = Book.Manifest;
			if (manifest == null) return Book.OpenOperation == null;
			bool exact = string.Equals(manifest.Id, Proof.ManifestId, StringComparison.Ordinal);
			if (Proof.Kind == KingdomTradeOperationKind.ManifestDelivery)
				exact = exact && manifest.Status == KingdomTradeManifestStatus.Delivered
					&& manifest.EscrowDrams == 0 && Proof.ManifestEscrowAfter == 0;
			else if (Proof.Kind == KingdomTradeOperationKind.ManifestLapse)
				exact = exact && manifest.Status == KingdomTradeManifestStatus.Quarantined
					&& manifest.EscrowDrams == Proof.RequestedWater
					&& Proof.RetainedDelta == Proof.RequestedWater;
			else exact = false;
			return exact;
		}

		private static bool ProofMatchesOperation(KingdomTradeBook Book,
			KingdomTradeProof Proof, KingdomTradeOperation Operation)
		{
			if (Book == null || Proof == null || Operation == null) return false;
			KingdomTradeProof expected = ProofFor(Book, Operation, Proof.Disposition,
				Proof.Tick, Proof.Fault);
			return ExactProof(Proof, expected)
				&& Operation.Phase == Proof.Disposition
				&& Operation.UpdatedTick == Proof.Tick;
		}

		private static bool ExactProof(KingdomTradeProof Left, KingdomTradeProof Right)
		{
			return Left != null && Right != null
				&& string.Equals(Left.RealmId, Right.RealmId, StringComparison.Ordinal)
				&& Left.Sequence == Right.Sequence
				&& string.Equals(Left.Id, Right.Id, StringComparison.Ordinal)
				&& string.Equals(Left.OperationEvidenceHash, Right.OperationEvidenceHash,
					StringComparison.Ordinal)
				&& Left.Kind == Right.Kind && Left.Disposition == Right.Disposition
				&& Left.ProvedWater == Right.ProvedWater
				&& Left.AmbiguousWater == Right.AmbiguousWater
				&& Left.RequestedWater == Right.RequestedWater
				&& string.Equals(Left.SettlementId, Right.SettlementId, StringComparison.Ordinal)
				&& string.Equals(Left.ManifestId, Right.ManifestId, StringComparison.Ordinal)
				&& Left.ManifestEscrowBefore == Right.ManifestEscrowBefore
				&& Left.ManifestEscrowDebit == Right.ManifestEscrowDebit
				&& Left.ManifestEscrowAfter == Right.ManifestEscrowAfter
				&& Left.ManifestEscrowState == Right.ManifestEscrowState
				&& Left.RetainedBefore == Right.RetainedBefore
				&& Left.RetainedDelta == Right.RetainedDelta
				&& Left.RetainedAfter == Right.RetainedAfter
				&& Left.RetainedState == Right.RetainedState
				&& Left.MaterialRequested == Right.MaterialRequested
				&& Left.MaterialProved == Right.MaterialProved
				&& Left.ChronicleState == Right.ChronicleState
				&& Left.LedgerState == Right.LedgerState
				&& Left.MessageState == Right.MessageState
				&& Left.DeedState == Right.DeedState
				&& Left.ManifestCleanup == Right.ManifestCleanup
				&& Left.Tick == Right.Tick
				&& string.Equals(Left.Fault, Right.Fault, StringComparison.Ordinal);
		}

		public static int DueCharterIndex(KingdomTradeBook Book, long Now)
		{
			if (Book == null || Book.OpenOperation != null || Book.Charters == null) return -1;
			int chosen = -1;
			for (int i = 0; i < Book.Charters.Count; i++)
			{
				KingdomTradeCharter row = Book.Charters[i];
				if (row == null || row.Quarantined || row.NextTick > Now) continue;
				if (chosen < 0 || row.NextTick < Book.Charters[chosen].NextTick
					|| (row.NextTick == Book.Charters[chosen].NextTick
						&& string.CompareOrdinal(row.Id, Book.Charters[chosen].Id) < 0)) chosen = i;
			}
			return chosen;
		}

		public static int ConservedManifestWater(int SourceBefore, int ProvedDebited,
			int EscrowBefore, int ProvedDelivered, int Retained)
		{
			long value = (long)SourceBefore - ProvedDebited + EscrowBefore
				- ProvedDelivered + Retained;
			if (value <= 0L) return 0;
			return value >= int.MaxValue ? int.MaxValue : (int)value;
		}

		public static bool TryReconcileEscrow(int Before, int Debit, int Current,
			out int After, out bool Apply)
		{
			After = 0;
			Apply = false;
			if (Before < 0 || Debit < 0 || Debit > Before || Current < 0) return false;
			After = Before - Debit;
			if (Current == Before) { Apply = true; return true; }
			return Current == After;
		}

		public static bool TryReconcileRetained(long Before, long Delta, long Current,
			out long After, out bool Apply)
		{
			After = 0L;
			Apply = false;
			if (Before < 0L || Delta < 0L || Current < 0L
				|| Delta > long.MaxValue - Before) return false;
			After = Before + Delta;
			if (Current == Before) { Apply = true; return true; }
			return Current == After;
		}

		public static bool TryAddEscrow(long Left, long Right, out long Result)
		{
			Result = 0L;
			if (Left < 0L || Right < 0L || Right > long.MaxValue - Left) return false;
			Result = Left + Right;
			return true;
		}

		private static bool ValidAccountingEvidence(KingdomTradeOperation Operation)
		{
			if (Operation == null || Operation.ManifestEscrowBefore < 0
				|| Operation.ManifestEscrowDebit < 0
				|| Operation.ManifestEscrowDebit > Operation.ManifestEscrowBefore
				|| Operation.ManifestEscrowAfter
					!= Operation.ManifestEscrowBefore - Operation.ManifestEscrowDebit
				|| Operation.RetainedBefore < 0L || Operation.RetainedDelta < 0L
				|| Operation.RetainedDelta > long.MaxValue - Operation.RetainedBefore
				|| Operation.RetainedAfter != Operation.RetainedBefore + Operation.RetainedDelta)
				return false;
			switch (Operation.Kind)
			{
			case KingdomTradeOperationKind.ManifestDelivery:
				return Operation.ManifestEscrowBefore == Operation.RequestedWater
					&& (Operation.ManifestEscrowState == KingdomTradePhysicalState.Prepared
						? Operation.ManifestEscrowDebit == 0
						: Operation.ManifestEscrowDebit == Operation.ProvedWater)
					&& Operation.ManifestEscrowState != KingdomTradePhysicalState.None
					&& Operation.RetainedBefore == 0L && Operation.RetainedDelta == 0L
					&& Operation.RetainedAfter == 0L
					&& Operation.RetainedState == KingdomTradePhysicalState.None;
			case KingdomTradeOperationKind.ManifestLapse:
				return Operation.RetainedDelta == Operation.RequestedWater
					&& Operation.RetainedState != KingdomTradePhysicalState.None
					&& Operation.ManifestEscrowBefore == 0
					&& Operation.ManifestEscrowDebit == 0
					&& Operation.ManifestEscrowAfter == 0
					&& Operation.ManifestEscrowState == KingdomTradePhysicalState.None;
			default:
				return Operation.ManifestEscrowBefore == 0
					&& Operation.ManifestEscrowDebit == 0
					&& Operation.ManifestEscrowAfter == 0
					&& Operation.ManifestEscrowState == KingdomTradePhysicalState.None
					&& Operation.RetainedBefore == 0L && Operation.RetainedDelta == 0L
					&& Operation.RetainedAfter == 0L
					&& Operation.RetainedState == KingdomTradePhysicalState.None;
			}
		}

		/// <summary>Strict repair. Malformed authority is quarantined, never guessed into validity.</summary>
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

		private static void NormalizeManifest(KingdomTradeBook Book, KingdomTradeManifestState Manifest,
			bool MalformedRealm)
		{
			if (Manifest == null) return;
			bool oversized = TooLong(Manifest.OperationId, MaxIdChars)
				|| TooLong(Manifest.Id, MaxIdChars)
				|| TooLong(Manifest.OriginId, MaxIdChars)
				|| TooLong(Manifest.DestinationId, MaxIdChars)
				|| TooLong(Manifest.OriginName, MaxNameChars)
				|| TooLong(Manifest.DestinationName, MaxNameChars)
				|| TooLong(Manifest.Fault, MaxTextChars);
			bool malformed = MalformedRealm || oversized || Manifest.OperationSequence <= 0L
				|| !string.Equals(Manifest.OperationId,
					OperationId(Book.RealmId, Manifest.OperationSequence), StringComparison.Ordinal)
				|| !string.Equals(Manifest.Id, ManifestId(Manifest.OperationId), StringComparison.Ordinal)
				|| !IdentityContainsSettlement(Book, Manifest.OriginId)
				|| !IdentityContainsSettlement(Book, Manifest.DestinationId) || !ValidName(Manifest.OriginName)
				|| !ValidName(Manifest.DestinationName) || Manifest.OriginalDrams <= 0
				|| Manifest.OriginalDrams > MaxOperationWater || Manifest.EscrowDrams < 0
				|| Manifest.EscrowDrams > Manifest.OriginalDrams || Manifest.LoadedTick < 0L
				|| Manifest.DeadlineTick < Manifest.LoadedTick
				|| !Enum.IsDefined(typeof(KingdomTradeManifestStatus), Manifest.Status)
				|| (Manifest.Status == KingdomTradeManifestStatus.Delivered
					&& Manifest.EscrowDrams != 0)
				|| (Manifest.Status == KingdomTradeManifestStatus.InFlight
					&& Manifest.EscrowDrams == 0);
			if (malformed)
			{
				Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Manifest.Fault = AppendFault(Manifest.Fault, "malformed manifest authority");
			}
		}

		private static void NormalizeOperation(KingdomTradeBook Book)
		{
			KingdomTradeOperation operation = Book.OpenOperation;
			if (operation == null) return;
			if (operation.Sequence <= Book.RetiredThrough)
			{
				if (Book.PendingRetirement != null) return;
				int matches = 0;
				KingdomTradeProof exact = null;
				for (int i = 0; i < Book.RecentProofs.Count; i++)
					if (Book.RecentProofs[i] != null
						&& (Book.RecentProofs[i].Sequence == operation.Sequence
							|| string.Equals(Book.RecentProofs[i].Id, operation.Id, StringComparison.Ordinal)))
					{
						matches++;
						exact = Book.RecentProofs[i];
					}
				if (matches == 1 && ValidProof(Book, exact, true)
					&& ProofMatchesOperation(Book, exact, operation)) Book.OpenOperation = null;
				else
				{
					operation.Phase = KingdomTradePhase.Quarantined;
					operation.Fault = AppendFault(operation.Fault,
						"retirement barrier lacks an exact completed receipt; open evidence was preserved");
					QuarantineBook(Book, operation.Fault);
				}
				return;
			}
			bool oversized = TooLong(operation.Id, MaxIdChars)
				|| TooLong(operation.ZoneId, MaxNameChars)
				|| TooLong(operation.SettlementId, MaxIdChars)
				|| TooLong(operation.SettlementName, MaxNameChars)
				|| TooLong(operation.CharterId, MaxIdChars)
				|| TooLong(operation.ManifestId, MaxIdChars)
				|| TooLong(operation.DealKey, MaxNameChars)
				|| TooLong(operation.DealDisplayName, MaxNameChars)
				|| TooLong(operation.Faction, MaxNameChars)
				|| TooLong(operation.CaravanBlueprint, MaxNameChars)
				|| TooLong(operation.ProjectionId, MaxIdChars)
				|| TooLong(operation.ProjectionObjectId, MaxIdChars)
				|| TooLong(operation.PriorProjectionId, MaxIdChars)
				|| TooLong(operation.PriorProjectionObjectId, MaxIdChars)
				|| TooLong(operation.PriorProjectionZoneId, MaxNameChars)
				|| TooLong(operation.MaterialClaim, MaxClaimChars)
				|| TooLong(operation.OriginId, MaxIdChars)
				|| TooLong(operation.DestinationId, MaxIdChars)
				|| TooLong(operation.OriginName, MaxNameChars)
				|| TooLong(operation.DestinationName, MaxNameChars)
				|| TooLong(operation.Fault, MaxTextChars);
			if (operation.WaterLegs == null || operation.MaterialOutputs == null)
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault, "missing operation evidence list");
				return;
			}
			bool malformed = oversized || operation.Sequence <= 0L
				|| !string.Equals(operation.Id, OperationId(Book.RealmId, operation.Sequence), StringComparison.Ordinal)
				|| !ValidName(operation.ZoneId) || !IdentityContainsSettlement(Book, operation.SettlementId)
				|| !ValidName(operation.SettlementName)
				|| operation.Kind == KingdomTradeOperationKind.None
				|| !Enum.IsDefined(typeof(KingdomTradeOperationKind), operation.Kind)
				|| !Enum.IsDefined(typeof(KingdomTradePhase), operation.Phase)
				|| operation.Phase == KingdomTradePhase.Invalid
				|| operation.CreatedTick < 0L || operation.UpdatedTick < 0L
				|| operation.RequestedWater < 0 || operation.RequestedWater > MaxOperationWater
				|| operation.ProvedWater < 0 || operation.ProvedWater > operation.RequestedWater
				|| operation.AmbiguousWater < 0 || operation.MaterialRequested < 0
				|| operation.MaterialProved < 0 || operation.MaterialProved > operation.MaterialRequested
				|| operation.ManifestEscrowBefore < 0 || operation.ManifestEscrowDebit < 0
				|| operation.ManifestEscrowDebit > operation.ManifestEscrowBefore
				|| operation.ManifestEscrowAfter != operation.ManifestEscrowBefore - operation.ManifestEscrowDebit
				|| operation.RetainedBefore < 0L || operation.RetainedDelta < 0L
				|| operation.RetainedDelta > long.MaxValue - operation.RetainedBefore
				|| operation.RetainedAfter != operation.RetainedBefore + operation.RetainedDelta
				|| !Enum.IsDefined(typeof(KingdomTradeWaterDirection), operation.WaterDirection)
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), operation.ProjectionState)
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), operation.PriorCleanupState)
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), operation.ManifestEscrowState)
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), operation.RetainedState)
				|| operation.WaterLegs.Count > MaxWaterLegs
				|| operation.MaterialOutputs.Count > MaxMaterialOutputs;
			if (!ValidAccountingEvidence(operation)) malformed = true;
			if (!string.IsNullOrEmpty(operation.ProjectionId)
				&& !string.Equals(operation.ProjectionId, ProjectionId(operation.Id), StringComparison.Ordinal)) malformed = true;
			if (operation.Outbox != null && !string.Equals(operation.Outbox.EventId,
				operation.Id, StringComparison.Ordinal)) malformed = true;
			int provedWater = 0;
			int plannedWater = 0;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
			{
				KingdomTradeWaterLeg leg = operation.WaterLegs[i];
				if (leg == null || !NormalizeWaterLeg(leg, operation.WaterDirection)) malformed = true;
				if (leg != null)
				{
					plannedWater = SaturatingAdd(plannedWater, leg.Delta);
					if (leg.State == KingdomTradePhysicalState.Proved)
						provedWater = SaturatingAdd(provedWater, leg.Delta);
					for (int j = 0; j < i; j++)
						if (operation.WaterLegs[j] != null && string.Equals(
							operation.WaterLegs[j].OwnerId, leg.OwnerId,
							StringComparison.Ordinal)) malformed = true;
				}
				if (leg != null && leg.State == KingdomTradePhysicalState.Intent)
				{
					operation.AmbiguousWater = Math.Max(operation.AmbiguousWater,
						operation.RequestedWater - operation.ProvedWater);
					operation.Phase = KingdomTradePhase.Quarantined;
					operation.Fault = AppendFault(operation.Fault,
						"reloaded water intent lacks live part witnesses");
				}
			}
			if (plannedWater > operation.RequestedWater || provedWater != operation.ProvedWater)
				malformed = true;
			int provedMaterial = 0;
			int plannedMaterial = 0;
			for (int i = 0; i < operation.MaterialOutputs.Count; i++)
			{
				KingdomTradeMaterialOutput output = operation.MaterialOutputs[i];
				bool createIntent = output != null
					&& output.State == KingdomTradePhysicalState.CreateIntent;
				bool cleanupIntent = output != null
					&& output.CleanupState == KingdomTradePhysicalState.CleanupIntent;
				if (output == null || !NormalizeMaterial(output)) malformed = true;
				if (output != null)
				{
					if (!ValidMaterialMarker(operation.Id, output.Marker)) malformed = true;
					plannedMaterial = SaturatingAdd(plannedMaterial, output.Count);
					if (output.State == KingdomTradePhysicalState.Proved)
						provedMaterial = SaturatingAdd(provedMaterial, output.Count);
					for (int j = 0; j < i; j++)
					{
						KingdomTradeMaterialOutput prior = operation.MaterialOutputs[j];
						if (prior != null && (string.Equals(prior.OutputId, output.OutputId,
							StringComparison.Ordinal) || string.Equals(prior.Marker, output.Marker,
							StringComparison.Ordinal))) malformed = true;
					}
				}
				if (createIntent || cleanupIntent)
				{
					operation.Phase = KingdomTradePhase.Quarantined;
					operation.Fault = AppendFault(operation.Fault,
						"reloaded material creation or cleanup intent is uninspectable and was not replayed");
				}
			}
			if (operation.ProjectionState == KingdomTradePhysicalState.CreateIntent)
			{
				operation.ProjectionState = KingdomTradePhysicalState.Lost;
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"reloaded projection creation intent is uninspectable and was not replayed");
			}
			if (operation.PriorCleanupState == KingdomTradePhysicalState.Intent
				|| operation.PriorCleanupState == KingdomTradePhysicalState.CleanupIntent)
			{
				operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"reloaded projection cleanup intent is uninspectable and was not replayed");
			}
			if (operation.ManifestEscrowState == KingdomTradePhysicalState.Lost
				|| operation.RetainedState == KingdomTradePhysicalState.Lost)
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"unresolved manifest accounting evidence remains open");
			}
			if (plannedMaterial > operation.MaterialRequested
				|| provedMaterial != operation.MaterialProved) malformed = true;
			if (operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& (!ValidId(operation.CharterId) || !ValidName(operation.DealKey)
					|| !ValidName(operation.Faction) || operation.Cycles <= 0
					|| operation.IncomePerCycle < 0
					|| operation.RequestedWater != SaturatingMultiply(operation.IncomePerCycle, operation.Cycles)
					|| operation.IntervalTicks <= 0L || operation.DueBefore < 0L
					|| operation.DueAfter != SaturatingAdd(operation.CreatedTick,
						operation.IntervalTicks))) malformed = true;
			if (operation.Kind != KingdomTradeOperationKind.CharterDelivery
				&& (!ValidId(operation.ManifestId) || !IdentityContainsSettlement(Book, operation.OriginId)
					|| !IdentityContainsSettlement(Book, operation.DestinationId) || !ValidName(operation.OriginName)
					|| !ValidName(operation.DestinationName))) malformed = true;
			if (operation.Kind == KingdomTradeOperationKind.ManifestLoad
				&& !string.Equals(operation.ManifestId, ManifestId(operation.Id), StringComparison.Ordinal)) malformed = true;
			if ((operation.Kind == KingdomTradeOperationKind.ManifestLoad
					&& operation.WaterDirection != KingdomTradeWaterDirection.Debit)
				|| ((operation.Kind == KingdomTradeOperationKind.CharterDelivery
						|| operation.Kind == KingdomTradeOperationKind.ManifestDelivery)
					&& operation.WaterDirection != KingdomTradeWaterDirection.Credit)
				|| ((operation.Kind == KingdomTradeOperationKind.ManifestTurnback
						|| operation.Kind == KingdomTradeOperationKind.ManifestLapse)
					&& operation.WaterDirection != KingdomTradeWaterDirection.None)) malformed = true;
			NormalizeStanding(operation.Standing, ref malformed);
			if (operation.Standing != null
				&& operation.Standing.State == KingdomTradePhysicalState.Intent)
			{
				operation.Standing.State = KingdomTradePhysicalState.Lost;
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"reloaded standing callback intent is uninspectable and was not replayed");
			}
			NormalizeOutbox(operation.Outbox, ref malformed);
			if (operation.Kind == KingdomTradeOperationKind.CharterDelivery)
			{
				if (operation.Pattern == null
					|| !KingdomTradePatternRules.Normalize(operation.Pattern)) malformed = true;
				bool terminalLane = operation.Phase == KingdomTradePhase.ScheduleIntent
					|| operation.Phase == KingdomTradePhase.RetirementReady
					|| operation.Phase == KingdomTradePhase.Terminal;
				if (operation.Phase == KingdomTradePhase.Quarantined)
				{
					if (operation.Outbox != null && !CharterOutboxLaneShape(operation)
						&& !QuarantineCharterOutboxLaneShape(operation)) malformed = true;
				}
				else
				{
					if ((operation.Outbox != null && !CharterOutboxLaneShape(operation))
						|| ((operation.Phase == KingdomTradePhase.Sinks || terminalLane)
							&& operation.Outbox == null)
						|| (terminalLane && !TerminalCharterOutboxExact(operation))) malformed = true;
				}
			}
			else if (operation.Pattern != null) malformed = true;
			if (operation.Outbox != null && (operation.Outbox.ChronicleState == KingdomTradeSinkState.Lost
				|| operation.Outbox.LedgerState == KingdomTradeSinkState.Lost
				|| operation.Outbox.MessageState == KingdomTradeSinkState.Lost
				|| operation.Outbox.DeedState == KingdomTradeSinkState.Lost))
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"reloaded outbox intent has an unresolved external effect");
			}
			bool exactPendingIdentity = Book.PendingRetirement != null
				&& Book.PendingRetirement.Sequence == operation.Sequence
				&& string.Equals(Book.PendingRetirement.Id, operation.Id, StringComparison.Ordinal);
			if (operation.Phase == KingdomTradePhase.Terminal && !exactPendingIdentity)
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault,
					"terminal operation remained open past retirement");
			}
			if (malformed)
			{
				operation.Phase = KingdomTradePhase.Quarantined;
				operation.Fault = AppendFault(operation.Fault, "malformed open trade operation");
			}
		}

		private static void NormalizeProjections(KingdomTradeBook Book)
		{
			if (Book.Projections == null)
			{
				QuarantineBook(Book, "missing projection evidence list");
				return;
			}
			if (Book.Projections.Count > MaxProjectionRows)
			{
				Book.SchemaState = KingdomTradeSchemaState.Quarantined;
				Book.SchemaFault = AppendFault(Book.SchemaFault,
					"active per-city projection row cap exceeded; no authority rows were discarded");
				return;
			}
			for (int i = 0; i < Book.Projections.Count; i++)
			{
				KingdomTradeProjectionRow row = Book.Projections[i];
				if (row == null)
				{
					Book.SchemaState = KingdomTradeSchemaState.Quarantined;
					Book.SchemaFault = AppendFault(Book.SchemaFault,
						"null active projection authority row");
					return;
				}
				bool oversized = TooLong(row.OperationId, MaxIdChars)
					|| TooLong(row.SettlementId, MaxIdChars)
					|| TooLong(row.ZoneId, MaxNameChars)
					|| TooLong(row.ProjectionId, MaxIdChars)
					|| TooLong(row.ObjectId, MaxIdChars);
				if (oversized || row.OperationSequence <= 0L
					|| !string.Equals(row.OperationId,
						OperationId(Book.RealmId, row.OperationSequence), StringComparison.Ordinal)
					|| !IdentityContainsSettlement(Book, row.SettlementId) || !ValidName(row.ZoneId)
					|| !string.Equals(row.ProjectionId, ProjectionId(row.OperationId), StringComparison.Ordinal)
					|| !ValidId(row.ObjectId) || TooLong(row.Fault, MaxTextChars))
				{
					row.Quarantined = true;
					row.Fault = AppendFault(row.Fault, "malformed projection authority row");
				}
			}
			for (int i = 0; i < Book.Projections.Count; i++)
			{
				KingdomTradeProjectionRow left = Book.Projections[i];
				for (int j = i + 1; j < Book.Projections.Count; j++)
				{
					KingdomTradeProjectionRow right = Book.Projections[j];
					if (!(string.Equals(left.SettlementId, right.SettlementId,
							StringComparison.Ordinal)
						|| string.Equals(left.ProjectionId, right.ProjectionId,
							StringComparison.Ordinal)
						|| string.Equals(left.ObjectId, right.ObjectId,
							StringComparison.Ordinal))) continue;
					left.Quarantined = true;
					right.Quarantined = true;
					left.Fault = AppendFault(left.Fault, "duplicate projection authority");
					right.Fault = AppendFault(right.Fault, "duplicate projection authority");
				}
			}
		}

		private static bool NormalizeWaterLeg(KingdomTradeWaterLeg Leg,
			KingdomTradeWaterDirection Direction)
		{
			bool oversized = TooLong(Leg.OwnerId, MaxIdChars)
				|| TooLong(Leg.ZoneId, MaxNameChars)
				|| TooLong(Leg.BeforeComposition, 64) || TooLong(Leg.AfterComposition, 64);
			return !oversized && ValidId(Leg.OwnerId) && ValidName(Leg.ZoneId) && Leg.Capacity >= 0
				&& Leg.Before >= 0 && Leg.Before <= Leg.Capacity && Leg.Delta > 0
				&& Leg.After >= 0 && Leg.After <= Leg.Capacity
				&& ((Direction == KingdomTradeWaterDirection.Debit && Leg.After == Leg.Before - Leg.Delta)
					|| (Direction == KingdomTradeWaterDirection.Credit && Leg.After == Leg.Before + Leg.Delta))
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Leg.State);
		}

		private static bool NormalizeMaterial(KingdomTradeMaterialOutput Output)
		{
			bool oversized = TooLong(Output.OutputId, MaxIdChars)
				|| TooLong(Output.Marker, MaxIdChars) || TooLong(Output.Blueprint, MaxNameChars)
				|| TooLong(Output.DestinationOwnerId, MaxIdChars)
				|| TooLong(Output.ZoneId, MaxNameChars);
			bool creating = Output.State == KingdomTradePhysicalState.CreateIntent;
			if (creating)
			{
				Output.State = KingdomTradePhysicalState.Lost;
			}
			if (Output.CleanupState == KingdomTradePhysicalState.CleanupIntent)
			{
				Output.CleanupState = KingdomTradePhysicalState.Lost;
			}
			return !oversized && (creating ? string.IsNullOrEmpty(Output.OutputId) : ValidId(Output.OutputId))
				&& ValidId(Output.Marker)
				&& ValidName(Output.Blueprint) && ValidId(Output.DestinationOwnerId)
				&& ValidName(Output.ZoneId) && Output.Count > 0
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Output.State)
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Output.CleanupState);
		}

		private static void NormalizeStanding(KingdomTradeStandingCas Standing, ref bool Malformed)
		{
			if (Standing == null) return;
			if (TooLong(Standing.Faction, MaxNameChars)) Malformed = true;
			long expected = (long)Standing.Before + Standing.Delta;
			if (!ValidName(Standing.Faction) || expected < int.MinValue || expected > int.MaxValue
				|| Standing.After != (int)expected
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), Standing.State)) Malformed = true;
		}

		private static void NormalizeOutbox(KingdomTradeOutbox Outbox, ref bool Malformed)
		{
			if (Outbox == null) return;
			if (TooLong(Outbox.EventId, MaxIdChars) || TooLong(Outbox.Chronicle, MaxTextChars)
				|| TooLong(Outbox.LedgerNote, MaxTextChars) || TooLong(Outbox.Message, MaxTextChars)
				|| TooLong(Outbox.Deed, MaxTextChars)) Malformed = true;
			if (Outbox.LedgerDeliveredDelta < 0) Malformed = true;
			Outbox.ChronicleState = ResumeSink(Outbox.ChronicleState);
			Outbox.LedgerState = ResumeSink(Outbox.LedgerState);
			Outbox.MessageState = ResumeSink(Outbox.MessageState);
			Outbox.DeedState = ResumeSink(Outbox.DeedState);
			NormalizeSink(ref Outbox.ChronicleState,
				!string.IsNullOrEmpty(Outbox.Chronicle), ref Malformed);
			NormalizeSink(ref Outbox.LedgerState,
				!string.IsNullOrEmpty(Outbox.LedgerNote)
					|| Outbox.LedgerDeliveredDelta > 0, ref Malformed);
			NormalizeSink(ref Outbox.MessageState,
				!string.IsNullOrEmpty(Outbox.Message), ref Malformed);
			NormalizeSink(ref Outbox.DeedState,
				!string.IsNullOrEmpty(Outbox.Deed), ref Malformed);
			if (!ValidId(Outbox.EventId)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Outbox.ChronicleState)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Outbox.LedgerState)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Outbox.MessageState)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Outbox.DeedState)) Malformed = true;
		}

		private static void NormalizeSink(ref KingdomTradeSinkState State,
			bool HasPayload, ref bool Malformed)
		{
			if (!Enum.IsDefined(typeof(KingdomTradeSinkState), State))
			{
				Malformed = true;
				return;
			}
			if (State == KingdomTradeSinkState.None)
			{
				if (HasPayload) Malformed = true;
				else State = KingdomTradeSinkState.Skipped;
				return;
			}
			if (HasPayload == (State == KingdomTradeSinkState.Skipped)) Malformed = true;
		}

		private static void NormalizeProofs(KingdomTradeBook Book)
		{
			if (Book.RecentProofs == null || Book.RecentProofs.Count > MaxRecentProofs)
			{
				QuarantineBook(Book, "retirement proof list is missing or oversized");
				return;
			}
			for (int i = 0; i < Book.RecentProofs.Count; i++)
			{
				KingdomTradeProof proof = Book.RecentProofs[i];
				bool exactPending = Book.PendingRetirement != null
					&& ExactProof(proof, Book.PendingRetirement);
				if (!ValidProof(Book, proof, !exactPending))
				{
					QuarantineBook(Book, "malformed retirement proof was preserved");
					return;
				}
				for (int j = 0; j < i; j++)
					if (Book.RecentProofs[j].Sequence == proof.Sequence
						|| string.Equals(Book.RecentProofs[j].Id, proof.Id, StringComparison.Ordinal))
					{
						QuarantineBook(Book, "duplicate retirement proofs were preserved symmetrically");
						return;
					}
			}
		}

		private static void NormalizeProofCompactions(KingdomTradeBook Book)
		{
			if (Book.CompactedProofs == null
				|| Book.CompactedProofs.Count > MaxCompactedProofs)
			{
				QuarantineBook(Book, "compacted retirement proof list is missing or oversized");
				return;
			}
			for (int i = 0; i < Book.CompactedProofs.Count; i++)
				if (!ValidProofCompaction(Book.CompactedProofs[i]))
				{
					QuarantineBook(Book, "malformed compacted retirement proof was preserved");
					return;
				}
		}

		private static bool ValidProofCompaction(KingdomTradeProofCompaction Row)
		{
			return Row != null && ValidId(Row.RealmId) && Row.FirstSequence > 0L
				&& Row.LastSequence >= Row.FirstSequence && Row.ProofCount > 0
				&& ValidId(Row.EvidenceHash);
		}

		private static void NormalizeArchives(KingdomTradeBook Book)
		{
			if (Book.Archives == null || Book.Archives.Count > MaxArchives)
			{
				QuarantineBook(Book, "archive evidence list is missing or oversized");
				return;
			}
			for (int i = 0; i < Book.Archives.Count; i++)
			{
				KingdomTradeArchive row = Book.Archives[i];
				if (!ValidArchiveEvidence(row))
				{
					QuarantineBook(Book, "malformed archive evidence was preserved");
					return;
				}
				for (int j = 0; j < i; j++)
					if (string.Equals(Book.Archives[j].RealmId, row.RealmId,
						StringComparison.Ordinal))
					{
						QuarantineBook(Book, "duplicate realm archive evidence was preserved symmetrically");
						return;
					}
			}
		}

		private static bool ValidArchiveEvidence(KingdomTradeArchive Row)
		{
			return Row != null && !TooLong(Row.RealmId, MaxIdChars) && ValidId(Row.RealmId)
				&& ValidSettlementSet(Row.SettlementIds)
				&& Row.RetainedEscrowDrams >= 0L && Row.ManifestEscrowDrams >= 0
				&& Enum.IsDefined(typeof(KingdomTradeManifestStatus), Row.ManifestStatus)
				&& (Row.ManifestStatus == KingdomTradeManifestStatus.None
					? string.IsNullOrEmpty(Row.ManifestId) && Row.ManifestEscrowDrams == 0
					: ValidId(Row.ManifestId))
				&& Row.CharterCount >= 0 && Row.CharterCount <= MaxCharters
				&& Row.ProjectionCount >= 0 && Row.ProjectionCount <= MaxProjectionRows
				&& Row.ProofCount >= 0 && Row.OpenRequestedWater >= 0
				&& Row.OpenProvedWater >= 0 && Row.OpenProvedWater <= Row.OpenRequestedWater
				&& Row.OpenAmbiguousWater >= 0 && Row.RetiredThrough >= 0L
				&& (string.IsNullOrEmpty(Row.OpenOperationId) || ValidId(Row.OpenOperationId))
				&& (string.IsNullOrEmpty(Row.PendingRetirementId)
					|| ValidId(Row.PendingRetirementId))
				&& CanonicalSha256(Row.AuthorityEvidenceHash) && Row.ClosedTick >= 0L
				&& CanonicalSha256(Row.ReceiptEvidenceHash)
				&& string.Equals(Row.ReceiptEvidenceHash, ArchiveReceiptDigest(Row),
					StringComparison.Ordinal);
		}

		private static void NormalizeIncidents(KingdomTradeBook Book)
		{
			if (Book.Incidents == null || Book.Incidents.Count > MaxIncidents)
			{
				QuarantineBook(Book, "incident evidence list is missing or oversized");
				return;
			}
			for (int i = 0; i < Book.Incidents.Count; i++)
			{
				KingdomTradeIncident row = Book.Incidents[i];
				if (!ValidIncidentEvidence(row))
				{
					QuarantineBook(Book, "malformed incident evidence was preserved");
					return;
				}
			}
		}

		private static bool ValidIncidentEvidence(KingdomTradeIncident Row)
		{
			return Row != null && ValidId(Row.RealmId) && Row.Sequence >= 0L
				&& (Row.Sequence == 0L ? string.IsNullOrEmpty(Row.OperationId)
					: string.Equals(Row.OperationId, OperationId(Row.RealmId, Row.Sequence),
						StringComparison.Ordinal))
				&& ValidId(Row.EvidenceHash) && Row.Tick >= 0L
				&& !TooLong(Row.Fault, MaxTextChars);
		}

		private static void NormalizePendingRetirement(KingdomTradeBook Book)
		{
			KingdomTradeProof pending = Book.PendingRetirement;
			if (pending == null) return;
			if (!ValidProof(Book, pending, false)
				|| !string.Equals(pending.RealmId, Book.RealmId, StringComparison.Ordinal))
			{
				QuarantineBook(Book, "partial retirement evidence could not be completed exactly");
				return;
			}
			if (!CompletePendingRetirement(Book))
				QuarantineBook(Book, "partial retirement evidence could not be completed exactly");
		}

		private static bool ValidProof(KingdomTradeBook Book, KingdomTradeProof Proof,
			bool RequireRetired)
		{
			if (Proof == null || Proof.Sequence <= 0L || !ValidId(Proof.RealmId)
				|| !ValidId(Proof.OperationEvidenceHash)
				|| (RequireRetired && Proof.Sequence > Book.RetiredThrough)
				|| !string.Equals(Proof.Id, OperationId(Proof.RealmId, Proof.Sequence), StringComparison.Ordinal)
				|| Proof.Kind == KingdomTradeOperationKind.None
				|| (Proof.Disposition != KingdomTradePhase.Terminal
					&& Proof.Disposition != KingdomTradePhase.Quarantined)
				|| Proof.RequestedWater < 0 || Proof.RequestedWater > MaxOperationWater
				|| Proof.ProvedWater < 0
				|| Proof.ProvedWater > Proof.RequestedWater || Proof.AmbiguousWater < 0
				|| Proof.AmbiguousWater != 0
				|| Proof.MaterialRequested < 0 || Proof.MaterialProved < 0
				|| Proof.MaterialProved != Proof.MaterialRequested
				|| Proof.ManifestEscrowBefore < 0 || Proof.ManifestEscrowDebit < 0
				|| Proof.ManifestEscrowDebit > Proof.ManifestEscrowBefore
				|| Proof.ManifestEscrowAfter != Proof.ManifestEscrowBefore - Proof.ManifestEscrowDebit
				|| Proof.RetainedBefore < 0L || Proof.RetainedDelta < 0L
				|| Proof.RetainedDelta > long.MaxValue - Proof.RetainedBefore
				|| Proof.RetainedAfter != Proof.RetainedBefore + Proof.RetainedDelta
				|| !ValidId(Proof.SettlementId) || Proof.Tick < 0L
				|| (Proof.Kind != KingdomTradeOperationKind.CharterDelivery
					&& !ValidId(Proof.ManifestId))
				|| !SinkClean(Proof.ChronicleState) || !SinkClean(Proof.LedgerState)
				|| !SinkClean(Proof.MessageState) || !SinkClean(Proof.DeedState)
				|| ((Proof.Kind == KingdomTradeOperationKind.CharterDelivery
						|| Proof.Kind == KingdomTradeOperationKind.ManifestLoad)
					&& Proof.ProvedWater != Proof.RequestedWater)
				|| (Proof.Kind == KingdomTradeOperationKind.CharterDelivery
					&& (Proof.ChronicleState != KingdomTradeSinkState.Delivered
						|| Proof.LedgerState != KingdomTradeSinkState.Delivered
						|| Proof.MessageState != KingdomTradeSinkState.Delivered
						|| (Proof.Disposition == KingdomTradePhase.Terminal
							? Proof.DeedState != KingdomTradeSinkState.Delivered
							: Proof.DeedState != KingdomTradeSinkState.Delivered
								&& Proof.DeedState != KingdomTradeSinkState.Skipped)))
				|| (Proof.Kind == KingdomTradeOperationKind.ManifestDelivery
					&& (Proof.ManifestEscrowBefore != Proof.RequestedWater
						|| Proof.ManifestEscrowDebit != Proof.ProvedWater
						|| Proof.ManifestEscrowState != KingdomTradePhysicalState.Proved
						|| Proof.RetainedBefore != 0L || Proof.RetainedDelta != 0L
						|| Proof.RetainedAfter != 0L
						|| Proof.RetainedState != KingdomTradePhysicalState.None))
				|| (Proof.Kind == KingdomTradeOperationKind.ManifestLapse
					&& (Proof.RetainedDelta != Proof.RequestedWater
						|| Proof.RetainedState != KingdomTradePhysicalState.Proved
						|| Proof.ManifestEscrowBefore != 0 || Proof.ManifestEscrowDebit != 0
						|| Proof.ManifestEscrowAfter != 0
						|| Proof.ManifestEscrowState != KingdomTradePhysicalState.None))
				|| ((Proof.Kind == KingdomTradeOperationKind.CharterDelivery
						|| Proof.Kind == KingdomTradeOperationKind.ManifestLoad
						|| Proof.Kind == KingdomTradeOperationKind.ManifestTurnback)
					&& (Proof.ManifestEscrowBefore != 0 || Proof.ManifestEscrowDebit != 0
						|| Proof.ManifestEscrowAfter != 0
						|| Proof.ManifestEscrowState != KingdomTradePhysicalState.None
						|| Proof.RetainedBefore != 0L || Proof.RetainedDelta != 0L
						|| Proof.RetainedAfter != 0L
						|| Proof.RetainedState != KingdomTradePhysicalState.None))
				|| (Proof.ManifestCleanup != (Proof.Kind == KingdomTradeOperationKind.ManifestLapse
					|| (Proof.Kind == KingdomTradeOperationKind.ManifestDelivery
						&& Proof.ManifestEscrowAfter == 0)))
				|| TooLong(Proof.Fault, MaxTextChars)) return false;
			return Enum.IsDefined(typeof(KingdomTradeOperationKind), Proof.Kind)
				&& Enum.IsDefined(typeof(KingdomTradePhase), Proof.Disposition)
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Proof.ManifestEscrowState)
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Proof.RetainedState)
				&& Enum.IsDefined(typeof(KingdomTradeSinkState), Proof.ChronicleState)
				&& Enum.IsDefined(typeof(KingdomTradeSinkState), Proof.LedgerState)
				&& Enum.IsDefined(typeof(KingdomTradeSinkState), Proof.MessageState)
				&& Enum.IsDefined(typeof(KingdomTradeSinkState), Proof.DeedState);
		}

		private static bool SinkClean(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Delivered
				|| State == KingdomTradeSinkState.Skipped;
		}

		private static bool ValidMaterialMarker(string OperationId, string Marker)
		{
			if (!ValidId(OperationId) || !ValidId(Marker)) return false;
			for (int i = 0; i < MaxMaterialOutputs; i++)
				if (string.Equals(Marker, MaterialMarker(OperationId, i), StringComparison.Ordinal)) return true;
			return false;
		}

		public static bool ValidId(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Value.Length <= MaxIdChars;
		}

		public static bool ValidName(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Value.Length <= MaxNameChars;
		}

		private static long PositiveCounter(long Value)
		{
			return Value <= 0L ? 1L : Value;
		}

		private static int Nonnegative(int Value)
		{
			return Value < 0 ? 0 : Value;
		}

		private static long Nonnegative(long Value)
		{
			return Value < 0L ? 0L : Value;
		}

		private static string Bound(string Value, int Maximum)
		{
			if (Value == null) return null;
			return Value.Length <= Maximum ? Value : Value.Substring(0, Maximum);
		}

		private static bool TooLong(string Value, int Maximum)
		{
			return Value != null && Value.Length > Maximum;
		}

		private static string AppendFault(string Existing, string Added)
		{
			if (!string.IsNullOrEmpty(Existing))
			{
				if (Existing.Length > MaxTextChars || string.IsNullOrEmpty(Added)
					|| Added.Length > MaxTextChars - Existing.Length - 2) return Existing;
				return Existing + "; " + Added;
			}
			return Bound(Added, MaxTextChars);
		}
	}
}
