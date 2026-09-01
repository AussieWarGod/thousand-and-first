using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomFaith
	{
		// ==================================================================================
		// Education's query surface -- consulted on demand, owns no pass of its own.
		// ==================================================================================

		/// <summary>
		/// Whether a live staffed education provider stands in a correct designation right now. The one fact the
		/// cohabitation and osmosis ladders need; neither is told which building or which creed,
		/// because education softens the whole zone's grudge rather than taking a side in it.
		/// </summary>
		public static bool ZoneEducated(Zone Z)
		{
			if (!Enabled || Z == null)
			{
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			return KingdomCapabilityRuntime.Roots(Z, survey,
				KingdomBenefitCapabilities.Education, "education").Count > 0;
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
			int picked = Popup.PickOption(Title: "Consecrate a shrine, at " + KingdomPresentation.Rich(System.SeatName), Options: shrineOptions, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject target = shrines[picked];
			if (KingdomLabCivicRuntime.BlocksConsecration(System, zone, target,
				out string civicReason))
			{
				Popup.Show(civicReason);
				return;
			}
			List<string> candidates = KingdomCreed.Candidates(System);
			candidates.RemoveAll(delegate(string creed)
			{
				return !KingdomData.CreedUsesTheology(creed);
			});
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
			KingdomGovernanceScope.Commit("consecrate shrine");
			target.SetIntProperty(ShrineLapsedAnnouncedProperty, 0);
			KingdomChronicle.Record(System, KingdomFaithRules.ConsecrationChronicle(target.ShortDisplayName, KingdomPresentation.Rich(System.SeatName), creedDisplay, reconsecration));
			Popup.Show(KingdomFaithRules.ConsecrationNotice(target.ShortDisplayName, creedDisplay, reconsecration, neverStaffable));
			KingdomLog.Log("faith: consecrated " + target.ShortDisplayName + " to " + chosenCreed + " reconsecration=" + reconsecration);
		}

		private static List<GameObject> FaithBuildingsIn(Zone Z)
		{
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			return KingdomCapabilityRuntime.Roots(Z, survey,
				KingdomBenefitCapabilities.Shrine, "consecration");
		}
	}
}
