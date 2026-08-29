using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSuccessionRite
	{
		/// <summary>Reads the heir city's whole resident law, then freezes every living exact body
		/// whose binding already stands in the rite zone. A local row/body disagreement refuses the
		/// succession; a resident in another quarter is physically absent and is never teleported
		/// into this synchronous death callback.</summary>
		private static bool TryExactResidentsIn(Zone zone, KingdomSystem system,
			KingdomCityBook cityBook, GameObject heir, out List<GameObject> result,
			out string failure)
		{
			result = new List<GameObject>();
			failure = "";
			KingdomCityState state;
			KingdomCityFault fault;
			int heirId = GameObject.Validate(heir)
				? heir.GetIntProperty(KingdomResidents.ResidentIdProperty) : 0;
			if (zone == null || system == null || cityBook == null || heirId <= 0
				|| !cityBook.TryRead(out state, out fault))
			{
				failure = "the heir city's complete resident law could not be read for the procession";
				return false;
			}
			HashSet<int> ids = new HashSet<int>();
			bool foundHeir = false;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row)
					|| row.Standing != KingdomResidentStanding.Resident) continue;
				GameObject body;
				string bound;
				bool exact = KingdomResidents.TryResolveBoundBody(system, row.ResidentId,
					LoadZone: false, out body, out bound);
				if (!exact)
				{
					if (string.Equals(row.BoundZoneId, zone.ZoneID, StringComparison.Ordinal))
					{
						failure = "a resident row names this rite ground but its exact living body cannot be proved";
						return false;
					}
					continue;
				}
				if (!string.Equals(bound, zone.ZoneID, StringComparison.Ordinal)) continue;
				KingdomCityBook locatedBook;
				int locatedId;
				string bodyName = body.GetStringProperty("KingdomName")
					?? body.BaseDisplayNameStripped;
				if (!GameObject.Validate(body) || !body.IsAlive || body.Brain == null
					|| body.CurrentCell == null || !ReferenceEquals(body.CurrentZone, zone)
					|| !KingdomResidents.TryLocate(system, body, out locatedBook, out locatedId)
					|| !ReferenceEquals(locatedBook, cityBook) || locatedId != row.ResidentId
					|| !string.Equals(bodyName, row.Name, StringComparison.Ordinal)
					|| !ids.Add(row.ResidentId))
				{
					failure = "a named resident present does not match the heir city's exact row, body, or binding";
					return false;
				}
				if (row.ResidentId == heirId)
				{
					if (!ReferenceEquals(body, heir))
					{
						failure = "the chosen heir's resident identity resolves to a different body";
						return false;
					}
					foundHeir = true;
				}
				result.Add(body);
			}
			if (!foundHeir || result.Count == 0
				|| result.Count > KingdomSuccessionRules.MaxRiteAttendees)
			{
				failure = "the chosen heir is not one of the exact named residents present at the rite ground";
				return false;
			}
			result.Sort(delegate(GameObject a, GameObject b)
			{
				if (ReferenceEquals(a, heir)) return ReferenceEquals(b, heir) ? 0 : -1;
				if (ReferenceEquals(b, heir)) return 1;
				return a.GetIntProperty(KingdomResidents.ResidentIdProperty)
					.CompareTo(b.GetIntProperty(KingdomResidents.ResidentIdProperty));
			});
			return true;
		}

		private static List<Cell> OpenRiteCells(Zone zone, Cell fixture, GameObject heir,
			int needed)
		{
			List<Cell> result = new List<Cell>();
			if (zone == null || fixture == null || needed <= 0) return result;
			int maxX = Math.Max(fixture.X, zone.Width - 1 - fixture.X);
			int maxY = Math.Max(fixture.Y, zone.Height - 1 - fixture.Y);
			int maxRadius = Math.Max(maxX, maxY);
			for (int radius = 1; radius <= maxRadius; radius++)
			{
				for (int y = fixture.Y - radius; y <= fixture.Y + radius; y++)
				for (int x = fixture.X - radius; x <= fixture.X + radius; x++)
				{
					if (Math.Max(Math.Abs(x - fixture.X), Math.Abs(y - fixture.Y)) != radius) continue;
					Cell cell = zone.GetCell(x, y);
					if (cell != null && cell.IsPassable(heir, false) && cell.Objects.Count == 0)
					{
						result.Add(cell);
						if (result.Count >= needed) return result;
					}
				}
			}
			return result;
		}

		private static GameObject FindFixture(Zone zone)
		{
			for (int p = 0; p < FixtureBlueprints.Length; p++)
			{
				GameObject found = null;
				foreach (GameObject obj in zone.GetObjects())
				{
					if (obj?.Blueprint != FixtureBlueprints[p] || obj.CurrentCell == null) continue;
					if (found == null || obj.CurrentCell.Y < found.CurrentCell.Y
						|| (obj.CurrentCell.Y == found.CurrentCell.Y
							&& obj.CurrentCell.X < found.CurrentCell.X)) found = obj;
				}
				if (found != null) return found;
			}
			return null;
		}

		private static GameObject FindByAssignedId(Zone zone, string id)
		{
			if (zone == null || string.IsNullOrEmpty(id)) return null;
			GameObject found = null;
			foreach (GameObject obj in zone.GetObjects())
			{
				if (string.Equals(obj.IDIfAssigned, id, StringComparison.Ordinal))
				{
					if (found != null) return null;
					found = obj;
				}
			}
			return found;
		}

		private static Zone ExactLoadedZone(string zoneId)
		{
			Zone zone = null;
			if (string.IsNullOrEmpty(zoneId) || The.ZoneManager?.CachedZones == null
				|| !The.ZoneManager.CachedZones.TryGetValue(zoneId, out zone)) return null;
			return zone;
		}

		private static bool OwnedGround(KingdomSystem system, string zoneId)
		{
			return system != null && system.OwnedZone(zoneId);
		}

		private sealed class Walker
		{
			internal readonly GameObject Body;
			internal readonly KingdomRiteAttendee Row;
			internal readonly Cell RiteCell;
			internal readonly Cell OriginalCell;
			internal Walker(GameObject body, KingdomRiteAttendee row, Cell rite, Cell original)
			{
				Body = body; Row = row; RiteCell = rite; OriginalCell = original;
			}
		}
	}
}
