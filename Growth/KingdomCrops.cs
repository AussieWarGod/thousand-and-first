using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the seed chain: what commits a field, what physically stands in
	/// one, what a gathering does with what it brings in, and how a harvest reaches a larder in a
	/// zone nobody is standing in. The rules it asks are all in
	/// <see cref="KingdomCropRules"/>; the state one field carries is on
	/// <see cref="XRL.World.Parts.r_KingdomPlot"/>; the per-pass walk is
	/// <see cref="KingdomPlot.OnSettlementPass"/>.
	/// <para>
	/// <b>The protection law, in the one place it binds hardest.</b> A committed seed is the
	/// founder's designation, exactly as a dedicated cask is. Nothing here sows a field the
	/// founder did not sow, nothing takes a seed the founder did not commit, and the only path
	/// out of a committed field is <see cref="Withdraw"/> &mdash; which is the founder's own
	/// action and hands the seed back. The rows this file lays are objects it created and marked
	/// (<see cref="RowProperty"/>), which is the only class of object a kingdom system may
	/// destroy.
	/// </para>
	/// </summary>
	public static partial class KingdomCrops
	{
	}
}

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the parts move; everything
// they do lives in ThousandAndFirst.KingdomCrops above.
namespace XRL.World.Parts
{
	/// <summary>
	/// Carried by every seed item. Offers the one thing a seed is for.
	/// </summary>
	[Serializable]
	public class r_KingdomSeed : IPart
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
			E.AddAction("Sow", "sow in a field", "r_SowSeed", null, 's', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_SowSeed" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomCrops.AttemptSow(E.Actor, E.Item ?? ParentObject);
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// Merged onto the vanilla wild plants whose species the settlement grows, so a founder who
	/// walks past a watervine can start a farm with what the marsh already offered. One plant is
	/// one seed, forever; a plant somebody owns gives nothing.
	/// </summary>
	[Serializable]
	public class r_KingdomWildSeed : IPart
	{
		/// <summary>The seed blueprint this species carries. Declared in XML beside the part, so
		/// the map from plant to seed is data rather than a switch nobody can extend.</summary>
		public string Seed;

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
			if (!string.IsNullOrEmpty(Seed) && ParentObject.GetIntProperty(ThousandAndFirst.KingdomCrops.WildSeedTakenProperty) != 1)
			{
				E.AddAction("Gather Seed", "gather seed", "r_GatherWildSeed", null, 'g', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_GatherWildSeed" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomCrops.TakeWildSeed(E.Actor, ParentObject, Seed);
			}
			return base.HandleEvent(E);
		}
	}
}
