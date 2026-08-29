using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Executable teardown registry. A source gate compares this frozen list with every production
	/// custom part/system and rejects additions without an explicit removal disposition.
	/// </summary>
	public static partial class KingdomRemovalCoverage
	{
		public static readonly string[] CustomSystems = new string[]
		{
			"KingdomCivicMemorySystem", "KingdomInheritanceLifecycle", "KingdomSeal",
			"KingdomSuccession", "KingdomSystem"
		};

		/// <summary>Every direct or inherited IPart carrier emitted by production source.</summary>
		public static readonly string[] CustomParts = new string[]
		{
			"KingdomCharterPart", "r_FounderBasin", "r_KingdomArcology",
			"r_KingdomArcologyZoneAnchor", "r_KingdomAssentingMoot",
			"r_KingdomAssentingMootMember", "r_KingdomBecomingAnnexe",
			"r_KingdomButcherSlab", "r_KingdomCarrySign", "r_KingdomChimericTheatre",
			"r_KingdomCitizenLegacy", "r_KingdomCitizenship", "r_KingdomClearance",
			"r_KingdomCrownHall", "r_KingdomEnrolled", "r_KingdomFirstGuestBody",
			"r_KingdomFounderKnowledge",
			"r_KingdomFounderRemains", "r_KingdomFounderShrine", "r_KingdomGatehouse",
			"r_KingdomGatehouseProjectionV1Pending", "r_KingdomGatehouseProjectionV2",
			"r_KingdomGraftingHall", "r_KingdomGuest", "r_KingdomHandCrankedVisual",
			"r_KingdomImprovement",
			"r_KingdomInheritedFabric", "r_KingdomInquiry", "r_KingdomLabCivicFriction",
			"r_KingdomLabEffectLedger", "r_KingdomLabJob", "r_KingdomLabRecord", "r_KingdomLabRemovalJob",
			"r_KingdomLiquidConduit", "r_KingdomLiquidCrossover", "r_KingdomLiquidTap",
			"r_KingdomLocusAmbient", "r_KingdomMirrorGate", "r_KingdomNamedCook",
			"r_KingdomNotableGuest", "r_KingdomNotice",
			"r_KingdomOfficeProjection", "r_KingdomPactVessel", "r_KingdomPlanMarker",
			"r_KingdomPlot", "r_KingdomPlotWorks",
			"r_KingdomPolityCohortBody", "r_KingdomPolityEscrow", "r_KingdomPorter", "r_KingdomPowerStore",
			"r_KingdomPowerWork", "r_KingdomProperty", "r_KingdomPurposeWork",
			"r_KingdomRaidDemand", "r_KingdomRaiderObjective", "r_KingdomRegistryOffice",
			"r_KingdomRelocationFrame", "r_KingdomRemembranceProjection", "r_KingdomScaffold", "r_KingdomSeed",
			"r_KingdomSocket", "r_KingdomStasisCustody", "r_KingdomStasisFieldAnchor",
			"r_KingdomStasisProjection", "r_KingdomStasisVault", "r_KingdomStation",
			"r_KingdomVatHouse", "r_KingdomVisualState", "r_KingdomWear",
			"r_KingdomWildSeed", "r_KingdomWitnessWorkProjection", "r_KingdomYardTrade",
			"r_KingdomYielding"
		};

		public static readonly string[] CustomZoneParts = new string[]
		{
			"KingdomAssentingWardAuthority"
		};

		public static readonly string[] CustomGameStateSingletons = new string[]
		{
			"KingdomInheritanceState"
		};

		public static readonly string[] CustomCookingRecipes = new string[]
		{
			"r_KingdomFavoredDish"
		};

		public static readonly string[] CustomJournalNotes = new string[]
		{
			"r_KingdomFounderHistoryNote"
		};

		/// <summary>Vanilla carrier with a TAF lifecycle marker; never a custom quest type.</summary>
		public static readonly string[] ProjectedQuestKinds = new string[]
		{
			"TAF:raid-recovery"
		};

		public static readonly string[] AbilityCommands = new string[]
		{
			"TAFReadFounderKnowledge", "r_AnswerPolityVisit",
			"r_KingdomCharterMenu", "r_SetKingdomResearchSubject"
		};

		/// <summary>Exact TAF-owned global keys. The identity fence is deliberately absent.</summary>
		public static readonly string[] OwnedGlobalStates = new string[]
		{
			"$ThousandAndFirst_ConstructionInputLostAuthorityCursor",
			"$ThousandAndFirst_ConstructionInputObservations",
			"r_TAF_AnnexeChromeSpoken", "r_TAF_ChronicleEventRegistryFault_v3",
			"r_TAF_ChronicleEventRegistry_v1", "r_TAF_CityBrinkStanding",
			"r_TAF_CityBrinkWarned", "r_TAF_ConstructionJobs", "r_TAF_Crown",
			"r_TAF_FounderRites", "r_TAF_FoundingGlobalReservation_v1",
			"r_TAF_HostedArcologyAuthorityV1:0", "r_TAF_HostedArcologyAuthorityV1:1",
			"r_TAF_ImprovementNoticed", "r_TAF_Inheritance", "r_TAF_KeepersRoster",
			"r_TAF_KingdomMode",
			"r_TAF_LabCivicOwners_v1", "r_TAF_LabJobRegistry_v1", "r_TAF_LabReplayProof_v1",
			"r_TAF_MirrorGates", "r_TAF_NextPlanOrder", "r_TAF_PurposePortfolioPair",
			"r_TAF_SaveSystemRoster_v1"
		};

		/// <summary>Only key grammars whose namespace and suffix are authored solely by TAF.</summary>
		public static readonly string[] OwnedGlobalStatePrefixes = new string[]
		{
			"$ThousandAndFirst_ConstructionInputRetirement_",
			"$ThousandAndFirst_ConstructionInputTransit_",
			"r_TAF_BountyManningOption_v1:", "r_TAF_Crown_", "r_TAF_DelveLink:", "r_TAF_Delved:",
			"r_TAF_FaithGlobalOption_v1:", "r_TAF_FoundingHeartRoot:",
			"r_TAF_GrowthArrivalEscrow:",
			"r_TAF_ImprovementGrowthEscrow:", "r_TAF_ImprovementHeld:",
			"r_TAF_ImprovementItemEscrow:", "r_TAF_MirrorGate_",
			"r_TAF_PurposeCargoEscrow:", "r_TAF_PurposePairCargo:",
			"r_TAF_ReachCity_", "r_TAF_ReachRealm_",
			"r_TAF_RelocationEscrow:", "r_TAF_ResearchSeedSources:",
			"r_TAF_RoadsGlobalOption_v1:", "r_TAF_SubsidenceOption_v1:"
		};

		/// <summary>Fixed slots shared by current and prior realms; clear only an exact match.</summary>
		public static readonly string[] HostedArcologyAuthorityStates = new string[]
		{
			"r_TAF_HostedArcologyAuthorityV1:0", "r_TAF_HostedArcologyAuthorityV1:1"
		};

		/// <summary>Exact zone properties written by TAF. Shared base keys are absent.</summary>
		public static readonly string[] OwnedZoneProperties = new string[]
		{
			"ThousandAndFirst.Inherit.Application", "r_TAF_ClaimChronicleDisposition_v1",
			"r_TAF_ClaimChronicleEvent_v1", "r_TAF_ClaimChronicleStage_v1",
			"r_TAF_ClaimWasFounding_v1", "r_TAF_ExternalOwnerBindingAuthority_v1",
			"r_TAF_ExternalOwnerBinding_v1", "r_TAF_ExternalOwnerContestedTold_v1",
			"r_TAF_ExternalOwnerContested_v1", "r_TAF_ExternalOwnerStageAuthority_v1",
			"r_TAF_ExternalOwnerStage_v1", "r_TAF_FaithOptionOwner_v1",
			"r_TAF_FaithOption_v1", "r_TAF_FoundingHeartReceipt",
			"r_TAF_FoundingSiteAuthority_v1",
			"r_TAF_FoundingSiteDisplay_v1", "r_TAF_FoundingSiteName_v1",
			"r_TAF_FoundingSiteTick_v1", "r_TAF_FoundingSiteVillage_v1",
			"r_TAF_FoundingSiteVocation_v1", "r_TAF_HeartRelocationFault",
			"r_TAF_HeartRelocationLast", "r_TAF_HeartRelocationReceipt",
			"r_TAF_HeartRung", "r_TAF_HeartSurveyX1", "r_TAF_HeartSurveyX2",
			"r_TAF_HeartSurveyY1", "r_TAF_HeartSurveyY2", "r_TAF_RiteX", "r_TAF_RiteY",
			"r_TAF_Roads", "r_TAF_RoadsFull", "r_TAF_RoadsOptionOwner_v1",
			"r_TAF_RoadsOption_v1", "r_TAF_RoadsSaid", "r_TAF_RoadsWalked",
			"r_TAF_RuinRestorationTransaction_v1", "r_TAF_SecondFoundingChronicle",
			"r_TAF_SecondFoundingChronicleDisposition_v1",
			"r_TAF_SecondFoundingChronicleStage",
			"r_TAF_SecondFoundingIdentityOrigin_v1",
			"r_TAF_SecondFoundingIdentityRealm_v1",
			"r_TAF_SecondFoundingIdentitySettlement_v1",
			"r_TAF_SecondFoundingIdentityTransaction_v1",
			"r_TAF_SecondFoundingIdentityVersion_v1",
			"r_TAF_SecondFoundingPublicationAuthority_v1",
			"r_TAF_SecondFoundingRecoveryName", "r_TAF_SecondFoundingRecoveryRealm",
			"r_TAF_SecondFoundingRecoveryRiteX", "r_TAF_SecondFoundingRecoveryRiteY",
			"r_TAF_SecondFoundingRecoveryTick", "r_TAF_SecondFoundingRecoveryTransaction",
			"r_TAF_SecondFoundingRecoveryVocation", "r_TAF_SecondFoundingRestored_v1"
		};

		/// <summary>Bounded dynamic object-property grammars with TAF-only owners.</summary>
		public static readonly string[] OwnedObjectPropertyPrefixes = new string[]
		{
			"r_TAF_FoundingReceipt_", "r_TAF_ImprovementHandover:",
			"r_TAF_LabOwner::", "r_TAF_LabOwnerNonce::",
			"r_TAF_LabPending::", "r_TAF_LayoutOutputId_", "r_TAF_LayoutOutputState_"
		};

		/// <summary>Shared vanilla state TAF may observe or once wrote but may not guess-restore.</summary>
		public static readonly string[] PreserveSharedFields = new string[]
		{
			"faction", "VillageMerchant", "InventoryTier", "SuppressPowerSwitchTwiddle",
			"Calm", "Hostile", "PartyLeader", "ConversationScript", "JournalHistory"
		};

		public static bool IsOwnedBlueprint(string Name)
		{
			return Contains(OwnedBlueprints, Name);
		}

		public static bool IsCustomPart(string Name)
		{
			return Contains(CustomParts, Name);
		}

		public static bool IsCustomSystem(string Name)
		{
			return Contains(CustomSystems, Name);
		}

		public static bool IsCustomZonePart(string Name)
		{
			return Contains(CustomZoneParts, Name);
		}

		public static bool IsCustomGameStateSingleton(string Name)
		{
			return Contains(CustomGameStateSingletons, Name);
		}

		public static bool IsCustomCookingRecipe(string Name)
		{
			return Contains(CustomCookingRecipes, Name);
		}

		public static bool IsCustomJournalNote(string Name)
		{
			return Contains(CustomJournalNotes, Name);
		}

		public static bool IsOwnedObjectProperty(string Name)
		{
			return Contains(OwnedObjectProperties, Name)
				|| StartsWithAny(Name, OwnedObjectPropertyPrefixes);
		}

		public static bool IsOwnedZoneProperty(string Name)
		{
			return Contains(OwnedZoneProperties, Name);
		}

		public static bool IsOwnedGlobalState(string Name)
		{
			return Contains(OwnedGlobalStates, Name)
				|| StartsWithAny(Name, OwnedGlobalStatePrefixes);
		}

		private static bool StartsWithAny(string Value, string[] Prefixes)
		{
			if (string.IsNullOrEmpty(Value)) return false;
			for (int i = 0; i < Prefixes.Length; i++)
				if (Value.StartsWith(Prefixes[i], StringComparison.Ordinal)) return true;
			return false;
		}

		private static bool Contains(string[] Values, string Value)
		{
			for (int i = 0; i < Values.Length; i++)
				if (Values[i] == Value) return true;
			return false;
		}
	}
}
