using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Object-local proof that one exact SocialRoles string came from one office row. A
	/// true clone copies this marker but not its frozen body id, making clone cleanup source-owned.</summary>
	[Serializable]
	public sealed class r_KingdomOfficeProjection : IPart
	{
		public string RealmId = "";
		public string SettlementId = "";
		public int Generation;
		public int ResidentId;
		public string BodyObjectId = "";
		public string RoleText = "";
		public bool OwnsRole;

		public bool Matches(KingdomSystem System, KingdomCivicOfficeReceipt Receipt,
			GameObject Body)
		{
			return System != null && Receipt != null && Body != null
				&& RealmId == System.RealmId && SettlementId == Receipt.SettlementId
				&& Generation == Receipt.Generation && ResidentId == Receipt.HolderResidentId
				&& BodyObjectId == Receipt.HolderObjectId
				&& Body.IDIfAssigned == BodyObjectId
				&& RoleText == KingdomOfficeRuntime.RoleFor(Receipt)
				&& OwnsRole == Receipt.OwnsRole;
		}
	}
}
