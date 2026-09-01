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

		private static bool ReconcileArrivalDomains(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation, GameObject settler)
		{
			KingdomGrowthArrivalCandidate candidate = growth?.ArrivalCandidate;
			if (candidate?.CreateStep == null || !string.Equals(
				candidate.CreateStep.AfterObjectGraphHash, ArrivalPersonHash(settler),
				StringComparison.Ordinal)) return false;
			while (operation.DomainCursor < operation.DomainSteps.Count)
			{
				int ordinal = operation.DomainCursor;
				KingdomGrowthDomainStep step = operation.DomainSteps[ordinal];
				if (!string.Equals(step.CallbackBodyHash,
					ArrivalDomainBodyHash(system, operation, settler, step.Kind,
						operation.LegacyGrowthV1Plan),
					StringComparison.Ordinal)) return false;
				string currentGraph = CurrentDomainGraphHash(system, settler, step.Kind,
					operation.Id, operation.LegacyGrowthV1Plan);
				string currentMap = CurrentDomainMapHash(system, settler, step.Kind,
					operation.Id);
				bool before = string.Equals(currentGraph, step.BeforeGraphHash,
					StringComparison.Ordinal) && string.Equals(currentMap,
					step.BeforeMapHash, StringComparison.Ordinal);
				bool after = string.Equals(currentGraph, step.AfterGraphHash,
					StringComparison.Ordinal) && string.Equals(currentMap,
					step.AfterMapHash, StringComparison.Ordinal);
				if (!before && !after) return false;
				if (step.State == KingdomLifecyclePhysicalState.Prepared)
				{
					if (!before || !KingdomLifecycleRules.BeginGrowthDomainCallback(growth,
						operation, ordinal)) return false;
				}
				if (before)
				{
					ApplyArrivalDomain(system, settler, operation, step);
					if (!string.Equals(candidate.CreateStep.AfterObjectGraphHash,
						ArrivalPersonHash(settler), StringComparison.Ordinal)) return false;
					currentGraph = CurrentDomainGraphHash(system, settler, step.Kind,
						operation.Id, operation.LegacyGrowthV1Plan);
					currentMap = CurrentDomainMapHash(system, settler, step.Kind,
						operation.Id);
					if (!string.Equals(currentGraph, step.AfterGraphHash,
						StringComparison.Ordinal) || !string.Equals(currentMap,
						step.AfterMapHash, StringComparison.Ordinal)) return false;
				}
				if (!KingdomLifecycleRules.CommitGrowthDomainCallback(growth, operation,
					ordinal, step.AfterValue, step.AfterGraphHash, step.AfterMapHash))
					return false;
			}
			return operation.DomainCursor == operation.DomainSteps.Count;
		}

		private static string ArrivalDomainBodyHash(KingdomSystem system,
			KingdomGrowthOperation operation, GameObject settler,
			KingdomGrowthDomainStepKind kind, bool legacyV1 = false)
		{
			if (kind == KingdomGrowthDomainStepKind.Accounting)
				return HashText("arrival-domain-body", operation?.Id, "accounting");
			if (kind == KingdomGrowthDomainStepKind.Enrollment && !legacyV1
				&& ExactCitizenshipPlan(settler))
			{
				return HashText("arrival-domain-body:v3", operation?.Id, kind.ToString(),
					settler?.GetStringProperty(ArrivalOriginPlanProperty),
					settler?.GetStringProperty(ArrivalCreedPlanProperty),
					settler?.GetStringProperty(ArrivalNamePlanProperty),
					settler?.GetStringProperty(ArrivalDatePlanProperty),
					ArrivalCitizenshipPlanValue, system?.KingdomFactionName,
					"base-slot=100", "receipt=v1", "conversation=preserved");
			}
			return kind == KingdomGrowthDomainStepKind.Enrollment && !legacyV1
				? HashText("arrival-domain-body:v2", operation?.Id, kind.ToString(),
					settler?.GetStringProperty(ArrivalOriginPlanProperty),
					settler?.GetStringProperty(ArrivalCreedPlanProperty),
					settler?.GetStringProperty(ArrivalNamePlanProperty),
					settler?.GetStringProperty(ArrivalDatePlanProperty),
					system?.KingdomFactionName + "-100", "calm=true", "hostile=false",
					ArrivalConversationText, ArrivalConversationGoodbye,
					ArrivalConversationQuestion, ArrivalConversationAnswerPrefix,
					ArrivalConversationAnswerSuffix)
				: HashText("arrival-domain-body", operation?.Id, kind.ToString(),
					settler?.GetStringProperty(ArrivalOriginPlanProperty),
					settler?.GetStringProperty(ArrivalCreedPlanProperty),
					settler?.GetStringProperty(ArrivalNamePlanProperty),
					settler?.GetStringProperty(ArrivalDatePlanProperty));
		}

		private static void ApplyArrivalDomain(KingdomSystem system, GameObject settler,
			KingdomGrowthOperation operation, KingdomGrowthDomainStep step)
		{
			switch (step.Kind)
			{
			case KingdomGrowthDomainStepKind.Enrollment:
				// Old v1/v2 operations expected destructive Brain replacement. Do not silently
				// reinterpret them: quarantine before touching the body. New operations freeze this
				// explicit plan marker before their expected graph hashes are published.
				if (!ExactCitizenshipPlan(settler))
					throw new InvalidOperationException(
						"legacy destructive citizenship plan requires visible quarantine");
				if (!KingdomFounding.EnrollCitizen(settler,
					KingdomCitizenshipEnrollmentReason.Arrival, operation.CreatedTick))
					throw new InvalidOperationException("citizen enrollment callback refused");
				settler.SetIntProperty("KingdomBorn", 1);
				string origin = settler.GetStringProperty(ArrivalOriginPlanProperty);
				settler.SetStringProperty("KingdomOrigin", origin);
				system.OriginCounts.TryGetValue(origin, out int origins);
				system.OriginCounts[origin] = origins + 1;
				// ConversationScript is native/foreign lifecycle state. Citizenship never removes,
				// appends to, or replaces it; Qud's helper would do all three through a shared graph.
				settler.SetStringProperty(ArrivalEnrollmentReceiptProperty, operation.Id);
				break;
			case KingdomGrowthDomainStepKind.Roster:
				string given = settler.GetStringProperty(ArrivalNamePlanProperty);
				settler.GiveProperName(given, Force: true);
				settler.SetStringProperty("KingdomName", given);
				settler.SetStringProperty(ArrivalRosterReceiptProperty, operation.Id);
				break;
			case KingdomGrowthDomainStepKind.Creed:
				string creed = PlannedCreed(settler);
				KingdomCreed.Record(system, settler, creed);
				settler.SetStringProperty(ArrivalCreedReceiptProperty, operation.Id);
				break;
			case KingdomGrowthDomainStepKind.Population:
				system.Population++;
				break;
			case KingdomGrowthDomainStepKind.Accounting:
				system.Ledger.ArrivalCost += KingdomRules.DramsPerArrival;
				system.Ledger.Arrivals++;
				break;
			default:
				throw new InvalidOperationException("unexpected arrival domain " + step.Kind);
			}
		}

		private static bool ExactCitizenshipPlan(GameObject settler)
		{
			return settler != null && string.Equals(
				settler.GetStringProperty(ArrivalCitizenshipPlanProperty),
				ArrivalCitizenshipPlanValue, StringComparison.Ordinal);
		}

		private static bool ReconcileArrivalClock(KingdomSystem system, KingdomGrowthBook growth,
			KingdomGrowthOperation operation)
		{
			long current = system.NextArrivalTick;
			KingdomLifecycleCasAction action = KingdomLifecycleRules.GrowthClockAction(growth,
				operation, current);
			if (action == KingdomLifecycleCasAction.Apply)
			{
				if (!KingdomLifecycleRules.BeginGrowthClock(growth, operation, current)) return false;
				system.NextArrivalTick = operation.ClockLease.After;
				current = system.NextArrivalTick;
			}
			else if (operation.ClockState == KingdomLifecyclePhysicalState.Intent
				&& current == operation.ClockLease.Before)
			{
				system.NextArrivalTick = operation.ClockLease.After;
				current = system.NextArrivalTick;
			}
			if (current != operation.ClockLease.After) return false;
			if (operation.ClockState == KingdomLifecyclePhysicalState.Intent
				&& !KingdomLifecycleRules.CommitGrowthClockWitness(growth, operation, current))
				return false;
			return operation.ClockState == KingdomLifecyclePhysicalState.Proved
				&& growth.NextArrivalTick == current;
		}

		private static bool ReconcileArrivalOutbox(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation)
		{
			for (int i = 0; i < operation.OutboxEvents.Count; i++)
			{
				KingdomGrowthOutboxEvent e = operation.OutboxEvents[i];
				if (!ReconcileChronicleOutbox(system, growth, operation, e, i)) return false;
				if (!ReconcileInspectableOutbox(system.Ledger.Notes, e.Outbox.Ledger,
					e.LedgerBeforeCount, e.LedgerBeforeHash,
					e.LedgerDeclaredAfterCount, e.LedgerDeclaredAfterHash,
					growth, operation, i, KingdomGrowthOutboxSinkKind.Ledger,
					delegate(string text) { system.Ledger.Note(text); })) return false;
			}
			return true;
		}

		private static bool ReconcileChronicleOutbox(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation,
			KingdomGrowthOutboxEvent e, int ordinal)
		{
			if (e.Outbox.Chronicle == null) return e.Outbox.ChronicleState ==
				KingdomLifecycleSinkState.Skipped;
			if (e.LegacySingleRegisterChronicle || system.ChronicleEntries == null
				|| system.OutsiderEntries == null) return false;
			if (!KingdomChronicleReceiptRules.TryHashList("official", system.ChronicleEntries,
				out string official) || !KingdomChronicleReceiptRules.TryHashList("outsider",
					system.OutsiderEntries, out string outsider)) return false;
			KingdomLifecycleCasAction action = KingdomLifecycleRules.GrowthChronicleOutboxAction(
				growth, operation, ordinal, system.ChronicleEntries.Count, official,
				system.OutsiderEntries.Count, outsider);
			if (e.Outbox.ChronicleState == KingdomLifecycleSinkState.Delivered)
				return action == KingdomLifecycleCasAction.Confirm;
			if (action == KingdomLifecycleCasAction.Apply
				&& e.Outbox.ChronicleState == KingdomLifecycleSinkState.Pending)
			{
				if (!KingdomLifecycleRules.BeginGrowthChronicleOutbox(growth, operation,
					ordinal, system.ChronicleEntries.Count, official,
					system.OutsiderEntries.Count, outsider)) return false;
			}
			else if (action != KingdomLifecycleCasAction.Apply
				&& action != KingdomLifecycleCasAction.Confirm) return false;
			string fingerprint;
			if (!KingdomChronicleReceiptRules.TryFingerprint(e.Outbox.ChronicleReceiptId,
				e.Outbox.Chronicle, false, null, out fingerprint)) return false;
			KingdomChronicleDeclaration declaration = new KingdomChronicleDeclaration(
				e.Outbox.ChronicleReceiptId, e.Outbox.Chronicle, false, null, null, fingerprint,
				e.ChronicleOfficial, e.ChronicleOutsider, e.ChronicleBeforeHash,
				e.ChronicleDeclaredAfterHash, e.OutsiderBeforeHash,
				e.OutsiderDeclaredAfterHash);
			if (!KingdomChronicle.RecordDeclaredOnce(system, declaration)) return false;
			if (!KingdomChronicleReceiptRules.TryHashList("official", system.ChronicleEntries,
				out official) || !KingdomChronicleReceiptRules.TryHashList("outsider",
					system.OutsiderEntries, out outsider)) return false;
			return KingdomLifecycleRules.CommitGrowthChronicleOutbox(growth, operation,
				ordinal, system.ChronicleEntries.Count, official, system.OutsiderEntries.Count,
				outsider);
		}

		private static bool ReconcileInspectableOutbox(List<string> list, string text,
			int beforeCount, string beforeHash, int afterCount, string afterHash,
			KingdomGrowthBook growth, KingdomGrowthOperation operation, int ordinal,
			KingdomGrowthOutboxSinkKind sink, Action<string> append)
		{
			if (text == null) return true;
			if (!TryHashStringList(list, out string current)) return false;
			bool before = list.Count == beforeCount && current == beforeHash;
			bool after = list.Count == afterCount && current == afterHash;
			KingdomLifecycleSinkState state = sink == KingdomGrowthOutboxSinkKind.Chronicle
				? operation.OutboxEvents[ordinal].Outbox.ChronicleState
				: operation.OutboxEvents[ordinal].Outbox.LedgerState;
			if (state == KingdomLifecycleSinkState.Delivered) return after;
			if (!before && !after) return false;
			if (state == KingdomLifecycleSinkState.Pending)
			{
				if (!before || !KingdomLifecycleRules.BeginGrowthInspectableOutbox(growth,
					operation, ordinal, sink, beforeCount, beforeHash)) return false;
				state = KingdomLifecycleSinkState.Intent;
			}
			if (state == KingdomLifecycleSinkState.Intent && before)
			{
				append(text);
				if (!TryHashStringList(list, out current)) return false;
				after = list.Count == afterCount && current == afterHash;
			}
			return state == KingdomLifecycleSinkState.Intent && after
				&& KingdomLifecycleRules.CommitGrowthInspectableOutbox(growth, operation,
					ordinal, sink, afterCount, afterHash);
		}
	}
}
