using System;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>
	/// The chimeric theatre. Rung 3, Class III, the four named procedures &mdash; and the city's one
	/// purpose (Addendum 22 A1, Design B).
	/// <para>
	/// It carries the same slate as the hall, because it IS the hall with its ceiling raised, and a
	/// second screen for the same act would be a second screen for the same act.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomChimericTheatre : IPart
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
			E.AddAction("Slate", "read the theatre's slate", "r_OpenLabSlate", null, 'l', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenLabSlate" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("lab slate", delegate
				{
					using (KingdomGovernanceScope.Begin(E.Actor))
					{
						KingdomLab.OpenSlate(ParentObject, E.Actor);
					}
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}
}
