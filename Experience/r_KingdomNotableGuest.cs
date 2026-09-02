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
	/// Carried by a spawned notable guest (<see cref="ThousandAndFirst.KingdomGuestbook"/>'s
	/// <c>SpawnNotableGuest</c>). Offers the one interactive moment a notable presents: the
	/// founder can lodge them into the settlement. Everything the action actually does lives in
	/// <see cref="ThousandAndFirst.KingdomGuestbook.TryLodge"/>; this part is only the event
	/// plumbing, the same split <c>r_KingdomGuest</c> uses for offering water.
	/// </summary>
	[Serializable]
	public class r_KingdomNotableGuest : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID
				&& ID != BeforeDeathRemovalEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (ParentObject.GetIntProperty(ThousandAndFirst.KingdomGuestbook.NotableGuestProperty) == 1)
			{
				E.AddAction("Lodge", "lodge notable guest", "r_LodgeNotableGuest", null, 'l', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_LodgeNotableGuest" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomGuestbook.TryLodge(ParentObject);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			ThousandAndFirst.KingdomGuestLifecycle.ObserveLodgeTargetDeath(ParentObject);
			return base.HandleEvent(E);
		}
	}
}
