using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCreedRules
	{

		/// <summary>
		/// How badly two creeds are at odds, from the engine's own faction feelings.
		/// <para>
		/// The worse of the two directions wins, because a grudge only one side holds is still a
		/// grudge, and in this data most grudges are one-sided: over half of all faction pairs in
		/// <c>Factions.xml</c> disagree about each other. The Templar hold the Girsh at -100 while
		/// the Girsh file no opinion of the Templar at all and fall back on a general -50; the
		/// Barathrumites hold the Templar at -100 while the Templar return only -50. A templar city
		/// beside either is not at peace on the strength of the gentler half.
		/// </para>
		/// </summary>
		/// <param name="FeelingAboutTheOther">How the first city's creed feels about the second's.</param>
		/// <param name="TheirFeelingBack">How the second city's creed feels about the first's.</param>
		/// <param name="SameCreed">True when both cities hold the same creed. Short-circuits to
		/// nothing rather than relying on the engine's own self-feeling, which is a hardcoded +100
		/// and would read here as warmth the cities have not earned.</param>
		/// <returns>0 to 100. Zero for creeds that get on, are indifferent, or that the caller
		/// could not resolve at all.</returns>
		public static int Hostility(int FeelingAboutTheOther, int TheirFeelingBack, bool SameCreed)
		{
			if (SameCreed)
			{
				return 0;
			}
			int worst = (FeelingAboutTheOther < TheirFeelingBack) ? FeelingAboutTheOther : TheirFeelingBack;
			if (worst >= 0)
			{
				return 0;
			}
			return (worst <= -100) ? 100 : -worst;
		}

		/// <summary>
		/// Points of dissent one day of the founder's attention adds, at a given hostility. Integer
		/// division on purpose: everything below <see cref="HostilityPerDissentPoint"/> buys
		/// nothing at all, so ordinary dislike between neighbours never becomes a countdown.
		/// </summary>
		public static int DissentPerDay(int Hostility)
		{
			return (Hostility <= 0) ? 0 : (Hostility / HostilityPerDissentPoint);
		}

		/// <summary>
		/// Dissent after a stretch of days.
		/// <para>
		/// <paramref name="Days"/> is UNCAPPED (Addendum 8 clause 1): two cities that cannot
		/// stand each other go on not standing each other whether or not the founder is watching.
		/// What bounds the outcome is no longer a forgiveness ceiling on the clock but
		/// <see cref="DissentBreaking"/> itself &mdash; the value clamps there, records a brink,
		/// and the unhappier city then waits <see cref="SecessionWindowDays"/> WORLD-DAYS
		/// for the founder to do something about it. Uncapping this without that window would have
		/// made an absence lose a city faster than presence does, which is clause 3 exactly
		/// inverted; the two moved together, in one package, which is why this doc no longer
		/// promises what it used to.
		/// </para>
		/// </summary>
		/// <param name="Dissent">Dissent standing now.</param>
		/// <param name="Hostility">From <see cref="Hostility"/>.</param>
		/// <param name="Days">Days to resolve. Non-positive changes nothing.</param>
		/// <returns>The new dissent, clamped to <c>[0, DissentBreaking]</c>. Arithmetic in long,
		/// because an uncapped day count times four points a day is a number an int cannot always
		/// hold, and a dissent that wrapped negative would read as concord.</returns>
		public static int AccrueDissent(int Dissent, int Hostility, int Days)
		{
			if (Days <= 0)
			{
				return Clamp(Dissent);
			}
			long accrued = (long)Dissent + (long)DissentPerDay(Hostility) * Days;
			if (accrued >= DissentBreaking)
			{
				return DissentBreaking;
			}
			return Clamp((int)accrued);
		}

		/// <summary>Dissent after something the founder did about it. Clamped, so a lever can
		/// never drive dissent below nothing or a shock above the breaking point.</summary>
		/// <param name="Dissent">Dissent standing now.</param>
		/// <param name="Delta">Signed change; negative eases.</param>
		public static int ApplyDissent(int Dissent, int Delta)
		{
			return Clamp(Dissent + Delta);
		}

		/// <summary>How the realm reads its own dissent. Boundaries are the named constants, so
		/// the tier the Charter shows is the tier the secession fires on.</summary>
		public static CityTemper ClassifyTemper(int Dissent)
		{
			if (Dissent >= DissentBreaking)
			{
				return CityTemper.Secession;
			}
			if (Dissent >= DissentRupture)
			{
				return CityTemper.Rupture;
			}
			if (Dissent >= DissentQuarrel)
			{
				return CityTemper.Quarrel;
			}
			if (Dissent >= DissentMuttering)
			{
				return CityTemper.Muttering;
			}
			return CityTemper.Concord;
		}

		/// <summary>
		/// The temper the realm remembers having said out loud, after seeing
		/// <paramref name="Current"/>. The hysteresis: a worsening speaks once, jitter back and
		/// forth across one threshold says nothing further, and only easing the thing all the way
		/// back to <see cref="CityTemper.Concord"/> re-arms the ladder. Mirrors
		/// <c>KingdomExileRules.RememberedRegard</c>, deliberately.
		/// </summary>
		public static CityTemper RememberedTemper(CityTemper Current, CityTemper Spoken)
		{
			if (Current == CityTemper.Concord)
			{
				return CityTemper.Concord;
			}
			return (Current > Spoken) ? Current : Spoken;
		}

		/// <summary>Whether this pass has something new to say about the two cities. False for a
		/// realm in concord and for a temper already spoken of.</summary>
		public static bool ShouldSpeak(CityTemper Current, CityTemper Spoken)
		{
			return Current > CityTemper.Concord && Current > Spoken;
		}

		/// <summary>
		/// Drams of fresh water a rite of shared water asks for at a given temper. Nothing at
		/// concord, because there is nothing to mend and the founder should not be sold a cure for
		/// it.
		/// </summary>
		public static int RiteCost(CityTemper Temper)
		{
			switch (Temper)
			{
			case CityTemper.Muttering:
				return 20;
			case CityTemper.Quarrel:
				return 40;
			case CityTemper.Rupture:
			case CityTemper.Secession:
				return 80;
			default:
				return 0;
			}
		}

		/// <summary>
		/// Dissent a rite eases at a given temper. Rises with the cost but not as fast, so a
		/// founder who lets it run pays more per point — and at the fault line's four points a day
		/// a rite every <see cref="RiteCooldownDays"/> days still wins ground, slowly, at a price.
		/// </summary>
		public static int RiteEase(CityTemper Temper)
		{
			switch (Temper)
			{
			case CityTemper.Muttering:
				return 15;
			case CityTemper.Quarrel:
				return 20;
			case CityTemper.Rupture:
			case CityTemper.Secession:
				return 25;
			default:
				return 0;
			}
		}

		/// <summary>Whether the rite may be held again yet.</summary>
		/// <param name="LastRiteTick">Tick of the last rite, or 0 if never.</param>
		/// <param name="TimeTicks">Now.</param>
		public static bool RiteReady(long LastRiteTick, long TimeTicks)
		{
			return LastRiteTick <= 0 || TimeTicks - LastRiteTick >= RiteCooldownDays * KingdomRules.TicksPerDay;
		}

		/// <summary>
		/// Whether the unhappier city leaves the realm.
		/// </summary>
		/// <param name="Cities">Cities the realm holds. Below two there is nobody to fall out
		/// with, which is how a founder who only ever holds one city never meets any of this.</param>
		/// <param name="Hostility">From <see cref="Hostility"/>. Zero means the creeds do not
		/// clash, whatever dissent was accrued before they stopped clashing.</param>
		/// <param name="Dissent">Dissent standing now.</param>
		/// <param name="Forced">True for the debug path, which skips the dissent requirement and
		/// nothing else.</param>
		public static SecessionVerdict JudgeSecession(int Cities, int Hostility, int Dissent, bool Forced)
		{
			if (Cities < 2)
			{
				return SecessionVerdict.OneCity;
			}
			if (Forced)
			{
				return SecessionVerdict.Warranted;
			}
			if (Hostility <= 0)
			{
				return SecessionVerdict.NoClash;
			}
			return (Dissent >= DissentBreaking) ? SecessionVerdict.Warranted : SecessionVerdict.DissentHolds;
		}

		/// <summary>
		/// Which of the two cities walks: the one whose creed holds the other's in worse regard.
		/// <para>
		/// On a tie of feeling the smaller city goes, because it is the minority that leaves. On a
		/// tie of both, the city the founder is not standing in goes — arbitrary, but fixed, so the
		/// same realm always breaks the same way.
		/// </para>
		/// </summary>
		/// <param name="SeatFeelingAboutAway">How the seated city's creed feels about the other's.</param>
		/// <param name="AwayFeelingAboutSeat">How the other city's creed feels about the seat's.</param>
		/// <param name="SeatPopulation">Residents of the seated city.</param>
		/// <param name="AwayPopulation">Residents of the other city.</param>
		/// <returns>True if the city the founder is not standing in is the one that leaves.</returns>
		public static bool AwayIsTheLeaver(int SeatFeelingAboutAway, int AwayFeelingAboutSeat, int SeatPopulation, int AwayPopulation)
		{
			if (AwayFeelingAboutSeat != SeatFeelingAboutAway)
			{
				return AwayFeelingAboutSeat < SeatFeelingAboutAway;
			}
			if (AwayPopulation != SeatPopulation)
			{
				return AwayPopulation < SeatPopulation;
			}
			return true;
		}

		/// <summary>
		/// Whether a seceded city may be taken back.
		/// <para>
		/// The cause must be gone before the city is: a founder who walks back with the same clash
		/// still live is told exactly that. Winning it back means having actually changed something
		/// — declared a creed and let the rolls drift, or watched one city's creed lapse to mixed —
		/// not waiting the quarrel out.
		/// </para>
		/// </summary>
		/// <param name="Seceded">Whether a city left at all.</param>
		/// <param name="Cities">Cities the realm holds now. A founder who founded another second
		/// city in the meantime has no room, and shut that door themselves.</param>
		/// <param name="OnTheirGround">Whether the founder is standing on the seceded city's own
		/// ground. It is asked where it can hear you.</param>
		/// <param name="Hostility">Hostility between the seceded city's creed and the realm's
		/// remaining one, now.</param>
		/// <param name="Standing">The realm's standing with the seceded city's creed's faction.</param>
		public static RejoinVerdict JudgeRejoin(bool Seceded, int Cities, bool OnTheirGround, int Hostility, int Standing)
		{
			if (!Seceded)
			{
				return RejoinVerdict.NothingSeceded;
			}
			if (Cities >= KingdomSettlement.MaxSettlements)
			{
				return RejoinVerdict.RealmIsFull;
			}
			if (!OnTheirGround)
			{
				return RejoinVerdict.NotOnTheirGround;
			}
			if (DissentPerDay(Hostility) > 0)
			{
				return RejoinVerdict.ClashStillLive;
			}
			return (Standing <= KingdomExileRules.RegardDisliked) ? RejoinVerdict.StandingTooLow : RejoinVerdict.Allowed;
		}
	}
}
