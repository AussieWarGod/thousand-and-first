using System;
using XRL.World;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the parts move; the rest of
// the guestbook and the carry-sign stay where the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// Carried by the carry-sign item. Offers the plant action; everything the action actually
	/// does lives in <see cref="ThousandAndFirst.KingdomGuestbook.AttemptPlantCarrySign"/>, the
	/// same split <c>r_FounderBasin</c> uses for founding.
	/// </summary>
	[Serializable]
	public class r_KingdomCarrySign : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Plant carry-sign", "plant carry-sign", "r_PlantCarrySign", null, 'p', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_PlantCarrySign" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomGuestbook.AttemptPlantCarrySign(E.Actor, E.Item);
			}
			return base.HandleEvent(E);
		}
	}
}
