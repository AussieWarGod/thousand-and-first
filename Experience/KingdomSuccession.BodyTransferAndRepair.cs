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
		private KingdomPlayerBodyTransfer SetPlayerBodyAndRebindAll(XRLGame Game,
			GameObject Original, GameObject Target, string Context)
		{
			KingdomPlayerBodyTransfer result = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				Original, Target,
				body => Game.Player.SetBody(body),
				() => The.Player,
				Game.Systems,
				(system, body) =>
				{
					IPlayerSystem playerSystem = system as IPlayerSystem;
					if (playerSystem != null)
					{
						playerSystem.RegisterPlayer(body,
							EventUnregistrar.Get(body, playerSystem));
					}
				},
				(system, body) =>
				{
					IPlayerSystem playerSystem = system as IPlayerSystem;
					if (playerSystem != null)
					{
						playerSystem.RegisterPlayer(body,
							EventRegistrar.Get(body, playerSystem));
					}
				});
			if (result.Failure != null)
			{
				MetricsManager.LogError("ThousandAndFirst: " + Context
					+ " body transfer or global player-system rebind failed", result.Failure);
			}
			if (!result.RegistrationsExact)
			{
				KingdomLog.Log("succession: " + Context
					+ " could not prove " + result.RegistrationFailures
					+ " player-system registration operation(s)");
			}
			return result;
		}

		private void QueueAccessionRepair(KingdomHeir Heir, string FounderName,
			string SettlementId)
		{
			PendingPhase = InterregnumPhase.RiteDue;
			PendingAccessionRepairResidentId = Heir.ResidentId;
			PendingAccessionRepairFounderName = BoundPendingName(FounderName);
			PendingAccessionRepairHeirName = BoundPendingName(Heir.Name);
			PendingAccessionRepairSettlementId = SettlementId;
			ClearLegacyAccessionRepairSeated();
			PendingAccessionRepairArrivedTick = Heir.ArrivedTick;
			PendingAccessionRepairKeptCreeds = BoundPendingCreeds(Heir.KeptCreeds);
		}

		private bool TryMigrateLegacyAccessionRepairSettlement(KingdomSystem System,
			string Context)
		{
			if (PendingAccessionRepairResidentId == 0 ||
				!string.IsNullOrEmpty(PendingAccessionRepairSettlementId)) return true;
			bool seated = ReadLegacyAccessionRepairSeated();
			string settlementId = seated ? System?.City?.SettlementId :
				(System?.NonSeatSettlementCount == 1
					? System.NonSeatSettlementAt(0)?.City?.SettlementId : null);
			if (!KingdomIdentityRules.IsSettlementId(settlementId))
			{
				KingdomLog.Log("succession: legacy accession repair cannot resolve its exact " +
					"settlement during " + Context);
				return false;
			}
			PendingAccessionRepairSettlementId = settlementId;
			ClearLegacyAccessionRepairSeated();
			return true;
		}

		private void TryCompletePendingAccessionRepair(string Context)
		{
			if (PendingAccessionRepairResidentId == 0)
			{
				return;
			}
			try
			{
				XRLGame game = The.Game;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				GameObject heir = The.Player;
				if (game == null || system == null || !system.Founded
					|| !GameObject.Validate(heir) || !heir.IsAlive)
				{
					KingdomLog.Log("succession: pending accession repair cannot prove its exact controlled resident during "
						+ Context);
					return;
				}
				if (!TryMigrateLegacyAccessionRepairSettlement(system, Context)) return;
				string settlementId = PendingAccessionRepairSettlementId;
				KingdomResidentRow formerRow = default(KingdomResidentRow);
				KingdomAccessionOutcome outcome = KingdomResidents.TryRepairAccession(system,
					heir, PendingAccessionRepairResidentId, settlementId,
					PendingAccessionRepairHeirName, PendingAccessionRepairArrivedTick,
					PendingAccessionRepairKeptCreeds, out formerRow);
				if (outcome != KingdomAccessionOutcome.Committed)
				{
					KingdomLog.Log("succession: pending accession repair remains after " + Context);
					TryPrepareRepairableHeir(heir);
					return;
				}
				string founderName = string.IsNullOrEmpty(PendingAccessionRepairFounderName)
					? "the founder" : PendingAccessionRepairFounderName;
				string token = PendingDeathToken ?? "";
				string heirCreed = heir.GetStringProperty(KingdomCreed.CreedProperty);
				CompleteAccession(game, system, heir, founderName, formerRow, token,
					PendingRoad, PendingDays, heirCreed,
					heir.CurrentZone?.ZoneID ?? "", "accession repair " + Context);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: pending accession repair failed closed", ex);
				KingdomLog.Log("succession: pending accession repair remains after " + Context
					+ " (" + ex.GetType().Name + ")");
			}
		}

	}
}
