using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomCivicLeadPhase : byte
	{ None = 0, Prepared = 1, Projected = 2, Invalidated = 3, Quarantined = 4 }

	public sealed class KingdomCivicLeadCause
	{
		public string SourceId, SettlementId, Locator, Title, AuthoredReason;
		public int SourceVersion;
		public long CompletedTick;
	}

	[Serializable]
	public sealed class KingdomCivicLeadReceipt
	{
		/// <summary>
		/// This book gained no field in wire revision 2; the second revision exists so that both
		/// books share one frame, one digest, and one future-versus-damage rule. A lead row still
		/// declares its own revision, and a row read from a v1 save keeps revision 1.
		/// </summary>
		public const int FirstVersion = 1;
		public const int CurrentVersion = FirstVersion;
		public int Version = CurrentVersion;
		public KingdomCivicLeadPhase Phase;
		public string SourceId, SettlementId, LeadId, Locator, Title, AuthoredReason;
		public int SourceVersion;
		public long CompletedTick;

		/// <summary>
		/// Always null on a lawful row, and written to the wire as a four-byte absence so that a
		/// later build may fill it without moving anything before it. A conflicted projection is
		/// held at <see cref="KingdomCivicLeadPhase.Prepared"/> for explicit recovery rather than
		/// being demoted here, which is why <see cref="KingdomCivicLeadPhase.Quarantined"/> is
		/// declared and unreachable.
		/// </summary>
		public string Fault;
		public KingdomCivicLeadReceipt Copy() => (KingdomCivicLeadReceipt)MemberwiseClone();
	}

	public sealed class KingdomCivicLeadBook
	{
		public const int MaxRows = 8;
		public long Revision;
		public readonly List<KingdomCivicLeadReceipt> Rows = new List<KingdomCivicLeadReceipt>();

		/// <summary>How this build read the bytes it was given. See
		/// <see cref="KingdomCuriosityBookState"/>; both books answer with the same three words.</summary>
		public KingdomCuriosityBookState State = KingdomCuriosityBookState.Compatible;

		/// <summary>Why the book is not <c>Compatible</c>, in words a founder can act on.</summary>
		public string Fault;

		/// <summary>The bytes exactly as they arrived. Integrity re-verified before they are ever
		/// written back; a caller-set state is evidence of nothing.</summary>
		public byte[] OpaquePayload;

		/// <summary>The wire revision a future book declared, for honest reporting. Zero if none.</summary>
		public int OpaqueVersion;

		/// <summary>Current wire this build could not read. Kept, defended, never overwritten.</summary>
		public bool Quarantined => State == KingdomCuriosityBookState.Quarantined;

		/// <summary>Written by a newer build. Preserved exactly and held read-only.</summary>
		public bool IsOpaqueFuture => State == KingdomCuriosityBookState.FutureOpaque;
	}
}
