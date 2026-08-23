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
			KingdomSystem kingdomSystem = The.Game?.RequireSystem<KingdomSystem>();
			KingdomSeal seal = The.Game?.RequireSystem<KingdomSeal>();
			if (kingdomSystem != null && kingdomSystem.Founded && The.Player != null)
			{
				The.Player.RequirePart<KingdomCharterPart>().EnsureAbility();
			}
			seal?.ReconcileProfile();
		}
	}

	[PlayerMutator]
	public class KingdomNewGameLoader : IPlayerMutator
	{
		public void mutate(GameObject player)
		{
			The.Game?.RequireSystem<KingdomSystem>();
			The.Game?.RequireSystem<KingdomSeal>();
		}
	}
}
