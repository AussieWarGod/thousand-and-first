using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// The realm's one carry-sign haul in flight: materials already swept from their origin,
	/// waiting to be poured into the destination settlement's stockpiles the next time it
	/// activates and the haul is due. Held on <see cref="KingdomSystem"/> directly, realm-level
	/// like <c>KingdomSystem.Manifest</c> — a haul is addressed to an immutable settlement id;
	/// the carried name is prose only, so it survives renames and every seat swap untouched.
	/// </summary>
	[Serializable]
	public class KingdomCarryHaul
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>Zone the sign was planted in, kept for the chronicle and for nothing the
		/// resolver reads back.</summary>
		public string OriginZoneID;

		public int OriginX;

		public int OriginY;

		/// <summary>Immutable destination authority. The name below is prose only.</summary>
		public string DestinationSettlementId;

		/// <summary>The settlement's frozen display name, used only in prose.</summary>
		public string DestinationSettlementName;

		public long PlantedTick;

		/// <summary>Absolute tick the haul is ready to resolve. No expiry beyond this — absence
		/// never punishes; a haul left unresolved simply waits for the next attended pass of its
		/// destination, exactly as a raid warning waits out an absent founder.</summary>
		public long DueTick;

		public int Mud;

		public int Brush;

		public int Timber;

		public int Stone;

		public int Marble;

		public int Scrap;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomCarryHaul));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomCarryHaul));
		}
#endif
	}
}

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
