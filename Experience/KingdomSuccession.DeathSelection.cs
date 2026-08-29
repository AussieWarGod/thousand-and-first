using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;
using XRL.World.Tinkering;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		private void HandleFounderDeath(AfterDieEvent E)
		{
			XRLGame game = The.Game;
			GameObject founder = E?.Dying;
			KingdomSystem system = game?.GetSystem<KingdomSystem>();
			if (game == null || founder == null
				|| !KingdomMaster.AutomaticWorkAllowed(system)
				|| !KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled)
				|| !ReferenceEquals(The.Player, founder))
			{
				return;
			}
			bool mode = KingdomSuccessionRules.ModeOn(game.gameMode,
				game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey));
			if (!mode)
			{
				return;
			}
			if (system == null || !system.Founded)
			{
				return;
			}
			ReconcileAbandonedSeatClimb(system);

			string founderName = founder.BaseDisplayNameStripped;
			string founderCause = DeathCause(E);
			if (string.IsNullOrEmpty(founderName)
				|| founderName.Length > KingdomSealRecord.MaxNameChars
				|| string.IsNullOrEmpty(founderCause)
				|| founderCause.Length > MaxPendingRiteChronicleChars)
			{
				KingdomLog.Log("succession: exact founder name/cause exceeded its persistence bound");
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			if (PendingAccessionRepairResidentId != 0)
			{
				TryCompletePendingAccessionRepair("before another death");
				if (PendingAccessionRepairResidentId != 0)
				{
					KingdomLog.Log("succession: unresolved accession repair could not precede another death");
					PublishFounderDeath(system, founderName, E);
					EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
					return;
				}
			}
			if (!string.IsNullOrEmpty(PendingSealAccessionToken))
			{
				TryCompletePendingSealAccession("before another death");
				if (!string.IsNullOrEmpty(PendingSealAccessionToken))
				{
					PublishFounderDeath(system, founderName, E);
					EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
					return;
				}
			}
			string founderId = founder.ID;
			long deathTick = game.TimeTicks < 0L ? 0L : game.TimeTicks;
			string token = KingdomSuccessionRules.FounderDeathToken(
				SuccessionOrdinal + 1, deathTick, founderId);
			if (string.IsNullOrEmpty(token) || token.Length > MaxSealAccessionTokenChars)
			{
				KingdomLog.Log("succession: exact founder-death token exceeded its persistence bound");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			SuccessionAttemptVerdict attempt = KingdomSuccessionRules.JudgeAttempt(
				token, PendingDeathToken, CompletedDeathToken);
			if (attempt != SuccessionAttemptVerdict.Begin)
			{
				KingdomLog.Log("succession: death ignored by idempotence gate " + attempt + " token=" + token);
				return;
			}

			List<HeirRuntime> heirs;
			if (!TryReadHeirs(system, out heirs))
			{
				KingdomLog.Log("succession: the complete resident law could not be read; no partial roll was used");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			KingdomHeir[] candidates = new KingdomHeir[heirs.Count];
			for (int i = 0; i < heirs.Count; i++)
			{
				candidates[i] = heirs[i].Rule;
			}
			KingdomSuccessionConfiguration configuration;
			string configurationFailure;
			if (!TryGetCurrentConfiguration(system, out configuration, out configurationFailure))
			{
				KingdomLog.Log("succession: the realm's custom could not be proved ("
					+ configurationFailure + ")");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			KingdomSuccessionSelection selection;
			KingdomGroomingRecord grooming;
			bool hasGrooming;
			if (!TryRefreshGrooming(system, true, out grooming, out hasGrooming,
				out configurationFailure))
			{
				KingdomLog.Log("succession: grooming proof could not be refreshed ("
					+ configurationFailure + ")");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			if (!KingdomSuccessionRules.TryResolveConfiguredHeir(candidates, configuration,
				grooming, hasGrooming, out selection))
			{
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.NoHeir, E);
				return;
			}

			HeirRuntime chosen = heirs[selection.HeirIndex];
			HeirRuntime lawHeir = heirs[selection.LawHeirIndex];
			KingdomSuccessionSelectionReceipt receipt;
			if (!KingdomSuccessionSelectionReceipt.TryCreate(system.RealmId, token,
				configuration.Revision, chosen.Rule.ResidentId, chosen.Rule.Name,
				lawHeir.Rule.ResidentId, lawHeir.Rule.Name, selection.Choice,
				selection.CostsTheSeat, selection.Reason, out receipt))
			{
				KingdomLog.Log("succession: configured selection could not freeze an exact receipt");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			string selectionReceipt = KingdomSuccessionSelectionReceipt.Encode(receipt);
			if (string.IsNullOrEmpty(selectionReceipt))
			{
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			if (selection.Reason == SuccessionSelectionReason.ChosenMissing
				|| selection.Reason == SuccessionSelectionReason.ChosenIneligible
				|| selection.Reason == SuccessionSelectionReason.ChosenAmbiguous)
				KingdomLog.Log("succession: configured resident failed exact roll proof ("
					+ selection.Reason + "); seniority applied without a seat cost");
			if (selection.Reason == SuccessionSelectionReason.GroomedMissing
				|| selection.Reason == SuccessionSelectionReason.GroomedIneligible
				|| selection.Reason == SuccessionSelectionReason.GroomedAmbiguous
				|| selection.Reason == SuccessionSelectionReason.GroomedUnready)
				KingdomLog.Log("succession: groomed successor was not ready ("
					+ selection.Reason + "); seniority applied without a seat cost");
			if ((chosen.Rule.KeptCreeds ?? "").Length > MaxPendingRepairCreedsChars)
			{
				KingdomLog.Log("succession: chosen heir's creed history exceeded the repair bound");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			GameObject heirBody;
			string heirZoneId;
			if (!KingdomResidents.TryResolveBoundBody(system, chosen.Rule.ResidentId, LoadZone: true,
				out heirBody, out heirZoneId))
			{
				KingdomLog.Log("succession: law chose resident " + chosen.Rule.ResidentId + " ("
					+ chosen.Rule.Name + "), but that exact bound body was unreachable; no substitute was tried");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			if (!GameObject.Validate(heirBody) || !heirBody.IsAlive || heirBody.CurrentCell == null
				|| ReferenceEquals(heirBody, founder))
			{
				KingdomLog.Log("succession: exact heir failed final body/cell validation; no substitute was tried");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			KingdomCityBook heirBook;
			int heirResidentId;
			if (!KingdomResidents.TryLocate(system, heirBody, out heirBook, out heirResidentId)
				|| heirResidentId != chosen.Rule.ResidentId)
			{
				KingdomLog.Log("succession: exact heir lost its resident row after body resolution");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			string citizenshipFailure;
			if (!KingdomCitizenship.CanRemove(system, heirBody, out citizenshipFailure))
			{
				KingdomLog.Log("succession: exact heir has no reversible citizenship boundary ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			bool heirWasSeated = ReferenceEquals(heirBook, system.City);
			KingdomSettlement heirSettlement = heirWasSeated ? null :
				system.FindNonSeatSettlementByBook(heirBook);
			string heirSettlementId = heirWasSeated ? system.City?.SettlementId :
				heirSettlement?.City?.SettlementId;
			if (!KingdomIdentityRules.IsSettlementId(heirSettlementId))
			{
				KingdomLog.Log("succession: heir city has no exact topology identity");
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			string riteCityName = heirWasSeated ? system.SeatName :
				heirSettlement.SettlementName;
			KingdomSuccessionRite.Plan ritePlan;
			string riteFailure;
			if (!KingdomSuccessionRite.TryFreeze(system, heirBook, heirBody, riteCityName,
				out ritePlan, out riteFailure))
			{
				KingdomLog.Log("succession: physical rite preflight refused (" + riteFailure + ")");
				PublishFounderDeath(system, founderName, E);
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}
			if ((ritePlan.CityName ?? "").Length > KingdomSealRecord.MaxNameChars
				|| (ritePlan.FixtureName ?? "").Length > KingdomSealRecord.MaxNameChars)
			{
				KingdomLog.Log("succession: exact rite locus names exceeded their persistence bound");
				EndDynasty(system, founderName, SuccessionVerdict.HeirUnreachable, E);
				return;
			}

			CarryFounderSuccession(E, game, founder, system, founderName, founderCause,
				deathTick, token, selectionReceipt, chosen, heirBody, heirZoneId,
				heirSettlementId, ritePlan);
		}
	}
}
