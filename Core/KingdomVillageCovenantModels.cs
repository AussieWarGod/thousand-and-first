using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>What this build was able to make of the covenant archive it was handed.</summary>
	public enum KingdomVillageCovenantState : byte
	{
		/// <summary>Written at a revision this build reads, and every row satisfied its rules.</summary>
		Compatible = 0,

		/// <summary>Whole, and written by a later build. Held byte-for-byte and never edited.</summary>
		FutureOpaque = 1,

		/// <summary>Refused. The real bytes are kept as the evidence of what went wrong.</summary>
		Quarantined = 2
	}

	/// <summary>
	/// One completed village covenant, frozen at the moment the rite finished.
	/// <para>
	/// Every field here is a copy of something that was true when the water was poured, and none of
	/// them is a window onto anything that is true now. That is the whole point of the row. A
	/// covenant is a thing that happened; standing, a faction's properties, a reservation and the
	/// ownership of a site are all things that are, and every one of them can move afterwards for
	/// reasons that have nothing to do with the covenant. A build that read today's standing and
	/// concluded a covenant had once been sealed would be inventing history out of weather.
	/// </para>
	/// <para>
	/// So the row is evidence and never authority. Nothing in this family grants a standing, moves
	/// a faction, or decides that a village belongs to anybody. It records that on one tick, under
	/// one founding transaction, a named village agreed &mdash; and it records enough of that
	/// moment that the claim can be checked rather than believed.
	/// </para>
	/// </summary>
	[Serializable]
	public sealed class KingdomVillageCovenantReceipt
	{
		/// <summary>The only row revision this build writes. A stranger's is not migrated.</summary>
		public const int CurrentVersion = 1;

		/// <summary>The row's own wire revision, carried so a later shape is recognisable.</summary>
		public int Version = CurrentVersion;

		/// <summary>
		/// The realm this covenant belongs to, frozen in the row itself.
		/// <para>
		/// The archive around it is bound to a realm too, and this is not that binding repeated for
		/// tidiness. A row carries its realm because the row is the evidence: it is digested under
		/// it, compared by it, and refused if it disagrees with the archive holding it. A row that
		/// took its realm from whatever archive it happened to be sitting in would be a covenant
		/// that changed hands by being moved.
		/// </para>
		/// </summary>
		public string RealmId = "";

		/// <summary>The derived, stable name of this exact covenant. See
		/// <see cref="KingdomVillageCovenantRules.ReceiptId"/> for what it is derived from.</summary>
		public string ReceiptId = "";

		/// <summary>The founding transaction that sealed it. Thirty-two lower-case hex digits.</summary>
		public string TransactionId = "";

		/// <summary>The exact encoded founding authority, byte for byte as the rite carried it.</summary>
		public string FoundingAuthority = "";

		/// <summary>The village's own faction key, never its display name.</summary>
		public string VillageFactionId = "";

		/// <summary>What that faction was called on the day. A snapshot, not a lookup.</summary>
		public string VillageDisplayName = "";

		/// <summary>The canonical locator of the ground the rite was performed on.</summary>
		public string SiteZoneId = "";

		/// <summary>The chronicle event the covenant was published under.</summary>
		public string ChronicleEventId = "";

		/// <summary>The standing the covenant was sealed at, as it stood then.</summary>
		public int SealedStanding;

		/// <summary>The tick the site reservation was taken at.</summary>
		public long ReservationTick;

		public KingdomVillageCovenantReceipt Copy()
		{
			return new KingdomVillageCovenantReceipt
			{
				Version = Version,
				RealmId = RealmId,
				ReceiptId = ReceiptId,
				TransactionId = TransactionId,
				FoundingAuthority = FoundingAuthority,
				VillageFactionId = VillageFactionId,
				VillageDisplayName = VillageDisplayName,
				SiteZoneId = SiteZoneId,
				ChronicleEventId = ChronicleEventId,
				SealedStanding = SealedStanding,
				ReservationTick = ReservationTick
			};
		}
	}

	/// <summary>
	/// The realm's bounded archive of completed covenants.
	/// <para>
	/// It is bound to one realm and holds nothing for any other. The binding is taken once, on an
	/// empty archive, and never afterwards: an archive that already carries rows cannot be adopted
	/// by a realm it was not written for, because there would be no way to tell an inherited save
	/// from a stolen one.
	/// </para>
	/// <para>
	/// Rows only ever arrive. There is no method here that removes one, and the capacity is a
	/// refusal rather than an eviction: at <see cref="MaxRows"/> the next covenant is declined and
	/// every covenant already recorded survives untouched. An archive that forgot its oldest
	/// evidence to make room for its newest would be worse than one that filled up, because the
	/// second failure announces itself and the first does not.
	/// </para>
	/// </summary>
	[Serializable]
	public sealed class KingdomVillageCovenantArchive
	{
		/// <summary>
		/// Forty-eight covenants, and the reason is arithmetic rather than appetite. The section
		/// this archive lives in is charged against the civic-memory envelope's shared byte budget,
		/// so its ceiling has to be a number that budget can absorb; forty-eight rows at the four
		/// kilobytes a row is capped at comes to 196,946 bytes of envelope, which sits below the
		/// 241,384 an unknown section at this id was already allowed. Making this section known can
		/// therefore only tighten what a payload here may be, never loosen it.
		/// </summary>
		public const int MaxRows = 48;

		/// <summary>The realm this archive belongs to. Null until it is bound.</summary>
		public string RealmId;

		/// <summary>Whether the binding has been taken. An unbound archive must be empty.</summary>
		public bool IdentityBound;

		/// <summary>Advances once per accepted append, so a stale writer cannot look current.</summary>
		public long Revision;

		public readonly List<KingdomVillageCovenantReceipt> Rows =
			new List<KingdomVillageCovenantReceipt>();

		public KingdomVillageCovenantState State = KingdomVillageCovenantState.Compatible;

		/// <summary>The wire revision a future archive declared. Zero while this build reads it.</summary>
		public int OpaqueVersion;

		/// <summary>The exact bytes of a future or refused archive, kept whole.</summary>
		public byte[] OpaquePayload;

		/// <summary>Why this archive is not compatible, in the founder's words.</summary>
		public string Fault = "";

		/// <summary>Whether anything at all has been recorded here.</summary>
		public bool IsEmpty
		{
			get { return Rows.Count == 0 && Revision == 0L; }
		}

		public KingdomVillageCovenantArchive Copy()
		{
			KingdomVillageCovenantArchive copy = new KingdomVillageCovenantArchive
			{
				RealmId = RealmId,
				IdentityBound = IdentityBound,
				Revision = Revision,
				State = State,
				OpaqueVersion = OpaqueVersion,
				OpaquePayload = OpaquePayload == null ? null : (byte[])OpaquePayload.Clone(),
				Fault = Fault
			};
			for (int i = 0; i < Rows.Count; i++) copy.Rows.Add(Rows[i].Copy());
			return copy;
		}
	}
}
