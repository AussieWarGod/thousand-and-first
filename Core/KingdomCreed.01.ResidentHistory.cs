using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomCreed
	{

		/// <summary>
		/// Records an arriving settler's creed on the settler and in the seated city's tally.
		/// <para>
		/// Side effects: writes <see cref="CreedProperty"/> on <paramref name="Settler"/> and
		/// increments the city's count. Safe to call with an empty creed, which is the common case
		/// and does nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Settler">The arriving settler. Null is tolerated; only the tally moves.</param>
		/// <param name="Creed">A faction name from <see cref="Draw"/>, or empty.</param>
		public static void Record(KingdomSystem System, GameObject Settler, string Creed)
		{
			if (System == null || string.IsNullOrEmpty(Creed))
			{
				return;
			}
			Settler?.SetStringProperty(CreedProperty, Creed);
			System.CreedCounts.TryGetValue(Creed, out var count);
			System.CreedCounts[Creed] = count + 1;
			KingdomLog.Log("creed: " + (Settler?.ShortDisplayName ?? "settler") + " holds with " + Creed + " (" + (count + 1) + " here)");
		}

		/// <summary>
		/// Takes a settler who has left or died out of the seated city's creed tally. Reads the
		/// creed off the settler, so a settler who carries none costs nothing. Never drives a
		/// count below zero.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Leaver">The departing settler. Null does nothing.</param>
		public static void Forget(KingdomSystem System, GameObject Leaver)
		{
			if (System == null || Leaver == null)
			{
				return;
			}
			// The whole person comes out of both tallies: what they hold, and what they held before
			// that. A history is a fact about somebody, so it leaves with them exactly as their
			// present creed does -- the alternative is a city that goes on being able to raise a
			// creed-work because of a believer who died a year ago.
			//
			// KingdomConversion.Convert is the one caller for whom the person has NOT left, and it
			// puts both halves back on the far side: Record for the present, RememberPast for the
			// history it has just added to.
			DropPast(System, Leaver);
			string creed = Leaver.GetStringProperty(CreedProperty);
			if (string.IsNullOrEmpty(creed))
			{
				return;
			}
			System.CreedCounts.TryGetValue(creed, out var count);
			if (count > 1)
			{
				System.CreedCounts[creed] = count - 1;
			}
			else
			{
				System.CreedCounts.Remove(creed);
			}
		}

		/// <summary>
		/// The creeds one settler has held and left, oldest first. Never null.
		/// </summary>
		/// <param name="Settler">A settler. Null reads as a history of nothing.</param>
		public static List<string> PastOf(GameObject Settler)
		{
			return KingdomCreedRules.DecodeKept(Settler?.GetStringProperty(CreedPastProperty));
		}

		/// <summary>
		/// Whether this settler ALIGNS with a creed: they hold it now, or they have held it and
		/// left it. Addendum 16 clause (4), asked of one person.
		/// </summary>
		public static bool Aligns(GameObject Settler, string Creed)
		{
			if (Settler == null || string.IsNullOrEmpty(Creed))
			{
				return false;
			}
			if (string.Equals(Settler.GetStringProperty(CreedProperty), Creed, System.StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return KingdomCreedRules.KeptHolds(Settler.GetStringProperty(CreedPastProperty), Creed);
		}

		/// <summary>
		/// Writes a creed into a settler's history and puts that whole history into the city's
		/// tally.
		/// <para>
		/// Called from <c>KingdomConversion.Convert</c> and nowhere else: the one conversion path
		/// is the one place a creed is ever LEFT, and a creed nobody left is not history, it is
		/// what they still believe. Side effects: the settler's
		/// <see cref="CreedPastProperty"/> may grow by one name, and every name in it is counted
		/// into <c>KingdomSystem.CreedPastCounts</c> &mdash; the whole history rather than the one
		/// name, because <see cref="Forget"/> ran a line earlier and took the rest of it out.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Settler">The settler who has just left a creed.</param>
		/// <param name="Creed">The creed they left. Empty is an ordinary settler taking their
		/// first creed, which leaves nothing behind and records nothing.</param>
		/// <returns>True when the settler's own record actually grew.</returns>
		public static bool RememberPast(KingdomSystem System, GameObject Settler, string Creed)
		{
			if (System == null || Settler == null)
			{
				return false;
			}
			bool added;
			string kept = KingdomCreedRules.RememberKept(Settler.GetStringProperty(CreedPastProperty), Creed, out added);
			Settler.SetStringProperty(CreedPastProperty, string.IsNullOrEmpty(kept) ? null : kept);
			List<string> names = KingdomCreedRules.DecodeKept(kept);
			for (int i = 0; i < names.Count; i++)
			{
				System.CreedPastCounts.TryGetValue(names[i], out var count);
				System.CreedPastCounts[names[i]] = count + 1;
			}
			if (added)
			{
				KingdomLog.Log("creed: " + (Settler.ShortDisplayName ?? "settler") + " has now held with " + Creed + " and left it ("
					+ names.Count + "/" + KingdomCreedRules.MaxKeptCreeds + " remembered)");
			}
			return added;
		}

		// Takes one settler's whole history out of the city's tally. Never drives a count below
		// zero, and removes the entry outright at zero so the tally never grows a tail of creeds
		// nobody here has ever held -- which is the exact fact the visibility law reads.
		private static void DropPast(KingdomSystem System, GameObject Leaver)
		{
			List<string> names = PastOf(Leaver);
			for (int i = 0; i < names.Count; i++)
			{
				if (!System.CreedPastCounts.TryGetValue(names[i], out var count))
				{
					continue;
				}
				if (count > 1)
				{
					System.CreedPastCounts[names[i]] = count - 1;
				}
				else
				{
					System.CreedPastCounts.Remove(names[i]);
				}
			}
		}
	}
}
