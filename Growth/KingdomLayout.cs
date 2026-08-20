using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the settlement's plan: reads a zone for what the settlement
	/// already has standing there, offers the clear ground to the grammar in
	/// <c>KingdomLayoutRules</c>, and hands back a cell. All the geometry lives in the pure
	/// file; everything here is looking at real objects and real cells.
	/// </summary>
	/// <remarks>
	/// This reads only the settlement's OWN works and the vessels the founder dedicated to it.
	/// A vanilla ruin wall, another mod's structure, and the player's dropped waterskin are all
	/// invisible to the plan: they define no quarter, and nothing is placed on them, because
	/// only cells the engine reports empty are ever offered.
	/// </remarks>
	public static class KingdomLayout
	{
		/// <summary>
		/// The property carrying the design key a structure was adopted or planned under. Named
		/// by string rather than by reference so the plan degrades to reading blueprints if the
		/// adoption path is ever absent, instead of failing to compile against it.
		/// </summary>
		public const string AdoptedKeyProperty = "KingdomAdoptedKey";

		/// <summary>The part a founder's own placed blueprint marker carries. Ground the founder
		/// has already spoken for is part of the settlement's shape before anything stands on
		/// it, so the plan does not aim a second commission at the same quarter.</summary>
		public const string PlanMarkerPart = "r_KingdomPlanMarker";

		/// <summary>
		/// What the settlement's own catalog says a blueprint is for. Blueprints are matched
		/// against the live registry rather than a hardcoded list, so a design shipped by
		/// another mod is read by its own <c>Category</c> the moment it is registered.
		/// </summary>
		/// <returns><c>Unknown</c> for a blueprint no registered design names, which makes the
		/// plan treat it as settled ground without filing it into any quarter.</returns>
		public static KingdomLayoutRules.LayoutPurpose PurposeOfBlueprint(string Blueprint)
		{
			if (string.IsNullOrEmpty(Blueprint))
			{
				return KingdomLayoutRules.LayoutPurpose.Unknown;
			}
			List<KingdomRules.BuildEntry> buildings = KingdomData.Buildings;
			for (int i = 0; i < buildings.Count; i++)
			{
				if (buildings[i].Blueprint == Blueprint)
				{
					return PurposeOfEntry(buildings[i]);
				}
			}
			return KingdomLayoutRules.LayoutPurpose.Unknown;
		}

		/// <summary>
		/// What the settlement's own catalog says a design KEY is for. This is how a structure
		/// the founder raised themselves and marked as serving a civic role joins the plan as
		/// that role: the key names the role, and the blueprint &mdash; a Hearthpyre house, a
		/// cleared ruin, walls laid by hand &mdash; never could.
		/// </summary>
		public static KingdomLayoutRules.LayoutPurpose PurposeOfKey(string Key)
		{
			if (string.IsNullOrEmpty(Key) || !KingdomData.TryGetBuilding(Key, out var entry))
			{
				return KingdomLayoutRules.LayoutPurpose.Unknown;
			}
			return PurposeOfEntry(entry);
		}

		/// <summary>
		/// The purpose a design is sited by: whatever its <c>Category</c> names, unless it
		/// carries a defence rating, which makes it a wall whatever else it is filed under.
		/// </summary>
		public static KingdomLayoutRules.LayoutPurpose PurposeOfEntry(KingdomRules.BuildEntry Entry)
		{
			if (Entry == null)
			{
				return KingdomLayoutRules.LayoutPurpose.Unknown;
			}
			if (Entry.Defence > 0)
			{
				return KingdomLayoutRules.LayoutPurpose.Defence;
			}
			return KingdomLayoutRules.PurposeOf(Entry.Category);
		}

		/// <summary>
		/// Whether one object is part of the settlement's shape, and what part. A raised work, a
		/// scaffold with a work still coming, a dedicated vessel, a dedicated larder, a wall.
		/// Nothing else counts &mdash; the plan reasons about the settlement, not about the
		/// ground it stands on.
		/// </summary>
		public static bool TryReadMark(GameObject Object, out KingdomLayoutRules.LayoutMark Mark)
		{
			Mark = default(KingdomLayoutRules.LayoutMark);
			Cell cell = Object?.CurrentCell;
			if (cell == null)
			{
				return false;
			}
			KingdomLayoutRules.LayoutPurpose purpose;
			r_KingdomScaffold scaffold = Object.GetPart<r_KingdomScaffold>();
			if (scaffold != null)
			{
				// A commission already standing is part of the shape before it finishes, so two
				// commissions in a row do not both aim at the same empty quarter.
				purpose = (Object.GetIntProperty("KingdomDefencePending") > 0)
					? KingdomLayoutRules.LayoutPurpose.Defence
					: PurposeOfBlueprint(scaffold.TargetBlueprint);
			}
			else if (Object.GetIntProperty("KingdomDefence") > 0)
			{
				purpose = KingdomLayoutRules.LayoutPurpose.Defence;
			}
			else if (Object.GetIntProperty("KingdomStores") == 1 || Object.GetIntProperty("KingdomLarder") == 1)
			{
				// Dedication is what makes a vessel the settlement's, whoever made it and
				// whenever. This is the line that puts the casks where the water already is.
				purpose = KingdomLayoutRules.LayoutPurpose.Storage;
			}
			else if (Object.GetIntProperty("KingdomBuilt") == 1 || Object.HasPart(PlanMarkerPart))
			{
				// The design key a structure was adopted or planned under names the role it
				// serves; the blueprint of a hall the founder laid by hand names nothing the
				// catalog knows. Read the role first and fall back to the blueprint, and let
				// anything neither names still count as settled ground.
				purpose = PurposeOfKey(Object.GetStringProperty(AdoptedKeyProperty));
				if (purpose == KingdomLayoutRules.LayoutPurpose.Unknown)
				{
					purpose = PurposeOfBlueprint(Object.Blueprint);
				}
			}
			else
			{
				return false;
			}
			Mark = new KingdomLayoutRules.LayoutMark(cell.X, cell.Y, purpose);
			return true;
		}

		/// <summary>Everything in this zone the plan reasons from.</summary>
		public static List<KingdomLayoutRules.LayoutMark> ReadMarks(Zone Z)
		{
			List<KingdomLayoutRules.LayoutMark> marks = new List<KingdomLayoutRules.LayoutMark>();
			if (Z == null)
			{
				return marks;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (TryReadMark(item, out var mark))
				{
					marks.Add(mark);
				}
			}
			return marks;
		}

		/// <summary>
		/// Ground for a commissioned work, chosen by what the settlement already is.
		/// </summary>
		/// <param name="Z">The zone to build in.</param>
		/// <param name="System">The realm, for its claim, which is what makes an edge a
		/// frontier. Null sites as though the zone were surrounded by the realm's own ground.</param>
		/// <param name="Entry">The design being raised; its <c>Category</c> is the purpose and
		/// its <c>Defence</c> overrides that, because a thing with a defence rating is a wall
		/// whatever else it is filed under.</param>
		/// <param name="Outcome">Whether the plan chose, the founder's own ground won, or the
		/// plan had nothing to say.</param>
		/// <returns>The cell, or null when the plan deferred or found no clear ground &mdash; in
		/// both cases the caller falls back to its own placement.</returns>
		public static Cell ChooseCell(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry, out KingdomLayoutRules.LayoutOutcome Outcome)
		{
			Outcome = KingdomLayoutRules.LayoutOutcome.None;
			if (Z == null || Entry == null)
			{
				return null;
			}
			KingdomLayoutRules.LayoutPurpose purpose = PurposeOfEntry(Entry);
			KingdomRules.Frontier edges = (System != null)
				? KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones)
				: KingdomRules.Frontier.None;
			List<KingdomLayoutRules.LayoutMark> marks = ReadMarks(Z);
			List<Cell> cells = new List<Cell>();
			List<KingdomLayoutRules.LayoutPoint> points = new List<KingdomLayoutRules.LayoutPoint>();
			foreach (Cell candidate in Z.GetEmptyCells())
			{
				if (!candidate.IsPassable() || candidate.HasObjectWithPart("LiquidVolume"))
				{
					continue;
				}
				if (purpose == KingdomLayoutRules.LayoutPurpose.Defence
					&& !KingdomRules.IsOnFrontier(candidate.X, candidate.Y, Z.Width, Z.Height, edges))
				{
					continue;
				}
				cells.Add(candidate);
				points.Add(new KingdomLayoutRules.LayoutPoint(candidate.X, candidate.Y));
			}
			Cell founderCell = The.Player?.CurrentCell;
			bool hasFounder = founderCell != null && founderCell.ParentZone == Z;
			Outcome = KingdomLayoutRules.Choose(purpose, Z.Width, Z.Height, edges, marks, points, hasFounder,
				hasFounder ? founderCell.X : 0, hasFounder ? founderCell.Y : 0, out var index);
			KingdomLog.Log("layout: " + purpose + " " + Outcome + " from " + cells.Count + " cells, " + marks.Count + " marks");
			if (index < 0 || index >= cells.Count)
			{
				return null;
			}
			return cells[index];
		}
	}
}
