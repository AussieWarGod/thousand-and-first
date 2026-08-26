using System;
using System.Collections.Generic;

using ThousandAndFirst;

// XRL.World.Parts, for the reason r_KingdomPlot, r_KingdomMirrorGate and the lab's four all state:
// GamePartBlueprint resolves a part named in XML as exactly "XRL.World.Parts.<Name>" and tries no
// other name. Only the part moves; everything it does lives in ThousandAndFirst.KingdomCrown below.
namespace XRL.World.Parts
{
	/// <summary>
	/// The crown hall: a room built to hold one thing, and the city that holds it is the capital.
	/// <para>
	/// <b>The crown is a building</b> (Addendum 22 A4), which is rule (b) of
	/// END-STATE-CITIES-RESEARCH &sect;5.2 &mdash; Civ's movable Palace, the only designation rule
	/// in the comparables with actual praise attached, and praised for exactly what this mod wants:
	/// the roleplay and the strategic consequence at once. Raising the hall is the whole project;
	/// setting the crown down in it is the moment, and moving the crown means raising another hall
	/// somewhere else and walking there.
	/// </para>
	/// <para>
	/// The part carries no state of its own. Which hall holds the crown is a REALM fact and is
	/// carried as one (<c>KingdomCrownRules.RegisterStateKey</c>), because the city that keeps it is
	/// dormant most of the time and a field on a dormant object cannot answer a menu.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomCrownHall : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(KingdomCrown.DescriptionLine(ParentObject));
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Crown", KingdomCrown.TakeUpLabel(ParentObject), "r_TakeUpCrown", null, 'c', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_TakeUpCrown" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("crown", delegate
				{
					KingdomCrown.TakeUp(ParentObject);
				});
				return true;
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// The arch's own anchoring discipline, for the same reason: a building fires EnteredCell
		/// exactly once, at placement, and that is where its ground becomes a name the realm can
		/// write down. Cheap and idempotent, so it is run rather than scheduled.
		/// </summary>
		public override bool FireEvent(Event E)
		{
			if (E.ID == "EnteredCell")
			{
				KingdomSystem.Guard("crown anchor", delegate
				{
					KingdomCrown.Anchor(ParentObject);
				});
			}
			return base.FireEvent(E);
		}
	}
}
