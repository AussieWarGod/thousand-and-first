using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace XRL.World.Parts
{
	[Serializable]
	public sealed class r_KingdomRaidDemand : IPart
	{
		public string IncidentId;
		public string ChannelId;
		public int Revision;
		public bool Inert;

		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			IncidentId = null; ChannelId = null; Revision = 0; Inert = true;
			ParentObject?.RemoveProperty(ThousandAndFirst.KingdomRaids.ProjectionMarkerProperty);
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == CanBeReplicatedEvent.ID;
		}

		public override bool HandleEvent(CanBeReplicatedEvent E) { return false; }

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			bool acknowledge;
			if (ThousandAndFirst.KingdomRaids.IsDemandActionable(ParentObject, E.Actor,
				out acknowledge))
				E.AddAction(acknowledge ? "Read and acknowledge demand" : "Answer demand",
					acknowledge ? "read and acknowledge demand" : "answer demand",
					"r_KingdomRaidDemand", null, 'a', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_KingdomRaidDemand" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomRaids.UseDemand(E.Actor, ParentObject);
				E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}
	}
}
