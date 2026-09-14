using System;
using System.Reflection;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Read-only placement verdicts at the actual paid floor insertion boundary.</summary>
	[HarmonyPatch(typeof(KingdomArchitectureStamper), "CanInsert")]
	internal static class KingdomPaidHousingFloorProbe
	{
		internal static bool Passed;
		private static bool Entered;
		[HarmonyPrefix]
		internal static void Prefix(GameObject Owner, Zone Z, Cell Cell, string Lot, string Hash,
			ArchitecturePlacement Placement, ArchitectureLayoutSnapshot Snapshot, MethodBase __originalMethod)
		{
			if (Entered || Passed || !KingdomPaidHousingCatalogue.Changed
				|| !ReferenceEquals(The.Game, KingdomPaidHousingNativeProvider.Owner)
				|| Owner?.GetStringProperty(KingdomConstruction.ReceiptProperty) != KingdomPaidHousingNativeProvider.JobId
				|| Placement.Layer != ArchitectureLayer.Ground || Cell != Owner.CurrentCell) return;
			GameObject chest = null;
			Entered = true;
			try
			{
				Require(KingdomPaidHousingFault.Outstanding, "floor probe preceded controlled paid retry");
				object[] args = { Owner, Z, Cell, Lot, Hash, Placement, Snapshot, null };
				Require(Read(__originalMethod, args), "exact paid predecessor refused floor: " + args[7]);
				chest = GameObject.Create("Chest");
				Require(GameObject.Validate(chest) && chest.Inventory != null, "foreign chest fixture absent");
				Cell.AddObject(chest, NoStack: true);
				Require(chest.CurrentCell == Cell && KingdomPlots.ReadObject(chest) == KingdomPlotRules.GroundKind.Held,
					"foreign chest does not occupy the actual floor cell");
				Require(!Read(__originalMethod, args) && !string.IsNullOrEmpty(args[7] as string),
					"foreign furniture did not refuse floor insertion");
				chest.SetStringProperty(KingdomConstruction.ReceiptProperty, KingdomPaidHousingNativeProvider.JobId);
				Require(!Read(__originalMethod, args) && !string.IsNullOrEmpty(args[7] as string),
					"borrowed payment receipt authorized foreign furniture");
				Remove(chest); chest = null;
				Require(Read(__originalMethod, args), "exact paid predecessor did not recover after foreign furniture removal");
				Require(KingdomScenarioJournal.Append("paid-housing-floor-access", true,
					"own-predecessor=accepted; foreign-furniture=refused; borrowed-receipt=refused; restored=exact") == null,
					"floor access journal unavailable");
				Passed = true;
			}
			catch (Exception error) { KingdomPaidHousingFault.Fault(error); }
			finally { Remove(chest); }
		}
		private static bool Read(MethodBase method, object[] args)
		{
			args[7] = null;
			return (bool)method.Invoke(null, args);
		}
		private static void Remove(GameObject chest)
		{
			if (!GameObject.Validate(chest)) return;
			// Remove borrowed authority before native graveyards retain the test object's tombstone.
			chest.RemoveStringProperty(KingdomConstruction.ReceiptProperty);
			chest.CurrentCell?.RemoveObject(chest);
			chest.Obliterate();
		}
		private static void Require(bool value, string failure) => KingdomPaidHousingNativeProvider.Require(value, failure);
	}
}
