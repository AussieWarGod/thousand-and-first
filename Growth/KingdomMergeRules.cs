using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for layering catalogue files: what a later <c>&lt;building Key="X"&gt;</c>
	/// does to the design an earlier file already declared, and what it is forbidden to do to
	/// anything the settlement has already built.
	/// <para>
	/// <b>The contract.</b> A later declaration of a key <em>merges</em>. Attributes it names
	/// override; attributes it omits survive; <c>&lt;skin&gt;</c> children append, with a repeated
	/// skin key replacing the earlier skin in place rather than shadowing it; an upgrade chain is
	/// extended by whichever file names the next link. Load order is file order, which is the mod
	/// system's own priority &mdash; this file never sorts anything and never decides who wins by
	/// any rule but "later".
	/// </para>
	/// <para>
	/// <b>The guardrail.</b> Merges shape future commissions only. A standing work keeps the ground
	/// it was cut, the object standing on it, and the water it was raised with, whatever a mod
	/// update later says the design costs (<see cref="Reconcile"/>, <see cref="MergeReach"/>). What
	/// a settlement re-reads every pass &mdash; names, contributions, crews, chains, skins &mdash;
	/// does follow the merge, because that is what a rebalance is for and it moves nothing.
	/// </para>
	/// <para>
	/// <b>What this file never does.</b> Parse an attribute, reject an entry, or touch a registry.
	/// Merging is string-level on purpose, so every parser downstream reads what it always read and
	/// an attribute invented in a later wave layers correctly with no change here.
	/// </para>
	/// </summary>
	public static partial class KingdomMergeRules
	{
		// --- The schema, named once ---------------------------------------------------------

		public const string AttrDisplayName = "DisplayName";

		public const string AttrBlueprint = "Blueprint";

		public const string AttrCost = "Cost";

		public const string AttrTicks = "Ticks";

		public const string AttrStyles = "Styles";

		public const string AttrCategory = "Category";

		public const string AttrMinStage = "MinStage";

		public const string AttrStaff = "Staff";

		public const string AttrManning = "Manning";

		public const string AttrDefence = "Defence";

		public const string AttrAdoptable = "Adoptable";

		public const string AttrCarries = "Carries";

		public const string AttrMaterials = "Materials";

		public const string AttrDistricts = "Districts";

		public const string AttrMinZones = "MinZones";

		public const string AttrKnowledge = "Knowledge";

		public const string AttrMinTech = "MinTech";

		/// <summary>The engine faction whose covenant opens this design. Written together with
		/// <see cref="AttrMinStanding"/>; one without the other is malformed. A live gate, so it
		/// is deliberately absent from <see cref="SpentAttributes"/> and
		/// <see cref="StampedAttributes"/>.</summary>
		public const string AttrCovenant = "Covenant";

		/// <summary>The realm standing required with <see cref="AttrCovenant"/> before this
		/// design may be commissioned. This is the kingdom's standing, not the founder's personal
		/// reputation, and is re-read whenever the design is judged.</summary>
		public const string AttrMinStanding = "MinStanding";

		/// <summary>Who must be living here for the design to be raised at all (Addendum 16
		/// clause 1): a comma list in the <c>kind:name</c> language <see cref="AttrKnowledge"/>
		/// already uses, optionally with a count. Deliberately absent from
		/// <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/> for exactly
		/// <see cref="AttrKnowledge"/>'s reason &mdash; a gate is asked again every time somebody
		/// tries to raise the design, and re-writing it moves nothing already standing.</summary>
		public const string AttrBuilders = "Builders";

		/// <summary>The creed a design belongs to (Addendum 16 clause 4): raised only by builders
		/// who hold it, or have previously held it. One faction name. A gate, so it reads again
		/// like the rest of them.</summary>
		public const string AttrCreed = "Creed";

		/// <summary>How much of the city must hold <see cref="AttrCreed"/> (Addendum 16 clause 2),
		/// in whole percent. A gate, so it reads again.</summary>
		public const string AttrCreedShare = "CreedShare";

		/// <summary>Which set of the catalogue a design lives in and which strata it may stand in
		/// besides (Addendum 15): <c>Strata="deep,surface"</c>. Deliberately absent from
		/// <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/> for exactly
		/// <see cref="AttrKnowledge"/>'s reason — a gate is asked again every time somebody asks.</summary>
		public const string AttrStrata = "Strata";

		/// <summary>
		/// Whether this design is a MEGASTRUCTURE: a city gets one, and it is what the city is for
		/// (Addendum 22 A1, Design B). <c>Megastructure="yes"</c>; anything else, absence included,
		/// means ordinary.
		/// <para>
		/// Deliberately absent from <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/>
		/// for exactly <see cref="AttrStrata"/>'s reason &mdash; a gate is asked again every time
		/// somebody asks, and what a city already keeps changes under a design that has not.
		/// </para>
		/// </summary>
		public const string AttrMegastructure = "Megastructure";

		/// <summary>
		/// Whether only the capital may raise this design (Addendum 22 A4 and the capital ruling
		/// extending Addendum 19). <c>Capital="yes"</c>; anything else, absence included, means any
		/// city may.
		/// <para>
		/// Deliberately absent from <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/>
		/// for exactly <see cref="AttrMegastructure"/>'s reason: this is a gate, gates are asked
		/// again every time somebody asks, and where the crown stands changes under a design that
		/// has not.
		/// </para>
		/// </summary>
		public const string AttrCapital = "Capital";

		/// <summary>
		/// The registry key of the great work this design is an outpost of
		/// (END-STATE-CITIES-RESEARCH &sect;5.5). A key rather than a flag, so a third-party file
		/// declares an outpost of ITS megastructure without a line of our code changing.
		/// <para>
		/// Absent from <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/> for
		/// <see cref="AttrCapital"/>'s reason: what the realm keeps is re-read every time somebody
		/// asks, and changing this moves nothing that is already standing.
		/// </para>
		/// </summary>
		public const string AttrSatellite = "Satellite";

		public const string AttrUpgradesTo = "UpgradesTo";

		public const string AttrUpgradeCost = "UpgradeCost";

		public const string AttrUpgradeTicks = "UpgradeTicks";

		public const string AttrUpgradeCrew = "UpgradeCrew";

		public const string AttrUpgradeMinStage = "UpgradeMinStage";

		public const string AttrUpgradeMaterials = "UpgradeMaterials";

		public const string AttrPlot = "Plot";

		public const string AttrOpen = "Open";

		public const string AttrSky = "Sky";

		public const string AttrContents = "Contents";

		/// <summary>A tier's own footprint, which the plot is merely the envelope for. Named here
		/// so the merge and the validator agree on the spelling even while the parser for it lands
		/// separately.</summary>
		public const string AttrFootprint = "Footprint";

		/// <summary>A tier's roof state: <c>Open</c>, <c>Soft</c>, <c>Walled</c>, <c>Carved</c>.
		/// </summary>
		public const string AttrRoof = "Roof";

		/// <summary>The quality-of-life tags a design offers whoever lives or works in it
		/// (Addendum 4). Named here so the merge and the loader agree on the spelling. Deliberately
		/// absent from <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/>: what a
		/// building provides is read again every time somebody asks whether they will live there,
		/// which is what <see cref="MergeReach.Read"/> already means for everything this file does
		/// not name, and it moves nothing when a mod changes it.</summary>
		public const string AttrProvides = "Provides";

		/// <summary>How close the quarters a design's residents keep are (Addendum 4c):
		/// <c>Packed</c>, <c>Close</c>, <c>Roomed</c>, <c>Private</c>. An override for the
		/// beds-per-footprint derivation, and absent from every design content to be measured.
		/// Named here so the merge and the loader agree on the spelling. Deliberately absent from
		/// <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/> for exactly
		/// <see cref="AttrProvides"/>'s reason: how close a roof holds people is re-read every time
		/// somebody asks whether they will live under it, and changing it moves nothing.</summary>
		public const string AttrCloseness = "Closeness";

		/// <summary>How far what a design gives actually carries (Addendum 6): <c>plot</c>,
		/// <c>quarter</c>, <c>zone</c>, <c>city</c>, <c>realm</c>. An override for the derivation
		/// from plot size and chain position, and absent from every design content to be derived.
		/// Named here so the merge and the loader agree on the spelling. Deliberately absent from
		/// <see cref="SpentAttributes"/> and <see cref="StampedAttributes"/> for exactly
		/// <see cref="AttrProvides"/>'s reason: how far a work carries is re-read every time
		/// somebody asks, and changing it moves nothing.</summary>
		public const string AttrReach = "Reach";

		/// <summary>What a design's crew needs to be capable of to raise it at full pace
		/// (Addendum 7): a <c>kind:amount</c> list in the same language as
		/// <see cref="AttrCarries"/>, e.g. <c>strength:16</c>. Named here so the merge and the
		/// loader agree on the spelling. Deliberately absent from <see cref="SpentAttributes"/> and
		/// <see cref="StampedAttributes"/> for exactly <see cref="AttrProvides"/>'s reason: what a
		/// work demands of its crew is re-read every time it is crewed, and a rebalance reaches a
		/// work already standing rather than the crew it happened to draw the day it was raised.
		/// </summary>
		public const string AttrCrewNeeds = "CrewNeeds";

		/// <summary>What a high-craft design costs in vanilla's own tinkering bits (Addendum 7),
		/// written in the game's own bit tiers: <c>Bits="0034"</c>. Named here so the merge and the
		/// loader agree on the spelling. Belongs to <see cref="SpentAttributes"/> for exactly
		/// <see cref="AttrCost"/>'s reason: bits are paid out the day the work goes up, so a later
		/// file that re-prices the design neither charges nor refunds a work that already stands.
		/// </summary>
		public const string AttrBits = "Bits";

		/// <summary>The rare finds a great work is finished in (Addendum 7):
		/// <c>Exotics="gold:2,gem:1"</c>. Spent at commission, so it joins
		/// <see cref="SpentAttributes"/> beside <see cref="AttrBits"/> and
		/// <see cref="AttrMaterials"/>.</summary>
		public const string AttrExotics = "Exotics";

		/// <summary>What a processing work turns raw stock into: <c>Refines="shapedstone"</c>, or
		/// the yard's own key. Deliberately absent from <see cref="SpentAttributes"/> and
		/// <see cref="StampedAttributes"/>: what a standing yard makes is asked again every pass, so
		/// a mod that re-purposes a yard changes what comes off it tomorrow and moves nothing today.
		/// </summary>
		public const string AttrRefines = "Refines";

		/// <summary>Frozen city purpose of a body megastructure: <c>flesh</c> or
		/// <c>chrome</c>. Optional and mergeable; absence leaves an ordinary building.</summary>
		public const string AttrPurpose = "Purpose";

		/// <summary>Distinct physical site predicate committed with <see cref="AttrPurpose"/>.</summary>
		public const string AttrPurposeSite = "PurposeSite";

		/// <summary>Stable typed consignment key produced by another city.</summary>
		public const string AttrPurposeCargoKey = "PurposeCargoKey";

		/// <summary>Founder-facing physical name of the exact consignment item.</summary>
		public const string AttrPurposeCargoName = "PurposeCargoName";

		/// <summary>Material identity conserved inside the consignment item.</summary>
		public const string AttrPurposeCargoMaterial = "PurposeCargoMaterial";

		/// <summary>Water spent by the producing city before dispatch.</summary>
		public const string AttrPurposeCargoWater = "PurposeCargoWater";

		/// <summary>Physical material claim spent by the producing city before dispatch.</summary>
		public const string AttrPurposeCargoCost = "PurposeCargoCost";

		/// <summary>Physical source-city works required to produce the consignment.</summary>
		public const string AttrPurposeProducers = "PurposeProducers";

		/// <summary>Honest existing runtime effect disclosed before purpose commitment.</summary>
		public const string AttrPurposeEffect = "PurposeEffect";

		/// <summary>What <c>KingdomRules.TryParseBuildAttributes</c> refuses an entry for the want
		/// of. A later file may omit every one of them &mdash; that is a merge &mdash; but the
		/// design as a whole must end up with all four.</summary>
		public static readonly string[] RequiredAttributes = new string[4] { AttrDisplayName, AttrBlueprint, AttrCost, AttrTicks };

		/// <summary>Attributes whose value was paid out when the work went up.</summary>
		public static readonly string[] SpentAttributes = new string[7]
		{
			AttrCost, AttrTicks, AttrMaterials, AttrBits, AttrExotics,
			AttrPurposeCargoWater, AttrPurposeCargoCost
		};

		/// <summary>Attributes whose value is cut into the ground the work stands on.</summary>
		public static readonly string[] StampedAttributes = new string[13]
		{
			AttrBlueprint, AttrPlot, AttrFootprint, AttrRoof, AttrOpen, AttrContents,
			AttrPurpose, AttrPurposeSite, AttrPurposeCargoKey, AttrPurposeCargoName,
			AttrPurposeCargoMaterial, AttrPurposeProducers, AttrPurposeEffect
		};

		/// <summary>
		/// How far a change to this attribute reaches. Anything this file does not name reads
		/// again: an attribute belonging to a third party's own system cannot have been spent by
		/// our economy or stamped on our ground, so treating it as read-again is both the safe
		/// answer and the true one.
		/// </summary>
		public static MergeReach Classify(string Attribute)
		{
			if (Contains(SpentAttributes, Attribute))
			{
				return MergeReach.Spent;
			}
			return Contains(StampedAttributes, Attribute) ? MergeReach.Stamped : MergeReach.Read;
		}

		/// <summary>Whether a merge changing this attribute may reach a work that already stands.
		/// </summary>
		public static bool ReachesStandingWork(string Attribute)
		{
			return Classify(Attribute) == MergeReach.Read;
		}

	}
}
