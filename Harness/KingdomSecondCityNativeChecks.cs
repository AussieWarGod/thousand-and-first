using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Behavioural coverage row 12 "Multiple cities": the frame. City one is founded by the
	/// built-in realize verb (the production first-city transaction); this shard then resolves a
	/// second site on a NON-adjacent surface parasang, and the four cases in
	/// KingdomSecondCityNativeCases drive travel, the production second founding, its
	/// already-ours refusal, and the production seat exchange on return.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED: the founder is relocated with zero-energy SystemMoveTo plus
	/// SetActiveZone rather than walked across the world map, and the second city is founded
	/// through KingdomFounding.FoundSecond - the same transaction the basin rite commits, minus
	/// the water and the three Popup prompts a sealed script cannot answer. Force is NEVER used:
	/// the adjacency law is observed, not bypassed. No turns are spent and no population,
	/// building or stockpile exists in city two.
	/// </para>
	/// NOT YET NATIVELY RUN.
	/// </summary>
	internal static class KingdomSecondCityNativeChecks
	{
		/// <summary>Frozen names for the second city and for the refused repeat attempt.</summary>
		internal const string SecondCityName = "Ashgat";
		internal const string RefusedCityName = "Ashgat Repeat";
		internal const string SecondVocation = "waystation";
		internal const string RefusedVocation = "refuge";

		/// <summary>How many candidate parasangs may be built before the site search refuses.</summary>
		internal const int MaxProbes = 8;

		private static readonly StringBuilder Detail = new StringBuilder();
		private static int Passed;
		private static int Failed;
		private static bool Bound;

		internal static string HomeZoneId;
		internal static string SiteZoneId;
		internal static Zone SiteZone;
		internal static Cell HomeCell;
		internal static Cell SiteCell;
		internal static string FirstSettlementId;
		internal static string SecondSettlementId;
		internal static string RealmFactionName;
		internal static long Turns;
		internal static long Ticks;

		internal static bool Vacant { get { return !Bound && Passed == 0 && Failed == 0; } }

		internal static bool Completed
		{
			get { return Passed + Failed >= KingdomSecondCityScript.CheckCalls; }
		}

		internal static bool Ok { get { return Failed == 0; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, out bool Complete)
		{
			if (Verb == KingdomSecondCityScript.SetupVerb) Bind(Game, Zone);
			else RunCase(Game);
			Complete = Completed;
			return Report();
		}

		private static string Report()
		{
			return "native-second-city cases="
				+ KingdomSecondCityScript.CheckCalls.ToString()
				+ " passed=" + Passed.ToString() + " failed=" + Failed.ToString() + Detail;
		}

		internal static string Fail(Exception Error)
		{
			KingdomLog.Log("native-second-city retained failure: " + Error + Detail);
			return "native-second-city cases=" + KingdomSecondCityScript.CheckCalls.ToString()
				+ " passed=" + Passed.ToString() + " failed=" + (Failed + 1).ToString()
				+ "; failure=" + KingdomScenarioRules.Bounded(
					Error.GetType().Name + ": " + Error.Message) + Detail;
		}

		internal static void Require(bool Value, string Failure)
		{
			KingdomSecondCityNativeProvider.Require(Value, Failure);
		}

		private static void RunCase(XRLGame Game)
		{
			Require(Bound, "the second-city frame was never bound");
			int index = Passed + Failed;
			Require(index < KingdomSecondCityScript.CheckCalls,
				"the second-city script called more checks than it declares cases");
			try
			{
				KingdomSecondCityNativeCases.Run(index, Game, Detail);
				Passed++;
			}
			catch (Exception error)
			{
				Failed++;
				Detail.Append("; case-failure=").Append(KingdomScenarioRules.Bounded(
					error.GetType().Name + ": " + error.Message));
				KingdomLog.Log("native-second-city case " + index + " retained failure: " + error);
			}
		}

		/// <summary>
		/// Binds the realized first city and resolves the second site. Observation only: nothing
		/// here founds, claims or moves anything.
		/// </summary>
		private static void Bind(XRLGame Game, Zone Zone)
		{
			Require(!Bound, "the second-city frame is already bound");
			KingdomSystem system = Game == null ? null : Game.GetSystem<KingdomSystem>();
			Require(system != null && system.Founded,
				"the second-city frame needs the realized first city");
			Require(system.SettlementCount == 1 && system.NonSeatSettlementCount == 0,
				"the realm already holds more than the realized first city");
			Require(Zone != null && ReferenceEquals(The.ZoneManager != null
				? The.ZoneManager.ActiveZone : null, Zone),
				"the first city's ground is not the active ground");
			Require(system.City != null && !string.IsNullOrEmpty(system.City.SettlementId),
				"the first city has no settlement identity");
			HomeZoneId = Zone.ZoneID;
			FirstSettlementId = system.City.SettlementId;
			RealmFactionName = system.KingdomFactionName;
			HomeCell = The.Player == null ? null : The.Player.CurrentCell;
			Require(HomeCell != null && ReferenceEquals(HomeCell.ParentZone, Zone),
				"the founder is not standing in the first city");
			Require(system.ClaimedZones.Contains(HomeZoneId),
				"the first city does not claim its own ground");
			Turns = Game.Turns;
			Ticks = Game.TimeTicks;
			string border = ObserveBorder(system, Zone);
			ResolveSite(system);
			Require(KingdomScenarioJournal.Append(KingdomSecondCityScript.SiteRow, true,
				"home=" + HomeZoneId + "; border=" + border
				+ " border-verdict=GroundIsTooClose; site=" + SiteZoneId
				+ " adjacent=false claimed=false verdict=Allowed"
				+ "; synthetic-travel=true synthetic-water=false-spent force=false") == null,
				"second-city-site journal unavailable");
			Bound = true;
		}

		/// <summary>The tabled negative: a bordering parasang is claimed, not founded.</summary>
		private static string ObserveBorder(KingdomSystem System, Zone Zone)
		{
			string border = Zone.GetZoneIDFromDirection("E");
			Require(!string.IsNullOrEmpty(border) && border != HomeZoneId
				&& KingdomFounding.ZonesAdjacent(HomeZoneId, border),
				"the eastern neighbour did not read as a bordering parasang");
			Zone bordering = The.ZoneManager.GetZone(border);
			Require(bordering != null, "the bordering parasang could not be built");
			Require(KingdomFounding.JudgeSite(System, bordering)
				== KingdomSettlement.SecondFoundingVerdict.GroundIsTooClose,
				"the bordering parasang did not refuse as GroundIsTooClose");
			return border;
		}

		private static void ResolveSite(KingdomSystem System)
		{
			IList<string> candidates = KingdomSecondCitySiteRules.Candidates(HomeZoneId);
			Require(candidates.Count > 0,
				"no surface parasang outside the bordering band exists for " + HomeZoneId);
			List<string> tried = new List<string>();
			int probes = 0;
			for (int i = 0; i < candidates.Count && probes < MaxProbes; i++)
			{
				string id = candidates[i];
				if (System.ClaimedZones.Contains(id)) { tried.Add(id + " (claimed)"); continue; }
				if (KingdomFounding.ZonesAdjacent(HomeZoneId, id))
				{ tried.Add(id + " (adjacent)"); continue; }
				probes++;
				Zone zone;
				try { zone = The.ZoneManager.GetZone(id); }
				catch (Exception) { tried.Add(id + " (unbuildable)"); continue; }
				if (zone == null) { tried.Add(id + " (null)"); continue; }
				if (KingdomRules.GroundIsForeignFaction(zone.GetZoneProperty("faction", null),
					RealmFactionName))
				{ tried.Add(id + " (foreign)"); continue; }
				KingdomSettlement.SecondFoundingVerdict verdict =
					KingdomFounding.JudgeSite(System, zone);
				if (verdict != KingdomSettlement.SecondFoundingVerdict.Allowed)
				{ tried.Add(id + " (" + verdict + ")"); continue; }
				Cell landing = Landing(zone);
				if (landing == null) { tried.Add(id + " (no landing)"); continue; }
				SiteZoneId = id;
				SiteZone = zone;
				SiteCell = landing;
				Detail.Append("; site=").Append(id).Append(" probes=").Append(probes)
					.Append(" rejected=").Append(tried.Count);
				return;
			}
			Require(false, "no eligible second-city site within "
				+ KingdomSecondCitySiteRules.MaxRing + " parasangs; tried "
				+ KingdomScenarioRules.Bounded(string.Join(", ", tried.ToArray())));
		}

		internal static Cell Landing(Zone Zone)
		{
			for (int y = 1; y < Zone.Height - 1; y++)
				for (int x = 1; x < Zone.Width - 1; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell != null && cell.Objects.Count == 0) return cell;
				}
			return null;
		}
	}
}
