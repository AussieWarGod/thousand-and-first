using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement as a place, not a screen: staffs the gathering bench with a keeper whose
	/// talk reflects what is actually happening, and brings the occasional traveller through who
	/// is offered water and leaves &mdash; never enrolled, never housed, never counted.
	/// <para>
	/// Runs from the kingdom's one <c>ZoneActivatedEvent</c> pass, after growth and raids have
	/// already settled the state of the turn (<see cref="KingdomSystem"/>'s wiring calls this
	/// last), and reads that state rather than keeping any clock of its own. Guest arrival and
	/// unattended departure are both decided here, on activation, for the same reason the rest of
	/// the mod avoids a per-turn tick: time away must catch up in one pass, not accrue while
	/// nobody is watching.
	/// </para>
	/// </summary>
	public static class KingdomLocus
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionLocus") != "No";

		/// <summary>The one building the growth catalogue's "Staff" attribute makes a work; this
		/// names it so the keeper pass can find it among <see cref="KingdomSurvey.Works"/>
		/// without a marker property of its own.</summary>
		public const string BenchBlueprint = "r_KingdomBench";

		/// <summary>Population the keeper had last seen, read fresh each pass to decide
		/// <see cref="KingdomLocusRules.KeeperMood.Growing"/>.</summary>
		private const string KeeperLastPopulationProperty = "KingdomKeeperLastPopulation";

		private const string KeeperMoodProperty = "KingdomKeeperMood";

		public const string CausalPilgrimProperty = "r_TAF_CausalPilgrim";

		public const string PilgrimSequenceProperty = "r_TAF_PilgrimSequence";

		public const string PilgrimCauseProperty = "r_TAF_PilgrimCause";

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return;
			if (System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			if (!KingdomGuestLifecycle.ObserveOption(System,
				KingdomLifecycleLane.PlainGuest, Enabled, timeTicks, out bool allowNew)) return;
			if (KingdomGuestLifecycle.Open(System, KingdomLifecycleLane.PlainGuest) != null)
			{
				KingdomGuestLifecycle.Drive(System, Z, KingdomLifecycleLane.PlainGuest);
				if (KingdomGuestLifecycle.Open(System, KingdomLifecycleLane.PlainGuest) != null) return;
			}
			if (!allowNew) return;
			RunKeeperPass(System, Survey);
			// Guests belong at the gate/rite heart, not on a random claimed parasang. This also
			// keeps the city's one patience clock bound to the one zone which owns it.
			if (!KingdomPlots.TryRiteGround(Z, out _, out _)) return;
			if (!RunPilgrimPass(System, Z, Survey, timeTicks))
			{
				RunGuestPass(System, Z, Survey, timeTicks);
			}
		}

		/// <summary>
		/// Crews the gathering bench from the settlement's own settlers and keeps its keeper's
		/// conversation current. The keeper is chosen once and kept while they remain a valid
		/// candidate (<see cref="KingdomLocusRules.SelectKeeper"/>); an unstaffed bench is
		/// demoted back to furniture and says so on examine.
		/// </summary>
		private static void RunKeeperPass(KingdomSystem System, KingdomSurvey Survey)
		{
			GameObject bench = FindBench(Survey);
			if (bench == null)
			{
				return;
			}
			bool staffed = bench.GetIntProperty("KingdomStaffed") == 1;
			GameObject markedKeeper = FindMarkedKeeper(Survey);
			if (!staffed)
			{
				if (markedKeeper != null)
				{
					DemoteKeeper(markedKeeper);
				}
				SetBenchDescription(bench, KingdomLocusRules.BenchDescription(Staffed: false, KeeperName: null));
				return;
			}
			List<string> candidateIDs = new List<string>(Survey.Settlers.Count);
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				candidateIDs.Add(Survey.Settlers[i].ID);
			}
			string selectedID = KingdomLocusRules.SelectKeeper(candidateIDs, markedKeeper?.ID);
			if (selectedID == null)
			{
				// Staffed by headcount (KingdomStaffed reads the crewed total, not identities),
				// but nobody the survey can name a settler among this pass. Leave the bench as it
				// was rather than guess; the next pass tries again.
				return;
			}
			GameObject keeper = (markedKeeper != null && markedKeeper.ID == selectedID) ? markedKeeper : FindSettler(Survey, selectedID);
			if (keeper == null)
			{
				return;
			}
			if (markedKeeper != null && markedKeeper != keeper)
			{
				DemoteKeeper(markedKeeper);
			}
			// Read before either property is touched below: a keeper taking the bench for the
			// first time has never had a mood recorded, so their stale (default-zero) reading
			// would otherwise alias KeeperMood.Peaceful and skip building any conversation at
			// all the one time it is most needed.
			bool isNewKeeper = keeper.GetIntProperty("KingdomKeeper") != 1;
			bool grew = System.Population > keeper.GetIntProperty(KeeperLastPopulationProperty);
			KingdomLocusRules.KeeperMood mood = KingdomLocusRules.ClassifyMood(
				DryStreakActive: System.DryStreak > 0,
				RaidIncoming: System.RaidState == 1,
				RecentlyRaided: KingdomLocusRules.WasRecentlyRaided(System.LastRaidTick, The.Game.TimeTicks),
				Grew: grew);
			keeper.SetIntProperty("KingdomKeeper", 1);
			keeper.SetIntProperty(KeeperLastPopulationProperty, System.Population);
			// Rebuilt only on a change (or on first taking the bench) so the founder is never
			// handed an identical greeting twice in a row for no reason.
			if (isNewKeeper || keeper.GetIntProperty(KeeperMoodProperty) != (int)mood)
			{
				keeper.SetIntProperty(KeeperMoodProperty, (int)mood);
				KingdomLocusRules.KeeperSpeech speech = KingdomLocusRules.KeeperSpeechFor(mood, KingdomPresentation.Rich(System.KingdomDisplayName));
				// Named Question/Answer, not positional: addSimpleConversationToObject has a
				// second overload (Filter/FilterExtras in those slots instead) that a 5-arg call
				// matches just as well by type, which the compiler cannot break the tie on.
				Qud.API.ConversationsAPI.addSimpleConversationToObject(keeper, speech.Greeting, "Live and drink.", Question: speech.Question, Answer: speech.Answer);
			}
			SetBenchDescription(bench, KingdomLocusRules.BenchDescription(Staffed: true, KeeperName: keeper.ShortDisplayName));
		}

		private static GameObject FindBench(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				if (Survey.Works[i].Blueprint == BenchBlueprint)
				{
					return Survey.Works[i];
				}
			}
			return null;
		}

		private static GameObject FindMarkedKeeper(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				if (Survey.Settlers[i].GetIntProperty("KingdomKeeper") == 1)
				{
					return Survey.Settlers[i];
				}
			}
			return null;
		}

		private static GameObject FindSettler(KingdomSurvey Survey, string ID)
		{
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				if (Survey.Settlers[i].ID == ID)
				{
					return Survey.Settlers[i];
				}
			}
			return null;
		}

		private static void DemoteKeeper(GameObject Keeper)
		{
			Keeper.SetIntProperty("KingdomKeeper", 0);
			Keeper.SetIntProperty(KeeperMoodProperty, 0);
			Qud.API.ConversationsAPI.addSimpleConversationToObject(Keeper, "Someone else has the bench for now. Live and drink, all the same.", "Live and drink.");
		}

		private static void SetBenchDescription(GameObject Bench, string Text)
		{
			Description description = Bench.GetPart<Description>();
			if (description != null)
			{
				description.Short = Text;
			}
		}

		/// <summary>
		/// Brings travellers through on the cadence the pure rules define, whether or not anybody
		/// was here to see them, and resolves what became of them at the moment the founder is
		/// back to be told.
		/// <para>
		/// Addendum 8 clause 1: the road does not wait for the founder. A season away is a season
		/// of people arriving, waiting out their patience at a gate nobody answered, and going on
		/// &mdash; and clause 3 says what awareness gets is the dated news of it. So the backlog
		/// is resolved rather than collapsed: everyone whose patience ran out during the absence
		/// leaves one honest dated trace between them, and the only person still standing there
		/// is the one who arrived recently enough to still be waiting. That is at most one, and
		/// it is one because <c>GuestPatienceTicks</c> is shorter than
		/// <c>GuestIntervalTicks</c> rather than because a live object happened to be blocking
		/// the spawn.
		/// </para>
		/// </summary>
		private static void RunGuestPass(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long TimeTicks)
		{
			GameObject guest = FindGuest(Survey);
			if (guest != null)
			{
				bool offered = guest.GetIntProperty("KingdomGuestOffered") == 1;
				if (!offered && KingdomLocusRules.GuestShouldDepartUnattended(TimeTicks, System.GuestDepartTick))
				{
					DepartGuest(System, guest, Greeted: false);
				}
				return;
			}
			long effectiveDue = KingdomGuestLifecycle.EffectiveDue(System,
				KingdomLifecycleLane.PlainGuest, KingdomLocusRules.GuestIntervalTicks);
			if (effectiveDue <= 0L || TimeTicks < effectiveDue) return;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(
				effectiveDue, TimeTicks, KingdomLocusRules.GuestIntervalTicks,
				KingdomLocusRules.GuestPatienceTicks);
			Cell standingCell = passages.StandingSince > 0L ? HeartArrivalCell(Z) : null;
			long scheduleBefore = System.NextGuestTick > 0L ? System.NextGuestTick : 0L;
			long scheduleAfter = passages.StandingSince > 0L && standingCell == null
				? passages.StandingSince : passages.NextDueTick;
			int daysAgo = passages.Departed > 0
				? KingdomRules.ElapsedDays(TimeTicks - passages.LastDepartedTick) : 0;
			string chronicle = passages.Departed > 0
				? KingdomLocusRules.PassagesChronicleLine(passages.Departed,
					KingdomPresentation.Rich(System.KingdomDisplayName), daysAgo) : null;
			string ledger = passages.Departed > 0
				? KingdomLocusRules.PassagesLedgerNote(passages.Departed, daysAgo) : null;
			if (!KingdomGuestLifecycle.PublishPassages(System, Z,
				KingdomLifecycleLane.PlainGuest, TimeTicks, scheduleBefore, scheduleAfter,
				passages.Departed, passages.LastDepartedTick, passages.StandingSince,
				chronicle, ledger, null)) return;
			if (passages.StandingSince <= 0L)
			{
				return;
			}
			// Spawned at the tick they actually walked up, not at the tick the founder walked in,
			// so their patience is already partly spent and they leave when they were always
			// going to leave.
			if (standingCell != null) SpawnGuest(System, Z, standingCell, passages.StandingSince);
		}

		/// <summary>
		/// Renders one exact history-caused opportunity at the rite ground. True means the causal
		/// lane owns the gate this pass, including while travel, blockage, or receipt recovery waits;
		/// generic traffic must not step over it.
		/// </summary>
		private static bool RunPilgrimPass(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long TimeTicks)
		{
			Simulation.City.KingdomCityBook book = System.City;
			if (book == null) return false;
			book.Normalize();
			KingdomLocusRules.PilgrimState state =
				(KingdomLocusRules.PilgrimState)book.PilgrimState;
			if (state == KingdomLocusRules.PilgrimState.None) return false;

			if (!KingdomLocusRules.TryPilgrimWindow(book.PilgrimCauseTick,
				out long arrivalTick, out long departTick))
			{
				// Malformed causal evidence is evidence, not permission to erase the story and
				// let an unrelated generic roll take its place. Fail the shared authority closed;
				// a later migration can inspect the untouched CityBook fields.
				KingdomGuestLifecycle.QuarantineLegacyEvidence(System,
					"malformed causal-pilgrim window retained for migration");
				return true;
			}

			GameObject exact = FindCausalPilgrim(Survey, book);
			if (state == KingdomLocusRules.PilgrimState.Waiting && GameObject.Validate(exact))
			{
				// Reconcile the one marker before considering a new body. This is the placement
				// cut-point: a body added successfully but followed by an interrupted carrier write
				// is adopted, never followed by a replacement.
				book.PilgrimState = (int)KingdomLocusRules.PilgrimState.Standing;
				book.PilgrimObjectId = exact.ID;
				// This is the one pre-lifecycle body adoption case. The causal tick remains the
				// evidence; do not manufacture a parallel System clock for it.
				state = KingdomLocusRules.PilgrimState.Standing;
			}
			if (state == KingdomLocusRules.PilgrimState.Standing)
			{
				if (GameObject.Validate(exact))
				{
					book.PilgrimObjectId = exact.ID;
					if (TimeTicks < departTick) return true;
					ResolvePilgrim(System, book, exact, Greeted: false, departTick);
					return true;
				}
				// Never mint a replacement for an already-published body. Once its patience has
				// elapsed, the exact event receipt may settle the missing body's departure.
				if (TimeTicks >= departTick)
				{
					ResolvePilgrim(System, book, null, Greeted: false, departTick);
				}
				return true;
			}

			if (TimeTicks < arrivalTick) return true;
			if (TimeTicks >= departTick)
			{
				// The whole visit happened while its ground was away. It still has a date and cause;
				// it never manufactures a body merely because the founder came home late.
				ResolvePilgrim(System, book, null, Greeted: false, departTick);
				return true;
			}
			// A plain traveller who was already waiting when the third story was told keeps their
			// own patience. Resolve that exact body before the causal lane takes the gate; merely
			// suppressing RunGuestPass here would strand it forever and prevent the pilgrim too.
			GameObject traffic = FindGuest(Survey);
			if (GameObject.Validate(traffic))
			{
				bool offered = traffic.GetIntProperty("KingdomGuestOffered") == 1;
				if (!offered && KingdomLocusRules.GuestShouldDepartUnattended(
					TimeTicks, System.GuestDepartTick))
				{
					if (!DepartGuest(System, traffic, Greeted: false)) return true;
				}
				else return true;
			}
			Cell cell = HeartArrivalCell(Z);
			if (cell == null) return true; // blockage defers without spending the opportunity.
			if (string.IsNullOrEmpty(book.PilgrimName))
			{
				string planned;
				string namingFailure;
				if (!KingdomSemanticSelection.TryNameOnly(System,
					KingdomSemanticSelection.CausalPilgrimStream,
					KingdomSemanticSelection.PersonEventKind, book.PilgrimSequence,
					out planned, out namingFailure))
				{
					KingdomLog.Log("causal pilgrim waits: " + namingFailure);
					return true;
				}
				book.PilgrimName = planned;
			}
			string name = book.PilgrimName;
			KingdomGuestLifecycle.PublishSpawn(System, Z,
				KingdomLifecycleLane.PlainGuest, cell, TimeTicks, departTick,
				"r_KingdomGuestPilgrim", name, "the road that heard " + book.PilgrimCause,
				book.PilgrimSequence, 0, book.PilgrimCause, "causal-pilgrim",
				book.PilgrimPlaceName, null, null, null, null);
			return true;
		}

		/// <summary>Creates an unplaced body from one frozen lifecycle plan. Placement, identity,
		/// marker, and post-scan proof remain owned by <see cref="KingdomGuestLifecycle"/>.</summary>
		internal static GameObject CreateLifecycleGuest(KingdomLifecycleOperation op,
			KingdomLifecycleProjection projection)
		{
			if (op == null || projection == null || op.Lane != KingdomLifecycleLane.PlainGuest
				|| op.Action != KingdomLifecycleAction.Spawn) return null;
			GameObject guest;
			try { guest = GameObject.Create(projection.Blueprint); }
			catch { return null; }
			if (!GameObject.Validate(guest)) return null;
			guest.SetIntProperty("KingdomGuest", 1);
			guest.SetStringProperty("KingdomOrigin", op.Origin ?? "the road");
			if (!string.IsNullOrEmpty(op.ObjectName))
				guest.GiveProperName(op.ObjectName, Force: true);
			if (string.Equals(op.Creed, "causal-pilgrim", StringComparison.Ordinal))
			{
				string detail = op.Detail ?? "a story from the city";
				string shownDetail = KingdomPresentation.Rich(detail);
				guest.SetIntProperty(CausalPilgrimProperty, 1);
				guest.SetIntProperty(PilgrimSequenceProperty, op.Kind);
				guest.SetStringProperty(PilgrimCauseProperty, detail);
				Description description = guest.GetPart<Description>();
				if (description != null)
					description.Short = "Road dust worked into ceremonial folds. This pilgrim came "
						+ "because " + shownDetail + ".";
				Qud.API.ConversationsAPI.addSimpleConversationToObject(guest,
					KingdomLocusRules.PilgrimGreeting(shownDetail), "Live and drink.",
					Question: "What drew you here?", Answer: "The roads kept telling of "
						+ shownDetail + ". I wanted to stand where it happened before I went on.");
			}
			else
			{
				Qud.API.ConversationsAPI.addSimpleConversationToObject(guest,
					"Live and drink, if you have it to spare. I'm not staying — just passing through.",
					"Live and drink.", Question: "Where are you bound?",
					Answer: "Wherever the road goes next. I heard there was water shared here, and wanted to see it for myself.");
			}
			return guest;
		}

		private static GameObject FindCausalPilgrim(KingdomSurvey Survey,
			Simulation.City.KingdomCityBook Book)
		{
			if (Survey == null) return null;
			if (!string.IsNullOrEmpty(Book.PilgrimObjectId))
			{
				GameObject global = GameObject.FindByID(Book.PilgrimObjectId);
				if (GameObject.Validate(global) && global.GetIntProperty(CausalPilgrimProperty) == 1
					&& ReferenceEquals(global.CurrentZone, Survey.Ground)
					&& Survey.CausalPilgrims.Contains(global)
					&& global.GetIntProperty(PilgrimSequenceProperty) == Book.PilgrimSequence)
					return global;
			}
			for (int i = 0; i < Survey.CausalPilgrims.Count; i++)
			{
				GameObject item = Survey.CausalPilgrims[i];
				if (GameObject.Validate(item) && item.GetIntProperty(CausalPilgrimProperty) == 1
					&& item.GetIntProperty(PilgrimSequenceProperty) == Book.PilgrimSequence)
					return item;
			}
			return null;
		}

		/// <summary>Rite cell first, then deterministic Chebyshev rings. No draw and no distant
		/// random empty cell: blockage is a real, retryable state.</summary>
		internal static Cell HeartArrivalCell(Zone Z)
		{
			if (!KingdomPlots.TryRiteGround(Z, out int riteX, out int riteY)) return null;
			for (int radius = 0; radius <= 3; radius++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					for (int dx = -radius; dx <= radius; dx++)
					{
						if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
						int x = riteX + dx;
						int y = riteY + dy;
						if (x < 0 || x >= Z.Width || y < 0 || y >= Z.Height) continue;
						Cell cell = Z.GetCell(x, y);
						if (cell == null || !cell.IsPassable()
							|| cell.HasObjectWithPart("LiquidVolume")) continue;
						bool living = false;
						List<GameObject> objects = cell.GetObjects();
						for (int i = 0; i < objects.Count; i++)
							if (GameObject.Validate(objects[i]) && objects[i].IsCreature)
							{
								living = true;
								break;
							}
						if (!living) return cell;
					}
				}
			}
			return null;
		}

		private static bool ResolvePilgrim(KingdomSystem System,
			Simulation.City.KingdomCityBook Book, GameObject Pilgrim, bool Greeted,
			long DepartTick)
		{
			string name = !string.IsNullOrEmpty(Book.PilgrimName) ? Book.PilgrimName
				: (GameObject.Validate(Pilgrim) ? PlainGuestName(Pilgrim) : "A pilgrim");
			if (string.IsNullOrEmpty(name) || name.Length > KingdomLocusRules.MaxPilgrimNameChars)
				name = "A pilgrim";
			if (string.IsNullOrEmpty(Book.PilgrimName) && GameObject.Validate(Pilgrim)
				&& name.Length <= KingdomLocusRules.MaxPilgrimNameChars)
				Book.PilgrimName = name;
			string cause = Book.PilgrimCause;
			string shownName = KingdomPresentation.Rich(name);
			string shownCause = KingdomPresentation.Rich(cause);
			string shownPlace = KingdomPresentation.Rich(Book.PilgrimPlaceName);
			int sequence = Book.PilgrimSequence;
			string line = KingdomLocusRules.PilgrimChronicleLine(shownName,
				shownPlace, shownCause, Book.PilgrimGreeted == 1 || Greeted);
			string note = Greeted ? shownName + " received water and went on speaking of "
				+ shownCause + "."
				: KingdomLocusRules.PilgrimLedgerNote(shownName, shownCause,
					KingdomRules.ElapsedDays(The.Game.TimeTicks - DepartTick));
			long next = KingdomLocusRules.NextGuestDueTick(The.Game.TimeTicks);
			if (!GameObject.Validate(Pilgrim))
			{
				long before = System.NextGuestTick > 0L ? System.NextGuestTick : 0L;
				if (before == next && next < long.MaxValue) next++;
				return KingdomGuestLifecycle.PublishMissedCausal(System,
					The.Player?.CurrentZone ?? null, The.Game.TimeTicks, before, next, sequence,
					name, cause, Book.PilgrimPlaceName, line, note);
			}
			return KingdomGuestLifecycle.PublishDeparture(System, Pilgrim,
				KingdomLifecycleLane.PlainGuest, The.Game.TimeTicks, next, Greeted,
				line, note, Greeted ? "{{C|" + shownName
					+ " received the settlement's water.}}" : null,
				null, Greeted && !System.FirstGuestGreeted);
		}

		private static GameObject FindGuest(KingdomSurvey Survey)
		{
			return Survey != null && Survey.Guests.Count > 0 ? Survey.Guests[0] : null;
		}

		/// <summary>Puts one traveller on the ground at the tick they walked up. False when there
		/// was nowhere to stand them, which is the caller's signal to leave their arrival unspent
		/// rather than losing them.</summary>
		private static bool SpawnGuest(KingdomSystem System, Zone Z, Cell cell, long ArrivalTick)
		{
			if (cell == null) return false;
			KingdomSemanticPersonPlan plan;
			string planFailure;
			if (!KingdomGuestLifecycle.TryPrepareSpawnPlan(System,
				KingdomLifecycleLane.PlainGuest, "r_KingdomGuests", "r_KingdomGuest",
				out plan, out planFailure))
			{
				KingdomLog.Log("plain guest waits: " + planFailure);
				return false;
			}
			long depart = KingdomLocusRules.GuestDepartTickFor(ArrivalTick);
			string shownName = KingdomPresentation.Rich(plan.Name);
			string chronicle = shownName + " came to "
				+ KingdomPresentation.Rich(System.KingdomDisplayName)
				+ " by the road and waited at its rite ground";
			string ledger = shownName + " is waiting at the rite ground.";
			string message = "{{C|" + shownName
				+ " has come to the rite ground as a guest.}}";
			return KingdomGuestLifecycle.PublishSpawn(System, Z,
				KingdomLifecycleLane.PlainGuest, cell, The.Game.TimeTicks, depart,
				plan.Blueprint, plan.Name, plan.Origin, 0, 0, null, null, null, chronicle,
				ledger, message, null, semanticPlan: plan);
		}

		/// <summary>
		/// Offers the settlement's own water to a guest, spent exactly from its dedicated stores.
		/// Called
		/// from <see cref="XRL.World.Parts.r_KingdomGuest"/>'s inventory action; a no-op if the
		/// guest has already been offered water or is no longer present.
		/// </summary>
		/// <param name="Guest">The guest object the player targeted.</param>
		public static void OfferGuestWater(GameObject Guest)
		{
			if (Guest == null || Guest.GetIntProperty("KingdomGuest") != 1 || Guest.GetIntProperty("KingdomGuestOffered") == 1)
			{
				return;
			}
			Zone zone = Guest.CurrentZone;
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused; the guest can be helped after it resumes.");
				return;
			}
			if (zone == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			int cost = KingdomLocusRules.GuestWaterCostDrams;
			string measure = cost + ((cost == 1) ? " dram" : " drams");
			KingdomSurvey survey = KingdomSurvey.Take(zone);
			if (survey.StoredWater < cost)
			{
				Popup.Show("Offering water to "
					+ KingdomPresentation.Rich(PlainGuestName(Guest))
					+ " requires exactly {{C|"
					+ measure + "}} from the dedicated stores, and they cannot provide it.");
				return;
			}
			string guestName = PlainGuestName(Guest);
			string shownGuestName = KingdomPresentation.Rich(guestName);
			bool causal = Guest.GetIntProperty(CausalPilgrimProperty) == 1;
			string cause = causal ? Guest.GetStringProperty(PilgrimCauseProperty) : null;
			string shownCause = KingdomPresentation.Rich(cause);
			string realm = KingdomPresentation.Rich(system.KingdomDisplayName);
			string chronicle = causal
				? KingdomLocusRules.PilgrimChronicleLine(shownGuestName,
					KingdomPresentation.Rich(system.City.PilgrimPlaceName), shownCause,
					Greeted: true)
				: (!system.FirstGuestGreeted
					? realm + " gave water to its first guest since its founding, and the traveller went on speaking well of it"
					: KingdomLocusRules.GuestChronicleLine(true, realm));
			string ledger = causal
				? shownGuestName + " received water and went on speaking of "
					+ shownCause + "."
				: shownGuestName + " received " + measure + " and continued along the road.";
			string message = "{{C|" + realm + " offered " + measure
				+ " to " + shownGuestName + ".}}";
			long next = KingdomLocusRules.NextGuestDueTick(The.Game.TimeTicks);
			bool milestone = !system.FirstGuestGreeted;
			if (!KingdomGuestLifecycle.PublishOfferWater(system, Guest, The.Game.TimeTicks,
				next, chronicle, ledger, message, milestone))
			{
				Popup.Show("The offering could not complete. Its exact lifecycle receipt remains open; no second offering can begin.");
				return;
			}
			Popup.Show(KingdomLocusRules.GuestThanks(shownGuestName, realm));
		}

		/// <summary>Plain lifecycle name; rich output is a separate projection.</summary>
		private static string PlainGuestName(GameObject guest)
		{
			if (!GameObject.Validate(guest)) return "A traveller";
			string named = guest.GetStringProperty("KingdomName");
			if (string.IsNullOrEmpty(named)) named = guest.BaseDisplayNameStripped;
			return string.IsNullOrEmpty(named) ? "A traveller" : named;
		}

		private static bool DepartGuest(KingdomSystem System, GameObject Guest, bool Greeted)
		{
			if (Guest.GetIntProperty(CausalPilgrimProperty) == 1)
			{
				long depart = System.GuestDepartTick;
				if (depart <= 0L && !KingdomLocusRules.TryPilgrimWindow(
					System.City.PilgrimCauseTick, out _, out depart)) return false;
				return ResolvePilgrim(System, System.City, Guest, Greeted, depart);
			}
			string name = PlainGuestName(Guest);
			string shownName = KingdomPresentation.Rich(name);
			bool milestone = Greeted && !System.FirstGuestGreeted;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			string line = milestone
				? realm + " gave water to its first guest since its founding, and the traveller went on speaking well of it"
				: KingdomLocusRules.GuestChronicleLine(Greeted, realm);
			string ledger = Greeted
				? shownName + " received water and continued along the road."
				: KingdomLocusRules.GuestLedgerNote(shownName,
					KingdomRules.ElapsedDays(The.Game.TimeTicks - System.GuestDepartTick));
			string message = Greeted ? "{{C|" + shownName
				+ " continued along the road.}}" : null;
			return KingdomGuestLifecycle.PublishDeparture(System, Guest,
				KingdomLifecycleLane.PlainGuest, The.Game.TimeTicks,
				KingdomLocusRules.NextGuestDueTick(The.Game.TimeTicks), Greeted,
				line, ledger, message, null, milestone);
		}
	}
}

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently.
namespace XRL.World.Parts
{
	/// <summary>
	/// Carried by a spawned guest (<see cref="ThousandAndFirst.KingdomLocus"/>'s
	/// <c>SpawnGuest</c>). Adds the one interactive moment a guest offers: the founder can offer
	/// them water from the settlement's own stores. Everything the action actually does lives in
	/// <see cref="ThousandAndFirst.KingdomLocus.OfferGuestWater"/>; this part is only the event
	/// plumbing, the same split <c>r_FounderBasin</c> and <c>r_KingdomScaffold</c> use.
	/// </summary>
	[Serializable]
	public class r_KingdomGuest : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (ParentObject.GetIntProperty("KingdomGuestOffered") == 0)
			{
				E.AddAction("Offer Water", "offer water", "r_OfferGuestWater", null, 'o', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OfferGuestWater" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomLocus.OfferGuestWater(ParentObject);
			}
			return base.HandleEvent(E);
		}
	}
}
