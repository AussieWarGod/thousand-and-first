using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Refusals (STANDARDS 7b: nothing stalls in silence) ---------------------------

		/// <summary>Names the thing standing in the way and where it stands. The one refusal the
		/// protection law makes unavoidable: the settlement will not take that ground, ever, so
		/// the founder has to be told which ground and why.</summary>
		public static string RefuseObstruction(string What, int X, int Y)
		{
			return "{{C|" + What + "}} stands at " + X + ", " + Y + ". The plot would have to take that ground, and nothing standing there is the settlement's to take. Clear it yourself, or stake the work elsewhere.";
		}

		/// <summary>Water refuses the plot and is never filled. The river is why the site was
		/// chosen.</summary>
		public static string RefuseLiquid(int X, int Y)
		{
			return "There is open water at " + X + ", " + Y + ". A plot is never laid over water, and the water is never filled in.";
		}

		/// <summary>Names the stage that would lift a tier gate.</summary>
		public static string RefuseStage(PlotSize Size, string SeatName, GrowthStage Stage)
		{
			return "A " + SizeName(Size) + " plot is the work of a " + StageForSize(Size).ToString().ToLowerInvariant()
				+ ". " + SeatName + " is a " + Stage.ToString().ToLowerInvariant() + " yet.";
		}

		/// <summary>Names a weather-dependent design refused underground.</summary>
		public static string RefuseSky(string Name)
		{
			return "The " + Name + " wants weather, and there is none under the rock. Raise it under open sky.";
		}

		/// <summary>No rect of this tier fits any clear ground here.</summary>
		public static string RefuseRoom(PlotSize Size)
		{
			return "There is no clear ground here wide enough for a " + SizeName(Size) + " plot and the lanes a settlement keeps around one.";
		}

		/// <summary>The zone is laid out to its budget: more plot would leave no road.</summary>
		public static string RefuseBudget(string SeatName)
		{
			return "This ground is laid out. What already stands at " + SeatName + ", and the lanes between, leave no room for another plot until something is struck.";
		}

		/// <summary>
		/// The improvement wants more ground than the plot it stands on holds. Refused BY NAME
		/// rather than by silently siting the larger tier somewhere else or quietly shrinking it:
		/// the ceiling was a choice the founder made when they staked this ground, and this is the
		/// sentence that tells them the choice has arrived.
		/// </summary>
		/// <param name="Name">What would be raised.</param>
		/// <param name="Width">Cells across it wants.</param>
		/// <param name="Height">Cells down it wants.</param>
		/// <param name="Plot">The tier of plot it stands on.</param>
		public static string RefuseFootprint(string Name, int Width, int Height, PlotSize Plot)
		{
			string ground = TryDimensions(Plot, out var plotWidth, out var plotHeight)
				? ("a " + SizeName(Plot) + " plot is " + SpanWord(plotWidth, plotHeight))
				: "this ground is less than that";
			return "The {{C|" + Name + "}} wants more ground than this plot holds: it stands "
				+ SpanWord(Width, Height) + ", and " + ground
				+ ". Strike what is here and stake larger ground, or leave it as it is.";
		}

		/// <summary>A design that needs weather, refused a tier that has declared itself
		/// closed.</summary>
		public static string RefuseRoofSky(string Name, RoofState Roof)
		{
			return "The " + Name + " wants weather, and this tier of it is " + RoofWord(Roof)
				+ ". Raise it under something that lets the sky in.";
		}

		/// <summary>
		/// The grown building would stand on the cell a yard trade is worked in. Never taken down
		/// on its own: the founder is told which trade is in the way and chooses, because a
		/// household's sideline is theirs and the settlement does not tidy it away to make room.
		/// </summary>
		public static string RefuseYardWork(string Name, string SuccessorName, string WorkName)
		{
			return "The " + Name + " could be raised into " + KingdomUpgradeRules.Article(SuccessorName)
				+ ", but the {{C|" + WorkName + "}} in its yard stands on ground the larger building needs."
				+ " Let the trade go first, and the work can begin. Nothing in the yard comes down on its own.";
		}

		/// <summary>
		/// Verbatim head of the stamper's occupant refusal (Growth/KingdomArchitectureStamper.
		/// Verification.cs, CanInsert). Kept here so the refusal text and the reader that turns it
		/// into a sentence for the founder cannot drift apart.
		/// </summary>
		public const string OccupantSlotRefusalPrefix = "a living occupant moved onto layout slot ";

		/// <summary>True when a stage refusal is the living-occupant one and names a slot.</summary>
		public static bool IsOccupantSlotRefusal(string Failure)
		{
			return OccupantSlotOf(Failure) != null;
		}

		/// <summary>The slot named by an occupant refusal, or null if it is another refusal.</summary>
		public static string OccupantSlotOf(string Failure)
		{
			// Ordinal, like every other prefix parse in Growth: the matched length has to BE
			// OccupantSlotRefusalPrefix.Length for the Substring below to cut the slot, and under
			// linguistic comparison it need not be.
			if (Failure == null || !Failure.StartsWith(OccupantSlotRefusalPrefix,
					global::System.StringComparison.Ordinal)
				|| Failure.Length <= OccupantSlotRefusalPrefix.Length) return null;
			return Failure.Substring(OccupantSlotRefusalPrefix.Length);
		}

		/// <summary>
		/// Whether a paid raising blocked by a living occupant owes the founder a sentence. Once
		/// per job and slot: a block already spoken for that slot stays quiet on every later pass,
		/// and the occupant standing on a DIFFERENT slot is a new fact worth saying.
		/// </summary>
		/// <param name="Announced">The raising root's announced flag: 1 once spoken.</param>
		/// <param name="AnnouncedSlot">The slot last spoken for, or null if none.</param>
		/// <param name="Slot">The slot the stamper refused now. Null says nothing.</param>
		public static bool ShouldAnnounceOccupiedSlot(int Announced, string AnnouncedSlot,
			string Slot)
		{
			return Slot != null && (Announced != 1 || AnnouncedSlot != Slot);
		}

		/// <summary>
		/// A fully paid raising that cannot put its first stage on the ground because somebody is
		/// standing on one of its slots. Named rather than retried in silence: the labour is spent,
		/// the work will never land while the body is there, and moving a living occupant is not
		/// the settlement's to do.
		/// </summary>
		public static string RefuseOccupiedSlot(string Name, string Slot)
		{
			return "The {{C|" + Name + "}} is paid for and cannot be raised: somebody is standing on the ground at layout slot " + Slot + ". Nothing living is moved to clear a plot. Send them off it, and the raising goes on.";
		}

		/// <summary>The stage pair a waiting line was said for, as stored on the raising root.</summary>
		public static string StageWaitingPair(int Applied, int Target)
		{
			return Applied + ":" + Target;
		}

		/// <summary>
		/// Whether a raising that is not advancing owes the log a line. Both conditions are load
		/// bearing: an unpaid raising is merely accumulating labour between stages and would print
		/// once per plot per pass, and a pair already said for this plot says nothing new.
		/// </summary>
		/// <param name="Remaining">Work ticks still owed; negative when unknown, which says nothing.</param>
		/// <param name="Applied">The last stage physically applied.</param>
		/// <param name="DoneStage">The stage value that means the raising is finished.</param>
		/// <param name="LastPair">The pair last said for this plot, or null.</param>
		/// <param name="Pair">The pair now in force.</param>
		public static bool ShouldSayStageWaiting(long Remaining, int Applied, int DoneStage,
			string LastPair, string Pair)
		{
			return Remaining == 0L && Applied < DoneStage && Pair != null && Pair != LastPair;
		}

		/// <summary>
		/// Whether a living body standing on an authored slot is actually in the way. Only a slot
		/// the map declares Blocked is: a wall, a ritestone or a canvas cannot be raised through
		/// somebody. A walkable ground or path tile laid UNDER a standing body is lawful and always
		/// was -- non-solid objects share a cell in Qud -- and refusing there is how the founder,
		/// who by construction stands on the rite cell they poured, bricked their own heart works.
		/// Adjacent slots are used from the side and never stood on, so they do not block either.
		/// </summary>
		public static bool SlotBlocksOccupant(ArchitecturePassability Passability)
		{
			return Passability == ArchitecturePassability.Blocked;
		}

		/// <summary>Why one body on a blocking slot is or is not the settlement's to stand off.
		/// Named in the log so an operator never sees "a living occupant" with no identity.</summary>
		public enum OccupantReason
		{
			/// <summary>One of ours, in standing, unposted: displaceable.</summary>
			Resident,
			/// <summary>The founder. Never moved.</summary>
			Player,
			/// <summary>Led by the founder. Never moved.</summary>
			PlayerLed,
			/// <summary>Not on this settlement's surveyed roll of settlers.</summary>
			NotOurs,
			/// <summary>Staged for a happening; its position is another system's.</summary>
			Staged,
			/// <summary>Ours by survey, but carrying no roll id: unproven, so not moved.</summary>
			NoRoll,
			/// <summary>On the roll, but not in resident standing.</summary>
			NotResident,
			/// <summary>Ours and displaceable, but posted into the layout: naming, not shoving.</summary>
			AnchorBound
		}

		/// <summary>What a raising may lawfully do about the living bodies on its slots.</summary>
		public enum OccupantVerdict
		{
			/// <summary>Nobody is standing on the layout.</summary>
			Clear,
			/// <summary>Every occupant is this settlement's own: the crew walks them off.</summary>
			Displace,
			/// <summary>The player or a stranger stands there: refused, and said once.</summary>
			Refuse,
			/// <summary>One of ours is posted INTO the layout: walking them off only sends them
			/// back next pass, so it is a siting fault to be named, not a body to be moved.</summary>
			AnchorBound
		}

		/// <summary>
		/// Who may be moved off a raising's ground. The settlement's own residents are walked off
		/// their own building site; the player and anybody who is not ours are never moved, because
		/// nothing the founder did not place is the settlement's to shove. Mixed company refuses:
		/// clearing half the slots would move residents for nothing. A resident whose post anchor
		/// lies inside the layout is named instead of shoved in a circle.
		/// </summary>
		/// <param name="Occupants">Living bodies standing on layout slots.</param>
		/// <param name="Residents">How many of them are this settlement's residents.</param>
		/// <param name="AnyPlayer">Whether the player is one of them.</param>
		/// <param name="AnyAnchorInLayout">Whether one of them is posted into the layout.</param>
		public static OccupantVerdict JudgeOccupants(int Occupants, int Residents, bool AnyPlayer,
			bool AnyAnchorInLayout)
		{
			if (Occupants <= 0) return OccupantVerdict.Clear;
			if (AnyPlayer || Residents != Occupants || Residents < 0) return OccupantVerdict.Refuse;
			return AnyAnchorInLayout ? OccupantVerdict.AnchorBound : OccupantVerdict.Displace;
		}

		/// <summary>
		/// The crew stood its own people off the site. Told outcome-first, because bodies are moved
		/// on the failing path too: a founder who is told "the work goes on" when the raising was
		/// refused anyway has been lied to about ground they can see standing empty.
		/// </summary>
		/// <param name="Moved">Bodies left standing off the site.</param>
		/// <param name="Raised">Whether the ground stage then landed.</param>
		/// <param name="Fault">Why it did not, when it did not.</param>
		public static string ClearedOccupiedSlots(string Name, int Moved, bool Raised, string Fault)
		{
			string stood = "The crew stood " + Moved + (Moved == 1 ? " settler" : " settlers")
				+ " off the ground the {{C|" + Name + "}} is being raised on, ";
			return Raised ? stood + "and the work goes on."
				: stood + "but the raising was refused: " + (Fault ?? "the ground would not take it")
					+ ".";
		}

		/// <summary>
		/// A raising whose ground is stood on by one of our own whose post is INSIDE the layout.
		/// Standing them off would only walk them back, so the founder is told where the post is:
		/// this is a siting fault to be settled, not a body to be shoved.
		/// </summary>
		public static string RefuseOccupiedAnchor(string Name, int X, int Y)
		{
			return "The {{C|" + Name + "}} is paid for and cannot be raised: one of your own is posted at " + X + ", " + Y + ", inside the ground it stands on, and walks back the moment the crew stands them off. Move the post or stake the work elsewhere.";
		}

		/// <summary>A design people are meant to sleep in, on a tier with nothing over it.</summary>
		public static string RefuseBedRoof(string Name)
		{
			return "Nobody sleeps in the open. The " + Name + " is " + RoofWord(RoofState.Open)
				+ ", and a bed wants canvas over it at the very least.";
		}
	}
}
