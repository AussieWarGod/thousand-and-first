using System;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>
	/// The grafting hall. Rung 2, and the lab proper: Class I and Class II.
	/// <para>
	/// <b>The verb is on the building and there is no charter hotkey.</b> The Charter's letters are
	/// full at thirty-six and a new entry there would be a chapter rather than a line, so the slate
	/// opens where the work is done &mdash; which is also where the founder is standing when they
	/// want it, and is the same call the mirror-gate's own dedication made.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomGraftingHall : IPart
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
			E.AddAction("Slate", "read the hall's slate", "r_OpenLabSlate", null, 'l', FireOnActor: false, 5);
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
