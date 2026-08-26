using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of worn ground: reading which errands a settlement's own shape
	/// implies, laying the day's walking on the cells those errands cross, letting the ground
	/// show it, and &mdash; only when the founder says so &mdash; paving a path in the material
	/// the settlement builds its walls in.
	/// <para>
	/// No road is ever drawn. The plot grammar already keeps a lane around every plot
	/// (<see cref="KingdomPlotRules.RoadMargin"/>) and the gap between two reserved rects IS the
	/// road; all this does is notice which gaps people cross. Everything resolves on the attended
	/// <c>ZoneActivatedEvent</c> pass out of a stored tick stamp, so a settlement wears its ways
	/// at exactly the rate it was lived in &mdash; the full elapsed, uncapped
	/// (<c>KingdomRules.ElapsedDays</c>), because errands are walked whether or not the founder
	/// is there to watch them being walked (Addendum 8 clause 1).
	/// <para>
	/// What keeps a season away from wearing a canyon is not a ceiling on the calendar but the
	/// labour term the formula already had: traffic is WALKERS x days, walkers come from the
	/// settlement's own population and its own errands, and a settlement with nobody in it walks
	/// nowhere however long the stretch (Addendum 8 clause 2). The per-pass bounds
	/// (<c>KingdomRoadRules.MaxRoutesPerPass</c>, <c>MaxFloorChangesPerPass</c>) stay exactly
	/// what they always were: loop guards on one visit's work, never forgiveness.
	/// </para>
	/// </para>
	/// <para>
	/// The protection law (STANDARDS 7) is the shape of this file, not a check inside it. Wear
	/// only ever ADDS a floor object of ours to a cell that <c>KingdomPlots.ReadGround</c> calls
	/// bare, that nobody owns, that holds no liquid, and that lies in no plot &mdash; and the only
	/// objects it ever destroys are its own, marked with <see cref="PathStateProperty"/>. The
	/// ground the cell already had is never cleared, exactly as vanilla's own
	/// <c>RoadBuilder</c> and <c>JoppaOutskirts</c> add a <c>DirtPath</c> over what is there
	/// rather than replacing it (<c>RoadBuilder.cs:168</c>, <c>JoppaOutskirts.cs:332</c>). Nothing
	/// here ever calls <c>PlaceHut</c> or <c>ClearRect</c>.
	/// </para>
	/// <para>
	/// State lives on the zone, not on the settlement, and deliberately: ways are a property of
	/// ground, and a realm's second city has ground of its own. Zone properties are serialized by
	/// <c>ZoneManager</c> (<c>ZoneManager.cs:507-516</c> and <c>:677-688</c>) and are the idiom
	/// the rite ground already uses (<c>KingdomPlots.RiteXProperty</c>), so this system adds no
	/// serialized field to any part or system and cannot move anyone's save layout.
	/// </para>
	/// </summary>
	public static partial class KingdomRoads
	{
	}
}
