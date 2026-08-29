using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private bool ExternalOwnershipAllows(Zone Site)
		{
			if (!Founded || Site == null || !OwnedZone(Site.ZoneID)) return true;
			KingdomExternalOwnershipBindingRuntime.FinishPublishedClaimStage(Site);
			return KingdomExternalOwnershipBindingRuntime.CanOperate(Site, out _);
		}
	}
}
