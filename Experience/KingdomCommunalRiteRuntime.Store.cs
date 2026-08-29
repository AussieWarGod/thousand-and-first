#if !TAF_TESTS
using System;
using System.IO;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>D8's typed section-7 adapter. It never replaces the C18 envelope and therefore
	/// cannot overwrite a sibling family's bytes.</summary>
	internal static partial class KingdomCommunalRiteRuntime
	{
		private const int SectionId = KingdomCivicMemoryLimits.SectionCommunalRite;

		internal static bool TryRead(KingdomSystem system,
			out KingdomCommunalRiteBook book, out string failure)
		{
			return TryOpen(system, out _, out _, out book, out failure);
		}

		internal static bool TryEnsureBound(KingdomSystem system, out string failure)
		{
			failure = null;
			if (!TryOpen(system, out KingdomCivicMemorySystem memory,
				out KingdomCivicMemorySectionLease lease, out KingdomCommunalRiteBook book,
				out failure)) return false;
			if (book.IdentityBound) return true;
			if (lease.Present)
			{
				failure = "communal-rite section is present without exact realm identity";
				return false;
			}
			if (!KingdomCommunalRiteRules.TryBindEmptyIdentity(book, system.RealmId,
				out failure)) return false;
			return TryCommit(memory, lease, book, out failure)
				&& TryReadBound(system, out _, out failure);
		}

		internal static bool TryPublish(KingdomSystem system, KingdomCommunalRiteBook next,
			out string failure)
		{
			failure = null;
			if (!TryOpen(system, out KingdomCivicMemorySystem memory,
				out KingdomCivicMemorySectionLease lease, out KingdomCommunalRiteBook current,
				out failure) || !current.IdentityBound
				|| !string.Equals(current.RealmId, system.RealmId, StringComparison.Ordinal))
			{
				if (failure == null) failure = "communal-rite realm binding is unavailable";
				return false;
			}
			if (next == null || next.Revision != current.Revision + 1L
				|| !string.Equals(next.RealmId, current.RealmId, StringComparison.Ordinal)
				|| !KingdomCommunalRiteRules.TryValidate(next, out failure)) return false;
			return TryCommit(memory, lease, next, out failure);
		}

		private static bool TryReadBound(KingdomSystem system,
			out KingdomCommunalRiteBook book, out string failure)
		{
			if (!TryRead(system, out book, out failure)) return false;
			if (book.IdentityBound && string.Equals(book.RealmId, system.RealmId,
				StringComparison.Ordinal)) return true;
			failure = "communal-rite section did not preserve exact realm identity";
			return false;
		}

		private static bool TryOpen(KingdomSystem system,
			out KingdomCivicMemorySystem memory, out KingdomCivicMemorySectionLease lease,
			out KingdomCommunalRiteBook book, out string failure)
		{
			memory = null; lease = null; book = null; failure = null;
			if (system == null || !KingdomIdentityRules.IsRealmId(system.RealmId)
				|| The.Game == null || !TryUniqueMemory(out memory, out failure)
				|| !memory.TryReadSection(SectionId, out lease, out failure)) return false;
			try { book = KingdomCommunalRiteCodec.DecodeEnvelope(lease.Payload()); }
			catch (Exception error) when (Recoverable(error))
			{
				failure = "communal-rite section decode failed (" + error.Message + ")";
				return false;
			}
			if (!KingdomCommunalRiteRules.TryValidate(book, out failure)) return false;
			if (lease.Present && (!book.IdentityBound || !string.Equals(book.RealmId,
				system.RealmId, StringComparison.Ordinal)))
			{
				failure = "communal-rite section belongs to another or unbound realm";
				return false;
			}
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
			failure = "communal-rite authority requires exactly one civic-memory system";
			memory = null; return false;
		}

		private static bool TryCommit(KingdomCivicMemorySystem memory,
			KingdomCivicMemorySectionLease lease, KingdomCommunalRiteBook book,
			out string failure)
		{
			failure = null; byte[] payload;
			try { payload = KingdomCommunalRiteCodec.EncodeEnvelope(book); }
			catch (Exception error) when (Recoverable(error))
			{
				failure = "communal-rite section encode failed (" + error.Message + ")";
				return false;
			}
			return memory.TryCommitSection(lease, payload, out failure);
		}

		private static bool Recoverable(Exception error)
		{
			return error is InvalidDataException || error is EndOfStreamException
				|| error is ArgumentException || error is OverflowException;
		}
	}
}
#endif
