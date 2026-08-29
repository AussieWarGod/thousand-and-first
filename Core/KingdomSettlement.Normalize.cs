using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public partial class KingdomSettlement
	{
#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomSettlement));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomSettlement));
			Normalize();
		}
#endif

		/// <summary>
		/// Repairs a settlement read from a save written by an older build, or handed in by a
		/// caller: null containers become empty ones, and a vocation this build does not know
		/// falls back to the neutral one rather than being carried as a name nothing can read.
		/// A null vocation is left null &mdash; that is the realm's first city, founded before
		/// there was a second to tell it from.
		/// </summary>
		public void Normalize()
		{
			// Carry bounded valid bytes exactly. Readers fold/dedupe their view, while an exact
			// settlement capture must not rewrite an extension's spelling merely by moving the city.
			// Only an aggregate outside the permanent heap contract is discarded on load.
			List<string> boundedKeepers;
			if (!KingdomZoningRules.TryDecodeRoster(KeepersRoster, out boundedKeepers))
			{
				KeepersRoster = "";
			}
			if (!Enum.IsDefined(typeof(GrowthStage), Stage))
			{
				Stage = GrowthStage.Camp;
			}
			if (LastMeal != KingdomRules.MealVerdict.None &&
				LastMeal != KingdomRules.MealVerdict.Scraps &&
				LastMeal != KingdomRules.MealVerdict.Plain &&
				LastMeal != KingdomRules.MealVerdict.Favored)
			{
				LastMeal = KingdomRules.MealVerdict.None;
			}
			if (Gate != KingdomRules.GatePolicy.Open &&
				Gate != KingdomRules.GatePolicy.Guarded)
			{
				Gate = KingdomRules.GatePolicy.Open;
			}
			if (Stores != KingdomRules.StoresPolicy.Plenty &&
				Stores != KingdomRules.StoresPolicy.Thrift)
			{
				Stores = KingdomRules.StoresPolicy.Plenty;
			}
			if (!Enum.IsDefined(typeof(KingdomRules.PetitionKind), PetitionKind))
			{
				PetitionKind = KingdomRules.PetitionKind.None;
			}
			if (!Enum.IsDefined(typeof(PetitionLifecycle), PetitionState))
			{
				PetitionState = PetitionLifecycle.None;
			}
			if (RaidState != 0 && RaidState != 1)
			{
				RaidState = 0;
				RaidFactionName = null;
				RaidDueTick = 0L;
			}
			// These three fields are frozen save-wire compatibility projections. Normalization is
			// their owning migration boundary, so the obsolete API warning is intentionally scoped
			// to this exact bridge rather than suppressed for the file or build.
#pragma warning disable 618
			if (RosterNames == null)
			{
				RosterNames = new List<string>();
			}
			if (RosterOrigins == null)
			{
				RosterOrigins = new List<string>();
			}
			if (RosterArrived == null)
			{
				RosterArrived = new List<string>();
			}
#pragma warning restore 618
			if (DeadNames == null)
			{
				DeadNames = new List<string>();
			}
			if (DeadOrigins == null)
			{
				DeadOrigins = new List<string>();
			}
			if (DeadArrived == null)
			{
				DeadArrived = new List<string>();
			}
			if (DeadCauses == null)
			{
				DeadCauses = new List<string>();
			}
			// Do not zip/truncate legacy living-roll evidence here. KingdomSystem owns the realm id
			// counter and performs the one-time adoption after both city books are available.
			TruncateParallelRows(DeadNames, DeadOrigins, DeadArrived, DeadCauses);
			if (OriginCounts == null)
			{
				OriginCounts = new Dictionary<string, int>();
			}
			if (CultureCounts == null)
			{
				CultureCounts = new Dictionary<string, int>();
			}
			if (SpeciesCounts == null)
			{
				SpeciesCounts = new Dictionary<string, int>();
			}
			if (IdentityCounts == null)
			{
				IdentityCounts = new Dictionary<string, int>();
			}
			if (CreedCounts == null)
			{
				CreedCounts = new Dictionary<string, int>();
			}
			if (CreedPastCounts == null)
			{
				CreedPastCounts = new Dictionary<string, int>();
			}
			if (ConversionShared == null)
			{
				ConversionShared = new Dictionary<string, int>();
			}
			if (ConversionToward == null)
			{
				ConversionToward = new Dictionary<string, string>();
			}
			if (ConversionResented == null)
			{
				ConversionResented = new Dictionary<string, int>();
			}
			if (ClaimedZones == null)
			{
				ClaimedZones = new List<string>();
			}
			if (ZoneDistricts == null)
			{
				ZoneDistricts = new Dictionary<string, string>();
			}
			if (ResearchShelf == null)
			{
				ResearchShelf = new Dictionary<string, int>();
			}
			// A negative accrual is a corrupt reading, not a city in debt to its own bench: the lab
			// mints nothing, so the worst a shelved or current subject can stand at is nothing.
			if (ResearchAccrued < 0)
			{
				ResearchAccrued = 0;
			}
			if (ResearchTakenUpTick < 0L)
			{
				ResearchTakenUpTick = 0L;
			}
			if (GuestbookLines == null)
			{
				GuestbookLines = new List<string>();
			}
			if (Ledger == null)
			{
				Ledger = new KingdomLedger();
			}
			Ledger.Normalize();
			if (City == null)
			{
				City = new Simulation.City.KingdomCityBook();
			}
			City.Normalize();
			if (LifecycleBook == null)
			{
				LifecycleBook = new KingdomLifecycleBook();
			}
			KingdomLifecycleRules.Normalize(LifecycleBook);
			if (string.IsNullOrEmpty(Style))
			{
				Style = "common";
			}
			if (!string.IsNullOrEmpty(Vocation) && !IsKnownVocation(Vocation))
			{
				Vocation = NeutralVocation;
			}
			// A stored level or stamp below zero is a corrupt reading, not a settlement in
			// debt: subsidence mints nothing, so both fail closed to "nothing measured yet".
			if (LastSubsidenceTick < 0L)
			{
				LastSubsidenceTick = 0L;
			}
			if (SupportedLevel < 0)
			{
				SupportedLevel = 0;
			}
			// Read the old field for ABI compatibility, then retire its economy unconditionally.
			// Optional civic titles cannot grant hidden capacity, including off-seat legacy rows.
			NotableShade = 0;
			// The meal shade fails closed the same way and for the same reason: a day's
			// eating is never a tax, so the worst a bad supper can be worth is nothing.
			if (MealShade < 0)
			{
				MealShade = 0;
			}
			// The two scarcity streaks fail closed the same way, and for the same reason: a
			// negative streak is a corrupt reading, and a ladder cannot owe a settlement rungs.
			if (DryStreak < 0)
			{
				DryStreak = 0;
			}
			if (HungerStreak < 0)
			{
				HungerStreak = 0;
			}
			if (LastFoodWorkTick < 0L)
			{
				LastFoodWorkTick = 0L;
			}
			if (LastSemanticTick < 0L)
			{
				LastSemanticTick = 0L;
			}
			if (HomecomingDays < 0)
			{
				HomecomingDays = 0;
			}
			if (!SemanticPassActive)
			{
				SemanticPassStartedTick = 0L;
				SemanticPassZoneId = null;
				SemanticPassStartedMask = 0L;
				SemanticPassCompletedMask = 0L;
			}
			else if (SemanticPassStartedTick < 0L || string.IsNullOrEmpty(SemanticPassZoneId)
				|| SemanticPassStartedMask < 0L || SemanticPassCompletedMask < 0L
				|| (SemanticPassCompletedMask & ~SemanticPassStartedMask) != 0L)
			{
				SemanticPassActive = false;
				SemanticPassStartedTick = 0L;
				SemanticPassZoneId = null;
				SemanticPassStartedMask = 0L;
				SemanticPassCompletedMask = 0L;
			}
			// A load in flight fails closed the same way: a negative count is a corrupt reading,
			// and a delivery cannot owe a city servings. A count with no crop name still arrives -
			// KingdomCrops.DeliverPending falls back to the city's own crop - so only the count is
			// repaired here.
			if (PendingCrop < 0)
			{
				PendingCrop = 0;
			}
		}

	}
}
