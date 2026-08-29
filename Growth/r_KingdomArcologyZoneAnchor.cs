using System;
using ThousandAndFirst;
using XRL.UI;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>Loaded-floor witness. It never loads its exterior or simulates an actor.</summary>
	[Serializable]
	public sealed class r_KingdomArcologyZoneAnchor : IPart
	{
		public string LotKey;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == ZoneActivatedEvent.ID
				|| ID == ZoneThawedEvent.ID || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID;
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			KingdomHostedArcologyVisual.Reconcile(ParentObject.CurrentZone, LotKey);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneThawedEvent E)
		{
			KingdomHostedArcologyVisual.Reconcile(ParentObject.CurrentZone, LotKey);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Hosted lot", "read the floor slate", "r_ReadHostedFloor",
				null, 'r', FireOnActor: false, 20);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_ReadHostedFloor" && E.Actor != null && E.Actor.IsPlayer())
			{
				GameObject root = KingdomHostedArcology.RootOf(ParentObject.CurrentZone);
				Popup.Show(root == null ? "The exterior authority is not loaded."
					: KingdomHostedArcology.Status(root.GetPart<r_KingdomArcology>()));
			}
			return base.HandleEvent(E);
		}
	}
}
