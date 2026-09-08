using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		/// <summary>
		/// Stakes the quickstart's two shelter lots on the founding pass, once each, and proves
		/// them.
		/// <para>
		/// Nothing here finishes a building. The lots are staked receiptless, which keeps them on
		/// the shipped schema-zero calendar, so the rows rise on the settlement's own attended
		/// passes over the first days rather than the moment the founder arrives.
		/// </para>
		/// <para>
		/// The stake runs inside <c>RunCore</c> only, after the founding is measured and before the
		/// receipt advances, and therefore outside every native creator fault probe: those probes
		/// call the grant creators directly and count the zone's objects around them.
		/// </para>
		/// </summary>
		private static bool TryStakeShelter(KingdomSystem System, Zone Zone, out string Failure)
		{
			Failure = "";
			if (System == null || Zone == null)
			{
				Failure = "The quickstart shelter needed a founded realm standing on its own reserved ground.";
				return false;
			}
			KingdomRules.BuildEntry entry;
			KingdomPlotRules.PlotSpec spec;
			if (!KingdomData.TryGetBuilding(KingdomQuickstartRules.ShelterBuildKey, out entry)
				|| entry == null || !KingdomPlots.TryGetSpec(entry.Key, out spec) || spec == null)
			{
				Failure = "The quickstart shelter design is absent from the merged building catalogue.";
				return false;
			}
			int width;
			int height;
			if (!KingdomPlotRules.TryDimensions(spec.Size, out width, out height))
			{
				Failure = "The quickstart shelter design declared no plot tier of its own.";
				return false;
			}
			for (int i = 0; i < KingdomQuickstartRules.ShelterLotCount; i++)
				if (!TryStakeShelterLot(System, Zone, KingdomQuickstartRules.ShelterLot(i),
					entry, spec, width, height, out Failure))
					return false;
			return true;
		}

		/// <summary>
		/// One lot: adopt whatever already stands on it, or stake it. Read before write, per lot,
		/// so a cut after the first row is staked resumes by staking only the second.
		/// </summary>
		private static bool TryStakeShelterLot(KingdomSystem System, Zone Zone,
			KingdomPlotRules.PlotRect Lot, KingdomRules.BuildEntry Entry,
			KingdomPlotRules.PlotSpec Spec, int Width, int Height, out string Failure)
		{
			Failure = "";
			if (Lot.Width != Width || Lot.Height != Height)
			{
				Failure = "The reserved quickstart shelter lot at " + ShelterCoordinates(Lot)
					+ " no longer matches the catalogue's plot tier.";
				return false;
			}
			GameObject standing;
			if (!TryFindShelter(Zone, Lot, out standing, out Failure)) return false;
			if (standing != null)
				return MarkShelter(standing, out Failure)
					&& VerifyShelter(Zone, Lot, standing, out Failure);

			// Zoning and the authored-ground preflight are the two judgements the direct stake still
			// makes, and either may refuse. A refusal stops the bootstrap with its reason rather than
			// leaving a half-claimed lot or a promise the settlement never took up.
			GameObject works = KingdomPlots.Stake(System, Zone, Lot, Entry, Spec,
				new KingdomPlots.GroundGrid(Zone), ShelterSkinKey(System, Entry),
				KingdomPlotRules.IsUnderground(Zone.Z));
			if (works == null)
			{
				Failure = "The settlement refused the quickstart shelter lot at "
					+ ShelterCoordinates(Lot) + "; no ground was staked.";
				return false;
			}
			return MarkShelter(works, out Failure) && VerifyShelter(Zone, Lot, works, out Failure);
		}

		/// <summary>
		/// The one claim already standing on this lot, or null. The stamped rectangle attributes an
		/// object to its lot, because the marker is written after the stake: a cut between the two
		/// must find that row, not stake a second one on top of it.
		/// <para>
		/// An unmarked object is adopted only when it also carries our own design key, so a foreign
		/// plot that happens to be stamped on this rectangle is never marked before it is refused.
		/// The bootstrap proves custody before it writes, as every other grant path here does.
		/// </para>
		/// </summary>
		private static bool TryFindShelter(Zone Zone, KingdomPlotRules.PlotRect Lot,
			out GameObject Shelter, out string Failure)
		{
			Shelter = null;
			Failure = "";
			List<GameObject> objects = Zone.GetObjects();
			int matches = 0;
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item)) continue;
				KingdomPlotRules.PlotRect rect;
				if (!KingdomPlots.TryReadStampedRect(item, out rect)
					|| !SameShelterRect(rect, Lot)) continue;
				if (!item.HasStringProperty(KingdomQuickstartRules.ShelterMarkerProperty)
					&& !string.Equals(item.GetStringProperty(KingdomUpgrade.BuildKeyProperty, ""),
						KingdomQuickstartRules.ShelterBuildKey, StringComparison.Ordinal))
					continue;
				matches++;
				Shelter = item;
			}
			if (matches > 1)
			{
				Shelter = null;
				Failure = "The reserved quickstart shelter lot at " + ShelterCoordinates(Lot)
					+ " carried more than one claim.";
				return false;
			}
			return true;
		}

		/// <summary>
		/// How many reserved lots carry a claim right now, read off the ground rather than off the
		/// branch that ran. A save cut past the Reserved phase resumes straight through to Complete
		/// without staking anything, so the completion notice may only name what actually stands.
		/// </summary>
		private static int ShelterLotsClaimed(Zone Zone)
		{
			if (Zone == null) return 0;
			int claimed = 0;
			for (int i = 0; i < KingdomQuickstartRules.ShelterLotCount; i++)
			{
				GameObject standing;
				string ignored;
				if (TryFindShelter(Zone, KingdomQuickstartRules.ShelterLot(i), out standing,
					out ignored) && standing != null)
					claimed++;
			}
			return claimed;
		}

		/// <summary>
		/// Writes the shelter's own reservation from the plot identity the staking machinery
		/// published, completing an interrupted stamp rather than claiming fresh ground.
		/// </summary>
		private static bool MarkShelter(GameObject Shelter, out string Failure)
		{
			Failure = "";
			string plotId = Shelter.GetStringProperty(KingdomPlots.PlotIdProperty, "");
			if (string.IsNullOrEmpty(plotId))
			{
				Failure = "The quickstart shelter lot published no plot identity to reserve.";
				return false;
			}
			string held = Shelter.GetStringProperty(
				KingdomQuickstartRules.ShelterMarkerProperty, "");
			if (string.IsNullOrEmpty(held))
			{
				Shelter.SetStringProperty(KingdomQuickstartRules.ShelterMarkerProperty, plotId);
				held = Shelter.GetStringProperty(
					KingdomQuickstartRules.ShelterMarkerProperty, "");
			}
			if (!string.Equals(held, plotId, StringComparison.Ordinal))
			{
				Failure = "The quickstart shelter reservation did not match its own plot identity.";
				return false;
			}
			return true;
		}

		/// <summary>
		/// Measures the claim without inventing its completion: ground, design, rectangle and
		/// reservation are proved here, and whether the row has RISEN remains the settlement
		/// calendar's answer.
		/// </summary>
		private static bool VerifyShelter(Zone Zone, KingdomPlotRules.PlotRect Lot,
			GameObject Shelter, out string Failure)
		{
			Failure = "";
			if (!GameObject.Validate(Shelter) || Shelter.CurrentZone != Zone)
			{
				Failure = "The quickstart shelter claim left its own reserved ground.";
				return false;
			}
			if (!string.Equals(Shelter.GetStringProperty(KingdomUpgrade.BuildKeyProperty, ""),
				KingdomQuickstartRules.ShelterBuildKey, StringComparison.Ordinal))
			{
				Failure = "The reserved quickstart shelter lot held a different design.";
				return false;
			}
			KingdomPlotRules.PlotRect rect;
			if (!KingdomPlots.TryReadStampedRect(Shelter, out rect) || !SameShelterRect(rect, Lot))
			{
				Failure = "The quickstart shelter claim did not stand on its reserved rectangle at "
					+ ShelterCoordinates(Lot) + ".";
				return false;
			}
			if (string.IsNullOrEmpty(Shelter.GetStringProperty(
				KingdomQuickstartRules.ShelterMarkerProperty, "")))
			{
				Failure = "The quickstart shelter claim carried no reservation of its own.";
				return false;
			}
			return true;
		}

		/// <summary>The city's own style, silently: the founding pass asks the founder nothing.</summary>
		private static string ShelterSkinKey(KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			KingdomDesignRules.SkinEntry skin = KingdomDesignRules.ResolveDefaultSkinForKeys(
				Entry.Skins, KingdomData.StyleKeys(System.Style));
			return skin == null ? null : skin.Key;
		}

		private static bool SameShelterRect(KingdomPlotRules.PlotRect Rect,
			KingdomPlotRules.PlotRect Lot)
		{
			return Rect.X1 == Lot.X1 && Rect.Y1 == Lot.Y1
				&& Rect.X2 == Lot.X2 && Rect.Y2 == Lot.Y2;
		}

		private static string ShelterCoordinates(KingdomPlotRules.PlotRect Lot)
		{
			return "(" + Lot.X1 + "," + Lot.Y1 + ")-(" + Lot.X2 + "," + Lot.Y2 + ")";
		}

	}
}
