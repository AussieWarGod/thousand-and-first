using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomProcedureRules
	{
		// --- The preservation chain (DIVERSITY §3.5) ------------------------------------------

		/// <summary>
		/// Preserved parts one raw part binds into, on vanilla's own arithmetic and nothing else.
		/// <para>
		/// <b>The design note this corrects, and it is worth stating plainly.</b> &sect;3.5 records
		/// the figure as "<c>Result x Number x Count</c>", which reads as a product of three. It is
		/// not. <c>Campfire.PerformPreserve</c> (<c>D/XRL/World/Parts/Campfire.cs:512</c>) seeds a
		/// count of one (<c>:543</c>), OVERWRITES it with <c>PreparedCookingIngredient.charges</c>
		/// if that part is present (<c>:544-547</c>), overwrites it AGAIN with
		/// <c>PreservableItem.Number</c> if THAT part is present (<c>:548-551</c> &mdash; so
		/// <c>Number</c> wins outright rather than multiplying), and only then multiplies by the
		/// stack (<c>:552</c> <c>num3 *= go.Count</c>). <c>Result</c> is the BLUEPRINT handed over
		/// (<c>:554-557</c>), not a factor. The vat-house issues exactly this, because inventing our
		/// own multiplier would be inventing a second economy on top of one that already works.
		/// </para>
		/// <para>
		/// Vanilla's shipped calibration is the sanity check: bear meat gives 5, a dawnglider tail
		/// 10, a psychal gland 5 &mdash; so a carcass yielding three to eight preserved parts is
		/// vanilla-shaped, and a Class III limb consuming a whole creature's yield reads correctly
		/// as one creature, one limb.
		/// </para>
		/// </summary>
		/// <param name="Number">The source's <c>PreservableItem.Number</c>. Zero or less reads as
		/// one, which is what a part carrying no number honestly is.</param>
		/// <param name="Count">The stack size going in.</param>
		public static int PreservedYield(int Number, int Count)
		{
			if (Count <= 0)
			{
				return 0;
			}
			long yield = (long)((Number > 0) ? Number : 1) * Count;
			return (yield > int.MaxValue) ? int.MaxValue : (int)yield;
		}

		/// <summary>
		/// What the vat-house's own labour turns one raw part into over a stretch of days.
		/// <para>
		/// <b>There is no rot anywhere in this and there never will be.</b> Vanilla has none &mdash;
		/// <c>PreservableItem</c> is two fields and no behaviour at all
		/// (<c>D/XRL/World/Parts/PreservableItem.cs:8,10</c>) &mdash; and a decay timer would be a
		/// rate that ran on time alone, which Addendum 8 clause 2 forbids outright. What gates the
		/// chain is LABOUR: a staffed work, real hands, real world-days. A vat-house with nobody in
		/// it keeps what it holds forever and preserves nothing new, which is the honest shape.
		/// </para>
		/// </summary>
		/// <param name="ElapsedTicks">Ticks since the vat last settled.</param>
		/// <param name="CrewEffectiveness">Hands and capability together, 0 to 100.</param>
		/// <param name="WearEffectiveness">What the building's condition leaves of it, 0 to 100.</param>
		/// <returns>Labour ticks actually worked. Zero when any term is zero, by arithmetic rather
		/// than by a special case.</returns>
		public static int VatWorked(long ElapsedTicks, int CrewEffectiveness, int WearEffectiveness)
		{
			return VatWorked(ElapsedTicks, CrewEffectiveness, WearEffectiveness,
				KingdomIdentityAffinityRules.NeutralPercent);
		}

		/// <summary>The same paid labour with Addendum 17's identity affinity as its own
		/// multiplicative factor. It never supplies hands: zero crew remains zero.</summary>
		public static int VatWorked(long ElapsedTicks, int CrewEffectiveness,
			int WearEffectiveness, int IdentityAffinity)
		{
			if (ElapsedTicks <= 0L || CrewEffectiveness <= 0 || WearEffectiveness <= 0)
			{
				return 0;
			}
			long rate = (long)Clamp(CrewEffectiveness, 0, 100)
				* Clamp(WearEffectiveness, 0, 100)
				* KingdomIdentityAffinityRules.Clamp(IdentityAffinity) / 10000L;
			long worked = KingdomRules.LabouredTicks(ElapsedTicks, (int)rate);
			return (worked > int.MaxValue) ? int.MaxValue : (int)worked;
		}

		/// <summary>Days of the vat's labour one raw part wants before it is kept. One, and it is
		/// deliberately the smallest number that is still a day: the vat-house is a gate, not a
		/// tax, and it has to be worth building for the trade good alone.</summary>
		public const int PreserveDays = 1;

		/// <summary>A procedure's authored staff-days, in ticks. Staff-days at the settlement's own
		/// day, exactly as the research bench counts its effort.</summary>
		public static int StaffDayTicks(int StaffDays)
		{
			if (StaffDays <= 0)
			{
				return (int)KingdomRules.TicksPerDay;
			}
			long ticks = (long)StaffDays * KingdomRules.TicksPerDay;
			return (ticks > int.MaxValue) ? int.MaxValue : (int)ticks;
		}

		// --- The mutation cap (DIVERSITY §3.4 source table; §3.9 risk 3) -----------------------

		/// <summary>The floor a granted mutation lands at.</summary>
		public const int MinMutationLevel = 1;

		/// <summary>
		/// The ceiling a granted mutation lands at, and it is <b>never the source's own level</b>.
		/// The single most load-bearing balance number in the wave: Playable Slime's own to-do file
		/// records that a free repeatable absorb verb <i>"makes all fighting styles ...
		/// unnecessary"</i>, and granting a level-10 creature's mutation at level 10 is that failure
		/// exactly (DIVERSITY &sect;3.0a, &sect;3.9 risk 3).
		/// </summary>
		public const int MaxMutationLevel = 3;

		/// <summary>What a granted mutation is actually worth. Clamped to
		/// <see cref="MinMutationLevel"/>..<see cref="MaxMutationLevel"/> whatever the source
		/// carried, and a source with no level at all still grants a real mutation rather than
		/// nothing.</summary>
		public static int GrantedMutationLevel(int SourceLevel)
		{
			return Clamp(SourceLevel, MinMutationLevel, MaxMutationLevel);
		}

		// --- Once, ever (DIVERSITY §3.7; Addendum 22 C11) --------------------------------------

		/// <summary>Between named procedures in the founder's own record of what has been done to
		/// them. The same shape the realm's arch register keeps, and for the same reason: it is one
		/// string that has to come back out saying exactly what went in.</summary>
		public const char LatchSeparator = '|';

		/// <summary>Whether this founder has already had a named procedure. Folded, because a key
		/// is a key whatever case the file wrote it in.</summary>
		public static bool Latched(string Latch, string Key)
		{
			if (string.IsNullOrEmpty(Latch) || string.IsNullOrEmpty(Key))
			{
				return false;
			}
			string wanted = Fold(Key);
			string[] done = Latch.Split(LatchSeparator);
			for (int i = 0; i < done.Length; i++)
			{
				if (Fold(done[i]) == wanted)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The founder's record with one more named procedure in it. Copy-on-write and idempotent:
		/// latching a thing twice is the same string, so nothing anywhere has to remember whether it
		/// already asked.
		/// </summary>
		/// <returns>The latch afterwards, or the latch unchanged when the key is unwritable or
		/// already held.</returns>
		public static string Latch(string Latch, string Key)
		{
			string folded = Fold(Key);
			if (folded == null || folded.IndexOf(LatchSeparator) >= 0 || Latched(Latch, folded))
			{
				return Latch ?? "";
			}
			return string.IsNullOrEmpty(Latch) ? folded : (Latch + LatchSeparator + folded);
		}

	}
}
