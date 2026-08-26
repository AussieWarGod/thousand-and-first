using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// One <c>&lt;building&gt;</c> entry as the validator needs to see it: what
	/// <c>KingdomRules.BuildEntry</c> carries, plus the plot spec, the material cost, the
	/// equilibrium contribution, and the successor its chain names.
	/// <para>
	/// Deliberately a separate shape rather than a widened <c>BuildEntry</c>. The validator reads
	/// the file as a whole and needs four registries side by side &mdash; the catalogue, the plot
	/// specs, the material costs, and the upgrade chains &mdash; which are four different tables
	/// at load time. A caller fills one of these per entry and hands the list over; nothing here
	/// ever reads a registry, and nothing here needs the engine.
	/// </para>
	/// </summary>
	public class CatalogueEntry
	{
		public string Key;

		public string DisplayName;

		public string Category = "civic";

		/// <summary>The raw <c>Styles</c> attribute: a comma list, or <c>all</c>.</summary>
		public string Styles = "common";

		/// <summary>The authored <c>MinStage</c>. The stage a design is actually reachable at is
		/// <see cref="KingdomCatalogueRules.EffectiveMinStage"/>, which also accounts for the fact
		/// that a camp cannot lay a large plot however early the design says it may be raised.
		/// </summary>
		public GrowthStage MinStage;

		/// <summary><c>PlotSize.None</c> for a single-cell design &mdash; a wall segment, a
		/// tower, anything raised as one object on one cell.</summary>
		public KingdomPlotRules.PlotSize Plot;

		/// <summary>True for a plot with no roof over it: fields, yards, reservoirs, markets, and
		/// the salt-pan.</summary>
		public bool Open;

		/// <summary>Population table the finished interior is furnished from, or null.</summary>
		public string Contents;

		public int CostDrams;

		/// <summary>The raw <c>Materials</c> attribute, read by
		/// <c>KingdomMaterialRules.TryParseMaterialCost</c>.</summary>
		public string Materials;

		/// <summary>The raw <c>Carries</c> attribute: a comma list of <c>support:settlers</c>.
		/// </summary>
		public string Carries;

		public int Staff;

		public string Manning = "scaled";

		public int Defence;

		/// <summary>The <c>UpgradesTo</c> key, or null for a design that never changes.</summary>
		public string SuccessorKey;

		/// <summary>
		/// Width of the footprint this tier declares, or zero for a design that fills its plot.
		/// The footprint belongs to the building's tier; the plot is only the envelope it must fit
		/// inside, and the yard is whatever the tier does not cover.
		/// </summary>
		public int FootprintWidth;

		/// <summary>Height of the declared footprint. See <see cref="FootprintWidth"/>.</summary>
		public int FootprintHeight;

		/// <summary>What stands over the footprint. Meaningless unless
		/// <see cref="RoofDeclared"/>, which is the only state that can contradict anything.
		/// </summary>
		public KingdomPlotRules.RoofState Roof = KingdomPlotRules.RoofState.Walled;

		/// <summary>Whether the tier declared a roof of its own. Only a declared roof can
		/// contradict a design that needs weather; a design that claimed nothing is raised exactly
		/// as it always was.</summary>
		public bool RoofDeclared;

		/// <summary>The design's <c>Sky</c> flag: it needs sun, wind, or rain.</summary>
		public bool RequiresSky;

		/// <summary>
		/// How many <c>&lt;building&gt;</c> declarations this design is the merge of
		/// (<see cref="KingdomMergeRules"/>). One for a design only its own file declares; two or
		/// more means a fault reported here may belong to a file the base catalogue's author never
		/// saw, which the findings say out loud.
		/// </summary>
		public int Declarations = 1;

		/// <summary>A label for the file that most recently named this key, when the loader has one
		/// to give. Null is ordinary and simply leaves it out of the sentence.</summary>
		public string Origin;
	}
}
