using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	public static partial class KingdomCrops
	{
		// ==================================================================================
		// The next link: what industry does with a harvest
		// ==================================================================================

		/// <summary>
		/// Whether this finished work is a mill &mdash; asked of the OBJECT, off vanilla's own
		/// <c>Mill</c> part, exactly as <see cref="FieldOf"/> asks a field off
		/// <c>r_KingdomPlot</c>.
		/// <para>
		/// Vanilla's <c>Mill</c> (<c>D/XRL/World/Parts/Mill.cs:9</c>) is one of only four parts in
		/// the whole game that transform matter, and its blank-target path runs
		/// <c>Campfire.PerformPreserve</c> (<c>:82-101</c>) &mdash; which is precisely what
		/// vanilla's shipped <c>Millstone</c> does with a vinewafer. Testing for the part rather
		/// than for a catalogue key means a third party's own millstone counts the moment it
		/// declares one, the same way a third party's cistern counts the moment it declares a
		/// <c>LiquidVolume</c>.
		/// </para>
		/// </summary>
		public static bool IsMill(GameObject Work)
		{
			return KingdomUpgrade.IsFunctionallyBuilt(Work) && Work.HasPart("Mill");
		}

		/// <summary>
		/// Servings a day the settlement's mills may transform at their current effectiveness.
		/// A mill delivers food PHYSICALLY, by taking real crops off the larder shelves and putting
		/// real preserves back. This figure budgets that positive operation; it is never population
		/// support, a passive daily credit, or a ration offset.
		/// <para>
		/// Baseline, with no method factor, so the physical transformation has one bounded source.
		/// </para>
		/// </summary>
		/// <param name="Survey">The pass's survey. Null makes nothing.</param>
		public static int MilledFoodPerDay(KingdomSurvey Survey)
		{
			if (Survey == null)
			{
				return 0;
			}
			int milled = 0;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!IsMill(work))
				{
					continue;
				}
				string key = KingdomUpgrade.DesignKeyOf(work);
				KingdomRules.BuildEntry entry;
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				List<KindAmount> carries;
				KingdomCatalogueRules.TryParseTally(entry.Carries, out carries, out _);
				if (carries == null)
				{
					continue;
				}
				int effectiveness = KingdomWear.EffectivenessOf(work);
				milled = KingdomCatalogueRules.SaturatingCounterAdd(milled,
					KingdomCatalogueRules.Carried(KingdomCatalogueRules.AmountOf(carries,
						KingdomCatalogueRules.SupportFood), effectiveness));
			}
			return milled;
		}

		/// <summary>
		/// What one crop of this settlement's becomes when it is bound to keep: the mod's stated
		/// staple where it has one (<c>KingdomRules.PreservedStapleFor</c>), otherwise the crop's
		/// OWN vanilla <c>PreservableItem Result</c>, read off a sample of the thing itself.
		/// <para>
		/// The fallback is what makes a third party's crop mill without this file knowing about
		/// it: <c>PreservableItem</c> is a two-field pure-data marker
		/// (<c>D/XRL/World/Parts/PreservableItem.cs:6-10</c>) and reading it is free of side
		/// effects, unlike <c>Campfire</c>'s own ingredient listing, which mutates counts
		/// (VANILLA-PRODUCTION-TRUTH 2.3's static-cache hazard note).
		/// </para>
		/// </summary>
		/// <param name="Crop">A crop blueprint. Null or unknown yields null.</param>
		/// <returns>The preserved blueprint, or null for a crop nothing can bind.</returns>
		public static string StapleFor(string Crop)
		{
			string stated = KingdomRules.PreservedStapleFor(Crop);
			if (!string.IsNullOrEmpty(stated))
			{
				return stated;
			}
			if (string.IsNullOrEmpty(Crop) || !GameObjectFactory.Factory.HasBlueprint(Crop))
			{
				return null;
			}
			GameObject sample = GameObject.Create(Crop);
			if (sample == null)
			{
				return null;
			}
			PreservableItem preservable = sample.GetPart<PreservableItem>();
			string result = (preservable == null) ? null : preservable.Result;
			sample.Obliterate();
			return string.IsNullOrEmpty(result) ? null : result;
		}

	}
}
