using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCatalogueRules
	{
		// --- The summation ------------------------------------------------------------------------
		//
		// The step between "the arithmetic exists" and "something uses it": one settlement's
		// standing works folded into the four figures Equilibrium takes. The arithmetic above is
		// frozen and nothing here touches it; this only ever produces its arguments.

		/// <summary>
		/// What a settlement's finished works carry between them, in the four figures
		/// <see cref="Equilibrium"/> takes. A plain sum and nothing more &mdash; the stage the
		/// water is denominated against belongs to the caller
		/// (<c>KingdomSubsidenceRules.SupportedLevel</c>), because this file knows what a design
		/// declares and not what the settlement has become.
		/// </summary>
		public struct SupportTally
		{
			/// <summary>Summed <c>water</c>, in drams a day sustained &mdash; which is settlers
			/// only at camp rates. See <see cref="KingdomCatalogueRules"/>' denomination note.</summary>
			public int Water;

			/// <summary>Summed <c>food</c>, in settlers fed.</summary>
			public int Food;

			/// <summary>Summed <c>roof</c>, in settlers housed.</summary>
			public int Roof;

			/// <summary>Every lifting support summed together &mdash; the <c>Lift</c> argument.</summary>
			public int Lift;

			/// <summary>How many finished works were folded in. Not an argument to anything; it is
			/// what lets a caller tell "nothing stands here" from "everything here carries
			/// nothing", which are different sentences to a founder (STANDARDS 7b).</summary>
			public int Works;
		}

		/// <summary>
		/// How much of a declared amount a work running at <paramref name="EffectivenessPercent"/>
		/// actually carries. Honest flooring: a field at a tenth of its crew feeds nobody, and says
		/// zero rather than one.
		/// <para>
		/// Deliberately NOT <c>KingdomReachRules.Scaled</c>, which floors a positive contribution at
		/// one. That floor is right for a lift &mdash; a barely-tended shrine still shades the
		/// ground it stands on &mdash; and wrong for a binding support, where one settler fed is a
		/// claim about a person who eats.
		/// </para>
		/// </summary>
		/// <param name="Amount">The design's declared figure.</param>
		/// <param name="EffectivenessPercent">0 to 100. At or above 100 the whole amount carries;
		/// at or below zero, none of it does.</param>
		public static int Carried(int Amount, int EffectivenessPercent)
		{
			if (Amount <= 0 || EffectivenessPercent <= 0)
			{
				return 0;
			}
			if (EffectivenessPercent >= 100)
			{
				return Amount;
			}
			long carried = (long)Amount * EffectivenessPercent / 100L;
			return (carried >= int.MaxValue) ? int.MaxValue : (int)carried;
		}

		/// <summary>Adds two non-negative counters without allowing malformed negative input or
		/// a large catalogue to wrap the answer below zero.</summary>
		internal static int SaturatingCounterAdd(int Left, int Right)
		{
			long left = (Left < 0) ? 0L : Left;
			long right = (Right < 0) ? 0L : Right;
			long total = left + right;
			return (total >= int.MaxValue) ? int.MaxValue : (int)total;
		}

		/// <summary>Subtracts a non-negative counter without allowing malformed input or two
		/// deductions to wrap a spent counter back above zero.</summary>
		internal static int SaturatingCounterSubtract(int Left, int Right)
		{
			long left = (Left < 0) ? 0L : Left;
			long right = (Right < 0) ? 0L : Right;
			long remainder = left - right;
			return (remainder <= 0L) ? 0 : (int)remainder;
		}

		/// <summary>Multiplies two non-negative counters through a widened intermediate and
		/// saturates the public counter at its representable ceiling.</summary>
		internal static int SaturatingCounterMultiply(int Left, int Right)
		{
			if (Left <= 0 || Right <= 0)
			{
				return 0;
			}
			long product = (long)Left * Right;
			return (product >= int.MaxValue) ? int.MaxValue : (int)product;
		}

		private static SupportTally Bounded(SupportTally Tally)
		{
			Tally.Water = SaturatingCounterAdd(Tally.Water, 0);
			Tally.Food = SaturatingCounterAdd(Tally.Food, 0);
			Tally.Roof = SaturatingCounterAdd(Tally.Roof, 0);
			Tally.Lift = SaturatingCounterAdd(Tally.Lift, 0);
			Tally.Works = SaturatingCounterAdd(Tally.Works, 0);
			return Tally;
		}

		/// <summary>
		/// Folds one finished work's parsed <c>Carries</c> into a running tally, scaled by how well
		/// that work is running.
		/// <para>
		/// Copy-on-write: the tally handed in is not touched, and the folded one is returned. A
		/// caller that walks a zone runs this once per standing work and keeps the answer.
		/// </para>
		/// </summary>
		/// <param name="Running">The tally so far.</param>
		/// <param name="Carries">One design's parsed list, from <see cref="TryParseTally"/>. Null
		/// or empty folds in nothing but still counts as a work &mdash; a palisade carries no
		/// support and is still something standing.</param>
		/// <param name="EffectivenessPercent">What this work is running at, from its caller's own
		/// reading of crew and condition (<c>KingdomWearRules.WorkEffectiveness</c>). A design that
		/// asks for no crew is handed its CONDITION rather than a flat 100 &mdash; a cistern holds
		/// water whoever is home, and a holed cistern holds less of it (Addendum 10(b)).</param>
		public static SupportTally FoldWork(SupportTally Running, List<KindAmount> Carries, int EffectivenessPercent)
		{
			SupportTally folded = FoldShade(Running, Carries, EffectivenessPercent);
			folded.Works = SaturatingCounterAdd(folded.Works, 1);
			return folded;
		}

		/// <summary>
		/// The same fold, for a contribution that is <b>not a work</b>: a household's yard trade
		/// (<c>KingdomYardRules.YardWorkSpec.Shades</c>), which stands in a plot somebody else
		/// already built and must not be counted as a second thing standing.
		/// <para>
		/// Copy-on-write, exactly as <see cref="FoldWork"/> is. The only difference between the
		/// two is <see cref="SupportTally.Works"/>, which is what lets a founder be told "nothing
		/// stands here" apart from "everything here carries nothing".
		/// </para>
		/// </summary>
		/// <param name="Running">The tally so far.</param>
		/// <param name="Shades">A parsed <c>support:amount</c> list. Null or empty folds in
		/// nothing.</param>
		/// <param name="EffectivenessPercent">What the household is working at. Hand a yard trade
		/// the condition of the house it belongs to, so a ruined house's sideline is worth what
		/// the house is (Addendum 10(b)).</param>
		public static SupportTally FoldShade(SupportTally Running, List<KindAmount> Shades, int EffectivenessPercent)
		{
			SupportTally folded = Bounded(Running);
			if (Shades == null)
			{
				return folded;
			}
			for (int i = 0; i < Shades.Count; i++)
			{
				int amount = Carried(Shades[i].Amount, EffectivenessPercent);
				if (amount <= 0)
				{
					continue;
				}
				switch (Fold(Shades[i].Kind))
				{
				case SupportWater:
					folded.Water = SaturatingCounterAdd(folded.Water, amount);
					break;
				case SupportFood:
					folded.Food = SaturatingCounterAdd(folded.Food, amount);
					break;
				case SupportRoof:
					folded.Roof = SaturatingCounterAdd(folded.Roof, amount);
					break;
				default:
					// Everything else lifts, including a kind this build has never heard of -
					// IsKnownSupport's own rule, applied to the sum rather than to the validator.
					folded.Lift = SaturatingCounterAdd(folded.Lift, amount);
					break;
				}
			}
			return folded;
		}

		/// <summary>
		/// The whole summation over a settlement's finished works, for a caller that already has
		/// every design's raw <c>Carries</c> attribute in hand. Every work counts at full, which is
		/// what an engine-free caller can honestly say; the zone-walking caller folds each work at
		/// its own effectiveness instead.
		/// </summary>
		/// <param name="CarriesAttributes">One raw attribute per standing work. Null reads as no
		/// works at all. A malformed entry contributes whatever parsed before the bad pair, exactly
		/// as <see cref="TryParseTally"/> hands it back.</param>
		public static SupportTally SumCarries(IEnumerable<string> CarriesAttributes)
		{
			SupportTally tally = default(SupportTally);
			if (CarriesAttributes == null)
			{
				return tally;
			}
			foreach (string attribute in CarriesAttributes)
			{
				List<KindAmount> carries;
				TryParseTally(attribute, out carries, out _);
				tally = FoldWork(tally, carries, 100);
			}
			return tally;
		}

	}
}
