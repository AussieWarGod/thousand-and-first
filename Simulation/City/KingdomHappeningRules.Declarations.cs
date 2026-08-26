using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What kind of thing happened. LIVING-CITY-ARCHITECTURE &sect;7.4 W4.
	/// <para>
	/// Four kinds and no fifth, because these are the four things the model can already SEE
	/// without anybody inventing a new dimension for them: two rows sharing a roof, a row that
	/// went <c>Dead</c>, a clock that came due on Qud's own calendar, and a work row that stopped.
	/// A happening is therefore always a <b>rendering of model state</b> (BUILDING-CATALOGUE-BRIEF
	/// Addendum 13, THE MESH CONDITION) and never a generator with a table of its own.
	/// </para>
	/// </summary>
	internal enum KingdomHappeningKind : byte
	{
		None = 0,

		/// <summary>Two resident rows who already share a roof, and whose creeds do not hold it
		/// against each other.</summary>
		Wedding = 1,

		/// <summary>A resident row that went <c>Dead</c>. Told exactly once, by the memory
		/// machinery that already tells it &mdash; see <see cref="FuneralClause"/>.</summary>
		Funeral = 2,

		/// <summary>Qud's own calendar came round. Never an invented holiday.</summary>
		Festival = 3,

		/// <summary>A work row stopped running, or fell under the condemned line.</summary>
		Breakdown = 4,

		/// <summary>
		/// W7. A work went quiet because its network ran short &mdash; a deficit, not a fault.
		/// <para>
		/// Deliberately NOT <see cref="Breakdown"/>: a broken work is a thing to mend and a
		/// browned-out work is a thing to feed, the founder's next move is different in each case,
		/// and STANDARDS 7b's whole complaint is about a settlement that says "stopped" without
		/// saying which. LIVING-CITY-ARCHITECTURE &sect;3.11.
		/// </para>
		/// </summary>
		Brownout = 5
	}

	/// <summary>
	/// Which day of Qud's own calendar a feast is anchored to.
	/// <para>
	/// <b>Both of these are vanilla's, and there are only two, because vanilla only has two.</b>
	/// A survey of <c>D/XRL/World/Calendar.cs</c> found no holiday machinery at all: no
	/// <c>Holiday</c> type, no <c>HolyDay</c>, no date-pinned event, and not one place in the whole
	/// engine that branches on <c>GetMonth()</c> or <c>GetDay()</c>. What vanilla does have is a
	/// thirteen-month year with one intercalary month and one named day a month, and those are the
	/// two anchors this enum carries. Addendum 13 lane 4 asks for <i>"festivals and rites anchored
	/// to vanilla months and holy days, never invented holidays"</i>; this is the whole of what
	/// there is to anchor to.
	/// </para>
	/// </summary>
	internal enum KingdomFestivalAnchor : byte
	{
		None = 0,

		/// <summary>
		/// The Ides. The one day of the month Qud declines to number: <c>Calendar.GetDay</c>
		/// returns the literal string <c>"Ides"</c> for the fifteenth
		/// (<c>D/XRL/World/Calendar.cs:223</c>) and an ordinal for every other day. Twelve a year,
		/// one per numbered month.
		/// </summary>
		Ides = 1,

		/// <summary>
		/// The festival of Ut yara Ux. Qud's one canonical named festival, and the only one:
		/// <c>D/Qud/API/JournalAPI.cs:467</c> ("Since the first festival of Ut yara Ux, the
		/// villagers of Joppa have feasted on warm apple matz"),
		/// <c>D/XRL/World/Parts/GenerateFriendOrFoe.cs:54</c> ("ruining the festival of Ut yara
		/// Ux"), and <c>B/Books.xml:499, 1323, 1368, 1631</c>. It shares its name with the
		/// five-day intercalary month it falls in (<c>D/XRL/World/Calendar.cs:87-89</c>), which is
		/// what makes it datable at all.
		/// </summary>
		UtYaraUx = 2
	}

	/// <summary>
	/// One happening, in the shape the told-log ring stores it. LIVING-CITY-ARCHITECTURE
	/// &sect;1.2(f).
	/// <para>
	/// Six fields and not one more, because the ring is thirty-two bytes a line and the prose is
	/// derived rather than stored &mdash; the same discipline that makes a district a code and not
	/// a sentence.
	/// </para>
	/// </summary>
	internal readonly struct KingdomHappening
	{
		internal readonly KingdomHappeningKind Kind;

		internal readonly long Tick;

		/// <summary>The resident id, or the work id, this happening is about. Zero for a
		/// happening the whole city is the subject of.</summary>
		internal readonly int SubjectA;

		/// <summary>The second party of a wedding; zero everywhere else.</summary>
		internal readonly int SubjectB;

		internal readonly string PlaceZoneId;

		/// <summary>The kind's own small integer: a festival's anchor, a breakdown's condition, a
		/// funeral's death cause ordinal.</summary>
		internal readonly int Outcome;

		internal KingdomHappening(KingdomHappeningKind kind, long tick, int subjectA, int subjectB, string placeZoneId, int outcome)
		{
			Kind = kind;
			Tick = tick;
			SubjectA = subjectA;
			SubjectB = subjectB;
			PlaceZoneId = placeZoneId;
			Outcome = outcome;
		}

		/// <summary>Nothing happened. What every eligibility check that failed returns.</summary>
		internal static KingdomHappening None
		{
			get { return new KingdomHappening(KingdomHappeningKind.None, 0L, 0, 0, null, 0); }
		}

		internal bool Stands
		{
			get { return Kind != KingdomHappeningKind.None; }
		}

		/// <summary>The told-log line this happening is. One ring, one vocabulary: the happening
		/// layer stores nothing of its own.</summary>
		internal KingdomToldRow ToldRow
		{
			get { return new KingdomToldRow(KingdomHappeningRules.ToldKindOf(Kind), Tick, SubjectA, SubjectB, PlaceZoneId, Outcome); }
		}
	}
}
