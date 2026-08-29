using System;
using ThousandAndFirst;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>Fieldless interaction surface for the zone-owned relocation receipt.</summary>
	[Serializable]
	public sealed class r_KingdomRelocationFrame : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == GetShortDescriptionEvent.ID
				|| ID == ZoneActivatedEvent.ID || ID == ZoneThawedEvent.ID
				|| ID == OnDestroyObjectEvent.ID;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Ring call", "inspect or hold the whole-lot move",
				"r_OpenHeartRelocation", null, 'r', FireOnActor: false, 20);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenHeartRelocation" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomRelocation.OpenFrame(ParentObject); E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append("\n\n").Append(KingdomRelocation.FrameDescription(ParentObject));
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			KingdomRelocation.ReconcileZone(ParentObject?.CurrentZone); return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneThawedEvent E)
		{
			KingdomRelocation.ReconcileZone(ParentObject?.CurrentZone); return base.HandleEvent(E);
		}

		public override bool HandleEvent(OnDestroyObjectEvent E)
		{
			KingdomRelocation.FrameDestroyed(ParentObject); return base.HandleEvent(E);
		}

		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }
	}
}
