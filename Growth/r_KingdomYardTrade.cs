using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>
	/// Attached to a house the moment it takes up a yard trade. Carries no state of its own
	/// &mdash; the trade lives on <see cref="KingdomYards.YardKeyProperty"/>, which survives a
	/// reload on its own, so this part only ever reads it back to say so on the object itself
	/// (see <c>r_KingdomImprovement</c>'s identical idiom in Growth/KingdomUpgrade.cs). Attaching
	/// it a second time after a reload is idempotent: <c>RequirePart</c> never duplicates it.
	/// </summary>
	[Serializable]
	public class r_KingdomYardTrade : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (base.WantEvent(ID, cascade))
			{
				return true;
			}
			return ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			string key = ParentObject?.GetStringProperty(KingdomYards.YardKeyProperty);
			if (!string.IsNullOrEmpty(key) && KingdomYards.TryGetSpec(key, out var work))
			{
				E.Postfix.Append("\n").Append(KingdomYardRules.DescriptionLine(work));
			}
			return base.HandleEvent(E);
		}
	}
}
