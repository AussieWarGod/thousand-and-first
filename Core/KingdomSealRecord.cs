using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// One sealed realm, as it crosses from one life to the next.
	/// <para>
	/// <b>This is a summary and not a save.</b> Everything here is a bounded primitive or a
	/// semantic id: names, a street plan of our own works, the roll of settlers as history, the
	/// chronicle, and the handful of numbers <c>KingdomRules.SealedVigour</c> reads. Nothing here
	/// is an object, an inventory, a charge, a liquid, a faction key, a blueprint the engine would
	/// resolve, a quest, a reputation, or a path. <c>DECISIONS.md:208-213</c> is the law it keeps:
	/// no item inheritance, ever &mdash; a settlement that returned your stash would turn
	/// permadeath into a bank.
	/// </para>
	/// <para>
	/// <see cref="StoredWater"/> is the one field that looks like an exception and is not. It is an
	/// <i>input to the seal's own arithmetic</i>, capped there at fifteen points, and it is never
	/// handed to the next world as water. <c>KingdomInheritRules</c> is where that is enforced and
	/// tested.
	/// </para>
	/// <para>
	/// Every string a player could have chosen is sanitized on the way in and on the way out, and
	/// every id is a token from a fixed alphabet. A name is never allowed to become a path, a
	/// faction id, a blueprint id, or a format template.
	/// </para>
	/// </summary>
	internal sealed partial class KingdomSealRecord
	{
		/// <summary>Schema assigned to new records. Parsed records retain their older canonical
		/// envelope until a transition creates a current-schema copy.</summary>
		public const int CurrentSchema = 6;

		/// <summary>The oldest schema this build reads. Pre-release schemas through 3 lacked
		/// per-member immutable topology provenance and are deliberately refused.</summary>
		public const int FirstSchema = 4;

		public const int MaxNameChars = 96;

		public const int MaxLineChars = 320;

		public const int MaxIdChars = 96;

		/// <summary>Works a seal may carry. The city model's own ceiling
		/// (<c>KingdomCityState.MaxWorks</c>); a seal never carries more of a settlement than the
		/// settlement could hold.</summary>
		public const int MaxWorks = 40;

		/// <summary>Named settlers a seal may carry, as history. The city model's own ceiling.</summary>
		public const int MaxRoll = 60;

		/// <summary>Origin and creed tallies a seal may carry.</summary>
		public const int MaxTallies = 32;

		/// <summary>The dead a seal may name.</summary>
		public const int MaxDead = 32;

		/// <summary>
		/// Chronicle lines a seal may carry, head and tail together.
		/// <para>
		/// Permanence is pinned retention, not unlimited growth. When a book is longer than this,
		/// the seal keeps the <b>beginning and the end</b> &mdash; how it started and how it ended
		/// &mdash; and says in the copy how much it skipped. Keeping only the tail would cross a
		/// realm's last quarrels and lose its founding, which is the half a stranger reads for.
		/// </para>
		/// </summary>
		public const int MaxChronicle = 64;

		private const string KeyKind = "kind";
		private const string KeyWriter = "writer";
		private const string KeyEngine = "engine";
		private const string KeyStatus = "status";
		private const string KeyLineage = "lineage";
		private const string KeyLegacy = "legacy";
		private const string KeyOrigin = "origin";
		private const string KeyGeneration = "generation";
		private const string KeyRevision = "revision";
		private const string KeyWritten = "written";
		private const string KeyFounder = "founder";
		private const string KeyCause = "cause";
		private const string KeyCauseKind = "cause_kind";
		private const string KeyCauseTurn = "cause_turn";
		private const string KeyRealm = "realm";
		private const string KeySettlement = "settlement";
		private const string KeySettlementId = "settlement_id";
		private const string KeyRealmId = "realm_id";
		private const string KeyRealmSettlementId = "realm_settlement_id";
		private const string KeyRealmSettlementProvenance = "realm_settlement_provenance";
		private const string KeyRealmIdentityVersion = "realm_identity_version";
		private const string KeyRealmIdentityOrigin = "realm_identity_origin";
		private const string KeyRealmIdentityTransaction = "realm_identity_transaction";
		private const string KeyRealmIdentityLegacy = "realm_identity_legacy";
		private const string KeyRealmIdentityFounded = "realm_identity_founded";
		private const string KeyRealmSeedHigh = "realm_seed_high";
		private const string KeyRealmSeedLow = "realm_seed_low";
		private const string KeyRealmIdentityZone = "realm_identity_zone";
		private const string KeySettlementIdentityVersion = "settlement_identity_version";
		private const string KeySettlementIdentityOrigin = "settlement_identity_origin";
		private const string KeySettlementIdentityTransaction = "settlement_identity_transaction";
		private const string KeySettlementIdentityFounded = "settlement_identity_founded";
		private const string KeySettlementIdentityZone = "settlement_identity_zone";
		private const string KeySettlementIdentityLegacy = "settlement_identity_legacy";
		private const string KeyVocation = "vocation";
		private const string KeyStyle = "style";
		private const string KeyFounded = "founded";
		private const string KeyGround = "ground";
		private const string KeyRegion = "region";
		private const string KeyTerrain = "terrain";
		private const string KeyDepth = "depth";
		private const string KeyStage = "stage";
		private const string KeyPeople = "people";
		private const string KeyDefence = "defence";
		private const string KeyWater = "water";
		private const string KeyWithered = "withered";
		private const string KeyVigour = "vigour";
		private const string KeyRoll = "roll";
		private const string KeyState = "state";
		private const string KeyWorkKey = "work_key";
		private const string KeyWorkX = "work_x";
		private const string KeyWorkY = "work_y";
		private const string KeyWorkCondition = "work_condition";
		private const string KeySpatialVersion = "spatial_version";
		private const string KeySpatialWidth = "spatial_width";
		private const string KeySpatialHeight = "spatial_height";
		private const string KeySpatialEntrySide = "spatial_entry_side";
		private const string KeySpatialEntryX = "spatial_entry_x";
		private const string KeySpatialEntryY = "spatial_entry_y";
		private const string KeyWorkSnapshot = "work_snapshot";
		private const string KeyWorkSnapshotHash = "work_snapshot_hash";
		private const string KeyStreetX = "street_x";
		private const string KeyStreetY = "street_y";
		private const string KeyRollName = "roll_name";
		private const string KeyRollOrigin = "roll_origin";
		private const string KeyRollArrived = "roll_arrived";
		private const string KeyOriginKey = "origin_key";
		private const string KeyOriginCount = "origin_count";
		private const string KeyCreedKey = "creed_key";
		private const string KeyCreedCount = "creed_count";
		private const string KeyChronicle = "chronicle";
		private const string KeyOutsider = "outsider";
		private const string KeyDeadName = "dead_name";
		private const string KeyDeadCause = "dead_cause";
		private const string KeyProfileSchema = "profile_schema";
		private const string KeyTechnologyBand = "technology_band";
		private const string KeyCanonicalBody = "canonical_body";
		private const string KeySourceProfileDigest = "source_profile_digest";
		private const string KeyProfileProvenanceDigest = "profile_provenance_digest";

		/// <summary>Every key this schema defines, in canonical order. A payload carrying anything
		/// else is refused rather than partly understood.</summary>
		private static readonly string[] CanonicalKeysV1 = new string[45]
		{
			KeyWriter, KeyEngine, KeyStatus, KeyLineage, KeyOrigin, KeyGeneration, KeyRevision,
			KeyWritten, KeyFounder, KeyCause, KeyCauseKind, KeyCauseTurn, KeyRealm, KeySettlement,
			KeySettlementId, KeyVocation, KeyStyle, KeyFounded, KeyGround, KeyRegion, KeyTerrain,
			KeyDepth, KeyStage, KeyPeople, KeyDefence, KeyWater, KeyWithered, KeyVigour, KeyRoll,
			KeyState, KeyWorkKey, KeyWorkX, KeyWorkY, KeyWorkCondition, KeyRollName, KeyRollOrigin,
			KeyRollArrived, KeyOriginKey, KeyOriginCount, KeyCreedKey, KeyCreedCount, KeyChronicle,
			KeyOutsider, KeyDeadName, KeyDeadCause
		};

		private static readonly string[] CanonicalKeysV2 = new string[47]
		{
			KeyKind, KeyWriter, KeyEngine, KeyStatus, KeyLineage, KeyLegacy, KeyOrigin, KeyGeneration,
			KeyRevision, KeyWritten, KeyFounder, KeyCause, KeyCauseKind, KeyCauseTurn, KeyRealm,
			KeySettlement, KeySettlementId, KeyVocation, KeyStyle, KeyFounded, KeyGround, KeyRegion,
			KeyTerrain, KeyDepth, KeyStage, KeyPeople, KeyDefence, KeyWater, KeyWithered, KeyVigour,
			KeyRoll, KeyState, KeyWorkKey, KeyWorkX, KeyWorkY, KeyWorkCondition, KeyRollName,
			KeyRollOrigin, KeyRollArrived, KeyOriginKey, KeyOriginCount, KeyCreedKey, KeyCreedCount,
			KeyChronicle, KeyOutsider, KeyDeadName, KeyDeadCause
		};

		private static readonly string[] CanonicalKeysV4 = new string[]
		{
			KeyKind, KeyWriter, KeyEngine, KeyStatus, KeyLineage, KeyLegacy, KeyOrigin,
			KeyGeneration, KeyRevision, KeyWritten, KeyFounder, KeyCause, KeyCauseKind,
			KeyCauseTurn, KeyRealm, KeyRealmId, KeyRealmSettlementId,
			KeyRealmSettlementProvenance,
			KeyRealmIdentityVersion, KeyRealmIdentityOrigin, KeyRealmIdentityTransaction,
			KeyRealmIdentityLegacy, KeyRealmIdentityFounded, KeyRealmSeedHigh, KeyRealmSeedLow,
			KeyRealmIdentityZone, KeySettlement, KeySettlementId,
			KeySettlementIdentityVersion, KeySettlementIdentityOrigin,
			KeySettlementIdentityTransaction, KeySettlementIdentityFounded,
			KeySettlementIdentityZone, KeySettlementIdentityLegacy, KeyVocation, KeyStyle,
			KeyFounded, KeyGround, KeyRegion, KeyTerrain, KeyDepth, KeyStage, KeyPeople,
			KeyDefence, KeyWater, KeyWithered, KeyVigour, KeyRoll, KeyState, KeyWorkKey,
			KeyWorkX, KeyWorkY, KeyWorkCondition, KeyRollName, KeyRollOrigin, KeyRollArrived,
			KeyOriginKey, KeyOriginCount, KeyCreedKey, KeyCreedCount, KeyChronicle,
			KeyOutsider, KeyDeadName, KeyDeadCause
		};

		private static readonly string[] CanonicalKeysV5 = new string[]
		{
			KeyKind, KeyWriter, KeyEngine, KeyStatus, KeyLineage, KeyLegacy, KeyOrigin,
			KeyGeneration, KeyRevision, KeyWritten, KeyFounder, KeyCause, KeyCauseKind,
			KeyCauseTurn, KeyRealm, KeyRealmId, KeyRealmSettlementId,
			KeyRealmSettlementProvenance,
			KeyRealmIdentityVersion, KeyRealmIdentityOrigin, KeyRealmIdentityTransaction,
			KeyRealmIdentityLegacy, KeyRealmIdentityFounded, KeyRealmSeedHigh, KeyRealmSeedLow,
			KeyRealmIdentityZone, KeySettlement, KeySettlementId,
			KeySettlementIdentityVersion, KeySettlementIdentityOrigin,
			KeySettlementIdentityTransaction, KeySettlementIdentityFounded,
			KeySettlementIdentityZone, KeySettlementIdentityLegacy, KeyVocation, KeyStyle,
			KeyFounded, KeyGround, KeyRegion, KeyTerrain, KeyDepth, KeyStage, KeyPeople,
			KeyDefence, KeyWater, KeyWithered, KeyVigour, KeyRoll, KeyState, KeyWorkKey,
			KeyWorkX, KeyWorkY, KeyWorkCondition, KeySpatialVersion, KeySpatialWidth,
			KeySpatialHeight, KeySpatialEntrySide, KeySpatialEntryX, KeySpatialEntryY,
			KeyWorkSnapshot, KeyWorkSnapshotHash, KeyStreetX, KeyStreetY,
			KeyRollName, KeyRollOrigin, KeyRollArrived, KeyOriginKey, KeyOriginCount,
			KeyCreedKey, KeyCreedCount, KeyChronicle, KeyOutsider, KeyDeadName, KeyDeadCause
		};

		private static readonly string[] CanonicalKeys = WithProfileKeys(CanonicalKeysV5);

		private static string[] WithProfileKeys(string[] Prior)
		{
			string[] result = new string[Prior.Length + 5];
			Array.Copy(Prior, result, Prior.Length);
			result[Prior.Length] = KeyProfileSchema;
			result[Prior.Length + 1] = KeyTechnologyBand;
			result[Prior.Length + 2] = KeyCanonicalBody;
			result[Prior.Length + 3] = KeySourceProfileDigest;
			result[Prior.Length + 4] = KeyProfileProvenanceDigest;
			return result;
		}

		private static readonly string[] StatusNames = new string[4] { "living", "terminal", "retired", "promoted" };

	}
}
