#if !TAF_TESTS
using System;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>
	/// The living game's only door onto the covenant archive.
	/// <para>
	/// Three things pass through it and nothing else does. Before a rite spends the founder's
	/// water, <see cref="TryPreflight"/> asks whether this exact covenant could be recorded at all.
	/// After the covenant's standing is set and its chronicle entry is terminal,
	/// <see cref="TryRecord"/> writes it down under one civic-memory lease and then reads the save
	/// back to prove it is there. And when the rite's completion is being observed,
	/// <see cref="TryArchived"/> hands back the row's own frozen facts, so that clearing a
	/// reservation can never be the thing that erases the only record of what happened.
	/// </para>
	/// <para>
	/// The order those three sit in is the whole design. A covenant that is sealed and then cannot
	/// be written down is a covenant nobody can prove; a covenant that is written down and then
	/// fails to seal is a claim about a rite that never finished. So the archive is asked before
	/// anything is spent, written after the standing and the chronicle are both terminal, and
	/// required before the receipt that paid for all of it may be cleared away.
	/// </para>
	/// </summary>
	public static partial class KingdomVillageCovenantRuntime
	{
		/// <summary>
		/// Whether this exact covenant could be recorded, asked before a dram is measured.
		/// <para>
		/// This reads and never writes. It is called from the founding transaction once the rite's
		/// identities exist and before anything is staged, because a covenant that is sealed and
		/// then cannot be written down is a covenant nobody could later prove &mdash; and the
		/// cheapest moment to discover that is while the founder still has their water. The
		/// candidate row is built and proved to encode here, so a name that is lawful to charter
		/// and one byte too wide to archive is refused before it costs anything.
		/// </para>
		/// </summary>
		public static bool TryPreflight(KingdomSystem System, string TransactionId,
			string FoundingAuthority, string VillageFactionId, string VillageDisplayName,
			string SiteZoneId, int SealedStanding, out string Failure)
		{
			if (TryOpenAuthority(System, out IKingdomCivicMemoryAuthority authority,
					out string realmId, out Failure)
				&& AuthorityIsThisRealms(System, FoundingAuthority, out Failure)
				&& KingdomVillageCovenantLease.TryPreflight(authority, realmId,
					Candidate(realmId, TransactionId, FoundingAuthority, VillageFactionId,
						VillageDisplayName, SiteZoneId, SealedStanding, 0L), out Failure))
				return true;
			Failure = "The realm cannot record this village covenant: " + Failure;
			return false;
		}

		/// <summary>
		/// Records one completed covenant durably, then asks the save whether it is really there.
		/// <para>
		/// Section nine is opened exactly once, by <c>TryReadArchive</c>, and that very lease is
		/// what the append is committed under: everything decided while the archive was being read
		/// stays true of the archive being written. The confirmation afterwards deliberately opens
		/// the section again, because its job is to ask the save rather than to re-read what was
		/// offered to it &mdash; and it confirms the covenant the archive now holds rather than the
		/// one this call built, so a retry after a moved standing finishes instead of stranding.
		/// </para>
		/// </summary>
		public static bool TryRecord(KingdomSystem System, string TransactionId,
			string FoundingAuthority, string VillageFactionId, string VillageDisplayName,
			string SiteZoneId, string ChronicleEventId, int SealedStanding, long ReservationTick,
			out string Failure)
		{
			if (!TryOpenAuthority(System, out IKingdomCivicMemoryAuthority authority,
					out string realmId, out Failure)) return false;
			KingdomVillageCovenantReceipt receipt = Candidate(realmId, TransactionId,
				FoundingAuthority, VillageFactionId, VillageDisplayName, SiteZoneId,
				SealedStanding, ReservationTick);
			if (!string.Equals(receipt.ChronicleEventId, ChronicleEventId,
				StringComparison.Ordinal))
				return KingdomVillageCovenantRules.Fail("this rite's chronicle event id is not the "
					+ "one a village covenant carries", out Failure);
			return KingdomVillageCovenantRuntimeCut.TryRecord(authority, realmId,
				System.KingdomFactionName, receipt, out _, out _, out Failure);
		}

		/// <summary>
		/// The covenant this founding transaction sealed, as the save froze it.
		/// <para>
		/// The row is matched on the facts that cannot move: the realm, the transaction, the
		/// encoded authority, the village, its display name on the day, the ground and the
		/// chronicle event. What comes back are the two that were read from a moving world and then
		/// frozen &mdash; the standing the covenant was sealed at, and the tick its reservation was
		/// taken at. A caller that wanted those from today's ledger instead would be asking the
		/// present to vouch for the past, which is the one thing this archive exists to stop.
		/// </para>
		/// </summary>
		public static bool TryArchived(KingdomSystem System, string TransactionId,
			string FoundingAuthority, string VillageFactionId, string VillageDisplayName,
			string SiteZoneId, string ChronicleEventId, out int SealedStanding,
			out long ReservationTick)
		{
			SealedStanding = 0;
			ReservationTick = -1L;
			if (!TryOpenAuthority(System, out IKingdomCivicMemoryAuthority authority,
				out string realmId, out string failure)) return false;
			if (!KingdomVillageCovenantLease.TryReadArchive(authority, realmId, out _,
				out KingdomVillageCovenantArchive archive, out failure)) return false;
			if (!archive.IdentityBound
				|| !string.Equals(archive.RealmId, realmId, StringComparison.Ordinal)) return false;
			for (int i = 0; i < archive.Rows.Count; i++)
			{
				KingdomVillageCovenantReceipt row = archive.Rows[i];
				if (!string.Equals(row.TransactionId, TransactionId, StringComparison.Ordinal))
					continue;
				if (!string.Equals(row.FoundingAuthority, FoundingAuthority, StringComparison.Ordinal)
					|| !string.Equals(row.VillageFactionId, VillageFactionId,
						StringComparison.Ordinal)
					|| !string.Equals(row.VillageDisplayName, VillageDisplayName,
						StringComparison.Ordinal)
					|| !string.Equals(row.SiteZoneId, SiteZoneId, StringComparison.Ordinal)
					|| !string.Equals(row.ChronicleEventId, ChronicleEventId,
						StringComparison.Ordinal)) return false;
				SealedStanding = row.SealedStanding;
				ReservationTick = row.ReservationTick;
				return true;
			}
			return false;
		}

		/// <summary>The realm the living game is standing as, put to the covenant's own rules.
		/// See <see cref="KingdomVillageCovenantRules.AuthorityBelongsToRealm"/> for why the row
		/// itself must not make this comparison.</summary>
		private static bool AuthorityIsThisRealms(KingdomSystem System, string FoundingAuthority,
			out string Failure)
		{
			return KingdomVillageCovenantRules.AuthorityBelongsToRealm(FoundingAuthority,
				System.KingdomFactionName, out Failure);
		}

		private static KingdomVillageCovenantReceipt Candidate(string realmId,
			string transactionId, string foundingAuthority, string villageFactionId,
			string villageDisplayName, string siteZoneId, int sealedStanding, long reservationTick)
		{
			return KingdomVillageCovenantRules.Receipt(realmId, transactionId, foundingAuthority,
				villageFactionId, villageDisplayName, siteZoneId,
				KingdomVillageCovenantRules.ChronicleEvent(transactionId), sealedStanding,
				reservationTick);
		}

		/// <summary>
		/// The one civic-memory system this save is allowed to have, and the realm it answers for.
		/// <para>
		/// Two systems would mean two archives, and no honest way to say which one holds the
		/// realm's covenants. Counting them is cheaper than discovering that later.
		/// </para>
		/// </summary>
		internal static bool TryOpenAuthority(KingdomSystem System,
			out IKingdomCivicMemoryAuthority Authority, out string RealmId, out string Failure)
		{
			Authority = null;
			RealmId = null;
			Failure = "";
			if (System == null || !System.Founded || The.Game == null)
				return KingdomVillageCovenantRules.Fail("no founded realm is standing to record a "
					+ "covenant for", out Failure);
			if (!System.TryGetCurrentIdentity(out string realmId, out _)
				|| !KingdomIdentityRules.IsRealmId(realmId))
				return KingdomVillageCovenantRules.Fail("the current realm identity is unavailable",
					out Failure);
			int count = 0;
			KingdomCivicMemorySystem memory = null;
			for (int i = 0; i < The.Game.Systems.Count; i++)
			{
				IGameSystem candidate = The.Game.Systems[i];
				if (candidate != null && candidate.GetType() == typeof(KingdomCivicMemorySystem)
					&& !candidate.Removed)
				{
					memory = (KingdomCivicMemorySystem)candidate;
					count++;
				}
			}
			if (count != 1)
				return KingdomVillageCovenantRules.Fail("the covenant archive requires exactly one "
					+ "civic-memory system and this save has " + count, out Failure);
			Authority = memory;
			RealmId = realmId;
			return true;
		}
	}
}
#endif
