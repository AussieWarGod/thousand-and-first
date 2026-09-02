using System;
using XRL.World;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently.
namespace XRL.World.Parts
{
	/// <summary>
	/// Carried by a spawned guest (<see cref="ThousandAndFirst.KingdomLocus"/>'s
	/// <c>SpawnGuest</c>). Adds the one interactive moment a guest offers: the founder can offer
	/// them water from the settlement's own stores. Everything the action actually does lives in
	/// <see cref="ThousandAndFirst.KingdomLocus.OfferGuestWater"/>; this part is only the event
	/// plumbing, the same split <c>r_FounderBasin</c> and <c>r_KingdomScaffold</c> use.
	/// </summary>
	[Serializable]
	public class r_KingdomGuest : IPart
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
			if (ParentObject.GetIntProperty("KingdomGuestOffered") == 0)
			{
				E.AddAction("Offer Water", "offer water", "r_OfferGuestWater", null, 'o', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OfferGuestWater" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomLocus.OfferGuestWater(ParentObject);
			}
			return base.HandleEvent(E);
		}
	}
}
