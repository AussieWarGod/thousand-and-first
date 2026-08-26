using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		private static bool PrepareCandidateArrivalOperation(KingdomSystem system, Zone zone,
			KingdomSurvey survey, KingdomGrowthArrivalCandidate candidate, GameObject settler,
			long tick)
		{
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, tick);
			if (operation == null) return false;
			operation.ArrivalDisposition = candidate.Disposition;
			operation.ArrivalCandidateId = candidate.Id;
			if (candidate.Disposition == KingdomGrowthArrivalDisposition.NoAcceptableHome)
			{
				KingdomLodgingRules.UnhousedReason reason =
					LodgingRefusalReason(candidate.RefusalReason);
				if (!system.NoRoomAnnounced && !AppendArrivalOutbox(system, operation,
					"lodging-refusal", KingdomLodgingRules.ArrivalRefusedChronicle(
						KingdomPresentation.Rich(system.KingdomDisplayName), reason), "{{r|"
						+ KingdomLodgingRules.ArrivalRefusedNote(reason) + "}}")) return false;
				return KingdomLifecycleRules.TryPublishGrowth(growth, operation);
			}
			if (candidate.Disposition != KingdomGrowthArrivalDisposition.Joined
				|| survey == null || !ExactCreatedCandidate(candidate, settler, zone))
				return false;
			operation.TargetId = candidate.ObjectId;
			operation.TargetMarker = candidate.Marker;
			operation.Blueprint = candidate.Blueprint;
			operation.ZoneId = candidate.LodgingZoneId;
			operation.TargetTopology = KingdomLifecycleTopology.Cell;
			operation.TargetLocation = KingdomGrowthLocationKind.Cell;
			operation.TargetOwnerId = null;
			operation.TargetX = candidate.LodgingX;
			operation.TargetY = candidate.LodgingY;
			operation.PopulationBefore = system.Population;
			operation.PopulationDelta = 1;
			operation.PopulationAfter = system.Population + 1;
			if (!PrepareArrivalWaterLegs(growth, operation, zone, survey,
				KingdomRules.DramsPerArrival)) return false;
			if (!PrepareArrivalDomainSteps(system, growth, operation, settler)) return false;
			string origin = settler.GetStringProperty(ArrivalOriginPlanProperty);
			string reasonText = KingdomRules.ArrivalReason(system.LastDeed,
				tick - system.LastDeedTick, origin);
			if (!AppendArrivalOutbox(system, operation, "joined",
				reasonText + ", and a settler came to "
					+ KingdomPresentation.Rich(system.KingdomDisplayName)
					+ " and drank of the shared water",
				"{{G|" + XRL.Language.Grammar.InitCap(reasonText)
					+ " - a settler has come.}}")) return false;
			return KingdomLifecycleRules.TryPublishGrowth(growth, operation);
		}

		private static bool PrepareArrivalPersonPlan(KingdomSystem system, GameObject settler,
			KingdomGrowthArrivalCandidate candidate)
		{
			if (!ExactFreshEscrowedCandidate(candidate, settler)
				&& !ExactEscrowedCandidate(candidate, settler) || candidate.LegacySemanticPlan
				|| settler.GetIntProperty("KingdomCitizen") != 0
				|| settler.GetPart<r_KingdomCitizenship>() != null)
				return false;
			return FreezePersonProperty(settler, ArrivalOriginPlanProperty,
				candidate.PlannedOrigin)
				&& FreezePersonProperty(settler, ArrivalCreedPlanProperty,
					candidate.PlannedCreed)
				&& FreezePersonProperty(settler, ArrivalNamePlanProperty,
					candidate.PlannedName)
				&& FreezePersonProperty(settler, ArrivalDatePlanProperty,
					candidate.PlannedArrived)
				&& FreezePersonProperty(settler, ArrivalCitizenshipPlanProperty,
					ArrivalCitizenshipPlanValue);
		}

		private static bool FreezePersonProperty(GameObject person, string property,
			string frozen)
		{
			if (!GameObject.Validate(person) || string.IsNullOrEmpty(property)
				|| string.IsNullOrEmpty(frozen)) return false;
			string current = person.GetStringProperty(property);
			if (string.IsNullOrEmpty(current)) person.SetStringProperty(property, frozen);
			return string.Equals(person.GetStringProperty(property), frozen,
				StringComparison.Ordinal);
		}

		private static bool TryMigrateArrivalSemanticPlan(KingdomSystem system, Zone zone,
			KingdomGrowthBook growth, KingdomGrowthArrivalCandidate candidate, long tick,
			out string failure)
		{
			failure = null;
			KingdomSemanticPersonPlan plan;
			if (!KingdomSemanticSelection.TryPrepareGrowthArrivalForFrozenBlueprint(system,
				zone, candidate.Sequence, candidate.CreatedTick, candidate.Blueprint,
				out plan, out failure)) return false;
			GameObject existing;
			if (TryExactArrivalRoot(candidate, out existing) && GameObject.Validate(existing))
			{
				string origin = existing.GetStringProperty(ArrivalOriginPlanProperty);
				string creed = existing.GetStringProperty(ArrivalCreedPlanProperty);
				string name = existing.GetStringProperty(ArrivalNamePlanProperty);
				string arrived = existing.GetStringProperty(ArrivalDatePlanProperty);
				if (!string.IsNullOrEmpty(origin)) plan.Origin = origin;
				if (!string.IsNullOrEmpty(creed)) plan.Creed = creed;
				if (!string.IsNullOrEmpty(name)) plan.Name = name;
				if (!string.IsNullOrEmpty(arrived)) plan.Arrived = arrived;
			}
			if (string.IsNullOrEmpty(plan.Creed)) plan.Creed = "-";
			return KingdomLifecycleRules.UpgradeLegacyGrowthArrivalSemanticPlan(growth,
				candidate, plan.RulesVersion, plan.StreamId, plan.EventKind, plan.Origin,
				plan.Creed, plan.Name, plan.Arrived, plan.X, plan.Y, tick)
				|| Fail("historical semantic payload publication refused", out failure);
		}

		private static bool Fail(string reason, out string failure)
		{
			failure = reason;
			return false;
		}

		private static bool PrepareArrivalWaterLegs(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, Zone zone, KingdomSurvey survey, int amount)
		{
			int remaining = amount;
			HashSet<LiquidVolume> seen = new HashSet<LiquidVolume>();
			for (int i = 0; i < survey.Stores.Count && remaining > 0; i++)
			{
				LiquidVolume vessel = survey.Stores[i];
				GameObject owner = vessel?.ParentObject;
				if (vessel == null || !seen.Add(vessel) || !GameObject.Validate(owner)
					|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
					|| owner.GetIntProperty("KingdomStores") != 1
					|| owner.CurrentCell == null || !ReferenceEquals(owner.CurrentZone, zone)
					|| !KingdomLiquids.HasFreshWater(vessel) || vessel.MaxVolume <= 0) continue;
				int take = Math.Min(remaining, vessel.Volume);
				int after = vessel.Volume - take;
				string beforeComposition = LiquidComposition(vessel, vessel.Volume);
				string afterComposition = LiquidComposition(vessel, after);
				KingdomGrowthWaterLeg leg = KingdomLifecycleRules.PrepareGrowthWaterLeg(
					growth, operation, KingdomGrowthWaterMutationKind.Drain, owner.ID,
					KingdomLifecycleTopology.Cell, null, owner.Blueprint, zone.ZoneID,
					owner.CurrentCell.X, owner.CurrentCell.Y, vessel.MaxVolume, vessel.Volume,
					take, beforeComposition, afterComposition,
					WaterOwnerHash(owner, vessel.Volume, beforeComposition),
					WaterOwnerHash(owner, after, afterComposition),
					WaterPartHash(owner, vessel.Volume, beforeComposition),
					WaterPartHash(owner, after, afterComposition),
					WaterTopologyHash(zone, owner, vessel.Volume),
					WaterTopologyHash(zone, owner, after));
				if (leg == null) return false;
				operation.WaterLegs.Add(leg);
				remaining -= take;
			}
			return remaining == 0 && operation.WaterLegs.Count > 0;
		}

		private static bool PrepareArrivalDomainSteps(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation, GameObject settler)
		{
			KingdomGrowthDomainStep enrollment = PreparePersonDomain(system, growth,
				operation, settler, KingdomGrowthDomainStepKind.Enrollment,
				KingdomGrowthDomainCallbackKind.Enroll, 0L, 1L);
			KingdomGrowthDomainStep roster = PreparePersonDomain(system, growth, operation,
				settler, KingdomGrowthDomainStepKind.Roster,
				KingdomGrowthDomainCallbackKind.RosterAdd, 0L, 1L);
			KingdomGrowthDomainStep creed = PreparePersonDomain(system, growth, operation,
				settler, KingdomGrowthDomainStepKind.Creed,
				KingdomGrowthDomainCallbackKind.CreedSet, 0L, 1L);
			KingdomGrowthDomainStep population = PreparePersonDomain(system, growth,
				operation, settler, KingdomGrowthDomainStepKind.Population,
				KingdomGrowthDomainCallbackKind.PopulationAdjust, system.Population,
				system.Population + 1L);
			KingdomGrowthAccountingSnapshot accountingBefore = AccountingSnapshot(system);
			KingdomGrowthAccountingSnapshot accountingAfter = AccountingSnapshot(system);
			accountingAfter.ArrivalCost += KingdomRules.DramsPerArrival;
			accountingAfter.Arrivals++;
			KingdomGrowthDomainStep accounting = KingdomLifecycleRules.PrepareGrowthDomainStep(
				growth, operation, KingdomGrowthDomainStepKind.Accounting,
				KingdomGrowthDomainCallbackKind.AccountingSet, growth.SettlementId,
				growth.SettlementId, operation.Sequence - 1L, operation.Sequence,
				ArrivalDomainBodyHash(system, operation, settler,
					KingdomGrowthDomainStepKind.Accounting),
				AccountingHash(system, false), AccountingHash(system, true),
				AccountingMapHash(system, false), AccountingMapHash(system, true),
				null, null, accountingBefore, accountingAfter);
			if (enrollment == null || roster == null || creed == null || population == null
				|| accounting == null) return false;
			operation.DomainSteps.Add(enrollment);
			operation.DomainSteps.Add(roster);
			operation.DomainSteps.Add(creed);
			operation.DomainSteps.Add(population);
			operation.DomainSteps.Add(accounting);
			return true;
		}

		private static KingdomGrowthDomainStep PreparePersonDomain(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation, GameObject settler,
			KingdomGrowthDomainStepKind kind, KingdomGrowthDomainCallbackKind callback,
			long before, long after)
		{
			return KingdomLifecycleRules.PrepareGrowthDomainStep(growth, operation, kind,
				callback, settler.ID, kind == KingdomGrowthDomainStepKind.Population
					? growth.SettlementId : settler.ID, before, after,
				ArrivalDomainBodyHash(system, operation, settler, kind),
				PersonDomainHash(system, settler, kind, false, operation.Id,
					frozenAppliedTick: operation.CreatedTick),
				PersonDomainHash(system, settler, kind, true, operation.Id,
					frozenAppliedTick: operation.CreatedTick),
				PersonDomainMapHash(system, settler, kind, false, operation.Id),
				PersonDomainMapHash(system, settler, kind, true, operation.Id));
		}
	}
}
