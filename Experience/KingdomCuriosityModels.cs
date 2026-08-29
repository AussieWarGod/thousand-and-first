using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomCuriosityState : byte
	{
		None = 0, Available = 1, Viewed = 2, Declined = 3, Invalidated = 4
	}

	/// <summary>
	/// What this build was able to make of a book it was handed.
	/// <para>
	/// Three answers, and the distinction between the last two is the whole point. A book written
	/// by a later build is not damaged; it is merely beyond us, and the only honest thing to do
	/// with it is to carry it unchanged and refuse to write over it. A book written by <i>this</i>
	/// build that no longer reads is damage, and deserves to be named as such. Collapsing the two
	/// into one flag means either calling a stranger corrupt or trusting a corpse.
	/// </para>
	/// </summary>
	public enum KingdomCuriosityBookState : byte
	{
		/// <summary>Read, understood, and lawful to change.</summary>
		Compatible = 0,

		/// <summary>Intact, later than us, held byte-for-byte and read-only.</summary>
		FutureOpaque = 1,

		/// <summary>Current wire that would not read. The real bytes are kept as evidence.</summary>
		Quarantined = 2
	}

	[Serializable]
	public sealed class KingdomCuriosityReceipt
	{
		/// <summary>
		/// Wire revision 1 knew no category; revision 2 records the exact one it matched.
		/// A row read from a v1 save keeps <see cref="Version"/> 1 and a null
		/// <see cref="NoteCategory"/> forever, because this build has no lawful way to learn
		/// which category a founder's curator once required.
		/// </summary>
		public const int FirstVersion = 1;
		public const int CategoryVersion = 2;
		public const int CurrentVersion = CategoryVersion;

		public int Version = CurrentVersion;
		public KingdomCuriosityState State;
		public string SourceId;
		public int SourceVersion;
		public string SettlementId;
		public int CuratorResidentId;
		public string CuratorName;
		public string CuratorObjectId;
		public string NoteId;
		public string Locator;
		public string NoteText;
		public string Reason;

		/// <summary>
		/// The exact journal category the curated note carried, which preparation proved equal to
		/// the cause's required category. Null only on a row migrated from wire revision 1.
		/// </summary>
		public string NoteCategory;

		public long PreparedTick;
		public long ClosedTick = -1L;
		public KingdomCuriosityReceipt Copy() => (KingdomCuriosityReceipt)MemberwiseClone();
	}

	public sealed class KingdomCuriosityBook
	{
		public const int MaxRows = 3;
		public long Revision;
		public readonly List<KingdomCuriosityReceipt> Rows =
			new List<KingdomCuriosityReceipt>();

		/// <summary>How this build read the bytes it was given. See the enum.</summary>
		public KingdomCuriosityBookState State = KingdomCuriosityBookState.Compatible;

		/// <summary>Why the book is not <c>Compatible</c>, in words a founder can act on.</summary>
		public string Fault;

		/// <summary>
		/// The bytes exactly as they arrived, kept for both non-compatible states.
		/// <para>
		/// Nothing here is trusted. Encoding re-verifies these bytes from scratch before it
		/// will emit them, so a caller that sets <see cref="State"/> by hand cannot smuggle a
		/// payload past the writer by calling it a future.
		/// </para>
		/// </summary>
		public byte[] OpaquePayload;

		/// <summary>The wire revision a future book declared, for honest reporting. Zero if none.</summary>
		public int OpaqueVersion;

		/// <summary>Current wire this build could not read. Kept, defended, never overwritten.</summary>
		public bool Quarantined => State == KingdomCuriosityBookState.Quarantined;

		/// <summary>Written by a newer build. Preserved exactly and held read-only.</summary>
		public bool IsOpaqueFuture => State == KingdomCuriosityBookState.FutureOpaque;
	}

	public readonly struct KingdomCuriosityNote
	{
		public readonly string Id, Locator, Text, Category;
		public readonly bool Revealed;
		public KingdomCuriosityNote(string id, string locator, string text, string category,
			bool revealed)
		{ Id = id; Locator = locator; Text = text; Category = category; Revealed = revealed; }
	}

	public sealed class KingdomCuriosityCause
	{
		public string SourceId, SettlementId, Reason, RequiredCategory;
		public int SourceVersion, CuratorResidentId;
		public string CuratorName, CuratorObjectId;
		public long CompletedTick;
	}

	/// <summary>Read-only source proof for idempotent Curator audience cleanup. This is derived
	/// from terminal authority; it is not another persisted lease or lifecycle owner.</summary>
	public readonly struct KingdomCuratorAttentionRelease
	{
		public readonly string ReservationId, SourceId, SettlementId;
		public readonly long EarliestCauseTick;

		public KingdomCuratorAttentionRelease(string reservationId, string sourceId,
			string settlementId, long earliestCauseTick)
		{
			ReservationId = reservationId; SourceId = sourceId;
			SettlementId = settlementId; EarliestCauseTick = earliestCauseTick;
		}
	}
}
