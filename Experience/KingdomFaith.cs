using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled shell for Addendum 5's shrine and education conversion channels
	/// (<see cref="KingdomFaithRules"/> owns every decision that has one right answer given the
	/// facts): consecrating a standing faith building to a creed from the Charter, the attended
	/// pass that lets a staffed, consecrated shrine draw the neutral toward it, and the query
	/// surface a staffed knowledge building offers the cohabitation and osmosis ladders so they
	/// read the ambient grudge one band gentler.
	/// <para>
	/// <b>What this file never does.</b> It never converts anyone OPPOSED to a shrine's creed
	/// &mdash; that stance is filtered out in <see cref="KingdomFaithRules.ClassifyStance"/>
	/// before a single property is written &mdash; and it never touches an opposed resident's
	/// emigration state itself. Addendum 5's own guard is that a settler may always emigrate
	/// rather than convert, answered once, by one surface, for every channel that can put a
	/// resident under pressure they might resent (a declared realm creed against theirs, a rival
	/// shrine consecrated in their own quarter). This file only supplies the trigger for the
	/// shrine's own case; see <see cref="HandOffOpposedPressure"/> for exactly where that call is
	/// made and what it is waiting on.
	/// </para>
	/// <para>
	/// <b>Where "quarter" lives.</b> Nothing here or anywhere else in the mod has a type or a
	/// field named "quarter" &mdash; Addendum 4d is explicit that the word names an emergent
	/// pattern in the layout, not a game object. A shrine's own quarter is read the same way
	/// every other attended pass in this mod reads locality (<c>KingdomCreed</c>'s dissent pass,
	/// <c>KingdomCeremony</c>'s raising roll call, <c>KingdomLocus</c>'s keeper and guest passes):
	/// the zone it stands in, not a radius, not a district.
	/// </para>
	/// </summary>
	public static class KingdomFaith
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionFaith") != "No";

		/// <summary>
		/// The creed a standing faith building was consecrated to, or empty for a shrine stone
		/// nobody has yet named a creed for. Written only by <see cref="OpenConsecration"/>.
		/// </summary>
		public const string ShrineCreedProperty = "KingdomShrineCreed";

		/// <summary>7b flag: has the founder already been told this consecrated shrine is
		/// standing empty of hands. Cleared the pass it is staffed again, so a shrine that lapses
		/// twice is announced twice.</summary>
		public const string ShrineLapsedAnnouncedProperty = "KingdomShrineLapsedAnnounced";

		/// <summary>7b flag: the same idiom, for a knowledge building built to be staffed that
		/// presently has nobody at it.</summary>
		public const string EducationLapsedAnnouncedProperty = "KingdomEducationLapsedAnnounced";

		/// <summary>
		/// Attended passes a neutral resident has spent drawn toward whichever staffed,
		/// consecrated shrine claimed them this zone's last pass. Lives on the resident, exactly
		/// as <c>KingdomCreed.CreedProperty</c> does, because a resident's own belief travelling
		/// with their own object &mdash; rather than a realm-level dictionary keyed by name, the
		/// way <c>KingdomSystem.LodgingGrace</c> tracks the UNHOUSED &mdash; is correct here: a
		/// shrine only ever pulls a resident who already has a place in this settlement's own
		/// zone, never one sleeping in the open with no stable object to speak of.
		/// </summary>
		public const string ShrinePullProperty = "KingdomShrinePull";

		/// <summary>Raw property name AssignWork stamps on every crewed work. No public const
		/// exists for it yet anywhere in the mod (<c>KingdomLocus</c> reads the same literal
		/// directly); this file follows that precedent rather than inventing a second one.</summary>
		private const string StaffedProperty = "KingdomStaffed";

		// ==================================================================================
		// The attended pass: shrine conversion, and both channels' 7b lapse lines.
		// ==================================================================================

		/// <summary>
		/// The kingdom's one attended pass over this zone's faith and knowledge buildings. Call
		/// from <c>KingdomSystem.HandleEvent(ZoneActivatedEvent)</c>, after growth has resolved
		/// this pass's staffing (<see cref="StaffedProperty"/> must already be current) and after
		/// creed has resolved this pass's dissent (residents' own creeds are stable facts by
		/// then). Wrapped by the caller's own <c>Guard</c>, like every other module's pass.
		/// </summary>
		/// <param name="System">The kingdom. Unfounded or a zone the realm does not claim does
		/// nothing.</param>
		/// <param name="Z">The activated zone.</param>
		/// <param name="Survey">This pass's already-taken survey, for its <c>Works</c> and
		/// <c>Settlers</c> lists.</param>
		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			HashSet<GameObject> claimed = new HashSet<GameObject>();
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				GameObject work = Survey.Works[i];
				KingdomRules.BuildEntry entry;
				string key = work.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				if (KingdomFaithRules.CanConsecrate(entry.Category))
				{
					RunShrine(System, Z, Survey, work, entry, claimed);
				}
				else if (KingdomFaithRules.IsEducationCategory(entry.Category))
				{
					RunEducationLapse(work, entry);
				}
			}
		}

		private static void RunShrine(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Shrine, KingdomRules.BuildEntry Entry, HashSet<GameObject> Claimed)
		{
			string shrineCreed = Shrine.GetStringProperty(ShrineCreedProperty);
			if (string.IsNullOrEmpty(shrineCreed))
			{
				// Not applicable: an unconsecrated shrine has no pass to run and says nothing
				// (STANDARDS 7b's other kind of early return).
				return;
			}
			bool staffed = Shrine.GetIntProperty(StaffedProperty) == 1;
			if (!staffed)
			{
				if (Shrine.GetIntProperty(ShrineLapsedAnnouncedProperty) != 1)
				{
					Shrine.SetIntProperty(ShrineLapsedAnnouncedProperty, 1);
					MessageQueue.AddPlayerMessage(KingdomFaithRules.ShrineLapsedLine(Entry.Name, KingdomCreed.CreedName(shrineCreed)));
				}
				return;
			}
			Shrine.SetIntProperty(ShrineLapsedAnnouncedProperty, 0);
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (Claimed.Contains(settler))
				{
					continue;
				}
				// Addendum 6: a shrine draws whoever is IN ITS REACH, which for an S or M plot is
				// its own quarter -- the cluster of built ground it stands in, measured, not the
				// whole zone. Before the claim, so a settler this shrine cannot reach is still
				// there for the shrine in their own quarter.
				if (!KingdomReach.Reaches(System, Z, Shrine, settler))
				{
					continue;
				}
				Claimed.Add(settler);
				string residentCreed = settler.GetStringProperty(KingdomCreed.CreedProperty);
				int hostility = KingdomCreed.HostilityBetween(residentCreed, shrineCreed);
				KingdomFaithRules.ShrineStance stance = KingdomFaithRules.ClassifyStance(residentCreed, shrineCreed, hostility);
				switch (stance)
				{
				case KingdomFaithRules.ShrineStance.Neutral:
					AdvancePull(System, Z, settler, shrineCreed, Entry.Name);
					break;
				case KingdomFaithRules.ShrineStance.Opposed:
					if (settler.GetIntProperty(ShrinePullProperty) != 0)
					{
						settler.SetIntProperty(ShrinePullProperty, 0);
					}
					HandOffOpposedPressure(System, Z, settler, shrineCreed);
					break;
				default:
					if (settler.GetIntProperty(ShrinePullProperty) != 0)
					{
						settler.SetIntProperty(ShrinePullProperty, 0);
					}
					break;
				}
			}
		}

		private static void AdvancePull(KingdomSystem System, Zone Z, GameObject Settler, string ShrineCreed, string BuildingName)
		{
			int pull = KingdomFaithRules.PullAfterPass(Settler.GetIntProperty(ShrinePullProperty));
			if (!KingdomFaithRules.ConversionReady(pull))
			{
				Settler.SetIntProperty(ShrinePullProperty, pull);
				return;
			}
			Settler.SetIntProperty(ShrinePullProperty, 0);
			string residentName = NameOf(Settler);
			// The one path a conversion may take. Calling KingdomCreed.Record directly here
			// skipped Forget (the old creed's tally never decremented), left the settler's
			// standing pressure entries in place, and wrote a chronicle line without the
			// two-register dispute the contested case demands. Convert does all of it, in the
			// only order that keeps the tally honest.
			if (!KingdomConversion.Convert(System, Z, Settler, ShrineCreed, ConversionChannel.Shrine))
			{
				return;
			}
			string creedName = KingdomCreed.CreedName(ShrineCreed);
			MessageQueue.AddPlayerMessage(KingdomFaithRules.ConversionMessage(residentName, creedName));
			KingdomLog.Log("faith: conversion " + residentName + " -> " + ShrineCreed + " at " + (Z?.ZoneID ?? "-"));
		}

		/// <summary>
		/// Addendum 5's guard, for the shrine's own case. A resident whose creed is OPPOSED to a
		/// staffed, consecrated shrine in their own quarter is never pulled toward it &mdash; the
		/// caller has already filtered that out &mdash; but the pressure of standing that ground
		/// anyway is real, and the guard promises it never becomes slow compulsion with no exit.
		/// <para>
		/// Hands off to <see cref="KingdomConversion.NotePressure"/> &mdash; the surface every
		/// channel shares &mdash; rather than reimplementing the grace here: the named, chronicled,
		/// attended-pass grace ending in the existing emigration machinery is that file's own state
		/// to own, not a second copy of <c>KingdomLodgingRules.GracePasses</c> kept here. This is
		/// deliberately the only place in this file that reaches outside
		/// <c>KingdomFaith</c>/<c>KingdomFaithRules</c> and the already-shipped
		/// <c>KingdomCreed</c>/<c>KingdomChronicle</c>/<c>KingdomLog</c> surfaces.
		/// </para>
		/// </summary>
		private static void HandOffOpposedPressure(KingdomSystem System, Zone Z, GameObject Resident, string ShrineCreed)
		{
			KingdomConversion.NotePressure(System, Z, Resident, ConversionChannel.Shrine, ShrineCreed);
		}

		private static void RunEducationLapse(GameObject Scriptorium, KingdomRules.BuildEntry Entry)
		{
			bool staffed = Scriptorium.GetIntProperty(StaffedProperty) == 1;
			if (staffed)
			{
				Scriptorium.SetIntProperty(EducationLapsedAnnouncedProperty, 0);
				return;
			}
			if (Scriptorium.GetIntProperty(EducationLapsedAnnouncedProperty) == 1)
			{
				return;
			}
			Scriptorium.SetIntProperty(EducationLapsedAnnouncedProperty, 1);
			MessageQueue.AddPlayerMessage(KingdomFaithRules.EducationLapsedLine(Entry.Name));
		}

		private static string NameOf(GameObject Resident)
		{
			string name = (Resident == null) ? null : Resident.GetStringProperty("KingdomName");
			if (!string.IsNullOrEmpty(name))
			{
				return name;
			}
			return (Resident == null) ? "a settler" : Resident.ShortDisplayName;
		}

		// ==================================================================================
		// Education's query surface -- consulted on demand, owns no pass of its own.
		// ==================================================================================

		/// <summary>
		/// Whether a staffed knowledge building stands in this zone right now. The one fact the
		/// cohabitation and osmosis ladders need; neither is told which building or which creed,
		/// because education softens the whole zone's grudge rather than taking a side in it.
		/// </summary>
		public static bool ZoneEducated(Zone Z)
		{
			if (!Enabled || Z == null)
			{
				return false;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1 || item.GetIntProperty(StaffedProperty) != 1)
				{
					continue;
				}
				string key = item.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry))
				{
					continue;
				}
				if (KingdomFaithRules.IsEducationCategory(entry.Category))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Convenience for a caller that already has the resident's own closeness rung and just
		/// wants education's own softening folded in: one band gentler when a knowledge building
		/// actually reaches this roof, the rung unchanged otherwise. See
		/// <c>KingdomFaithRules.SoftenedCloseness</c> for the arithmetic, and
		/// <c>Growth/KingdomLodging.cs</c> for the two call sites.
		/// </summary>
		/// <param name="Z">The zone the roof stands in.</param>
		/// <param name="Quarters">The resident's own closeness rung.</param>
		/// <param name="Home">The roof being judged. Naming it asks the re-based question
		/// (Addendum 6: education softens the grudge of whoever the knowledge work REACHES); a
		/// caller that cannot name one gets the zone-wide answer this file has always given.
		/// </param>
		public static KingdomLodgingRules.Closeness EducatedCloseness(Zone Z, KingdomLodgingRules.Closeness Quarters, GameObject Home = null)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			bool educated = (Home != null && system != null)
				? KingdomReach.EducatedAt(system, Z, Home)
				: ZoneEducated(Z);
			return educated ? KingdomFaithRules.SoftenedCloseness(Quarters) : Quarters;
		}

		// ==================================================================================
		// Consecration -- the Charter's own ceremony.
		// ==================================================================================

		/// <summary>
		/// The Charter's "consecrate a shrine" action: names a standing faith building for a
		/// creed the realm has dealt with. One creed per shrine; naming a second creed later is a
		/// second ceremony, and the chronicle keeps the first exactly as it was written.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Founder">The Charter's own object, for its current zone &mdash; the same
		/// shape every other Charter action in this mod takes (<c>KingdomDesign.RenameBuilding</c>,
		/// <c>KingdomSocket.OpenConvert</c>).</param>
		public static void OpenConsecration(KingdomSystem System, GameObject Founder)
		{
			if (!Enabled || System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			Zone zone = Founder?.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A shrine is consecrated standing on the realm's own ground.");
				return;
			}
			List<GameObject> shrines = FaithBuildingsIn(zone);
			if (shrines.Count == 0)
			{
				Popup.Show("Nothing built here answers to a creed. Raise a shrine stone, a shrine garth, or a temple first.");
				return;
			}
			string[] shrineOptions = new string[shrines.Count];
			for (int i = 0; i < shrines.Count; i++)
			{
				string held = shrines[i].GetStringProperty(ShrineCreedProperty);
				shrineOptions[i] = shrines[i].ShortDisplayName + (string.IsNullOrEmpty(held) ? "" : (" {{C|[" + KingdomCreed.CreedName(held) + "]}}"));
			}
			int picked = Popup.PickOption(Title: "Consecrate a shrine, at " + System.SeatName, Options: shrineOptions, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject target = shrines[picked];
			List<string> candidates = KingdomCreed.Candidates(System);
			if (candidates.Count == 0)
			{
				Popup.Show("The realm has dealt with nobody yet that it could consecrate a shrine to. Standings come first.");
				return;
			}
			string currentCreed = target.GetStringProperty(ShrineCreedProperty);
			string[] creedOptions = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				creedOptions[i] = KingdomCreed.CreedName(candidates[i]) + ((candidates[i] == currentCreed) ? " {{G|[consecrated]}}" : "");
			}
			int creedPicked = Popup.PickOption(Title: "Consecrate " + target.ShortDisplayName + " to", Options: creedOptions, AllowEscape: true);
			if (creedPicked < 0)
			{
				return;
			}
			string chosenCreed = candidates[creedPicked];
			if (chosenCreed == currentCreed)
			{
				Popup.Show("It is consecrated to them already.");
				return;
			}
			bool reconsecration = !string.IsNullOrEmpty(currentCreed);
			bool neverStaffable = !KingdomData.TryGetBuilding(target.GetStringProperty(KingdomUpgrade.BuildKeyProperty), out KingdomRules.BuildEntry entry) || entry.Staff <= 0;
			string creedDisplay = KingdomCreed.CreedName(chosenCreed);
			if (Popup.ShowYesNo(KingdomFaithRules.ConsecrationPrompt(target.ShortDisplayName, creedDisplay, reconsecration, neverStaffable)) != DialogResult.Yes)
			{
				return;
			}
			target.SetStringProperty(ShrineCreedProperty, chosenCreed);
			target.SetIntProperty(ShrineLapsedAnnouncedProperty, 0);
			KingdomChronicle.Record(System, KingdomFaithRules.ConsecrationChronicle(target.ShortDisplayName, System.SeatName, creedDisplay, reconsecration));
			Popup.Show(KingdomFaithRules.ConsecrationNotice(target.ShortDisplayName, creedDisplay, reconsecration, neverStaffable));
			KingdomLog.Log("faith: consecrated " + target.ShortDisplayName + " to " + chosenCreed + " reconsecration=" + reconsecration);
		}

		private static List<GameObject> FaithBuildingsIn(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
				{
					continue;
				}
				string key = item.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry))
				{
					continue;
				}
				if (KingdomFaithRules.CanConsecrate(entry.Category))
				{
					found.Add(item);
				}
			}
			return found;
		}
	}
}
