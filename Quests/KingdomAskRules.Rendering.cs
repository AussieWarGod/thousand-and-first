using System;
using System.Collections.Generic;
using ThousandAndFirst.Api;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomAskRules
	{
		// ==================================================================================
		// A store at its ceiling while another zone has room. Addendum 12(f)'s haulage, asked
		// for rather than assumed: the city cannot move it and is saying so.
		// ==================================================================================

		private static void Backed(KingdomCityReading city, List<KingdomAsk> asks)
		{
			for (int i = 0; i < city.ZoneCount; i++)
			{
				KingdomZoneReading zone;
				if (!city.TryZone(i, out zone) || zone.Food.Capacity <= 0L || zone.Food.Room > 0L)
				{
					continue;
				}
				if (!RoomElsewhere(city, i))
				{
					continue;
				}
				asks.Add(new KingdomAsk(OwnKindPrefix + "haulage",
					"A larder is full to the lid, and there is room for it elsewhere.",
					"Set hands to haulage, or raise another larder where the food is grown.",
					zone.ZoneId, KingdomAskWeight.Passing));
			}
		}

		private static bool RoomElsewhere(KingdomCityReading city, int exceptIndex)
		{
			for (int i = 0; i < city.ZoneCount; i++)
			{
				KingdomZoneReading other;
				if (i == exceptIndex || !city.TryZone(i, out other))
				{
					continue;
				}
				if (other.Food.Room > 0L)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// What a work is called on the board: the name the founder sees on the building when the
		/// caller can resolve one, and the design key when it cannot. The model carries no display
		/// name of its own &mdash; appearance stays on the object (&sect;1.2(c)) &mdash; so this is
		/// the honest fallback rather than a second name store.
		/// </summary>
		internal static string Name(KingdomWorkReading Work, Func<string, string> Resolve)
		{
			string resolved = (Resolve == null || string.IsNullOrEmpty(Work.DesignKey)) ? null : Resolve(Work.DesignKey);
			if (!string.IsNullOrEmpty(resolved))
			{
				return Capitalised(resolved);
			}
			return string.IsNullOrEmpty(Work.DesignKey) ? "A work" : ("The " + Work.DesignKey);
		}

		/// <summary>First letter up, and nothing else touched. Local rather than the engine's
		/// <c>Grammar.InitCap</c> because these rules are engine-free by construction, and one
		/// character is not worth the dependency.</summary>
		private static string Capitalised(string text)
		{
			if (string.IsNullOrEmpty(text) || text[0] < 'a' || text[0] > 'z')
			{
				return text;
			}
			return (char)(text[0] - 32) + text.Substring(1);
		}
	}
}
