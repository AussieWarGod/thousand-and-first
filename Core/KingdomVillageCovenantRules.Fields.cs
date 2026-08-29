using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The parts of a covenant row that are judged against something outside the row: the founding
	/// authority it was sealed under, and the names a founder will actually be shown.
	/// </summary>
	public static partial class KingdomVillageCovenantRules
	{
		/// <summary>
		/// The encoded authority must be the exact one the rite carried, and it must name this
		/// rite in every part.
		/// <para>
		/// Parsing it back and requiring the re-encoding to equal the stored string is what makes
		/// "exact" mean exact; the founding codec already does that inside <c>TryParseAuthority</c>,
		/// so a re-spelt authority cannot pass here either. What is checked on top of that is
		/// ownership and identity: only the basin path can seal a covenant, only a village charter
		/// is a covenant at all, and the transaction and the ground must be this row's own.
		/// </para>
		/// <para>
		/// The realm inside the authority is deliberately <b>not</b> compared with the row's realm
		/// id, and the reason is a migration. An authority freezes the engine faction key the realm
		/// was standing under; a realm migrated to immutable identity mints a fresh canonical realm
		/// id while keeping the faction key it already had. Requiring the two to be equal would make
		/// every covenant impossible on exactly the saves that have been carried furthest. They are
		/// two true names for one realm, both frozen in the receipt's digest, and the one that has
		/// to agree with the living game is checked where the living game is &mdash; see
		/// <c>KingdomVillageCovenantRuntime</c>, which requires the authority's faction key to be
		/// the realm's own before a covenant is preflighted or recorded.
		/// </para>
		/// </summary>
		private static bool ValidAuthority(KingdomVillageCovenantReceipt row, out string failure)
		{
			if (!Bounded(row.FoundingAuthority, MaxAuthorityBytes)
				|| !KingdomFoundingTransactionRules.TryParseAuthority(row.FoundingAuthority,
					out KingdomFoundingAuthority parsed))
				return Fail("the covenant's founding authority does not decode exactly", out failure);
			if (parsed.Kind != KingdomFoundingKind.VillageCharter)
				return Fail("the covenant's founding authority is not a village charter's",
					out failure);
			if (parsed.OwnerKind != KingdomFoundingOwnerKind.Basin)
				return Fail("the covenant's founding authority was not owned by the basin that "
					+ "poured; no other owner path can seal a covenant", out failure);
			if (!string.Equals(parsed.TransactionID, row.TransactionId, StringComparison.Ordinal))
				return Fail("the covenant's founding authority names another transaction",
					out failure);
			if (!string.Equals(parsed.ZoneID, row.SiteZoneId, StringComparison.Ordinal))
				return Fail("the covenant's founding authority names another site", out failure);
			failure = "";
			return true;
		}

		/// <summary>
		/// The exact chronicle event a completed village covenant carries, assembled the way
		/// <c>KingdomFoundingTransaction.FoundingEventID</c> assembles it
		/// (<c>Core/KingdomFoundingTransaction.21EngineProjection.cs:95-113</c>) and transcribed
		/// rather than called, because that method lives on the engine side of the mod and this
		/// file is compiled into projects with no game to link against.
		/// </summary>
		public static string ChronicleEvent(string transactionId)
		{
			return ChronicleEventPrefix
				+ ((int)KingdomFoundingKind.VillageCharter).ToString(CultureInfo.InvariantCulture)
				+ ":" + transactionId + ":" + ChronicleLane;
		}

		/// <summary>
		/// A name a founder could be shown and a machine could compare.
		/// <para>
		/// Control characters, format characters and unpaired surrogates are all refused, and for
		/// one reason between them: each lets two names that read identically compare unequal, or
		/// two that compare equal read differently. A zero-width joiner inside a faction key is not
		/// a typo a founder can see.
		/// </para>
		/// </summary>
		private static bool Named(string value, int maximum)
		{
			// Both bounds, and in this order: the character bound is the founding contract's and
			// the byte bound is the wire's, and a name that satisfies one and not the other is a
			// name that would be lawful in one half of this rite and unlawful in the other.
			if (!Bounded(value, maximum) || value.Length > MaxNameChars) return false;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (char.IsControl(c)
					|| char.GetUnicodeCategory(c) == UnicodeCategory.Format) return false;
			}
			return true;
		}

		/// <summary>Present, strictly encodable, and inside its cap in bytes rather than chars.</summary>
		internal static bool Bounded(string value, int maximum)
		{
			if (string.IsNullOrEmpty(value) || value.IndexOf('\0') >= 0) return false;
			if (!KingdomCuriosityRules.Utf8Encodable(value)) return false;
			try { return new UTF8Encoding(false, true).GetByteCount(value) <= maximum; }
			catch (EncoderFallbackException) { return false; }
		}

		internal static bool Fail(string text, out string failure)
		{
			failure = text;
			return false;
		}

		/// <summary>
		/// Whether an encoded authority was minted under the realm the game is standing as.
		/// <para>
		/// A realm has two true names, and this is where they are told apart. The row freezes the
		/// immutable realm id; the authority freezes the engine faction key the realm was
		/// registered under, and on a save carried through the immutable-identity migration those
		/// are different strings for one realm. So the row's own rules never compare them &mdash;
		/// that would make a covenant impossible on precisely the oldest saves &mdash; and the
		/// question is asked here instead, against the faction key the living game supplies.
		/// </para>
		/// <para>
		/// It lives on the pure side deliberately. The engine hands in the key and nothing else, so
		/// the judgement that a migrated realm can still seal a covenant is one a test can make
		/// without a game running.
		/// </para>
		/// </summary>
		public static bool AuthorityBelongsToRealm(string foundingAuthority,
			string realmFactionKey, out string failure)
		{
			if (!KingdomFoundingTransactionRules.TryParseAuthority(foundingAuthority,
				out KingdomFoundingAuthority parsed))
				return Fail("this rite's founding authority does not decode exactly", out failure);
			if (string.IsNullOrEmpty(realmFactionKey)
				|| !string.Equals(parsed.RealmFaction, realmFactionKey, StringComparison.Ordinal))
				return Fail("this rite's founding authority was minted under another realm than "
					+ "the one standing", out failure);
			failure = "";
			return true;
		}

		/// <summary>
		/// The facts about a covenant that no later moment can change: which realm it belongs to,
		/// which rite sealed it, under what authority, with whom, on what ground, and under which
		/// chronicle entry.
		/// <para>
		/// The standing and the reservation tick are deliberately absent. They are frozen in the
		/// row exactly as everything else is &mdash; nothing overwrites them, ever &mdash; but they
		/// are read from a world that keeps moving, so a retry that rebuilt them a moment later can
		/// legitimately differ. Matching on them would turn a recoverable retry into a permanent
		/// conflict; matching on everything else means a covenant can only ever be recognised, never
		/// quietly redefined.
		/// </para>
		/// </summary>
		public static bool SameFrozenFacts(KingdomVillageCovenantReceipt left,
			KingdomVillageCovenantReceipt right)
		{
			return left != null && right != null && left.Version == right.Version
				&& string.Equals(left.RealmId, right.RealmId, StringComparison.Ordinal)
				&& string.Equals(left.TransactionId, right.TransactionId, StringComparison.Ordinal)
				&& string.Equals(left.FoundingAuthority, right.FoundingAuthority,
					StringComparison.Ordinal)
				&& string.Equals(left.VillageFactionId, right.VillageFactionId,
					StringComparison.Ordinal)
				&& string.Equals(left.VillageDisplayName, right.VillageDisplayName,
					StringComparison.Ordinal)
				&& string.Equals(left.SiteZoneId, right.SiteZoneId, StringComparison.Ordinal)
				&& string.Equals(left.ChronicleEventId, right.ChronicleEventId,
					StringComparison.Ordinal);
		}

		/// <summary>Whether two rows are the same covenant in every frozen field, moving ones
		/// included. This is what a caller confirming its own work asks.</summary>
		public static bool Same(KingdomVillageCovenantReceipt left,
			KingdomVillageCovenantReceipt right)
		{
			return SameFrozenFacts(left, right)
				&& string.Equals(left.ReceiptId, right.ReceiptId, StringComparison.Ordinal)
				&& left.SealedStanding == right.SealedStanding
				&& left.ReservationTick == right.ReservationTick;
		}

		/// <summary>Whether a state value is one of the three this build has an answer for.</summary>
		public static bool Defined(KingdomVillageCovenantState state)
		{
			return state == KingdomVillageCovenantState.Compatible
				|| state == KingdomVillageCovenantState.FutureOpaque
				|| state == KingdomVillageCovenantState.Quarantined;
		}
	}
}
