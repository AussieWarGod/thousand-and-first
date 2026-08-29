using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private bool AttendFormerClaimCustody(Zone zone)
		{
			if (!Founded || zone == null || ClaimedZones == null
				|| ClaimedZones.Contains(zone.ZoneID)
				|| !KingdomMaster.AutomaticWorkAllowed(this)
				|| !KingdomExternalOwnershipBindingRuntime.CanOperate(zone, out string _))
				return false;
			KingdomSurvey custody = KingdomSurvey.TakeCustodyOnly(zone);
			using (KingdomSurvey.PassScope scope = custody.BindPass())
				KingdomConstruction.OnLostAuthorityAttendedPass(this, zone, custody);
			return true;
		}
	}
}
