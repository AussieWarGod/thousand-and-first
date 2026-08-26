using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealRecord
	{
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

			if (!TryReadCollectionsAndValidate(Schema, Body, record, ref Fault, ref Detail))
			{
				return false;
			}

			Record = record;
			return true;
		}

	}
}
