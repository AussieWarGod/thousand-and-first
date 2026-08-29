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
		public static bool TryTalkDown(KingdomSystem system, out string failure)
		{
			return TryTalkDown(system, The.Player?.CurrentZone, out failure);
		}

		public static bool TryTalkDown(KingdomSystem system, Zone zone, out string failure)
		{
			failure = null;
			KingdomRaidIncident incident;
			if (!CanAnswerHere(system, zone, out incident, out failure)) return false;
			if (system.GetRegardForRealm(incident.AttackerFactionId) <
				KingdomRules.DiplomacyStandingRequired)
			{
				failure = "They require {{C|" + KingdomRules.DiplomacyStandingRequired
					+ " standing}} before they will hear an envoy.";
				return false;
			}
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidTalkDown,
				"called in its regard with " + DisplayFaction(incident.AttackerFactionId)
					+ ", owing a doubled demand if that faction is provoked again",
				"{{G|The envoy is heard. No water changes hands; one obligation remains.}}", null);
			if (!PublishSimple(system, op))
			{
				failure = "The diplomatic answer could not be recorded.";
				return false;
			}
			KingdomGovernanceScope.Commit("answer threat by envoy");
			return true;
		}

		public static bool TryFight(KingdomSystem system, Zone zone, out string failure)
		{
			failure = null;
			KingdomRaidIncident incident;
			if (!CanAnswerHere(system, zone, out incident, out failure)) return false;
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidFight,
				"refused the demand of " + DisplayFaction(incident.AttackerFactionId)
					+ " and chose to meet the warband",
				"{{r|The demand is refused. The watch will meet the warband at the named hour.}}", null);
			if (!PublishSimple(system, op))
			{
				failure = "The decision to fight could not be recorded.";
				return false;
			}
			KingdomGovernanceScope.Commit("meet threat");
			return true;
		}

		public static bool TryFortify(KingdomSystem system, Zone zone, out string failure)
		{
			failure = null;
			KingdomRaidIncident incident;
			if (!CanAnswerHere(system, zone, out incident, out failure)) return false;
			if (zone == null || !string.Equals(zone.ZoneID, incident.TargetZoneId,
				StringComparison.Ordinal))
			{
				KingdomLifecycleOperation order = ResponseOperation(system, incident,
					KingdomLifecycleAction.RaidFortifyOrder,
					"ordered the named seat to muster against "
						+ DisplayFaction(incident.AttackerFactionId),
					"{{G|The muster order travels ahead. Exact works and crews will be proved at the seat; failure will reopen every answer without penalty.}}",
					null);
				if (!PublishSimple(system, order))
				{
					failure = "The remote muster order could not be recorded.";
					return false;
				}
				KingdomGovernanceScope.Commit("order fortification against threat");
				return true;
			}
			KingdomSurvey survey = KingdomSurvey.Take(zone, system);
			string commitment;
			int defence;
			if (!FreezeDefence(system, survey, out commitment, out defence))
			{
				failure = "No named defensive work here is presently able to answer the muster.";
				return false;
			}
			defence = ApplyWatchDisarray(system, defence);
			if (defence <= 0)
			{
				failure = "Raid-disarray leaves no effective defensive point to bind.";
				return false;
			}
			KingdomLifecycleOperation op = ResponseOperation(system, incident,
				KingdomLifecycleAction.RaidFortify,
				"mustered named works against " + DisplayFaction(incident.AttackerFactionId),
				"{{G|The named works are mustered. Their crews will be checked again when the warband comes.}}",
				commitment);
			if (op == null)
			{
				failure = "The muster could not reserve raid authority.";
				return false;
			}
			op.Defence = defence;
			if (!PublishSimple(system, op))
			{
				failure = "The muster could not be recorded.";
				return false;
			}
			KingdomGovernanceScope.Commit("fortify against threat");
			return true;
		}

		internal static void StepRaider(GameObject actor, r_KingdomRaiderObjective part,
			long timeTick)
		{
			if (!GameObject.Validate(actor) || part == null || actor.CurrentCell == null) return;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (!Enabled || !KingdomMaster.AutomaticWorkAllowed(system)) return;
			KingdomLifecycleOperation op = system?.LifecycleBook?.Raid;
			if (op == null || op.Action != KingdomLifecycleAction.RaidAttack
				|| op.Phase != KingdomLifecyclePhase.EffectIntent
				|| !string.Equals(op.Id, part.OperationId, StringComparison.Ordinal)) return;
			int distance = Math.Abs(actor.CurrentCell.X - part.TargetX)
				+ Math.Abs(actor.CurrentCell.Y - part.TargetY);
			if (distance > 1) return;
			KingdomSystem.Guard("raid objective contact", delegate
			{
				ProveObjectiveContact(system, actor.CurrentZone, op, part.TargetObjectId,
					part.TargetX, part.TargetY);
			});
		}

		internal static void RaiderDying(GameObject actor, r_KingdomRaiderObjective part)
		{
			if (actor == null || part == null) return;
			Zone zone = actor.CurrentZone;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			KingdomLifecycleOperation op = system?.LifecycleBook?.Raid;
			if (zone == null || system?.LifecycleBook == null) return;
			if (op != null && op.Action == KingdomLifecycleAction.RaidAttack
				&& op.Phase == KingdomLifecyclePhase.EffectIntent
				&& string.Equals(op.Id, part.OperationId, StringComparison.Ordinal)
				&& CountLiveRaiders(zone, op.Id, actor) == 0)
			{
				KingdomSystem.Guard("last raid body died", delegate
				{
					if (!KingdomLifecycleRules.RaidRuntimeAdapter.SkipEffectWithoutContact(
						system.LifecycleBook, op)
							|| !KingdomLifecycleRules.AdvancePhase(system.LifecycleBook, op,
								KingdomLifecyclePhase.EffectsSettled, The.Game.TimeTicks)) return;
					ResumeOpen(system, zone);
				});
				return;
			}
			KingdomRaidIncident recovery = FindRecovery(system.LifecycleBook.RaidLedger,
				system.LifecycleBook.SettlementId);
			if (recovery == null || recovery.RecoveryState != KingdomRaidRecoveryState.Active
				|| !string.Equals(recovery.AttackOperationId, part.OperationId,
					StringComparison.Ordinal)
				|| CountLiveRaiders(zone, part.OperationId, actor) != 0) return;
			KingdomSystem.Guard("last recovery-marked raider died", delegate
			{
				ReconcileRecoveryAtSeat(system, zone, actor);
			});
		}

	}
}
