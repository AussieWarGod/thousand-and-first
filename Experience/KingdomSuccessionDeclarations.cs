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
		Chosen,

		/// <summary>The realm raised an exact resident through its durable grooming custom.
		/// Unlike the founder's one-life choice, this is a lawful accession and never costs the
		/// successor the Charter.</summary>
		Groomed
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

		/// <summary>The zone their body was last bound in, or null when the realm never bound one.</summary>
		public readonly string BoundZoneId;

		/// <summary>Their resident id, or zero. Used only to break a tie no other field breaks.</summary>
		public readonly int ResidentId;

		public KingdomHeir(string name, long arrivedTick, string creed, string keptCreeds,
			bool onTheRoll, string boundZoneId, int residentId)
		{
			Name = name;
			ArrivedTick = arrivedTick;
			Creed = creed;
			KeptCreeds = keptCreeds;
			OnTheRoll = onTheRoll;
			BoundZoneId = boundZoneId;
			ResidentId = residentId;
		}
	}
}
