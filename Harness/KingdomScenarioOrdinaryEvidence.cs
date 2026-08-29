using System;
using System.Collections.Generic;

using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Positive ordinary-commission evidence for anchor capture.
	/// <para>
	/// Split from the report only to hold the house line cap. A blocklist answers "is this the one
	/// path I thought of"; this answers "is this a path an anchor is allowed to come from", which is
	/// what the governing ruling actually requires.
	/// </para>
	/// </summary>
	internal static partial class KingdomScenarioCaptureReport
	{
		/// <summary>
		/// POSITIVE ordinary-commission evidence, not merely the absence of gallery keys.
		/// <para>
		/// A blocklist answers "is this the one path I thought of"; an allowlist answers "is this the
		/// path an anchor is allowed to come from". Every stamper that leaves no gallery property
		/// passed the blocklist, so an anchor could be founded from a building nobody commissioned.
		/// </para>
		/// <para>
		/// What is provable from the production object: no gallery authority anywhere on the owner or
		/// its lot components; a REAL staked plot whose rect is the owner's rect (the review gallery
		/// invents a `taf-gallery-&lt;guid&gt;` lot and stakes no plot root, so it cannot pass); a
		/// construction receipt, which ordinary commission leaves and staging does not; and no
		/// upgrade authority, because an upgraded building is not the commission that founded it.
		/// </para>
		/// </summary>
		private static bool TryProveOrdinaryCommission(Zone Zone, GameObject Owner,
			out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (!KingdomArchitectureStamper.TryReadOwner(Owner, out intent, out snapshot, out lot,
				out Failure)) return false;
			if (KingdomScenarioGallerySlice.CarriesGalleryAuthority(Owner))
				return Refuse("this building carries debug-gallery authority; a gallery-staged "
					+ "owner was not reached by ordinary play and may never found an ordinary "
					+ "anchor", out Failure);
			List<GameObject> objects = Zone.GetObjects() ?? new List<GameObject>();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item)) continue;
				if (!string.Equals(item.GetStringProperty(KingdomPlots.PlotIdProperty), lot,
					StringComparison.Ordinal)) continue;
				// RED 19 said "an owner OR component"; a component keeps its receipt even where an
				// owner might not, so the whole lot is swept rather than just its root.
				if (KingdomScenarioGallerySlice.CarriesGalleryAuthority(item))
					return Refuse("a component of this building carries debug-gallery authority; "
						+ "it may never found an ordinary anchor", out Failure);
			}
			if (Owner.HasIntProperty(KingdomArchitectureStamper.UpgradeSchemaProperty)
				|| Owner.HasStringProperty(KingdomArchitectureStamper.UpgradeSchemaProperty))
				return Refuse("this building carries upgrade authority; the commission that founded "
					+ "it is not the state standing here", out Failure);
			if (!Owner.HasStringProperty(KingdomConstruction.ReceiptProperty))
				return Refuse("this building carries no construction receipt, so ordinary "
					+ "commission is not proved - only that nothing refuted it", out Failure);
			if (!TryProveStakedPlot(Zone, intent, lot, out Failure)) return false;
			return true;
		}

		/// <summary>
		/// The lot must be a plot somebody actually staked, at the owner's own rect.
		/// <para>
		/// This is the clause the review gallery cannot satisfy: it mints a lot id per staging and
		/// stakes no plot, so there is no root carrying that id and no rect to agree with.
		/// </para>
		/// </summary>
		private static bool TryProveStakedPlot(Zone Zone, KingdomArchitectureIntent Intent,
			string Lot, out string Failure)
		{
			Failure = null;
			List<GameObject> objects = Zone.GetObjects() ?? new List<GameObject>();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item)
					|| !string.Equals(item.GetStringProperty(KingdomPlots.PlotIdProperty), Lot,
						StringComparison.Ordinal)
					|| !item.HasIntProperty(KingdomPlots.PlotX1Property)
					|| !item.HasIntProperty(KingdomPlots.PlotY1Property)
					|| !item.HasIntProperty(KingdomPlots.PlotX2Property)
					|| !item.HasIntProperty(KingdomPlots.PlotY2Property)) continue;
				if (item.GetIntProperty(KingdomPlots.PlotX1Property) == Intent.Rect.X1
					&& item.GetIntProperty(KingdomPlots.PlotY1Property) == Intent.Rect.Y1
					&& item.GetIntProperty(KingdomPlots.PlotX2Property) == Intent.Rect.X2
					&& item.GetIntProperty(KingdomPlots.PlotY2Property) == Intent.Rect.Y2)
					return true;
				return Refuse("this building's lot is staked at a different rect than the building "
					+ "occupies", out Failure);
			}
			return Refuse("no staked plot in this zone carries this building's lot id; a lot minted "
				+ "for a staging is not a commission", out Failure);
		}
	}
}
