using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Object-local compare-and-swap proof for one optional remembrance carrier. It owns
	/// only the exact display, description and memorial property values it froze.</summary>
	[Serializable]
	public sealed class r_KingdomRemembranceProjection : IPart
	{
		public string RealmId = "";
		public string SettlementId = "";
		public int Generation;
		public int SubjectResidentId;
		public string CarrierObjectId = "";
		public string CarrierZoneId = "";
		public string PriorDisplayName = "";
		public string PriorDescription = "";
		public bool HadMemorialProperty;
		public string PriorMemorialFor = "";
		public string ProjectedDisplayName = "";
		public string ProjectedDescription = "";
		public string ProjectedMemorialFor = "";

		public bool MatchesAuthority(KingdomSystem System, KingdomRemembranceReceipt Receipt,
			GameObject Carrier)
		{
			return System != null && Receipt != null && Carrier != null
				&& RealmId == System.RealmId && SettlementId == Receipt.SettlementId
				&& Generation == Receipt.Generation
				&& SubjectResidentId == Receipt.SubjectResidentId
				&& CarrierObjectId == Receipt.CarrierObjectId
				&& Carrier.IDIfAssigned == CarrierObjectId
				&& CarrierZoneId == Receipt.CarrierZoneId;
		}
	}
}
