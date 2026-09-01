using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		/// <summary>
		/// Structural authority for an ordinary authored lot which annexes settlement ground.
		/// Fixed-envelope additive/renovation work never enters this branch; additive-expand and
		/// renovate-expand do. Founding-heart accretion retains its separate surveyed-ground
		/// authority.
		/// </summary>
		private static bool TryAuthorizedEnvelopeExpansion(GameObject Owner, Zone Z,
			KingdomArchitectureIntent BeforeIntent, ArchitectureLayoutSnapshot Before,
			KingdomArchitectureIntent AfterIntent, ArchitectureLayoutSnapshot After,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Owner) || Z == null || BeforeIntent == null || Before == null
				|| AfterIntent == null || After == null)
				return Fail("authored plot-envelope growth lacks exact frozen endpoints",
					out Failure);
			if (KingdomPlotRules.HeartRungOf(Before.BuildKey) != 0
				|| KingdomPlotRules.HeartRungOf(After.BuildKey) != 0
				|| Owner.GetIntProperty(KingdomPlots.HeartPlotProperty) == 1)
				return Fail("ordinary plot-envelope growth cannot claim founding-heart authority",
					out Failure);
			if (!KingdomArchitectureTransitionRules.AllowsLotExpansion(
					After.IncomingTransitionMode))
				return Fail("a larger authored lot needs explicit expansion authority", out Failure);
			if (!KingdomArchitectureExpansionRules.SameFrozenLineage(Before, After)
				|| BeforeIntent.MainWorldX != AfterIntent.MainWorldX
				|| BeforeIntent.MainWorldY != AfterIntent.MainWorldY)
				return Fail("plot-envelope growth changes frozen lineage, type, pose, or main root",
					out Failure);
			if (!KingdomPlotPoseSitingRules.IsStrictContainingEnvelope(BeforeIntent.Rect,
				AfterIntent.Rect))
				return Fail("authored successor does not strictly contain its standing plot",
					out Failure);
			int beforeWidth;
			int beforeHeight;
			int afterWidth;
			int afterHeight;
			if (!KingdomArchitectureRules.TryWorldDimensions(Before.Width, Before.Height,
				Before.Facing, out beforeWidth, out beforeHeight)
				|| !KingdomArchitectureRules.TryWorldDimensions(After.Width, After.Height,
					After.Facing, out afterWidth, out afterHeight)
				|| BeforeIntent.Rect.Width != beforeWidth || BeforeIntent.Rect.Height != beforeHeight
				|| AfterIntent.Rect.Width != afterWidth || AfterIntent.Rect.Height != afterHeight)
				return Fail("authored plot-envelope endpoints do not exactly fit their frozen poses",
					out Failure);
			KingdomPlotRules.PlotRect standing;
			string lot = Owner.GetStringProperty(LotIdProperty);
			if (!ValidLotId(lot) || Owner.GetStringProperty(KingdomPlots.PlotIdProperty) != lot
				|| !KingdomPlots.TryReadRect(Owner, out standing)
				|| !SameRect(standing, BeforeIntent.Rect)
				|| Owner.CurrentZone != Z || Owner.CurrentCell != Z.GetCell(
					BeforeIntent.MainWorldX, BeforeIntent.MainWorldY))
				return Fail("standing layout does not own its exact recorded plot envelope",
					out Failure);
			return true;
		}

		/// <summary>
		/// Read-only proof for every newly annexed cell plus surviving public-road ingress.
		/// Called before debit and again before every durable application attempt. Pre-debit proof
		/// consults the live mapping and selection context; paid retries validate only the frozen
		/// snapshot plus current physical ground/ingress. Exact successor components already
		/// published by an interrupted retry are the sole non-ground objects admitted in the added
		/// envelope.
		/// </summary>
		internal static bool TryProveEnvelopeGrowth(KingdomSystem System, Zone Z,
			GameObject Owner, GameObject SuccessorOwner, KingdomArchitectureIntent Successor,
			bool AllowSettledSuccessor, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent beforeIntent;
			ArchitectureLayoutSnapshot before;
			string lot;
			ArchitectureLayoutSnapshot after;
			if (System == null || !System.Founded || System.ClaimedZones == null || Z == null
				|| !System.ClaimedZones.Contains(Z.ZoneID)
				|| !TryExactLayoutOwner(Owner, Z, out beforeIntent, out before, out lot,
					out Failure)
				|| !KingdomArchitectureRuntime.TryDecode(Successor, out after, out Failure))
				return Failure != null ? false : Fail(
					"plot-envelope growth needs exact owned settlement ground", out Failure);
			if (SameRect(beforeIntent.Rect, Successor.Rect)) return true;
			if (!TryAuthorizedEnvelopeExpansion(Owner, Z, beforeIntent, before, Successor,
				after, out Failure)) return false;
			ArchitectureLayoutDelta delta;
			if (!KingdomArchitectureRules.TryBuildDelta(before, after, out delta, out Failure))
				return false;
			KingdomPlotRules.PlotRect interior;
			if (!KingdomPlotRules.TryInterior(Z.Width, Z.Height, out interior)
				|| !KingdomPlotRules.Fits(Successor.Rect, interior))
				return Fail("the enlarged authored lot does not fit settlement interior ground",
					out Failure);

			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			// PlotRoots contains only geometry that decoded when the survey classified it. Inspect
			// every root carrying any plot-coordinate prefix as well, or a torn/out-of-zone root
			// could disappear from that index and donate its reserved ground to this expansion.
			for (int i = 0; i < survey.Objects.Count; i++)
			{
				GameObject candidate = survey.Objects[i];
				if (ReferenceEquals(candidate, Owner)
					|| ReferenceEquals(candidate, SuccessorOwner)) continue;
				if (KingdomPlots.HasRectEvidence(candidate)
					&& (!GameObject.Validate(candidate)
						|| !KingdomPlots.TryReadRect(candidate, out _)))
					return Fail("the loaded zone carries malformed or out-of-zone plot geometry",
						out Failure);
			}
			long laidArea = 0L;
			int ownerRoots = 0;
			for (int i = 0; i < survey.PlotRoots.Count; i++)
			{
				GameObject root = survey.PlotRoots[i];
				if (!GameObject.Validate(root))
					return Fail("the loaded plot index carries an invalid root", out Failure);
				if (ReferenceEquals(root, Owner))
				{
					ownerRoots++;
					continue;
				}
				if (ReferenceEquals(root, SuccessorOwner)) continue;
				KingdomPlotRules.PlotRect other;
				if (!KingdomPlots.TryReadRect(root, out other))
					return Fail("the loaded plot index carries malformed or out-of-zone geometry",
						out Failure);
				if (KingdomPlotRules.Overlaps(Successor.Rect,
					KingdomPlotRules.Reserved(other)))
					return Fail("the enlarged authored lot would consume the reserved lane of "
						+ EnvelopeObjectName(root), out Failure);
				laidArea += other.Area;
			}
			if (ownerRoots != 1)
				return Fail("standing plot ownership is absent or ambiguous in the loaded zone",
					out Failure);
			if (laidArea + Successor.Rect.Area
				> KingdomPlotRules.PlotAreaAllowance(Z.Width, Z.Height))
				return Fail("the enlarged authored lot would spend settlement road ground",
					out Failure);

			if (!AllowSettledSuccessor)
			{
				KingdomArchitectureRuntime.SitingProbe probe;
				if (!KingdomArchitectureRuntime.TryCreateSitingProbe(System, Z, Successor.Rect,
					after.BuildKey, after.LotType, out probe, out Failure)
					|| !probe.TryAcceptExact(Successor.Rect, after, true, out Failure)) return false;
			}
			else if (!KingdomArchitectureRuntime.TryAcceptFrozenEnvelope(Z, Successor.Rect,
				after, true, out Failure)) return false;

			HashSet<GameObject> settled = new HashSet<GameObject>();
			if (!TryReadSettledExpansionOutputs(Owner, SuccessorOwner, Z, beforeIntent,
				Successor, before, after, delta, lot, AllowSettledSuccessor, settled,
				out Failure)) return false;
			HashSet<int> connections = ConnectionCells(Z);
			HashSet<int> wornRoads = ReadWornRoadCells(Z);
			for (int y = Successor.Rect.Y1; y <= Successor.Rect.Y2; y++)
				for (int x = Successor.Rect.X1; x <= Successor.Rect.X2; x++)
				{
					if (beforeIntent.Rect.Contains(x, y)) continue;
					int packed = y * Z.Width + x;
					Cell cell = Z.GetCell(x, y);
					if (cell == null || connections.Contains(packed) || cell.HasStairs()
						|| cell.HasObjectWithPart("StairsUp")
						|| cell.HasObjectWithPart("StairsDown"))
						return Fail("plot-envelope growth would cover stairs or a zone connection at "
							+ Coordinate(x, y), out Failure);
					if (cell.HasOpenLiquidVolume())
						return Fail("plot-envelope growth would cover open liquid at "
							+ Coordinate(x, y), out Failure);
					if (KingdomConstruction.HasActiveAt(System, Z, cell))
						return Fail("plot-envelope growth overlaps another active paid construction at "
							+ Coordinate(x, y), out Failure);
					GameObject road;
					KingdomPhysicalLookupState roadState = KingdomRoads.FindOurFloor(cell, out road);
					if (roadState != KingdomPhysicalLookupState.Absent || wornRoads.Contains(packed))
						return Fail("plot-envelope growth would absorb public road ground at "
							+ Coordinate(x, y), out Failure);
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (!GameObject.Validate(item)) continue;
						if (settled.Contains(item)) continue;
						if (item.IsCreature || item.IsPlayer())
							return Fail("a living occupant stands on plot-envelope growth ground at "
								+ Coordinate(x, y), out Failure);
						if (item.GetIntProperty(KingdomPlots.HeartStakeProperty) == 1
							|| item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)
							return Fail("founding-heart ground occupies plot-envelope growth at "
								+ Coordinate(x, y), out Failure);
						if (KingdomPlots.ReadObject(item) == KingdomPlotRules.GroundKind.Bare)
							continue;
						return Fail(EnvelopeObjectName(item)
							+ " occupies plot-envelope growth ground at " + Coordinate(x, y),
							out Failure);
					}
				}
			return true;
		}

		private static HashSet<int> ReadWornRoadCells(Zone Z)
		{
			HashSet<int> roads = new HashSet<int>();
			List<KingdomRoadRules.WornCell> tally = KingdomRoads.ReadTally(Z);
			for (int i = 0; i < tally.Count; i++)
			{
				KingdomRoadRules.WornCell worn = tally[i];
				if (worn.X >= 0 && worn.X < Z.Width && worn.Y >= 0 && worn.Y < Z.Height
					&& KingdomRoadRules.WearAt(worn.Traffic)
						> KingdomRoadRules.WearState.Untouched)
					roads.Add(worn.Y * Z.Width + worn.X);
			}
			return roads;
		}

		private static string EnvelopeObjectName(GameObject Item)
		{
			if (!GameObject.Validate(Item)) return "foreign state";
			string label = Item.ShortDisplayNameStripped;
			return KingdomDesign.ReferenceFor(Item,
				string.IsNullOrEmpty(label) ? (Item.Blueprint ?? "foreign state") : label);
		}
	}
}
