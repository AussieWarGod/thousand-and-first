using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Where a seal is in its life. The state machine of
	/// <c>INHERITANCE-SEAMS.md:158-218</c>, named.</summary>
	internal enum KingdomSealStatus
	{
		/// <summary>A live stage. The realm is still being played; this record creates no
		/// behaviour anywhere and proves nothing.</summary>
		Living = 0,
		/// <summary>A terminal attempt: the founder died, and the cause has been written down. It
		/// is still only an attempt &mdash; a checkpoint restore or continued play overwrites it.</summary>
		Terminal = 1,
		/// <summary>Deliberate retirement. The founder closed the book themselves, and this
		/// generation of the lineage can no longer be rewritten by continuing to play.</summary>
		Retired = 2,
		/// <summary>Promoted: an ended run, proved ended, with the interregnum drawn and the
		/// inherited state fixed. Immutable from here.</summary>
		Promoted = 3
	}

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
	internal sealed class KingdomSealRecord
	{
		/// <summary>The only schema this build writes.</summary>
		public const int CurrentSchema = 5;

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

		private static readonly string[] CanonicalKeys = new string[]
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

		private static readonly string[] StatusNames = new string[4] { "living", "terminal", "retired", "promoted" };

		public string WriterVersion = "";

		public string EngineVersion = "";

		public KingdomSealStatus Status = KingdomSealStatus.Living;

		/// <summary>The lineage this realm belongs to. Minted once, at founding, and carried by
		/// every generation after it.</summary>
		public string LineageId = "";

		/// <summary>The unique identity of this generation's immutable result. Unlike
		/// <see cref="LineageId"/>, this changes for every successor.</summary>
		public string LegacyId = "";

		/// <summary>The game this record was written from. The eligibility matrix is keyed on it.</summary>
		public string OriginGameId = "";

		/// <summary>How many lives deep this lineage is. Zero for a founder who founded from
		/// nothing.</summary>
		public int Generation;

		/// <summary>Monotonic within one origin. The journal keeps the highest complete one.</summary>
		public int Revision;

		/// <summary>The world tick the record was written at. Diagnostics and ordering only: no
		/// rule anywhere reads it as a clock (<c>DECISIONS.md:151-165</c>).</summary>
		public long WrittenTick;

		public string FounderName = "";

		/// <summary>How the founder died, in one bounded clause. Empty until a terminal attempt.</summary>
		public string CauseText = "";

		/// <summary>A short token for what kind of death it was, for the cairn's grammar.</summary>
		public string CauseKind = "";

		public long CauseTurn;

		public string RealmName = "";

		public string SettlementName = "";

		/// <summary>The settlement's stable semantic id, for telling two seals apart.</summary>
		public string SettlementId = "";

		public string RealmId = "";
		public List<string> RealmSettlementIds = new List<string>();
		public List<string> RealmSettlementProvenance = new List<string>();
		public int RealmIdentityVersion;
		public KingdomIdentityOrigin RealmIdentityOrigin;
		public string RealmIdentityTransactionId = "";
		public string RealmIdentityLegacyFaction = "";
		public long RealmIdentityFoundedTick;
		public ulong RealmIdentitySeedHigh;
		public ulong RealmIdentitySeedLow;
		public string RealmIdentityFirstClaimedZone = "";
		public int SettlementIdentityVersion;
		public KingdomIdentityOrigin SettlementIdentityOrigin;
		public string SettlementIdentityTransactionId = "";
		public long SettlementIdentityFoundedTick;
		public string SettlementIdentityFirstClaimedZone = "";
		public string SettlementIdentityLegacyId = "";

		public string Vocation = "";

		public string Style = "";

		public long FoundedTick;

		/// <summary>The seat's ground, as a zone id. Qud's overworld map is fixed
		/// (<c>SUCCESSION-RESEARCH.md</c> &sect;1.7), so this names real ground in the next world
		/// too &mdash; which is what lets a later life find the same place rather than a copy of
		/// it somewhere else.</summary>
		public string GroundZoneId = "";

		public string RegionName = "";

		public string TerrainBlueprint = "";

		public int Depth;

		/// <summary>The growth stage at sealing, as <c>GrowthStage</c>.</summary>
		public int Stage;

		public int Population;

		public int Defence;

		/// <summary>Drams in the dedicated stores at sealing. A <b>term of the seal's arithmetic
		/// only</b>; see this type's own remarks. No water crosses.</summary>
		public int StoredWater;

		public bool Withered;

		/// <summary>The one bounded number, from <c>KingdomRules.SealedVigour</c>. Written by the
		/// capture and re-derivable from the terms above, which is how a reader checks the two
		/// against each other.</summary>
		public int Vigour;

		/// <summary>The interregnum draw, 0&ndash;99, or -1 before promotion.</summary>
		public int InterregnumRoll = -1;

		/// <summary>The resolved state, or -1 before promotion.</summary>
		public int InheritedState = -1;

		public List<string> WorkKeys = new List<string>();

		public List<int> WorkX = new List<int>();

		public List<int> WorkY = new List<int>();

		public List<int> WorkConditions = new List<int>();

		/// <summary>Zero is a schema-4 compatible anchor proxy. One freezes exact authored
		/// architecture and a zone-relative street graph.</summary>
		public int SpatialVersion;

		public int SpatialWidth;

		public int SpatialHeight;

		public int SpatialEntrySide = KingdomInheritanceSpatialRules.NoEntry;

		public int SpatialEntryX;

		public int SpatialEntryY;

		public List<string> WorkSnapshots = new List<string>();

		public List<string> WorkSnapshotHashes = new List<string>();

		public List<int> StreetX = new List<int>();

		public List<int> StreetY = new List<int>();

		public List<string> RollNames = new List<string>();

		public List<string> RollOrigins = new List<string>();

		public List<string> RollArrived = new List<string>();

		public List<string> OriginKeys = new List<string>();

		public List<int> OriginCounts = new List<int>();

		public List<string> CreedKeys = new List<string>();

		public List<int> CreedCounts = new List<int>();

		public List<string> Chronicle = new List<string>();

		public List<string> Outsider = new List<string>();

		public List<string> DeadNames = new List<string>();

		public List<string> DeadCauses = new List<string>();

		/// <summary>True once the interregnum has been drawn and the state fixed.</summary>
		public bool IsResolved => InterregnumRoll >= 0 && KingdomRules.IsKnownState((KingdomRules.InheritedState)InheritedState);

		/// <summary>
		/// The whole record as one canonical seal file.
		/// <para>
		/// Side effects: none, and in particular the record is not repaired in place &mdash; a
		/// caller that wrote nonsense into a field gets it back on the next read, which is what
		/// makes the round-trip test meaningful. Sanitization happens at capture, in
		/// <c>KingdomSealRules</c>.
		/// </para>
		/// </summary>
		/// <exception cref="InvalidOperationException">No digest provider is available.</exception>
		public string Compose()
		{
			return KingdomSealFormat.Compose(CurrentSchema, WriteBody());
		}

		/// <summary>
		/// Reads a seal file whole, or refuses it whole.
		/// </summary>
		/// <param name="FileText">The file's text.</param>
		/// <param name="Record">The record on success; null on failure.</param>
		/// <param name="Fault">Why it was refused.</param>
		/// <param name="Detail">A line naming the refusal for the log; never null.</param>
		/// <returns>True when the file is a complete, checked, in-schema seal.</returns>
		public static bool TryParse(string FileText, out KingdomSealRecord Record, out KingdomSealFault Fault, out string Detail)
		{
			Record = null;
			int schema;
			KingdomSealBody body;
			if (!KingdomSealFormat.TryParse(FileText, FirstSchema, CurrentSchema, out schema, out body, out Fault, out Detail))
			{
				return false;
			}
			try
			{
				return TryReadBody(schema, body, out Record, out Fault, out Detail);
			}
			catch (Exception)
			{
				Record = null;
				Fault = KingdomSealFault.Malformed;
				Detail = "the seal's record is malformed";
				return false;
			}
		}

		internal KingdomSealBody WriteBody()
		{
			KingdomSealBody body = new KingdomSealBody();
			body.Put(KeyKind, "record");
			body.Put(KeyWriter, WriterVersion);
			body.Put(KeyEngine, EngineVersion);
			body.Put(KeyStatus, StatusNames[(int)Status]);
			body.Put(KeyLineage, LineageId);
			body.Put(KeyLegacy, LegacyId);
			body.Put(KeyOrigin, OriginGameId);
			body.Put(KeyGeneration, Generation);
			body.Put(KeyRevision, Revision);
			body.Put(KeyWritten, WrittenTick);
			body.Put(KeyFounder, FounderName);
			body.Put(KeyCause, CauseText);
			body.Put(KeyCauseKind, CauseKind);
			body.Put(KeyCauseTurn, CauseTurn);
			body.Put(KeyRealm, RealmName);
			body.Put(KeyRealmId, RealmId);
			body.PutList(KeyRealmSettlementId, RealmSettlementIds);
			body.PutList(KeyRealmSettlementProvenance, RealmSettlementProvenance);
			body.Put(KeyRealmIdentityVersion, RealmIdentityVersion);
			body.Put(KeyRealmIdentityOrigin, (int)RealmIdentityOrigin);
			body.Put(KeyRealmIdentityTransaction, RealmIdentityTransactionId);
			body.Put(KeyRealmIdentityLegacy, EncodeEvidence(RealmIdentityLegacyFaction));
			body.Put(KeyRealmIdentityFounded, RealmIdentityFoundedTick);
			body.Put(KeyRealmSeedHigh, RealmIdentitySeedHigh.ToString("x16"));
			body.Put(KeyRealmSeedLow, RealmIdentitySeedLow.ToString("x16"));
			body.Put(KeyRealmIdentityZone, RealmIdentityFirstClaimedZone);
			body.Put(KeySettlement, SettlementName);
			body.Put(KeySettlementId, SettlementId);
			body.Put(KeySettlementIdentityVersion, SettlementIdentityVersion);
			body.Put(KeySettlementIdentityOrigin, (int)SettlementIdentityOrigin);
			body.Put(KeySettlementIdentityTransaction, SettlementIdentityTransactionId);
			body.Put(KeySettlementIdentityFounded, SettlementIdentityFoundedTick);
			body.Put(KeySettlementIdentityZone, SettlementIdentityFirstClaimedZone);
			body.Put(KeySettlementIdentityLegacy, EncodeEvidence(SettlementIdentityLegacyId));
			body.Put(KeyVocation, Vocation);
			body.Put(KeyStyle, Style);
			body.Put(KeyFounded, FoundedTick);
			body.Put(KeyGround, GroundZoneId);
			body.Put(KeyRegion, RegionName);
			body.Put(KeyTerrain, TerrainBlueprint);
			body.Put(KeyDepth, Depth);
			body.Put(KeyStage, Stage);
			body.Put(KeyPeople, Population);
			body.Put(KeyDefence, Defence);
			body.Put(KeyWater, StoredWater);
			body.Put(KeyWithered, Withered ? 1L : 0L);
			body.Put(KeyVigour, Vigour);
			body.Put(KeyRoll, InterregnumRoll);
			body.Put(KeyState, StateName(InheritedState));
			body.PutList(KeyWorkKey, WorkKeys);
			body.PutList(KeyWorkX, Widen(WorkX));
			body.PutList(KeyWorkY, Widen(WorkY));
			body.PutList(KeyWorkCondition, Widen(WorkConditions));
			body.Put(KeySpatialVersion, SpatialVersion);
			body.Put(KeySpatialWidth, SpatialWidth);
			body.Put(KeySpatialHeight, SpatialHeight);
			body.Put(KeySpatialEntrySide, SpatialEntrySide);
			body.Put(KeySpatialEntryX, SpatialEntryX);
			body.Put(KeySpatialEntryY, SpatialEntryY);
			body.PutList(KeyWorkSnapshot, WorkSnapshots);
			body.PutList(KeyWorkSnapshotHash, WorkSnapshotHashes);
			body.PutList(KeyStreetX, Widen(StreetX));
			body.PutList(KeyStreetY, Widen(StreetY));
			body.PutList(KeyRollName, RollNames);
			body.PutList(KeyRollOrigin, RollOrigins);
			body.PutList(KeyRollArrived, RollArrived);
			body.PutList(KeyOriginKey, OriginKeys);
			body.PutList(KeyOriginCount, Widen(OriginCounts));
			body.PutList(KeyCreedKey, CreedKeys);
			body.PutList(KeyCreedCount, Widen(CreedCounts));
			body.PutList(KeyChronicle, Chronicle);
			body.PutList(KeyOutsider, Outsider);
			body.PutList(KeyDeadName, DeadNames);
			body.PutList(KeyDeadCause, DeadCauses);
			return body;
		}

		internal static bool TryReadBody(int Schema, KingdomSealBody Body, out KingdomSealRecord Record, out KingdomSealFault Fault, out string Detail)
		{
			Record = null;
			Fault = KingdomSealFault.None;
			Detail = "";
			string[] canonical = (Schema == 1) ? CanonicalKeysV1 :
				(Schema == 2 ? CanonicalKeysV2 : (Schema == 4 ? CanonicalKeysV4 : CanonicalKeys));
			HashSet<string> known = new HashSet<string>(canonical);
			for (int i = 0; i < Body.Keys.Count; i++)
			{
				if (!known.Contains(Body.Keys[i]))
				{
					Fault = KingdomSealFault.UnknownKey;
					Detail = "the seal carries a field this build does not define: '" + Body.Keys[i] + "'";
					return false;
				}
			}
			for (int i = 0; i < canonical.Length; i++)
			{
				if (!Body.Has(canonical[i]))
				{
					Fault = KingdomSealFault.MissingKey;
					Detail = "the seal is missing the field '" + canonical[i] + "'";
					return false;
				}
			}
			if (Schema >= 2 && (Body.KindOf(KeyKind) != KingdomSealKind.Text || Body.Text(KeyKind) != "record"))
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "the payload is not a kingdom record";
				return false;
			}

			KingdomSealRecord record = new KingdomSealRecord();
			if (!ReadText(Body, KeyWriter, MaxNameChars, out record.WriterVersion, ref Fault, ref Detail)
				|| !ReadText(Body, KeyEngine, MaxNameChars, out record.EngineVersion, ref Fault, ref Detail)
				|| !ReadText(Body, KeyFounder, MaxNameChars, out record.FounderName, ref Fault, ref Detail)
				|| !ReadText(Body, KeyCause, MaxLineChars, out record.CauseText, ref Fault, ref Detail)
				|| !ReadText(Body, KeyRealm, MaxNameChars, out record.RealmName, ref Fault, ref Detail)
				|| !ReadText(Body, KeySettlement, MaxNameChars, out record.SettlementName, ref Fault, ref Detail)
				|| !ReadText(Body, KeyVocation, MaxNameChars, out record.Vocation, ref Fault, ref Detail)
				|| !ReadText(Body, KeyStyle, MaxNameChars, out record.Style, ref Fault, ref Detail)
				|| !ReadText(Body, KeyRegion, MaxNameChars, out record.RegionName, ref Fault, ref Detail))
			{
				return false;
			}
			if (!ReadToken(Body, KeyLineage, out record.LineageId, ref Fault, ref Detail)
				|| (Schema >= 2 && !ReadToken(Body, KeyLegacy, out record.LegacyId, ref Fault, ref Detail))
				|| !ReadToken(Body, KeyOrigin, out record.OriginGameId, ref Fault, ref Detail)
				|| !ReadToken(Body, KeySettlementId, out record.SettlementId, ref Fault, ref Detail)
				|| !ReadToken(Body, KeyGround, out record.GroundZoneId, ref Fault, ref Detail)
				|| !ReadToken(Body, KeyTerrain, out record.TerrainBlueprint, ref Fault, ref Detail))
			{
				return false;
			}
			if (Schema >= 3)
			{
				string realmLegacy;
				string settlementLegacy;
				string high;
				string low;
				int realmOrigin;
				int settlementOrigin;
				if (!ReadToken(Body, KeyRealmId, out record.RealmId, ref Fault, ref Detail) ||
					!ReadTokens(Body, KeyRealmSettlementId, KingdomIdentityRules.MaxSettlements,
						out record.RealmSettlementIds, ref Fault, ref Detail) ||
					!ReadBoundedTokens(Body, KeyRealmSettlementProvenance,
						KingdomIdentityRules.MaxSettlements, 4300,
						out record.RealmSettlementProvenance, ref Fault, ref Detail) ||
					!ReadInt(Body, KeyRealmIdentityVersion, 0, 32,
						out record.RealmIdentityVersion, ref Fault, ref Detail) ||
					!ReadInt(Body, KeyRealmIdentityOrigin, 0, 3, out realmOrigin,
						ref Fault, ref Detail) ||
					!ReadOptionalToken(Body, KeyRealmIdentityTransaction,
						out record.RealmIdentityTransactionId, ref Fault, ref Detail) ||
					!ReadText(Body, KeyRealmIdentityLegacy, 1400, out realmLegacy,
						ref Fault, ref Detail) ||
					!ReadLong(Body, KeyRealmIdentityFounded, 0L, long.MaxValue,
						out record.RealmIdentityFoundedTick, ref Fault, ref Detail) ||
					!ReadToken(Body, KeyRealmSeedHigh, out high, ref Fault, ref Detail) ||
					!ReadToken(Body, KeyRealmSeedLow, out low, ref Fault, ref Detail) ||
					!ReadBoundedToken(Body, KeyRealmIdentityZone, 512,
						out record.RealmIdentityFirstClaimedZone, ref Fault, ref Detail) ||
					!ReadInt(Body, KeySettlementIdentityVersion, 0, 32,
						out record.SettlementIdentityVersion, ref Fault, ref Detail) ||
					!ReadInt(Body, KeySettlementIdentityOrigin, 0, 3,
						out settlementOrigin, ref Fault, ref Detail) ||
					!ReadOptionalToken(Body, KeySettlementIdentityTransaction,
						out record.SettlementIdentityTransactionId, ref Fault, ref Detail) ||
					!ReadLong(Body, KeySettlementIdentityFounded, 0L, long.MaxValue,
						out record.SettlementIdentityFoundedTick, ref Fault, ref Detail) ||
					!ReadBoundedToken(Body, KeySettlementIdentityZone, 512,
						out record.SettlementIdentityFirstClaimedZone, ref Fault, ref Detail) ||
					!ReadText(Body, KeySettlementIdentityLegacy, 1400,
						out settlementLegacy, ref Fault, ref Detail) ||
					!TryParseHex64(high, out record.RealmIdentitySeedHigh) ||
					!TryParseHex64(low, out record.RealmIdentitySeedLow) ||
					!TryDecodeEvidence(realmLegacy, out record.RealmIdentityLegacyFaction) ||
					!TryDecodeEvidence(settlementLegacy, out record.SettlementIdentityLegacyId))
				{
					if (Fault == KingdomSealFault.None)
					{
						Fault = KingdomSealFault.OutOfBounds;
						Detail = "the seal's immutable identity provenance is malformed";
					}
					return false;
				}
				record.RealmIdentityOrigin = (KingdomIdentityOrigin)realmOrigin;
				record.SettlementIdentityOrigin = (KingdomIdentityOrigin)settlementOrigin;
				KingdomIdentityFault identityFault;
				if (!KingdomIdentityRules.ReproveRealm(record.RealmId,
					record.RealmIdentityVersion, record.RealmIdentityOrigin,
					record.RealmIdentityTransactionId, record.RealmIdentityLegacyFaction,
					record.RealmIdentityFoundedTick, record.RealmIdentitySeedHigh,
					record.RealmIdentitySeedLow, record.RealmIdentityFirstClaimedZone,
					out identityFault) || !KingdomIdentityRules.ValidateRealmTopology(
						record.RealmId, record.RealmSettlementIds, out identityFault) ||
					!record.RealmSettlementIds.Contains(record.SettlementId) ||
					!KingdomSealRules.ExactTopologyProvenance(record.RealmId,
						record.RealmSettlementIds, record.RealmSettlementProvenance,
						record.SettlementId, record.SettlementIdentityVersion,
						record.SettlementIdentityOrigin,
						record.SettlementIdentityTransactionId,
						record.SettlementIdentityFoundedTick,
						record.SettlementIdentityFirstClaimedZone,
						record.SettlementIdentityLegacyId) ||
					!KingdomIdentityRules.ReproveSettlement(record.SettlementId,
						record.RealmId, record.SettlementIdentityVersion,
						record.SettlementIdentityOrigin,
						record.SettlementIdentityTransactionId,
						record.SettlementIdentityFoundedTick,
						record.SettlementIdentityFirstClaimedZone, out identityFault))
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "the seal's immutable realm topology or provenance cannot be reproved";
					return false;
				}
			}
			if (Schema == 1)
			{
				record.LegacyId = record.LineageId;
			}
			if (!ReadOptionalToken(Body, KeyCauseKind, out record.CauseKind, ref Fault, ref Detail))
			{
				return false;
			}

			string statusName = Body.Text(KeyStatus);
			int status = IndexOf(StatusNames, statusName);
			if (Body.KindOf(KeyStatus) != KingdomSealKind.Text || status < 0)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal's status is not one this build knows";
				return false;
			}
			record.Status = (KingdomSealStatus)status;
			if (record.Status == KingdomSealStatus.Terminal
				&& (record.CauseText.Length == 0 || record.CauseKind.Length == 0))
			{
				Fault = KingdomSealFault.MissingKey;
				Detail = "the terminal attempt does not name both its cause and cause kind";
				return false;
			}
			string stateName = Body.Text(KeyState);
			if (Body.KindOf(KeyState) != KingdomSealKind.Text)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "the seal's inherited state is not written as a name";
				return false;
			}
			if (stateName == "")
			{
				record.InheritedState = -1;
			}
			else
			{
				int state = IndexOf(KingdomRules.InheritedStateNames, stateName);
				if (state < 0)
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "the seal names an inherited state this build does not know";
					return false;
				}
				record.InheritedState = state;
			}

			if (!ReadInt(Body, KeyGeneration, 0, 1024, out record.Generation, ref Fault, ref Detail)
				|| !ReadInt(Body, KeyRevision, 0, int.MaxValue, out record.Revision, ref Fault, ref Detail)
				|| !ReadInt(Body, KeyDepth, -128, 128, out record.Depth, ref Fault, ref Detail)
				|| !ReadInt(Body, KeyStage, 0, 8, out record.Stage, ref Fault, ref Detail)
				|| !ReadInt(Body, KeyPeople, 0, MaxRoll, out record.Population, ref Fault, ref Detail)
				|| !ReadInt(Body, KeyDefence, 0, 4096, out record.Defence, ref Fault, ref Detail)
				|| !ReadInt(Body, KeyWater, 0, 1000000, out record.StoredWater, ref Fault, ref Detail)
				|| !ReadInt(Body, KeyVigour, 0, KingdomRules.MaxSealedVigour, out record.Vigour, ref Fault, ref Detail)
				|| !ReadInt(Body, KeyRoll, -1, 99, out record.InterregnumRoll, ref Fault, ref Detail))
			{
				return false;
			}
			long withered;
			if (!ReadLong(Body, KeyWithered, 0L, 1L, out withered, ref Fault, ref Detail)
				|| !ReadLong(Body, KeyWritten, 0L, long.MaxValue, out record.WrittenTick, ref Fault, ref Detail)
				|| !ReadLong(Body, KeyCauseTurn, 0L, long.MaxValue, out record.CauseTurn, ref Fault, ref Detail)
				|| !ReadLong(Body, KeyFounded, 0L, long.MaxValue, out record.FoundedTick, ref Fault, ref Detail))
			{
				return false;
			}
			record.Withered = withered == 1L;
			if ((record.Status == KingdomSealStatus.Living || record.Status == KingdomSealStatus.Retired)
				&& (record.CauseText.Length > 0 || record.CauseKind.Length > 0 || record.CauseTurn > 0L))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "a non-terminal stage carries terminal cause data";
				return false;
			}
			if (record.Status == KingdomSealStatus.Promoted
				&& ((record.CauseText.Length > 0) != (record.CauseKind.Length > 0)))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the promoted seal carries only half of its terminal cause";
				return false;
			}

			if (!ReadTokens(Body, KeyWorkKey, MaxWorks, out record.WorkKeys, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyWorkX, MaxWorks, 0, 255, out record.WorkX, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyWorkY, MaxWorks, 0, 255, out record.WorkY, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyWorkCondition, MaxWorks, 0, 100, out record.WorkConditions, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyRollName, MaxRoll, MaxNameChars, out record.RollNames, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyRollOrigin, MaxRoll, MaxNameChars, out record.RollOrigins, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyRollArrived, MaxRoll, MaxNameChars, out record.RollArrived, ref Fault, ref Detail)
				|| !ReadTokens(Body, KeyOriginKey, MaxTallies, out record.OriginKeys, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyOriginCount, MaxTallies, 0, 100000, out record.OriginCounts, ref Fault, ref Detail)
				|| !ReadTokens(Body, KeyCreedKey, MaxTallies, out record.CreedKeys, ref Fault, ref Detail)
				|| !ReadInts(Body, KeyCreedCount, MaxTallies, 0, 100000, out record.CreedCounts, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyChronicle, MaxChronicle, MaxLineChars, out record.Chronicle, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyOutsider, MaxChronicle, MaxLineChars, out record.Outsider, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyDeadName, MaxDead, MaxNameChars, out record.DeadNames, ref Fault, ref Detail)
				|| !ReadTexts(Body, KeyDeadCause, MaxDead, MaxLineChars, out record.DeadCauses, ref Fault, ref Detail))
			{
				return false;
			}
			if (Schema >= 5)
			{
				if (!ReadInt(Body, KeySpatialVersion, 0,
					KingdomInheritanceSpatialRules.SpatialVersion, out record.SpatialVersion,
					ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialWidth, 0,
						KingdomInheritanceSpatialRules.Width, out record.SpatialWidth,
						ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialHeight, 0,
						KingdomInheritanceSpatialRules.Height, out record.SpatialHeight,
						ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialEntrySide,
						KingdomInheritanceSpatialRules.NoEntry, KingdomInheritanceSpatialRules.West,
						out record.SpatialEntrySide, ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialEntryX, 0,
						KingdomInheritanceSpatialRules.Width - 1, out record.SpatialEntryX,
						ref Fault, ref Detail)
					|| !ReadInt(Body, KeySpatialEntryY, 0,
						KingdomInheritanceSpatialRules.Height - 1, out record.SpatialEntryY,
						ref Fault, ref Detail)
					|| !ReadTexts(Body, KeyWorkSnapshot, MaxWorks,
						KingdomInheritanceSpatialRules.MaxSnapshotChars, out record.WorkSnapshots,
						ref Fault, ref Detail)
					|| !ReadTokens(Body, KeyWorkSnapshotHash, MaxWorks,
						out record.WorkSnapshotHashes, ref Fault, ref Detail)
					|| !ReadInts(Body, KeyStreetX, KingdomInheritanceSpatialRules.MaxStreetCells,
						0, KingdomInheritanceSpatialRules.Width - 1, out record.StreetX,
						ref Fault, ref Detail)
					|| !ReadInts(Body, KeyStreetY, KingdomInheritanceSpatialRules.MaxStreetCells,
						0, KingdomInheritanceSpatialRules.Height - 1, out record.StreetY,
						ref Fault, ref Detail))
				{
					return false;
				}
				if (record.SpatialVersion == 0)
				{
					if (record.SpatialWidth != 0 || record.SpatialHeight != 0
						|| record.SpatialEntrySide != KingdomInheritanceSpatialRules.NoEntry
						|| record.SpatialEntryX != 0 || record.SpatialEntryY != 0
						|| record.WorkSnapshots.Count != 0 || record.WorkSnapshotHashes.Count != 0
						|| record.StreetX.Count != 0 || record.StreetY.Count != 0)
					{
						Fault = KingdomSealFault.OutOfBounds;
						Detail = "the legacy spatial proxy carries partial current geometry";
						return false;
					}
				}
				else
				{
					KingdomInheritanceSpatialFault spatialFault;
					if (!KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys, record.WorkX,
						record.WorkY, record.WorkConditions, record.WorkSnapshots,
						record.WorkSnapshotHashes, record.SpatialWidth, record.SpatialHeight,
						record.SpatialEntrySide, record.SpatialEntryX, record.SpatialEntryY,
						record.StreetX, record.StreetY, out spatialFault))
					{
						Fault = KingdomSealFault.OutOfBounds;
						Detail = "the sealed architecture or street graph is malformed: "
							+ spatialFault;
						return false;
					}
				}
			}

			// Parallel columns are a row or they are nothing. A reader that trusted the longest
			// would invent a work out of a default coordinate, which is the city book's own rule
			// (KingdomCityBook.Normalize) applied at the one boundary where the data is untrusted.
			if (record.WorkKeys.Count != record.WorkX.Count || record.WorkKeys.Count != record.WorkY.Count
				|| record.WorkKeys.Count != record.WorkConditions.Count)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal's plan of works is ragged";
				return false;
			}
			if (record.RollNames.Count != record.RollOrigins.Count || record.RollNames.Count != record.RollArrived.Count)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal's roll of settlers is ragged";
				return false;
			}
			if (record.OriginKeys.Count != record.OriginCounts.Count || record.CreedKeys.Count != record.CreedCounts.Count
				|| record.DeadNames.Count != record.DeadCauses.Count)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal's tallies are ragged";
				return false;
			}
			if (HasDuplicate(record.OriginKeys) || HasDuplicate(record.CreedKeys))
			{
				Fault = KingdomSealFault.DuplicateKey;
				Detail = "the seal tallies the same origin or creed twice";
				return false;
			}
			int expectedVigour = KingdomRules.SealedVigour((GrowthStage)record.Stage, record.Population,
				record.Defence, record.StoredWater, record.Withered);
			if (record.Vigour != expectedVigour)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal's vigour does not match the facts it carries";
				return false;
			}
			// A resolved seal must be resolved in both halves. Half a promotion would place a
			// settlement in a state nothing drew, which is exactly the silent guess the whole
			// format exists to make impossible.
			bool rolled = record.InterregnumRoll >= 0;
			bool stated = record.InheritedState >= 0;
			if (rolled != stated)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal is half-promoted: it has " + (rolled ? "a draw and no state" : "a state and no draw");
				return false;
			}
			if (record.Status == KingdomSealStatus.Promoted && !rolled)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal claims to be promoted and carries no draw";
				return false;
			}
			if (record.Status != KingdomSealStatus.Promoted && rolled)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "the seal carries a draw it was never promoted to make";
				return false;
			}

			Record = record;
			return true;
		}

		private static string StateName(int State)
		{
			return (State >= 0 && State < KingdomRules.InheritedStateNames.Length) ? KingdomRules.InheritedStateNames[State] : "";
		}

		private static int IndexOf(string[] Names, string Value)
		{
			if (Value == null)
			{
				return -1;
			}
			for (int i = 0; i < Names.Length; i++)
			{
				if (Names[i] == Value)
				{
					return i;
				}
			}
			return -1;
		}

		private static bool HasDuplicate(List<string> Values)
		{
			HashSet<string> seen = new HashSet<string>();
			for (int i = 0; i < Values.Count; i++)
			{
				if (!seen.Add(Values[i]))
				{
					return true;
				}
			}
			return false;
		}

		private static List<long> Widen(List<int> Values)
		{
			List<long> wide = new List<long>(Values.Count);
			for (int i = 0; i < Values.Count; i++)
			{
				wide.Add(Values[i]);
			}
			return wide;
		}

		private static bool ReadText(KingdomSealBody Body, string Key, int MaxChars, out string Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = "";
			if (Body.KindOf(Key) != KingdomSealKind.Text)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as text";
				return false;
			}
			string text = Body.Text(Key) ?? "";
			if (text.Length > MaxChars)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is longer than " + MaxChars + " characters";
				return false;
			}
			if (!KingdomSealRules.IsSafeText(text))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' carries something no name may carry";
				return false;
			}
			Value = text;
			return true;
		}

		private static bool ReadToken(KingdomSealBody Body, string Key, out string Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = "";
			if (Body.KindOf(Key) != KingdomSealKind.Text)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as text";
				return false;
			}
			string text = Body.Text(Key) ?? "";
			if (text.Length > MaxIdChars || !KingdomSealRules.IsToken(text))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is not an identifier this build accepts";
				return false;
			}
			Value = text;
			return true;
		}

		private static bool ReadOptionalToken(KingdomSealBody Body, string Key, out string Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = "";
			if (Body.KindOf(Key) != KingdomSealKind.Text)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as text";
				return false;
			}
			string text = Body.Text(Key) ?? "";
			if (text.Length > MaxIdChars || (text.Length > 0 && !KingdomSealRules.IsToken(text)))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is not an identifier this build accepts";
				return false;
			}
			Value = text;
			return true;
		}

		private static bool ReadBoundedToken(KingdomSealBody Body, string Key, int Maximum,
			out string Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = "";
			if (Body.KindOf(Key) != KingdomSealKind.Text)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as text";
				return false;
			}
			string text = Body.Text(Key) ?? "";
			if (text.Length == 0 || text.Length > Maximum || !KingdomSealRules.IsToken(text))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is not a bounded identifier this build accepts";
				return false;
			}
			Value = text;
			return true;
		}

		private static bool ReadLong(KingdomSealBody Body, string Key, long Low, long High, out long Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = 0L;
			if (Body.KindOf(Key) != KingdomSealKind.Number)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a number";
				return false;
			}
			long number = Body.Number(Key);
			if (number < Low || number > High)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is " + number + ", outside " + Low + " to " + High;
				return false;
			}
			Value = number;
			return true;
		}

		private static bool ReadInt(KingdomSealBody Body, string Key, int Low, int High, out int Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = 0;
			long wide;
			if (!ReadLong(Body, Key, Low, High, out wide, ref Fault, ref Detail))
			{
				return false;
			}
			Value = (int)wide;
			return true;
		}

		private static bool ReadTexts(KingdomSealBody Body, string Key, int MaxItems, int MaxChars, out List<string> Values, ref KingdomSealFault Fault, ref string Detail)
		{
			Values = null;
			List<string> list = Body.TextList(Key);
			if (list == null)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a list of text";
				return false;
			}
			if (list.Count > MaxItems)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' holds " + list.Count + " entries; no more than " + MaxItems + " may cross";
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Length > MaxChars || !KingdomSealRules.IsSafeText(list[i]))
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "an entry of '" + Key + "' is too long, or carries something no line may carry";
					return false;
				}
			}
			Values = list;
			return true;
		}

		private static bool ReadTokens(KingdomSealBody Body, string Key, int MaxItems, out List<string> Values, ref KingdomSealFault Fault, ref string Detail)
		{
			Values = null;
			List<string> list = Body.TextList(Key);
			if (list == null)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a list of text";
				return false;
			}
			if (list.Count > MaxItems)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' holds " + list.Count + " entries; no more than " + MaxItems + " may cross";
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Length == 0 || list[i].Length > MaxIdChars || !KingdomSealRules.IsToken(list[i]))
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "an entry of '" + Key + "' is not an identifier this build accepts";
					return false;
				}
			}
			Values = list;
			return true;
		}

		private static bool ReadBoundedTokens(KingdomSealBody Body, string Key, int MaxItems,
			int MaxChars, out List<string> Values, ref KingdomSealFault Fault, ref string Detail)
		{
			Values = null;
			List<string> list = Body.TextList(Key);
			if (list == null)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a list of text";
				return false;
			}
			if (list.Count > MaxItems)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' exceeds its row cap";
				return false;
			}
			for (int i = 0; i < list.Count; i++)
				if (string.IsNullOrEmpty(list[i]) || list[i].Length > MaxChars ||
					!KingdomSealRules.IsToken(list[i]))
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "an entry of '" + Key + "' is not a bounded canonical token";
					return false;
				}
			Values = list;
			return true;
		}

		private static bool ReadInts(KingdomSealBody Body, string Key, int MaxItems, int Low, int High, out List<int> Values, ref KingdomSealFault Fault, ref string Detail)
		{
			Values = null;
			List<long> list = Body.NumberList(Key);
			if (list == null)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a list of numbers";
				return false;
			}
			if (list.Count > MaxItems)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' holds " + list.Count + " entries; no more than " + MaxItems + " may cross";
				return false;
			}
			List<int> narrow = new List<int>(list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] < Low || list[i] > High)
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "an entry of '" + Key + "' is outside " + Low + " to " + High;
					return false;
				}
				narrow.Add((int)list[i]);
			}
			Values = narrow;
			return true;
		}

		private static string EncodeEvidence(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "";
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			if (bytes.Length > 1024)
				throw new InvalidOperationException("Immutable identity evidence exceeds seal cap.");
			return Convert.ToBase64String(bytes);
		}

		private static bool TryDecodeEvidence(string Value, out string Evidence)
		{
			Evidence = "";
			if (string.IsNullOrEmpty(Value)) return true;
			if (Value.Length > 1400) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(Value);
				if (bytes.Length > 1024) return false;
				Evidence = new UTF8Encoding(false, true).GetString(bytes);
				return true;
			}
			catch { Evidence = ""; return false; }
		}

		private static bool TryParseHex64(string Value, out ulong Parsed)
		{
			Parsed = 0UL;
			if (Value == null || Value.Length != 16) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				int digit = c >= '0' && c <= '9' ? c - '0' :
					(c >= 'a' && c <= 'f' ? c - 'a' + 10 : -1);
				if (digit < 0) { Parsed = 0UL; return false; }
				Parsed = (Parsed << 4) | (uint)digit;
			}
			return true;
		}

		/// <summary>One line naming this seal for a log or a tester. Never player-facing.</summary>
		public string Describe()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(StatusNames[(int)Status]).Append(' ').Append(LegacyId).Append(" lineage=").Append(LineageId).Append(" gen=").Append(Generation)
				.Append(" rev=").Append(Revision).Append(" origin=").Append(OriginGameId)
				.Append(" '").Append(SettlementName).Append("' vigour=").Append(Vigour)
				.Append(" roll=").Append(InterregnumRoll).Append(" state=").Append(StateName(InheritedState))
				.Append(" works=").Append(WorkKeys.Count).Append(" roll=").Append(RollNames.Count)
				.Append(" chronicle=").Append(Chronicle.Count);
			return sb.ToString();
		}
	}
}
