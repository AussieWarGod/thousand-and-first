using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterials
	{
		/// <summary>Explains the original refusal using the actual carried job and current owner.
		/// Read-only: no closure, receipt replacement or strike publication occurs here.</summary>
		private static string StrikeReceiptFailure(KingdomSystem System, Zone Zone,
			GameObject Building, KingdomConstructionJob Job)
		{
			if (KingdomConstruction.Owns(System, Zone, Job)
				&& KingdomConstructionRules.HasPendingOwnTerminalClosure(Job,
					KingdomConstruction.OwnerOf(System), Zone.ZoneID,
					Building.GetStringProperty(KingdomConstruction.ReceiptProperty),
					Building.IDIfAssigned))
				return KingdomConstructionRules.PendingStrikeClosureMessage;
			return "That building carries another construction receipt.";
		}
	}
}
