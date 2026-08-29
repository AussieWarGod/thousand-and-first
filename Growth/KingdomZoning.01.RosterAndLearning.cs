using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomZoning
	{
		/// <summary>
		/// Every knowledge key effective at the SEATED city: its own stored rolls &mdash; designs taught,
		/// machines certified, ceremonies held, nodes worked out &mdash; plus one <c>origin:</c> key
		/// for each people living there right now. Origins are read live off
		/// <c>KingdomSystem.OriginCounts</c> rather than stored, because a trade the settlement holds
		/// only because somebody from that country lives here should leave with them.
		/// The founder's permanent <c>rite:</c> ledger is then projected into this read. It never
		/// enters <see cref="KingdomSettlement.KeepersRoster"/>: the founder carries doors between
		/// cities and out of exile, while each city keeps only the rooms its own people finished.
		/// <para>
		/// <b>Seat only</b> (Addendum 22 B4). Knowledge is where it was taught, and teaching the
		/// other city is an ACT: carry the disk and walk, certify the machine there too, or set down
		/// at their bench what your other keepers worked out and let them walk the rest of it
		/// (<see cref="ShowKeepers"/>). What the founder carries between cities is doors, never
		/// rooms.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null yields an empty roster.</param>
		public static List<string> Roster(KingdomSystem System)
		{
			List<string> roster = KingdomZoningRules.DecodeRoster(Stored(System));
			if (System == null)
			{
				return roster;
			}
			foreach (string rite in KingdomResearch.FounderRites())
			{
				if (!roster.Contains(rite))
				{
					roster.Add(rite);
				}
			}
			AppendTallyKeys(roster, System.OriginCounts, KingdomZoningRules.KindOrigin);
			AppendKeys(roster, KingdomResidentIdentityRules.RosterKeys(System.CultureCounts,
				KingdomZoningRules.KindCulture));
			AppendKeys(roster, KingdomResidentIdentityRules.RosterKeys(System.SpeciesCounts,
				KingdomZoningRules.KindSpecies));
			AppendKeys(roster,
				KingdomResidentIdentityRules.IdentityRosterKeys(System.IdentityCounts));
			KingdomReopenedExoticActivation.AppendDerivedKeys(System, roster);
			return roster;
		}

		private static void AppendTallyKeys(List<string> Roster,
			IDictionary<string, int> Tallies, string Kind)
		{
			if (Roster == null || Tallies == null) return;
			foreach (KeyValuePair<string, int> people in Tallies)
			{
				if (people.Value <= 0)
				{
					continue;
				}
				string key = KingdomZoningRules.ComposeKey(Kind, people.Key);
				if (key != null && !Roster.Contains(key))
				{
					Roster.Add(key);
				}
			}
		}

		private static void AppendKeys(List<string> Roster, IList<string> Keys)
		{
			if (Roster == null || Keys == null) return;
			for (int i = 0; i < Keys.Count; i++)
				if (!Roster.Contains(Keys[i])) Roster.Add(Keys[i]);
		}

		/// <summary>
		/// The same read for a city the founder is not standing in &mdash; the realm's other city,
		/// a seceded one, or one captured into an exile. Used by the teaching act, which has to be
		/// able to say what the OTHER keepers know without seating them.
		/// </summary>
		/// <param name="City">The settlement record. Null yields an empty roster.</param>
		public static List<string> RosterOf(KingdomSettlement City)
		{
			List<string> roster = KingdomZoningRules.DecodeRoster((City == null) ? null : City.KeepersRoster);
			if (City == null)
			{
				return roster;
			}
			AppendTallyKeys(roster, City.OriginCounts, KingdomZoningRules.KindOrigin);
			AppendKeys(roster, KingdomResidentIdentityRules.RosterKeys(City.CultureCounts,
				KingdomZoningRules.KindCulture));
			AppendKeys(roster, KingdomResidentIdentityRules.RosterKeys(City.SpeciesCounts,
				KingdomZoningRules.KindSpecies));
			AppendKeys(roster,
				KingdomResidentIdentityRules.IdentityRosterKeys(City.IdentityCounts));
			return roster;
		}

		/// <summary>
		/// Adds one design to the SEATED city's keepers' stored knowledge &mdash; the keepers in
		/// front of the founder are the keepers being taught. Idempotent per city: teaching the same
		/// design twice in the same place changes nothing and reports false, so nothing can be
		/// farmed by repetition, and teaching it again in the OTHER city teaches that city
		/// (Addendum 22 B4/B5). Announces a rise in that city's craft when one happens, once, where
		/// the founder is standing.
		/// </summary>
		/// <param name="System">The realm; must be founded for the announcement to have a name
		/// to use, but the roster is stored regardless.</param>
		/// <param name="Kind">A knowledge kind &mdash; <c>disk</c>, <c>machine</c>, or one your
		/// own mod invents.</param>
		/// <param name="Name">Blueprint or design name. Case is folded away.</param>
		/// <returns>True when the settlement did not already know this.</returns>
		public static bool Learn(KingdomSystem System, string Kind, string Name,
			string GovernanceVerb = null)
		{
			string key = KingdomZoningRules.ComposeKey(Kind, Name);
			if (key == null)
			{
				MetricsManager.LogError("ThousandAndFirst zoning: refused an unusable knowledge key for kind '" + Kind + "', name '" + Name + "'");
				return false;
			}
			List<string> stored = KingdomZoningRules.DecodeRoster(Stored(System));
			if (stored.Contains(key))
			{
				return false;
			}
			TechLevel before = KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(stored));
			stored.Add(key);
			string encoded;
			if (!KingdomZoningRules.TryEncodeRoster(stored, out encoded))
			{
				KingdomLog.Log("zoning: the keepers' permanent roster is full; refused " + key);
				return false;
			}
			Store(System, encoded);
			if (!string.IsNullOrEmpty(GovernanceVerb) && !KingdomGovernanceScope.HasCommitted)
			{
				KingdomGovernanceScope.Commit(GovernanceVerb);
			}
			TechLevel after = KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(stored));
			KingdomLog.Log("zoning: learned " + key + " (" + before + " -> " + after + ")");
			if (after > before && System != null && System.Founded)
			{
				MessageQueue.AddPlayerMessage("{{G|" + KingdomPresentation.Rich(System.SeatName) + " now builds at the level of " + KingdomZoningRules.TechName(after) + ".}}");
				KingdomChronicle.Record(System, "the keepers of " + KingdomPresentation.Rich(System.KingdomDisplayName) + " reached the level of " + KingdomZoningRules.TechName(after));
			}
			return true;
		}

		/// <summary>
		/// Records that a machine hauled home was certified fit for the grid, which is one of the
		/// two ways a settlement's craft rises. Deliberately one-way: taking the machine back off
		/// the grid later returns the machine to the founder, not the knowledge to nobody &mdash;
		/// and one-way PER CITY (Addendum 22 B5), so a machine dragged on to the realm's other city
		/// and certified there teaches there too, and neither city forgets when the machine
		/// eventually leaves. Safe to call for a machine this city already recorded.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Machine">The machine just certified. Null and blueprint-less objects are
		/// ignored rather than stored as a blank key.</param>
		public static void RecordCertification(KingdomSystem System, GameObject Machine)
		{
			KingdomSystem.Guard("zoning certification", delegate
			{
				if (Machine == null || string.IsNullOrEmpty(Machine.Blueprint))
				{
					return;
				}
				if (Learn(System, KingdomZoningRules.KindMachine, Machine.Blueprint))
				{
					// Holding the artifact is most of an answer and never all of it: a node this
					// machine seeds is revealed and begun here, and the keepers still finish it.
					KingdomResearch.ApplySources(System);
				}
			});
		}

		/// <summary>The settlement's craft, derived from its roster. See
		/// <see cref="KingdomZoningRules.TechPoints"/> for what each kind of knowledge is worth.</summary>
		public static TechLevel Tech(KingdomSystem System)
		{
			return KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(Roster(System)));
		}

		/// <summary>
		/// One line for the status report naming the settlement's craft and what the next level
		/// costs, so the level is never a number the founder has to reverse-engineer from
		/// refusals.
		/// </summary>
		/// <param name="System">The realm. Null or unfounded reports an empty string.</param>
		public static string Readout(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return "";
			}
			List<string> roster = Roster(System);
			int points = KingdomZoningRules.TechPoints(roster);
			TechLevel level = KingdomZoningRules.LevelForPoints(points);
			int wanted = KingdomZoningRules.PointsToNext(points);
			string next = (wanted <= 0)
				? "  {{K|(the keepers have learned everything this settlement can)}}"
				: ("  {{K|(" + wanted + " more toward " + KingdomZoningRules.TechName((TechLevel)((int)level + 1)) + ")}}");
			return "\nCraft: {{C|" + KingdomZoningRules.TechName(level) + "}}" + next;
		}

	}
}
