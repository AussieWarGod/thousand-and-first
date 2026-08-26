using System;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomHappeningRules
	{
		// ==================================================================================
		// Weddings — cohabitation rows plus creed compatibility
		// ==================================================================================

		/// <summary>
		/// How long two people must have shared a roof before the city expects a wedding of them.
		/// <para>
		/// <c>KingdomBrinkRules.CreedBrinkWindowDays</c>, borrowed rather than invented: it is
		/// already the number of world-days this mod treats as long enough for two people's
		/// feelings about each other to be settled rather than fresh, and a second number meaning
		/// the same thing is a number that will drift.
		/// </para>
		/// </summary>
		internal const int CourtshipDays = KingdomBrinkRules.CreedBrinkWindowDays;

		/// <summary>
		/// The chance, per eligible pair per reckoning, that this is the reckoning they marry on.
		/// <para>
		/// One draw per PAIR (&sect;0.0(a): per happening, never per day), and low enough that a
		/// city does not marry itself off in a fortnight. A pair that does not marry this pass is
		/// eligible again next pass; nothing is remembered and nothing accumulates.
		/// </para>
		/// </summary>
		internal const int WeddingChancePercent = 20;

		/// <summary>
		/// The hostility at which two people will not stand each other under one roof, borrowed
		/// from the lodging vocabulary that already decides it
		/// (<c>KingdomLodgingRules.CreedRefusalHostilityFloor</c>). A pair the housing machinery
		/// would not have put together in the first place is not a pair the city marries.
		/// </summary>
		internal const int WeddingHostilityCeiling = KingdomLodgingRules.CreedRefusalHostilityFloor;

		/// <summary>
		/// What two settlers whose creeds the model cannot compare are worth.
		/// <para>
		/// <b>A row carries a creed CODE, and the code is one-way</b>
		/// (<c>KingdomCityRules.StableId</c> is FNV-1a and there is no inverse), so the model can
		/// prove two people hold with the same thing and can never prove two different things get
		/// on. Rather than invent a number for the pair, the model declines: this value is one
		/// past the ceiling, so an unprovable pair is simply not married. <b>The city does not
		/// marry on an assumption</b> &mdash; and it costs nothing, because a mixed household the
		/// lodging machinery DID put together still weds the moment they hold with the same thing
		/// or one of them holds with nothing.
		/// </para>
		/// </summary>
		internal const int UnknownCreedHostility = WeddingHostilityCeiling + 1;

		/// <summary>
		/// What the pair's creed codes are worth to a wedding.
		/// <para>
		/// Same code is one creed and no quarrel. A zero code is a settler who holds with nothing
		/// in particular (<c>StableId</c> answers zero for an empty string, which is what a
		/// settler with no <c>KingdomCreed</c> property has), and a person who holds with nothing
		/// has nothing to hold against anybody. Everything else is
		/// <see cref="UnknownCreedHostility"/>.
		/// </para>
		/// </summary>
		internal static int CreedHostility(int creedCodeA, int creedCodeB)
		{
			if (creedCodeA == creedCodeB || creedCodeA == 0 || creedCodeB == 0)
			{
				return 0;
			}
			return UnknownCreedHostility;
		}

		/// <summary>
		/// Whether these two rows are a wedding waiting for a draw.
		/// <para>
		/// <b>Every clause is a row the model already keeps.</b> They are both on the roll and
		/// standing here; they are two different people; they share a home work id, which is the
		/// model's own record that the lodging machinery already judged them able to live together
		/// (Addendum 4c's closeness ladder did the compatibility work, and re-deciding it here
		/// would be the parallel machinery Addendum 13 forbids); they have both been here long
		/// enough; and their creeds do not hold it against them.
		/// </para>
		/// </summary>
		/// <param name="a">One resident row.</param>
		/// <param name="b">The other.</param>
		/// <param name="creedHostility">From <c>KingdomCreed.HostilityBetween</c> &mdash; the
		/// engine's own faction feelings, never a grudge table of ours.</param>
		/// <param name="nowTick">The tick being reckoned to.</param>
		internal static bool WeddingEligible(KingdomResidentRow a, KingdomResidentRow b, int creedHostility, long nowTick)
		{
			if (a.ResidentId == b.ResidentId || a.ResidentId <= 0 || b.ResidentId <= 0)
			{
				return false;
			}
			if (a.Standing != KingdomResidentStanding.Resident || b.Standing != KingdomResidentStanding.Resident)
			{
				return false;
			}
			if (a.HomeWorkId <= 0 || a.HomeWorkId != b.HomeWorkId)
			{
				return false;
			}
			if (creedHostility > WeddingHostilityCeiling)
			{
				return false;
			}
			long settled = (long)CourtshipDays * TicksPerDay;
			return (nowTick - a.ArrivedTick) >= settled && (nowTick - b.ArrivedTick) >= settled;
		}

		/// <summary>
		/// The order two people's ids go into the ring in: lower first, always.
		/// <para>
		/// The ring is what answers <i>"have we already said this"</i>
		/// (<see cref="AlreadyTold"/>), and a pair stored one way round and asked the other way
		/// round is a second wedding for the same two people. Row order is not stable &mdash; the
		/// roster is rebuilt from the ground every pass &mdash; so the pair has to be ordered by
		/// something that is, and an id is.
		/// </para>
		/// </summary>
		internal static void PairOrder(int idA, int idB, out int first, out int second)
		{
			bool ascending = idA < idB;
			first = ascending ? idA : idB;
			second = ascending ? idB : idA;
		}

		// ==================================================================================
		// Funerals — one telling, and it is the one the city already gives
		// ==================================================================================

		/// <summary>
		/// Whether this row's person is owed a funeral: dead, with a cause the memory machinery
		/// can name.
		/// <para>
		/// <b>The one-telling rule is structural, not a guard.</b> A death is announced exactly
		/// once, by <c>KingdomOffices.RecordDeath</c>, at the one moment the engine reports the
		/// body died &mdash; and W4 does not add a second announcement beside it. What W4 adds is
		/// the RITE inside that same telling: the clause below is folded into the line
		/// <c>RecordDeath</c> was already going to print, and the told-log row is written in the
		/// same call. There is no code path that can speak about a death twice, because there is
		/// only one path that speaks about a death at all.
		/// </para>
		/// </summary>
		internal static bool FuneralDue(KingdomResidentRow row)
		{
			int ordinal;
			return row.Standing == KingdomResidentStanding.Dead
				&& KingdomResidentRules.TryDeathCauseOrdinal(row.Cause, out ordinal);
		}

	}
}
