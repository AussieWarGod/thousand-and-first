using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.World;

	/// <summary>
	/// The engine-coupled half of the satellites: which great works the realm keeps ANYWHERE, which
	/// outposts a city already keeps, and the small registry that maps an outpost to its parent.
	/// <para>
	/// <b>Why a registry of its own rather than a ninth field on <c>ZoneGate</c>.</b> The other
	/// eight gates are all questions about one design against one piece of ground; this one is a
	/// question about the whole realm, answered from the city books, and it needs a map from
	/// outpost to parent that nothing else wants. Kept here it is one system in one folder with one
	/// test file, and the catalogue's own gate parser is left alone &mdash; STANDARDS &sect;2's
	/// folder-per-system, applied to a lane rather than to a file.
	/// </para>
	/// <para>
	/// <b>Process-static and rebuilt at every catalogue load</b>, which is the same discipline
	/// <c>KingdomZoning</c>'s gate table keeps and for the same reason: a third-party file may
	/// declare an outpost of its own megastructure, and re-declaring a key must own it whole
	/// (STANDARDS &sect;6).
	/// </para>
	/// </summary>
	internal static class KingdomSatellite
	{
		private static readonly Dictionary<string, string> Parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Forgets every declaration. Called when the catalogue is rebuilt, before the
		/// first <see cref="Declare"/> of the new load.</summary>
		internal static void Reset()
		{
			Parents.Clear();
		}

		/// <summary>
		/// Records that one design is an outpost of another. Called once per building record at
		/// catalogue load, from the same place the zoning gates are registered.
		/// </summary>
		/// <param name="Key">The outpost's registry key.</param>
		/// <param name="Satellite">Its raw <c>Satellite</c> attribute. Absent or blank forgets any
		/// earlier declaration for the key, so a third-party file may un-declare one.</param>
		internal static void Declare(string Key, string Satellite)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			string parent = KingdomSatelliteRules.ParentOf(Satellite);
			if (parent == null)
			{
				Parents.Remove(Key);
				return;
			}
			Parents[Key] = parent;
		}

		/// <summary>The great work a design is an outpost of, or null when it is not one. Null for
		/// every design in the catalogue but two.</summary>
		internal static string ParentOf(string Key)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return null;
			}
			return Parents.TryGetValue(Key, out string parent) ? parent : null;
		}

		/// <summary>
		/// The whole outpost verdict for one design on one piece of ground.
		/// <para>
		/// <b>Fails open at every unknown</b>, which is the bargain the purpose gate already makes:
		/// a realm whose books could not be read must let the founder build, because the alternative
		/// is a catalogue bricked by a record nobody can open. Concretely, a design nothing declared
		/// an outpost is permitted without a question being asked at all.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null permits.</param>
		/// <param name="ZoneID">The ground being built on.</param>
		/// <param name="Key">The design being zoned.</param>
		/// <param name="Detail">The parent's key when the realm keeps none of it, or the outpost's
		/// key when this city already keeps one. Empty when permitted.</param>
		internal static KingdomSatelliteVerdict Judge(KingdomSystem System, string ZoneID, string Key, out string Detail)
		{
			Detail = "";
			string parent = ParentOf(Key);
			if (parent == null || System == null || !System.Founded)
			{
				return KingdomSatelliteVerdict.Allowed;
			}
			if (!RealmKeeps(System, parent))
			{
				Detail = parent;
				return KingdomSatelliteVerdict.RefusedNoParent;
			}
			string kept = KeptOutpostOf(System, ZoneID, parent, Key);
			KingdomSatelliteVerdict verdict = KingdomSatelliteRules.Judge(
				Satellite: true, RealmKeepsParent: true, CityKeeps: kept, Key: Key);
			if (verdict == KingdomSatelliteVerdict.RefusedCityKeeps)
			{
				Detail = kept;
			}
			return verdict;
		}

		/// <summary>
		/// The same verdict for the ground under the founder's feet, which is the only ground a
		/// commission menu is ever opened on. The one call the zoning gate makes.
		/// </summary>
		internal static KingdomSatelliteVerdict JudgeActiveGround(KingdomSystem System, string Key, out string Detail)
		{
			Zone active = The.ZoneManager?.ActiveZone;
			return Judge(System, (active != null) ? active.ZoneID : null, Key, out Detail);
		}

		/// <summary>
		/// Whether a design stands anywhere in the realm.
		/// <para>
		/// Both books, then the loaded zone as the freshness patch &mdash;
		/// <c>KingdomZoning.KeptMegastructure</c>'s two sources in the same order of authority, and
		/// for the same reason: the books cover ground nobody has stood in for a season, and a work
		/// finished since that ground's last settlement pass is standing in the world without being
		/// written down yet.
		/// </para>
		/// </summary>
		internal static bool RealmKeeps(KingdomSystem System, string DesignKey)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(DesignKey))
			{
				return false;
			}
			KingdomData.EnsureBuildings();
			string blueprint = BlueprintOf(DesignKey);
			if (Keeps(System.City, DesignKey, blueprint))
			{
				return true;
			}
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				if (Keeps(nonSeat[i].City, DesignKey, blueprint)) return true;
			return StandsIn(The.ZoneManager?.ActiveZone, DesignKey);
		}

		/// <summary>
		/// The outpost of one parent this city already keeps, or null. The key is compared against
		/// the design being zoned by the pure rule, so re-raising the one already kept is not a
		/// second one.
		/// </summary>
		private static string KeptOutpostOf(KingdomSystem System, string ZoneID, string Parent, string Key)
		{
			Simulation.City.KingdomCityBook book = BookFor(System, ZoneID);
			if (book == null || book.WorkDesignKeys == null)
			{
				return null;
			}
			foreach (KeyValuePair<string, string> pair in Parents)
			{
				if (!string.Equals(pair.Value, Parent, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				if (Keeps(book, pair.Key, BlueprintOf(pair.Key)))
				{
					return pair.Key;
				}
			}
			// The freshness patch again, and only for the design actually being zoned: an outpost
			// raised this morning refuses a second one this afternoon.
			Zone active = The.ZoneManager?.ActiveZone;
			if (active != null && string.Equals(active.ZoneID, ZoneID, StringComparison.Ordinal) && StandsIn(active, Key))
			{
				return Key;
			}
			return null;
		}

		/// <summary>The line an outpost carries in its own description.</summary>
		internal static string OfficeDescription()
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			return KingdomSatelliteRules.DescriptionLine(
				KingdomSatelliteRules.OfficeSlice(), KingdomSatelliteRules.OfficeWithheld(), CityKeeping(system, KingdomAnnexeRules.AnnexeKey));
		}

		/// <summary>Which city keeps a design, for a sentence that would rather name a place than
		/// say "somewhere". Null when nothing could tell, which reads as an honest phrase.</summary>
		private static string CityKeeping(KingdomSystem System, string DesignKey)
		{
			if (System == null || !System.Founded)
			{
				return null;
			}
			KingdomData.EnsureBuildings();
			string blueprint = BlueprintOf(DesignKey);
			if (Keeps(System.City, DesignKey, blueprint))
			{
				return KingdomPresentation.Rich(System.SeatName);
			}
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				if (Keeps(nonSeat[i].City, DesignKey, blueprint))
					return KingdomPresentation.Rich(string.IsNullOrEmpty(nonSeat[i].SettlementName)
						? System.KingdomDisplayName : nonSeat[i].SettlementName);
			return null;
		}

		private static Simulation.City.KingdomCityBook BookFor(KingdomSystem System, string ZoneID)
		{
			if (System == null || string.IsNullOrEmpty(ZoneID))
			{
				return null;
			}
			if (System.ClaimedZones != null && System.ClaimedZones.Contains(ZoneID))
			{
				return System.City;
			}
			return System.FindNonSeatSettlementByZone(ZoneID)?.City;
		}

		private static bool Keeps(Simulation.City.KingdomCityBook Book, string DesignKey, string Blueprint)
		{
			if (Book == null || Book.WorkDesignKeys == null || string.IsNullOrEmpty(DesignKey))
			{
				return false;
			}
			for (int i = 0; i < Book.WorkDesignKeys.Count; i++)
			{
				string stored = Book.WorkDesignKeys[i];
				if (string.IsNullOrEmpty(stored))
				{
					continue;
				}
				// The book's column carries a blueprint and a loaded-zone read carries a key, so
				// both are matched (KingdomZoning.MegastructureKeyOf's own reason).
				if (string.Equals(stored, DesignKey, StringComparison.OrdinalIgnoreCase)
					|| (!string.IsNullOrEmpty(Blueprint) && string.Equals(stored, Blueprint, StringComparison.OrdinalIgnoreCase)))
				{
					return true;
				}
			}
			return false;
		}

		private static bool StandsIn(Zone Where, string DesignKey)
		{
			if (Where == null || string.IsNullOrEmpty(DesignKey))
			{
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Where) ?? KingdomSurvey.Take(Where);
			for (int i = 0; i < survey.Built.Count; i++)
			{
				GameObject work = survey.Built[i];
				if (work != null && work.GetIntProperty("KingdomBuilt") == 1
					&& string.Equals(KingdomUpgrade.DesignKeyOf(work), DesignKey, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		private static string BlueprintOf(string DesignKey)
		{
			List<KingdomRules.BuildEntry> entries = KingdomData.Buildings;
			for (int i = 0; i < entries.Count; i++)
			{
				if (string.Equals(entries[i].Key, DesignKey, StringComparison.OrdinalIgnoreCase))
				{
					return entries[i].Blueprint;
				}
			}
			return null;
		}
	}
}
