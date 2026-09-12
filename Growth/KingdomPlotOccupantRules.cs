namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Occupants (who may be moved off a raising's ground, and who never is) ---------

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
			/// <summary>A post standing inside the layout that will not move. The post normally
			/// moves with its holder; this is the one case left where it cannot, and then the
			/// raising waits rather than shoving somebody who would walk straight back.</summary>
			AnchorBound,
			/// <summary>A wild animal: no name, no trade, nobody's neighbour. Driven off.</summary>
			Beast
		}

		/// <summary>
		/// A body nobody will miss from the building site: an animal-kind creature with no proper
		/// name and no trade. Every clause is a person-test that must FAIL before the settlement
		/// will drive a body off its ground.
		/// <para>Engine predicates (ILSpy 9.1 of core 2.0.211.51): a proper name is
		/// GameObject.HasProperName (XRL/World/GameObject.cs:1126); a trader is
		/// GameObject.IsMerchant(), which is the "Merchant" tag or property or a
		/// GenericInventoryRestocker part (XRL/World/GameObject.cs:1747-1753); animal kind is
		/// blueprint inheritance from the base "Animal" blueprint
		/// (GameObjectBlueprint.InheritsFrom, XRL/World/GameObjectBlueprint.cs:237-249), which is
		/// what separates a croc (Croc -> BaseReptile -> Animal -> Creature) from every villager,
		/// merchant and named NPC, who descend from Humanoid instead. Inheritance is used rather
		/// than a Culture/faction reading because the "Culture=Animal" tag sits on BaseAnimal and
		/// a croc never inherits it: GetCulture would answer "croc".</para>
		/// </summary>
		/// <param name="ProperName">The body has a name of its own.</param>
		/// <param name="Merchant">The body trades.</param>
		/// <param name="AnimalKind">The body's blueprint descends from Animal.</param>
		public static bool IsDrivableBeast(bool ProperName, bool Merchant, bool AnimalKind)
		{
			return AnimalKind && !ProperName && !Merchant;
		}

		/// <summary>Everything the settlement knows about one body on a blocked slot, frozen before
		/// any decision is taken. Engine-free so the ladder can be tested rung by rung.</summary>
		public readonly struct OccupantFacts
		{
			public readonly bool Player;
			public readonly bool PlayerLed;
			public readonly bool Staged;
			public readonly bool OurSettler;
			public readonly bool ProperName;
			public readonly bool Merchant;
			public readonly bool AnimalKind;
			public readonly bool RollId;
			public readonly bool ResidentStanding;

			public OccupantFacts(bool Player, bool PlayerLed, bool Staged, bool OurSettler,
				bool ProperName, bool Merchant, bool AnimalKind, bool RollId,
				bool ResidentStanding)
			{
				this.Player = Player;
				this.PlayerLed = PlayerLed;
				this.Staged = Staged;
				this.OurSettler = OurSettler;
				this.ProperName = ProperName;
				this.Merchant = Merchant;
				this.AnimalKind = AnimalKind;
				this.RollId = RollId;
				this.ResidentStanding = ResidentStanding;
			}
		}

		/// <summary>
		/// The ladder, in order. The founder and anything they lead come first and are never moved.
		/// A staged body belongs to a happening and is left alone even when it is an animal. Then
		/// the settlement's own and the wild part: a body that is not ours is driven off only when
		/// it is a beast, and is otherwise a person the settlement has no business shoving. Ours is
		/// movable only once the roll proves it.
		/// </summary>
		public static OccupantReason JudgeOccupant(OccupantFacts Facts)
		{
			if (Facts.Player) return OccupantReason.Player;
			if (Facts.PlayerLed) return OccupantReason.PlayerLed;
			if (Facts.Staged) return OccupantReason.Staged;
			if (!Facts.OurSettler)
				return IsDrivableBeast(Facts.ProperName, Facts.Merchant, Facts.AnimalKind)
					? OccupantReason.Beast : OccupantReason.NotOurs;
			if (!Facts.RollId) return OccupantReason.NoRoll;
			return Facts.ResidentStanding ? OccupantReason.Resident : OccupantReason.NotResident;
		}

		/// <summary>
		/// How many of the bodies standing off a site were the settlement's own. The clearance
		/// counts residents and beasts together, so the settler sentence is always the remainder;
		/// a count that cannot be made is nobody rather than a negative crowd.
		/// </summary>
		public static int SettlersMoved(int Moved, int Beasts)
		{
			return Moved <= Beasts ? 0 : Moved - Beasts;
		}

		/// <summary>
		/// The operator's line for one body standing on ground a raising or an improvement wants.
		/// "unwitnessed" says exactly one thing: no settlement pass was bound for that ground when
		/// the body was read, so nothing witnessed it. A classified body always names its rung.
		/// </summary>
		/// <param name="Reason">The ladder's answer, or null when nothing could be read.</param>
		public static string OccupantLine(string Id, string Blueprint, string At,
			ArchitecturePassability Passability, string Reason)
		{
			return "architecture: envelope occupant " + (string.IsNullOrEmpty(Id) ? "unknown" : Id)
				+ " (" + (string.IsNullOrEmpty(Blueprint) ? "gone" : Blueprint) + ") at "
				+ (string.IsNullOrEmpty(At) ? "nowhere" : At)
				+ " passability=" + Passability
				+ " reason=" + (string.IsNullOrEmpty(Reason) ? "unwitnessed" : Reason);
		}

		/// <summary>Whether a classified body may be moved off the ground at all.</summary>
		public static bool IsMovableOccupant(OccupantReason Reason)
		{
			return Reason == OccupantReason.Resident || Reason == OccupantReason.Beast;
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
		/// Who may be moved off a raising's ground. The settlement's own residents and the wild
		/// things are moved off their building site; the player and any person who is not ours are
		/// never moved, because nothing the founder did not place is the settlement's to shove.
		/// Mixed company refuses: clearing half the slots would move bodies for nothing. A post
		/// standing inside the layout is not judged here at all -- it moves with its holder, and
		/// the one post that will not move is caught before anybody walks.
		/// </summary>
		/// <param name="Occupants">Living bodies standing on blocked layout slots.</param>
		/// <param name="Movable">How many of them are ours to move: residents and beasts.</param>
		/// <param name="AnyPlayer">Whether the player is one of them.</param>
		public static OccupantVerdict JudgeOccupants(int Occupants, int Movable, bool AnyPlayer)
		{
			if (Occupants <= 0) return OccupantVerdict.Clear;
			if (AnyPlayer || Movable != Occupants || Movable < 0) return OccupantVerdict.Refuse;
			return OccupantVerdict.Displace;
		}

		/// <summary>
		/// The crew drove the wild things off the site. Separate from the settler sentence because
		/// standing your own neighbour aside and driving a croc off your fire are not the same act.
		/// </summary>
		public static string DroveBeastsOff(string Name, int Moved, bool Raised, string Fault)
		{
			string drove = "The crew drove " + Moved + (Moved == 1 ? " beast" : " beasts")
				+ " off the ground the {{C|" + Name + "}} is being raised on, ";
			return Raised ? drove + "and the work goes on."
				: drove + "but the raising was refused: " + (Fault ?? "the ground would not take it")
					+ ".";
		}

		/// <summary>The settlement moved a resident's post with the resident, so the work can go on
		/// and the post is not left standing on a building site.</summary>
		public static string MovedPostWithResident(string Name, int X, int Y)
		{
			return "A post stood on the ground the {{C|" + Name + "}} is being raised on. The crew stood its holder off and moved the post with them, to " + X + ", " + Y + ".";
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

	}
}
