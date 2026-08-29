using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomCommunalRiteRules
	{
		public const int MaxRows = KingdomExperienceRules.MaxSettlements;
		public const int MaxRealmIdBytes = 77;
		public const int MaxSettlementIdBytes = 82;
		public const int MaxPracticeIdBytes = 100;
		public const int MaxEventIdBytes = 133;
		private const string EventPrefix = "taf:happening:";
		private const string SubjectDomain = "taf:communal-rite-subject:v1:";
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool TryValidate(KingdomCommunalRiteBook book, out string failure)
		{
			failure = null;
			if (book == null || book.SchemaState != KingdomExperienceSchemaState.Compatible
				|| book.SchemaFault != null || book.Revision < 0L || book.Rows == null
				|| book.Rows.Count > MaxRows || book.OpaqueWireVersion != 0
				|| book.OpaqueFuturePayload != null || book.OpaqueEnvelope != null)
				return Fail("communal-rite header is invalid", out failure);
			if (!book.IdentityBound)
			{
				if (book.RealmId != null || book.Revision != 0L || book.Rows.Count != 0)
					return Fail("unbound communal-rite book carries authority", out failure);
			}
			else if (!KingdomIdentityRules.IsRealmId(book.RealmId))
				return Fail("communal-rite realm is invalid", out failure);
			string prior = null;
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomCommunalRiteReceipt row = book.Rows[i];
				if (!Valid(row) || prior != null
					&& string.CompareOrdinal(prior, row.SettlementId) >= 0)
					return Fail("communal-rite row is invalid or unsorted", out failure);
				prior = row.SettlementId;
			}
			return true;
		}

		public static bool TryBindEmptyIdentity(KingdomCommunalRiteBook book,
			string realmId, out string failure)
		{
			failure = null;
			if (!TryValidate(book, out failure) || !KingdomIdentityRules.IsRealmId(realmId))
				return Fail(failure ?? "communal-rite realm is invalid", out failure);
			if (book.IdentityBound) return string.Equals(book.RealmId, realmId,
				StringComparison.Ordinal) || Fail("communal-rite realm mismatch", out failure);
			book.IdentityBound = true; book.RealmId = realmId; book.Revision = 1L;
			return true;
		}

		public static bool TryPrepare(KingdomCommunalRiteBook book, long expectedRevision,
			KingdomFirstFeastReceipt practice, string eventId, long eventTick, long enableEpoch,
			out KingdomCommunalRiteReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!TryValidate(book, out failure) || !book.IdentityBound
				|| !KingdomFirstFeastRules.IsAffirmative(practice) || eventTick <= 0L
				|| eventTick < practice.DecidedTick || enableEpoch <= 0L
				|| !TryPracticeSubject(practice.PracticeId, out int subject)
				|| eventId != EventId(practice.SettlementId, eventTick, subject))
				return Fail(failure ?? "communal-rite preparation evidence is invalid", out failure);
			int index = Index(book, practice.SettlementId);
			if (index >= 0)
			{
				receipt = Copy(book.Rows[index]);
				return SamePlan(receipt, practice.PracticeId, eventId, eventTick, enableEpoch)
					|| Fail("settlement already names another communal rite", out failure);
			}
			if (expectedRevision != book.Revision || book.Rows.Count >= MaxRows
				|| book.Revision == long.MaxValue)
				return Fail("communal-rite preparation revision or capacity refused", out failure);
			KingdomCommunalRiteReceipt row = new KingdomCommunalRiteReceipt
			{
				Phase = KingdomCommunalRitePhase.Prepared,
				SettlementId = practice.SettlementId, PracticeId = practice.PracticeId,
				EventId = eventId, EventTick = eventTick, EnableEpoch = enableEpoch
			};
			KingdomCommunalRiteBook next = Clone(book); next.Rows.Add(row);
			next.Rows.Sort((a, b) => string.CompareOrdinal(
				a.SettlementId, b.SettlementId)); next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); receipt = Copy(row); return true;
		}

		public static bool TryCommit(KingdomCommunalRiteBook book, long expectedRevision,
			string practiceId, string eventId, out KingdomCommunalRiteReceipt receipt,
			out string failure)
		{
			receipt = null; failure = null;
			if (!TryValidate(book, out failure)) return false;
			int index = PracticeIndex(book, practiceId);
			if (index < 0 || book.Rows[index].EventId != eventId)
				return Fail("exact prepared communal rite is absent", out failure);
			KingdomCommunalRiteReceipt row = book.Rows[index];
			if (row.Phase != KingdomCommunalRitePhase.Prepared)
			{
				receipt = Copy(row);
				return true;
			}
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("communal-rite commit CAS refused", out failure);
			KingdomCommunalRiteBook next = Clone(book);
			next.Rows[index].Phase = KingdomCommunalRitePhase.Committed;
			next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); receipt = Copy(next.Rows[index]); return true;
		}

		public static bool TryFinish(KingdomCommunalRiteBook book, long expectedRevision,
			string practiceId, string eventId, bool attended, long projectionTick,
			out KingdomCommunalRiteReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!TryValidate(book, out failure)) return false;
			int index = PracticeIndex(book, practiceId);
			if (index < 0 || book.Rows[index].EventId != eventId)
				return Fail("exact communal rite is absent", out failure);
			KingdomCommunalRiteReceipt row = book.Rows[index];
			KingdomCommunalRitePhase phase = attended ? KingdomCommunalRitePhase.Attended
				: KingdomCommunalRitePhase.Suppressed;
			if (row.Phase == phase) { receipt = Copy(row); return true; }
			bool lawfulSource = attended
				? row.Phase == KingdomCommunalRitePhase.Committed
				: row.Phase == KingdomCommunalRitePhase.Prepared
					|| row.Phase == KingdomCommunalRitePhase.Committed;
			if (!lawfulSource
				|| expectedRevision != book.Revision || projectionTick < row.EventTick
				|| book.Revision == long.MaxValue)
				return Fail("communal-rite terminal CAS refused", out failure);
			KingdomCommunalRiteBook next = Clone(book); row = next.Rows[index];
			row.Phase = phase; row.ProjectionTick = projectionTick; next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); receipt = Copy(row); return true;
		}

		public static bool TryFind(KingdomCommunalRiteBook book, string settlementId,
			out KingdomCommunalRiteReceipt receipt)
		{
			receipt = null;
			if (!TryValidate(book, out string _)) return false;
			int index = Index(book, settlementId);
			if (index < 0) return true;
			receipt = Copy(book.Rows[index]); return true;
		}

		public static bool TryPracticeSubject(string practiceId, out int subject)
		{
			subject = 0;
			if (!KernelSemanticId.IsValid(practiceId)
				|| !practiceId.StartsWith(KingdomFirstFeastRules.PracticePrefix,
					StringComparison.Ordinal)
				|| practiceId.Length != KingdomFirstFeastRules.PracticePrefix.Length + 64
				|| !LowerHex(practiceId.Substring(
					KingdomFirstFeastRules.PracticePrefix.Length))) return false;
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(StrictUtf8.GetBytes(SubjectDomain + practiceId));
				subject = (hash[0] | hash[1] << 8 | hash[2] << 16 | hash[3] << 24)
					& int.MaxValue;
			}
			if (subject == 0) subject = 1;
			return true;
		}

		public static KingdomCommunalRiteOptionDisposition OptionDisposition(
			bool observationReadable, bool enabled, long observedEpoch, long frozenEpoch)
		{
			if (!observationReadable || frozenEpoch <= 0L)
				return KingdomCommunalRiteOptionDisposition.Unreadable;
			if (!enabled) return KingdomCommunalRiteOptionDisposition.Disabled;
			if (observedEpoch <= 0L)
				return KingdomCommunalRiteOptionDisposition.Unreadable;
			return observedEpoch == frozenEpoch
				? KingdomCommunalRiteOptionDisposition.Current
				: KingdomCommunalRiteOptionDisposition.SupersededEpoch;
		}

		private static bool LowerHex(string value)
		{
			if (value == null || value.Length != 64) return false;
			for (int i = 0; i < value.Length; i++)
				if (!((value[i] >= '0' && value[i] <= '9')
					|| value[i] >= 'a' && value[i] <= 'f')) return false;
			return true;
		}

		public static string EventId(string settlementId, long eventTick, int subject)
		{
			return EventPrefix + (settlementId ?? "") + ":5:"
				+ eventTick.ToString(CultureInfo.InvariantCulture) + ":"
				+ subject.ToString(CultureInfo.InvariantCulture) + ":0:0";
		}

		private static bool Valid(KingdomCommunalRiteReceipt row)
		{
			if (row == null || row.Version != KingdomCommunalRiteReceipt.CurrentVersion
				|| row.Phase != KingdomCommunalRitePhase.Prepared
					&& row.Phase != KingdomCommunalRitePhase.Committed
					&& row.Phase != KingdomCommunalRitePhase.Attended
					&& row.Phase != KingdomCommunalRitePhase.Suppressed
				|| !KingdomIdentityRules.IsSettlementId(row.SettlementId)
				|| !TryPracticeSubject(row.PracticeId, out int subject)
				|| row.EventTick <= 0L || row.EnableEpoch <= 0L
				|| row.EventId != EventId(row.SettlementId, row.EventTick, subject)
				|| !Text(row.EventId, MaxEventIdBytes)) return false;
			return row.Phase == KingdomCommunalRitePhase.Prepared
				|| row.Phase == KingdomCommunalRitePhase.Committed
				? row.ProjectionTick == 0L : row.ProjectionTick >= row.EventTick;
		}

		private static bool SamePlan(KingdomCommunalRiteReceipt row, string practiceId,
			string eventId, long eventTick, long epoch)
		{
			return row.PracticeId == practiceId && row.EventId == eventId
				&& row.EventTick == eventTick && row.EnableEpoch == epoch;
		}

		private static int Index(KingdomCommunalRiteBook book, string settlementId)
		{
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].SettlementId == settlementId) return i;
			return -1;
		}

		private static int PracticeIndex(KingdomCommunalRiteBook book, string practiceId)
		{
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].PracticeId == practiceId) return i;
			return -1;
		}

		internal static KingdomCommunalRiteReceipt Copy(KingdomCommunalRiteReceipt row)
		{
			return new KingdomCommunalRiteReceipt { Version = row.Version, Phase = row.Phase,
				SettlementId = row.SettlementId, PracticeId = row.PracticeId,
				EventId = row.EventId, EventTick = row.EventTick,
				EnableEpoch = row.EnableEpoch, ProjectionTick = row.ProjectionTick };
		}

		internal static KingdomCommunalRiteBook Clone(KingdomCommunalRiteBook book)
		{
			KingdomCommunalRiteBook clone = new KingdomCommunalRiteBook
			{
				SchemaState = book.SchemaState, SchemaFault = book.SchemaFault,
				RealmId = book.RealmId, IdentityBound = book.IdentityBound,
				Revision = book.Revision, OpaqueWireVersion = book.OpaqueWireVersion,
				OpaqueFuturePayload = book.OpaqueFuturePayload == null ? null
					: (byte[])book.OpaqueFuturePayload.Clone(),
				OpaqueEnvelope = book.OpaqueEnvelope == null ? null
					: (byte[])book.OpaqueEnvelope.Clone()
			};
			for (int i = 0; i < book.Rows.Count; i++) clone.Rows.Add(Copy(book.Rows[i]));
			return clone;
		}

		internal static void Replace(KingdomCommunalRiteBook target,
			KingdomCommunalRiteBook source)
		{
			target.SchemaState = source.SchemaState; target.SchemaFault = source.SchemaFault;
			target.RealmId = source.RealmId; target.IdentityBound = source.IdentityBound;
			target.Revision = source.Revision; target.Rows = source.Rows;
			target.OpaqueWireVersion = source.OpaqueWireVersion;
			target.OpaqueFuturePayload = source.OpaqueFuturePayload;
			target.OpaqueEnvelope = source.OpaqueEnvelope;
		}

		internal static bool Text(string value, int maxBytes)
		{
			try { return value != null && value.Length > 0
				&& StrictUtf8.GetByteCount(value) <= maxBytes; }
			catch (EncoderFallbackException) { return false; }
		}

		private static bool Fail(string message, out string failure)
		{
			failure = message; return false;
		}
	}
}
