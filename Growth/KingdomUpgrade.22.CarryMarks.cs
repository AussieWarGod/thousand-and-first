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
		public static void CarryMarks(GameObject Predecessor, GameObject Successor, string SuccessorKey)
		{
			if (Predecessor == null || Successor == null)
			{
				return;
			}
			Successor.SetIntProperty(BuiltProperty, 1);
			if (!string.IsNullOrEmpty(SuccessorKey))
			{
				Successor.SetStringProperty(BuildKeyProperty, SuccessorKey);
			}
			if (Predecessor.GetIntProperty(KingdomAdopt.LarderProperty) == 1 && Successor.Inventory != null)
			{
				Successor.SetIntProperty(KingdomAdopt.LarderProperty, 1);
			}
			if (Predecessor.GetIntProperty(KingdomAdopt.StoresProperty) == 1 && Successor.GetPart<LiquidVolume>() != null)
			{
				Successor.SetIntProperty(KingdomAdopt.StoresProperty, 1);
			}
			if (Predecessor.GetIntProperty(KingdomSalvage.CertifiedProperty) == 1)
			{
				Successor.SetIntProperty(KingdomSalvage.CertifiedProperty, 1);
			}
			KingdomWear.TryCarryStableState(Predecessor, Successor);
			// A name the founder gave is the most personal decision anything in this mod records.
			// Losing one because the thing it was given to got better would be the same bug as
			// losing a dedication, so it is carried the same way and for the same reason.
			string given = Predecessor.GetStringProperty(KingdomDesign.GivenNameProperty);
			if (!string.IsNullOrEmpty(given))
			{
				Successor.SetStringProperty(KingdomDesign.GivenNameProperty, given);
			}
			// An adopted work is never improved (UpgradeVerdict.NotOurWork), so this is
			// unreachable today. It is carried anyway because the cost of being wrong is a
			// founder's own building quietly losing the settlement's recognition of it.
			if (Predecessor.GetIntProperty(AdoptedProperty) == 1)
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
