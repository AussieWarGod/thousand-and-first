using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal sealed class KingdomRealmRemovalGroundPlan
	{
		internal Zone Zone;
		internal List<GameObject> Objects = new List<GameObject>();
		internal List<GameObject> MutationObjects = new List<GameObject>();
		internal List<KingdomRemovalRecord> ObjectPreviewRecords =
			new List<KingdomRemovalRecord>();
		internal List<KingdomRemovalRecord> ObjectCompletionRecords =
			new List<KingdomRemovalRecord>();
		internal Dictionary<GameObject, GameObjectBlueprint> Fallbacks =
			new Dictionary<GameObject, GameObjectBlueprint>();
		internal List<GameObject> ExactForeignCitizens = new List<GameObject>();
		internal HashSet<GameObject> RemovedObjects = new HashSet<GameObject>();
		internal HashSet<GameObject> MarketStockRetirements = new HashSet<GameObject>();
		internal HashSet<GameObject> LegendaryMarketRetirements = new HashSet<GameObject>();
		internal List<KingdomStasisVaultRemovalPlan> StasisVaults =
			new List<KingdomStasisVaultRemovalPlan>();
		internal List<KingdomWitnessWorkRemovalPlan> WitnessWorks =
			new List<KingdomWitnessWorkRemovalPlan>();
		internal KingdomCivicMemorySystem WitnessAuthority;
		internal KingdomSurvey WitnessSurvey;
		internal long WitnessRetirementTick;
		internal bool WitnessRetryProgress;
		internal KingdomRelocationRemovalPlan Relocation;
		internal KingdomExternalOwnershipResetPlan ExternalOwnership;
		internal KingdomRemovalRecord LegacyCitizenRecord;
		internal KingdomRemovalRecord SharedFactionRecord;
		internal KingdomRemovalRecord ObjectRecord;
		internal string ProjectedEvidenceDigest;
		internal string RecoveryDigest;
		internal int RetainedObjectCount;
		internal int LegacyCitizenCount;
		internal int OwnedBlueprintCount;
		internal int CustomPartCount;
		internal int ObjectPropertyCount;
		internal int ZonePropertyCount;
		internal int ZonePartCount;
		internal string SharedFaction;
	}
}
