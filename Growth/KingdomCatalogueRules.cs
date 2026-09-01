using System.Collections.Generic;

namespace ThousandAndFirst
{

	/// <summary>
	/// Engine-free rules for how the building catalogue is denominated, and for what is wrong with
	/// a catalogue file read as a whole.
	/// <para>
	/// <b>The denomination.</b> A design's <c>Carries</c> number is not a flow. It is what the
	/// building's authored civic contribution. Water and roof bind the live sustainable
	/// population; food describes the physical crop/storage/industry lane and never creates
	/// population pressure. Everything else a work gives lifts the habitable level
	/// (<see cref="LiftingSupports"/>) and is capped, so no amount of shrines outruns the water.
	/// One point of <c>water</c> is one dram a day sustained, which is one settler's thirst at
	/// camp rates.
	/// </para>
	/// <para>
	/// <b>What this file does not own.</b> Plot geometry, stage-by-tier gating, legacy contents rolls and
	/// wall material are <see cref="KingdomPlotRules"/>. The material vocabulary and the cost
	/// parser are <c>KingdomMaterialRules</c>. Gates are <see cref="KingdomZoningRules"/> and
	/// chains are <see cref="KingdomUpgradeRules"/>. This file adds the denomination and the
	/// whole-file cross-checks none of them can see alone, and defers everywhere else.
	/// </para>
	/// <para>
	/// <b>The catalogue it reads is the merged one.</b> Layering across files belongs to
	/// <see cref="KingdomMergeRules"/>: by the time a list reaches <see cref="Validate"/>, every
	/// file that named a key has been folded into one entry. That is what makes the whole-file
	/// checks worth having, because the contradictions that survive are the ones no single file
	/// contains &mdash; a footprint one mod declared standing on a plot a second mod shrank, an
	/// improvement ring whose last link is closed by a third. Every finding about a design more
	/// than one file declares says so, so the author reading the log knows to look past their own.
	/// </para>
	/// <para>
	/// <b>What this file never does.</b> Reject an entry. Every check returns a finding for the
	/// log, because a design that is wrong about itself should be visible and still buildable, and
	/// because a third-party file must never be able to delete the base catalogue by mis-spelling
	/// one attribute.
	/// </para>
	/// </summary>
	public static partial class KingdomCatalogueRules
	{
		/// <summary>Separator between entries in a <c>Carries</c> list.</summary>
		public const char ListSeparator = ',';

		/// <summary>Separator between a support and its amount: <c>water:8</c>.</summary>
		public const char AmountSeparator = ':';

		// --- Stage, against the plot the design stands on ----------------------------------------

		/// <summary>
		/// The stage a design is actually reachable at: the later of what its own <c>MinStage</c>
		/// asks for and what its plot tier requires (<c>KingdomPlotRules.StageForSize</c>). A
		/// design may gate itself above its tier &mdash; that is an author saying "not yet, even
		/// then" &mdash; but it can never gate itself below one, because there is no ground to put
		/// it on.
		/// </summary>
		public static GrowthStage EffectiveMinStage(GrowthStage Authored, KingdomPlotRules.PlotSize Plot)
		{
			GrowthStage plotStage = KingdomPlotRules.StageForSize(Plot);
			return (Authored > plotStage) ? Authored : plotStage;
		}

		// --- Supports and the equilibrium level -------------------------------------------------

		public const string SupportWater = "water";

		public const string SupportFood = "food";

		public const string SupportRoof = "roof";

		/// <summary>Prosperity made physically legible by a productive great work. Wealth lifts;
		/// it never binds, drives threats, or changes the equilibrium arithmetic.</summary>
		public const string SupportWealth = "wealth";

		/// <summary>
		/// Frozen catalogue/API vocabulary. Food remains classified here so existing XML, extension
		/// validation, and callers keep their shape; live population equilibrium uses
		/// <see cref="PopulationBindingSupports"/> instead.
		/// </summary>
		public static readonly string[] BindingSupports = new string[3] { SupportWater, SupportFood, SupportRoof };

		/// <summary>The two live population constraints. Food is deliberately absent: missing crops,
		/// ingredients, or food works may withhold a positive act but never shrink a settlement.</summary>
		public static readonly string[] PopulationBindingSupports = new string[2] { SupportWater, SupportRoof };

		/// <summary>
		/// What a smithy, a shrine, a scriptorium, a barracks, a bathhouse, and a prosperous great
		/// work give: not a reason
		/// one more person can live here, but a reason one more person wants to. Lifting supports
		/// are summed and then capped against the binding level by <see cref="LiftCapPercent"/>.
		/// </summary>
		public static readonly string[] LiftingSupports = new string[6]
		{
			"craft", "spirit", "learning", "order", "luxury", SupportWealth
		};

		/// <summary>
		/// How far past its binding supports a settlement's comfort, faith, learning, order, and
		/// luxury can carry it, as a percentage of the binding level. Half: a well-loved town holds
		/// half again the people its water and roofs alone would, and not one more.
		/// </summary>
		public const int LiftCapPercent = 50;

		/// <summary>
		/// The level below which nothing sinks. A camp carries itself &mdash; four people, a fire,
		/// and whatever they walked in with &mdash; so the floor is the smallest stage's own
		/// equilibrium rather than a special case bolted under the arithmetic.
		/// </summary>
		public const int FloorLevel = 4;

		/// <summary>Whether a support kind belongs to the frozen three-kind catalogue vocabulary.
		/// This is an authoring/compatibility classification, not the live population calculation.</summary>
		public static bool IsBindingSupport(string Kind)
		{
			return Contains(BindingSupports, Fold(Kind));
		}

		/// <summary>Whether a support kind is one this file names at all. A kind it does not know
		/// is not an error &mdash; it lifts, because a third party inventing a new binding good
		/// would make every catalogue that predates it unbuildable.</summary>
		public static bool IsKnownSupport(string Kind)
		{
			string kind = Fold(Kind);
			return Contains(BindingSupports, kind) || Contains(LiftingSupports, kind);
		}

		/// <summary>
		/// Frozen five-argument equilibrium, retained for source and wire compatibility. It takes
		/// the least of the historical three support arguments, adds bounded lift, and never falls
		/// below <see cref="FloorLevel"/>. Live population calls
		/// <see cref="PopulationEquilibrium"/>, where food is deliberately non-binding.
		/// </summary>
		/// <param name="Water">Summed <c>water</c> contribution of every finished work.</param>
		/// <param name="Food">Summed <c>food</c> contribution.</param>
		/// <param name="Roof">Summed <c>roof</c> contribution.</param>
		/// <param name="Lift">Summed contribution of every lifting support together, already
		/// scoped to what each work actually reaches (Addendum 6).</param>
		/// <param name="Shade">What the settlement's named notable is worth to it &mdash; met
		/// tastes, leader traits, and met <c>Prefers</c> together, from
		/// <c>KingdomCeremonyRules.NotableShade</c>. Zero for a settlement that has named nobody,
		/// and a negative reads as zero rather than as a tax.
		/// <para>
		/// Deliberately summed into the lift rather than added after it: a shade is a reason one
		/// more person WANTS to live here, exactly as a shrine is, so it is bound by the same
		/// <see cref="LiftCapPercent"/> and can never let a settlement outrun its own water. The
		/// two are separate arguments rather than one because only one of them is a building.
		/// </para></param>
		public static int Equilibrium(int Water, int Food, int Roof, int Lift, int Shade)
		{
			int least = Least(Water, Food, Roof);
			if (least < 0)
			{
				least = 0;
			}
			// Each half is floored before they meet, so neither can eat the other: an unmet taste
			// is never a penalty (the brief rejects the penalty half outright), and a shade cannot
			// cancel a shrine that is standing.
			long lift = ((Lift < 0) ? 0L : Lift) + ((Shade < 0) ? 0L : Shade);
			long cap = (long)least * LiftCapPercent / 100L;
			if (lift > cap)
			{
				lift = cap;
			}
			long level = (long)least + lift;
			if (level >= int.MaxValue)
			{
				return int.MaxValue;
			}
			return (level < FloorLevel) ? FloorLevel : (int)level;
		}

		/// <summary>
		/// Live sustainable population. Reuses the frozen five-argument arithmetic with food held
		/// non-binding, preserving its floor, lift cap, overflow behavior, and public compatibility.
		/// </summary>
		public static int PopulationEquilibrium(int Water, int Roof, int Lift, int Shade)
		{
			return Equilibrium(Water, int.MaxValue, Roof, Lift, Shade);
		}

		/// <summary>The live population constraint, with water winning a tie. Food can never be
		/// returned and therefore can never author subsidence or departure prose.</summary>
		public static string PopulationBindingSupport(int Water, int Roof)
		{
			return (Water <= Roof) ? SupportWater : SupportRoof;
		}

		/// <summary>
		/// Frozen three-kind diagnostic retained for compatible catalogue consumers. Ties go to
		/// water, then food, then roofs. Live population and subsidence call
		/// <see cref="PopulationBindingSupport"/> and therefore never return food.
		/// <para>
		/// This exists so older integrations can continue to interpret their own three-axis model;
		/// it is not a live settlement-pressure authority.
		/// </para>
		/// </summary>
		/// <returns>One of <see cref="BindingSupports"/>. Never null.</returns>
		public static string BindingSupport(int Water, int Food, int Roof)
		{
			int least = Least(Water, Food, Roof);
			if (Water == least)
			{
				return SupportWater;
			}
			return (Food == least) ? SupportFood : SupportRoof;
		}

		/// <summary>One line for the ledger, naming the level and what holds it there.</summary>
		public static string LimitLine(string Support, int Level)
		{
			string level = Level.ToString();
			switch (Fold(Support))
			{
			case SupportWater:
				return "The settlement carries " + level + ", and it is the water that holds it there.";
			case SupportFood:
				return "The settlement carries " + level + ", and it is the harvest that holds it there.";
			case SupportRoof:
				return "The settlement carries " + level + ". There are only so many roofs.";
			default:
				return "The settlement carries " + level + ".";
			}
		}

	}
}
