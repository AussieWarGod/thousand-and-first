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
	public sealed class r_KingdomRaiderObjective : IPart
	{
		public string OperationId;
		public string IncidentId;
		public string TargetObjectId;
		public int TargetX;
		public int TargetY;

		public r_KingdomRaiderObjective() { }

		public r_KingdomRaiderObjective(string operationId, string incidentId,
			string targetObjectId, int targetX, int targetY)
		{
			OperationId = operationId; IncidentId = incidentId;
			TargetObjectId = targetObjectId; TargetX = targetX; TargetY = targetY;
		}

		public override bool WantTurnTick() { return true; }

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeDeathRemovalEvent.ID;
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			ThousandAndFirst.KingdomRaids.RaiderDying(ParentObject, this);
			return base.HandleEvent(E);
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (!ThousandAndFirst.KingdomMaster.AutomaticWorkAllowed(
				The.Game?.GetSystem<ThousandAndFirst.KingdomSystem>())) return;
			ThousandAndFirst.KingdomRaids.StepRaider(ParentObject, this, TimeTick);
		}
	}
}
