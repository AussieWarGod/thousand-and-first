using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Physical-designation boundary for runtime reach. Catalogue reach remains useful
	/// for previews; live works must prove an exact designation before they shade any ground.</summary>
	public static partial class KingdomReach
	{
		internal static bool TryActiveBenefits(Zone Z, KingdomSurvey Survey,
			string Consumer, out KingdomBenefitIndex Benefits)
		{
			Benefits = null;
			if (Z == null) return false;
			KingdomSurvey exact = Survey ?? KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			string failure = null;
			if (exact != null && ReferenceEquals(exact.Ground, Z)
				&& exact.TryBenefits(out Benefits, out failure)) return true;
			KingdomLog.Log((Consumer ?? "reach") + ": physical benefits failed closed ("
				+ (failure ?? "no exact active-zone survey") + ")");
			return false;
		}

		internal static bool TryReading(GameObject Work, KingdomBenefitIndex Benefits,
			out KingdomBenefitReading Reading)
		{
			Reading = null;
			if (!GameObject.Validate(Work) || Benefits == null
				|| string.IsNullOrEmpty(Work.IDIfAssigned)) return false;
			Reading = Benefits.ReadingForRoot(Work.IDIfAssigned);
			return Reading?.Designation != null
				&& string.Equals(Reading.Designation.RootId, Work.IDIfAssigned,
					StringComparison.Ordinal);
		}

		internal static bool TryRoot(Zone Z, KingdomBenefitReading Reading,
			out GameObject Root)
		{
			Root = null;
			return Z != null && Reading?.Designation != null
				&& KingdomConstruction.FindExactId(Z, Reading.Designation.RootId, out Root)
					== KingdomPhysicalLookupState.Exact
				&& GameObject.Validate(Root) && ReferenceEquals(Root.CurrentZone, Z);
		}

		/// <summary>The live physical band. Missing or unreadable designation evidence reaches
		/// only the root's own cell and never falls back to a catalogue plot tier.</summary>
		public static ReachBand BandOf(GameObject Work)
		{
			Zone zone = Work?.CurrentZone;
			return TryActiveBenefits(zone, null, "reach band", out var benefits)
				&& TryReading(Work, benefits, out var reading)
				? BandOf(Work, reading) : ReachBand.Plot;
		}

		public static ReachBand EffectiveBandOf(GameObject Work)
		{
			Zone zone = Work?.CurrentZone;
			return TryActiveBenefits(zone, null, "effective reach band", out var benefits)
				&& TryReading(Work, benefits, out var reading)
				? EffectiveBandOf(Work, reading) : ReachBand.Plot;
		}

		public static int QuarterRadiusOf(GameObject Work)
		{
			Zone zone = Work?.CurrentZone;
			return TryActiveBenefits(zone, null, "reach radius", out var benefits)
				&& TryReading(Work, benefits, out var reading)
				? QuarterRadiusOf(Work, reading) : KingdomReachRules.QuarterRadius(0);
		}

		/// <summary>Runtime band from exact physical extent. Authored architecture may retain an
		/// explicit reach override; an adopted or external tiny room cannot borrow one.</summary>
		internal static ReachBand BandOf(GameObject Work, KingdomBenefitReading Reading)
		{
			if (!Matches(Work, Reading)) return ReachBand.Plot;
			string key = Reading.Designation.BuildingKey;
			if (Reading.Designation.ProviderId == "taf.architecture"
				&& Declared.TryGetValue(key, out ReachBand declared)) return declared;
			KingdomPlotRules.PlotSize size = KingdomReachRules.SizeForDesignation(
				Reading.Designation.Cells);
			ChainPlace place = string.IsNullOrEmpty(key) ? new ChainPlace() : PlaceOf(key);
			return KingdomReachRules.Derive(size, place.Index, place.Count);
		}

		internal static ReachBand EffectiveBandOf(GameObject Work,
			KingdomBenefitReading Reading)
		{
			ReachBand band = BandOf(Work, Reading);
			return !KingdomReachRules.RequiresSeat(band) || IsHeaded(Work)
				? band : KingdomReachRules.Unheaded(band);
		}

		internal static int QuarterRadiusOf(GameObject Work, KingdomBenefitReading Reading)
		{
			if (!Matches(Work, Reading)) return KingdomReachRules.QuarterRadius(0);
			string key = Reading.Designation.BuildingKey;
			return KingdomReachRules.QuarterRadius(
				string.IsNullOrEmpty(key) ? 0 : PlaceOf(key).Index);
		}

		private static bool Matches(GameObject Work, KingdomBenefitReading Reading)
		{
			return GameObject.Validate(Work) && Reading?.Designation != null
				&& !string.IsNullOrEmpty(Work.IDIfAssigned)
				&& string.Equals(Work.IDIfAssigned, Reading.Designation.RootId,
					StringComparison.Ordinal);
		}
	}
}
