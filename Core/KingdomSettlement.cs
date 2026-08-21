using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// Raised when a seat does not carry every field a settlement holds. Thrown before any
	/// value is written, so a mismatched seat is refused whole rather than half-filled.
	/// </summary>
	[Serializable]
	public class KingdomSeatMismatchException : Exception
	{
		public KingdomSeatMismatchException(string Message)
			: base(Message)
		{
		}
	}

	/// <summary>
	/// What a settlement is, apart from the founder standing in it: one city's whole state.
	/// <para>
	/// The realm keeps its faction, its standings and its chronicle; a city keeps its ground,
	/// its people, its water and its clocks. One faction, one history, two places it happened.
	/// </para>
	/// <para>
	/// The settlement the founder is standing in &mdash; the seat &mdash; lives in
	/// <see cref="KingdomSystem"/>'s flat fields, because that is what every consumer already
	/// reads. This record is the other city, and the two are exchanged when the founder walks
	/// from one to the other. The exchange is by field name (see <see cref="ReadFrom"/>), so a
	/// field added here is carried the moment a same-named field exists on the seat, and refused
	/// loudly when one does not.
	/// </para>
	/// </summary>
	[Serializable]
	public class KingdomSettlement
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>How many cities one realm may hold. Two is the whole of it: a second city
		/// is a place with a purpose, and a third is a management screen.</summary>
		public const int MaxSettlements = 2;

		/// <summary>The vocation every site offers, and the one a founder who will not commit
		/// receives. It promises nothing, which is the point.</summary>
		public const string NeutralVocation = "holding";

		/// <summary>The vocations a second city may be founded for. Order is menu order;
		/// <see cref="NeutralVocation"/> comes last because it is the refusal.</summary>
		public static readonly string[] Vocations = new string[4] { "waystation", "refuge", "reliquary", NeutralVocation };

		/// <summary>What each vocation asks of the city, for the founding menu. One sentence of
		/// what it is, one of what it costs nobody.</summary>
		public static readonly string[] VocationBlurbs = new string[4]
		{
			"Water sold to whoever passes, and news given free. The city exists so the road has a middle.",
			"A wall and a bed for whoever arrives with nothing. The city exists to have room.",
			"What the realm means to keep is kept here. The city exists to remember.",
			"Ground held for its own sake, and no promise made about it. Nothing is closed off by this."
		};

		/// <summary>This settlement's own name. The realm's display name is a separate thing and
		/// does not change when a second city is founded.</summary>
		public string SettlementName;

		/// <summary>What the city was founded for, from <see cref="Vocations"/>, or null for a
		/// city founded before the realm had a second one to distinguish it from.</summary>
		public string Vocation;

		public string Style = "common";

		public string FoundingTerrainBlueprint;

		public string FoundingRegionName;

		public int FoundingZLevel;

		public long FoundedTick;

		public GrowthStage Stage = GrowthStage.Camp;

		public int Population;

		public int DryStreak;

		public bool Withered;

		/// <summary>Resolves in a row this city's ration bill went unpaid. Carried, so a city
		/// left hungry is still hungry when the founder walks back into it.</summary>
		public int HungerStreak;

		/// <summary>Whether this city stands marked by a long hunger. Carried beside
		/// <see cref="Withered"/>; both can be true at once.</summary>
		public bool Famished;

		public bool HasShopkeeper;

		public bool NoRoomAnnounced;

		public long LastHeartbeatTick;

		/// <summary>Tick this city's people last fetched water. Carried, so a dormant city does
		/// not fetch a windfall the moment it is seated.</summary>
		public long LastFetchTick;

		/// <summary>Tick this city's water works last produced. Carried, so a dormant city's
		/// works do not pour a windfall the moment it is seated.</summary>
		public long LastWaterWorkTick;

		/// <summary>Tick this city's fields last brought a harvest in. Carried for the reason
		/// <see cref="LastWaterWorkTick"/> is: a dormant city's fields do not fill a granary the
		/// moment it is seated.</summary>
		public long LastFoodWorkTick;

		/// <summary>Citizens of this city crewing works. Carried with the city.</summary>
		public int AssignedCrew;

		/// <summary>Settlers this city has on the water detail. Carried, so a city keeps its own
		/// standing orders while the founder is away in the other one.</summary>
		public int WaterCrew;

		public int IdleWorks;

		public int ShorthandedWorks;

		/// <summary>How many of the settlement's works stand damaged and run reduced
		/// (Addendum 7). Counted fresh by <c>KingdomWear</c> on every attended pass; carried
		/// between seats beside <see cref="IdleWorks"/> and <see cref="ShorthandedWorks"/>.
		/// </summary>
		public int DamagedWorks;

		public bool IdleWorksAnnounced;

		/// <summary>Whether this city has already been told its harvest has nowhere to go
		/// (STANDARDS 7b). Carried, so walking away and back does not re-say it.</summary>
		public bool HarvestUnstoredAnnounced;

		/// <summary>
		/// Tick this settlement's level was last reckoned against its people
		/// (<c>KingdomSubsidence</c>). Uncapped world time: the slide runs whether the founder is
		/// there or not, and the stamp advances by exactly the steps a reckoning cashed, keeping
		/// the part-step remainder. Carried, so a dormant city does not settle a season's worth
		/// the moment it is seated.
		/// </summary>
		public long LastSubsidenceTick;

		/// <summary>
		/// STANDARDS 7b's once-flag for the slide, and the slide's own memory of being under way.
		/// Set when a settlement is first told it is settling back, cleared the moment it arrests
		/// &mdash; and read by <c>KingdomSubsidenceRules.Slide</c> as "already sliding", because a
		/// slide that has been announced converges to the level rather than stopping at the band's
		/// edge. The announcement and the hysteresis are the same fact, so they are one field.
		/// </summary>
		public bool SubsidenceAnnounced;

		/// <summary>
		/// People this settlement's finished works honestly carry, as of the last attended pass
		/// (<c>KingdomSubsidenceRules.SupportedLevel</c>). Knowledge, not truth: it is exactly as
		/// stale as the last visit. Zero on a settlement no pass has measured yet.
		/// </summary>
		public int SupportedLevel;

		/// <summary>
		/// Which of <c>KingdomCatalogueRules.BindingSupports</c> is holding
		/// <see cref="SupportedLevel"/> where it is, so the level can always say why (7b). Null
		/// until a pass has measured it, and read back through
		/// <c>KingdomSubsidenceRules.NormalizedBinding</c> rather than repaired in
		/// <c>Normalize</c> &mdash; the seat swap's contract is a byte-for-byte round trip, and
		/// what actually needs preventing is a sentence blaming a good this build cannot name.
		/// </summary>
		public string SubsidenceBinding;

		/// <summary>
		/// What this settlement's named notable is worth to the level
		/// (<c>KingdomCeremonyRules.NotableShade</c>): their met tastes, the net of their virtue
		/// and their flaw, and the <c>Prefers</c> their quarters happen to meet (Addendum 4).
		/// Written when the office is first filled or passes to somebody else, so it is exactly as
		/// stale as the last time it changed hands &mdash; knowledge, like
		/// <see cref="SupportedLevel"/>, rather than a meter. Zero for a settlement that has named
		/// nobody, which is every settlement until it has people enough to.
		/// </summary>
		public int NotableShade;


		/// <summary>
		/// What this settlement's last day's eating was worth to the level, for exactly the day
		/// it was earned (<c>KingdomRules.MealShadeFor</c>). Re-drawn every heartbeat: a
		/// settlement that ate its own dish yesterday and scraps today is worth the scraps. Rides
		/// the same lift term as <see cref="NotableShade"/> and is capped again with it by
		/// <c>KingdomCatalogueRules.LiftCapPercent</c>, so nobody eats their way past their own
		/// water. Carried, so a city left mid-feast is still well fed when the founder walks back
		/// into it.
		/// </summary>
		public int MealShade;

		/// <summary>What the settlement's last drawn day of rations actually was
		/// (<c>KingdomRules.JudgeMeal</c>). Knowledge for the report and the once-flag below;
		/// <see cref="KingdomRules.MealVerdict.None"/> on a settlement no heartbeat has billed
		/// yet.</summary>
		public KingdomRules.MealVerdict LastMeal = KingdomRules.MealVerdict.None;

		/// <summary>STANDARDS 7b's once-flag for a settlement whose larders gave nothing. Set
		/// when the sentence is said, cleared the moment the settlement eats out of its own
		/// stores again, so walking away and back does not re-say it.</summary>
		public bool ScrapsAnnounced;

		public int ShopTier;

		public long LastVisitTick;

		public string LastDeed;

		public long LastDeedTick;

		public KingdomRules.GatePolicy Gate = KingdomRules.GatePolicy.Open;

		public KingdomRules.StoresPolicy Stores = KingdomRules.StoresPolicy.Plenty;

		public int RaidState;

		public string RaidFactionName;

		public long RaidDueTick;

		public long LastRaidTick;

		public int RaidTimesDeferred;

		public List<string> RosterNames = new List<string>();

		public List<string> RosterOrigins = new List<string>();

		public List<string> RosterArrived = new List<string>();

		public Dictionary<string, int> OriginCounts = new Dictionary<string, int>();

		/// <summary>This city's own tally of settler creeds. See <see cref="KingdomCreed"/>.
		/// Per-city, so it is carried by the seat swap like <see cref="OriginCounts"/>; the realm's
		/// dissent, declared creed, and secession state are not (see <c>KingdomSystem</c>, which
		/// carries those only on itself).</summary>
		public Dictionary<string, int> CreedCounts = new Dictionary<string, int>();

		/// <summary>See <see cref="KingdomSystem.ConversionShared"/>. Per-city, so a founder who
		/// walks to the other city does not carry this one's half-turned believers with them.</summary>
		public Dictionary<string, int> ConversionShared = new Dictionary<string, int>();

		/// <summary>See <see cref="KingdomSystem.ConversionToward"/>.</summary>
		public Dictionary<string, string> ConversionToward = new Dictionary<string, string>();

		/// <summary>See <see cref="KingdomSystem.ConversionResented"/>.</summary>
		public Dictionary<string, int> ConversionResented = new Dictionary<string, int>();

		public KingdomRules.PetitionKind PetitionKind = KingdomRules.PetitionKind.None;

		public string PetitionPetitioner;

		public string PetitionFaction;

		public int PetitionTarget;

		public long PetitionIssuedTick;

		public long LastPetitionTick;

		public int PetitionsMet;

		public int Dead;

		/// <summary>See <see cref="KingdomSystem.DeadNames"/>.</summary>
		public List<string> DeadNames = new List<string>();

		/// <summary>See <see cref="KingdomSystem.DeadOrigins"/>.</summary>
		public List<string> DeadOrigins = new List<string>();

		/// <summary>See <see cref="KingdomSystem.DeadArrived"/>.</summary>
		public List<string> DeadArrived = new List<string>();

		/// <summary>See <see cref="KingdomSystem.DeadCauses"/>.</summary>
		public List<string> DeadCauses = new List<string>();

		/// <summary>See <see cref="KingdomSystem.MemorialsRaised"/>.</summary>
		public int MemorialsRaised;

		/// <summary>See <see cref="KingdomSystem.OfficeHolderName"/>.</summary>
		public string OfficeHolderName;

		/// <summary>
		/// Free space in this city's dedicated stores as of the last attended pass. Knowledge,
		/// not truth: it is exactly as stale as the founder's last visit, which is the whole
		/// point &mdash; a manifest is loaded against what the realm BELIEVES the other city can
		/// take. When the belief turns out wrong, water arrives with nowhere to go, and that is a
		/// story rather than a bug.
		/// </summary>
		public int LastKnownStorageSpace;

		/// <summary>Servings of this city's own harvest still on the road to one of its pantries.
		/// See <see cref="KingdomSystem.PendingCrop"/>. Carried, so a load gathered in the city
		/// the founder walked out of is still waiting there when they walk back in.</summary>
		public int PendingCrop;

		/// <summary>What that load physically is. See
		/// <see cref="KingdomSystem.PendingCropBlueprint"/>.</summary>
		public string PendingCropBlueprint;

		public KingdomLedger Ledger = new KingdomLedger();

		public long NextArrivalTick;

		public long NextGuestTick;

		public long GuestDepartTick;

		public bool FirstGuestGreeted;

		public long NextNotableGuestTick;

		public long NotableGuestDepartTick;

		public bool FirstNotableGuestLodged;

		public List<string> GuestbookLines = new List<string>();

		public List<string> ClaimedZones = new List<string>();

		public Dictionary<string, string> ZoneDistricts = new Dictionary<string, string>();

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomSettlement));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomSettlement));
			Normalize();
		}
#endif

		/// <summary>
		/// Repairs a settlement read from a save written by an older build, or handed in by a
		/// caller: null containers become empty ones, and a vocation this build does not know
		/// falls back to the neutral one rather than being carried as a name nothing can read.
		/// A null vocation is left null &mdash; that is the realm's first city, founded before
		/// there was a second to tell it from.
		/// </summary>
		public void Normalize()
		{
			if (RosterNames == null)
			{
				RosterNames = new List<string>();
			}
			if (RosterOrigins == null)
			{
				RosterOrigins = new List<string>();
			}
			if (RosterArrived == null)
			{
				RosterArrived = new List<string>();
			}
			if (DeadNames == null)
			{
				DeadNames = new List<string>();
			}
			if (DeadOrigins == null)
			{
				DeadOrigins = new List<string>();
			}
			if (DeadArrived == null)
			{
				DeadArrived = new List<string>();
			}
			if (DeadCauses == null)
			{
				DeadCauses = new List<string>();
			}
			if (OriginCounts == null)
			{
				OriginCounts = new Dictionary<string, int>();
			}
			if (CreedCounts == null)
			{
				CreedCounts = new Dictionary<string, int>();
			}
			if (ConversionShared == null)
			{
				ConversionShared = new Dictionary<string, int>();
			}
			if (ConversionToward == null)
			{
				ConversionToward = new Dictionary<string, string>();
			}
			if (ConversionResented == null)
			{
				ConversionResented = new Dictionary<string, int>();
			}
			if (ClaimedZones == null)
			{
				ClaimedZones = new List<string>();
			}
			if (ZoneDistricts == null)
			{
				ZoneDistricts = new Dictionary<string, string>();
			}
			if (GuestbookLines == null)
			{
				GuestbookLines = new List<string>();
			}
			if (Ledger == null)
			{
				Ledger = new KingdomLedger();
			}
			Ledger.Normalize();
			if (string.IsNullOrEmpty(Style))
			{
				Style = "common";
			}
			if (!string.IsNullOrEmpty(Vocation) && !IsKnownVocation(Vocation))
			{
				Vocation = NeutralVocation;
			}
			// A stored level or stamp below zero is a corrupt reading, not a settlement in
			// debt: subsidence mints nothing, so both fail closed to "nothing measured yet".
			if (LastSubsidenceTick < 0L)
			{
				LastSubsidenceTick = 0L;
			}
			if (SupportedLevel < 0)
			{
				SupportedLevel = 0;
			}
			// A shade below zero is a corrupt reading too: a notable is texture and never a tax,
			// so the worst any of them can be worth is nothing. Nothing clamps it from above
			// here - a shade a later build writes wider is still a number this one can read, and
			// KingdomCatalogueRules.LiftCapPercent binds whatever it is against the water.
			if (NotableShade < 0)
			{
				NotableShade = 0;
			}
			// The meal shade fails closed the same way and for the same reason: a day's
			// eating is never a tax, so the worst a bad supper can be worth is nothing.
			if (MealShade < 0)
			{
				MealShade = 0;
			}
			// The two scarcity streaks fail closed the same way, and for the same reason: a
			// negative streak is a corrupt reading, and a ladder cannot owe a settlement rungs.
			if (DryStreak < 0)
			{
				DryStreak = 0;
			}
			if (HungerStreak < 0)
			{
				HungerStreak = 0;
			}
			if (LastFoodWorkTick < 0L)
			{
				LastFoodWorkTick = 0L;
			}
			// A load in flight fails closed the same way: a negative count is a corrupt reading,
			// and a delivery cannot owe a city servings. A count with no crop name still arrives -
			// KingdomCrops.DeliverPending falls back to the city's own crop - so only the count is
			// repaired here.
			if (PendingCrop < 0)
			{
				PendingCrop = 0;
			}
		}

		/// <summary>
		/// Reads every field of this settlement from the same-named field of <paramref name="Seat"/>.
		/// <para>
		/// Preconditions: the seat carries a field of the same name and exact type for every field
		/// declared here. Side effects: this settlement's fields are overwritten and
		/// <see cref="Normalize"/> is run; the seat is not touched. Mutable containers are handed
		/// over by reference rather than cloned, which is safe only because the caller restores
		/// another settlement over the seat in the same breath &mdash; see
		/// <c>KingdomSystem.TrySeat</c>.
		/// </para>
		/// </summary>
		/// <param name="Seat">The object holding the seated settlement's flat fields.</param>
		/// <exception cref="KingdomSeatMismatchException">A field here has no counterpart there.
		/// Nothing is written when this is thrown.</exception>
		public void ReadFrom(object Seat)
		{
			if (Seat == null)
			{
				throw new KingdomSeatMismatchException("No seat was supplied to read a settlement from.");
			}
			FieldInfo[] carried = Carried;
			FieldInfo[] counterparts = Counterparts(Seat.GetType());
			// Gathered first, assigned second: a failure mid-read must not leave a settlement
			// wearing half of one city and half of another.
			object[] values = new object[carried.Length];
			for (int i = 0; i < carried.Length; i++)
			{
				values[i] = counterparts[i].GetValue(Seat);
			}
			for (int i = 0; i < carried.Length; i++)
			{
				carried[i].SetValue(this, values[i]);
			}
			Normalize();
		}

		/// <summary>
		/// Writes every field of this settlement onto the same-named field of
		/// <paramref name="Seat"/>, making it the seated city.
		/// <para>
		/// Preconditions and hand-over semantics are those of <see cref="ReadFrom"/>, reversed.
		/// <see cref="Normalize"/> runs on this settlement first, so a seat is never given a null
		/// roster or ledger.
		/// </para>
		/// </summary>
		/// <param name="Seat">The object holding the seated settlement's flat fields.</param>
		/// <exception cref="KingdomSeatMismatchException">A field here has no counterpart there.
		/// Nothing is written when this is thrown.</exception>
		public void WriteTo(object Seat)
		{
			if (Seat == null)
			{
				throw new KingdomSeatMismatchException("No seat was supplied to write a settlement to.");
			}
			Normalize();
			FieldInfo[] carried = Carried;
			FieldInfo[] counterparts = Counterparts(Seat.GetType());
			object[] values = new object[carried.Length];
			for (int i = 0; i < carried.Length; i++)
			{
				values[i] = carried[i].GetValue(this);
			}
			for (int i = 0; i < carried.Length; i++)
			{
				counterparts[i].SetValue(Seat, values[i]);
			}
		}

		/// <summary>
		/// Every field one settlement holds. Reflected rather than listed, because a hand-written
		/// list is what rots. A copy, so a caller cannot reorder the array the swap is keyed on.
		/// </summary>
		public static FieldInfo[] CarriedFields()
		{
			return (FieldInfo[])Carried.Clone();
		}

		/// <summary>
		/// Names the fields <paramref name="SeatType"/> cannot carry, with the reason. Empty means
		/// the seat is a complete home for a settlement. This is the check a tester can run
		/// (<c>kingdom:dump</c>) to prove that adding a field here did not silently start losing
		/// a city on every swap.
		/// </summary>
		/// <param name="SeatType">The type holding the seated settlement's flat fields.</param>
		/// <returns>One line per unusable field; never null.</returns>
		public static List<string> SeatMismatches(Type SeatType)
		{
			List<string> mismatches = new List<string>();
			if (SeatType == null)
			{
				mismatches.Add("(no seat type)");
				return mismatches;
			}
			foreach (FieldInfo field in Carried)
			{
				FieldInfo counterpart = FindCounterpart(SeatType, field.Name);
				if (counterpart == null)
				{
					mismatches.Add(field.Name + " (no field of that name on " + SeatType.Name + ")");
				}
				else if (counterpart.FieldType != field.FieldType)
				{
					mismatches.Add(field.Name + " (expected " + field.FieldType.Name + ", seat holds " + counterpart.FieldType.Name + ")");
				}
			}
			return mismatches;
		}

		/// <summary>True if <paramref name="Vocation"/> is one this build knows.</summary>
		public static bool IsKnownVocation(string Vocation)
		{
			for (int i = 0; i < Vocations.Length; i++)
			{
				if (Vocations[i] == Vocation)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Founder-facing clause naming what a city is for. Lower-case, article included, fit to
		/// follow "founded as " or stand after a comma.
		/// </summary>
		/// <param name="Vocation">A vocation, or null.</param>
		/// <returns>The clause; the neutral clause for anything unrecognised, empty for null.</returns>
		public static string VocationClause(string Vocation)
		{
			if (string.IsNullOrEmpty(Vocation))
			{
				return "";
			}
			switch (Vocation)
			{
			case "waystation":
				return "a waystation on the long roads";
			case "refuge":
				return "a refuge for whoever reaches it";
			case "reliquary":
				return "a reliquary for what should not be lost";
			default:
				return "a holding of the realm";
			}
		}

		/// <summary>The vocation clause as a trailing comma clause, or empty. For sentences that
		/// name the city first and its purpose second.</summary>
		public static string VocationSuffix(string Vocation)
		{
			string clause = VocationClause(Vocation);
			return string.IsNullOrEmpty(clause) ? "" : (", " + clause);
		}

		/// <summary>The menu blurb for a vocation, or empty for one this build does not know.</summary>
		public static string VocationBlurb(string Vocation)
		{
			for (int i = 0; i < Vocations.Length && i < VocationBlurbs.Length; i++)
			{
				if (Vocations[i] == Vocation)
				{
					return VocationBlurbs[i];
				}
			}
			return "";
		}

		/// <summary>
		/// Why a second founding may not proceed, or <see cref="SecondFoundingVerdict.Allowed"/>.
		/// Pure so the rule is tabled rather than discovered in the field.
		/// </summary>
		public enum SecondFoundingVerdict
		{
			Allowed,
			NothingFoundedYet,
			GroundIsAlreadyOurs,
			GroundIsTooClose,
			RealmIsFull
		}

		/// <summary>
		/// Judges whether the founding rite, performed on ground the founder is standing on,
		/// founds the realm's second city.
		/// </summary>
		/// <param name="Founded">Whether the realm exists at all.</param>
		/// <param name="SettlementsHeld">Cities the realm already holds, seat included.</param>
		/// <param name="GroundIsClaimed">Whether this zone is already the realm's ground.</param>
		/// <param name="GroundIsAdjacent">Whether this zone borders the realm's ground. Bordering
		/// ground is an expansion of a city, not a new one, and is claimed rather than founded.</param>
		public static SecondFoundingVerdict JudgeSecondFounding(bool Founded, int SettlementsHeld, bool GroundIsClaimed, bool GroundIsAdjacent)
		{
			if (!Founded)
			{
				return SecondFoundingVerdict.NothingFoundedYet;
			}
			if (SettlementsHeld >= MaxSettlements)
			{
				return SecondFoundingVerdict.RealmIsFull;
			}
			if (GroundIsClaimed)
			{
				return SecondFoundingVerdict.GroundIsAlreadyOurs;
			}
			if (GroundIsAdjacent)
			{
				return SecondFoundingVerdict.GroundIsTooClose;
			}
			return SecondFoundingVerdict.Allowed;
		}

		/// <summary>
		/// What the founder is told when the rite will not found a second city. Written as the
		/// water-keepers would say it, not as a rule.
		/// </summary>
		/// <param name="Verdict">The refusal. <see cref="SecondFoundingVerdict.Allowed"/> returns empty.</param>
		/// <param name="RealmName">The realm's display name, for the line that names it.</param>
		public static string SecondFoundingRefusal(SecondFoundingVerdict Verdict, string RealmName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : ("{{C|" + RealmName + "}}");
			switch (Verdict)
			{
			case SecondFoundingVerdict.RealmIsFull:
				return realm + " holds two cities, and two is what one founder can be in. Pouring here would only take the water from a place that already drinks it.";
			case SecondFoundingVerdict.GroundIsAlreadyOurs:
				return "This ground is already " + realm + "'s. Water poured here is water poured twice.";
			case SecondFoundingVerdict.GroundIsTooClose:
				return "This ground borders " + realm + " already. It is claimed, not founded — walk out past the horizon of what you hold if you mean to begin somewhere new.";
			case SecondFoundingVerdict.NothingFoundedYet:
				return "There is nothing to found a second city for yet.";
			default:
				return "";
			}
		}

		/// <summary>
		/// One line describing this settlement for a tester: who it is, what it is for, and the
		/// clocks that decide what it does the next time the founder stands in it.
		/// </summary>
		public string Describe()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(string.IsNullOrEmpty(SettlementName) ? "(unnamed)" : SettlementName);
			if (!string.IsNullOrEmpty(Vocation))
			{
				sb.Append(" [").Append(Vocation).Append("]");
			}
			sb.Append(" ").Append(Style).Append(" ").Append(Stage).Append(Withered ? " (withered)" : "").Append(Famished ? " (famished)" : "")
				.Append(" pop=").Append(Population)
				.Append(" dry=").Append(DryStreak)
				.Append(" hunger=").Append(HungerStreak)
				.Append(" claims=").Append(ClaimedZones.Count)
				.Append(" founded=").Append(FoundedTick)
				.Append(" visit=").Append(LastVisitTick)
				.Append(" heartbeat=").Append(LastHeartbeatTick)
				.Append(" nextArrival=").Append(NextArrivalTick)
				.Append(" raid=").Append(RaidState).Append("/").Append(RaidFactionName ?? "-")
				.Append(" petition=").Append(PetitionKind);
			return sb.ToString();
		}

		/// <summary>
		/// The same-named, same-typed seat fields for every carried field, in
		/// <see cref="CarriedFields"/> order. Cached per seat type: the answer cannot change
		/// within a run, and the swap must stay cheap enough to sit in zone activation.
		/// </summary>
		/// <exception cref="KingdomSeatMismatchException">The seat cannot carry a settlement.</exception>
		private static FieldInfo[] Counterparts(Type SeatType)
		{
			if (CounterpartCache.TryGetValue(SeatType, out var cached))
			{
				return cached;
			}
			List<string> mismatches = SeatMismatches(SeatType);
			if (mismatches.Count > 0)
			{
				throw new KingdomSeatMismatchException(SeatType.Name + " cannot carry a settlement; " + mismatches.Count + " field(s) unaccounted for: " + string.Join("; ", mismatches.ToArray()));
			}
			FieldInfo[] carried = Carried;
			FieldInfo[] counterparts = new FieldInfo[carried.Length];
			for (int i = 0; i < carried.Length; i++)
			{
				counterparts[i] = FindCounterpart(SeatType, carried[i].Name);
			}
			CounterpartCache[SeatType] = counterparts;
			return counterparts;
		}

		/// <summary>
		/// The seat's field of this name, or null. Declared fields are searched before inherited
		/// ones so a seat that shadows a base-class field resolves to its own rather than raising
		/// an ambiguity the caller cannot act on.
		/// </summary>
		private static FieldInfo FindCounterpart(Type SeatType, string Name)
		{
			for (Type type = SeatType; type != null; type = type.BaseType)
			{
				FieldInfo field = type.GetField(Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
				if (field != null)
				{
					return field;
				}
			}
			return null;
		}

		/// <summary>Read once: the swap pairs fields by position in this array, so every part of
		/// it must be looking at the same array.</summary>
		private static readonly FieldInfo[] Carried = typeof(KingdomSettlement).GetFields(BindingFlags.Instance | BindingFlags.Public);

		private static readonly Dictionary<Type, FieldInfo[]> CounterpartCache = new Dictionary<Type, FieldInfo[]>();
	}
}
