using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		/// <summary>
		/// Stakes the single quickstart shelter lot on the founding pass, once, and proves it.
		/// <para>
		/// Nothing here finishes a building. The lot is staked receiptless, which keeps it on the
		/// shipped schema-zero calendar, so the tent rises on the settlement's own attended passes
		/// over the first days rather than the moment the founder arrives.
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
			GameObject standing;
			if (!TryFindShelter(Zone, out standing, out Failure)) return false;
			if (standing != null)
				return MarkShelter(standing, out Failure) && VerifyShelter(Zone, standing, out Failure);

			KingdomRules.BuildEntry entry;
			KingdomPlotRules.PlotSpec spec;
			if (!KingdomData.TryGetBuilding(KingdomQuickstartRules.ShelterBuildKey, out entry)
				|| entry == null || !KingdomPlots.TryGetSpec(entry.Key, out spec) || spec == null)
			{
				Failure = "The quickstart shelter design is absent from the merged building catalogue.";
				return false;
			}
			KingdomPlotRules.PlotRect lot = ShelterRect();
			int width;
			int height;
			if (!KingdomPlotRules.TryDimensions(spec.Size, out width, out height)
				|| lot.Width != width || lot.Height != height)
			{
				Failure = "The reserved quickstart shelter lot no longer matches the catalogue's plot tier.";
				return false;
			}
			// Zoning and the authored-ground preflight are the two judgements the direct stake still
			// makes, and either may refuse. A refusal stops the bootstrap with its reason rather than
			// leaving a half-claimed lot or a promise the settlement never took up.
			GameObject works = KingdomPlots.Stake(System, Zone, lot, entry, spec,
				new KingdomPlots.GroundGrid(Zone), ShelterSkinKey(System, entry),
				KingdomPlotRules.IsUnderground(Zone.Z));
			if (works == null)
			{
				Failure = "The settlement refused the quickstart shelter lot at "
					+ ShelterCoordinates() + "; no ground was staked.";
				return false;
			}
			return MarkShelter(works, out Failure) && VerifyShelter(Zone, works, out Failure);
		}

		/// <summary>
		/// The one shelter already standing on this ground, or null. The stamped rectangle is read
		/// as well as the marker, because the marker is written after the stake: a cut between the
		/// two must find the lot, not stake a second one.
		/// <para>
		/// An unmarked object is adopted only when it also carries our own design key, so a foreign
		/// plot that happens to be stamped on this rectangle is never marked before it is refused.
		/// The bootstrap proves custody before it writes, as every other grant path here does.
		/// </para>
		/// </summary>
		private static bool TryFindShelter(Zone Zone, out GameObject Shelter, out string Failure)
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
				if (!item.HasStringProperty(KingdomQuickstartRules.ShelterMarkerProperty)
					&& !(KingdomPlots.TryReadStampedRect(item, out rect) && SameShelterRect(rect)
						&& string.Equals(item.GetStringProperty(KingdomUpgrade.BuildKeyProperty, ""),
							KingdomQuickstartRules.ShelterBuildKey, StringComparison.Ordinal)))
					continue;
				matches++;
				Shelter = item;
			}
			if (matches > 1)
			{
				Shelter = null;
				Failure = "The reserved quickstart shelter lot carried more than one claim.";
				return false;
			}
			return true;
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
		/// reservation are proved here, and whether the tent has RISEN remains the settlement
		/// calendar's answer.
		/// </summary>
		private static bool VerifyShelter(Zone Zone, GameObject Shelter, out string Failure)
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
			if (!KingdomPlots.TryReadStampedRect(Shelter, out rect) || !SameShelterRect(rect))
			{
				Failure = "The quickstart shelter claim did not stand on its reserved rectangle at "
					+ ShelterCoordinates() + ".";
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

		private static KingdomPlotRules.PlotRect ShelterRect()
		{
			return new KingdomPlotRules.PlotRect(
				KingdomQuickstartRules.ShelterX1, KingdomQuickstartRules.ShelterY1,
				KingdomQuickstartRules.ShelterX2, KingdomQuickstartRules.ShelterY2);
		}

		private static bool SameShelterRect(KingdomPlotRules.PlotRect Rect)
		{
			return Rect.X1 == KingdomQuickstartRules.ShelterX1
				&& Rect.Y1 == KingdomQuickstartRules.ShelterY1
				&& Rect.X2 == KingdomQuickstartRules.ShelterX2
				&& Rect.Y2 == KingdomQuickstartRules.ShelterY2;
		}

		private static string ShelterCoordinates()
		{
			return "(" + KingdomQuickstartRules.ShelterX1 + ","
				+ KingdomQuickstartRules.ShelterY1 + ")-("
				+ KingdomQuickstartRules.ShelterX2 + ","
				+ KingdomQuickstartRules.ShelterY2 + ")";
		}

	}
}
