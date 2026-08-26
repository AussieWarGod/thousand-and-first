using XRL;
using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
	{
	public static partial class KingdomBrink
	{
		// --- The realm's brink -------------------------------------------------------------

		/// <summary>
		/// What is standing over the realm. The reached tick is <c>KingdomSystem.LastDissentTick</c>
		/// itself, and honestly so: dissent stops accruing at the breaking point, so the last tick
		/// dissent moved IS the tick the realm reached the brink, and freezing it there costs no
		/// field and tells no lie.
		/// </summary>
		public static BrinkRecord OfCity(KingdomSystem System)
		{
			if (System == null || The.Game == null || The.Game.GetIntGameState(CityStandingStateKey) == 0)
			{
				return BrinkRecord.None;
			}
			return new BrinkRecord(Stands: true, System.LastDissentTick, CityWarnedTick(), null, 0);
		}

		/// <summary>Whether the realm stands at the breaking point with its window still
		/// running.</summary>
		public static bool CityStands()
		{
			return The.Game != null && The.Game.GetIntGameState(CityStandingStateKey) != 0;
		}

		/// <summary>
		/// Records that the realm has reached the breaking point, freezing
		/// <c>KingdomSystem.LastDissentTick</c> at the day the crossing actually happened so the
		/// warning can quote it. Idempotent, for the reason the settler form is.
		/// </summary>
		public static bool RecordCity(KingdomSystem System, long ReachedTick)
		{
			if (System == null || The.Game == null || CityStands())
			{
				return false;
			}
			if (ReachedTick > 0L)
			{
				System.LastDissentTick = ReachedTick;
			}
			The.Game.SetIntGameState(CityStandingStateKey, 1);
			The.Game.SetStringGameState(CityWarnedStateKey, "");
			return true;
		}

		/// <summary>Stamps the tick the realm's warning went out. Idempotent, for the reason
		/// <see cref="MarkWarned"/> is.</summary>
		public static bool MarkCityWarned(long NowTick)
		{
			if (!CityStands() || KingdomBrinkRules.Warned(CityWarnedTick()))
			{
				return false;
			}
			The.Game.SetStringGameState(CityWarnedStateKey, ((NowTick > 0L) ? NowTick : 1L).ToString());
			return true;
		}

		/// <summary>
		/// Forgets the realm's brink and restarts its clock at <paramref name="NowTick"/>, because
		/// the quarrel has been eased and the days the realm spent standing at the edge must not
		/// be billed again the moment it steps back.
		/// </summary>
		/// <returns>True when there was something to forget.</returns>
		public static bool LiftCity(KingdomSystem System, long NowTick)
		{
			if (!CityStands())
			{
				return false;
			}
			The.Game.SetIntGameState(CityStandingStateKey, 0);
			The.Game.SetStringGameState(CityWarnedStateKey, "");
			if (System != null)
			{
				System.LastDissentTick = NowTick;
			}
			return true;
		}

		/// <summary>Whether the realm's window has run out with the quarrel still standing.</summary>
		public static bool CityWindowSpent(long NowTick)
		{
			return CityStands() && KingdomBrinkRules.WindowSpent(BrinkKind.City, CityWarnedTick(), NowTick);
		}

		// --- Saying it, and unsaying it ----------------------------------------------------

		/// <summary>
		/// The warning, pushed once through <see cref="KingdomWord"/>: to the founder wherever
		/// they stand, into the ledger's brink lane for the report they read at the seat, and into
		/// the chronicle which dates it. Rule 3, coaching clause and all.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Kind">Which brink.</param>
		/// <param name="Subject">The settler by name, or the city by name.</param>
		/// <param name="Cause">What the founder would act on.</param>
		/// <param name="Record">The brink as it now stands, for its reached and warned ticks.</param>
		/// <param name="NowTick">Now, for the honest elapsed.</param>
		/// <param name="Here">Whether the founder is standing in the ground this is about.</param>
		/// <param name="From">The city the word comes out of, when they are not.</param>
		/// <param name="Spoken">A consumer's own louder wording for the pushed line, or null to
		/// push the ledger note itself.</param>
		public static void Announce(KingdomSystem System, BrinkKind Kind, string Subject, string Cause, BrinkRecord Record, long NowTick, bool Here, string From, string Spoken)
		{
			if (System == null)
			{
				return;
			}
			int days = KingdomBrinkRules.DaysStood(Record.ReachedTick, NowTick);
			int left = KingdomBrinkRules.DaysLeft(Kind, Record.WarnedTick, NowTick);
			// Subjects are semantic resident/settlement names. Creed causes are already the
			// engine's formatted faction display name; city causes are semantic names too.
			string shownSubject = KingdomPresentation.Rich(Subject);
			string shownCause = Kind == BrinkKind.City
				? KingdomPresentation.Rich(Cause)
				: Cause;
			KingdomWord.Warn(System, From, Here,
				KingdomBrinkRules.AnnounceNote(Kind, shownSubject, shownCause, days, left),
				KingdomBrinkRules.AnnounceTelling(Kind, shownSubject, shownCause, days),
				Spoken);
			KingdomLog.Log("brink: " + Kind + " " + (Subject ?? "-") + " cause=" + (Cause ?? "-")
				+ " days=" + days + " left=" + left);
		}

		/// <summary>
		/// The unsaying, when the cause went before the window did. Pushed the same way the
		/// warning was, and out of the chronicle: the book records what happened, and a thing that
		/// stopped happening is news for the report rather than an entry in it.
		/// </summary>
		public static void Unsay(KingdomSystem System, BrinkKind Kind, string Subject, bool Here, string From)
		{
			KingdomWord.Unsay(System, From, Here,
				KingdomBrinkRules.LiftedNote(Kind, KingdomPresentation.Rich(Subject)));
		}

		// --- Where the record is kept --------------------------------------------------------

		private static long CityWarnedTick()
		{
			string stored = (The.Game == null) ? null : The.Game.GetStringGameState(CityWarnedStateKey, "");
			long tick;
			return (!string.IsNullOrEmpty(stored) && long.TryParse(stored, out tick)) ? tick : KingdomBrinkRules.Unwarned;
		}

		/// <summary>
		/// The realm, or null. <c>GetSystem</c> and not <c>RequireSystem</c>: these accessors are
		/// called from passes that can run before a realm exists, and a settler with no realm has
		/// no roll to stand on and therefore no brink &mdash; which is the same answer the property
		/// bag gave, arrived at honestly.
		/// </summary>
		private static KingdomSystem Realm()
		{
			return (The.Game == null) ? null : The.Game.GetSystem<KingdomSystem>();
		}
	}
}
