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
		/// <summary>Cold-load recovery exists for debugger/injected saves only. Native Qud cannot
		/// save between AfterDie and Die's immediate IsPlayer recheck, but a checkpoint that does
		/// exist must either re-prove exact physical evidence or remain fail-closed.</summary>
		private void TryResumePendingRite(string Context)
		{
			if (string.IsNullOrEmpty(PendingDeathToken)
				|| PendingRiteStage == MourningRiteStage.None
				|| PendingAccessionRepairResidentId != 0)
			{
				return;
			}
			try
			{
				XRLGame game = The.Game;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				bool alreadyCrossed = PendingRiteStage == MourningRiteStage.BodyCrossed;
				GameObject founder = alreadyCrossed ? null : The.Player;
				if (game == null || system == null || (!alreadyCrossed && (founder == null
					|| !string.Equals(founder.IDIfAssigned, PendingFounderObjectId,
						StringComparison.Ordinal))))
				{
					QuarantinePendingRite(Context, "the exact controlled founder is absent");
					return;
				}
				GameObject heir = alreadyCrossed ? The.Player : null;
				string boundZone;
				bool heirExact = alreadyCrossed
					? GameObject.Validate(heir) && heir.IsPlayer()
						&& heir.GetIntProperty(KingdomResidents.ResidentIdProperty) == PendingHeirResidentId
						&& string.Equals(heir.IDIfAssigned, PendingHeirObjectId, StringComparison.Ordinal)
						&& string.Equals(heir.CurrentZone?.ZoneID, PendingHeirZoneId, StringComparison.Ordinal)
					: KingdomResidents.TryResolveBoundBody(system, PendingHeirResidentId, true,
						out heir, out boundZone)
						&& string.Equals(heir.IDIfAssigned, PendingHeirObjectId, StringComparison.Ordinal)
						&& string.Equals(boundZone, PendingHeirZoneId, StringComparison.Ordinal);
				if (!heirExact)
				{
					QuarantinePendingRite(Context, "the exact frozen heir is absent");
					return;
				}

				if (PendingRiteStage == MourningRiteStage.Frozen)
				{
					if (KingdomSuccessionRules.WorldTicksUntilDue(game.TimeTicks, PendingDueTick) > 0L)
					{
						game.TimeTicks = PendingDueTick;
					}
					PendingPhase = InterregnumPhase.RiteDue;
					Checkpoint(MourningRiteStage.WordArrived);
				}
				if (PendingRiteStage == MourningRiteStage.WordArrived)
				{
					GameObject walked;
					string failure;
					if (!KingdomSuccessionRite.ProcessionEvidence(system, PendingDeathToken,
						PendingRiteZoneId, PendingRiteFixtureObjectId,
						PendingRiteAttendeeManifest, out walked)
						&& !KingdomSuccessionRite.TryHoldProcession(system, PendingDeathToken,
							PendingRiteZoneId, PendingRiteFixtureObjectId,
							PendingRiteAttendeeManifest, out walked, out failure))
					{
						QuarantinePendingRite(Context, failure);
						return;
					}
					if (!ReferenceEquals(walked, heir))
					{
						QuarantinePendingRite(Context, "procession evidence names another body");
						return;
					}
					Checkpoint(MourningRiteStage.ProcessionComplete);
				}
				if (PendingRiteStage == MourningRiteStage.ProcessionComplete)
				{
					GameObject proved;
					if (!KingdomSuccessionRite.ProcessionEvidence(system, PendingDeathToken,
						PendingRiteZoneId, PendingRiteFixtureObjectId,
						PendingRiteAttendeeManifest, out proved)
						|| !ReferenceEquals(proved, heir))
					{
						QuarantinePendingRite(Context, "completed procession evidence is absent");
						return;
					}
					int ordinal;
					long deathTick;
					KingdomSuccessionRules.TryReadDeathToken(PendingDeathToken,
						out ordinal, out deathTick);
					string history = KingdomSuccessionRules.FounderEpitaph(
						KingdomPresentation.Rich(PendingFounderName),
						KingdomPresentation.Rich(PendingRiteCityName),
						KingdomPresentation.Rich(system.FoundingRegionName),
						KingdomPresentation.Rich(PendingFounderCause))
						+ " The named residents walked to "
						+ KingdomPresentation.Rich(PendingRiteFixtureName)
						+ " and held the mourning rite here.";
					GameObject shrine;
					string failure;
					if (!KingdomSuccessionRite.TryEnsureFounderShrine(PendingDeathToken,
						PendingFounderName, deathTick, PendingFounderCause, history,
						PendingRiteCityName, PendingRiteZoneId, PendingRiteFixtureObjectId,
						PendingShrineX, PendingShrineY, PendingShrineObjectId,
						out shrine, out failure))
					{
						QuarantinePendingRite(Context, failure);
						return;
					}
					PendingShrineObjectId = shrine.IDIfAssigned;
					CompletedShrineToken = PendingDeathToken;
					CompletedShrineObjectId = PendingShrineObjectId;
					CompletedShrineZoneId = PendingRiteZoneId;
					Checkpoint(MourningRiteStage.ShrinePlaced);
				}

				KingdomCityBook book;
				int residentId;
				KingdomCityState city;
				KingdomResidentRow row;
				int rowIndex;
				KingdomCityFault cityFault;
				if ((PendingRiteStage != MourningRiteStage.ShrinePlaced
						&& PendingRiteStage != MourningRiteStage.BodyCrossed)
					|| !KingdomResidents.TryLocate(system, heir, out book, out residentId)
					|| residentId != PendingHeirResidentId || !book.TryRead(out city, out cityFault)
					|| !city.TryResidentIndex(residentId, out rowIndex)
					|| !city.TryResident(rowIndex, out row))
				{
					QuarantinePendingRite(Context, "the frozen resident row cannot cross the rite boundary");
					return;
				}
				int officeId = ReferenceEquals(book, system.City)
					? system.OfficeHolderResidentId : system.Away?.OfficeHolderResidentId ?? 0;
				string legacyOffice = ReferenceEquals(book, system.City)
					? system.OfficeHolderName : system.Away?.OfficeHolderName;
				bool heldOffice = officeId > 0 ? officeId == row.ResidentId
					: string.Equals(legacyOffice, row.Name, StringComparison.Ordinal);
				string heirCreed = heir.GetStringProperty(KingdomCreed.CreedProperty);
				if (!alreadyCrossed)
				{
					string citizenshipFailure;
					if (!KingdomCitizenship.CanRemove(system, heir, out citizenshipFailure))
					{
						QuarantinePendingRite(Context,
							"citizenship preflight failed: " + citizenshipFailure);
						return;
					}
					KingdomPlayerBodyTransfer transfer = SetPlayerBodyAndRebindAll(game, founder,
						heir, "cold-load accession");
					if (!transfer.MayPublishAccession)
					{
						SetPlayerBodyAndRebindAll(game, heir, founder, "cold-load rollback");
						QuarantinePendingRite(Context, "body transfer was not exact");
						return;
					}
					Checkpoint(MourningRiteStage.BodyCrossed);
				}
				KingdomResidentRow former;
				bool seated;
				KingdomAccessionOutcome outcome = KingdomResidents.TryAccede(system, heir,
					out former, out seated);
				if (outcome != KingdomAccessionOutcome.Committed)
				{
					AccessionOwnershipCommitted = true;
					QueueAccessionRepair(new KingdomHeir(row.Name, row.ArrivedTick, null,
						row.KeptCreeds, true, heldOffice, row.BoundZoneId, row.ResidentId),
						PendingFounderName, ReferenceEquals(book, system.City));
					TryPrepareRepairableHeir(heir);
					return;
				}
				CompleteAccession(game, system, heir, PendingFounderName, former,
					PendingDeathToken, PendingRoad, PendingDays, heldOffice, heirCreed,
					PendingHeirZoneId, "cold-load accession " + Context);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: cold-load rite recovery failed", ex);
				QuarantinePendingRite(Context, ex.GetType().Name);
			}
		}

		private void QuarantinePendingRite(string Context, string Failure)
		{
			SuccessionDisabled = true;
			KingdomLog.Log("succession: pending rite quarantined during " + Context + " ("
				+ (string.IsNullOrEmpty(Failure) ? "unproved physical evidence" : Failure) + ")");
			TryTellFailure("The saved mourning rite cannot prove its exact heir, residents, fixture, and shrine. Succession is disabled for this save; nothing was substituted or minted.");
		}

	}
}
