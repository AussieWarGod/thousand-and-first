using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal sealed class KingdomRealmRemovalFinalPlan
	{
		internal string RealmId;
		internal List<KingdomRemovalRecord> PreviewRecords = new List<KingdomRemovalRecord>();
		internal List<KingdomRemovalRecord> CompletionRecords = new List<KingdomRemovalRecord>();
		internal List<Faction> Factions = new List<Faction>();
		internal List<string> LocatorZoneIds = new List<string>();
		internal List<string> StringStates = new List<string>();
		internal List<string> IntStates = new List<string>();
		internal List<string> Int64States = new List<string>();
		internal List<string> BooleanStates = new List<string>();
		internal List<string> EmptyObjectStates = new List<string>();
		internal List<IGameSystem> Systems = new List<IGameSystem>();
		internal List<string> PlayerRows = new List<string>();
		internal Dictionary<string, string> HostedAuthorityStates =
			new Dictionary<string, string>();
		internal Dictionary<string, string> HostedDepartureStates =
			new Dictionary<string, string>();
		internal int QuestCount;
		internal int RecipeCount;
		internal int JournalCount;
		internal int AbilityCount;
		internal int SystemCount;
	}
}
