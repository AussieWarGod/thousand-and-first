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
		private void CompleteAccession(XRLGame Game, KingdomSystem System, GameObject Heir,
			string FounderName, KingdomResidentRow FormerRow, string Token, NewsRoad Road,
			int Days, bool HeldOffice, string HeirCreed, string HeirZoneId, string Context)
		{
			// Player body and resident law have committed. No later presentation, knowledge,
			// chronicle, or profile failure may declare the line ended.
			AccessionOwnershipCommitted = true;
			PendingPhase = InterregnumPhase.Reigning;
			CompletedDeathToken = Token;
			SuccessionOrdinal++;
			PendingSealAccessionToken = Token;
			PendingSealAccessionReady = false;
			string shownHeir = KingdomPresentation.Rich(FormerRow.Name);
			PendingSealRiteChronicle = BoundPendingRite("the charter passed from "
				+ KingdomPresentation.Rich(FounderName) + " to " + shownHeir + " at "
				+ KingdomPresentation.Rich(PendingRiteCityName
					?? System.SeatName ?? "the settlement") + ".");
			try
			{
				PendingSealRiteChronicle = BoundPendingRite(LegacyPhysicalRiteUnavailable
					? KingdomSuccessionRules.RiteChronicle(KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(FounderName),
						shownHeir, Road, Days)
					: KingdomSuccessionRules.SuccessionChronicle(KingdomPresentation.Rich(PendingRiteCityName),
						KingdomPresentation.Rich(FounderName),
						KingdomPresentation.Rich(PendingFounderCause), shownHeir, Road, Days,
						KingdomPresentation.Rich(PendingRiteFixtureName)));
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: accession rite telling fell back", ex);
			}
			PendingDeathToken = null;
			PendingDueTick = 0L;
			PendingDays = 0;
			PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = "";
			PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSeated = false;
			PendingAccessionRepairArrivedTick = 0L;
			PendingAccessionRepairKeptCreeds = "";
			PendingRiteStage = MourningRiteStage.Complete;
			ClearPendingRiteIdentity();

			TryFinishAccessionBodyCleanup(Heir);
			TryPrepareRepairableHeir(Heir);

			int regard = 0;
			try
			{
				bool creedMatches = !string.IsNullOrEmpty(System.DeclaredCreed)
					&& string.Equals(HeirCreed, System.DeclaredCreed,
						StringComparison.OrdinalIgnoreCase);
				bool creedLeft = KingdomCreedRules.KeptHolds(FormerRow.KeptCreeds,
					System.DeclaredCreed);
				regard = KingdomSuccessionRules.AccessionRegard(FormerRow.ArrivedTick,
					Game.TimeTicks, creedMatches, creedLeft, HeldOffice);
				if (!TryResetPersonalKnowledge(System, Token, regard))
				{
					KingdomLog.Log("succession: honesty reset rolled back after accession; successor remains seated with prior knowledge intact");
					TryTellFailure("The charter changed hands, but the successor's personal records could not be opened safely. Nothing in them was changed.");
				}
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: post-accession honesty step failed", ex);
				KingdomLog.Log("succession: post-accession honesty step failed without reversing accession");
			}

			TryCompletePendingSealAccession(Context);
			TryInheritOpenQuests(Game, System, Token, FounderName);
			try
			{
				TryTell(KingdomSuccessionRules.RiteAttendedPopup(
					KingdomPresentation.Rich(System.SeatName),
					KingdomPresentation.Rich(FounderName), shownHeir, Road, Days));
				KingdomLog.Log("succession: " + FounderName + " -> " + FormerRow.Name + " token="
					+ Token + " heirZone=" + HeirZoneId + " road=" + Road + " days=" + Days
					+ " regard=" + regard);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: accession telling failed after commit", ex);
			}
		}

		private static void TryInheritOpenQuests(XRLGame Game, KingdomSystem System,
			string DeathToken, string FounderName)
		{
			if (Game == null || Game.Quests == null || System == null) return;
			foreach (KeyValuePair<string, Quest> pair in Game.Quests)
			{
				Quest quest = pair.Value;
				if (quest == null || quest.Finished) continue;
				string questId = string.IsNullOrEmpty(quest.ID) ? pair.Key : quest.ID;
				if (string.IsNullOrEmpty(questId)) questId = quest.Name;
				try
				{
					string eventId = KingdomSuccessionRules.InheritedQuestEventId(
						DeathToken, questId);
					if (string.IsNullOrEmpty(eventId) || !KingdomChronicle.RecordOnce(System,
						eventId, KingdomSuccessionRules.InheritedQuestChronicle(
							FounderName, quest.Name)))
					{
						KingdomLog.Log("succession: one inherited undertaking could not settle its Chronicle receipt");
					}
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: inherited quest Chronicle failed", ex);
					KingdomLog.Log("succession: one inherited undertaking lacked its Chronicle line ("
						+ ex.GetType().Name + ")");
				}
				try
				{
					if (KingdomSuccessionRules.PersonalQuest(questId, quest.Name)
						&& !quest.HasProperty(KingdomSuccessionRules.InheritedQuestMarker))
					{
						quest.Name = KingdomSuccessionRules.InheritedQuestName(quest.Name);
						quest.SetProperty(KingdomSuccessionRules.InheritedQuestMarker, "1");
					}
				}
				catch (Exception ex)
				{
					MetricsManager.LogError("ThousandAndFirst: inherited quest label failed", ex);
					KingdomLog.Log("succession: one personal undertaking remained unlabelled ("
						+ ex.GetType().Name + ")");
				}
			}
		}

		private static void TryPrepareRepairableHeir(GameObject Heir)
		{
			try
			{
				PrepareSuccessor(Heir);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: successor body preparation failed", ex);
				KingdomLog.Log("succession: successor body preparation remains pending ("
					+ ex.GetType().Name + ")");
			}
			try
			{
				Heir.RequirePart<KingdomCharterPart>().EnsureAbility();
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: successor charter ability failed", ex);
				KingdomLog.Log("succession: successor charter ability remains pending ("
					+ ex.GetType().Name + ")");
			}
		}

		private static void TryFinishAccessionBodyCleanup(GameObject Heir)
		{
			try
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				string citizenshipFailure;
				if (!KingdomCitizenship.TryRemove(system, Heir,
					KingdomCitizenshipRemovalReason.Accession, out citizenshipFailure))
				{
					KingdomLog.Log("succession: exact citizenship cleanup remains pending ("
						+ (citizenshipFailure ?? "unknown failure") + ")");
					return;
				}
				KingdomStations.Post(Heir, 0, KingdomWorkKind.Other);
				Heir.RemoveIntProperty(KingdomResidents.ResidentIdProperty);
				Heir.RemoveIntProperty("KingdomBorn");
				Heir.RemoveStringProperty("KingdomName");
				Heir.RemoveStringProperty(KingdomLodging.HomePlotIdProperty);
				Heir.RemovePart<r_KingdomCitizenLegacy>();
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: successor resident cleanup failed", ex);
			}
		}

		private static string BoundPendingName(string Name)
		{
			string value = string.IsNullOrEmpty(Name) ? "the founder" : Name;
			return value.Length <= KingdomSealRecord.MaxNameChars
				? value : value.Substring(0, KingdomSealRecord.MaxNameChars);
		}

		private static string BoundPendingCreeds(string KeptCreeds)
		{
			string value = KeptCreeds ?? "";
			return value.Length <= MaxPendingRepairCreedsChars
				? value : value.Substring(0, MaxPendingRepairCreedsChars);
		}

	}
}
