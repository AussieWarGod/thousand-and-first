using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal sealed partial class KingdomLodgingRoomNativeFixture
	{
		private void CheckFurnitureAccess()
		{
			GameObject chair = Create("Chair");
			Require(chair.HasPart("Chair") && !chair.ConsiderSolid(), "chair must be native walkable furniture");
			Place(chair, DoorObject.CurrentCell);
			Check("chair-in-door", KingdomLodgingRules.Closeness.Packed, 1, 0, true);
			Detach(chair);
			Check("door-cleared", KingdomLodgingRules.Closeness.Private, 1, 22, true);
			GameObject chest = Create("Chest");
			Require(!chest.ConsiderSolid(), "bed access probe must use walkable storage");
			Place(chest, At(2, 1)); Place(chair, At(1, 2));
			Check("bed-isolated", KingdomLodgingRules.Closeness.Packed, 1, 20, true);
			Detach(chest); Detach(chair);
			Check("bed-access-restored", KingdomLodgingRules.Closeness.Private, 1, 22, true);
			GameObject cabinet = Create("r_TAF_LodgingRoomCabinet");
			Require(cabinet.Inventory != null && cabinet.Inventory.Objects.Count == 0 && cabinet.ConsiderSolid(),
				"solid control must be an empty native cabinet");
			Place(cabinet, At(4, 2));
			Check("solid-cabinet", KingdomLodgingRules.Closeness.Private, 1, 21, true);
			Detach(cabinet);
			Check("cabinet-removed", KingdomLodgingRules.Closeness.Private, 1, 22, true);
		}
	}
}
