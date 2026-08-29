using System;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Shared interaction surface for all five purpose roots; authority stays in the
	/// canonical realm pair receipt, never in a duplicated part-local boolean.</summary>
	[Serializable]
	public sealed class r_KingdomPurposeWork : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Pair", "read or bind the reciprocal purpose pair",
				"r_OpenPurposePair", null, 'p', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenPurposePair" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("purpose pair", delegate
				{
					using (KingdomGovernanceScope.Begin(E.Actor))
						KingdomPurpose.OpenPortfolio(ParentObject, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}
}
