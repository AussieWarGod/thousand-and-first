using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritanceSpatial
	{
		private static bool TrySourceRows(Simulation.City.KingdomCityBook Book,
			KingdomSealRecord Record, out List<SourceWork> Rows, out string Failure)
		{
			Rows = new List<SourceWork>();
			Failure = "";
			for (int i = 0; i < Book.WorkIds.Count && Rows.Count < KingdomSealRecord.MaxWorks; i++)
			{
				if (i >= Book.WorkZoneIds.Count || Book.WorkZoneIds[i] != Record.GroundZoneId)
					continue;
				if (i >= Book.WorkDesignKeys.Count || i >= Book.WorkAnchorsX.Count
					|| i >= Book.WorkAnchorsY.Count || i >= Book.WorkConditions.Count) continue;
				string key;
				string design = Book.WorkDesignKeys[i];
				if (!KingdomInheritRules.TrySemanticKeyForBlueprint(design, out key))
				{
					key = KingdomSealRules.SanitizeToken(design, KingdomSealRecord.MaxIdChars);
					if (!KingdomInheritRules.IsStableSemanticKey(key)) continue;
				}
				int x = Book.WorkAnchorsX[i];
				int y = Book.WorkAnchorsY[i];
				if (x < 0 || x > 255 || y < 0 || y > 255) continue;
				int at = Rows.Count;
				if (at >= Record.WorkKeys.Count || Record.WorkKeys[at] != key
					|| Record.WorkX[at] != x || Record.WorkY[at] != y)
				{
					Failure = "the city book changed while its spatial seal was witnessed";
					Rows = null;
					return false;
				}
				Rows.Add(new SourceWork
				{
					WorkId = Book.WorkIds[i], Blueprint = design, X = x, Y = y
				});
			}
			if (Rows.Count != Record.WorkKeys.Count)
			{
				Failure = "the city book's spatial work rows are incomplete";
				Rows = null;
				return false;
			}
			return true;
		}

		/// <summary>
		/// The one root a witnessed row may have that is not the object it was written from: the
		/// successor of a heart whose root climbed a rung.
		///
		/// <para>WHY A ROW CAN OUTLIVE ITS OBJECT. A work row's id is the FOLD of the standing
		/// object's identity (<c>Simulation/City/KingdomCity.z09</c>), and an improvement replaces
		/// that object with one carrying its own identity. The row is rebuilt at the next
		/// check-in, but the seal may witness before that, and until then the row names an
		/// identity nothing carries -- which is indistinguishable, from here, from a root that was
		/// destroyed.</para>
		///
		/// <para>WHAT IS ADDED, AND WHAT IS NOT. The chain answers only when the records prove it:
		/// the row's id must be the fold of an identity the founding heart's own sealed terminal
		/// bound, exactly one completed improvement must have retired that identity, its output
		/// must stand as exactly one live object carrying that job's receipt and removal proof.
		/// Position is still required and still exact -- the successor must stand at this row's
		/// own anchor cell, once -- and nothing else is relaxed: a root that is simply gone still
		/// refuses, because no completed improvement names it, and a foreign object at the anchor
		/// still refuses, because the chain never looked at the cell to find it.</para>
		/// </summary>
		private static bool TryClimbedRoot(Zone Zone, Cell Cell, SourceWork Row,
			out GameObject Climbed)
		{
			Climbed = null;
			if (Zone == null || Cell == null
				|| !KingdomPlots.TryChainedWorkSuccessor(Zone, Row.WorkId, out GameObject proved)
				|| !GameObject.Validate(proved)) return false;
			int here = 0;
			for (int i = 0; i < Cell.Objects.Count; i++)
				if (object.ReferenceEquals(Cell.Objects[i], proved)) here++;
			if (here != 1) return false;
			Climbed = proved;
			return true;
		}

		private static bool TryExactRoot(Zone Zone, SourceWork Row, out GameObject Root,
			out string Failure)
		{
			return TryExactRoot(Zone, Row, out Root, out _, out Failure);
		}

		/// <summary>
		/// The witnessed root, and the one thing the caller needs when there is none: whether the
		/// absence is a climb the settlement is still resolving (<paramref name="Pending" />)
		/// rather than a root that is gone.
		/// </summary>
		private static bool TryExactRoot(Zone Zone, SourceWork Row, out GameObject Root,
			out bool Pending, out string Failure)
		{
			Root = null;
			Pending = false;
			Failure = "";
			Cell cell = Zone.GetCell(Row.X, Row.Y);
			if (cell == null)
			{
				Failure = "a sealed work anchor is outside its witnessed zone";
				return false;
			}
			int count = 0;
			for (int i = 0; i < cell.Objects.Count; i++)
			{
				GameObject item = cell.Objects[i];
				if (!GameObject.Validate(item) || item.Blueprint != Row.Blueprint
					|| Simulation.City.KingdomCityRules.StableId(item.IDIfAssigned)
						!= Row.WorkId) continue;
				Root = item;
				count++;
			}
			if (count == 0 && TryClimbedRoot(Zone, cell, Row, out GameObject climbed))
			{
				Root = climbed;
				return true;
			}
			if (count != 1)
			{
				Root = null;
				Pending = KingdomSealPendingRules.ClimbUnderInspection(count == 0,
					count == 0 && KingdomPlots.HasPendingClimb(Zone, Row.WorkId));
				// Told only when the row really is classified that way: a DUPLICATED root is
				// malformed, and must not be announced as an inspection. And when an absent root's
				// climb is no longer pending -- cancelled, most of all, which never reaches the
				// completion path that clears the hold -- the saying is taken back here.
				if (Pending) KingdomPlots.NoteClimbUnderInspection(Zone, Row.WorkId);
				else if (count == 0) KingdomPlots.ReleaseSettledClimbHold(Zone, Row.WorkId);
				Failure = Pending
					? "a sealed work root is being replaced by an improvement still under inspection"
					: "a sealed work root is absent, duplicated, moved, or changed";
				return false;
			}
			return true;
		}

		private static bool HasArchitectureEvidence(GameObject Root)
		{
			return Root.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.SnapshotProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.HashProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.PlanKeyProperty)
				|| Root.HasStringProperty(KingdomArchitectureRuntime.BindingKeyProperty);
		}

		private static bool TryRoadEvidence(Zone Zone,
			IList<KingdomInheritanceSpatialRules.Rect> Rects, out bool[,] Roads,
			out string Failure)
		{
			Roads = new bool[KingdomInheritanceSpatialRules.Width,
				KingdomInheritanceSpatialRules.Height];
			Failure = "";
			for (int y = 0; y < Zone.Height; y++)
			{
				for (int x = 0; x < Zone.Width; x++)
				{
					GameObject floor;
					KingdomPhysicalLookupState state = KingdomRoads.FindOurFloor(
						Zone.GetCell(x, y), out floor);
					if (state == KingdomPhysicalLookupState.Ambiguous)
					{
						Failure = "road evidence is physically ambiguous at " + x + "," + y;
						return false;
					}
					if (state == KingdomPhysicalLookupState.Exact) Roads[x, y] = true;
				}
			}
			List<KingdomRoadRules.WornCell> tally;
			string error;
			if (!KingdomRoadRules.TryDecode(Zone.GetZoneProperty(KingdomRoads.TallyProperty,
				null), out tally, out error))
			{
				Failure = error ?? "the road tally is malformed";
				return false;
			}
			for (int i = 0; i < tally.Count; i++)
			{
				KingdomRoadRules.WornCell cell = tally[i];
				if (cell.X >= 0 && cell.Y >= 0 && cell.X < Zone.Width && cell.Y < Zone.Height
					&& KingdomRoadRules.WearAt(cell.Traffic) > KingdomRoadRules.WearState.Untouched)
					Roads[cell.X, cell.Y] = true;
			}
			for (int y = 0; y < Zone.Height; y++)
				for (int x = 0; x < Zone.Width; x++)
					for (int i = 0; Roads[x, y] && i < Rects.Count; i++)
						if (Rects[i].Contains(x, y)) Roads[x, y] = false;
			return true;
		}

	}
}
