using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// How far what a work gives actually carries. Derived from the ground it stands on
	/// (<see cref="KingdomPlotRules.PlotSize"/>) and where in its own improvement chain it sits,
	/// never authored per design unless an author insists.
	/// </summary>
	public enum ReachBand
	{
		/// <summary>Its own footprint and nothing beyond it. The wayside statue, the cask rack,
		/// the shrine stone.</summary>
		Plot = 0,

		/// <summary>The cluster of built ground it stands in &mdash; what the layout grammar
		/// gathers around it, measured rather than declared
		/// (<see cref="KingdomReachRules.QuarterMarks"/>).</summary>
		Quarter = 1,

		/// <summary>Everything standing in the same zone.</summary>
		Zone = 2,

		/// <summary>Every zone the seated city claims.</summary>
		City = 3,

		/// <summary>Every zone the realm holds, the second city included.</summary>
		Realm = 4
	}

	/// <summary>
	/// How near a place is to a work, in the only terms reach cares about. Ordered from farthest
	/// to nearest so a covering test is one comparison and can never disagree with itself.
	/// </summary>
	public enum ReachRelation
	{
		/// <summary>Not the realm's ground at all.</summary>
		Elsewhere = 0,

		/// <summary>The realm holds it, but the work's own city does not.</summary>
		SameRealm = 1,

		/// <summary>A zone of the work's own city, but not the work's own zone.</summary>
		SameCity = 2,

		/// <summary>The work's own zone, outside its quarter.</summary>
		SameZone = 3,

		/// <summary>Inside the cluster of built ground the work stands in.</summary>
		SameQuarter = 4,

		/// <summary>On the work's own footprint.</summary>
		SamePlot = 5
	}

	/// <summary>
	/// What shades one piece of ground: every lift in reach of it, folded by kind, and which kind
	/// is the loudest. The readable half of Addendum 6 &mdash; a quarter's character is a thing
	/// the founder can be told, not an invisible modifier.
	/// </summary>
	public sealed class GroundCharacter
	{
		/// <summary>One entry per kind, amounts summed, in
		/// <see cref="KingdomReachRules.LiftOrder"/> and then first-seen order. Never null.
		/// </summary>
		public List<KindAmount> Lifts = new List<KindAmount>();

		/// <summary>Every lift together.</summary>
		public int Total;

		/// <summary>The loudest kind, or null when nothing shades this ground.</summary>
		public string Dominant;

		/// <summary>How much of <see cref="Dominant"/> there is. Zero when there is none.
		/// </summary>
		public int DominantAmount;
	}

}
