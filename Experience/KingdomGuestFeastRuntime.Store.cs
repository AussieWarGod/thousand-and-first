#if !TAF_TESTS
using System;
using System.IO;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>O11's typed section-8 adapter. Every mutation uses C18's selected-section lease;
	/// unrelated civic-memory sections remain byte-for-byte owned by their siblings.</summary>
	internal static partial class KingdomGuestFeastRuntime
	{
		private const int SectionId = KingdomCivicMemoryLimits.SectionGuestFeast;

		internal static bool TryRead(KingdomSystem system, out KingdomGuestFeastBook book,
			out string failure)
		{
			return TryOpen(system, out _, out _, out book, out failure);
		}

		private static bool TryEnsureBound(KingdomSystem system, out string failure)
		{
			failure = null;
			if (!TryOpen(system, out KingdomCivicMemorySystem memory,
				out KingdomCivicMemorySectionLease lease, out KingdomGuestFeastBook book,
				out failure)) return false;
			if (book.IdentityBound) return true;
			if (lease.Present)
			{
				failure = "guest-feast section is present without exact realm identity";
				return false;
			}
			if (!KingdomGuestFeastRules.TryBindEmptyIdentity(book, system.RealmId,
				out failure)) return false;
			if (!TryCommit(memory, lease, book, out failure)
				|| !TryRead(system, out book, out failure)) return false;
			if (book.IdentityBound && string.Equals(book.RealmId, system.RealmId,
				StringComparison.Ordinal)) return true;
			return Fail("guest-feast realm binding was not reproved", out failure);
		}

		private static bool TryPublish(KingdomSystem system, KingdomGuestFeastBook next,
			out string failure)
		{
			failure = null;
			if (!TryOpen(system, out KingdomCivicMemorySystem memory,
				out KingdomCivicMemorySectionLease lease, out KingdomGuestFeastBook current,
				out failure) || !current.IdentityBound
				|| !string.Equals(current.RealmId, system.RealmId, StringComparison.Ordinal))
				return Fail(failure ?? "guest-feast realm binding is unavailable", out failure);
			if (next == null || next.Revision != current.Revision + 1L
				|| !string.Equals(next.RealmId, current.RealmId, StringComparison.Ordinal)
				|| !KingdomGuestFeastRules.TryValidate(next, out failure)) return false;
			return TryCommit(memory, lease, next, out failure);
		}

		private static bool TryOpen(KingdomSystem system,
			out KingdomCivicMemorySystem memory, out KingdomCivicMemorySectionLease lease,
			out KingdomGuestFeastBook book, out string failure)
		{
			memory = null; lease = null; book = null; failure = null;
			if (system == null || !KingdomIdentityRules.IsRealmId(system.RealmId)
				|| The.Game == null || !TryUniqueMemory(out memory, out failure)
				|| !memory.TryReadSection(SectionId, out lease, out failure)) return false;
			try { book = KingdomGuestFeastCodec.DecodeEnvelope(lease.Payload()); }
			catch (Exception error) when (Recoverable(error))
			{
				failure = "guest-feast section decode failed (" + error.Message + ")";
				return false;
			}
			if (!KingdomGuestFeastRules.TryValidate(book, out failure)) return false;
			if (lease.Present && (!book.IdentityBound || !string.Equals(book.RealmId,
				system.RealmId, StringComparison.Ordinal)))
				return Fail("guest-feast section belongs to another or unbound realm", out failure);
			return true;
		}

		private static bool TryUniqueMemory(out KingdomCivicMemorySystem memory,
			out string failure)
		{
			memory = null; failure = null; int count = 0;
			for (int i = 0; i < The.Game.Systems.Count; i++)
			{
				IGameSystem candidate = The.Game.Systems[i];
				if (candidate != null && candidate.GetType() == typeof(KingdomCivicMemorySystem)
					&& !candidate.Removed)
				{ memory = (KingdomCivicMemorySystem)candidate; count++; }
			}
			if (count == 1) return true;
			failure = "guest-feast authority requires exactly one civic-memory system";
			memory = null; return false;
		}

		private static bool TryCommit(KingdomCivicMemorySystem memory,
			KingdomCivicMemorySectionLease lease, KingdomGuestFeastBook book,
			out string failure)
		{
			failure = null; byte[] payload;
			try { payload = KingdomGuestFeastCodec.EncodeEnvelope(book); }
			catch (Exception error) when (Recoverable(error))
			{
				failure = "guest-feast section encode failed (" + error.Message + ")";
				return false;
			}
			return memory.TryCommitSection(lease, payload, out failure);
		}

		private static bool Recoverable(Exception error)
		{
			return error is InvalidDataException || error is EndOfStreamException
				|| error is ArgumentException || error is OverflowException;
		}

		private static bool Fail(string message, out string failure)
		{
			failure = message; return false;
		}
	}
}
#endif
