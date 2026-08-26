using System;

using ThousandAndFirst;

// XRL.World.Parts, for the reason r_KingdomPlot, r_KingdomSeed and r_KingdomMirrorGate all state:
// GamePartBlueprint resolves a part named in XML as exactly "XRL.World.Parts.<Name>" and tries no
// other name. Only the parts move; everything they do lives in ThousandAndFirst.KingdomLab below.
namespace XRL.World.Parts
{
	/// <summary>
	/// The butcher's slab. Rung 0, and not the lab: the work that turns what the founder drags home
	/// into parts.
	/// <para>
	/// It invents no butchery. Vanilla's <c>Butcherable</c> and <c>Corpse</c> already do the whole
	/// job, gated on the founder's own <c>CookingAndGathering_Butchery</c> skill, and Addendum 11(c)
	/// says inherit rather than reinvent. What the slab adds is the one thing vanilla has no opinion
	/// about: reading what the creature was BEARING before the knife, and stamping it onto what
	/// comes off, so that a part can still be a part a season later.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomButcherSlab : IPart
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
			E.AddAction("Dress", "dress a carcass on the slab", "r_DressCarcass", null, 'd', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_DressCarcass" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("butcher slab", delegate
				{
					KingdomLab.Dress(E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}
}
