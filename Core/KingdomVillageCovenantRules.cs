using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// What a covenant row has to be before this build will keep it, and what its name is derived
	/// from.
	/// <para>
	/// Two rules do most of the work here. The first is that an identifier is canonical or it is
	/// nothing: a locator with a leading zero, a transaction with an upper-case digit and a realm
	/// with a stray sign are all refused, because two spellings of one place are two places to
	/// everything downstream that compares them. The second is that the row's name is derived from
	/// the row rather than carried alongside it, so a receipt id and the fields it claims to name
	/// cannot drift apart &mdash; changing any frozen field changes the name, and a name that no
	/// longer matches its own fields is refused rather than trusted.
	/// </para>
	/// <para>
	/// The digest under that name is a SHA-256 and is claimed for exactly one thing: telling
	/// whether these bytes are the bytes that were hashed. There is no secret behind it, so it says
	/// nothing about who wrote them &mdash; a deliberate edit that recomputes the name passes as
	/// cleanly as the original. It is an integrity check and it is called one.
	/// </para>
	/// </summary>
	public static partial class KingdomVillageCovenantRules
	{
		public const string ReceiptPrefix = "taf:village-covenant:v1:";
		private const string ReceiptDomain = "TAF-VILLAGE-COVENANT-RECEIPT-V1";

		/// <summary>
		/// Every field's exact authored maximum, and not one of them is a round number chosen for
		/// comfort. A row's whole width is the sum of these plus its framing, and that sum has to
		/// fit under a cap the civic-memory envelope already reserved before this family had a
		/// name, so each one is quoted from the contract that produces the value rather than
		/// guessed at generously.
		/// <para>
		/// The prefix is twenty-four ASCII characters and the digest sixty-four hex digits, so a
		/// receipt id is exactly eighty-eight bytes and never varies.
		/// </para>
		/// </summary>
		public const int ReceiptPrefixBytes = 24;
		public const int MaxReceiptIdBytes = ReceiptPrefixBytes + KingdomIdentityRules.HashHexChars;

		/// <summary>Thirty-two lower-case hex digits, the shape
		/// <see cref="KingdomIdentityRules.IsFoundingTransaction"/> admits and no other.</summary>
		public const int MaxTransactionIdBytes = 32;

		/// <summary>A realm id is the thirteen-character prefix and sixty-four hex digits, so it is
		/// exactly seventy-seven bytes &mdash; the same seventy-seven the identity frame reserves.
		/// </summary>
		public const int MaxRealmIdBytes = KingdomVillageCovenantCodec.MaxRealmIdBytes;

		/// <summary>
		/// The founding authority's own declared ceiling, quoted rather than guessed.
		/// <para>
		/// <c>KingdomFoundingTransactionRules.MaximumAuthorityLength</c> is 2048 characters, and
		/// this cap is that number in bytes because the encoded authority is ASCII by construction:
		/// a version tag, three integers, a lower-case hex digest, pipe separators, and every
		/// variable field passed through <c>Convert.ToBase64String</c>
		/// (<c>Core/KingdomFoundingTransactionRules.AuthorityCodec.cs:203-206</c>). One character is
		/// therefore one byte, and a cap below the contract's would refuse an authority the founding
		/// codec had just declared lawful.
		/// </para>
		/// </summary>
		public const int MaxAuthorityBytes =
			KingdomFoundingTransactionRules.MaximumAuthorityLength;

		/// <summary>
		/// A faction key and a display-name snapshot are each bounded twice, and both bounds are
		/// enforced.
		/// <para>
		/// The founding contract counts characters: 256 of them, at the staging gate
		/// (<c>Core/KingdomFoundingTransaction.10Begin.cs</c>) and again at the site reservation
		/// (<c>Core/KingdomFoundingTransaction.08SiteReservation.cs</c>). The wire counts bytes, and
		/// a character without a surrogate partner can cost three of them. Enforcing only the byte
		/// cap would admit a thousand-character name that happened to be ASCII; enforcing only the
		/// character cap would let a 256-character name of three-byte characters overflow a row that
		/// had already been paid for. So both, and the byte cap is the character cap tripled rather
		/// than a separate opinion.
		/// </para>
		/// </summary>
		public const int MaxNameChars = 256;
		public const int MaxUtf8BytesPerChar = 3;
		public const int MaxFactionIdBytes = MaxNameChars * MaxUtf8BytesPerChar;
		public const int MaxDisplayNameBytes = MaxNameChars * MaxUtf8BytesPerChar;

		/// <summary>
		/// A canonical locator at its widest: sixty-four world characters at three bytes each, five
		/// separators, and eight numerals. Every term is one of
		/// <see cref="KingdomCuriosityRules"/>' own, so a locator grammar that moves moves this too.
		/// </summary>
		public const int MaxZoneIdBytes =
			KingdomCuriosityRules.MaxWorldIdChars * MaxUtf8BytesPerChar
			+ KingdomCuriosityRules.LocatorSeparators
			+ KingdomCuriosityRules.MaxLocatorNumericChars;

		/// <summary>
		/// The event id is not bounded, it is fixed: sixteen prefix bytes, one kind digit, a colon,
		/// thirty-two transaction digits, a colon and a nine-character lane comes to sixty, and
		/// there is exactly one such string per covenant. A cap with slack in it would be slack in
		/// a row whose total has to fit a reservation made before this family existed.
		/// </summary>
		public const int ChronicleEventPrefixBytes = 16;
		public const int ChronicleLaneBytes = 9;
		public const int MaxChronicleEventBytes = ChronicleEventPrefixBytes + 1 + 1
			+ MaxTransactionIdBytes + 1 + ChronicleLaneBytes;

		/// <summary>
		/// The standing a wire-revision-1 covenant was sealed at, and it has a floor and no ceiling.
		/// <para>
		/// The floor is frozen here rather than read from the rite, and that is the whole of the
		/// point. Judging an archived row against a <i>mutable</i> gameplay constant would
		/// quarantine a founder's real covenant the day somebody retuned it &mdash; history read
		/// off the weather, which is the thing this family exists to refuse. But leaving the floor
		/// at "any positive number" is the opposite mistake: a row claiming a standing of one would
		/// then read as a completed covenant, and a completed covenant is a claim about a rite that
		/// pays. So revision 1 owns its own number. A later revision that seals at a different
		/// figure carries a later row revision and its own floor beside this one.
		/// </para>
		/// <para>
		/// There is deliberately no upper bound. <c>KingdomSystem.SetStanding</c> stores whatever
		/// integer it is given, so a realm that has spent a long time being generous can legitimately
		/// stand higher than any number worth writing down here, and a ceiling invented for tidiness
		/// would refuse a real covenant for the crime of being well liked. The field is four bytes
		/// wide on the wire and that is the only bound this needs.
		/// </para>
		/// </summary>
		public const int MinimumSealedStandingV1 = 600;

		/// <summary>
		/// The one chronicle event id a completed village covenant can carry.
		/// <para>
		/// It is built rather than parsed. A grammar check would accept any well-formed founding
		/// event &mdash; a second city's, another transaction's, a different lane of this very rite
		/// &mdash; and a row that paired a village authority with somebody else's founding event
		/// would look canonical while pointing the chronicle somewhere it never went. So the
		/// expected string is assembled from this row's own kind, transaction and lane, and
		/// compared whole.
		/// </para>
		/// </summary>
		private const string ChronicleEventPrefix = "taf:founding:v1:";
		private const string ChronicleLane = "chronicle";

		/// <summary>The stable name of one covenant, derived from the covenant itself.</summary>
		public static string ReceiptId(KingdomVillageCovenantReceipt row)
		{
			if (row == null) return null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter writer = new BinaryWriter(stream,
						new UTF8Encoding(false, true), true))
					{
						writer.Write(ReceiptDomain);
						writer.Write(row.Version);
						writer.Write(row.RealmId ?? "");
						writer.Write(row.TransactionId ?? "");
						writer.Write(row.FoundingAuthority ?? "");
						writer.Write(row.VillageFactionId ?? "");
						writer.Write(row.VillageDisplayName ?? "");
						writer.Write(row.SiteZoneId ?? "");
						writer.Write(row.ChronicleEventId ?? "");
						writer.Write(row.SealedStanding);
						writer.Write(row.ReservationTick);
						writer.Flush();
					}
					using (SHA256 sha = SHA256.Create())
					{
						byte[] digest = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(ReceiptPrefix);
						for (int i = 0; i < digest.Length; i++)
							text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
						return text.ToString();
					}
				}
			}
			catch (EncoderFallbackException) { return null; }
		}

		/// <summary>
		/// Builds one covenant row and derives its name from the facts it has just frozen.
		/// <para>
		/// The name is never a parameter. A caller that could supply one could supply a name that
		/// belongs to a different covenant, and the row would then carry an identity it had not
		/// earned; deriving it here means the only way to change what a covenant is called is to
		/// change what the covenant says.
		/// </para>
		/// </summary>
		public static KingdomVillageCovenantReceipt Receipt(string realmId, string transactionId,
			string foundingAuthority, string villageFactionId, string villageDisplayName,
			string siteZoneId, string chronicleEventId, int sealedStanding, long reservationTick)
		{
			KingdomVillageCovenantReceipt row = new KingdomVillageCovenantReceipt
			{
				Version = KingdomVillageCovenantReceipt.CurrentVersion,
				RealmId = realmId,
				TransactionId = transactionId,
				FoundingAuthority = foundingAuthority,
				VillageFactionId = villageFactionId,
				VillageDisplayName = villageDisplayName,
				SiteZoneId = siteZoneId,
				ChronicleEventId = chronicleEventId,
				SealedStanding = sealedStanding,
				ReservationTick = reservationTick
			};
			row.ReceiptId = ReceiptId(row) ?? "";
			return row;
		}

		/// <summary>Whether one row is a covenant this build will keep.</summary>
		public static bool TryValidateRow(KingdomVillageCovenantReceipt row, out string failure)
		{
			failure = null;
			if (row == null) return Fail("a covenant row is absent", out failure);
			if (row.Version != KingdomVillageCovenantReceipt.CurrentVersion)
				return Fail("a covenant row declares revision " + row.Version
					+ ", which this build does not write", out failure);
			if (!Bounded(row.RealmId, MaxRealmIdBytes)
				|| !KingdomIdentityRules.IsRealmId(row.RealmId))
				return Fail("the covenant's realm id is not canonical", out failure);
			if (!Bounded(row.TransactionId, MaxTransactionIdBytes)
				|| !KingdomIdentityRules.IsFoundingTransaction(row.TransactionId))
				return Fail("the covenant's founding transaction is not canonical", out failure);
			if (!Bounded(row.SiteZoneId, MaxZoneIdBytes)
				|| !KingdomCuriosityRules.TryFullLocator(row.SiteZoneId))
				return Fail("the covenant's site locator is not the canonical name of a real zone",
					out failure);
			if (!ValidAuthority(row, out failure)) return false;
			if (!Named(row.VillageFactionId, MaxFactionIdBytes))
				return Fail("the covenant's village faction key is unusable", out failure);
			if (!Named(row.VillageDisplayName, MaxDisplayNameBytes))
				return Fail("the covenant's village display-name snapshot is unusable", out failure);
			if (!Bounded(row.ChronicleEventId, MaxChronicleEventBytes)
				|| !string.Equals(row.ChronicleEventId, ChronicleEvent(row.TransactionId),
					StringComparison.Ordinal))
				return Fail("the covenant's chronicle event id is not this rite's own", out failure);
			if (row.SealedStanding < MinimumSealedStandingV1)
				return Fail("the covenant's sealed standing is below anything a rite could seal",
					out failure);
			if (row.ReservationTick < 0L)
				return Fail("the covenant's site-reservation tick is before the world began",
					out failure);
			string derived = ReceiptId(row);
			if (derived == null || !string.Equals(derived, row.ReceiptId, StringComparison.Ordinal))
				return Fail("the covenant's receipt id does not name the covenant it is attached to",
					out failure);
			failure = "";
			return true;
		}
	}
}
