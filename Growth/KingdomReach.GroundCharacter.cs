using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomReach
	{
		// --- What one piece of ground is like ---------------------------------------------------

		/// <summary>
		/// Everything in reach of one cell, folded into what that ground is like to stand on.
		/// Reads only the zone given, which is the whole of what an attended pass can honestly
		/// see; the citywide half is <see cref="CityShade"/>, which reads what earlier passes
		/// recorded.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone the place is in.</param>
		/// <param name="X">The place.</param>
		/// <param name="Y">The place.</param>
		/// <returns>Never null.</returns>
		public static GroundCharacter CharacterAt(KingdomSystem System, Zone Z, int X, int Y)
		{
			List<KindAmount> lifts = new List<KindAmount>();
			if (Z != null)
			{
				List<KingdomLayoutRules.LayoutMark> marks = MarksOf(Z);
				foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
				{
					if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
					{
						continue;
					}
					KingdomRules.BuildEntry entry;
					string key = KingdomUpgrade.DesignKeyOf(item);
					if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
					{
						continue;
					}
					if (!CoversWithin(item, marks, X, Y))
					{
						continue;
					}
					Gather(lifts, entry, item);
				}
			}
			// Everything standing HERE has just been counted from the ground itself, so the
			// recorded half deliberately skips this zone: the record exists to carry a great work
			// the founder cannot presently see, never to count one twice.
			for (int i = 0; i < KingdomReachRules.LiftOrder.Length; i++)
			{
				string kind = KingdomReachRules.LiftOrder[i];
				int city = CityShadeExcept(System, kind, (Z == null) ? null : Z.ZoneID);
				if (city > 0)
				{
					lifts.Add(new KindAmount(kind, city));
				}
			}
			return KingdomReachRules.Character(lifts);
		}

		// The same-zone half of RelationOf, kept separate so a whole-zone sweep reads the marks
		// once instead of once per work.
		private static bool CoversWithin(GameObject Work, List<KingdomLayoutRules.LayoutMark> Marks, int X, int Y)
		{
			ReachBand band = EffectiveBandOf(Work);
			if (band >= ReachBand.Zone)
			{
				return true;
			}
			KingdomPlotRules.PlotRect footprint;
			if (KingdomPlots.TryReadFootprint(Work, out footprint) && footprint.Contains(X, Y))
			{
				return true;
			}
			if (band != ReachBand.Quarter)
			{
				return false;
			}
			Cell cell = Work.CurrentCell;
			return cell != null && KingdomReachRules.InQuarter(Marks, cell.X, cell.Y, X, Y,
				KingdomReachRules.QuarterLinkCells, QuarterRadiusOf(Work));
		}

		/// <summary>
		/// Whether anything in reach of a place shades it with one kind. The re-based form of
		/// every hand-authored scope: a knowledge work softens the quarrel of whoever it
		/// <em>reaches</em>, not of whoever happens to share a zone with it.
		/// </summary>
		public static bool ShadedAt(KingdomSystem System, Zone Z, int X, int Y, string Kind)
		{
			GroundCharacter character = CharacterAt(System, Z, X, Y);
			for (int i = 0; i < character.Lifts.Count; i++)
			{
				if (character.Lifts[i].Kind == Kind && character.Lifts[i].Amount > 0)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Whether a staffed knowledge work reaches this home &mdash; the re-based form
		/// of <c>KingdomFaith.ZoneEducated</c>. A home standing nowhere is reached by
		/// nothing.</summary>
		public static bool EducatedAt(KingdomSystem System, Zone Z, GameObject Home)
		{
			Cell cell = (Home == null) ? null : Home.CurrentCell;
			return cell != null && ShadedAt(System, Z, cell.X, cell.Y, LearningSupport);
		}

		/// <summary>One line for the status report naming what shades the ground the founder is
		/// standing on (Addendum 6: a quarter's character must be readable).</summary>
		public static string QuarterLine(KingdomSystem System, Zone Z)
		{
			Cell cell = The.Player?.CurrentCell;
			if (cell == null || Z == null || cell.ParentZone == null || cell.ParentZone.ZoneID != Z.ZoneID)
			{
				return "";
			}
			return KingdomReachRules.QuarterLine(CharacterAt(System, Z, cell.X, cell.Y));
		}

		// --- The citywide record -----------------------------------------------------------------

		/// <summary>Game-state key prefix a claimed zone's headed city-band lift is recorded
		/// under, per kind. A generic, already-serialized slot on the game rather than a new field
		/// on <c>KingdomSystem</c>, exactly as <c>KingdomPlots.MaterialStatePrefix</c> is &mdash;
		/// so a citywide effect can be read from a zone that is not loaded without touching any
		/// positionally-reflected field layout.</summary>
		public const string CityStatePrefix = "r_TAF_ReachCity_";

		/// <summary>The same, for a great work whose reach is the whole realm and which therefore
		/// carries into the realm's other city.</summary>
		public const string RealmStatePrefix = "r_TAF_ReachRealm_";

		/// <summary>
		/// What the realm's headed great works shade this city with. Summed from what each
		/// claimed zone's own attended pass last recorded, so nothing here advances while the
		/// founder is away: a zone the founder has not visited since the temple was struck goes on
		/// reporting the temple until they walk back in and see the ground.
		/// </summary>
		/// <param name="System">The realm. Null shades nothing.</param>
		/// <param name="Kind">A lifting support.</param>
		public static int CityShade(KingdomSystem System, string Kind)
		{
			return CityShadeExcept(System, Kind, null);
		}

		/// <summary>The same, less one zone's own record &mdash; for a caller that has just
		/// counted that zone's ground for itself and must not count it twice.</summary>
		public static int CityShadeExcept(KingdomSystem System, string Kind, string ExceptZoneID)
		{
			if (System == null || string.IsNullOrEmpty(Kind) || The.Game == null)
			{
				return 0;
			}
			int total = 0;
			for (int i = 0; i < System.ClaimedZones.Count; i++)
			{
				if (System.ClaimedZones[i] != ExceptZoneID)
				{
					total += The.Game.GetIntGameState(CityStatePrefix + System.ClaimedZones[i] + "_" + Kind);
				}
			}
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int s = 0; s < nonSeat.Count; s++)
			{
				for (int i = 0; i < nonSeat[s].ClaimedZones.Count; i++)
				{
					if (nonSeat[s].ClaimedZones[i] != ExceptZoneID)
					{
						total += The.Game.GetIntGameState(RealmStatePrefix +
							nonSeat[s].ClaimedZones[i] + "_" + Kind);
					}
				}
			}
			return total;
		}

		/// <summary>Whether any headed great work shades this city with one kind &mdash; the
		/// question the outsider register asks about a great scriptorium.</summary>
		public static bool CityShaded(KingdomSystem System, string Kind)
		{
			return CityShade(System, Kind) > 0;
		}

		// Rewrites this zone's own record from what is standing here now, including to zero: a
		// great work that was struck, or whose seat emptied, stops shading the city the pass the
		// founder sees it, and never before.
		private static void Record(Zone Z, List<KindAmount> Shaded, List<KindAmount> Realm)
		{
			if (The.Game == null || Z == null)
			{
				return;
			}
			// The realm-band half is written from its own filter rather than derived from the
			// city half: only a work that reaches the whole realm carries into the other city, so
			// a city-band cathedral never shades a city it cannot see.
			GroundCharacter cityCharacter = KingdomReachRules.Character(Shaded);
			GroundCharacter realmCharacter = KingdomReachRules.Character(Realm);
			for (int i = 0; i < KingdomReachRules.LiftOrder.Length; i++)
			{
				string kind = KingdomReachRules.LiftOrder[i];
				The.Game.SetIntGameState(CityStatePrefix + Z.ZoneID + "_" + kind, AmountIn(cityCharacter, kind));
				The.Game.SetIntGameState(RealmStatePrefix + Z.ZoneID + "_" + kind, AmountIn(realmCharacter, kind));
			}
		}

		private static int AmountIn(GroundCharacter Character, string Kind)
		{
			for (int i = 0; i < Character.Lifts.Count; i++)
			{
				if (Character.Lifts[i].Kind == Kind)
				{
					return Character.Lifts[i].Amount;
				}
			}
			return 0;
		}

		// What one standing work actually contributes: its declared lifts, scaled by how well it
		// is running. A work that declares no crew runs at full; a crewed work runs at whatever
		// the staffing pass gave it, so an idle shrine shades nothing and says nothing new.
		private static void Gather(List<KindAmount> Into, KingdomRules.BuildEntry Entry, GameObject Work)
		{
			// A malformed Carries is already reported by the catalogue validator, and whatever
			// parsed before the bad pair still counts, so the verdict is deliberately unread.
			List<KindAmount> carries;
			KingdomCatalogueRules.TryParseTally(Entry.Carries, out carries, out _);
			// Crewed or not, a work shades its ground by what it is actually managing (Addendum
			// 10(b)). KingdomWear no longer folds condition back into KingdomEffectiveness - that
			// property is the staffing pass's crew stretch and nothing else - so this asks for the
			// combined figure directly, the way KingdomSubsidence and KingdomPower do.
			int percent = KingdomWear.EffectivenessOf(Work);
			for (int i = 0; i < carries.Count; i++)
			{
				if (!KingdomReachRules.ScopedByReach(carries[i].Kind))
				{
					continue;
				}
				int amount = KingdomReachRules.Scaled(carries[i].Amount, percent);
				if (amount > 0)
				{
					Into.Add(new KindAmount(carries[i].Kind, amount));
				}
			}
		}	}
}
