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
			The.Game?.RequireSystem<KingdomSystem>();
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
