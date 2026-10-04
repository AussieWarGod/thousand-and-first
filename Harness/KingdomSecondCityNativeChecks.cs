using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Behavioural coverage row 12 "Multiple cities": the frame. City one is founded by the built-in
	/// realize verb (the production first-city transaction) and lives one ordinary turn; this shard
	/// then proves no polity window is open yet, reads the bordering zone's verdict and has
	/// KingdomSecondCityNativeSite resolve a second site on a NON-adjacent surface zone, one world
	/// parasang out and three zones from city one, probing the nearest candidates in order, and the
	/// four cases in KingdomSecondCityNativeCases drive travel, the production second founding, its
	/// already-ours refusal, and the production seat exchange and polity window on return.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED: the founder is relocated with zero-energy SystemMoveTo plus
	/// SetActiveZone rather than walked across the world map, and the second city is founded
	/// through KingdomFoundingTransaction.TryFoundSecondWithoutWater - the transaction
	/// KingdomFounding.FoundSecond wraps and the basin rite's own transaction minus the water and
	/// the three Popup prompts a sealed script cannot answer. Force is NEVER used: the adjacency
	/// law is observed, not bypassed. No case spends a turn and no population, building or
	/// stockpile exists in city two.
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

		private static readonly StringBuilder Detail = new StringBuilder();
		private static int Passed;
		private static int Failed;
		private static bool Bound;

		internal static string HomeZoneId;
		internal static string SiteZoneId;
		internal static Zone SiteZone;
		internal static Cell HomeCell;
		internal static Cell SiteCell;
		internal static KingdomPlotRules.PlotRect SiteHeart;
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
			// The return must be the realm's first polity reconciliation, so the window it opens
			// freezes the two-city facts (KingdomSecondCityNativeCases.ReturnSeat reads it).
			Require(system.PolityDispatch == null || !system.PolityDispatch.HasWindow,
				"a polity dispatch window is already open before the return");
			string border = ObserveBorder(system, Zone);
			string site;
			IList<string> rejections;
			string refusal;
			IList<string> probed;
			bool found = KingdomSecondCityNativeSite.TryResolve(system, out site, out rejections,
				out refusal, out probed);
			string rejected = KingdomSecondCitySiteRules.RejectedList(rejections);
			// The probes must be the nearest candidates in order, each rejected with a reason or
			// chosen: a search that skipped, reordered or filtered them would stand in for
			// production's adjacency law.
			string order = KingdomSecondCitySiteRules.ProbeOrderFault(HomeZoneId, probed,
				rejections.Count, found);
			bool sited = found && order == null;
			string row = sited
				? KingdomSecondCitySiteRules.SiteRow(HomeZoneId, HomeCell.X, HomeCell.Y, border,
					site, rejected)
				: KingdomSecondCitySiteRules.RefusedSiteRow(HomeZoneId, HomeCell.X, HomeCell.Y,
					border, order ?? refusal, rejected);
			// Journalled with its real outcome on both paths, BEFORE a refusal is thrown: the
			// verb row's failure text is bounded to 300 characters, so a refused search keeps its
			// rejected candidates and their reasons only here and in the log.
			string note = KingdomScenarioJournal.Append(KingdomSecondCityScript.SiteRow, sited, row);
			string unjournalled = note == null ? "" : "; the site row was not journalled: " + note;
			Require(order == null, order + unjournalled);
			Require(found, refusal + unjournalled);
			Detail.Append("; ").Append(site).Append(" polity-window=none");
			Require(note == null, "second-city-site journal unavailable");
			Bound = true;
		}

		/// <summary>The tabled negative: a bordering zone is claimed, not founded.</summary>
		private static string ObserveBorder(KingdomSystem System, Zone Zone)
		{
			string border = Zone.GetZoneIDFromDirection("E");
			Require(!string.IsNullOrEmpty(border) && border != HomeZoneId
				&& KingdomFounding.ZonesAdjacent(HomeZoneId, border),
				"the eastern neighbour did not read as a bordering zone");
			Zone bordering = The.ZoneManager.GetZone(border);
			Require(bordering != null, "the bordering zone could not be built");
			Require(KingdomFounding.JudgeSite(System, bordering)
				== KingdomSettlement.SecondFoundingVerdict.GroundIsTooClose,
				"the bordering zone did not refuse as GroundIsTooClose");
			return border;
		}
	}
}
