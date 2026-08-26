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
	internal static partial class KingdomSuccessionRite
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

	}
}
