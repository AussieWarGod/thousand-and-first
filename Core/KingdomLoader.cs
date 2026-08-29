using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	[HasCallAfterGameLoaded]
	public static class KingdomLoader
	{
		[CallAfterGameLoaded]
		public static void RequireKingdomSystem()
		{
			KingdomData.Reload();
			KingdomGreatArchive.EnsureRegistered();
			// The post-import roster boundary already proved or recovered these carriers.
			// Never resurrect a prepared-removal roster from this later load callback.
			KingdomSystem kingdomSystem = The.Game?.GetSystem<KingdomSystem>();
			KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
			KingdomCivicMemorySystem memory =
				The.Game?.GetSystem<KingdomCivicMemorySystem>();
			if (kingdomSystem == null || seal == null || memory == null) return;
			bool founded = kingdomSystem != null && kingdomSystem.Founded;
			KingdomLoadReconciliationMode loadMode = KingdomLoadReconciliationRules.Select(
				founded, founded && KingdomMaster.NewWorkAllowed(kingdomSystem));
			if (loadMode != KingdomLoadReconciliationMode.None)
			{
				long tick = The.Game.TimeTicks;
				if (loadMode == KingdomLoadReconciliationMode.Full)
				{
					Faction realm = Factions.GetIfExists(kingdomSystem.KingdomFactionName);
					if (!KingdomPolityRuntime.TryEnsureFoundation(kingdomSystem, realm,
						tick, out string polityFailure))
						KingdomLog.Log("polity: load reconciliation refused (" +
							polityFailure + ")");
					else if (!KingdomPolityActiveRuntime.TryReconcile(kingdomSystem,
						tick, out polityFailure))
						KingdomLog.Log("polity: active load reconciliation refused (" +
							polityFailure + ")");
					if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(kingdomSystem,
						tick, out string experienceFailure))
						KingdomLog.Log("experience: option reconciliation refused (" +
							experienceFailure + ")");
				}
				else if (!KingdomPolityActiveRuntime.TryReconcileCommittedCapacity(
					kingdomSystem, tick, out string committedFailure))
					KingdomLog.Log("polity: committed capacity reconciliation refused (" +
						committedFailure + ")");
			}
			if (founded) KingdomFirstFeastRuntime.ReconcileBestEffort(kingdomSystem);
			if (founded && The.Player != null)
			{
				KingdomSuccession succession = The.Game?.GetSystem<KingdomSuccession>();
				if (succession != null && succession.WithholdsCharter(kingdomSystem))
					The.Player.GetPart<KingdomCharterPart>()?.RemoveAbility();
				else
					The.Player.RequirePart<KingdomCharterPart>().EnsureAbility();
			}
			if (seal != null && KingdomMaster.AutomaticWorkAllowed(kingdomSystem))
			{
				seal.ReconcileProfile();
			}
		}
	}

}
