using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomSuccessionRules
	{
		// ==================================================================================
		// The mode
		// ==================================================================================

		/// <summary>
		/// The value <c>XRLGame.gameMode</c> carries in Kingdom Mode, and the id of the embark
		/// entry that sets it. Vanilla's own ladder is a string in a game state
		/// (<c>D/XRL/XRLGame.cs:245-254</c>), set from data at embark
		/// (<c>D/XRL/CharacterBuilds/Qud/QudGamemodeModule.cs:341-364</c>), so a mode is a data
		/// entry plus a system and Classic is untouched by construction.
		/// </summary>
		public const string ModeId = "Kingdom";

		/// <summary>
		/// The boolean game state the mode's embark entry also sets, and the surface everything in
		/// this mod actually reads.
		/// <para>
		/// Deliberately not the mode string. The mode string is vanilla's, shared with the score
		/// screen and the save browser, and a mod that keys behaviour to it is a mod that breaks the
		/// day somebody ships a second mode with the same word in it. A namespaced flag beside it
		/// costs one line of XML, composes with any future mode, and is the only thing a debug hook
		/// or a compatibility shim ever has to write.
		/// </para>
		/// </summary>
		public const string ModeFlagStateKey = "r_TAF_KingdomMode";

		/// <summary>Whether Kingdom Mode is in force, from the two surfaces that can say so.</summary>
		/// <param name="GameMode">The value of <c>XRLGame.gameMode</c>.</param>
		/// <param name="ModeFlag">The value of the <see cref="ModeFlagStateKey"/> boolean state.</param>
		public static bool ModeOn(string GameMode, bool ModeFlag)
		{
			return ModeFlag || string.Equals(GameMode, ModeId, StringComparison.Ordinal);
		}

		// ==================================================================================
		// The word on the road (Addendum 22 C8)
		// ==================================================================================

		/// <summary>
		/// Zone-steps the word covers in a day. Two, against the carry-sign's one
		/// (<c>KingdomGuestRules.CarrySignDaysPerZoneStep</c>): news travels at the pace of somebody
		/// carrying nothing, and a laden porter is the mod's own measure of somebody carrying
		/// something. Derived rather than invented, and the only place the ratio is stated.
		/// </summary>
		public const int WordZoneStepsPerDay = 2;

		/// <summary>
		/// The longest the word can be on the road, in world-days, however far away the founder
		/// fell. Not a cap on a cost &mdash; STANDARDS &sect;8 forbids those &mdash; but the floor
		/// under a rumour: past the reach of any road the realm keeps, the news arrives the way news
		/// about far countries always arrives, and that takes about a fortnight from anywhere.
		/// </summary>
		public const int RumourDays = 14;

		/// <summary>Days in one of Qud's own months, which is the grain the accession regard counts
		/// tenure in. Vanilla's calendar month is 36,000 ticks against
		/// <c>KingdomRules.TicksPerDay</c>'s 1,200.</summary>
		public const int DaysPerMonth = 30;

		/// <summary>
		/// Zone-steps between where the founder fell and the seat, on the mod's own three-axis
		/// distance vocabulary &mdash; with the one correction rock demands: a stratum is not a
		/// step, it is a shaft, and <see cref="KingdomDelveRules.ShaftHopMultiplier"/> already prices
		/// one for anybody carrying anything. Word pays it too, because a rider cannot ride through
		/// stone either.
		/// </summary>
		/// <param name="DX">Absolute difference in global zone x.</param>
		/// <param name="DY">Absolute difference in global zone y.</param>
		/// <param name="DZ">Absolute difference in stratum.</param>
		public static int NewsSteps(int DX, int DY, int DZ)
		{
			long dx = AbsAsLong(DX);
			long dy = AbsAsLong(DY);
			long dz = AbsAsLong(DZ);
			long flat = (dx > dy) ? dx : dy;
			long steps = flat + dz * (long)KingdomDelveRules.ShaftHopMultiplier;
			return (steps >= int.MaxValue) ? int.MaxValue : (int)steps;
		}

		/// <summary>Whole world-days the word spends on the road to cover this many zone-steps,
		/// rounding up, because a day half-ridden is still a day the realm did not know.</summary>
		public static int NewsDays(int Steps)
		{
			if (Steps <= 0)
			{
				return 0;
			}
			int days = (int)(((long)Steps + WordZoneStepsPerDay - 1L) / WordZoneStepsPerDay);
			return (days > RumourDays) ? RumourDays : days;
		}

		/// <summary>
		/// How long the kingdom takes to learn its founder is dead, and by what road.
		/// <para>
		/// The order is frozen and every rung of it is a thing the realm actually built. A lit arch
		/// answering the seat carries the word with the light, which is what an arch is for. Ground
		/// the realm holds needs no telling at all. Anything else is ridden. Another world is not
		/// ridden to at all, and the realm hears it as rumour.
		/// </para>
		/// </summary>
		/// <param name="ArchAnswers">A lit mirror-gate stands where the founder fell and answers the
		/// seat's city.</param>
		/// <param name="SameWorld">The death zone and the seat share a world.</param>
		/// <param name="DX">Absolute global-zone-x difference, ignored unless ridden.</param>
		/// <param name="DY">Absolute global-zone-y difference.</param>
		/// <param name="DZ">Absolute stratum difference.</param>
		/// <param name="Days">World-days the word is on the road.</param>
		/// <param name="Road">Which road it took, for the telling.</param>
		public static void JudgeNews(bool ArchAnswers, bool SameWorld, int DX, int DY, int DZ, out int Days, out NewsRoad Road)
		{
			if (ArchAnswers)
			{
				Days = 0;
				Road = NewsRoad.Arch;
				return;
			}
			if (!SameWorld)
			{
				Days = RumourDays;
				Road = NewsRoad.Rumour;
				return;
			}
			int steps = NewsSteps(DX, DY, DZ);
			if (steps <= 0)
			{
				Days = 0;
				Road = NewsRoad.Seat;
				return;
			}
			Days = NewsDays(steps);
			Road = NewsRoad.Road;
		}

		/// <summary>The tick the word arrives, from the tick the founder fell.</summary>
		public static long NewsDueTick(long DeathTick, int Days)
		{
			if (DeathTick < 0L)
			{
				DeathTick = 0L;
			}
			if (Days < 0)
			{
				Days = 0;
			}
			long delay = (long)Days * KingdomRules.TicksPerDay;
			return (DeathTick > long.MaxValue - delay) ? long.MaxValue : DeathTick + delay;
		}

		/// <summary>Ticks still owed before the rite. Both inputs are normalized to the world clock's
		/// non-negative domain, and subtraction cannot wrap.</summary>
		public static long WorldTicksUntilDue(long NowTick, long DueTick)
		{
			if (NowTick < 0L)
			{
				NowTick = 0L;
			}
			if (DueTick < 0L || DueTick <= NowTick)
			{
				return 0L;
			}
			return DueTick - NowTick;
		}

		private static long AbsAsLong(int Value)
		{
			return (Value < 0) ? -(long)Value : Value;
		}

		/// <summary>Whether the word has arrived. Deed-keyed by the world's own clock, never by
		/// anybody's presence: the realm learns of its founder's death whether or not the heir is
		/// standing there to be told (Addendum 8, STANDARDS &sect;5.4).</summary>
		public static bool WordArrived(long NowTick, long DueTick)
		{
			return NowTick >= DueTick;
		}

		/// <summary>Where the realm stands, from the two ticks and the one flag that decide it.</summary>
		public static InterregnumPhase Phase(bool FounderFell, bool RiteHeld, long NowTick, long DueTick)
		{
			if (!FounderFell)
			{
				return InterregnumPhase.None;
			}
			if (RiteHeld)
			{
				return InterregnumPhase.Reigning;
			}
			return WordArrived(NowTick, DueTick) ? InterregnumPhase.RiteDue : InterregnumPhase.WordOnTheRoad;
		}
	}
}
