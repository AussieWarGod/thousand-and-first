using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of plots: reading real ground into the pure geometry's terms,
	/// refusing ground the settlement may not take, and STAMPING a building over a rect in stages
	/// &mdash; staked, cleared, framed, walled, done.
	/// <para>
	/// This never calls vanilla's <c>PlaceHut</c>, and it never will. <c>PlaceHut</c> opens with
	/// <c>ClearRect</c> and lays its walls with <c>ClearAndAddObject</c>
	/// (<c>ZoneBuilderSandbox.cs:647-687</c>): it deletes whatever stands on the ground it is
	/// handed. That is correct for a zone builder running before a player has ever seen the zone,
	/// and it is the exact opposite of the protection law (STANDARDS 7). Every cell this file
	/// touches was surveyed first, and any cell holding something the settlement may not take
	/// refuses the whole plot by name and position (STANDARDS 7b).
	/// </para>
	/// <para>
	/// What the survey WILL clear is the ground itself: brush, trees, rock, marble seams, and
	/// somebody else's collapsed walls. That is the founder's own explicit designation &mdash;
	/// they commissioned a building on that ground &mdash; and it is the only source of building
	/// material a settlement without a mine has. Anything the table cannot name a yield for is
	/// <c>Held</c>, which refuses; the yield table is the allow-list, not a filter over one.
	/// </para>
	/// <para>
	/// Migration honesty: nothing already standing becomes a plot. A settlement raised before this
	/// existed is a scatter of single-cell works and stays exactly that, working exactly as it
	/// did. Plots begin with the next thing built.
	/// </para>
	/// </summary>
	/// <summary>One mutation-free, exact plot quote shown before any water or material moves.</summary>
	public sealed class KingdomPlotQuote
	{
		public KingdomPlotRules.PlotRect Rect;
		public KingdomPlotRules.PlotSize StakedSize;
		public KingdomLayoutRules.LayoutOutcome Outcome;
		public KingdomArchitectureIntent Architecture;
		public string Payload;
		public long LabourTicks;
		public int WaterDrams;
		public KingdomMaterialDebitCost MaterialClaim;
		public int MainX;
		public int MainY;
		/// <summary>Exact cross-city cargo/site commitment, null for ordinary buildings.</summary>
		public string PurposeReceipt;
	}
}
