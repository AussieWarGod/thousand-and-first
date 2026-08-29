using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomLodging
	{
		private static List<GameObject> ResidentsIn(Zone Z)
		{
			List<GameObject> list = new List<GameObject>();
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (KingdomCitizenship.BelongsTo(system, item))
				{
					list.Add(item);
				}
			}
			return list;
		}

		/// <summary>
		/// Whether this standing home has been worn past the point of being a roof
		/// (<see cref="KingdomLodgingRules.CondemnedWearPercent"/>). A home with no wear part
		/// has never been damaged and is sound.
		/// <para>
		/// The building is not touched, moved or unbuilt &mdash; the protection law forbids it
		/// and there is nothing to forbid here anyway. It simply stops being counted as somewhere
		/// to live until somebody mends it, which is a thing the founder can do with materials
		/// and hands on any pass.
		/// </para>
		/// </summary>
		public static bool IsCondemned(GameObject Home)
		{
			if (!GameObject.Validate(Home))
			{
				return false;
			}
			r_KingdomWear wear = Home.GetPart<r_KingdomWear>();
			return wear != null && KingdomLodgingRules.IsCondemned(wear.Wear);
		}

		/// <summary>
		/// The residents this home currently holds, by their stored assignment. For the caller
		/// that has just condemned a roof and owes the people under it a dated record of losing
		/// it &mdash; <c>KingdomSubsidence</c>'s ruin is the one that does.
		/// </summary>
		/// <param name="Z">The zone. Null holds nobody.</param>
		/// <param name="Home">The home. One with no plot id holds nobody, because an assignment
		/// is stored as a plot id and nothing else.</param>
		public static List<GameObject> ResidentsOf(Zone Z, GameObject Home)
		{
			List<GameObject> list = new List<GameObject>();
			string plotId = GameObject.Validate(Home) ? Home.GetStringProperty(KingdomPlots.PlotIdProperty) : null;
			if (Z == null || string.IsNullOrEmpty(plotId))
			{
				return list;
			}
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (KingdomCitizenship.BelongsTo(system, item)
					&& item.GetStringProperty(HomePlotIdProperty) == plotId)
				{
					list.Add(item);
				}
			}
			return list;
		}

		/// <summary>
		/// Records the roof brink for everyone living under a home that has just been condemned,
		/// at the tick it actually happened rather than the pass that notices.
		/// <para>
		/// This is the honest-elapsed half of the brink, and the reason it is worth the call:
		/// <see cref="RunRoofBrink"/> records at the pass that finds the loss, which is
		/// right when the loss happened at that pass. A subsidence ruins a home at a breakpoint
		/// days or seasons back, and the settler has been sleeping in the open ever since. Record
		/// is idempotent, so the earliest honest tick is the one that stands and a second caller
		/// cannot redate it; nothing is warned and no window starts here, because the window is
		/// anchored at the founder's WARNING and this call has nobody to warn.
		/// </para>
		/// <para>
		/// Recorded only for an OCCUPIED home that actually crossed the line. A ruined shed
		/// nobody sleeps in, and a home worn but still livable, both record nothing.
		/// </para>
		/// </summary>
		/// <param name="Z">The zone the home stands in.</param>
		/// <param name="Home">The home that has just crossed into condemnation.</param>
		/// <param name="AtTick">The tick it crossed &mdash; the ruining breakpoint's own tick.</param>
		/// <returns>How many residents this recorded for.</returns>
		public static int RecordCondemnedRoofBrink(Zone Z, GameObject Home, long AtTick)
		{
			if (!Enabled || !IsCondemned(Home))
			{
				return 0;
			}
			List<GameObject> residents = ResidentsOf(Z, Home);
			int recorded = 0;
			for (int i = 0; i < residents.Count; i++)
			{
				// Unnamed residents never enter the brink, exactly as RunRoofBrink has it:
				// the brink names its subject, and staying is the safe answer to a question the
				// registers cannot record.
				if (string.IsNullOrEmpty(RollNameOf(residents[i])))
				{
					continue;
				}
				if (KingdomBrink.Record(residents[i], BrinkKind.Roof, AtTick, null, 0))
				{
					recorded++;
				}
			}
			return recorded;
		}

	}
}
