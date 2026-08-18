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
			KingdomSystem kingdomSystem = The.Game?.RequireSystem<KingdomSystem>();
			if (kingdomSystem != null && kingdomSystem.Founded && The.Player != null)
			{
				The.Player.RequirePart<KingdomCharterPart>().EnsureAbility();
			}
		}
	}

	[PlayerMutator]
	public class KingdomNewGameLoader : IPlayerMutator
	{
		public void mutate(GameObject player)
		{
			The.Game?.RequireSystem<KingdomSystem>();
		}
	}
}
