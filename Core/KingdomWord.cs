using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The push channel: the one way the settlement reaches the founder with something they must
	/// act on, wherever they happen to be standing.
	/// <para>
	/// <b>Why it exists.</b> Before Addendum 10(a) every brink warning went into
	/// <c>KingdomLedger</c> and nowhere else, and the ledger is a PULL &mdash; a report waiting at
	/// the seat for a founder who chooses to open the Charter. That was fair while irreversible
	/// consequences waited at the brink for awareness. It stopped being fair the moment the author
	/// ruled that they may fire in absence: a loss the founder can be dealt while they are away
	/// must be announced by something that goes to THEM, not by something that waits for them.
	/// </para>
	/// <para>
	/// <b>What it does.</b> One call, three surfaces, no double-telling:
	/// <list type="bullet">
	/// <item>a player message &mdash; the push, which reaches the founder wherever they are;</item>
	/// <item>the ledger's brink lane &mdash; so the same words are still in the homecoming report
	/// they will read at the seat;</item>
	/// <item>the chronicle &mdash; so the book holds the warning as well as the loss.</item>
	/// </list>
	/// The push is framed by WHERE the founder is: standing in the city the news is about they get
	/// the plain announcement, and anywhere else it arrives as
	/// <c>KingdomBrinkRules.WordFrom</c> &mdash; word out of a named city, finding them. One line
	/// either way. Nobody is ever told the same thing twice in two voices.
	/// </para>
	/// <para>
	/// <b>What it deliberately is not.</b> It is not a queue, and it does not retry. The mod's
	/// clocks only ever resolve inside a settlement pass, so there is no moment at which word can
	/// be MADE that has nobody to deliver it to; the honest shape is therefore a send, not an
	/// outbox. If a future wave gives the realm a pass that runs without the founder, this is the
	/// one place that has to learn to hold a letter, and every brink already speaks through it.
	/// </para>
	/// </summary>
	public static class KingdomWord
	{
		/// <summary>
		/// Whether the founder is standing in the ground this news is about. Decides the framing
		/// of the push and nothing else &mdash; the word goes out either way.
		/// </summary>
		/// <param name="Z">The zone the news is about. Null reads as "not here", which errs toward
		/// naming the city the word came from.</param>
		public static bool StandsIn(Zone Z)
		{
			return Z != null && The.Player != null && The.Player.CurrentZone == Z;
		}

		/// <summary>The name to put on word that came from elsewhere: the city it is about, or the
		/// seat when the caller has nothing better.</summary>
		public static string CityName(KingdomSystem System, string Named)
		{
			if (!string.IsNullOrEmpty(Named))
			{
				return Named;
			}
			return (System != null) ? System.SeatName : null;
		}

		/// <summary>
		/// A warning, sent once. The caller has already made sure of the "once" &mdash; every
		/// brink stamps its warned tick and never speaks twice about the same standing cause.
		/// </summary>
		/// <param name="System">The realm. Null sends nothing.</param>
		/// <param name="From">The city the news is about, for the away framing.</param>
		/// <param name="Here">Whether the founder is standing in that city.</param>
		/// <param name="Note">The founder-facing line: names the subject, the cause, the honest
		/// elapsed, the arrest and the days left.</param>
		/// <param name="Telling">The chronicle's lower-case clause, or null to write nothing in
		/// the book.</param>
		/// <param name="Spoken">The line to push, when a consumer's own prose says it better than
		/// the ledger note does. Null pushes the note itself.</param>
		public static void Warn(KingdomSystem System, string From, bool Here, string Note, string Telling, string Spoken)
		{
			if (System == null)
			{
				return;
			}
			if (!string.IsNullOrEmpty(Note))
			{
				System.Ledger.NoteBrink(Note);
			}
			if (!string.IsNullOrEmpty(Telling))
			{
				KingdomChronicle.Record(System, Telling);
			}
			Push(System, From, Here, string.IsNullOrEmpty(Spoken) ? Note : Spoken);
		}

		/// <summary>
		/// The unsaying, when the cause went before the window did. Pushed for the same reason the
		/// warning is: a founder who was told from a distance that they were about to lose
		/// somebody is owed the withdrawal from the same distance. Ledger only in the book's
		/// sense &mdash; the chronicle records what happened, and a thing that stopped happening is
		/// news for the report rather than an entry.
		/// </summary>
		public static void Unsay(KingdomSystem System, string From, bool Here, string Note)
		{
			if (System == null || string.IsNullOrEmpty(Note))
			{
				return;
			}
			System.Ledger.NoteBrinkLifted(Note);
			Push(System, From, Here, Note);
		}

		/// <summary>
		/// The aftermath of a consequence that has already happened &mdash; dated by the caller to
		/// the tick the window actually ran out on, not to the pass that found it. Carries the
		/// consequence's OWN prose; this channel only decides where the founder hears it.
		/// </summary>
		public static void Aftermath(KingdomSystem System, string From, bool Here, string Note)
		{
			if (System == null || string.IsNullOrEmpty(Note))
			{
				return;
			}
			System.Ledger.NoteBrink(Note);
			Push(System, From, Here, Note);
		}

		private static void Push(KingdomSystem System, string From, bool Here, string Line)
		{
			if (string.IsNullOrEmpty(Line))
			{
				return;
			}
			string said = Here ? Line : KingdomBrinkRules.WordFrom(CityName(System, From), Line);
			MessageQueue.AddPlayerMessage(said);
			KingdomLog.Log("word: " + (Here ? "here " : "away ") + said);
		}
	}
}
