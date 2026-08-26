using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One settler. The brink windows that today live as object properties live here instead
	/// (LIVING-CITY-ARCHITECTURE &sect;1.2(d)), because a row is what survives a zone going to disk.
	/// <para>
	/// One hundred fifteen declared bytes against the 120 &sect;0.0(c) budget, plus the one unique
	/// heap name per resident. Exact origin, frozen arrival label, bound zone, creed target and
	/// creed history are shared carrier/body references; none creates a second authority.
	/// </para>
	/// <para>
	/// <b>What W2 corrected in W1's draft.</b> The warned tick is the anchor the whole window runs
	/// from (<c>KingdomBrinkRules.WindowSpent</c>), and W1 modelled it as a <c>bool</c>, which
	/// cannot carry an anchor; and "a brink stands and the word has not gone out yet" was not
	/// representable apart from "no brink". Both are now what <c>KingdomBrink</c> always kept on
	/// the property bag, which is what let the storage swap be invisible.
	/// </para>
	/// </summary>
	internal readonly struct KingdomResidentRow
	{
		internal readonly int ResidentId;

		/// <summary>The one unique heap string per resident that &sect;0.0(c) budgets at ~64 bytes.</summary>
		internal readonly string Name;

		/// <summary>The exact provenance text. <see cref="OriginCode"/> is the fast catalogue
		/// projection for built-in origins; it cannot replace this field because guests and migrated
		/// citizens may truthfully name an origin outside that closed table.</summary>
		internal readonly string Origin;

		internal readonly int OriginCode;

		internal readonly int CreedCode;

		internal readonly long ArrivedTick;

		/// <summary>The exact dated roll label frozen when the person joined. The tick remains the
		/// only clock; this is presentation evidence, including legacy dates that cannot be parsed
		/// back into a tick without inventing one.</summary>
		internal readonly string Arrived;

		internal readonly int HomeWorkId;

		internal readonly int JobWorkId;

		internal readonly byte JobRole;

		internal readonly KingdomDayShape DayShape;

		internal readonly KingdomResidentStanding Standing;

		/// <summary>Why the row left <see cref="KingdomResidentStanding.Resident"/>.
		/// <see cref="KingdomStandingCause.None"/> while it has not.</summary>
		internal readonly KingdomStandingCause Cause;

		/// <summary>The zone the body was last bound in. The registry (&sect;3.8) is what answers
		/// whether a body is actually there; this is what the row remembers about where to look.</summary>
		internal readonly string BoundZoneId;

		/// <summary>Roof brink: <c>KingdomBrinkRoofStanding</c>, <c>RoofTick</c> and
		/// <c>RoofWarned</c>, in a row rather than in a property bag.</summary>
		internal readonly KingdomBrinkWindow RoofBrink;

		/// <summary>Creed brink, on the same terms.</summary>
		internal readonly KingdomBrinkWindow CreedBrink;

		/// <summary>The creed a creed brink pulls toward, by faction name. A shared reference:
		/// creeds are open-ended faction names and there is no code to fold one into that could be
		/// read back out again.</summary>
		internal readonly string CreedToward;

		/// <summary>The <c>ConversionChannel</c> a creed brink was reached through, so the
		/// conversion that fires at the end of the window picks the same words it would have picked
		/// on the day.</summary>
		internal readonly byte CreedChannel;

		/// <summary>
		/// The creeds this person has HELD AND LEFT, as <c>KingdomCreedRules.EncodeKept</c> stores
		/// them &mdash; bounded to <c>KingdomCreedRules.MaxKeptCreeds</c> names and joined by its
		/// separator. Addendum 16's recorded fact, in the city's own book rather than only on the
		/// settler's property bag, because a fact the city can be asked about while its people are
		/// not loaded has to live where the city does.
		/// <para>
		/// A shared reference, like <see cref="CreedToward"/> and for the same reason: creeds are
		/// open-ended faction names with no code to fold them into. The row holds the very string
		/// the settler already carries, so the eight bytes are the reference and the heap grows by
		/// nothing.
		/// </para>
		/// </summary>
		internal readonly string KeptCreeds;

		internal KingdomResidentRow(
			int residentId,
			string name,
			int originCode,
			int creedCode,
			long arrivedTick,
			int homeWorkId,
			int jobWorkId,
			byte jobRole,
			KingdomDayShape dayShape,
			KingdomResidentStanding standing,
			KingdomStandingCause cause,
			string boundZoneId,
			KingdomBrinkWindow roofBrink,
			KingdomBrinkWindow creedBrink,
			string creedToward,
			byte creedChannel)
			: this(residentId, name, originCode, creedCode, arrivedTick, homeWorkId, jobWorkId, jobRole,
				dayShape, standing, cause, boundZoneId, roofBrink, creedBrink, creedToward, creedChannel,
				null, KingdomResidentRules.OriginKey(originCode), null)
		{
		}

		internal KingdomResidentRow(
			int residentId,
			string name,
			int originCode,
			int creedCode,
			long arrivedTick,
			int homeWorkId,
			int jobWorkId,
			byte jobRole,
			KingdomDayShape dayShape,
			KingdomResidentStanding standing,
			KingdomStandingCause cause,
			string boundZoneId,
			KingdomBrinkWindow roofBrink,
			KingdomBrinkWindow creedBrink,
			string creedToward,
			byte creedChannel,
			string keptCreeds)
			: this(residentId, name, originCode, creedCode, arrivedTick, homeWorkId, jobWorkId,
				jobRole, dayShape, standing, cause, boundZoneId, roofBrink, creedBrink, creedToward,
				creedChannel, keptCreeds, KingdomResidentRules.OriginKey(originCode), null)
		{
		}

		internal KingdomResidentRow(
			int residentId,
			string name,
			int originCode,
			int creedCode,
			long arrivedTick,
			int homeWorkId,
			int jobWorkId,
			byte jobRole,
			KingdomDayShape dayShape,
			KingdomResidentStanding standing,
			KingdomStandingCause cause,
			string boundZoneId,
			KingdomBrinkWindow roofBrink,
			KingdomBrinkWindow creedBrink,
			string creedToward,
			byte creedChannel,
			string keptCreeds,
			string origin,
			string arrived)
		{
			KeptCreeds = string.IsNullOrEmpty(keptCreeds) ? null : keptCreeds;
			ResidentId = residentId;
			Name = name;
			Origin = origin ?? "";
			OriginCode = originCode;
			CreedCode = creedCode;
			ArrivedTick = arrivedTick;
			Arrived = arrived ?? "";
			HomeWorkId = homeWorkId;
			JobWorkId = jobWorkId;
			JobRole = jobRole;
			DayShape = dayShape;
			Standing = standing;
			Cause = cause;
			BoundZoneId = boundZoneId;
			RoofBrink = roofBrink;
			CreedBrink = creedBrink;
			// A creed a brink no longer stands toward is not remembered: the row would otherwise
			// keep naming a pull that has been arrested, and KingdomBrink.Lift's whole contract is
			// that a lifted brink is forgotten rather than banked.
			CreedToward = creedBrink.Stands ? (string.IsNullOrEmpty(creedToward) ? null : creedToward) : null;
			CreedChannel = creedBrink.Stands ? creedChannel : (byte)0;
		}

		/// <summary>The brink of this kind as the row holds it. Total over the enum: a kind the row
		/// has no window for reads as no brink rather than as the roof's.</summary>
		internal KingdomBrinkWindow BrinkOf(BrinkKind kind)
		{
			switch (kind)
			{
			case BrinkKind.Roof:
				return RoofBrink;
			case BrinkKind.Creed:
				return CreedBrink;
			default:
				return KingdomBrinkWindow.None;
			}
		}

		/// <summary>This row with one brink window replaced. The creed reference and channel travel
		/// with the creed window and are ignored for any other kind, which is what stops a roof
		/// brink from ever acquiring a creed.</summary>
		internal KingdomResidentRow WithBrink(BrinkKind kind, KingdomBrinkWindow window, string creedToward, byte creedChannel)
		{
			switch (kind)
			{
			case BrinkKind.Roof:
				return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick, HomeWorkId, JobWorkId,
					JobRole, DayShape, Standing, Cause, BoundZoneId, window, CreedBrink, CreedToward,
					CreedChannel, KeptCreeds, Origin, Arrived);
			case BrinkKind.Creed:
				return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick, HomeWorkId, JobWorkId,
					JobRole, DayShape, Standing, Cause, BoundZoneId, RoofBrink, window, creedToward,
					creedChannel, KeptCreeds, Origin, Arrived);
			default:
				return this;
			}
		}

		/// <summary>This row standing somewhere else, with the reason. The transition RULES live in
		/// <c>KingdomResidentRules</c>; this is only how a row is rewritten once they have
		/// allowed it.</summary>
		internal KingdomResidentRow WithStanding(KingdomResidentStanding standing, KingdomStandingCause cause)
		{
			return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick, HomeWorkId, JobWorkId,
				JobRole, DayShape, standing, cause, BoundZoneId, RoofBrink, CreedBrink, CreedToward,
				CreedChannel, KeptCreeds, Origin, Arrived);
		}

		/// <summary>This row bound to other ground. Placement is W3; what W2 ships is the fact that
		/// the row knows where its body was last seen.</summary>
		internal KingdomResidentRow WithBoundZone(string boundZoneId)
		{
			return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick, HomeWorkId, JobWorkId,
				JobRole, DayShape, Standing, Cause, boundZoneId, RoofBrink, CreedBrink, CreedToward,
				CreedChannel, KeptCreeds, Origin, Arrived);
		}

		/// <summary>This row with what the ground says about the person: their name, where they came
		/// from, what they hold with, and the work they are posted to.</summary>
		internal KingdomResidentRow WithReading(string name, int originCode, int creedCode,
			int homeWorkId, int jobWorkId, byte jobRole, KingdomDayShape dayShape)
		{
			return WithReading(name, KingdomResidentRules.OriginKey(originCode), originCode, creedCode,
				homeWorkId, jobWorkId, jobRole, dayShape);
		}

		internal KingdomResidentRow WithReading(string name, string origin, int originCode,
			int creedCode, int homeWorkId, int jobWorkId, byte jobRole, KingdomDayShape dayShape)
		{
			return new KingdomResidentRow(ResidentId, name, originCode, creedCode, ArrivedTick, homeWorkId, jobWorkId,
				jobRole, dayShape, Standing, Cause, BoundZoneId, RoofBrink, CreedBrink, CreedToward,
				CreedChannel, KeptCreeds, origin, Arrived);
		}

		/// <summary>This row with the creeds the person has held and left. A separate reading from
		/// <see cref="WithReading"/> because it is the one column that only ever GROWS: a history
		/// that came back empty is a settler whose property bag has not been read yet, never a life
		/// that un-happened, so an empty reading leaves what the row already remembers alone.</summary>
		internal KingdomResidentRow WithKeptCreeds(string keptCreeds)
		{
			if (string.IsNullOrEmpty(keptCreeds))
			{
				return this;
			}
			return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick, HomeWorkId, JobWorkId,
				JobRole, DayShape, Standing, Cause, BoundZoneId, RoofBrink, CreedBrink, CreedToward,
				CreedChannel, keptCreeds, Origin, Arrived);
		}
	}
}
