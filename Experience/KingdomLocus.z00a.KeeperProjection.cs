using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomLocus
	{
		private static void DemoteKeepers(KingdomSurvey Survey, GameObject Except)
		{
			for (int i = 0; Survey != null && i < Survey.Settlers.Count; i++)
			{
				GameObject keeper = Survey.Settlers[i];
				if (GameObject.Validate(keeper) && !ReferenceEquals(keeper, Except)
					&& keeper.GetIntProperty("KingdomKeeper") == 1) DemoteKeeper(keeper);
			}
		}

		private static void DemoteKeeper(GameObject Keeper)
		{
			if (!GameObject.Validate(Keeper)) return;
			Keeper.SetIntProperty("KingdomKeeper", 0, RemoveIfZero: true);
			Keeper.SetIntProperty(KeeperMoodProperty, 0, RemoveIfZero: true);
			Qud.API.ConversationsAPI.addSimpleConversationToObject(Keeper,
				"The gathering bench has no keeper here now. Live and drink, all the same.",
				"Live and drink.");
		}

		private static void UpdateKeeperConversation(KingdomSystem System, GameObject Keeper,
			long TimeTicks)
		{
			// Read before either property is touched: an unmarked body has no prior mood even
			// though the engine's absent integer reads as the Peaceful enum's zero.
			bool isNew = Keeper.GetIntProperty("KingdomKeeper") != 1;
			bool grew = System.Population > Keeper.GetIntProperty(KeeperLastPopulationProperty);
			KingdomLocusRules.KeeperMood mood = KingdomLocusRules.ClassifyMood(
				DryStreakActive: System.DryStreak > 0,
				RaidIncoming: System.RaidState == 1,
				RecentlyRaided: KingdomLocusRules.WasRecentlyRaided(
					System.LastRaidTick, TimeTicks), Grew: grew);
			Keeper.SetIntProperty("KingdomKeeper", 1);
			Keeper.SetIntProperty(KeeperLastPopulationProperty, System.Population);
			if (!isNew && Keeper.GetIntProperty(KeeperMoodProperty) == (int)mood) return;
			Keeper.SetIntProperty(KeeperMoodProperty, (int)mood);
			KingdomLocusRules.KeeperSpeech speech = KingdomLocusRules.KeeperSpeechFor(mood,
				KingdomPresentation.Rich(System.KingdomDisplayName));
			// Named parameters avoid the Conversation API's same-shaped Filter overload.
			Qud.API.ConversationsAPI.addSimpleConversationToObject(Keeper, speech.Greeting,
				"Live and drink.", Question: speech.Question, Answer: speech.Answer);
		}

		private static void DescribeOtherBenches(List<GameObject> Benches, GameObject Chosen)
		{
			for (int i = 0; Benches != null && i < Benches.Count; i++)
			{
				if (!ReferenceEquals(Benches[i], Chosen))
					SetBenchDescription(Benches[i], KingdomLocusRules.BenchDescription(
						KingdomLocusRules.KeeperServiceState.OtherGround, null));
			}
		}

		private static void SetBenchDescription(GameObject Bench, string Text)
		{
			Description description = Bench?.GetPart<Description>();
			if (description != null) description.Short = Text;
		}

		/// <summary>Installs at most one attended-only hook. Removing/requiring the part is a
		/// projection change only; all authority fields are nonserialized and must be restamped by
		/// the current active-ground pass after every load or thaw.</summary>
		private static void ConfigureAmbient(List<GameObject> Benches, GameObject Chosen,
			GameObject Keeper, KingdomSystem System, Zone Z, long TimeTicks, bool Enabled)
		{
			for (int i = 0; Benches != null && i < Benches.Count; i++)
			{
				r_KingdomLocusAmbient old = Benches[i].GetPart<r_KingdomLocusAmbient>();
				if (old != null && (!Enabled || !ReferenceEquals(Benches[i], Chosen)))
					Benches[i].RemovePart(old);
			}
			if (!Enabled || !GameObject.Validate(Chosen) || !GameObject.Validate(Keeper)
				|| System?.City == null || Z == null) return;
			string chosenId = Chosen.IDIfAssigned;
			string keeperId = Keeper.IDIfAssigned;
			if (string.IsNullOrEmpty(chosenId) || string.IsNullOrEmpty(keeperId)) return;
			int workId = Simulation.City.KingdomCityRules.StableId(chosenId);
			int residentId = Simulation.City.KingdomResidents.IdOf(Keeper);
			if (workId == 0 || residentId <= 0) return;
			r_KingdomLocusAmbient part = Chosen.RequirePart<r_KingdomLocusAmbient>();
			bool sameAuthority = part.AuthorityEnabled
				&& string.Equals(part.OwnerRealmId, System.RealmId,
					StringComparison.Ordinal)
				&& string.Equals(part.OwnerSettlementId, System.City.SettlementId,
					StringComparison.Ordinal)
				&& string.Equals(part.OwnerZoneId, Z.ZoneID, StringComparison.Ordinal)
				&& part.WorkId == workId && part.KeeperResidentId == residentId
				&& string.Equals(part.KeeperObjectId, keeperId,
					StringComparison.Ordinal);
			if (!sameAuthority)
			{
				part.HasUsed = false;
				part.LastUseTick = 0L;
			}
			part.AuthorityEnabled = true;
			part.OwnerRealmId = System.RealmId;
			part.OwnerSettlementId = System.City.SettlementId;
			part.OwnerZoneId = Z.ZoneID;
			part.WorkId = workId;
			part.KeeperResidentId = residentId;
			part.KeeperObjectId = keeperId;
			part.ConfiguredTick = TimeTicks;
		}
	}
}
