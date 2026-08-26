using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the part moves; the
// settlement-side resolver below stays where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// Ground the founder has ordered cleared: a rect, the effort still owed on it, and the day
	/// the crew last swung at it. Carries no <c>WantTurnTick</c> and never will &mdash; clearing
	/// is crew work, and crew work is resolved only from the settlement's ordinary
	/// <c>ZoneActivatedEvent</c> pass, through
	/// <see cref="ThousandAndFirst.KingdomMaterials.OnSettlementPass"/>, so a founder who is not
	/// there is never spending hands they never assigned.
	/// </summary>
	[Serializable]
	public class r_KingdomClearance : IPart
	{
		/// <summary>West edge of the ordered rect, in zone cells, inclusive.</summary>
		public int X1;

		/// <summary>North edge of the ordered rect, in zone cells, inclusive.</summary>
		public int Y1;

		/// <summary>East edge of the ordered rect, in zone cells, inclusive.</summary>
		public int X2;

		/// <summary>South edge of the ordered rect, in zone cells, inclusive.</summary>
		public int Y2;

		/// <summary>Effort still owed. The order is finished the pass this reaches zero.</summary>
		public int EffortLeft;

		/// <summary>Effort the order was assessed at, kept so the founder can be told how far
		/// along it is without the settlement having to re-walk the ground to find out.</summary>
		public int EffortTotal;

		/// <summary>Tick the crew last worked this ground. Zero until the first pass sees it.</summary>
		public long LastWorkedTick;

		/// <summary>Set once the founder has been told there is nobody free to clear. Cleared the
		/// moment hands are free again, so the reason is given once per stall and not once per
		/// visit. STANDARDS 7b.</summary>
		public bool NoHandsAnnounced;

		/// <summary>Set once the founder has been told something is standing in the finished
		/// rect that the settlement will not touch. Cleared when the obstruction goes.</summary>
		public bool BlockedAnnounced;
	}
}
