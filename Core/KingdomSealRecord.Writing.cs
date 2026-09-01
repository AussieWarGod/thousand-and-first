using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealRecord
	{
		// Parsed records retain their exact envelope schema so saved inheritance authority can
		// reprove its canonical bytes across upgrades. New records always begin at CurrentSchema.
		private int WireSchema = CurrentSchema;

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
			return KingdomSealFormat.Compose(WireSchema, WriteBody(WireSchema));
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
			return WriteBody(CurrentSchema);
		}

		private KingdomSealBody WriteBody(int Schema)
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
			if (Schema >= 5)
			{
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
			}
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
			if (Schema >= 6)
			{
				body.Put(KeyProfileSchema, ProfileSchema);
				body.Put(KeyTechnologyBand, TechnologyBand);
				body.PutList(KeyCanonicalBody, CanonicalBodyKeys);
				body.Put(KeySourceProfileDigest, SourceProfileDigest);
				body.Put(KeyProfileProvenanceDigest, ProfileProvenanceDigest);
			}
			return body;
		}

	}
}
