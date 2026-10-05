using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		/// <returns>False only when the yielding part could not be attached; the caller's existing
		/// quarantine branch reports it.</returns>
		public static bool CarryMarks(GameObject Predecessor, GameObject Successor, string SuccessorKey)
		{
			if (Predecessor == null || Successor == null)
			{
				return false;
			}
			Successor.SetIntProperty(BuiltProperty, 1);
			if (!string.IsNullOrEmpty(SuccessorKey))
			{
				Successor.SetStringProperty(BuildKeyProperty, SuccessorKey);
			}
			// The scalar marks are written exactly as the shared rule returns them, so this writer
			// and ExactCarriedMarks read one table (Growth/KingdomUpgradeRules.FounderMarks.cs).
			KingdomUpgradeRules.FounderMarks carried = KingdomUpgradeRules.CarryFounderMarks(
				ReadFounderMarks(Predecessor), Successor.Inventory != null,
				Successor.GetPart<LiquidVolume>() != null);
			if (carried.Larder)
			{
				Successor.SetIntProperty(KingdomAdopt.LarderProperty, 1);
			}
			if (carried.Stores)
			{
				Successor.SetIntProperty(KingdomAdopt.StoresProperty, 1);
			}
			if (carried.Certified)
			{
				Successor.SetIntProperty(KingdomSalvage.CertifiedProperty, 1);
			}
			KingdomWear.TryCarryStableState(Predecessor, Successor);
			// A name the founder gave is the most personal decision anything in this mod records.
			// Losing one because the thing it was given to got better would be the same bug as
			// losing a dedication, so it is carried the same way and for the same reason.
			if (!string.IsNullOrEmpty(carried.GivenName))
			{
				Successor.SetStringProperty(KingdomDesign.GivenNameProperty, carried.GivenName);
			}
			// An adopted work is never improved (UpgradeVerdict.NotOurWork), so this is
			// unreachable today. It is carried anyway because the cost of being wrong is a
			// founder's own building quietly losing the settlement's recognition of it.
			if (carried.Adopted)
			{
				Successor.SetIntProperty(AdoptedProperty, 1);
				string adoptedKey = Predecessor.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
				if (!string.IsNullOrEmpty(adoptedKey))
				{
					Successor.SetStringProperty(KingdomAdopt.AdoptedKeyProperty, adoptedKey);
				}
				string adoptedMark = Predecessor.GetStringProperty(KingdomAdopt.AdoptedMarkProperty);
				if (!string.IsNullOrEmpty(adoptedMark))
				{
					Successor.SetStringProperty(KingdomAdopt.AdoptedMarkProperty, adoptedMark);
				}
			}
			// #283: a work staked inside the heart's survey promised to yield to the heart. The
			// legacy growth lane carries that promise (Growth/KingdomPlot2.22.Growth.cs); the
			// authored lane did not, so ExactCarriedMarks refused every paid renovation of such a
			// work after its layout had already been rebuilt. Same shape as the legacy lane.
			if (carried.Yielding)
			{
				Successor.SetIntProperty(KingdomPlots.YieldingProperty, 1);
				try { Successor.RequirePart<r_KingdomYielding>(); }
				catch (System.Exception) { return false; }
			}
			return true;
		}

		/// <summary>The scalar founder marks one work carries, read for the shared rule.</summary>
		private static KingdomUpgradeRules.FounderMarks ReadFounderMarks(GameObject Work)
		{
			return new KingdomUpgradeRules.FounderMarks(
				Work.GetIntProperty(KingdomAdopt.LarderProperty) == 1,
				Work.GetIntProperty(KingdomAdopt.StoresProperty) == 1,
				Work.GetIntProperty(KingdomSalvage.CertifiedProperty) == 1,
				Work.GetIntProperty(AdoptedProperty) == 1,
				Work.GetIntProperty(KingdomPlots.YieldingProperty) == 1,
				Work.GetStringProperty(KingdomDesign.GivenNameProperty));
		}

		/// <summary>
		/// The Charter's improvements screen: everything on this ground that can grow, what it
		/// grows into, and &mdash; for anything that is not growing &mdash; why not, in one
		/// sentence each. Picking a work holds it or releases it; the last entry does the same
		/// for the whole ground.
		/// <para>
		/// Nothing here starts, cancels, or hurries a work. The founder's only decision in this
		/// screen is what to leave alone, which is the one decision the settlement cannot make
		/// for them.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
	}
}
