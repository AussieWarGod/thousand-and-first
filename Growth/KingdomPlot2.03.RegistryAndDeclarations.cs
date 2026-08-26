using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		/// <summary>Durable classification for a free-standing perimeter work. Plot roots use their
		/// existing PlotId receipt instead; the explicit positive marker prevents a later catalogue
		/// override from reclassifying an already-built wall.</summary>
		public const string FrontierWorkProperty = "KingdomFrontierWork";

		/// <summary>Positive ownership receipt for plot geometry stamped by adoption. Commissioned
		/// plots already carry <see cref="PlotIdProperty"/> from their paid construction receipt;
		/// adopted rooms need this separate mark so release removes only geometry adoption itself
		/// authored. It is also the commit marker for the adopted plot ID and rect.</summary>
		public const string AdoptedPlotProperty = "KingdomAdoptedPlot";

		// --- Registry ---------------------------------------------------------------------

		// Plot specs live beside the catalog rather than inside KingdomRules.BuildEntry, the same
		// way zoning gates and upgrade chains do, so the registry parser needs one line of wiring
		// instead of a rewritten entry type. Keyed by building Key, which is what the registry
		// already overrides by (STANDARDS 6): a later file re-using a key registers its own spec
		// over the earlier one, including an entry that declares no plot attributes at all, which
		// correctly returns that design to the single-cell path.
		private static readonly Dictionary<string, KingdomPlotRules.PlotSpec> Specs = new Dictionary<string, KingdomPlotRules.PlotSpec>();

		/// <summary>Forgets every registered plot spec. Called by the registry loader before it
		/// re-reads the XML streams.</summary>
		public static void ClearSpecs()
		{
			Specs.Clear();
		}

		/// <summary>
		/// Registers one entry's plot attributes as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the raw attribute
		/// strings; all four may be null, which registers "not a plot".
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="Plot">Raw <c>Plot</c> attribute.</param>
		/// <param name="Open">Raw <c>Open</c> attribute.</param>
		/// <param name="Sky">Raw <c>Sky</c> attribute.</param>
		/// <param name="Contents">Raw <c>Contents</c> attribute.</param>
		public static void RegisterSpec(string Key, string Plot, string Open, string Sky, string Contents)
		{
			RegisterSpec(Key, Plot, Open, Sky, Contents, null, null);
		}

		/// <summary>
		/// Registers one entry's plot attributes, the tier's own footprint and roof included. Call
		/// once per <c>&lt;building&gt;</c> element that parsed successfully, with the raw
		/// attribute strings; all of them may be null, which registers "not a plot".
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="Plot">Raw <c>Plot</c> attribute: the envelope of ground.</param>
		/// <param name="Open">Raw <c>Open</c> attribute.</param>
		/// <param name="Sky">Raw <c>Sky</c> attribute.</param>
		/// <param name="Contents">Raw <c>Contents</c> attribute.</param>
		/// <param name="Footprint">Raw <c>Footprint</c> attribute: the ground THIS TIER stands on
		/// inside the plot. Absent fills the plot, which is what every entry written before
		/// footprints existed does.</param>
		/// <param name="Roof">Raw <c>Roof</c> attribute.</param>
		public static void RegisterSpec(string Key, string Plot, string Open, string Sky, string Contents, string Footprint, string Roof)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomPlotRules.TryParsePlotAttributes(Key, Plot, Open, Sky, Contents, Footprint, Roof, out var spec, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				Specs.Remove(Key);
				return;
			}
			Specs[Key] = spec;
		}

		/// <summary>The plot spec a design was registered with, if it is a plot at all.</summary>
		/// <returns>False for a design that declares no <c>Plot</c> size, which is every design
		/// written before plots existed.</returns>
		public static bool TryGetSpec(string Key, out KingdomPlotRules.PlotSpec Spec)
		{
			Spec = null;
			if (string.IsNullOrEmpty(Key) || !Specs.TryGetValue(Key, out var spec) || spec == null
				|| spec.Size == KingdomPlotRules.PlotSize.None)
			{
				return false;
			}
			Spec = spec;
			return true;
		}

		/// <summary>Whether a design is raised as a plot. False sends the caller down the
		/// single-cell path it has always used, untouched.</summary>
		public static bool IsPlotDesign(string Key)
		{
			return TryGetSpec(Key, out _);
		}

		// --- Properties and blueprints ----------------------------------------------------

		/// <summary>Marks a wall, floor, door, or furnishing as belonging to a plot. Deliberately
		/// NOT <c>KingdomBuilt</c>: the building cap counts plots, not the hundred objects one
		/// plot is made of.</summary>
		public const string PlotPartProperty = "KingdomPlotPart";

		/// <summary>Groups every object of one plot, so a later striking can find all of them
		/// and take down exactly what the settlement raised and nothing else.</summary>
		public const string PlotIdProperty = "KingdomPlotId";

		/// <summary>Low corner x of a plot, stamped on the object that represents it.</summary>
		public const string PlotX1Property = "KingdomPlotX1";

		/// <summary>Low corner y of a plot, stamped on the object that represents it.</summary>
		public const string PlotY1Property = "KingdomPlotY1";

		/// <summary>High corner x of a plot, stamped on the object that represents it.</summary>
		public const string PlotX2Property = "KingdomPlotX2";

		/// <summary>High corner y of a plot, stamped on the object that represents it.</summary>
		public const string PlotY2Property = "KingdomPlotY2";

		/// <summary>
		/// Low corner x of the FOOTPRINT: the ground the building itself stands on, inside the
		/// plot. Stamped separately from the plot rect because the two are different questions
		/// &mdash; the plot is the envelope the founder staked and never changes, the footprint is
		/// the current tier's own ground and grows when the work does.
		/// <para>
		/// Absent on anything raised before tiers declared footprints, which is exactly why
		/// <see cref="TryReadFootprint"/> falls back to the plot rect: a building that already
		/// stands filled its plot, and still does.
		/// </para>
		/// </summary>
		public const string FootX1Property = "KingdomFootX1";

		/// <summary>Low corner y of the footprint. See <see cref="FootX1Property"/>.</summary>
		public const string FootY1Property = "KingdomFootY1";

		/// <summary>High corner x of the footprint, inclusive. See <see cref="FootX1Property"/>.</summary>
		public const string FootX2Property = "KingdomFootX2";

		/// <summary>High corner y of the footprint, inclusive. See <see cref="FootX1Property"/>.</summary>
		public const string FootY2Property = "KingdomFootY2";

		/// <summary>
		/// The tier's roof state as an int, so the stamped value never depends on an enum's
		/// backing type. Absent is read through <see cref="RoofOf"/>, which derives the same three
		/// states the open and carved flags always meant.
		/// </summary>
		public const string PlotRoofProperty = "KingdomPlotRoof";

		/// <summary>Set once on a staked plan whose ground is blocked, so the reason is given
		/// once rather than every settlement pass (STANDARDS 7b).</summary>
		public const string BlockAnnouncedProperty = "KingdomPlotBlockSaid";

		/// <summary>Named-property plan receipt. Kept off r_KingdomPlanMarker's positional fields
		/// so old saves retain their exact part layout. Schema is the final commit marker.</summary>
		public const string PlanSchemaProperty = "r_TAF_PlanPlotSchema";
		public const string PlanPayloadProperty = "r_TAF_PlanPlotPayload";
		public const string PlanLabourProperty = "r_TAF_PlanPlotLabour";
		public const string PlanWaterProperty = "r_TAF_PlanPlotWater";
		public const string PlanMaterialProperty = "r_TAF_PlanPlotMaterial";
		public const int PlanSchema = 1;

		/// <summary>Zone property carrying the rite ground's x, written where the rite was
		/// poured. Absent on a settlement founded before it was recorded, which simply has no
		/// rite seed for the heart.</summary>
		public const string RiteXProperty = "r_TAF_RiteX";

		/// <summary>Zone property carrying the rite ground's y. See <see cref="RiteXProperty"/>.</summary>
		public const string RiteYProperty = "r_TAF_RiteY";

		// --- The heart -------------------------------------------------------------------

		/// <summary>Zone property carrying the low corner x of the ground the heart was surveyed
		/// for at the founding rite. Absent means this zone has no surveyed heart, and every plot
		/// in it is sited exactly as it was before the survey existed.</summary>
		public const string SurveyX1Property = "r_TAF_HeartSurveyX1";

		/// <summary>Low corner y of the surveyed heart. See <see cref="SurveyX1Property"/>.</summary>
		public const string SurveyY1Property = "r_TAF_HeartSurveyY1";

		/// <summary>High corner x of the surveyed heart, inclusive.</summary>
		public const string SurveyX2Property = "r_TAF_HeartSurveyX2";

		/// <summary>High corner y of the surveyed heart, inclusive.</summary>
		public const string SurveyY2Property = "r_TAF_HeartSurveyY2";

		/// <summary>Zone property carrying which rung of the heart currently stands on the rite
		/// ground, one-based. Absent or zero means nothing has been raised there yet, and the rite
		/// ground counts for exactly one vote as it always did.</summary>
		public const string HeartRungProperty = "r_TAF_HeartRung";

		/// <summary>Marks the works and the building of the heart's own plot, so the one plot
		/// that grows with its rung is told apart from the forty that do not.</summary>
		public const string HeartPlotProperty = "r_TAF_HeartPlot";

		/// <summary>Marks a survey stake. Read as bare ground by <see cref="ReadObject"/>: a
		/// stake is a mark, never a claim, and an ordinary plot may be built straight over
		/// it.</summary>
		public const string HeartStakeProperty = "r_TAF_HeartStake";

		/// <summary>Marks the founder's own basin, set down where the first water was poured and
		/// never taken up again. Read as bare ground for the same reason a stake is, so every rung
		/// of the heart is raised around it rather than refused by it.</summary>
		public const string HeartRelicProperty = "r_TAF_HeartRelic";

		/// <summary>Marks a plot staked in surveyed heart ground: told at placement, carried in
		/// the plot's own description forever, and read by the ring call when the great work is
		/// finally called for that ground.</summary>
		public const string YieldingProperty = "r_TAF_Yielding";

		/// <summary>The stake driven at the corners of the surveyed ground.</summary>
		public const string SurveyStakeBlueprint = "r_KingdomHeartStake";

		/// <summary>The founder's basin, kept at the middle of every rung.</summary>
		public const string HeartRelicBlueprint = "r_KingdomFirstBasin";

		// Transaction state belongs to GameObject's named property maps, never to the shipped
		// positional r_KingdomPlotWorks layout (which must end at DoorY forever).
		private const string FinalOutputIdProperty = "r_TAF_PlotFinalOutputId";
		private const string ClearPhaseProperty = "r_TAF_PlotClearPhase";
		private const string ClearIdProperty = "r_TAF_PlotClearId";
		private const string ClearBlueprintProperty = "r_TAF_PlotClearBlueprint";
		private const string ClearXProperty = "r_TAF_PlotClearX";
		private const string ClearYProperty = "r_TAF_PlotClearY";
		private const string ClearMaterialProperty = "r_TAF_PlotClearMaterial";
		private const string ClearAmountProperty = "r_TAF_PlotClearAmount";
		private const string ClearRemovedProperty = "r_TAF_PlotClearRemoved";
		private const string ClearOutputIdProperty = "r_TAF_PlotClearOutputId";
		private const string ClearOutputBlueprintProperty = "r_TAF_PlotClearOutputBlueprint";
		private const string ClearOutputMarkerProperty = "r_TAF_PlotClearOutputMarker";
		private const string ClearOutputMark = "r_TAF_PlotClearReceipt";
		private const string ClearDestinationKindProperty = "r_TAF_PlotClearDestinationKind";
		private const string ClearDestinationIdProperty = "r_TAF_PlotClearDestinationId";
		private const string ClearDestinationZoneProperty = "r_TAF_PlotClearDestinationZone";
		private const string ClearDestinationXProperty = "r_TAF_PlotClearDestinationX";
		private const string ClearDestinationYProperty = "r_TAF_PlotClearDestinationY";
		private const string ClearTallyBeforeProperty = "r_TAF_PlotClearTallyBefore";
		private const string ClearTallyAfterProperty = "r_TAF_PlotClearTallyAfter";
		private const string ClearQuarantinedProperty = "r_TAF_PlotClearQuarantined";
		private const string ClearFailureProperty = "r_TAF_PlotClearFailure";
		private const string ClearTimberProperty = "r_TAF_PlotClearTimber";
		private const string ClearStoneProperty = "r_TAF_PlotClearStone";
		private const string ClearMarbleProperty = "r_TAF_PlotClearMarble";
		private const string ClearScrapProperty = "r_TAF_PlotClearScrap";
		internal const string FurnishReceiptProperty = "r_TAF_ConstructionFurnishReceipt";
		private const string LegacyFurnishPlanProperty = "r_TAF_LegacyFurnishPlan";
		private const string HeartEffectProperty = "r_TAF_ConstructionHeartEffect";
		private const string DelveEffectProperty = "r_TAF_ConstructionDelveEffect";
		private const string GrowthReceiptProperty = "r_TAF_ImprovementGrowthReceipt";
		private const string GrowthEscrowPrefix = "r_TAF_ImprovementGrowthEscrow:";
		/// <summary>Named-property schema for labour-driven plot works. A missing value is a
		/// pre-polish legacy plot and deliberately retains its old absolute-clock path.</summary>
		public const string PlotWorkSchemaProperty = "r_TAF_PlotWorkSchema";
		public const string PlotWorkRequiredProperty = "r_TAF_PlotWorkRequired";
		public const string PlotWorkRemainingProperty = "r_TAF_PlotWorkRemaining";
		public const string PlotWorkLastTickProperty = "r_TAF_PlotWorkLastTick";
		public const string PlotWorkCompletedTickProperty = "r_TAF_PlotWorkCompletedTick";
		public const string PlotWorkShortfallSaidProperty = "r_TAF_PlotWorkShortfallSaid";
		public const string PlotWorkFaultSaidProperty = "r_TAF_PlotWorkFaultSaid";
		public const int PlotWorkSchema = 2;
		private const int MaxFurnishItems = 64;
		private const int MaxGrowthRows = 512;
		private const int MaxPlotSkinChars = 256;
		private static readonly System.Text.Encoding StrictPlotUtf8 =
			new System.Text.UTF8Encoding(false, true);

		/// <summary>The object that stands in a plot while it is being raised.</summary>
		public const string WorksBlueprint = "r_KingdomPlotWorks";

		/// <summary>A corner post, standing only between the frame stage and the walls.</summary>
		public const string FrameBlueprint = "r_KingdomPlotFrame";

		/// <summary>Floor laid inside a roofed plot. Vanilla's own, the one <c>PlaceHut</c>
		/// uses.</summary>
		public const string FloorBlueprint = "DirtPath";

		/// <summary>The door cut in a roofed plot's wall.</summary>
		public const string DoorBlueprint = "Door";

	}
}
