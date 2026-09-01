using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Read-only claims which must be closed before a resident changes identity or is
	/// destroyed. Runtime inspection projects exact physical and durable evidence onto these bits;
	/// this layer decides policy without consulting mutable engine state.</summary>
	[Flags]
	public enum KingdomResidentTransitionClaim : uint
	{
		None = 0,
		AuthorityUnproved = 1u << 0,
		NamedCook = 1u << 1,
		AssentingMoot = 1u << 2,
		PhysicalHappening = 1u << 3,
		OpenLodge = 1u << 4,
		Expedition = 1u << 5,
		BountyManning = 1u << 6,
		Keeper = 1u << 7,
		StasisCustody = 1u << 8,
		PreparedMarketHandoff = 1u << 9,
		CivicOffice = 1u << 10,
		CompletedLegendaryMarket = 1u << 11,
		MarketStock = 1u << 12,
		MarketTransfer = 1u << 13,
		NativeMerchantStock = 1u << 14,
		SuccessionAccessionOwner = 1u << 15,
		SuccessionProtectedResident = 1u << 16,
		LabRefusalDeparture = 1u << 17,
		CookDeparturePrepared = 1u << 18,
		OfficeDeparturePrepared = 1u << 19,
		PolityResidentBridge = 1u << 20,
		ResidentDeparture = 1u << 21
	}

	internal enum KingdomResidentDestructionAuthorizationKind : byte
	{
		None = 0,
		LabRefusalDeparture = 1
	}

	/// <summary>A capability frozen by the exact operation that is allowed to consume one
	/// otherwise protected resident. Callers cannot authorize a class of departures.</summary>
	internal readonly struct KingdomResidentDestructionAuthorization
	{
		internal readonly KingdomResidentDestructionAuthorizationKind Kind;
		internal readonly string EventId;
		internal readonly string OwnerObjectId;
		internal readonly string CauseDigest;

		internal KingdomResidentDestructionAuthorization(
			KingdomResidentDestructionAuthorizationKind kind, string eventId,
			string ownerObjectId, string causeDigest)
		{
			Kind = kind; EventId = eventId; OwnerObjectId = ownerObjectId;
			CauseDigest = causeDigest;
		}
	}

	internal readonly struct KingdomSuccessionResidentAuthority
	{
		internal readonly bool AccessionOwner;
		internal readonly bool RepairOwner;
		internal readonly string RepairSettlementId;
		internal readonly string RepairName;

		internal KingdomSuccessionResidentAuthority(bool accessionOwner, bool repairOwner,
			string repairSettlementId, string repairName)
		{
			AccessionOwner = accessionOwner; RepairOwner = repairOwner;
			RepairSettlementId = repairSettlementId; RepairName = repairName;
		}
	}

	internal static class KingdomResidentTransitionRules
	{
		private const KingdomResidentTransitionClaim AccessionBlockers =
			KingdomResidentTransitionClaim.AuthorityUnproved
			| KingdomResidentTransitionClaim.NamedCook
			| KingdomResidentTransitionClaim.AssentingMoot
			| KingdomResidentTransitionClaim.PhysicalHappening
			| KingdomResidentTransitionClaim.OpenLodge
			| KingdomResidentTransitionClaim.Expedition
			| KingdomResidentTransitionClaim.BountyManning
			| KingdomResidentTransitionClaim.Keeper
			| KingdomResidentTransitionClaim.StasisCustody
			| KingdomResidentTransitionClaim.PreparedMarketHandoff
			| KingdomResidentTransitionClaim.SuccessionProtectedResident
			| KingdomResidentTransitionClaim.LabRefusalDeparture
			| KingdomResidentTransitionClaim.ResidentDeparture;

		private const KingdomResidentTransitionClaim DestructionBlockers =
			AccessionBlockers
			| KingdomResidentTransitionClaim.SuccessionAccessionOwner
			| KingdomResidentTransitionClaim.CivicOffice
			| KingdomResidentTransitionClaim.CompletedLegendaryMarket
			| KingdomResidentTransitionClaim.MarketStock
			| KingdomResidentTransitionClaim.MarketTransfer
			| KingdomResidentTransitionClaim.NativeMerchantStock
			| KingdomResidentTransitionClaim.PolityResidentBridge;

		/// <summary>Office and completed legendary-market roles have exact accession closures.
		/// Every other claim must retire before body control can cross.</summary>
		internal static bool CanAccede(KingdomResidentTransitionClaim Claims)
		{
			if ((Claims & KingdomResidentTransitionClaim.SuccessionAccessionOwner) != 0)
				Claims &= ~KingdomResidentTransitionClaim.SuccessionProtectedResident;
			KingdomResidentTransitionClaim owners = Claims
				& (KingdomResidentTransitionClaim.CivicOffice
					| KingdomResidentTransitionClaim.CompletedLegendaryMarket);
			return (Claims & AccessionBlockers) == 0
				&& owners != (KingdomResidentTransitionClaim.CivicOffice
					| KingdomResidentTransitionClaim.CompletedLegendaryMarket);
		}

		/// <summary>Generic emigration obliterates its body. No live civic, custody, transfer, or
		/// nested stock claim may survive that boundary.</summary>
		internal static bool CanDestroy(KingdomResidentTransitionClaim Claims,
			bool ExactLabAuthorization = false)
		{
			if (ExactLabAuthorization)
				Claims &= ~KingdomResidentTransitionClaim.LabRefusalDeparture;
			if ((Claims & KingdomResidentTransitionClaim.CookDeparturePrepared) != 0)
				Claims &= ~KingdomResidentTransitionClaim.NamedCook;
			if ((Claims & KingdomResidentTransitionClaim.OfficeDeparturePrepared) != 0)
				Claims &= ~KingdomResidentTransitionClaim.CivicOffice;
			return (Claims & DestructionBlockers) == 0;
		}

		/// <summary>Read-only preflight for the departure owner. Cook and office claims may be
		/// staged only after every unrelated destructive blocker has been excluded.</summary>
		internal static bool CanPrepareDestroy(KingdomResidentTransitionClaim Claims,
			bool ExactLabAuthorization = false)
		{
			Claims &= ~(KingdomResidentTransitionClaim.NamedCook
				| KingdomResidentTransitionClaim.CivicOffice
				| KingdomResidentTransitionClaim.PolityResidentBridge);
			return CanDestroy(Claims, ExactLabAuthorization);
		}

		internal static bool ExactCarrierMultiplicity(int ResidentRows,
			int ResidentBindings, bool AccessionRepair)
		{
			return ResidentRows >= 0 && ResidentRows <= 1
				&& ResidentBindings >= 0 && ResidentBindings <= 1
				&& (AccessionRepair || ResidentRows == 1 && ResidentBindings == 1);
		}
	}
}
