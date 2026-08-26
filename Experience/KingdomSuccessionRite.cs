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
	/// <summary>Engine edge for C8/C12. Qud offers no post-death cancellable turn seam, so this
	/// adapter stages physical evidence inside AfterDie: existing bodies walk to an existing civic
	/// fixture, other mourners walk back, the founder shrine is placed exactly once, and only then
	/// may KingdomSuccession call GamePlayer.SetBody.</summary>
	internal static class KingdomSuccessionRite
	{
		internal const string ShrineBlueprint = "r_KingdomFounderShrine";
		private const int MaxWalkSteps = 4096;
		private static readonly string[] FixtureBlueprints =
		{
			"r_KingdomFirstBasin", "r_KingdomGreatCourt", "r_KingdomMootYard",
			"r_KingdomWaterstone", "r_KingdomRiteGround", "r_KingdomShrineGarth",
			"r_KingdomShrine", "r_KingdomTemple"
		};

		internal sealed class Plan
		{
			internal string ZoneId;
			internal string CityName;
			internal string FixtureObjectId;
			internal string FixtureName;
			internal int ShrineX;
			internal int ShrineY;
			internal string Manifest;
		}

		internal static bool TryFreeze(KingdomSystem system, KingdomCityBook cityBook,
			GameObject heir, string cityName, out Plan plan, out string failure)
		{
			plan = null;
			failure = "";
			Zone zone = heir?.CurrentZone;
			string zoneId = zone?.ZoneID;
			if (system == null || !GameObject.Validate(heir) || !heir.IsAlive
				|| heir.Brain == null || zone == null || string.IsNullOrEmpty(zoneId)
				|| !OwnedGround(system, zoneId))
			{
				failure = "the exact heir is not standing on owned, authored city ground";
				return false;
			}

			GameObject fixture = FindFixture(zone);
			if (!GameObject.Validate(fixture) || fixture.CurrentCell == null
				|| string.IsNullOrEmpty(fixture.ID))
			{
				failure = "no extant civic mourning fixture exists in the heir's city";
				return false;
			}

			List<GameObject> bodies;
			if (!TryExactResidentsIn(zone, system, cityBook, heir, out bodies, out failure))
			{
				return false;
			}
			List<Cell> open = OpenRiteCells(zone, fixture.CurrentCell, heir,
				bodies.Count + 1);
			if (open.Count < bodies.Count + 1)
			{
				failure = "the civic fixture has too little open ground for every named resident present and the founder marker";
				return false;
			}

			List<KingdomRiteAttendee> rows = new List<KingdomRiteAttendee>();
			for (int i = 0; i < bodies.Count; i++)
			{
				GameObject body = bodies[i];
				int targetIndex = -1;
				for (int j = 0; j < open.Count; j++)
				{
					if (CanWalk(body, open[j]))
					{
						targetIndex = j;
						break;
					}
				}
				if (targetIndex < 0)
				{
					failure = ReferenceEquals(body, heir)
						? "the exact heir cannot physically reach the mourning fixture"
						: "a named resident present cannot physically reach any open place at the mourning fixture";
					return false;
				}
				Cell target = open[targetIndex];
				open.RemoveAt(targetIndex);
				Cell at = body.CurrentCell;
				rows.Add(new KingdomRiteAttendee(body.GetIntProperty(
					KingdomResidents.ResidentIdProperty), body.IDIfAssigned,
					body.GetStringProperty("KingdomName") ?? body.BaseDisplayNameStripped,
					zoneId, at.X, at.Y, PostReceipt(body),
					body.GetStringProperty(KingdomLodging.HomePlotIdProperty) ?? "",
					target.X, target.Y));
			}
			if (rows.Count == 0 || rows[0].ResidentId != heir.GetIntProperty(
				KingdomResidents.ResidentIdProperty))
			{
				failure = "the chosen heir could not be frozen first in the procession";
				return false;
			}
			Cell shrineCell = open[0];
			string manifest = KingdomSuccessionRules.EncodeRiteManifest(rows.ToArray());
			if (string.IsNullOrEmpty(manifest))
			{
				failure = "the exact procession manifest exceeded its persistence bound";
				return false;
			}
			plan = new Plan
			{
				ZoneId = zoneId,
				CityName = string.IsNullOrEmpty(cityName) ? "the settlement" : cityName,
				FixtureObjectId = fixture.IDIfAssigned,
				FixtureName = fixture.BaseDisplayNameStripped,
				ShrineX = shrineCell.X,
				ShrineY = shrineCell.Y,
				Manifest = manifest
			};
			return true;
		}

		internal static bool TryHoldProcession(KingdomSystem system, string token, string zoneId,
			string fixtureObjectId, string manifest, out GameObject heir, out string failure)
		{
			heir = null;
			failure = "";
			Zone zone = ExactLoadedZone(zoneId);
			KingdomRiteAttendee[] rows;
			GameObject fixture = FindByAssignedId(zone, fixtureObjectId);
			if (system == null || zone == null || !OwnedGround(system, zoneId)
				|| !KingdomSuccessionRules.TryDecodeRiteManifest(manifest, out rows)
				|| !GameObject.Validate(fixture) || fixture.CurrentCell == null)
			{
				failure = "the frozen procession locus or manifest no longer exists exactly";
				return false;
			}

			List<Walker> walkers = new List<Walker>();
			for (int i = 0; i < rows.Length; i++)
			{
				GameObject body;
				string boundZone;
				if (!KingdomResidents.TryResolveBoundBody(system, rows[i].ResidentId,
					LoadZone: false, out body, out boundZone)
					|| !string.Equals(boundZone, zoneId, StringComparison.Ordinal)
					|| !string.Equals(body.IDIfAssigned, rows[i].ObjectId, StringComparison.Ordinal)
					|| !string.Equals(body.GetStringProperty("KingdomName")
						?? body.BaseDisplayNameStripped, rows[i].Name, StringComparison.Ordinal)
					|| body.Brain == null || !UnchangedPosts(body, rows[i]))
				{
					failure = "a named attendee no longer matches the frozen body, post, or home";
					return false;
				}
				Cell target = zone.GetCell(rows[i].RiteX, rows[i].RiteY);
				Cell original = zone.GetCell(rows[i].OriginalX, rows[i].OriginalY);
				if (target == null || original == null)
				{
					failure = "an attendee cell fell outside the frozen city ground";
					return false;
				}
				walkers.Add(new Walker(body, rows[i], target, original));
			}
			if (string.Equals(fixture.GetStringProperty("KingdomLastMourningRiteToken"),
				token, StringComparison.Ordinal))
			{
				// The fixture token is written only after everybody stood at the rite. A cut
				// while mourners were walking home resumes restoration, never the ceremony.
				if (!Walk(walkers[0], walkers[0].RiteCell))
				{
					failure = "the proved rite no longer has its exact heir at the boundary";
					return false;
				}
				for (int i = walkers.Count - 1; i >= 1; i--)
				{
					if (!Walk(walkers[i], walkers[i].OriginalCell))
					{
						failure = "the proved rite could not finish restoring a mourner";
						return false;
					}
				}
				for (int i = 0; i < walkers.Count; i++)
				{
					Cell wanted = i == 0 ? walkers[i].RiteCell : walkers[i].OriginalCell;
					if (walkers[i].Body.CurrentCell != wanted
						|| !UnchangedPosts(walkers[i].Body, walkers[i].Row))
					{
						failure = "the proved rite's restoration evidence is incomplete";
						return false;
					}
				}
				heir = walkers[0].Body;
				return true;
			}

			for (int i = 0; i < walkers.Count; i++)
			{
				if (!Walk(walkers[i], walkers[i].RiteCell))
				{
					ReturnAll(walkers, includeHeir: true);
					failure = "a named attendee could not walk the frozen route to the fixture";
					return false;
				}
			}
			for (int i = 0; i < walkers.Count; i++)
			{
				if (walkers[i].Body.CurrentCell != walkers[i].RiteCell
					|| !UnchangedPosts(walkers[i].Body, walkers[i].Row))
				{
					ReturnAll(walkers, includeHeir: true);
					failure = "the assembled procession lost exact physical or schedule evidence";
					return false;
				}
			}

			fixture.SetStringProperty("KingdomLastMourningRiteToken", token);
			fixture.SetStringProperty("KingdomLastMourningAttendees", manifest);
			for (int i = walkers.Count - 1; i >= 1; i--)
			{
				if (!Walk(walkers[i], walkers[i].OriginalCell))
				{
					ReturnAll(walkers, includeHeir: true);
					failure = "a mourner could not walk back to their exact prior place";
					return false;
				}
			}
			for (int i = 1; i < walkers.Count; i++)
			{
				if (walkers[i].Body.CurrentCell != walkers[i].OriginalCell
					|| !UnchangedPosts(walkers[i].Body, walkers[i].Row))
				{
					ReturnAll(walkers, includeHeir: true);
					failure = "a mourner's prior place, post, or home was not restored exactly";
					return false;
				}
			}
			heir = walkers[0].Body;
			return true;
		}

		internal static bool ProcessionEvidence(KingdomSystem system, string token, string zoneId,
			string fixtureObjectId, string manifest, out GameObject heir)
		{
			heir = null;
			Zone zone = ExactLoadedZone(zoneId);
			KingdomRiteAttendee[] rows;
			GameObject fixture = FindByAssignedId(zone, fixtureObjectId);
			if (system == null || zone == null || !OwnedGround(system, zoneId)
				|| !KingdomSuccessionRules.TryDecodeRiteManifest(manifest, out rows)
				|| !GameObject.Validate(fixture)
				|| !string.Equals(fixture.GetStringProperty("KingdomLastMourningRiteToken"),
					token, StringComparison.Ordinal)) return false;
			for (int i = 0; i < rows.Length; i++)
			{
				GameObject body;
				string bound;
				if (!KingdomResidents.TryResolveBoundBody(system, rows[i].ResidentId, false,
					out body, out bound) || body.IDIfAssigned != rows[i].ObjectId
					|| !UnchangedPosts(body, rows[i])) return false;
				Cell wanted = zone.GetCell(i == 0 ? rows[i].RiteX : rows[i].OriginalX,
					i == 0 ? rows[i].RiteY : rows[i].OriginalY);
				if (body.CurrentCell != wanted) return false;
				if (i == 0) heir = body;
			}
			return GameObject.Validate(heir);
		}

		internal static bool TryEnsureFounderShrine(string token, string founderName,
			long deathTick, string cause, string history, string cityName, string zoneId,
			string fixtureObjectId, int shrineX, int shrineY, string receiptObjectId,
			out GameObject shrine, out string failure)
		{
			shrine = null;
			failure = "";
			Zone zone = ExactLoadedZone(zoneId);
			Cell cell = zone?.GetCell(shrineX, shrineY);
			if (zone == null || cell == null)
			{
				failure = "the frozen founder-shrine cell is unavailable";
				return false;
			}
			List<GameObject> matches = new List<GameObject>();
			foreach (GameObject candidate in zone.GetObjects())
			{
				r_KingdomFounderShrine part = candidate?.GetPart<r_KingdomFounderShrine>();
				if (part != null && part.Matches(token)) matches.Add(candidate);
			}
			if (matches.Count > 1)
			{
				failure = "more than one in-run shrine claims the same founder death";
				return false;
			}
			GameObject evidence = matches.Count == 1 ? matches[0] : null;
			bool hasReceipt = !string.IsNullOrEmpty(receiptObjectId);
			bool exact = GameObject.Validate(evidence) && evidence.CurrentCell == cell
				&& (!hasReceipt || evidence.IDIfAssigned == receiptObjectId);
			FounderShrinePlacementVerdict verdict = KingdomSuccessionRules.JudgeFounderShrinePlacement(
				hasReceipt || evidence != null, exact, cell.IsPassable(null, false), cell.Objects.Count);
			if (verdict == FounderShrinePlacementVerdict.AdoptExact)
			{
				shrine = evidence;
				return true;
			}
			if (verdict != FounderShrinePlacementVerdict.Create)
			{
				failure = "the founder-shrine receipt conflicts with its exact ground";
				return false;
			}

			GameObject created = null;
			try
			{
				created = GameObject.Create(ShrineBlueprint);
				r_KingdomFounderShrine part = created?.GetPart<r_KingdomFounderShrine>();
				if (part == null) throw new InvalidOperationException("founder shrine blueprint has no history part");
				part.Stamp(token, founderName, deathTick, cause, history, cityName, fixtureObjectId);
				created.SetIntProperty("KingdomFounderShrine", 1);
				created.SetStringProperty("KingdomFounderDeathToken", token);
				cell.AddObject(created);
				if (created.CurrentCell != cell || !cell.Objects.Contains(created))
				{
					throw new InvalidOperationException("founder shrine did not remain on its frozen cell");
				}
				string id = created.ID;
				if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("founder shrine has no exact object id");
				shrine = created;
				return true;
			}
			catch (Exception ex)
			{
				r_KingdomFounderShrine retained = created?.GetPart<r_KingdomFounderShrine>();
				if (GameObject.Validate(created) && created.CurrentCell == cell
					&& retained != null && retained.Matches(token)
					&& cell.Objects.Contains(created) && !string.IsNullOrEmpty(created.IDIfAssigned))
				{
					// Cell.AddObject and ID assignment can publish before a later callback throws.
					// Exact ground is authority; adopt the proved object instead of minting again.
					shrine = created;
					return true;
				}
				if (GameObject.Validate(created) && created.CurrentCell == null)
				{
					try { created.Obliterate(); } catch { }
				}
				failure = "founder shrine placement failed (" + ex.GetType().Name + ")";
				return false;
			}
		}

		private static bool Walk(Walker walker, Cell destination)
		{
			GameObject body = walker?.Body;
			if (!GameObject.Validate(body) || body.Brain == null || destination == null
				|| body.CurrentZone != destination.ParentZone) return false;
			if (body.CurrentCell == destination) return true;
			List<GoalHandler> goals = new List<GoalHandler>(body.Brain.Goals.Items);
			GlobalLocation anchor = body.Brain.StartingCell == null ? null
				: new GlobalLocation(body.Brain.StartingCell.ToString());
			bool staying = body.Brain.Staying;
			try
			{
				body.Brain.PushGoal(new MoveTo(destination, careful: true,
					overridesCombat: true, AbortIfMoreSteps: MaxWalkSteps));
				FindPath path = new FindPath(body.CurrentZone.ZoneID, body.CurrentCell.X,
					body.CurrentCell.Y, destination.ParentZone.ZoneID, destination.X,
					destination.Y, PathGlobal: false, PathUnlimited: false, Looker: body,
					Juggernaut: false, IgnoreCreatures: false, IgnoreGases: false,
					FlexPhase: false, MaxWeight: 95);
				if (!path.Usable || path.Directions.Count > MaxWalkSteps) return false;
				for (int i = 0; i < path.Directions.Count; i++)
				{
					if (!body.Move(path.Directions[i], Forced: false, System: false,
						AllowDashing: false, DoConfirmations: false, EnergyCost: 0,
						Type: "KingdomMourningProcession", Peaceful: true)) return false;
				}
				return body.CurrentCell == destination;
			}
			finally
			{
				body.Brain.Goals.Items.Clear();
				body.Brain.Goals.Items.AddRange(goals);
				body.Brain.StartingCell = anchor;
				body.Brain.Staying = staying;
			}
		}

		private static bool CanWalk(GameObject body, Cell destination)
		{
			if (!GameObject.Validate(body) || body.CurrentCell == null || destination == null) return false;
			if (body.CurrentCell == destination) return true;
			FindPath path = new FindPath(body.CurrentZone.ZoneID, body.CurrentCell.X,
				body.CurrentCell.Y, destination.ParentZone.ZoneID, destination.X, destination.Y,
				PathGlobal: false, PathUnlimited: false, Looker: body, Juggernaut: false,
				IgnoreCreatures: false, IgnoreGases: false, FlexPhase: false, MaxWeight: 95);
			return path.Usable && path.Directions.Count <= MaxWalkSteps;
		}

		private static void ReturnAll(List<Walker> walkers, bool includeHeir)
		{
			for (int i = walkers.Count - 1; i >= (includeHeir ? 0 : 1); i--)
			{
				try { Walk(walkers[i], walkers[i].OriginalCell); } catch { }
			}
		}

		private static bool UnchangedPosts(GameObject body, KingdomRiteAttendee row)
		{
			return string.Equals(PostReceipt(body), row.Post, StringComparison.Ordinal)
				&& string.Equals(body.GetStringProperty(KingdomLodging.HomePlotIdProperty) ?? "",
					row.Home, StringComparison.Ordinal);
		}

		private static string PostReceipt(GameObject body)
		{
			return KingdomStations.PostOf(body).ToString(CultureInfo.InvariantCulture) + "/"
				+ body.GetIntProperty(KingdomStations.PostKindProperty).ToString(CultureInfo.InvariantCulture);
		}

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
			return !string.IsNullOrEmpty(zoneId) && ((system.ClaimedZones != null
				&& system.ClaimedZones.Contains(zoneId)) || (system.Away?.ClaimedZones != null
				&& system.Away.ClaimedZones.Contains(zoneId)));
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
