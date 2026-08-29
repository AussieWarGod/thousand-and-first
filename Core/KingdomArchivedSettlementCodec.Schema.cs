using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomArchivedSettlementCodec
	{
		private static bool IsList(Type Type)
		{
			return Type.IsGenericType && Type.GetGenericTypeDefinition() == typeof(List<>);
		}

		private static bool IsDictionary(Type Type)
		{
			return Type.IsGenericType && Type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
		}

		private static bool CanonicalDictionaryComparer(Type Type, IDictionary Value)
		{
			if (Value == null || !IsDictionary(Type)) return false;
			Type[] arguments = Type.GetGenericArguments();
			if (arguments[0] != typeof(string)) return false;
			PropertyInfo property = Type.GetProperty("Comparer", BindingFlags.Instance |
				BindingFlags.Public);
			if (property == null) return false;
			object comparer = property.GetValue(Value, null);
			return ReferenceEquals(comparer, EqualityComparer<string>.Default)
				|| ReferenceEquals(comparer, StringComparer.Ordinal);
		}

		private static bool Approved(Type Type)
		{
			// KingdomCarryHaul lives in the engine-coupled Guestbook file, which the pure test
			// project intentionally omits. Runtime reference scans still admit that exact type name.
			if (Type != null && Type.FullName == "ThousandAndFirst.KingdomCarryHaul") return true;
			for (int i = 0; i < ApprovedObjects.Length; i++)
				if (ApprovedObjects[i] == Type) return true;
			return false;
		}

		private static FieldInfo[] Fields(Type Type)
		{
			return Fields(Type, CurrentVersion);
		}

		private static FieldInfo[] Fields(Type Type, int SchemaVersion)
		{
			FieldInfo[] source = Type.GetFields(BindingFlags.Instance | BindingFlags.Public);
			List<FieldInfo> fields = new List<FieldInfo>(source.Length);
			for (int i = 0; i < source.Length; i++)
				if (!source[i].IsDefined(typeof(NonSerializedAttribute), false)
					&& SchemaField(Type, source[i].Name, SchemaVersion))
					fields.Add(source[i]);
			fields.Sort(delegate(FieldInfo Left, FieldInfo Right)
			{
				return string.CompareOrdinal(Left.Name, Right.Name);
			});
			return fields.ToArray();
		}

		/// <summary>Archive v1 predates nested Growth; v2 predates RaidLedger; v3 predates
		/// resident culture/species tallies; v4 predates extension-identity tallies; v5 predates
		/// causal pilgrims and expeditions; v6 predates the behaviour sidecar; v7 predates physical
		/// happenings; v8 predates exact central logistics; v9 predates exact defensive
		/// WorkId/resident reservations; v10 predates frozen semantic person plans; v11 predates
		/// independent extension-happening cursors; v12 predates the expanded construction-delivery
		/// authority and phase domain; v13 predates city-local cook and moot authority; v14 predates
		/// first-guest correspondence authority; v15 predates physical first-guest evidence; v16
		/// predates fixed-rate arrival cadence authority. Historical readers retain exactly those surfaces
		/// rather than interpreting new default fields.</summary>
		private static bool SchemaField(Type Type, string Name, int SchemaVersion)
		{
			if (Type == typeof(KingdomLifecycleBook))
			{
				if (SchemaVersion == LegacyVersion
					&& string.Equals(Name, "Growth", StringComparison.Ordinal)) return false;
				if (SchemaVersion < RaidVersion
					&& string.Equals(Name, "RaidLedger", StringComparison.Ordinal)) return false;
			}
			if (Type == typeof(KingdomSettlement))
			{
				if (SchemaVersion < ResidentIdentityVersion
					&& (string.Equals(Name, "CultureCounts", StringComparison.Ordinal)
						|| string.Equals(Name, "SpeciesCounts", StringComparison.Ordinal))) return false;
				if (SchemaVersion < ExtensionIdentityVersion
					&& string.Equals(Name, "IdentityCounts", StringComparison.Ordinal)) return false;
				if (SchemaVersion < SemanticSelectionVersion
					&& string.Equals(Name, "OfficeHolderResidentId", StringComparison.Ordinal)) return false;
			}
			if (SchemaVersion < SalvageVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& (string.Equals(Name, "PilgrimCause", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimCauseTick", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimGreeted", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimLoudness", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimName", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimObjectId", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimPlaceName", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimSequence", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimState", StringComparison.Ordinal))) return false;
			if (SchemaVersion < SalvageVersion
				&& Type == typeof(Simulation.City.KingdomJobRegistry)
				&& (string.Equals(Name, "SubjectIds", StringComparison.Ordinal)
					|| string.Equals(Name, "SubjectNames", StringComparison.Ordinal)
					|| string.Equals(Name, "TargetNames", StringComparison.Ordinal)
					|| string.Equals(Name, "DueTicks", StringComparison.Ordinal)
					|| string.Equals(Name, "WaterCosts", StringComparison.Ordinal)
					|| string.Equals(Name, "ProvisionCosts", StringComparison.Ordinal)
					|| string.Equals(Name, "OutcomeCodes", StringComparison.Ordinal))) return false;
			if (SchemaVersion < SalvageVersion && Type == typeof(KingdomLedger)
				&& string.Equals(Name, "ExpeditionLines", StringComparison.Ordinal)) return false;
			if (SchemaVersion < BehaviourVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& string.Equals(Name, "ExtensionModel", StringComparison.Ordinal)) return false;
			if (SchemaVersion < PhysicalHappeningVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& string.Equals(Name, "HappeningModel", StringComparison.Ordinal)) return false;
			if (SchemaVersion < HappeningCursorVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& string.Equals(Name, "ExtensionHappeningCursors", StringComparison.Ordinal)) return false;
			if (SchemaVersion < CivicAuthorityVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& (string.Equals(Name, "NamedCook", StringComparison.Ordinal)
					|| string.Equals(Name, "AssentingMoot", StringComparison.Ordinal))) return false;
			if (SchemaVersion < PhysicalHappeningVersion && Type == typeof(KingdomRaidLedger)
				&& string.Equals(Name, "OpaqueFuturePayload", StringComparison.Ordinal)) return false;
			if (SchemaVersion < PhysicalHappeningVersion && Type == typeof(KingdomRaidIncident)
				&& (string.Equals(Name, "AttackOperationId", StringComparison.Ordinal)
					|| string.Equals(Name, "ChannelRevision", StringComparison.Ordinal)
					|| string.Equals(Name, "ChannelState", StringComparison.Ordinal)
					|| string.Equals(Name, "DemandChannelId", StringComparison.Ordinal)
					|| string.Equals(Name, "DemandLeadTicks", StringComparison.Ordinal)
					|| string.Equals(Name, "DemandObjectId", StringComparison.Ordinal)
					|| string.Equals(Name, "FortifyOrderedTick", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryNotice", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryOpenedTick", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryQuestId", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryResolvedTick", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryState", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryStepId", StringComparison.Ordinal)
					|| string.Equals(Name, "RemainingLeadTicks", StringComparison.Ordinal))) return false;
			if (SchemaVersion < DefensiveReservationVersion
				&& Type == typeof(KingdomRaidIncident)
				&& (string.Equals(Name, "DefenceReservationVersion", StringComparison.Ordinal)
					|| string.Equals(Name, "DefenceReservations", StringComparison.Ordinal))) return false;
			if (SchemaVersion < SemanticSelectionVersion
				&& Type == typeof(KingdomGrowthArrivalCandidate)
				&& (string.Equals(Name, "LegacySemanticPlan", StringComparison.Ordinal)
					|| string.Equals(Name, "SemanticPlanVersion", StringComparison.Ordinal)
					|| string.Equals(Name, "SemanticStreamId", StringComparison.Ordinal)
					|| string.Equals(Name, "SemanticEventKind", StringComparison.Ordinal)
					|| string.Equals(Name, "PlannedOrigin", StringComparison.Ordinal)
					|| string.Equals(Name, "PlannedCreed", StringComparison.Ordinal)
					|| string.Equals(Name, "PlannedName", StringComparison.Ordinal)
					|| string.Equals(Name, "PlannedArrived", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalX", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalY", StringComparison.Ordinal))) return false;
			if (SchemaVersion < FirstGuestVersion
				&& Type == typeof(KingdomGrowthArrivalCandidate)
				&& (string.Equals(Name, "LegacyAutomaticRecovery", StringComparison.Ordinal)
					|| string.Equals(Name, "FirstGuest", StringComparison.Ordinal))) return false;
			if (SchemaVersion < FirstGuestVersion
				&& Type == typeof(KingdomGrowthBook)
				&& string.Equals(Name, "FirstGuestTerminal", StringComparison.Ordinal)) return false;
			if (SchemaVersion < PhysicalFirstGuestVersion
				&& Type == typeof(KingdomGrowthFirstGuestOpportunity)
				&& (string.Equals(Name, "GuestPhase", StringComparison.Ordinal)
					|| string.Equals(Name, "GuestTerminalState", StringComparison.Ordinal)
					|| string.Equals(Name, "GuestActionTick", StringComparison.Ordinal)
					|| string.Equals(Name, "GuestActionReceiptId", StringComparison.Ordinal)
					|| string.Equals(Name, "GuestTerminalTick", StringComparison.Ordinal)
					|| string.Equals(Name, "GuestTerminalReceiptId", StringComparison.Ordinal)))
				return false;
			if (SchemaVersion < ArrivalCadenceVersion && Type == typeof(KingdomGrowthBook)
				&& (string.Equals(Name, "ArrivalEventStreamId", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalRulesVersion", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalRateEpoch", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalRateEpochStartedTick", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalProcessedThroughTick", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalCadenceNextDueTick", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalRateCohort", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalOrdinalHighWater", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalOrdinalRetiredThrough", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalCadenceMigrationPending", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalCadenceResumePending", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalOpportunity", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalDebtRanges", StringComparison.Ordinal))) return false;
			if (SchemaVersion < ArrivalCadenceVersion
				&& (Type == typeof(KingdomGrowthArrivalCandidate)
					|| Type == typeof(KingdomGrowthOperation))
				&& (string.Equals(Name, "ArrivalOpportunityOrdinal", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalOpportunityDueTick", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalOpportunityRateEpoch", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalOpportunityPayloadHash", StringComparison.Ordinal)))
				return false;
			if (SchemaVersion < SemanticSelectionVersion
				&& Type == typeof(KingdomRaidDefenceReservation)
				&& string.Equals(Name, "CrewSemanticIds", StringComparison.Ordinal)) return false;
			if (SchemaVersion < SemanticSelectionVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& (string.Equals(Name, "ResidentOrigins", StringComparison.Ordinal)
					|| string.Equals(Name, "ResidentArrived", StringComparison.Ordinal))) return false;
			if (SchemaVersion < ExactLogisticsVersion
				&& Type == typeof(Simulation.City.KingdomJobRegistry)
				&& (string.Equals(Name, "DeliverySourceEndpointIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliverySourceObjectIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliverySourceXs", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliverySourceYs", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetEndpointIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetObjectIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetXs", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetYs", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliverySourceBeforeAmounts", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTripIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryStopOrdinals", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryPhases", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryCargoAuthorityKinds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryOwnerOperationIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryOwnerManifestVersions", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryOwnerManifestDigests", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryOwnerManifestRevisions", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryManifestSourceStarts", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryManifestSourceCounts", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetBeforeAmounts", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetReceiptStates", StringComparison.Ordinal))) return false;
			return true;
		}

		private static bool HistoricalPhysicalFirstGuestOpportunity(
			KingdomGrowthFirstGuestOpportunity Value, int SchemaVersion)
		{
			return SchemaVersion >= PhysicalFirstGuestVersion || Value == null
				|| Value.RulesVersion == 1
					&& Value.GuestPhase == KingdomGrowthFirstGuestGuestPhase.None
					&& Value.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.None
					&& Value.GuestActionTick == -1L && Value.GuestActionReceiptId == null
					&& Value.GuestTerminalTick == -1L && Value.GuestTerminalReceiptId == null;
		}

		/// <summary>Versions v9-v12 carry these integer columns, but only v13 may interpret the
		/// append-only construction authority and landed phase. Validate the paired column domain at
		/// the reflected object boundary because their declared type is <c>List&lt;int&gt;</c>.</summary>
		private static bool ValidDeliveryDomain(
			Simulation.City.KingdomJobRegistry Value, int SchemaVersion)
		{
			if (Value == null || Value.JobIds == null || Value.DeliveryPhases == null
				|| Value.DeliveryCargoAuthorityKinds == null
				|| Value.DeliveryPhases.Count != Value.JobIds.Count
				|| Value.DeliveryCargoAuthorityKinds.Count != Value.JobIds.Count) return false;
			int maximumAuthority = SchemaVersion < DeliveryDomainVersion
				? (int)Simulation.City.KingdomDeliveryCargoAuthority.CarryBookManifest
				: (int)Simulation.City.KingdomDeliveryCargoAuthority.ConstructionInput;
			int maximumPhase = SchemaVersion < DeliveryDomainVersion
				? (int)Simulation.City.KingdomDeliveryPhase.Quarantined
				: (int)Simulation.City.KingdomDeliveryPhase.LandedAwaitingOwner;
			for (int i = 0; i < Value.JobIds.Count; i++)
				if (Value.DeliveryCargoAuthorityKinds[i] < 0
					|| Value.DeliveryCargoAuthorityKinds[i] > maximumAuthority
					|| Value.DeliveryPhases[i] < 0
					|| Value.DeliveryPhases[i] > maximumPhase) return false;
			return true;
		}

		private static bool ValidCivicAuthority(
			Simulation.City.KingdomCityBook Value)
		{
			return Value != null && Value.NamedCook != null && Value.AssentingMoot != null
				&& KingdomNamedCookRules.Validate(Value.NamedCook, out string _)
				&& KingdomAssentingMootRules.Validate(Value.AssentingMoot, out string _);
		}

#if TAF_TESTS
		internal static bool ValidDeliveryDomainForTests(
			Simulation.City.KingdomJobRegistry Value, int SchemaVersion)
		{
			return ValidDeliveryDomain(Value, SchemaVersion);
		}
#endif

	}
}
