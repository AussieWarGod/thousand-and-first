using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name. This part is
// only ever added in code (see r_KingdomImprovement's own header for why), but it lives here
// anyway alongside every other part this mod ships: a part whose namespace depends on how it
// happened to be attached is a trap waiting for the first blueprint that names it.
namespace XRL.World.Parts
{
	/// <summary>
	/// A cleared lot: everything the settlement raised on this reserved ground has come down, and the
	/// rect, its lane, and the ground itself stand ready for whatever the founder chooses next.
	/// See BUILDING-CATALOGUE-BRIEF.md's 2026-08-21 addendum, "the plot as socket".
	/// <para>
	/// Carries no geometry of its own. The rect a later stake needs rides on the same
	/// <c>KingdomPlots.PlotX1Property</c> family every laid plot already carries &mdash;
	/// <c>KingdomSocket</c> stamps them with <c>KingdomPlots.StampRect</c> the moment this part is
	/// attached &mdash; so <c>KingdomPlots.ReadPlots</c>, the lane rule, and the road budget all
	/// count a socket exactly as they count a standing plot. Named <c>GameObject</c> properties
	/// preserve the lot's frozen type, actual size, and facing without changing this positional
	/// part layout; save-era sockets without that receipt remain visibly legacy and untyped.
	/// <see cref="LastDesignKey"/> is purely descriptive: nothing anywhere reads it to decide
	/// anything, only to tell the founder what stood here.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomSocket : IPart
	{
		/// <summary>Registry key of the design that last stood here, if it is still known.
		/// Null when nothing was ever recorded, or when the design has since left the catalogue.</summary>
		public string LastDesignKey;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append("\n{{rules|This ground stands as "
				+ KingdomSocket.SocketLotLabel(ParentObject)
				+ ", staked out and ready for a matching plan.}}");
			return base.HandleEvent(E);
		}
	}
}
