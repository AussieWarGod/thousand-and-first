using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomWaterRite
	{
		// ==================================================================================
		// The attended pass: shared living, and nothing else
		// ==================================================================================

		/// <summary>
		/// The kingdom's one attended pass over shared living: counts one pass of it for every
		/// citizen standing here.
		/// <para>
		/// Preconditions: called from the settlement pass, on claimed ground, beside
		/// <c>KingdomLodging.OnSettlementPass</c>. Side effects: advances
		/// <see cref="SharedDaysProperty"/> by the whole days each citizen has lived here since
		/// they were last counted (<c>KingdomWaterRiteRules.SharedDaysAfter</c>), and registers
		/// this channel's pressure source if a rebuild dropped it. Failure mode: returns having
		/// done nothing.
		/// </para>
		/// <para>
		/// Days pass here whether or not the founder does (Addendum 8 clause 1): a settler goes on
		/// living in the settlement while nobody is watching, and pretending otherwise made a
		/// founder who came home every third day the only founder whose people ever settled in.
		/// Nothing irreversible rides on it &mdash; shared living buys REACH, and reach only makes
		/// an invitation the founder must still extend and the settler must still accept more
		/// likely to be accepted &mdash; so this counter carries no brink of its own.
		/// </para>
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Z == null || System.ClaimedZones == null
				|| Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			// Two jobs, two gates, ONE enumeration of the ground. Lane 1 of Addendum 13 (the water
			// ritual with citizens) stands on its own option -- whether the founder's settlers will
			// share water with them on Qud's terms has nothing to do with whether this mod's
			// inward rite of belief is switched on -- but it walks the same citizens under the same
			// filter this counter already walks, and a second Z.GetObjects() a pass for a step that
			// is a no-op after the first pass is a cost with nothing behind it.
			bool shared = Enabled;
			KingdomCitizenRite.RiteTally rite = KingdomCitizenRite.Begin(System, Z);
			if (!shared && rite == null)
			{
				return;
			}
			if (shared)
			{
				Register();
			}
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			for (int i = 0; i < Survey.CitizenBodies.Count; i++)
			{
				GameObject item = Survey.CitizenBodies[i];
				if (shared)
				{
					// Counted BEFORE the rite observes them, so a settler who crossed into a
					// different greeting this pass is greeted with the one they have earned.
					AdvanceSharedDays(item, now);
				}
				KingdomCitizenRite.Observe(rite, System, item);
			}
			KingdomCitizenRite.Close(System, rite);
		}

		/// <summary>One settler's share of this pass's shared living. Side effects: advances
		/// <see cref="SharedDayTickProperty"/> and <see cref="SharedDaysProperty"/> by the whole
		/// days since they were last counted. Failure mode: returns having done nothing.</summary>
		private static void AdvanceSharedDays(GameObject citizen, long now)
		{
			long last = citizen.GetLongProperty(SharedDayTickProperty);
			if (last <= 0L || now <= 0L)
			{
				// Planted before the first count, never read as elapsed: an unplanted stamp
				// resolved against an uncapped clock is the age of the world, and a newcomer
				// would arrive having already lived here a lifetime.
				citizen.SetLongProperty(SharedDayTickProperty, now);
				return;
			}
			int days = KingdomRules.ElapsedDays(now - last);
			if (days <= 0)
			{
				return;
			}
			// Advanced by exactly the days credited, so the part-day counts toward the next one
			// and a founder who steps out of the zone and back in buys nobody a free day.
			citizen.SetLongProperty(SharedDayTickProperty, KingdomRules.AdvanceCheckpoint(last, now));
			citizen.SetIntProperty(SharedDaysProperty, KingdomWaterRiteRules.SharedDaysAfter(citizen.GetIntProperty(SharedDaysProperty), days));
		}

		/// <summary>Cohabited days this settler has lived here. Zero for anybody the pass has not
		/// reached yet, which is the ordinary state of a newcomer.</summary>
		public static int SharedDaysOf(GameObject Resident)
		{
			return (Resident == null) ? 0 : Resident.GetIntProperty(SharedDaysProperty);
		}

		/// <summary>The line <c>kingdom:dump</c> appends for the zone the founder is standing in:
		/// how much of this settlement's life the people standing here have lived, and who has been
		/// asked once too often.</summary>
		public static string DumpLine(KingdomSystem System, Zone Z)
		{
			if (!Enabled || Z == null)
			{
				return "";
			}
			int here = 0;
			int total = 0;
			List<string> closed = new List<string>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (!KingdomCitizenship.BelongsTo(System, item))
				{
					continue;
				}
				here++;
				total += SharedDaysOf(item);
				string creed = item.GetStringProperty(AskedTooOftenCreedProperty);
				if (!string.IsNullOrEmpty(creed))
				{
					closed.Add(KingdomPresentation.Rich(NameOf(item)) + " (" + creed + ")");
				}
			}
			if (here == 0)
			{
				return "";
			}
			string line = "\nShared living: " + total + " passes over " + here + " here (cap "
				+ KingdomWaterRiteRules.MaxCountedDays + " each)";
			if (closed.Count > 0)
			{
				line += "  asked too often: " + string.Join(", ", closed);
			}
			return line;
		}

	}
}
