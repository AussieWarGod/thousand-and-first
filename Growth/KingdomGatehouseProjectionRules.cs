using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Durable state of one gatehouse satellite identity on its root.</summary>
	public enum KingdomGatehouseSlotState : byte
	{
		Empty = 0,
		Pending = 1,
		Settled = 2,
		Contested = 3
	}

	/// <summary>Exact loaded-world evidence for one frozen satellite identity.</summary>
	public enum KingdomGatehouseSlotEvidence : byte
	{
		Absent = 0,
		Staged = 1,
		ExactPlacement = 2,
		Foreign = 3,
		Duplicate = 4
	}

	public enum KingdomGatehouseSlotAction : byte
	{
		Refuse = 0,
		PublishIdentity = 1,
		PublishPending = 2,
		Create = 3,
		Place = 4,
		Settle = 5,
		Verify = 6
	}

	/// <summary>Recovery at the two pending-v1 identity publication boundaries.</summary>
	public enum KingdomGatehouseLegacyPublicationAction : byte
	{
		Refuse = 0,
		Create = 1,
		AdoptCustody = 2,
		PublishPending = 3
	}

	/// <summary>Pure reducer shared by live projection and callback-cut tests.</summary>
	public static class KingdomGatehouseProjectionRules
	{
		public const string SatelliteIdPrefix = "taf:gatehouse:v1:";

		/// <summary>Stable output identity; retries never draw a replacement object ID.</summary>
		public static string StableSatelliteId(string RootId, string PlanReceipt, int Index)
		{
			if (string.IsNullOrEmpty(RootId) || RootId.Length > 256
				|| string.IsNullOrEmpty(PlanReceipt) || PlanReceipt.Length > 512
				|| Index < 0 || Index >= KingdomGatehouseTopology.SatelliteCount) return null;
			string payload = "TAF-GATEHOUSE-SATELLITE-V1|"
				+ RootId.Length.ToString(CultureInfo.InvariantCulture) + ":" + RootId + "|"
				+ PlanReceipt.Length.ToString(CultureInfo.InvariantCulture) + ":" + PlanReceipt
				+ "|" + Index.ToString(CultureInfo.InvariantCulture);
			byte[] digest;
			using (SHA256 sha = SHA256.Create())
				digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
			StringBuilder result = new StringBuilder(SatelliteIdPrefix, 81);
			for (int i = 0; i < digest.Length; i++) result.Append(digest[i].ToString("x2"));
			return result.ToString();
		}

		/// <summary>A mutable root receipt may name only the identity derived from paid truth.</summary>
		public static bool ExactSatelliteId(string RootId, string PlanReceipt, int Index,
			string ObservedId)
		{
			string expected = StableSatelliteId(RootId, PlanReceipt, Index);
			return !string.IsNullOrEmpty(expected)
				&& string.Equals(expected, ObservedId, StringComparison.Ordinal);
		}

		/// <summary>Historical v1 stored engine IDs; v2 alone requires derivation.</summary>
		public static bool ExactStoredSatelliteId(bool Deterministic, string RootId,
			string PlanReceipt, int Index, string ObservedId)
		{
			if (Deterministic)
				return ExactSatelliteId(RootId, PlanReceipt, Index, ObservedId);
			if (Index < 0 || Index >= KingdomGatehouseTopology.SatelliteCount
				|| string.IsNullOrEmpty(ObservedId) || ObservedId.Length > 256) return false;
			for (int i = 0; i < ObservedId.Length; i++)
				if (ObservedId[i] < 0x20 || ObservedId[i] == 0x7f) return false;
			return true;
		}

		/// <summary>
		/// A landed pending-v1 owner must survive the cut after its temporary carrier was removed
		/// and before schema publication. Body truth is deliberately not an input: a missing,
		/// duplicate, or foreign output blocks resume, but must not make cleanup orphan the rest.
		/// </summary>
		public static bool MustRetainLegacyOwnerAcrossSchemaCut(bool HasSchemaInt,
			bool HasSchemaString, bool HasV2Carrier, bool HasV1PendingCarrier,
			int StateFieldCount, int SettledStateCount, bool CanonicalPlan,
			bool SixUniqueStoredIds)
		{
			return !HasSchemaInt && !HasSchemaString && !HasV2Carrier
				&& !HasV1PendingCarrier
				&& StateFieldCount == KingdomGatehouseTopology.SatelliteCount
				&& SettledStateCount == KingdomGatehouseTopology.SatelliteCount
				&& CanonicalPlan && SixUniqueStoredIds;
		}

		/// <summary>Cleanup keeps the owner broadly; resume additionally needs exact six bodies.</summary>
		public static bool CanResumeLegacySchemaCut(bool HasSchemaInt,
			bool HasSchemaString, bool HasV2Carrier, bool HasV1PendingCarrier,
			int StateFieldCount, int SettledStateCount, bool CanonicalPlan,
			bool SixUniqueStoredIds, bool ExactSixBodies)
		{
			return ExactSixBodies && MustRetainLegacyOwnerAcrossSchemaCut(HasSchemaInt,
				HasSchemaString, HasV2Carrier, HasV1PendingCarrier, StateFieldCount,
				SettledStateCount, CanonicalPlan, SixUniqueStoredIds);
		}

		/// <summary>
		/// A pending-v1 object acquired its engine identity before that identity could be
		/// published.  Its serialized carrier is the only lawful authority for adopting it.
		/// </summary>
		public static bool CanAdoptUnpublishedLegacyCustody(int Index,
			KingdomGatehouseSlotState State, bool HasPublishedIdentity,
			bool HasExactCarrier, bool BlueprintExact, bool Unplaced,
			bool BoundedIdentity, bool UniqueGlobalIdentity, bool ExistingMarksCompatible)
		{
			return Index >= 0 && Index < KingdomGatehouseTopology.SatelliteCount
				&& State == KingdomGatehouseSlotState.Empty && !HasPublishedIdentity
				&& HasExactCarrier && BlueprintExact && Unplaced && BoundedIdentity
				&& UniqueGlobalIdentity && ExistingMarksCompatible;
		}

		/// <summary>The identity commit precedes the pending-phase commit on pending-v1.</summary>
		public static bool CanPublishLegacyPendingFromStagedIdentity(int Index,
			KingdomGatehouseSlotState State, bool HasPublishedIdentity,
			KingdomGatehouseSlotEvidence Evidence)
		{
			return Index >= 0 && Index < KingdomGatehouseTopology.SatelliteCount
				&& State == KingdomGatehouseSlotState.Empty && HasPublishedIdentity
				&& Evidence == KingdomGatehouseSlotEvidence.Staged;
		}

		public static KingdomGatehouseLegacyPublicationAction ResolveLegacyPublicationCut(
			int Index, KingdomGatehouseSlotState State, bool HasPublishedIdentity,
			bool HasExactCarrier, bool BlueprintExact, bool Unplaced,
			bool BoundedIdentity, bool UniqueGlobalIdentity, bool ExistingMarksCompatible,
			KingdomGatehouseSlotEvidence Evidence)
		{
			if (Index < 0 || Index >= KingdomGatehouseTopology.SatelliteCount
				|| State != KingdomGatehouseSlotState.Empty)
				return KingdomGatehouseLegacyPublicationAction.Refuse;
			if (HasPublishedIdentity)
				return HasExactCarrier
					&& CanPublishLegacyPendingFromStagedIdentity(Index, State, true, Evidence)
					? KingdomGatehouseLegacyPublicationAction.PublishPending
					: KingdomGatehouseLegacyPublicationAction.Refuse;
			if (HasExactCarrier)
				return CanAdoptUnpublishedLegacyCustody(Index, State, false, true,
					BlueprintExact, Unplaced, BoundedIdentity, UniqueGlobalIdentity,
					ExistingMarksCompatible)
					? KingdomGatehouseLegacyPublicationAction.AdoptCustody
					: KingdomGatehouseLegacyPublicationAction.Refuse;
			return Evidence == KingdomGatehouseSlotEvidence.Absent
				? KingdomGatehouseLegacyPublicationAction.Create
				: KingdomGatehouseLegacyPublicationAction.Refuse;
		}

		/// <summary>
		/// Deterministic v2 may enter serialized custody only after its whole frozen body is
		/// complete.  A cut before custody therefore recreates the same derived identity.
		/// </summary>
		public static bool CanSerializeDeterministicCustody(bool PaletteExact,
			bool IdentityExact, bool MarksExact)
		{
			return PaletteExact && IdentityExact && MarksExact;
		}

		/// <summary>
		/// Vanilla Door synchronizes both display and tile from its Open flag.  The declared
		/// pair remains frozen form truth; only the live member of that pair may be rendered.
		/// </summary>
		public static bool ExactLiveDoorRender(bool Open, bool SyncRender,
			string LiveDisplay, string LiveTile,
			string DeclaredClosedDisplay, string DeclaredOpenDisplay,
			string DeclaredClosedTile, string DeclaredOpenTile,
			string FrozenClosedDisplay, string FrozenOpenDisplay,
			string FrozenClosedTile, string FrozenOpenTile)
		{
			if (!SyncRender || string.IsNullOrEmpty(FrozenClosedDisplay)
				|| string.IsNullOrEmpty(FrozenOpenDisplay)
				|| string.IsNullOrEmpty(FrozenClosedTile)
				|| string.IsNullOrEmpty(FrozenOpenTile)
				|| !string.Equals(DeclaredClosedDisplay, FrozenClosedDisplay,
					StringComparison.Ordinal)
				|| !string.Equals(DeclaredOpenDisplay, FrozenOpenDisplay,
					StringComparison.Ordinal)
				|| !string.Equals(DeclaredClosedTile, FrozenClosedTile,
					StringComparison.Ordinal)
				|| !string.Equals(DeclaredOpenTile, FrozenOpenTile,
					StringComparison.Ordinal)) return false;
			return string.Equals(LiveDisplay,
				Open ? FrozenOpenDisplay : FrozenClosedDisplay, StringComparison.Ordinal)
				&& string.Equals(LiveTile,
					Open ? FrozenOpenTile : FrozenClosedTile, StringComparison.Ordinal);
		}

		/// <summary>Pure callback-boundary authority for an uncommitted root envelope.</summary>
		public static bool ExactPendingEnvelope(bool HasSchemaInt, bool HasSchemaString,
			bool HasPlanInt, string FrozenPlan, string ExpectedPlan, bool FootprintExact)
		{
			return !HasSchemaInt && !HasSchemaString && !HasPlanInt && FootprintExact
				&& !string.IsNullOrEmpty(ExpectedPlan)
				&& string.Equals(FrozenPlan, ExpectedPlan, StringComparison.Ordinal);
		}

		public static KingdomGatehouseSlotAction Resolve(int Index,
			KingdomGatehouseSlotState State, bool HasIdentity,
			KingdomGatehouseSlotEvidence Evidence)
		{
			if (Index < 0 || Index >= KingdomGatehouseTopology.SatelliteCount)
				return KingdomGatehouseSlotAction.Refuse;
			if (State == KingdomGatehouseSlotState.Empty)
			{
				if (Evidence != KingdomGatehouseSlotEvidence.Absent)
					return KingdomGatehouseSlotAction.Refuse;
				return HasIdentity ? KingdomGatehouseSlotAction.PublishPending
					: KingdomGatehouseSlotAction.PublishIdentity;
			}
			if (!HasIdentity) return KingdomGatehouseSlotAction.Refuse;
			if (State == KingdomGatehouseSlotState.Pending)
			{
				if (Evidence == KingdomGatehouseSlotEvidence.Absent)
					return KingdomGatehouseSlotAction.Create;
				if (Evidence == KingdomGatehouseSlotEvidence.Staged)
					return KingdomGatehouseSlotAction.Place;
				if (Evidence == KingdomGatehouseSlotEvidence.ExactPlacement)
					return KingdomGatehouseSlotAction.Settle;
				return KingdomGatehouseSlotAction.Refuse;
			}
			return State == KingdomGatehouseSlotState.Settled
				&& Evidence == KingdomGatehouseSlotEvidence.ExactPlacement
					? KingdomGatehouseSlotAction.Verify : KingdomGatehouseSlotAction.Refuse;
		}

		/// <summary>Only a proved absent object may leave serialized reference custody.</summary>
		public static bool CanClearCustody(KingdomGatehouseSlotState State,
			bool HasIdentity, KingdomGatehouseSlotEvidence Evidence)
		{
			return State == KingdomGatehouseSlotState.Pending && HasIdentity
				&& Evidence == KingdomGatehouseSlotEvidence.Absent;
		}

		public static bool HasLiveCustody(KingdomGatehouseSlotEvidence Evidence)
		{
			return Evidence == KingdomGatehouseSlotEvidence.Staged
				|| Evidence == KingdomGatehouseSlotEvidence.ExactPlacement
				|| Evidence == KingdomGatehouseSlotEvidence.Foreign
				|| Evidence == KingdomGatehouseSlotEvidence.Duplicate;
		}
	}
}
