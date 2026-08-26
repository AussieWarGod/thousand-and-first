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

		private static int CountArrivalMarker(Zone zone, string marker)
		{
			if (zone == null || string.IsNullOrEmpty(marker)) return -1;
			int count = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
				if (item.GetStringProperty(ArrivalMarkerProperty) == marker) count++;
			return count;
		}

		private static bool ExactWaterEndpoint(Zone zone, GameObject owner,
			LiquidVolume vessel, KingdomGrowthWaterLeg leg, int volume)
		{
			if (zone == null || !GameObject.Validate(owner) || vessel == null
				|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
				|| !ReferenceEquals(owner.CurrentZone, zone) || owner.CurrentCell == null
				|| owner.ID != leg.ContainerId || owner.Blueprint != leg.Blueprint
				|| owner.CurrentCell.X != leg.X || owner.CurrentCell.Y != leg.Y
				|| owner.GetIntProperty("KingdomStores") != 1 || vessel.MaxVolume != leg.Capacity
				|| vessel.Volume != volume) return false;
			string composition = LiquidComposition(vessel, volume);
			bool before = volume == leg.Before;
			return composition == (before ? leg.BeforeComposition : leg.AfterComposition)
				&& WaterOwnerHash(owner, volume, composition) == (before
					? leg.BeforeOwnerGraphHash : leg.AfterOwnerGraphHash)
				&& WaterPartHash(owner, volume, composition) == (before
					? leg.BeforePartGraphHash : leg.AfterPartGraphHash)
				&& WaterTopologyHash(zone, owner, volume) == (before
					? leg.BeforeTopologyHash : leg.AfterTopologyHash);
		}

		private static KingdomGrowthArrivalRefusalReason ArrivalRefusalReason(
			KingdomLodgingRules.UnhousedReason reason)
		{
			switch (reason)
			{
			case KingdomLodgingRules.UnhousedReason.NoRoofAtAll:
				return KingdomGrowthArrivalRefusalReason.NoRoofAtAll;
			case KingdomLodgingRules.UnhousedReason.NeedsUnmet:
				return KingdomGrowthArrivalRefusalReason.NeedsUnmet;
			case KingdomLodgingRules.UnhousedReason.Full:
				return KingdomGrowthArrivalRefusalReason.Full;
			case KingdomLodgingRules.UnhousedReason.Refused:
				return KingdomGrowthArrivalRefusalReason.Refused;
			case KingdomLodgingRules.UnhousedReason.Condemned:
				return KingdomGrowthArrivalRefusalReason.Condemned;
			default: return KingdomGrowthArrivalRefusalReason.Refused;
			}
		}

		private static KingdomLodgingRules.UnhousedReason LodgingRefusalReason(
			KingdomGrowthArrivalRefusalReason reason)
		{
			switch (reason)
			{
			case KingdomGrowthArrivalRefusalReason.NoRoofAtAll:
				return KingdomLodgingRules.UnhousedReason.NoRoofAtAll;
			case KingdomGrowthArrivalRefusalReason.NeedsUnmet:
				return KingdomLodgingRules.UnhousedReason.NeedsUnmet;
			case KingdomGrowthArrivalRefusalReason.Full:
				return KingdomLodgingRules.UnhousedReason.Full;
			case KingdomGrowthArrivalRefusalReason.Condemned:
				return KingdomLodgingRules.UnhousedReason.Condemned;
			default: return KingdomLodgingRules.UnhousedReason.Refused;
			}
		}

		private static ArrivalResult CandidateResult(KingdomGrowthArrivalCandidate candidate)
		{
			return candidate.Disposition == KingdomGrowthArrivalDisposition.Joined
				? ArrivalResult.Joined : ArrivalResult.Refused;
		}

		private static ArrivalResult OperationResult(KingdomGrowthArrivalDisposition disposition)
		{
			switch (disposition)
			{
			case KingdomGrowthArrivalDisposition.Joined: return ArrivalResult.Joined;
			case KingdomGrowthArrivalDisposition.NoAcceptableHome: return ArrivalResult.Refused;
			case KingdomGrowthArrivalDisposition.WaterUnavailable: return ArrivalResult.WaterUnavailable;
			case KingdomGrowthArrivalDisposition.NoGround: return ArrivalResult.NoGround;
			case KingdomGrowthArrivalDisposition.PopulationCap: return ArrivalResult.PopulationCap;
			case KingdomGrowthArrivalDisposition.SupportCap: return ArrivalResult.SupportCap;
			default: return ArrivalResult.Failed;
			}
		}

		private static ArrivalResult CandidateFault(KingdomGrowthBook growth,
			KingdomGrowthArrivalCandidate candidate, string fault)
		{
			string safe = BoundedFault(fault);
			bool quarantined = KingdomLifecycleRules.QuarantineGrowthArrivalCandidate(
				growth, candidate, safe);
			KingdomLog.Log("growth arrival candidate " + (quarantined ? "quarantined: "
				: "stopped with retained evidence: ") + safe);
			return ArrivalResult.Failed;
		}

		private static ArrivalResult OperationFault(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, string fault)
		{
			string safe = BoundedFault(fault);
			bool quarantined = KingdomLifecycleRules.QuarantineGrowthOperation(growth,
				operation, safe);
			KingdomLog.Log("growth arrival operation " + (quarantined ? "quarantined: "
				: "stopped with retained evidence: ") + safe);
			return ArrivalResult.Failed;
		}

		private static ArrivalResult QuarantineArrival(KingdomGrowthBook growth, string fault)
		{
			if (growth?.ArrivalOp != null)
				return OperationFault(growth, growth.ArrivalOp, fault);
			if (growth?.ArrivalCandidate != null)
				return CandidateFault(growth, growth.ArrivalCandidate, fault);
			KingdomLog.Log("growth arrival failed before quarantine evidence could bind: " + fault);
			return ArrivalResult.Failed;
		}

		private static string BoundedFault(string fault)
		{
			if (string.IsNullOrEmpty(fault)) return "arrival callback failed";
			int length = Math.Min(fault.Length, KingdomLifecycleRules.MaxTextChars);
			if (length > 0 && length < fault.Length && char.IsHighSurrogate(fault[length - 1]))
				length--;
			return fault.Substring(0, length);
		}

		private static string CurrentDomainGraphHash(KingdomSystem system,
			GameObject settler, KingdomGrowthDomainStepKind kind, string operationId,
			bool legacyV1 = false)
		{
			return kind == KingdomGrowthDomainStepKind.Accounting
				? AccountingHash(system, false)
				: PersonDomainHash(system, settler, kind, false, operationId, legacyV1);
		}

		private static string CurrentDomainMapHash(KingdomSystem system,
			GameObject settler, KingdomGrowthDomainStepKind kind, string operationId)
		{
			return kind == KingdomGrowthDomainStepKind.Accounting
				? AccountingMapHash(system, false)
				: PersonDomainMapHash(system, settler, kind, false, operationId);
		}

		private static string PersonDomainHash(KingdomSystem system, GameObject settler,
			KingdomGrowthDomainStepKind kind, bool projectedAfter, string operationId,
			bool legacyV1 = false, long frozenAppliedTick = 0L)
		{
			bool exactCitizenship = kind == KingdomGrowthDomainStepKind.Enrollment
				&& !legacyV1 && ExactCitizenshipPlan(settler);
			if (exactCitizenship)
			{
				if (!ArrivalAllegianceAcyclic(settler?.Brain?.Allegiance)) return null;
				ConversationScript exactConversation = settler?.GetPart<ConversationScript>();
				if (!ArrivalConversationAcyclic(exactConversation?.Blueprint)) return null;
			}
			else if (kind == KingdomGrowthDomainStepKind.Enrollment && !legacyV1)
			{
				if (!ArrivalAllegianceRepresentable(settler?.Brain?.Allegiance)) return null;
				ConversationScript actual = projectedAfter
					? null : settler?.GetPart<ConversationScript>();
				ConversationXMLBlueprint conversation = projectedAfter
					? ExpectedArrivalConversationBlueprint(settler?.ID,
						settler?.GetStringProperty(ArrivalOriginPlanProperty))
					: actual?.Blueprint;
				if (!ArrivalConversationRepresentable(conversation))
					return null;
			}
			return Hash(delegate(BinaryWriter writer)
			{
				WriteString(writer, "arrival-domain-graph");
				writer.Write((byte)kind); WriteString(writer, settler?.ID);
				WriteString(writer, settler?.Blueprint);
				switch (kind)
				{
				case KingdomGrowthDomainStepKind.Enrollment:
					bool hasBrain = settler?.Brain != null;
					writer.Write(hasBrain);
					if (legacyV1)
					{
						WriteString(writer, projectedAfter && hasBrain
							? system.KingdomFactionName : settler?.GetPrimaryFaction());
						writer.Write(hasBrain && (projectedAfter
							|| settler.Brain.Allegiance.Calm));
						writer.Write(hasBrain && !projectedAfter
							&& settler.Brain.Allegiance.Hostile);
						writer.Write(projectedAfter ? 1
							: settler.GetIntProperty("KingdomCitizen"));
						writer.Write(projectedAfter ? 1
							: settler.GetIntProperty("KingdomBorn"));
						WriteString(writer, projectedAfter
							? settler.GetStringProperty(ArrivalOriginPlanProperty)
							: settler.GetStringProperty("KingdomOrigin"));
						WriteString(writer, projectedAfter ? operationId
							: settler.GetStringProperty(ArrivalEnrollmentReceiptProperty));
						WriteString(writer, projectedAfter ? operationId
							: settler.GetStringProperty(ArrivalConversationReceiptProperty));
						writer.Write(projectedAfter
							|| settler != null && settler.HasPart<ConversationScript>());
						break;
					}
					if (exactCitizenship)
					{
						WriteExactAllegianceGraph(writer, settler?.Brain?.Allegiance,
							projectedAfter, system?.KingdomFactionName);
						writer.Write(projectedAfter ? 1
							: settler.GetIntProperty("KingdomCitizen"));
						writer.Write(projectedAfter ? 1
							: settler.GetIntProperty("KingdomBorn"));
						string exactOrigin = projectedAfter
							? settler.GetStringProperty(ArrivalOriginPlanProperty)
							: settler.GetStringProperty("KingdomOrigin");
						WriteString(writer, exactOrigin);
						WriteString(writer, projectedAfter ? operationId
							: settler.GetStringProperty(ArrivalEnrollmentReceiptProperty));
						// This legacy property and the full native conversation are projections of
						// themselves: the exact citizenship callback does not touch either.
						WriteString(writer,
							settler.GetStringProperty(ArrivalConversationReceiptProperty));
						WriteArrivalConversationGraph(writer, settler, false, exactOrigin);
						WriteCitizenshipReceiptGraph(writer, system, settler,
							projectedAfter, frozenAppliedTick);
						break;
					}
					bool baseReplaced = false;
					WriteAllegianceGraph(writer, settler?.Brain?.Allegiance, projectedAfter,
						system?.KingdomFactionName, true, 0, ref baseReplaced);
					WriteString(writer, projectedAfter && hasBrain
						? system.KingdomFactionName : settler?.GetPrimaryFaction());
					writer.Write(hasBrain && (projectedAfter
						|| settler.Brain.Allegiance.Calm));
					writer.Write(hasBrain && !projectedAfter
						&& settler.Brain.Allegiance.Hostile);
					writer.Write(projectedAfter ? 1 : settler.GetIntProperty("KingdomCitizen"));
					writer.Write(projectedAfter ? 1 : settler.GetIntProperty("KingdomBorn"));
					string origin = projectedAfter
						? settler.GetStringProperty(ArrivalOriginPlanProperty)
						: settler.GetStringProperty("KingdomOrigin");
					WriteString(writer, origin);
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalEnrollmentReceiptProperty));
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalConversationReceiptProperty));
					WriteArrivalConversationGraph(writer, settler, projectedAfter, origin);
					break;
				case KingdomGrowthDomainStepKind.Roster:
					WriteString(writer, projectedAfter
						? settler.GetStringProperty(ArrivalNamePlanProperty)
						: settler.DisplayName);
					WriteString(writer, projectedAfter
						? settler.GetStringProperty(ArrivalNamePlanProperty)
						: settler.GetStringProperty("KingdomName"));
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalRosterReceiptProperty));
					break;
				case KingdomGrowthDomainStepKind.Creed:
					WriteString(writer, projectedAfter ? PlannedCreed(settler)
						: settler.GetStringProperty(KingdomCreed.CreedProperty));
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalCreedReceiptProperty));
					break;
				case KingdomGrowthDomainStepKind.Population:
					writer.Write(projectedAfter ? system.Population + 1 : system.Population);
					break;
				}
			});
		}
	}
}
