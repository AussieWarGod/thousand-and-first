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
			KingdomSurvey survey = Z == null ? null
				: KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			if (TryActiveBenefits(Z, survey, "ground character", out var benefits))
			{
				List<KingdomLayoutRules.LayoutMark> marks = MarksOf(Z);
				IReadOnlyList<KingdomBenefitReading> readings = benefits.Readings;
				for (int i = 0; i < readings.Count; i++)
				{
					KingdomBenefitReading reading = readings[i];
					if (!TryRoot(Z, reading, out GameObject item))
					{
						continue;
					}
					bool ours = reading.Designation.ProviderId == "taf.architecture"
						|| reading.Designation.ProviderId == "taf.adoption";
					if (ours && !KingdomUpgrade.IsFunctionallyBuilt(item))
					{
						continue;
					}
					if (!CoversWithin(item, reading, marks, X, Y))
					{
						continue;
					}
					Gather(lifts, item, reading);
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
		private static bool CoversWithin(GameObject Work, KingdomBenefitReading Reading,
			List<KingdomLayoutRules.LayoutMark> Marks, int X, int Y)
		{
			ReachBand band = EffectiveBandOf(Work, Reading);
			if (band >= ReachBand.Zone)
			{
				return true;
			}
			if (KingdomReachRules.ContainsPlotCell(Reading.Designation.Cells, X, Y))
			{
				return true;
			}
			if (band != ReachBand.Quarter)
			{
				return false;
			}
			Cell cell = Work.CurrentCell;
			int preferredX = cell == null ? int.MinValue : cell.X;
			int preferredY = cell == null ? int.MinValue : cell.Y;
			return KingdomReachRules.TryDesignationAnchor(Reading.Designation.Cells,
				preferredX, preferredY, out int anchorX, out int anchorY)
				&& KingdomReachRules.InQuarter(Marks, anchorX, anchorY, X, Y,
					KingdomReachRules.QuarterLinkCells, QuarterRadiusOf(Work, Reading));
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

		/// <summary>Whether a live education provider in its exact designation reaches this home &mdash; the re-based form
		/// of <c>KingdomFaith.ZoneEducated</c>. A home standing nowhere is reached by
		/// nothing.</summary>
		public static bool EducatedAt(KingdomSystem System, Zone Z, GameObject Home)
		{
			Cell cell = (Home == null) ? null : Home.CurrentCell;
			if (cell == null || !TryActiveBenefits(Z, null, "education reach",
				out KingdomBenefitIndex benefits)) return false;
			IReadOnlyList<KingdomBenefitReading> readings = benefits.Readings;
			for (int i = 0; i < readings.Count; i++)
			{
				KingdomBenefitReading reading = readings[i];
				if (KingdomBenefitCapabilities.Has(reading,
					KingdomBenefitCapabilities.Education)
					&& TryRoot(Z, reading, out GameObject root)
					&& ReachesCell(System, Z, root, reading, Z, cell.X, cell.Y)) return true;
			}
			return false;
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

		/// <summary>Retired pre-release key prefix. Old saves are zeroed on the next explicit
		/// observation or ownership transition; these unbound integers are never read as authority.</summary>
		public const string CityStatePrefix = "r_TAF_ReachCity_";

		/// <summary>Retired realm-band sibling of <see cref="CityStatePrefix"/>.</summary>
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
			// A report is a nominal query: it never cleans malformed state. Disabled or invalid
			// authority simply contributes zero; activation owns explicit revocation.
			if (!KingdomOffices.Enabled || System == null || string.IsNullOrEmpty(Kind)
				|| The.Game == null)
			{
				return 0;
			}
			int total = 0; long tick = The.Game.TimeTicks;
			for (int i = 0; i < (System.ClaimedZones?.Count ?? 0); i++)
			{
				if (System.ClaimedZones[i] != ExceptZoneID)
				{
					total = KingdomCatalogueRules.SaturatingCounterAdd(total,
						KingdomReachObservationRuntime.Amount(System,
							System.ClaimedZones[i], System.City?.SettlementId,
							Kind, RealmBand: false, CurrentTick: tick));
				}
			}
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int s = 0; s < nonSeat.Count; s++)
			{
				for (int i = 0; i < (nonSeat[s]?.ClaimedZones?.Count ?? 0); i++)
				{
					if (nonSeat[s].ClaimedZones[i] != ExceptZoneID)
					{
						total = KingdomCatalogueRules.SaturatingCounterAdd(total,
							KingdomReachObservationRuntime.Amount(System,
								nonSeat[s].ClaimedZones[i], nonSeat[s].City?.SettlementId,
								Kind, RealmBand: true, CurrentTick: tick));
					}
				}
			}
			string settlement = string.IsNullOrEmpty(ExceptZoneID)
				? System.SettlementIdForOwnedZone(The.ZoneManager?.ActiveZone?.ZoneID)
				: System.SettlementIdForOwnedZone(ExceptZoneID);
			if (string.IsNullOrEmpty(settlement)) settlement = System.City?.SettlementId;
			total = KingdomCatalogueRules.SaturatingCounterAdd(total,
				KingdomHostedArcology.ReachOverlay(
					System, Kind, settlement, ExceptZoneID));
			return total;
		}

		/// <summary>Whether any headed great work shades this city with one kind &mdash; the
		/// question the outsider register asks about a great scriptorium.</summary>
		public static bool CityShaded(KingdomSystem System, string Kind)
		{
			return CityShade(System, Kind) > 0;
		}

		// What one exact designation physically contributes. Provider operation, staffing, wear,
		// power, scope, and the catalogue cap have already been resolved by the benefit index.
		private static void Gather(List<KindAmount> Into, GameObject Root,
			KingdomBenefitReading Reading)
		{
			if (!KingdomObservedBenefitProjection.TryCarries(Root, Reading,
				out List<KindAmount> carries, out string failure))
			{
				KingdomLog.Log("reach: observed benefits failed closed ("
					+ (failure ?? "unknown physical evidence") + ")");
				return;
			}
			for (int i = 0; i < carries.Count; i++)
			{
				KindAmount carry = carries[i];
				if (!KingdomReachRules.IsPhysicalLift(carry.Kind) || carry.Amount <= 0)
				{
					continue;
				}
				Into.Add(new KindAmount(carry.Kind, carry.Amount));
			}
		}

		private static void GatherLive(List<KindAmount> Into, KingdomBenefitReading Reading)
		{
			IReadOnlyList<KindAmount> carries = Reading?.Carries;
			for (int i = 0; carries != null && i < carries.Count; i++)
			{
				KindAmount carry = carries[i];
				if (KingdomReachRules.IsPhysicalLift(carry.Kind) && carry.Amount > 0)
					Into.Add(new KindAmount(carry.Kind, carry.Amount));
			}
		}
	}
}
