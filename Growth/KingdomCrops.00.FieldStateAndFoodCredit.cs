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
		/// <summary>Blueprint tag declaring how many rows a design stands when it is sown. Read
		/// from the blueprint exactly the way a pantry's capacity is
		/// (<c>KingdomRules.LarderCapacityTag</c>), and for the same reason: what a design adds to
		/// the settlement's LEVEL is a catalogue fact, and how much actually stands in the ground
		/// is a fact about the object. <c>_notes/balance-sim.py</c> re-derives every food design's
		/// <c>Carries</c> from this tag.</summary>
		public const string RowsTag = "r_KingdomCropRows";

		/// <summary>An optional exact crop-blueprint declaration on a field design. A field
		/// without it accepts every seed in the merged style registry; a field with it accepts
		/// only that crop's seed. This is object behavior, not stratum behavior: the two deep
		/// vault designs can name fungus without inventing a second crop table for every cave.</summary>
		public const string CropBlueprintTag = "r_KingdomCropBlueprint";

		/// <summary>Marks a plant this file laid, so a later withdrawal or striking can find its
		/// own rows and nothing else's. The protection law's whole warrant for removing them.</summary>
		public const string RowProperty = "KingdomCropRow";

		/// <summary>Ties a row to the field that sowed it.</summary>
		public const string RowFieldProperty = "KingdomCropField";

		/// <summary>Tick the founder committed seed to this field. A DATE and not a clock: it is
		/// never re-anchored, and nothing reads it to decide what is owed &mdash; the cycle runs
		/// off <c>r_KingdomPlot.NextStageTick</c>. It exists so the chronicle and the report can
		/// say when this field was sown.</summary>
		public const string SownTickProperty = "KingdomCropSownTick";

		/// <summary>Rows this field was sown with. Stamped from <see cref="RowsTag"/> at sowing so
		/// a retune of the catalogue never silently changes what a field already in the ground is
		/// worth.</summary>
		public const string RowsProperty = "KingdomCropRows";

		/// <summary>Gatherings this field has already resolved. The kernel ordinal the seed-return
		/// draw is keyed on, so no cycle is ever asked twice and a reload cannot re-roll one.</summary>
		public const string CyclesProperty = "KingdomCropCycles";

		/// <summary>The seed blueprint committed to this field, so a withdrawal hands back what
		/// was actually put in.</summary>
		public const string SeedProperty = "KingdomCropSeed";

		/// <summary>The last want this field announced (STANDARDS 7b), as a
		/// <c>KingdomCropRules.FieldWant</c>. Zero means nothing is being said, so the next real
		/// block speaks.</summary>
		public const string SaidProperty = "KingdomCropSaid";

		// ==================================================================================
		// Reading a field
		// ==================================================================================

		/// <summary>The field part of a finished work, or null for anything that is not a field.</summary>
		public static r_KingdomPlot FieldOf(GameObject Work)
		{
			if (!GameObject.Validate(Work) || Work.GetIntProperty("KingdomBuilt") != 1)
			{
				return null;
			}
			return Work.GetPart<r_KingdomPlot>();
		}

		/// <summary>Rows this design stands when it is sown, off its own blueprint. Zero for a
		/// blueprint that declares none, which is a field that grows nothing and says so at the
		/// first attempt to sow it.</summary>
		public static int DeclaredRows(GameObject Work)
		{
			if (Work == null)
			{
				return 0;
			}
			int rows;
			if (!int.TryParse(Work.GetTag(RowsTag, ""), out rows) || rows < 0)
			{
				return 0;
			}
			return rows;
		}

		/// <summary>The exact crop this field design accepts, or null when its design leaves the
		/// choice to the founder. Blueprint inheritance is resolved by Qud's ordinary tag lookup.</summary>
		public static string DeclaredCrop(GameObject Work)
		{
			if (!GameObject.Validate(Work))
			{
				return null;
			}
			string crop = Work.GetTag(CropBlueprintTag, "");
			return string.IsNullOrWhiteSpace(crop) ? null : crop.Trim();
		}

		/// <summary>Whether the founder has committed seed to this field. The whole of the
		/// Addendum 11(b) gate, read in one place so every consumer agrees.</summary>
		public static bool IsSown(GameObject Work)
		{
			r_KingdomPlot field = FieldOf(Work);
			return field != null && field.Stage != KingdomCropRules.PlotStage.Dormant;
		}

		/// <summary>Whether this work is worn past the point where anything comes out of it.</summary>
		public static bool IsCondemned(GameObject Work)
		{
			return KingdomLodgingRules.IsCondemned(KingdomWear.WearOf(Work));
		}

		/// <summary>
		/// The same parsed <c>Carries</c> list with the <c>food</c> entry dropped when this work
		/// is a field nobody has sown. Addendum 11(b): a farm starts producing only once seeds
		/// are committed, so uncommitted ground carries no food to the settlement's level and
		/// makes none in a day.
		/// <para>
		/// Everything else the design carries is left exactly where it was. A home farm's mill and
		/// its yard are built, standing and real whether or not a row is in the ground; only the
		/// dinner is conditional. The list is copied rather than edited, because the caller's is
		/// the catalogue's own parse and is reused for every work of the same design.
		/// </para>
		/// </summary>
		/// <param name="Work">The finished work being folded into the level.</param>
		/// <param name="Carries">Its design's parsed carries. Null passes straight through.</param>
		public static List<KindAmount> WithoutUnsownFood(GameObject Work, List<KindAmount> Carries)
		{
			if (Carries == null || Carries.Count == 0)
			{
				return Carries;
			}
			r_KingdomPlot field = FieldOf(Work);
			if (field == null || field.Stage != KingdomCropRules.PlotStage.Dormant)
			{
				return Carries;
			}
			// TryParseTally already folds every kind to its lower-case token, so the comparison
			// is against the constant directly rather than through a second normaliser that
			// could disagree with the first.
			List<KindAmount> kept = null;
			for (int i = 0; i < Carries.Count; i++)
			{
				if (Carries[i].Kind != KingdomCatalogueRules.SupportFood)
				{
					continue;
				}
				kept = new List<KindAmount>(Carries.Count - 1);
				for (int j = 0; j < Carries.Count; j++)
				{
					if (Carries[j].Kind != KingdomCatalogueRules.SupportFood)
					{
						kept.Add(Carries[j]);
					}
				}
				break;
			}
			return kept ?? Carries;
		}

		/// <summary>
		/// The daily food this zone's SOWN fields are already counted for, which the growth pass
		/// subtracts from its clocked make. The cycle delivers that food physically, on the crop's
		/// own six days, so counting it a second time per day would feed the settlement twice out
		/// of one field.
		/// <para>
		/// Folded at exactly the effectiveness <c>KingdomSubsidence.Supports</c> folds it at, and
		/// through exactly the same <c>KingdomCatalogueRules.Carried</c>, so the subtraction
		/// cancels the addition to the unit rather than approximately.
		/// </para>
		/// <para>
		/// <b>And it carries no method factor, deliberately.</b> What this subtracts is what the
		/// book CREDITED the field with, not what the field GREW: the credit is
		/// <c>Supports</c>'s own baseline carry, so the subtraction has to be that same baseline or
		/// it stops cancelling. The keepers' method lands on the physical gathering instead
		/// (<c>KingdomCropRules.HarvestYield</c>), which is why a researched realm eats better
		/// &mdash; the book removes one field's worth and the field delivers rather more than one field's
		/// worth, and the difference is exactly the bonus. Methoding this line as well would hand
		/// that difference straight back and, on a settlement whose granaries are counted here
		/// too, would charge the granaries for the fields' learning.
		/// </para>
		/// </summary>
		/// <param name="Survey">The pass's survey. Null carries nothing.</param>
		public static int CycledFoodPerDay(KingdomSurvey Survey)
		{
			if (Survey == null)
			{
				return 0;
			}
			int cycled = 0;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				r_KingdomPlot field = FieldOf(work);
				if (field == null || field.Stage == KingdomCropRules.PlotStage.Dormant)
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
				cycled += KingdomCatalogueRules.Carried(
					KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportFood), effectiveness);
			}
			return cycled;
		}

	}
}
