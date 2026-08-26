using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Which law the realm keeps for the founder's death (Addendum 22 C2/C3).</summary>
	public enum SuccessionLaw
	{
		/// <summary>The shipped law: the settler who has served longest. Config B, and it is
		/// <c>KingdomOffices</c>' own rule for the office read a second time.</summary>
		Seniority,

		/// <summary>A named designee, falling back to seniority when the name is not on the roll.
		/// C3 rules the designee "the first succession verb later"; the law is representable now so
		/// the verb, when it lands, changes a charter declaration and nothing here.</summary>
		Designee
	}

	/// <summary>Whether the realm can carry the founder's death, and if not, why not.</summary>
	public enum SuccessionVerdict
	{
		/// <summary>An heir stands. The run continues.</summary>
		Succeeds,

		/// <summary>Not Kingdom Mode. Classic and Roleplay are untouched by construction.</summary>
		NotKingdomMode,

		/// <summary>No realm was ever founded, so there is nobody to inherit anything.</summary>
		Unfounded,

		/// <summary>The realm stands and its roll is empty. The line ends here.</summary>
		NoHeir,

		/// <summary>An heir is named on the roll and no body could be reached to seat them in.
		/// Distinct from <see cref="NoHeir"/> because it is an engine condition rather than a
		/// judgement about the realm, and because it is the one that must be logged.</summary>
		HeirUnreachable
	}

	/// <summary>Where the realm stands between the founder's death and the mourning rite.</summary>
	public enum InterregnumPhase
	{
		/// <summary>No founder has died in this realm.</summary>
		None,

		/// <summary>The founder is dead and the kingdom does not know it yet (Addendum 22 C8).</summary>
		WordOnTheRoad,

		/// <summary>The word has arrived. The rite is owed and has not been held.</summary>
		RiteDue,

		/// <summary>The rite was held; the heir holds the charter.</summary>
		Reigning
	}

	/// <summary>The road the word took, for the telling. Never a number on screen.</summary>
	public enum NewsRoad
	{
		/// <summary>The founder died on either city's owned ground. Nobody had to be told.</summary>
		Seat,

		/// <summary>Overland, at a rider's pace, through however much rock stood in the way.</summary>
		Road,

		/// <summary>Through a lit arch answering the seat. The word crosses with the light.</summary>
		Arch,

		/// <summary>No road reaches where the founder fell. The word arrives the way every other
		/// thing about that country arrives: carried by somebody who heard it from somebody.</summary>
		Rumour
	}

	/// <summary>Which side of the honesty rule one journal kind falls on.</summary>
	public enum JournalKind
	{
		Observation,
		GeneralNote,
		VillageNote,
		RecipeNote,
		SultanNote,
		MapNote,
		Accomplishment
	}

	/// <summary>How the heir was arrived at, which is what decides whether the seat is paid.</summary>
	public enum HeirChoice
	{
		/// <summary>The realm's own law picked. Config B, and the default.</summary>
		Law,

		/// <summary>The founder's own will picked. Config A, and C13 prices it.</summary>
		Chosen
	}

	/// <summary>Whether a death-token may begin the one synchronous accession transaction.</summary>
	public enum SuccessionAttemptVerdict
	{
		/// <summary>No transaction owns this system. The death may begin.</summary>
		Begin,

		/// <summary>This exact death is already in flight. Do not repeat any phase.</summary>
		DuplicatePending,

		/// <summary>This exact death already completed. Do not repeat the accession.</summary>
		AlreadyCompleted,

		/// <summary>A different death is in flight. Fail closed rather than overwrite it.</summary>
		Conflict,

		/// <summary>The proposed token cannot identify a death.</summary>
		Invalid
	}

	/// <summary>Durable checkpoints inside the death-time mourning rite. These are physical
	/// checkpoints, not claims that Qud yielded a turn between them: <c>GameObject.Die</c> invokes
	/// <c>AfterDieEvent</c> and immediately rechecks <c>IsPlayer()</c>.</summary>
	public enum MourningRiteStage
	{
		None,
		Frozen,
		WordArrived,
		ProcessionComplete,
		ShrinePlaced,
		BodyCrossed,
		Complete
	}

	/// <summary>What an exact shrine receipt and its frozen cell permit.</summary>
	public enum FounderShrinePlacementVerdict
	{
		Create,
		AdoptExact,
		Refuse
	}

	/// <summary>One real, already-bound resident frozen into the mourning procession.</summary>
	public readonly struct KingdomRiteAttendee
	{
		public readonly int ResidentId;
		public readonly string ObjectId;
		public readonly string Name;
		public readonly string ZoneId;
		public readonly int OriginalX;
		public readonly int OriginalY;
		public readonly string Post;
		public readonly string Home;
		public readonly int RiteX;
		public readonly int RiteY;

		public KingdomRiteAttendee(int residentId, string objectId, string name, string zoneId,
			int originalX, int originalY, string post, string home, int riteX, int riteY)
		{
			ResidentId = residentId;
			ObjectId = objectId ?? "";
			Name = name ?? "";
			ZoneId = zoneId ?? "";
			OriginalX = originalX;
			OriginalY = originalY;
			Post = post ?? "";
			Home = home ?? "";
			RiteX = riteX;
			RiteY = riteY;
		}
	}

	/// <summary>The exact outcome of one player-body assignment and its mandatory global
	/// <c>IPlayerSystem</c> registration sweep.</summary>
	internal readonly struct KingdomPlayerBodyTransfer
	{
		internal readonly bool SetBodyReturnedClean;
		internal readonly bool OriginalControls;
		internal readonly bool TargetControls;
		internal readonly bool RegistrationsExact;
		internal readonly int RegistrationFailures;
		internal readonly Exception Failure;

		internal KingdomPlayerBodyTransfer(bool setBodyReturnedClean, bool originalControls,
			bool targetControls, bool registrationsExact, int registrationFailures,
			Exception failure)
		{
			SetBodyReturnedClean = setBodyReturnedClean;
			OriginalControls = originalControls;
			TargetControls = targetControls;
			RegistrationsExact = registrationsExact;
			RegistrationFailures = registrationFailures;
			Failure = failure;
		}

		/// <summary>A resident accession may begin only after the engine body setter returned
		/// normally, the exact heir controls, and every player system was rebound.</summary>
		internal bool MayPublishAccession => SetBodyReturnedClean && TargetControls
			&& RegistrationsExact;
	}

	/// <summary>
	/// One candidate for the charter, flattened out of a resident row into exactly what the law
	/// reads. A struct of its own rather than the row itself, so this whole file stays free of the
	/// simulation slice's internals and of the engine, and so the roster-only fallback (a realm
	/// whose city book has no rows for a name it still keeps on the roll) can build one too.
	/// </summary>
	public readonly struct KingdomHeir
	{
		/// <summary>The settler's given name, as the roll and the chronicle spell it.</summary>
		public readonly string Name;

		/// <summary>The tick they came. Seniority is the smallest of these.</summary>
		public readonly long ArrivedTick;

		/// <summary>Their creed's faction name, or null. Read against the realm's declared creed.</summary>
		public readonly string Creed;

		/// <summary>Creeds held and left, as <c>KingdomCreedRules.EncodeKept</c> stores them.</summary>
		public readonly string KeptCreeds;

		/// <summary>Whether they are still a resident in good standing rather than a row kept for
		/// the record. Only somebody on the roll may take the charter.</summary>
		public readonly bool OnTheRoll;

		/// <summary>Whether they already hold the settlement's one office.</summary>
		public readonly bool HoldsOffice;

		/// <summary>The zone their body was last bound in, or null when the realm never bound one.</summary>
		public readonly string BoundZoneId;

		/// <summary>Their resident id, or zero. Used only to break a tie no other field breaks.</summary>
		public readonly int ResidentId;

		public KingdomHeir(string name, long arrivedTick, string creed, string keptCreeds, bool onTheRoll, bool holdsOffice, string boundZoneId, int residentId)
		{
			Name = name;
			ArrivedTick = arrivedTick;
			Creed = creed;
			KeptCreeds = keptCreeds;
			OnTheRoll = onTheRoll;
			HoldsOffice = holdsOffice;
			BoundZoneId = boundZoneId;
			ResidentId = residentId;
		}
	}

	/// <summary>
	/// Everything Kingdom Mode can work out without the engine: how long the word takes to reach
	/// the seat, which settler the realm's own law raises, what the realm thinks of them on the day
	/// they take the charter, which half of the founder's journal dies with the founder, and when a
	/// line has simply run out.
	/// <para>
	/// <b>The mode is not a difficulty setting; it is a claim about who the run belongs to.</b>
	/// Classic says the run is the character's and ends with them. Roleplay says the death was a
	/// mistake and rewinds it. Kingdom Mode says the death was real and the kingdom was never the
	/// character's to begin with &mdash; so the person is gone, permanently and witnessed, and the
	/// realm goes on to raise somebody else. Addendum 21 and its extension; Addendum 22 C1-C13.
	/// </para>
	/// <para>
	/// <b>The honesty rule (Addendum 21, binding) is what every number here serves.</b> Becoming a
	/// citizen is as if a new game began as that citizen: their body, their attributes, their
	/// knowledge, and their standing &mdash; never the founder's. Which is why the accession regard
	/// below is derived from the heir's own life and floored well short of trust, and why the
	/// forget table exempts exactly two kinds and no others.
	/// </para>
	/// <para>
	/// No <c>XRL</c> usings, by the same law every other <c>*Rules</c> file in this mod keeps
	/// (STANDARDS &sect;2). The engine half is <c>KingdomSuccession</c>.
	/// </para>
	/// </summary>
	public static class KingdomSuccessionRules
	{
		internal const int MaxDeathTokenChars = 512;
		/// <summary>The mourning rite is not an ordinary four-person happening. Every living
		/// resident whose exact bound body already stands in the rite zone is evidence, up to the
		/// city's legal population envelope.</summary>
		internal const int MaxRiteAttendees = KingdomRules.MaxPopulation;

		/// <summary>Worst-case canonical base64 for sixty admitted rows at the per-field bounds
		/// enforced by <see cref="ValidRiteAttendee"/>. This state normally exists for one
		/// synchronous callback, but injected/cold-load checkpoints must retain it whole.</summary>
		internal const int MaxRiteManifestChars = 1024 * 1024;

		/// <summary>Only forward, adjacent rite checkpoints are legal. Repeating the current
		/// checkpoint is the exact-once retry; skipping physical evidence is never legal.</summary>
		public static bool MayAdvanceRite(MourningRiteStage Current, MourningRiteStage Next)
		{
			if (!Enum.IsDefined(typeof(MourningRiteStage), Current)
				|| !Enum.IsDefined(typeof(MourningRiteStage), Next))
			{
				return false;
			}
			return Next == Current || (int)Next == (int)Current + 1;
		}

		/// <summary>Check-before-mint for the in-run founder shrine.</summary>
		public static FounderShrinePlacementVerdict JudgeFounderShrinePlacement(
			bool HasReceipt, bool ExactObjectMatches, bool CellPassable, int CellObjectCount)
		{
			if (HasReceipt)
			{
				return ExactObjectMatches ? FounderShrinePlacementVerdict.AdoptExact
					: FounderShrinePlacementVerdict.Refuse;
			}
			return CellPassable && CellObjectCount == 0
				? FounderShrinePlacementVerdict.Create : FounderShrinePlacementVerdict.Refuse;
		}

		/// <summary>Canonical, bounded receipt for the real bodies assigned to the procession.
		/// Text fields are base64 so names, object ids and zone ids cannot alter row topology.</summary>
		public static string EncodeRiteManifest(KingdomRiteAttendee[] Attendees)
		{
			if (Attendees == null || Attendees.Length == 0
				|| Attendees.Length > MaxRiteAttendees)
			{
				return "";
			}
			StringBuilder encoded = new StringBuilder();
			HashSet<int> residentIds = new HashSet<int>();
			HashSet<string> objectIds = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Attendees.Length; i++)
			{
				KingdomRiteAttendee row = Attendees[i];
				if (!ValidRiteAttendee(row) || !residentIds.Add(row.ResidentId)
					|| !objectIds.Add(row.ObjectId))
				{
					return "";
				}
				if (i > 0) encoded.Append('\n');
				encoded.Append("v1|").Append(row.ResidentId.ToString(CultureInfo.InvariantCulture))
					.Append('|').Append(ToBase64(row.ObjectId))
					.Append('|').Append(ToBase64(row.Name))
					.Append('|').Append(ToBase64(row.ZoneId))
					.Append('|').Append(row.OriginalX.ToString(CultureInfo.InvariantCulture))
					.Append('|').Append(row.OriginalY.ToString(CultureInfo.InvariantCulture))
					.Append('|').Append(ToBase64(row.Post))
					.Append('|').Append(ToBase64(row.Home))
					.Append('|').Append(row.RiteX.ToString(CultureInfo.InvariantCulture))
					.Append('|').Append(row.RiteY.ToString(CultureInfo.InvariantCulture));
				if (encoded.Length > MaxRiteManifestChars) return "";
			}
			return encoded.ToString();
		}

		public static bool TryDecodeRiteManifest(string Manifest,
			out KingdomRiteAttendee[] Attendees)
		{
			Attendees = Array.Empty<KingdomRiteAttendee>();
			if (string.IsNullOrEmpty(Manifest) || Manifest.Length > MaxRiteManifestChars)
			{
				return false;
			}
			string[] lines = Manifest.Split('\n');
			if (lines.Length == 0 || lines.Length > MaxRiteAttendees) return false;
			KingdomRiteAttendee[] rows = new KingdomRiteAttendee[lines.Length];
			HashSet<int> residentIds = new HashSet<int>();
			HashSet<string> objectIds = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < lines.Length; i++)
			{
				string[] p = lines[i].Split('|');
				int id, ox, oy, rx, ry;
				string objectId, name, zone, post, home;
				if (p.Length != 11 || p[0] != "v1"
					|| !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out id)
					|| !TryFromBase64(p[2], out objectId)
					|| !TryFromBase64(p[3], out name)
					|| !TryFromBase64(p[4], out zone)
					|| !int.TryParse(p[5], NumberStyles.None, CultureInfo.InvariantCulture, out ox)
					|| !int.TryParse(p[6], NumberStyles.None, CultureInfo.InvariantCulture, out oy)
					|| !TryFromBase64(p[7], out post)
					|| !TryFromBase64(p[8], out home)
					|| !int.TryParse(p[9], NumberStyles.None, CultureInfo.InvariantCulture, out rx)
					|| !int.TryParse(p[10], NumberStyles.None, CultureInfo.InvariantCulture, out ry))
				{
					return false;
				}
				rows[i] = new KingdomRiteAttendee(id, objectId, name, zone, ox, oy,
					post, home, rx, ry);
				if (!ValidRiteAttendee(rows[i]) || !residentIds.Add(id)
					|| !objectIds.Add(objectId)) return false;
			}
			if (!string.Equals(EncodeRiteManifest(rows), Manifest, StringComparison.Ordinal))
			{
				return false;
			}
			Attendees = rows;
			return true;
		}

		private static bool ValidRiteAttendee(KingdomRiteAttendee Row)
		{
			return Row.ResidentId > 0 && !string.IsNullOrEmpty(Row.ObjectId)
				&& !string.IsNullOrEmpty(Row.Name) && !string.IsNullOrEmpty(Row.ZoneId)
				&& Row.OriginalX >= 0 && Row.OriginalX <= 4096
				&& Row.OriginalY >= 0 && Row.OriginalY <= 4096
				&& Row.RiteX >= 0 && Row.RiteX <= 4096
				&& Row.RiteY >= 0 && Row.RiteY <= 4096
				&& Row.ObjectId.Length <= 512 && Row.Name.Length <= 512
				&& Row.ZoneId.Length <= 1024 && Row.Post.Length <= 1024
				&& Row.Home.Length <= 1024;
		}

		private static string ToBase64(string Text)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? ""));
		}

		private static bool TryFromBase64(string Encoded, out string Text)
		{
			Text = "";
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded ?? "");
				Text = new UTF8Encoding(false, true).GetString(bytes);
				return Convert.ToBase64String(bytes) == Encoded;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Runs one engine body transfer and then repairs every player-system registration from
		/// the body identity that actually won. <c>GamePlayer.SetBody</c> assigns its body before
		/// it raises <c>AfterPlayerBodyChangeEvent</c>. That dispatch may throw, or may stop on a
		/// handler returning false even though <c>SetBody</c> itself returns normally. Therefore
		/// neither the event nor one system's handler is the transaction boundary: the explicit
		/// isolated sweep is. The delegates keep both engine fault shapes directly testable here.
		/// </summary>
		internal static KingdomPlayerBodyTransfer TrySetBodyAndRebindPlayerSystems<TBody, TSystem>(
			TBody Original, TBody Target, Action<TBody> SetBody, Func<TBody> ReadCurrentBody,
			IList<TSystem> Systems, Action<TSystem, TBody> Unregister,
			Action<TSystem, TBody> Register)
			where TBody : class where TSystem : class
		{
			Exception failure = null;
			if (Original == null || Target == null || SetBody == null || ReadCurrentBody == null
				|| Systems == null || Unregister == null || Register == null)
			{
				return new KingdomPlayerBodyTransfer(false, false, false, false, 1,
					new ArgumentNullException("body transfer seam"));
			}

			bool returnedClean = false;
			try
			{
				SetBody(Target);
				returnedClean = true;
			}
			catch (Exception ex)
			{
				// The assignment may already have happened. Read and repair below before returning.
				failure = ex;
			}

			TBody current;
			try
			{
				current = ReadCurrentBody();
			}
			catch (Exception ex)
			{
				if (failure == null)
				{
					failure = ex;
				}
				return new KingdomPlayerBodyTransfer(returnedClean, false, false, false,
					1, failure);
			}

			bool targetControls = ReferenceEquals(current, Target);
			bool originalControls = ReferenceEquals(current, Original);
			if (current == null)
			{
				if (failure == null)
				{
					failure = new InvalidOperationException(
						"The body transfer ended without a controlled body.");
				}
				return new KingdomPlayerBodyTransfer(returnedClean, false, false, false,
					1, failure);
			}

			int registrationFailures = 0;
			for (int i = 0; i < Systems.Count; i++)
			{
				TSystem system = Systems[i];
				if (system == null)
				{
					registrationFailures++;
					if (failure == null)
					{
						failure = new InvalidOperationException(
							"The player-system list contains a null entry.");
					}
					continue;
				}
				// A torn forward event can leave some systems on either participant. Remove both
				// non-current candidates before the de-duplicating exact registration.
				if (!ReferenceEquals(current, Original)
					&& !TryPlayerRegistration(delegate { Unregister(system, Original); },
						ref failure))
				{
					registrationFailures++;
				}
				if (!ReferenceEquals(Target, Original) && !ReferenceEquals(current, Target)
					&& !TryPlayerRegistration(delegate { Unregister(system, Target); },
						ref failure))
				{
					registrationFailures++;
				}
				if (!TryPlayerRegistration(delegate { Register(system, current); }, ref failure))
				{
					registrationFailures++;
				}
			}
			return new KingdomPlayerBodyTransfer(returnedClean, originalControls,
				targetControls, registrationFailures == 0, registrationFailures, failure);
		}

		private static bool TryPlayerRegistration(Action Operation, ref Exception Failure)
		{
			try
			{
				Operation();
				return true;
			}
			catch (Exception ex)
			{
				if (Failure == null)
				{
					Failure = ex;
				}
				return false;
			}
		}

		internal static bool MayTerminalAfterAccessionFailure(bool CarriersExactlyOriginal,
			bool FounderControls)
		{
			return CarriersExactlyOriginal && FounderControls;
		}

		internal static bool MayQueueAccessionRepair(bool ExactHeirControls,
			bool PlayerRegistrationsExact)
		{
			return ExactHeirControls && PlayerRegistrationsExact;
		}

		internal static bool SuccessionEnabled(bool CurrentReadFailed, bool PersistedDisabled)
		{
			return !CurrentReadFailed && !PersistedDisabled;
		}

		internal static bool TryValidateSavedState(int SuccessionOrdinal, string PendingDeathToken,
			string CompletedDeathToken, InterregnumPhase Phase, long DueTick, NewsRoad Road,
			int Days, bool HasAccessionRepair, string PendingSealToken, out string Failure)
		{
			Failure = "";
			if (SuccessionOrdinal < 0 || SuccessionOrdinal == int.MaxValue
				|| !Enum.IsDefined(typeof(InterregnumPhase), Phase)
				|| !Enum.IsDefined(typeof(NewsRoad), Road) || DueTick < 0L
				|| Days < 0 || Days > RumourDays)
			{
				Failure = "the succession counters or enums are out of bounds";
				return false;
			}
			string pending = PendingDeathToken ?? "";
			string completed = CompletedDeathToken ?? "";
			string seal = PendingSealToken ?? "";
			int pendingOrdinal;
			long pendingTick;
			int completedOrdinal;
			long completedTick;
			if ((pending.Length > 0 && !TryReadDeathToken(pending, out pendingOrdinal, out pendingTick))
				|| (completed.Length > 0
					&& !TryReadDeathToken(completed, out completedOrdinal, out completedTick))
				|| (seal.Length > 0 && !TryReadDeathToken(seal, out completedOrdinal, out completedTick)))
			{
				Failure = "a founder-death token is malformed or out of bounds";
				return false;
			}
			if ((SuccessionOrdinal == 0) != (completed.Length == 0)
				|| (completed.Length > 0
					&& (!TryReadDeathToken(completed, out completedOrdinal, out completedTick)
						|| completedOrdinal != SuccessionOrdinal))
				|| (seal.Length > 0 && seal != completed))
			{
				Failure = "the completed succession identity does not match its ordinal";
				return false;
			}

			bool pendingPhase = Phase == InterregnumPhase.WordOnTheRoad
				|| Phase == InterregnumPhase.RiteDue;
			if (pendingPhase != (pending.Length > 0)
				|| HasAccessionRepair && Phase != InterregnumPhase.RiteDue)
			{
				Failure = "the pending death identity does not match its phase";
				return false;
			}
			if (pendingPhase)
			{
				if (!TryReadDeathToken(pending, out pendingOrdinal, out pendingTick)
					|| pendingOrdinal != SuccessionOrdinal + 1 || pending == completed
					|| NewsDueTick(pendingTick, Days) != DueTick || !RoadFitsDays(Road, Days))
				{
					Failure = "the pending death schedule is incoherent";
					return false;
				}
			}
			else if (DueTick != 0L || Days != 0 || HasAccessionRepair)
			{
				Failure = "an idle or reigning succession carries pending schedule state";
				return false;
			}
			if (Phase == InterregnumPhase.Reigning && SuccessionOrdinal == 0)
			{
				Failure = "a reigning state has no completed succession";
				return false;
			}
			return true;
		}

		internal static bool TryReadDeathToken(string Token, out int Ordinal, out long DeathTick)
		{
			Ordinal = 0;
			DeathTick = 0L;
			if (string.IsNullOrEmpty(Token) || Token.Length > MaxDeathTokenChars)
			{
				return false;
			}
			string[] pieces = Token.Split(':');
			if (pieces.Length != 4 || pieces[0] != "v1"
				|| !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out Ordinal)
				|| Ordinal < 1
				|| !long.TryParse(pieces[2], NumberStyles.None, CultureInfo.InvariantCulture, out DeathTick)
				|| DeathTick < 0L || pieces[3].Length == 0)
			{
				Ordinal = 0;
				DeathTick = 0L;
				return false;
			}
			try
			{
				byte[] identity = Convert.FromBase64String(pieces[3]);
				string decoded = new UTF8Encoding(false, true).GetString(identity);
				return decoded.Length > 0 && Convert.ToBase64String(identity) == pieces[3]
					&& FounderDeathToken(Ordinal, DeathTick, decoded) == Token;
			}
			catch
			{
				Ordinal = 0;
				DeathTick = 0L;
				return false;
			}
		}

		private static bool RoadFitsDays(NewsRoad Road, int Days)
		{
			switch (Road)
			{
			case NewsRoad.Seat:
			case NewsRoad.Arch:
				return Days == 0;
			case NewsRoad.Road:
				return Days > 0 && Days <= RumourDays;
			case NewsRoad.Rumour:
				return Days == RumourDays;
			default:
				return false;
			}
		}

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

		// ==================================================================================
		// The law (Addendum 22 C3)
		// ==================================================================================

		/// <summary>
		/// Which candidate the realm raises. Seniority is <c>KingdomOffices.UpdateOffice</c>'s own
		/// rule &mdash; the settler who has served longest &mdash; asked a second time and for a
		/// second purpose, which is what makes config B free of new machinery.
		/// <para>
		/// Ties are broken by name and then by resident id rather than left to enumeration order,
		/// because two settlers who arrived on the same tick is the ordinary case for a growth pass
		/// that seated three of them, and a realm that raised a different heir depending on how its
		/// rows happened to be sorted would not be keeping a law at all.
		/// </para>
		/// </summary>
		/// <param name="Candidates">Everyone the realm knows about. Nulls and empty names are skipped.</param>
		/// <param name="Law">The realm's declared custom.</param>
		/// <param name="Designee">The named designee, for <see cref="SuccessionLaw.Designee"/>.</param>
		/// <param name="Index">Index into <paramref name="Candidates"/>, or -1.</param>
		/// <returns>True when somebody may take the charter.</returns>
		public static bool TryChooseHeir(KingdomHeir[] Candidates, SuccessionLaw Law, string Designee, out int Index)
		{
			Index = -1;
			if (Candidates == null)
			{
				return false;
			}
			if (Law == SuccessionLaw.Designee && !string.IsNullOrEmpty(Designee))
			{
				for (int i = 0; i < Candidates.Length; i++)
				{
					if (Eligible(Candidates[i]) && string.Equals(Candidates[i].Name, Designee, StringComparison.Ordinal))
					{
						Index = i;
						return true;
					}
				}
			}
			for (int i = 0; i < Candidates.Length; i++)
			{
				if (!Eligible(Candidates[i]))
				{
					continue;
				}
				if (Index < 0 || Senior(Candidates[i], Candidates[Index]))
				{
					Index = i;
				}
			}
			return Index >= 0;
		}

		/// <summary>Whether a candidate may be raised at all: a name, and still on the roll.</summary>
		public static bool Eligible(KingdomHeir Candidate)
		{
			return !string.IsNullOrEmpty(Candidate.Name) && Candidate.OnTheRoll;
		}

		/// <summary>Whether <paramref name="A"/> outranks <paramref name="B"/> under seniority.</summary>
		public static bool Senior(KingdomHeir A, KingdomHeir B)
		{
			if (A.ArrivedTick != B.ArrivedTick)
			{
				return A.ArrivedTick < B.ArrivedTick;
			}
			int byName = string.CompareOrdinal(A.Name ?? "", B.Name ?? "");
			if (byName != 0)
			{
				return byName < 0;
			}
			return A.ResidentId < B.ResidentId;
		}

		/// <summary>
		/// Whether the run continues, and if not, which honest ending it takes. The order is frozen:
		/// the mode first, because Classic and Roleplay must never reach any of this; then the realm,
		/// because an unfounded death is an ordinary death; then the roll; then the ground.
		/// </summary>
		public static SuccessionVerdict Judge(bool ModeOn, bool Founded, bool AnyHeir, bool HeirReachable)
		{
			if (!ModeOn)
			{
				return SuccessionVerdict.NotKingdomMode;
			}
			if (!Founded)
			{
				return SuccessionVerdict.Unfounded;
			}
			if (!AnyHeir)
			{
				return SuccessionVerdict.NoHeir;
			}
			if (!HeirReachable)
			{
				return SuccessionVerdict.HeirUnreachable;
			}
			return SuccessionVerdict.Succeeds;
		}

		/// <summary>Whether the line has run out, which is the one verdict that ends the run through
		/// Qud's own door: score, tombstone, and whatever the mode does with the save.</summary>
		public static bool DynastyEnds(SuccessionVerdict Verdict)
		{
			return Verdict == SuccessionVerdict.NoHeir || Verdict == SuccessionVerdict.HeirUnreachable;
		}

		// ==================================================================================
		// What the realm thinks of the heir on the day (Addendum 22 C4)
		// ==================================================================================

		/// <summary>Regard the heir cannot start below, however badly their row reads.</summary>
		public const int AccessionRegardFloor = -200;

		/// <summary>
		/// Regard the heir cannot start above. Below <c>KingdomExileRules.RegardLiked</c> on purpose
		/// and by construction: a realm may be glad of its heir and must never begin by trusting
		/// them. Trust is what the run is for.
		/// </summary>
		public const int AccessionRegardCeiling = 200;

		/// <summary>What one month of service is worth to the people who watched it.</summary>
		public const int RegardPerMonthServed = 10;

		/// <summary>Months past which longer service buys nothing more. Ten, so a settler of a
		/// year's standing and one of five years' standing are both simply old hands.</summary>
		public const int MonthsServedCap = 10;

		/// <summary>Held the realm's declared creed.</summary>
		public const int RegardForDeclaredCreed = 75;

		/// <summary>Held the realm's declared creed once and left it (Addendum 16's kept roll). The
		/// realm remembers, and a realm that did not would be a realm with no creed worth leaving.</summary>
		public const int RegardForCreedLeft = -75;

		/// <summary>Already held the settlement's one office when the founder fell.</summary>
		public const int RegardForOffice = 50;

		/// <summary>
		/// What the realm's own faction holds the heir at on the day they take the charter, derived
		/// from the heir's row and from nothing else &mdash; which is the whole of C4: the founder's
		/// diplomatic ledger is the founder's, it dies with them, and the one cell with a better
		/// answer than zero is the realm's own, because the realm actually knew this person.
		/// <para>
		/// Zero is indifference, and it is the honest default: <c>KingdomExileRules</c> already rules
		/// that a realm taking somebody back opens the gate and does not smile
		/// (<c>RegardFloorOnReturn</c>), and a realm burying its founder has even less to smile
		/// about. Everything above zero here was earned by the heir before anyone thought of them as
		/// an heir.
		/// </para>
		/// </summary>
		/// <param name="ArrivedTick">When the heir came.</param>
		/// <param name="NowTick">The tick of the accession.</param>
		/// <param name="CreedMatchesRealm">The heir holds the realm's declared creed.</param>
		/// <param name="OnceLeftRealmCreed">The heir's kept roll names the realm's declared creed.</param>
		/// <param name="HoldsOffice">The heir already held the office.</param>
		public static int AccessionRegard(long ArrivedTick, long NowTick, bool CreedMatchesRealm, bool OnceLeftRealmCreed, bool HoldsOffice)
		{
			int regard = MonthsServed(ArrivedTick, NowTick) * RegardPerMonthServed;
			if (CreedMatchesRealm)
			{
				regard += RegardForDeclaredCreed;
			}
			if (OnceLeftRealmCreed)
			{
				regard += RegardForCreedLeft;
			}
			if (HoldsOffice)
			{
				regard += RegardForOffice;
			}
			if (regard < AccessionRegardFloor)
			{
				return AccessionRegardFloor;
			}
			return (regard > AccessionRegardCeiling) ? AccessionRegardCeiling : regard;
		}

		/// <summary>Whole months of service, capped at <see cref="MonthsServedCap"/>. A row whose
		/// arrival is in the future &mdash; which a save carried across a clock rework can hold
		/// &mdash; counts as no service rather than as negative service.</summary>
		public static int MonthsServed(long ArrivedTick, long NowTick)
		{
			if (ArrivedTick <= 0L || NowTick <= ArrivedTick)
			{
				return 0;
			}
			long days = (NowTick - ArrivedTick) / KingdomRules.TicksPerDay;
			long months = days / DaysPerMonth;
			return (months > MonthsServedCap) ? MonthsServedCap : (int)months;
		}

		// ==================================================================================
		// The ledger scrub (Addendum 22 C4/C5, QB-2/QB-3/QB-4)
		// ==================================================================================

		/// <summary>
		/// Whether the honesty rule forgets one journal entry.
		/// <para>
		/// Three rulings meet in this one table, and it is worth naming which is which. C5 says the
		/// founder's journal dies with the founder. <b>QB-3</b> exempts accomplishments: they are the
		/// realm's own record, they feed vanilla's mural machinery unfiltered
		/// (<c>D/XRL/World/Parts/PlayerMuralController.cs:232-233</c> reads the list without checking
		/// <c>Revealed</c>), and forgetting them would rewrite the founder's history out of the walls
		/// rather than out of anyone's memory. <b>QB-2</b> keeps the chart: vanilla marks map notes
		/// unforgettable outright (<c>D/Qud/API/JournalMapNote.cs:305-308</c> returns false
		/// unconditionally), so forcing it would mean field surgery against the engine's own intent
		/// for the one inheritance players actually treasure.
		/// </para>
		/// <para>
		/// Everything else is forgotten if and only if the engine agrees it is forgettable, which is
		/// how the sultan notes that function as map knowledge keep themselves
		/// (<c>D/Qud/API/JournalSultanNote.cs:91-111</c>).
		/// </para>
		/// </summary>
		/// <param name="Kind">Which list the entry lives in.</param>
		/// <param name="EngineForgettable">What the entry's own <c>Forgettable()</c> answered.</param>
		public static bool Forgets(JournalKind Kind, bool EngineForgettable)
		{
			if (Kind == JournalKind.Accomplishment || Kind == JournalKind.MapNote)
			{
				return false;
			}
			return EngineForgettable;
		}

		/// <summary>Namespaced prefix for the per-entry attribute that records which founder knew a
		/// thing, so the corpse-read can give back exactly what that founder lost and nothing else.
		/// <c>Attributes</c> is already used semantically by vanilla's own amnesia
		/// (<c>D/XRL/World/Parts/Mutation/Amnesia.cs:61-75</c>), so this rides a shipped surface.</summary>
		public const string FounderAttributePrefix = "taf:founder:";

		/// <summary>Quest metadata belongs to Qud's game-scoped ledger. Succession never moves,
		/// fails, completes, hides, or rewrites that state; these keys support only the v1.5
		/// flavor layer settled in QUEST-HANDLING-RESEARCH.md.</summary>
		public const string InheritedQuestMarker = "taf:succession:inherited:v1";
		public const string InheritedQuestSuffix = " (inherited)";
		public const string QuestOriginAttribute = "taf:succession:quest-origin:v1";
		public const int MaxQuestIdentityChars = 1024;
		public const int MaxQuestTellingLabelChars = 512;

		private static readonly HashSet<string> PersonalQuestIds = new HashSet<string>(
			StringComparer.Ordinal)
		{
			"Fetch Argyve a Knickknack",
			"Fetch Argyve Another Knickknack",
			"Weirdwire Conduit... Eureka!",
			"A Canticle for Barathrum",
			"A Signal in the Noise",
			"O Glorious Shekhinah!",
			"The Assessment",
			"Pax Klanq, I Presume?",
			"Petals on the Wind",
			"Find Eskhind",
			"Love and Fear",
			"Kith and Kin",
			"If, Then, Else"
		};

		/// <summary>Whether an open vanilla quest is one of the documented person-tinted class-B
		/// undertakings. ID is authoritative; name is only the compatibility fallback for a
		/// synthetic or older quest record with no ID.</summary>
		public static bool PersonalQuest(string QuestId, string QuestName)
		{
			string identity = string.IsNullOrEmpty(QuestId) ? WithoutInheritedSuffix(QuestName)
				: QuestId;
			return !string.IsNullOrEmpty(identity) && PersonalQuestIds.Contains(identity);
		}

		/// <summary>Live-name precedent from Reclamation, applied idempotently. This is display
		/// flavor only; the quest ID, steps, system, giver, completion, and rewards stay untouched.</summary>
		public static string InheritedQuestName(string QuestName)
		{
			if (string.IsNullOrEmpty(QuestName) || QuestName.EndsWith(InheritedQuestSuffix,
				StringComparison.Ordinal)) return QuestName;
			return QuestName + InheritedQuestSuffix;
		}

		public static string WithoutInheritedSuffix(string QuestName)
		{
			if (string.IsNullOrEmpty(QuestName) || !QuestName.EndsWith(InheritedQuestSuffix,
				StringComparison.Ordinal)) return QuestName;
			return QuestName.Substring(0, QuestName.Length - InheritedQuestSuffix.Length);
		}

		/// <summary>One exact open-quest telling for the succession chronicle.</summary>
		public static string InheritedQuestChronicle(string FounderName, string QuestName)
		{
			string founder = BoundQuestLabel(FounderName, "the founder");
			string quest = BoundQuestLabel(WithoutInheritedSuffix(QuestName), "an undertaking");
			return founder + " died with " + quest + " undone, and the heir inherited the undertaking";
		}

		/// <summary>Permanent Chronicle identity for one death and one open quest.</summary>
		public static string InheritedQuestEventId(string DeathToken, string QuestId)
		{
			string hash = SuccessionQuestHash("unfinished", DeathToken, QuestId);
			return hash == null ? null : "taf:succession:unfinished:v1:" + hash;
		}

		/// <summary>Permanent Chronicle identity for the accession rite owned by one exact founder
		/// death. The bounded digest lets the retry receipt survive a cut after either Chronicle
		/// list sink without carrying an arbitrary object-id token into the receipt key.</summary>
		public static string AccessionRiteEventId(string DeathToken)
		{
			string hash = SuccessionQuestHash("accession-rite", DeathToken, "rite");
			return hash == null ? null : "taf:succession:accession-rite:v1:" + hash;
		}

		/// <summary>Journal secret identity for one founder-death/quest-giver origin. A bounded
		/// cryptographic digest prevents arbitrary third-party quest IDs from bloating the save and
		/// prevents field-boundary aliases.</summary>
		public static string QuestOriginSecretId(string DeathToken, string QuestId)
		{
			string hash = SuccessionQuestHash("quest-origin", DeathToken, QuestId);
			return hash == null ? null : "taf:succession:quest-origin:v1:" + hash;
		}

		private static string BoundQuestLabel(string Value, string Fallback)
		{
			string value = string.IsNullOrWhiteSpace(Value) ? Fallback : Value.Trim();
			if (value.Length <= MaxQuestTellingLabelChars) return value;
			return value.Substring(0, MaxQuestTellingLabelChars - 1) + "…";
		}

		private static string SuccessionQuestHash(string Domain, string DeathToken, string QuestId)
		{
			if (string.IsNullOrEmpty(Domain) || string.IsNullOrEmpty(DeathToken)
				|| DeathToken.Length > MaxDeathTokenChars || string.IsNullOrEmpty(QuestId)
				|| QuestId.Length > MaxQuestIdentityChars) return null;
			try
			{
				byte[] bytes;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{
					writer.Write("TAF-SUCCESSION-QUEST-V1");
					writer.Write(Domain);
					writer.Write(DeathToken);
					writer.Write(QuestId);
					writer.Flush();
					bytes = stream.ToArray();
				}
				using (SHA256 sha = SHA256.Create())
				{
					if (sha == null) return null;
					byte[] digest = sha.ComputeHash(bytes);
					StringBuilder text = new StringBuilder(digest.Length * 2);
					for (int i = 0; i < digest.Length; i++)
						text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
					return text.ToString();
				}
			}
			catch
			{
				return null;
			}
		}

		/// <summary>Stable, exact identity for one founder death. The object id is encoded whole rather
		/// than hashed, so two founders can never alias through a collision.</summary>
		public static string FounderDeathToken(int Succession, long DeathTick, string FounderId)
		{
			int ordinal = (Succession < 1) ? 1 : Succession;
			long tick = (DeathTick < 0L) ? 0L : DeathTick;
			string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(FounderId ?? ""));
			return "v1:" + ordinal.ToString(CultureInfo.InvariantCulture) + ":"
				+ tick.ToString(CultureInfo.InvariantCulture) + ":" + encoded;
		}

		/// <summary>The journal attribute for one exact founder-death token.</summary>
		public static string FounderAttribute(string DeathToken)
		{
			return FounderAttributePrefix + (DeathToken ?? "");
		}

		/// <summary>Whether an entry's attribute names this founder's lost knowledge.</summary>
		public static bool StampedBy(string Attribute, string Wanted)
		{
			return !string.IsNullOrEmpty(Attribute) && string.Equals(Attribute, Wanted, StringComparison.Ordinal);
		}

		/// <summary>Pure idempotence gate for the synchronous death handler.</summary>
		public static SuccessionAttemptVerdict JudgeAttempt(string Wanted, string Pending, string Completed)
		{
			if (string.IsNullOrEmpty(Wanted))
			{
				return SuccessionAttemptVerdict.Invalid;
			}
			if (string.Equals(Wanted, Completed, StringComparison.Ordinal))
			{
				return SuccessionAttemptVerdict.AlreadyCompleted;
			}
			if (string.IsNullOrEmpty(Pending))
			{
				return SuccessionAttemptVerdict.Begin;
			}
			return string.Equals(Wanted, Pending, StringComparison.Ordinal)
				? SuccessionAttemptVerdict.DuplicatePending
				: SuccessionAttemptVerdict.Conflict;
		}

		// ==================================================================================
		// The price of choosing (Addendum 22 C13)
		// ==================================================================================

		/// <summary>
		/// Whether this accession costs the realm its seat. C13 defines config A by its price:
		/// the law's heir is free, and choosing one is not. The orchestrator's ruling under the
		/// author's delegation is that the price is on by default, because choice being free and
		/// consequence not is Qud's own posture.
		/// </summary>
		public static bool CostsTheSeat(HeirChoice Choice, bool SeatCostEnabled)
		{
			return Choice == HeirChoice.Chosen && SeatCostEnabled;
		}

		// ==================================================================================
		// The telling
		// ==================================================================================

		/// <summary>The line the chronicle keeps for a founder's death. No trailing period, the
		/// register the rest of the chronicle is written in.</summary>
		public static string FallenChronicle(string FounderName, string SeatName, string CauseClause)
		{
			string who = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			string how = string.IsNullOrEmpty(CauseClause) ? "was lost, and no one living can say how" : CauseClause;
			return who + ", who founded " + where + ", " + how;
		}

		/// <summary>What the outsiders say instead, which is never quite what happened. The
		/// disputed half of C12's rumour register.</summary>
		public static string FallenRumour(string FounderName, string SeatName)
		{
			string who = string.IsNullOrEmpty(FounderName) ? "the one who founded it" : FounderName;
			string where = string.IsNullOrEmpty(SeatName) ? "that settlement out east" : SeatName;
			return "word going about is that " + who + " will not be coming back to " + where + ", and that nobody there has said why";
		}

		/// <summary>What a founder's cairn is cut with. Deliberately not
		/// <c>KingdomOfficeRules.Epitaph</c>: that grammar says who CAME to the settlement, and the
		/// founder is the one person of whom it was never true.</summary>
		public static string FounderEpitaph(string FounderName, string SeatName, string Region, string CauseClause)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("Here is remembered ").Append(string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName);
			builder.Append(", who poured out the first water at ").Append(string.IsNullOrEmpty(SeatName) ? "this place" : SeatName);
			if (!string.IsNullOrEmpty(Region))
			{
				builder.Append(" in ").Append(Region);
			}
			builder.Append(" and ").Append(string.IsNullOrEmpty(CauseClause) ? "was lost, and no one living can say how" : CauseClause);
			builder.Append(". The water was shared, and is shared still.");
			return builder.ToString();
		}

		/// <summary>
		/// What the heir is told the moment they find themselves alive and somewhere else. Spoken
		/// once, at the swap, before the realm knows anything at all.
		/// </summary>
		public static string WakingLine(string HeirName, string SeatName)
		{
			string who = string.IsNullOrEmpty(HeirName) ? "somebody else" : "{{C|" + HeirName + "}}";
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : "{{C|" + SeatName + "}}";
			return "You are " + who + ", and you are standing in " + where + ". Somewhere a long way from here, the founder is dead, and nobody has been told.";
		}

		/// <summary>How the word came, said in the rite's own voice. Named roads only, never a
		/// number of days on screen.</summary>
		public static string RoadClause(NewsRoad Road, int Days)
		{
			switch (Road)
			{
			case NewsRoad.Arch:
				return "the word crossed the arch with the light, the same hour";
			case NewsRoad.Seat:
				return "there was no one to send: it happened here";
			case NewsRoad.Rumour:
				return "no road went there, so the word came the long way, hand to hand, and took " + Plural(Days, "day");
			default:
				return (Days <= 0)
					? "the word was ridden in before the day was out"
					: ("the word was ridden in, and was " + Plural(Days, "day") + " on the road");
			}
		}

		/// <summary>The chronicle's telling of the mourning rite and the accession together, because
		/// they are one occasion: a realm does not crown anybody on a day it is not burying somebody.</summary>
		public static string RiteChronicle(string SeatName, string FounderName, string HeirName, NewsRoad Road, int Days)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			string heir = string.IsNullOrEmpty(HeirName) ? "one of its own" : HeirName;
			return where + " learned that " + founder + " was dead — " + RoadClause(Road, Days)
				+ " — and held the mourning rite, and put the charter into the hands of " + heir;
		}

		/// <summary>The one semantic chronicle row for a successful succession: exact death,
		/// priced news, physical rite, in-run shrine, and accession.</summary>
		public static string SuccessionChronicle(string SeatName, string FounderName,
			string CauseClause, string HeirName, NewsRoad Road, int Days, string FixtureName)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			string heir = string.IsNullOrEmpty(HeirName) ? "one of its own" : HeirName;
			string cause = string.IsNullOrEmpty(CauseClause)
				? "died, and no one living can say how" : CauseClause;
			string fixture = string.IsNullOrEmpty(FixtureName) ? "the rite ground" : FixtureName;
			return founder + ", who founded " + where + ", " + cause + "; "
				+ RoadClause(Road, Days) + ", so its named residents present walked to " + fixture
				+ ", held the mourning rite, raised the founder's shrine-marker, and put the charter into the hands of "
				+ heir;
		}

		/// <summary>The modal the heir reads when the rite is held with them standing in it.</summary>
		public static string RiteAttendedPopup(string SeatName, string FounderName, string HeirName, NewsRoad Road, int Days)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : "{{C|" + SeatName + "}}";
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : "{{C|" + FounderName + "}}";
			string heir = string.IsNullOrEmpty(HeirName) ? "you" : "{{C|" + HeirName + "}}";
			return where + " has heard: " + RoadClause(Road, Days) + ".\n\n"
				+ "They lay out water for " + founder + ", and drink none of it, and stand in the dust until the sun is off the roofs.\n\n"
				+ "Then they turn round and look at " + heir + ", because there is nobody else left to look at.";
		}

		/// <summary>The line pushed to an heir who was not there. The rite happens whether or not
		/// anybody attends it (STANDARDS &sect;5.4), and this is how they hear.</summary>
		public static string RiteAbsentWord(string SeatName, string FounderName)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return where + " buried " + founder + " without you, and the charter was set aside with your name on it";
		}

		/// <summary>What the founder's remains offer, once, to whoever kneels at them.</summary>
		public static string CorpseReadPrompt(string FounderName)
		{
			return "Read what " + (string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName) + " knew?";
		}

		/// <summary>What reading them is like. The psychal gland's own register
		/// (<c>D/XRL/World/Parts/SecretsOnEat.cs:22-35</c>), which is Qud's established grammar for
		/// coming into another mind's knowledge.</summary>
		public static string CorpseReadLine(int Entries, int QuestMarks)
		{
			if (Entries <= 0 && QuestMarks <= 0)
			{
				return "{{K|There is nothing left in there that you did not already know.}}";
			}
			StringBuilder builder = new StringBuilder();
			builder.Append("{{W|Someone else's memories seep into your own.}}");
			if (Entries > 0)
			{
				builder.Append(" You remember ").Append(Plural(Entries, "thing")).Append(" you never learned.");
			}
			if (QuestMarks > 0)
			{
				builder.Append(" And you know where ").Append(Plural(QuestMarks, "undertaking")).Append(" began.");
			}
			return builder.ToString();
		}

		/// <summary>The map note left at a quest-giver's ground by the corpse-read. The concrete
		/// half of C5's "quest updates": no quest state is touched, and the heir is simply told
		/// where the founder took the errand on.</summary>
		public static string QuestMarkNote(string QuestName, string GiverName)
		{
			string quest = BoundQuestLabel(WithoutInheritedSuffix(QuestName), "an undertaking");
			if (string.IsNullOrEmpty(GiverName))
			{
				return "the founder's journal marks where " + quest + " began";
			}
			return "the founder's journal marks where " + quest + " began, and names "
				+ BoundQuestLabel(GiverName, "the quest-giver");
		}

		/// <summary>What the founder is told when the line ends with them. The honest ending, in
		/// the mod's own words, before Qud's own door closes.</summary>
		public static string DynastyEndPopup(string SeatName, SuccessionVerdict Verdict)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "your settlement" : "{{C|" + SeatName + "}}";
			if (Verdict == SuccessionVerdict.HeirUnreachable)
			{
				return "There is a name on the roll at " + where + ", and nobody standing under it.\n\nThe line ends here.";
			}
			return "There is nobody left at " + where + " to take the charter up.\n\nThe line ends here.";
		}

		/// <summary>The chronicle's last line, which the chronicle keeps regardless: a state that
		/// erases the chronicle is a defect (DECISIONS.md).</summary>
		public static string DynastyEndChronicle(string SeatName, string FounderName)
		{
			string where = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return founder + " died with no one on the roll to follow, and " + where + " kept no charter after that day";
		}

		/// <summary>Counted noun, Qud-plain: "one day", "three days".</summary>
		public static string Plural(int Count, string Noun)
		{
			if (Count == 1)
			{
				return "one " + Noun;
			}
			return Count.ToString() + " " + Noun + "s";
		}
	}
}
