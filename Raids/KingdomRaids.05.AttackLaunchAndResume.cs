using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomRaids
	{
		// Plan-before-effect ordering: LaunchRaid now only builds/publishes ProjectionIntent
		// (no mint, no place, no commit). The per-actor Create->Add->Activate interleave now
		// happens entirely inside ResumeOpen -> ResumeAttackProjections (09.cs, unchanged).
		// Later factory callbacks therefore observe published authority plus any raiders/RNG
		// already placed in that same resume call, so a successful launch is no longer
		// bit-equivalent to the old single-batch mint order, and entry cells can change synchronously. OPEN 09.cs
		// custody defects, still unaddressed here: a same-blueprint replacement can still be ID/part-stamped, and
		// a BeginProjection/AddObject refusal can leave a surviving body while a later zero/zero reset re-mints.
		private static void LaunchRaid(KingdomSystem system, Zone zone,
			KingdomSurvey survey, KingdomRaidIncident incident)
		{
			KingdomRaidProfile profile;
			GrowthStage frozenStage;
			if (!KingdomRaidProfiles.TryResolveFrozen(incident.AttackerFactionId,
				incident.ForceProfileId, incident.Seed, incident.PlannedPartySize,
				out profile, out frozenStage))
			{
				CancelIncident(system, incident, KingdomRaidResolution.SourceInvalid,
					"The frozen faction profile no longer exists; the threat was cancelled without loss.");
				return;
			}
			GameObject objective = ExactStore(survey, incident.Seed);
			if (objective == null || objective.CurrentCell == null)
			{
				CancelIncident(system, incident, KingdomRaidResolution.NoValidObjective,
					"No exact dedicated water store remained at the named target; no substitute was chosen.");
				return;
			}
			int defence = 0;
			if (incident.State == KingdomRaidIncidentState.Fortified)
			{
				defence = ApplyWatchDisarray(system,
					RevalidateDefence(system, survey, incident));
				if (defence != incident.DefenceEstimate)
				{
					PublishFortifyFailure(system, incident,
						"One or more exact named works or crews no longer match the muster. Every answer is open again; no substitute work and no penalty were chosen.");
					return;
				}
			}
			int size = Math.Max(1, incident.PlannedPartySize);
			KingdomRules.RaidOutcome outcome = KingdomRules.ResolveRaid(defence, size);
			int party = KingdomRules.RaidingPartySize(size, defence, outcome);
			if (party <= 0)
			{
				CancelIncident(system, incident, KingdomRaidResolution.Repelled,
					"The named works turned the warband back before it entered the settlement.");
				return;
			}
			if (string.IsNullOrEmpty(objective.IDIfAssigned))
			{
				CancelIncident(system, incident, KingdomRaidResolution.NoValidObjective,
					"The named raid objective lacks assigned physical identity.");
				return;
			}
			List<Cell> cells = DeterministicEntryCells(zone, objective.CurrentCell, incident.Seed);
			party = Math.Min(party, Math.Min(cells.Count, KingdomRaidIncidentRules.MaxParty));
			if (party <= 0)
			{
				CancelIncident(system, incident, KingdomRaidResolution.NoValidObjective,
					"No exact entry cell could receive the warband; no raid loss was inferred.");
				return;
			}
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(
				system.LifecycleBook, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidAttack, The.Game.TimeTicks);
			if (op == null) return;
			op.ZoneId = incident.TargetZoneId;
			op.ObjectId = incident.Id;
			op.Faction = incident.AttackerFactionId;
			op.DisplayFaction = DisplayFaction(incident.AttackerFactionId);
			op.Origin = objective.IDIfAssigned;
			op.ArrivalText = "stores";
			op.Target = objective.CurrentCell.X;
			op.Count = objective.CurrentCell.Y;
			op.Defence = defence;
			op.PartySize = party;
			op.PlunderRequested = KingdomRules.RaidPlunder(
				incident.MaximumPlunder, defence, outcome);
			op.EffectState = KingdomLifecyclePhysicalState.Prepared;
			for (int i = 0; i < party; i++)
			{
				string objectId = KingdomLifecycleRules.ChildId(op.Id, "raider", i);
				string blueprint = KingdomRaidProfiles.Blueprint(profile, frozenStage, incident.Seed, i);
				KingdomLifecycleProjection projection = KingdomLifecycleRules.RaidRuntimeAdapter.PrepareProjection(
					system.LifecycleBook, op, i, objectId, blueprint,
					zone.ZoneID, cells[i].X, cells[i].Y);
				if (projection == null) return;
			}
			string display = op.DisplayFaction;
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op,
				"a warband of " + display + " entered " + KingdomPresentation.Rich(system.SeatName)
					+ " seeking the exact dedicated stores named in its grievance",
				"Raiders entered for store " + objective.IDIfAssigned + "; no plunder is recorded before contact.",
				"{{R|A warband of " + display + " enters the settlement and moves on the named stores!}}",
				"the watch that met the warband of " + display, null);
			if (!KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(system.LifecycleBook, op)
				|| !KingdomLifecycleRules.TryPublish(system.LifecycleBook, op)
				|| !KingdomLifecycleRules.AdvancePhase(system.LifecycleBook, op,
					KingdomLifecyclePhase.ProjectionIntent, The.Game.TimeTicks)) return;
			ResumeOpen(system, zone);
		}

		private static void ResumeOpen(KingdomSystem system, Zone zone)
		{
			KingdomLifecycleBook book = system?.LifecycleBook;
			KingdomLifecycleOperation op = book?.Raid;
			if (op == null) return;
			for (int guard = 0; guard < 24 && book.Raid == op; guard++)
			{
				long now = Math.Max(The.Game.TimeTicks, op.UpdatedTick);
				switch (op.Phase)
				{
				case KingdomLifecyclePhase.Prepared:
					if (!KingdomLifecycleRules.AdvancePhase(book, op, NextAfterPrepared(op.Action), now)) return;
					break;
				case KingdomLifecyclePhase.ProjectionIntent:
					if (op.Action == KingdomLifecycleAction.RaidAttack
						&& !ResumeAttackProjections(system, zone, op)) return;
					if (op.Action == KingdomLifecycleAction.RaidDeliverDemand
						&& !ResumeDemandProjection(system, zone, op)) return;
					if (!AllProjectionsProved(op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.Projected, now)) return;
					break;
				case KingdomLifecyclePhase.Projected:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						op.Action == KingdomLifecycleAction.RaidAttack
							? KingdomLifecyclePhase.WaterIntent
							: KingdomLifecyclePhase.DomainIntent, now)) return;
					break;
				case KingdomLifecyclePhase.WaterIntent:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						KingdomLifecyclePhase.WaterSettled, now)) return;
					break;
				case KingdomLifecyclePhase.WaterSettled:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						KingdomLifecyclePhase.DomainIntent, now)) return;
					break;
				case KingdomLifecyclePhase.DomainIntent:
					if (!KingdomLifecycleRules.RaidRuntimeAdapter.ProveDomain(book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.DomainSettled, now)) return;
					break;
				case KingdomLifecyclePhase.DomainSettled:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						op.Action == KingdomLifecycleAction.RaidAttack
							? KingdomLifecyclePhase.EffectIntent : KingdomLifecyclePhase.Sinks, now)) return;
					break;
				case KingdomLifecyclePhase.EffectIntent:
					InspectOpenAttack(system, zone, op);
					return;
				case KingdomLifecyclePhase.EffectsSettled:
					if (!KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.Sinks, now)) return;
					break;
				case KingdomLifecyclePhase.Sinks:
					if (!DispatchOutbox(system, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.ScheduleIntent, now)) return;
					break;
				case KingdomLifecyclePhase.ScheduleIntent:
					if (!KingdomLifecycleRules.RaidRuntimeAdapter.ProveSchedule(book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.Terminal, now)) return;
					break;
				case KingdomLifecyclePhase.Terminal:
					if (op.Action == KingdomLifecycleAction.RaidAttack)
					{
						KingdomRaidResolution result;
						string notice;
						if (!TryDeriveAttackResult(zone, op, out result, out notice))
						{
							KingdomLifecycleRules.Quarantine(op,
								"settled raid attack had no exact terminal result witness");
							return;
						}
						int plunder = op.PlunderProved;
						if (!KingdomLifecycleRules.Retire(book, op, now)) return;
						ResolveIncident(system, result, plunder, notice);
						return;
					}
					KingdomLifecycleRules.Retire(book, op, now);
					return;
				default:
					return;
				}
			}
		}

	}
}
