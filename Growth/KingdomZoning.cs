using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the settlement's zoning: what a founder may commission, and on
	/// which ground. Reads the four optional gates a <c>&lt;building&gt;</c> entry may declare
	/// (see <see cref="KingdomZoningRules"/> for the arithmetic and MODDING.md for the schema),
	/// keeps the roster of designs the keepers have been taught, and composes every refusal so
	/// that it names both what is missing and what would fix it.
	/// <para>
	/// Nothing here ever blocks in silence. A design the founder cannot raise still appears in
	/// the commission list, tagged with the one thing standing in its way
	/// (<see cref="GateNote"/>), and an attempt on it answers with a whole sentence
	/// (<see cref="Permits"/>). That is STANDARDS 7b applied to the one part of a settlement game
	/// where players complain about it most: plots that will not build and will not say why.
	/// </para>
	/// </summary>
	public static class KingdomZoning
	{
		/// <summary>Whether the zoning gates are switched on. Off, every design is offered
		/// wherever the style and stage already allowed it, exactly as before this existed.</summary>
		public static bool Enabled => Options.GetOption("r_TAF_OptionZoning") != "No";

		/// <summary>
		/// Game-state key the keepers' roster USED to be stored under, kept only so
		/// <see cref="Stored"/> can fold an older save's roster into the city it belongs to and
		/// retire the key.
		/// <para>
		/// It was a flat entry on the game, and that was wrong in a way nobody chose: the store was
		/// game-wide rather than realm-wide, so a seceding city walked away with none of what its
		/// own keepers had learned, and an exiled founder founded their next realm already holding
		/// every design the old one had been taught. The exile modal says <i>"the charter is taken
		/// from you"</i>; the tech base walked out of the gate with them. Addendum 22 B1 ends it:
		/// the rolls sit on the city (<see cref="KingdomSettlement.KeepersRoster"/>), the leads sit
		/// with the founder (the journal), and the realm reads rather than holds.
		/// </para>
		/// </summary>
		public const string RosterState = "r_TAF_KeepersRoster";

		// Gates live beside the catalog rather than inside KingdomRules.BuildEntry so that the
		// registry parser needs two lines of wiring instead of a rewritten entry type. Keyed by
		// building Key, which is what the registry already overrides by (STANDARDS 6): a later
		// file re-using a key registers its own gate over the earlier one, including an entry
		// that declares no gates at all, which correctly un-gates the design.
		private static readonly Dictionary<string, ZoneGate> Gates = new Dictionary<string, ZoneGate>();

		/// <summary>
		/// Forgets every registered gate. Called by the registry loader before it re-reads the
		/// XML streams, so a reload never leaves a gate behind for an entry that no longer
		/// declares one.
		/// </summary>
		public static void ClearGates()
		{
			Gates.Clear();
			// The purpose cache is derived from these, so it cannot outlive them. This is also the
			// per-load invalidation: the catalogue is re-read on every AfterGameLoaded
			// (KingdomLoader), so a second game in the same session cannot inherit the first one's
			// answer about what its cities were about.
			KeptCacheZone = null;
			KeptCacheTick = -1L;
			KeptCacheValue = null;
		}

		/// <summary>
		/// Registers one entry's gate attributes as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the raw attribute
		/// strings; all four may be null, which registers an open gate.
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="Districts">Raw <c>Districts</c> attribute.</param>
		/// <param name="MinZones">Raw <c>MinZones</c> attribute.</param>
		/// <param name="Knowledge">Raw <c>Knowledge</c> attribute.</param>
		/// <param name="MinTech">Raw <c>MinTech</c> attribute.</param>
		public static void RegisterGate(string Key, string Districts, string MinZones, string Knowledge, string MinTech)
		{
			RegisterGate(Key, Districts, MinZones, Knowledge, MinTech, null, null, null);
		}

		/// <summary>
		/// The same registration with Addendum 16's creed stack. Every one of the three is
		/// optional and an absent attribute gates nothing, exactly like the four before them.
		/// </summary>
		/// <param name="Builders">Raw <c>Builders</c> attribute.</param>
		/// <param name="Creed">Raw <c>Creed</c> attribute.</param>
		/// <param name="CreedShare">Raw <c>CreedShare</c> attribute.</param>
		public static void RegisterGate(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare)
		{
			RegisterGate(Key, Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare, null);
		}

		/// <summary>
		/// The same registration with Addendum 15's <c>Strata</c>. Optional like the seven before
		/// it: an entry that names no stratum stands in every one of them, which is what every
		/// entry in the catalogue did the day before this landed.
		/// </summary>
		/// <param name="Strata">Raw <c>Strata</c> attribute.</param>
		public static void RegisterGate(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, string Strata)
		{
			RegisterGate(Key, Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare, Strata, null);
		}

		/// <summary>
		/// The same registration with Addendum 22 A1's <c>Megastructure</c>. Optional like the eight
		/// before it: a design that does not claim to be one of the great works is ordinary, and
		/// every design in the catalogue but one is.
		/// </summary>
		/// <param name="Megastructure">Raw <c>Megastructure</c> attribute.</param>
		public static void RegisterGate(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, string Strata, string Megastructure)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes(Key, Districts, MinZones, Knowledge, MinTech,
				Builders, Creed, CreedShare, Strata, Megastructure, out string error);
			if (error != null)
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
			}
			Gates[Key] = gate;
		}

		/// <summary>The gate declared for a design key. An unregistered key is open, which is
		/// what any caller reaching a design the registry never saw should get.</summary>
		public static ZoneGate GateFor(string Key)
		{
			// The gates are filled by KingdomData's own pass, so asking for one before anything has
			// read the catalog would answer "open" for every design in the game.
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && Gates.TryGetValue(Key, out ZoneGate gate))
			{
				return gate;
			}
			return ZoneGate.Open;
		}

		/// <summary>
		/// Every knowledge key the SEATED city holds: its own stored rolls &mdash; designs taught,
		/// machines certified, ceremonies held, nodes worked out &mdash; plus one <c>origin:</c> key
		/// for each people living there right now. Origins are read live off
		/// <c>KingdomSystem.OriginCounts</c> rather than stored, because a trade the settlement holds
		/// only because somebody from that country lives here should leave with them.
		/// <para>
		/// <b>Seat only</b> (Addendum 22 B4). Knowledge is where it was taught, and teaching the
		/// other city is an ACT: carry the disk and walk, certify the machine there too, or set down
		/// at their bench what your other keepers worked out and let them walk the rest of it
		/// (<see cref="ShowKeepers"/>). What the founder carries between cities is doors, never
		/// rooms.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null yields an empty roster.</param>
		public static List<string> Roster(KingdomSystem System)
		{
			List<string> roster = KingdomZoningRules.DecodeRoster(Stored(System));
			if (System == null || System.OriginCounts == null)
			{
				return roster;
			}
			foreach (KeyValuePair<string, int> people in System.OriginCounts)
			{
				if (people.Value <= 0)
				{
					continue;
				}
				string key = KingdomZoningRules.ComposeKey(KingdomZoningRules.KindOrigin, people.Key);
				if (key != null && !roster.Contains(key))
				{
					roster.Add(key);
				}
			}
			return roster;
		}

		/// <summary>
		/// The same read for a city the founder is not standing in &mdash; the realm's other city,
		/// a seceded one, or one captured into an exile. Used by the teaching act, which has to be
		/// able to say what the OTHER keepers know without seating them.
		/// </summary>
		/// <param name="City">The settlement record. Null yields an empty roster.</param>
		public static List<string> RosterOf(KingdomSettlement City)
		{
			List<string> roster = KingdomZoningRules.DecodeRoster((City == null) ? null : City.KeepersRoster);
			if (City == null || City.OriginCounts == null)
			{
				return roster;
			}
			foreach (KeyValuePair<string, int> people in City.OriginCounts)
			{
				if (people.Value <= 0)
				{
					continue;
				}
				string key = KingdomZoningRules.ComposeKey(KingdomZoningRules.KindOrigin, people.Key);
				if (key != null && !roster.Contains(key))
				{
					roster.Add(key);
				}
			}
			return roster;
		}

		/// <summary>
		/// Adds one design to the SEATED city's keepers' stored knowledge &mdash; the keepers in
		/// front of the founder are the keepers being taught. Idempotent per city: teaching the same
		/// design twice in the same place changes nothing and reports false, so nothing can be
		/// farmed by repetition, and teaching it again in the OTHER city teaches that city
		/// (Addendum 22 B4/B5). Announces a rise in that city's craft when one happens, once, where
		/// the founder is standing.
		/// </summary>
		/// <param name="System">The realm; must be founded for the announcement to have a name
		/// to use, but the roster is stored regardless.</param>
		/// <param name="Kind">A knowledge kind &mdash; <c>disk</c>, <c>machine</c>, or one your
		/// own mod invents.</param>
		/// <param name="Name">Blueprint or design name. Case is folded away.</param>
		/// <returns>True when the settlement did not already know this.</returns>
		public static bool Learn(KingdomSystem System, string Kind, string Name)
		{
			string key = KingdomZoningRules.ComposeKey(Kind, Name);
			if (key == null)
			{
				MetricsManager.LogError("ThousandAndFirst zoning: refused an unusable knowledge key for kind '" + Kind + "', name '" + Name + "'");
				return false;
			}
			List<string> stored = KingdomZoningRules.DecodeRoster(Stored(System));
			if (stored.Contains(key))
			{
				return false;
			}
			TechLevel before = KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(stored));
			stored.Add(key);
			Store(System, KingdomZoningRules.EncodeRoster(stored));
			TechLevel after = KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(stored));
			KingdomLog.Log("zoning: learned " + key + " (" + before + " -> " + after + ")");
			if (after > before && System != null && System.Founded)
			{
				MessageQueue.AddPlayerMessage("{{G|" + System.SeatName + " now builds at the level of " + KingdomZoningRules.TechName(after) + ".}}");
				KingdomChronicle.Record(System, "the keepers of " + System.KingdomDisplayName + " reached the level of " + KingdomZoningRules.TechName(after));
			}
			return true;
		}

		/// <summary>
		/// Records that a machine hauled home was certified fit for the grid, which is one of the
		/// two ways a settlement's craft rises. Deliberately one-way: taking the machine back off
		/// the grid later returns the machine to the founder, not the knowledge to nobody &mdash;
		/// and one-way PER CITY (Addendum 22 B5), so a machine dragged on to the realm's other city
		/// and certified there teaches there too, and neither city forgets when the machine
		/// eventually leaves. Safe to call for a machine this city already recorded.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Machine">The machine just certified. Null and blueprint-less objects are
		/// ignored rather than stored as a blank key.</param>
		public static void RecordCertification(KingdomSystem System, GameObject Machine)
		{
			KingdomSystem.Guard("zoning certification", delegate
			{
				if (Machine == null || string.IsNullOrEmpty(Machine.Blueprint))
				{
					return;
				}
				if (Learn(System, KingdomZoningRules.KindMachine, Machine.Blueprint))
				{
					// Holding the artifact is most of an answer and never all of it: a node this
					// machine seeds is revealed and begun here, and the keepers still finish it.
					KingdomResearch.ApplySources(System);
				}
			});
		}

		/// <summary>The settlement's craft, derived from its roster. See
		/// <see cref="KingdomZoningRules.TechPoints"/> for what each kind of knowledge is worth.</summary>
		public static TechLevel Tech(KingdomSystem System)
		{
			return KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(Roster(System)));
		}

		/// <summary>
		/// One line for the status report naming the settlement's craft and what the next level
		/// costs, so the level is never a number the founder has to reverse-engineer from
		/// refusals.
		/// </summary>
		/// <param name="System">The realm. Null or unfounded reports an empty string.</param>
		public static string Readout(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return "";
			}
			List<string> roster = Roster(System);
			int points = KingdomZoningRules.TechPoints(roster);
			TechLevel level = KingdomZoningRules.LevelForPoints(points);
			int wanted = KingdomZoningRules.PointsToNext(points);
			string next = (wanted <= 0)
				? "  {{K|(the keepers have learned everything this settlement can)}}"
				: ("  {{K|(" + wanted + " more toward " + KingdomZoningRules.TechName((TechLevel)((int)level + 1)) + ")}}");
			return "\nCraft: {{C|" + KingdomZoningRules.TechName(level) + "}}" + next;
		}

		/// <summary>
		/// Who lives in the seated city, as the creed stack has to see them: the head count, the
		/// countries they walked in from, what they hold with, and what they have held and left
		/// (<c>KingdomSystem.CreedPastCounts</c>).
		/// <para>
		/// Read off the city's own tallies rather than off the ground, so it answers for a city
		/// whose people are not loaded &mdash; which is every city the founder is not standing in,
		/// and the seated one before its zone has been walked.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null or unfounded yields
		/// <c>BuilderRoll.Unknown</c>, which permits every creed gate.</param>
		public static BuilderRoll BuilderRollOf(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return BuilderRoll.Unknown;
			}
			return new BuilderRoll(System.Population, System.OriginCounts, System.CreedCounts, System.CreedPastCounts);
		}

		/// <summary>
		/// Whether a design is OFFERED to this settlement at all &mdash; the one question every
		/// menu that lists the catalogue asks, so that they all ask it the same way.
		/// <para>
		/// Style, stage, and one more: Addendum 14's visibility law as Addendum 16 applies it to
		/// creed-works. <b>You see what you have unlocked, you do not see what you have not, and
		/// you especially cannot see what you CAN'T unlock.</b> Everything else in this file's
		/// gates is a door with a key somewhere &mdash; a disk to carry home, a machine to certify,
		/// a parasang to claim, ground to name &mdash; and every one of those designs stays in the
		/// list wearing the tag that says which key (<see cref="GateNote"/>), because a list that
		/// silently shortens teaches nothing.
		/// </para>
		/// <para>
		/// A creed nobody here holds and nobody here has ever held is the one gate with no key at
		/// all. There is nothing the founder could go and do about it, so naming it would be noise
		/// dressed as guidance, and the design is not shown. The moment one person aligns &mdash;
		/// by arriving, by converting, or by having converted away years ago &mdash; the design
		/// appears, tagged with whatever is still in its way.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null or unfounded offers nothing.</param>
		/// <param name="Entry">The design. Null is not offered.</param>
		public static bool Offered(KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			if (System == null || Entry == null)
			{
				return false;
			}
			if (!KingdomRules.StyleAllows(Entry.Styles, System.Style) || System.Stage < Entry.MinStage)
			{
				return false;
			}
			return Visible(System, Entry);
		}

		/// <summary>
		/// The visibility half of <see cref="Offered"/> on its own, for a caller that has already
		/// answered style and stage.
		/// <para>
		/// Fails OPEN, like every other judgment in this file: if the question throws, the design
		/// is shown. A founder who sees one design they cannot raise is told why by
		/// <see cref="GateNote"/>; a founder who cannot see a design they CAN raise has no way to
		/// find out it exists.
		/// </para>
		/// </summary>
		public static bool Visible(KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			bool hidden = false;
			KingdomSystem.Guard("zoning visibility", delegate
			{
				if (!Enabled || System == null || !System.Founded || Entry == null || string.IsNullOrEmpty(Entry.Key))
				{
					return;
				}
				ZoneGate gate = GateFor(Entry.Key);
				// The second gate with no key: a design that waits on a thing the founder has never
				// heard of. Named here rather than in the catalogue or in the map, because this is
				// the one question every menu, every map row and every refusal already funnels
				// through -- so a third party's building gated on a hidden node is filtered by
				// exactly the code that filters ours. Vanilla's own precedent for an unknown recipe
				// is total omission: no greyed row, no silhouette, no count.
				if (!KingdomResearch.KnowledgeGateHeardOf(System, gate.Knowledge))
				{
					hidden = true;
					return;
				}
				if (string.IsNullOrEmpty(gate.Creed))
				{
					return;
				}
				hidden = KingdomZoningRules.NoPathToCreed(BuilderRollOf(System), gate.Creed);
			});
			return !hidden;
		}

		/// <summary>
		/// The settlement's verdict on raising one design on one piece of ground, with the module
		/// switch and every null case already folded in.
		/// </summary>
		/// <param name="System">The realm. Null permits &mdash; there is nothing to gate for.</param>
		/// <param name="ZoneID">Zone the work would stand in; its district is looked up in
		/// <c>KingdomSystem.ZoneDistricts</c>. Null or unclaimed ground reads as undistricted.</param>
		/// <param name="Entry">The design. Null permits.</param>
		public static ZoningJudgement Judge(KingdomSystem System, string ZoneID, KingdomRules.BuildEntry Entry)
		{
			return JudgeAt(System, DistrictOf(System, ZoneID), Entry, StratumOf(ZoneID));
		}

		/// <summary>
		/// Whether a zone id names ground below the surface, read off the id itself rather than
		/// off a loaded zone: the stratum is in the id, so the offer can be narrowed for ground
		/// the founder is standing on and for ground they are only planning on. An id this build
		/// cannot parse reads as the surface, which gates nothing.
		/// </summary>
		public static bool StratumOf(string ZoneID)
		{
			if (string.IsNullOrEmpty(ZoneID) || !KingdomRules.TryParseZoneID(ZoneID, out _, out _, out _, out int z))
			{
				return false;
			}
			return KingdomPlotRules.IsUnderground(z);
		}

		/// <summary>
		/// Whether a design may be raised here, with a whole player-facing sentence when it may
		/// not. The one call a commission path needs.
		/// <para>
		/// Fails OPEN: if judging itself throws, the design is permitted and the fault is logged.
		/// A bug in a gate must never be able to make a settlement unbuildable, and a founder who
		/// gets one building they should not have is a far smaller harm than one who gets a
		/// refusal nothing in the game can explain.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="ZoneID">Zone the work would stand in.</param>
		/// <param name="Entry">The design.</param>
		/// <param name="Failure">Set to the refusal when this returns false; untouched otherwise.</param>
		public static bool Permits(KingdomSystem System, string ZoneID, KingdomRules.BuildEntry Entry, out string Failure)
		{
			string refusal = null;
			KingdomSystem.Guard("zoning gate", delegate
			{
				ZoningJudgement judgement = Judge(System, ZoneID, Entry);
				if (!judgement.Permitted)
				{
					refusal = Refusal(System, ZoneID, Entry, judgement);
				}
			});
			Failure = refusal;
			return refusal == null;
		}

		/// <summary>
		/// The short coloured tag a commission or plan menu line carries when a design is
		/// blocked, so the founder sees the whole catalog and which parts of it are out of reach
		/// rather than a list that silently shortens. Null when the design may be raised.
		/// </summary>
		public static string GateNote(KingdomSystem System, string ZoneID, KingdomRules.BuildEntry Entry)
		{
			string note = null;
			KingdomSystem.Guard("zoning note", delegate
			{
				ZoningJudgement judgement = Judge(System, ZoneID, Entry);
				if (!judgement.Permitted && judgement.Note != null)
				{
					note = " {{K|[" + judgement.Note + "]}}";
				}
			});
			return note;
		}

		/// <summary>
		/// What designating this ground would cost, named before it costs it: the designs the
		/// founder can raise here today and could not raise here afterward. Zoning is meant to be
		/// a decision, and a decision the founder cannot see the price of is a trap.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="ZoneID">The ground about to be designated.</param>
		/// <param name="District">The district key being proposed.</param>
		/// <returns>A founder-facing sentence, or null when nothing would be shut out.</returns>
		public static string LockoutWarning(KingdomSystem System, string ZoneID, string District)
		{
			string warning = null;
			KingdomSystem.Guard("zoning lockout warning", delegate
			{
				if (System == null || !System.Founded || !Enabled)
				{
					return;
				}
				string current = DistrictOf(System, ZoneID);
				List<string> lost = new List<string>();
				foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
				{
					if (!Offered(System, entry))
					{
						continue;
					}
					// Judged on this ground's own stratum on both sides, so the warning names what
					// the DISTRICT would cost and never what the rock already forbids.
					bool underground = StratumOf(ZoneID);
					if (JudgeAt(System, current, entry, underground).Permitted && !JudgeAt(System, District, entry, underground).Permitted && !lost.Contains(entry.Name))
					{
						lost.Add(entry.Name);
					}
				}
				if (lost.Count > 0)
				{
					warning = "Naming this ground the " + KingdomRules.DistrictName(District) + " puts "
						+ KingdomZoningRules.JoinAnd(lost) + " beyond what may be raised here. Nothing already standing is touched, and the ground can be named again later.";
				}
			});
			return warning;
		}

		/// <summary>
		/// The keepers' own screen: what the settlement's craft stands at, everything it has been
		/// taught, and the one action that teaches it more. Owns its whole interaction the way
		/// <c>KingdomLarder</c> and <c>KingdomSalvage</c> do, so the Charter needs one line to
		/// reach it.
		/// </summary>
		/// <param name="System">The realm; must be founded.</param>
		public static void ShowKeepers(KingdomSystem System)
		{
			KingdomSystem.Guard("keepers screen", delegate
			{
				if (System == null || !System.Founded)
				{
					Popup.Show("You rule nothing yet.");
					return;
				}
				KingdomResearch.RevealRoots(System);
				KingdomResearch.EnsureBenches(System, The.ZoneManager?.ActiveZone);
				while (true)
				{
					List<GameObject> disks = CarriedDisks();
					// A fragment in hand tells the founder a thing exists before anybody is taught
					// it, which is vanilla's own idiom one step out: a disk you cannot learn from
					// still tells you what it is. Scanned here rather than on pickup, because a
					// per-turn inventory walk is a cost this design refuses to pay.
					KingdomResearch.RevealFromCarried(System, CarriedKeys(disks));
					KingdomResearch.ApplySources(System);
					List<ResearchNode> subjects = KingdomResearch.Offerable(System);
					List<ResearchNode> carried = KingdomResearch.CarriedFromAway(System);
					List<string> options = new List<string>();
					List<char> hotkeys = new List<char>();
					options.Add((disks.Count > 0)
						? "{{W|Teach the keepers a design from a data disk}}"
						: "{{K|You carry no data disk to teach from}}");
					hotkeys.Add('t');
					if (KingdomResearch.Enabled)
					{
						options.Add((subjects.Count > 0)
							? "{{W|Set the keepers a thing to work out}}"
							: "{{K|There is nothing here the keepers have heard of and not worked out}}");
						hotkeys.Add('w');
						if (carried.Count > 0)
						{
							options.Add("{{W|Set down what the keepers of " + AwayName(System) + " worked out}}");
							hotkeys.Add('s');
						}
					}
					options.Add("Close");
					hotkeys.Add('z');
					int chosen = Popup.PickOption(Title: "What the keepers of " + System.SeatName + " know", Intro: KeepersIntro(System), Options: options, Hotkeys: hotkeys, AllowEscape: true);
					if (chosen < 0 || chosen >= hotkeys.Count || hotkeys[chosen] == 'z')
					{
						return;
					}
					switch (hotkeys[chosen])
					{
					case 't':
						if (disks.Count > 0)
						{
							TeachFromDisk(System, disks);
						}
						break;
					case 'w':
						if (subjects.Count > 0)
						{
							SetSubject(System, subjects);
						}
						break;
					case 's':
						SetDownWhatWasLearned(System, carried);
						break;
					}
				}
			});
		}

		/// <summary>
		/// The district on a piece of the realm's ground: whatever the founder designated, or
		/// null for ground never designated. Unclaimed and unknown zones read as undistricted,
		/// which is the permissive answer.
		/// </summary>
		public static string DistrictOf(KingdomSystem System, string ZoneID)
		{
			if (System == null || ZoneID == null || System.ZoneDistricts == null)
			{
				return null;
			}
			return System.ZoneDistricts.TryGetValue(ZoneID, out string district) ? district : null;
		}

		private static ZoningJudgement JudgeAt(KingdomSystem System, string District, KingdomRules.BuildEntry Entry, bool Underground)
		{
			if (!Enabled || System == null || !System.Founded || Entry == null)
			{
				return ZoningJudgement.Allowed;
			}
			int claimed = (System.ClaimedZones != null) ? System.ClaimedZones.Count : 0;
			return KingdomZoningRules.Judge(GateFor(Entry.Key), District, Entry.Category, claimed, Roster(System),
				Underground, WantsSky(Entry), BuilderRollOf(System), KingdomZoningRules.StratumOfGround(Underground),
				Entry.Key, KeptMegastructure(System));
		}

		// One entry, keyed by the ground and the tick it was read on. The purpose gate is asked once
		// per catalogue row per menu redraw -- LockoutWarning alone asks it twice for every design in
		// the game -- so the read has to be cheap or it is a stutter every time a founder opens the
		// commission list. It is invalidated by the tick moving, which is the coarsest correct key:
		// a megastructure cannot appear without time passing.
		private static string KeptCacheZone;

		private static long KeptCacheTick = -1L;

		private static string KeptCacheValue;

		/// <summary>
		/// The registry key of the megastructure this city already keeps, or null when it keeps none
		/// &mdash; and null, deliberately, when nothing could tell.
		/// <para>
		/// <b>Two sources, and they are not equals.</b> The city book is the RECORD: its work rows
		/// cover every zone the city holds, including the ones nobody has stood in for a season, and
		/// a cardinality rule that only saw loaded ground would let a founder raise a second great
		/// work simply by walking away from the first. The loaded zone is the FRESHNESS PATCH: the
		/// book's work rows for a zone are rebuilt at that zone's own settlement pass
		/// (<c>KingdomCity.ReadWorks</c>), so a theatre finished since the last pass is standing in
		/// the world and not yet written down. Where the two disagree it is always in that one
		/// direction, and the patch closes it.
		/// </para>
		/// <para>
		/// <b>Derivation only &mdash; nothing here is stored.</b> A serialized "this city's purpose"
		/// field would be a second record of a thing the book already knows, and the two would drift
		/// the first time a great work was demolished.
		/// </para>
		/// <para>
		/// The book stores each work's BLUEPRINT (<c>KingdomCity.ReadWorks</c> writes
		/// <c>work.Blueprint</c> into the design-key column), so each stored value is resolved
		/// against both the registry's keys and its blueprints. Reading the raw column rather than
		/// the frozen model is deliberate and is the one place in this file that does: <c>TryRead</c>
		/// allocates a whole city &mdash; zones, works, residents &mdash; and this is a single-column
		/// scan on a hot menu path.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null yields null, which permits.</param>
		public static string KeptMegastructure(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return null;
			}
			// Before the cache is trusted, not after: this is what runs ClearGates for a freshly
			// loaded game, and a cache read that happened first could hand a second game in the same
			// session the first one's answer on a shared tick and zone.
			KingdomData.EnsureBuildings();
			Zone active = The.ZoneManager?.ActiveZone;
			string here = (active != null) ? active.ZoneID : "";
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			if (KeptCacheTick == now && string.Equals(KeptCacheZone, here))
			{
				return KeptCacheValue;
			}
			// Gathered once and searched against, rather than walking the whole catalogue for every
			// stored work: a city's book can carry forty work rows and the catalogue eighty designs,
			// and the megastructures among them are — by the rule this enforces — almost always one.
			List<string> keys = new List<string>();
			List<string> blueprints = new List<string>();
			List<KingdomRules.BuildEntry> entries = KingdomData.Buildings;
			for (int i = 0; i < entries.Count; i++)
			{
				if (GateFor(entries[i].Key).Megastructure)
				{
					keys.Add(entries[i].Key);
					blueprints.Add(entries[i].Blueprint ?? "");
				}
			}
			string kept = null;
			if (keys.Count > 0)
			{
				Simulation.City.KingdomCityBook book = System.City;
				if (book != null && book.WorkDesignKeys != null)
				{
					for (int i = 0; i < book.WorkDesignKeys.Count && kept == null; i++)
					{
						kept = MegastructureKeyOf(book.WorkDesignKeys[i], keys, blueprints);
					}
				}
				if (kept == null && active != null)
				{
					foreach (GameObject work in active.GetObjects())
					{
						if (work == null || work.GetIntProperty("KingdomBuilt") != 1)
						{
							continue;
						}
						kept = MegastructureKeyOf(KingdomUpgrade.DesignKeyOf(work), keys, blueprints);
						if (kept != null)
						{
							break;
						}
					}
				}
			}
			KeptCacheZone = here;
			KeptCacheTick = now;
			KeptCacheValue = kept;
			return kept;
		}

		/// <summary>
		/// The registry key a stored work-row value names, if that design is a megastructure.
		/// Matched against the registry's KEYS first and its BLUEPRINTS second, because the book's
		/// column carries a blueprint (<c>KingdomCity.ReadWorks</c>) while the loaded-zone read
		/// carries a key (<c>KingdomUpgrade.DesignKeyOf</c>), and a rule that read only one of the
		/// two would be right about half its callers.
		/// </summary>
		private static string MegastructureKeyOf(string Stored, List<string> Keys, List<string> Blueprints)
		{
			if (string.IsNullOrEmpty(Stored))
			{
				return null;
			}
			for (int i = 0; i < Keys.Count; i++)
			{
				if (string.Equals(Keys[i], Stored) || string.Equals(Blueprints[i], Stored))
				{
					return Keys[i];
				}
			}
			return null;
		}

		// The weather half of the depth gate is the design's own Sky flag, which lives on the plot
		// spec rather than on the build entry. A design the plot registry never registered wants no
		// weather by definition, so it is never refused for the want of it. Read in one place
		// because the refusal has to ask the same question the judgement did, and two lookups that
		// could ever disagree would put a sentence in front of the founder that was true of neither.
		private static bool WantsSky(KingdomRules.BuildEntry Entry)
		{
			KingdomPlotRules.PlotSpec spec;
			return Entry != null && KingdomPlots.TryGetSpec(Entry.Key, out spec) && spec != null && spec.RequiresSky;
		}

		/// <summary>
		/// Composes a refusal in the settlement's own voice. Every branch names the lack AND the
		/// act that lifts it &mdash; the whole point of the gate is to teach the founder what the
		/// realm is short of, and a refusal that only says "no" teaches nothing.
		/// </summary>
		private static string Refusal(KingdomSystem System, string ZoneID, KingdomRules.BuildEntry Entry, ZoningJudgement Judgement)
		{
			string seat = (System != null) ? System.SeatName : "the settlement";
			string name = (Entry != null) ? Entry.Name : "that";
			switch (Judgement.Verdict)
			{
			case ZoningVerdict.RefusedUnlearned:
				return "Nobody at " + seat + " knows how to raise " + XRL.Language.Grammar.A(name) + ". It wants {{C|" + Judgement.Detail
					+ "}}. Teach the keepers from a data disk you carry, certify one hauled home, or take in people who already do the work.";
			case ZoningVerdict.RefusedTechLevel:
				return seat + " builds at the level of {{C|" + KingdomZoningRules.TechName(Tech(System)) + "}}, and "
					+ XRL.Language.Grammar.A(name) + " wants {{C|" + Judgement.Detail
					+ "}}. Teach the keepers more designs and certify more machines hauled home; the craft rises with the work, not with the asking.";
			case ZoningVerdict.RefusedTerritory:
				return XRL.Language.Grammar.A(name) + " wants a realm of at least {{C|" + Judgement.Detail + "}}, and " + seat
					+ " holds {{C|" + ((System != null && System.ClaimedZones != null) ? System.ClaimedZones.Count : 0) + "}}. Claim more ground and ask again.";
			case ZoningVerdict.RefusedStratum:
				// One verdict, two refusals, and they want different sentences: the weather is a
				// fact about the rock and the set is a fact about the catalogue. Asked in the same
				// order Judge asks them, so the words always match the reason.
				if (!KingdomZoningRules.StratumAccepts(StratumOf(ZoneID), WantsSky(Entry)))
				{
					return XRL.Language.Grammar.A(name) + " wants weather — sun, wind, or rain — and there is none under the rock. Raise it on ground under {{C|"
						+ Judgement.Detail + "}}.";
				}
				return XRL.Language.Grammar.A(name) + " belongs to {{C|" + Judgement.Detail + "}}, and this ground is {{C|"
					+ KingdomZoningRules.StratumName(KingdomZoningRules.StratumOfGround(StratumOf(ZoneID)))
					+ "}}. Claim ground there and raise it there — a claim reaches the stratum directly above or below the one you hold.";
			case ZoningVerdict.RefusedUnaligned:
				return "Nobody at " + seat + " holds with {{C|" + KingdomCreed.CreedName(Judgement.Detail) + "}}, and nobody here ever has. "
					+ XRL.Language.Grammar.A(name) + " is raised by people who believe it, or who once did. Take in people who hold with them, or let the creed spread here.";
			case ZoningVerdict.RefusedCreedShare:
			{
				string creed = KingdomCreed.CreedName(Judgement.Detail);
				int holding = (System != null && System.CreedCounts != null && System.CreedCounts.TryGetValue(Judgement.Detail, out var held)) ? held : 0;
				int people = (System != null) ? System.Population : 0;
				int wanted = (Entry != null) ? GateFor(Entry.Key).EffectiveCreedShare : KingdomCreedRules.DominantSharePercent;
				return XRL.Language.Grammar.A(name) + " wants {{C|" + wanted + "%}} of the city holding with {{C|" + creed
					+ "}}, and " + seat + " has {{C|" + holding + "}} of {{C|" + people + "}} ("
					+ KingdomZoningRules.ShareHeld(holding, people) + "%, and never fewer than "
					+ KingdomCreedRules.MinBelievers + " of them). A creed-work waits on a congregation, not on a convert.";
			}
			case ZoningVerdict.RefusedBuilders:
				return XRL.Language.Grammar.A(name) + " is raised by {{C|" + Judgement.Detail + "}}, and there is nobody at " + seat
					+ " who answers to that. Grow, take in people from further off, or wait for somebody who does.";
			case ZoningVerdict.RefusedDistrict:
			{
				string here = DistrictOf(System, ZoneID);
				string standing = string.IsNullOrEmpty(here)
					? "This ground carries no district"
					: ("This ground is the {{C|" + KingdomRules.DistrictName(here) + "}}");
				return standing + ", and " + XRL.Language.Grammar.A(name) + " is raised in {{C|" + Judgement.Detail
					+ "}}. Name this ground from the Charter, or walk to ground that already carries it.";
			}
			case ZoningVerdict.RefusedMegastructure:
				// The Judgement carries the KEY; the founder is owed the NAME. Composed here, where
				// the catalogue can be asked, so the rules layer never has to know it exists.
				return KingdomLabRules.PurposeRefusalLine(KingdomUpgrade.DisplayNameOf(Judgement.Detail));
			default:
				return XRL.Language.Grammar.A(name) + " cannot be raised here.";
			}
		}

		// The stand-in for the lab building's own action, and named one. Verdict 3 rules that the
		// one pressable thing is the building in the world; until the laboratory is raised, the
		// keepers' own screen is where a subject is taken up, and this whole method moves onto the
		// lab the day it exists. Nothing else about the loop changes when it does.
		private static void SetSubject(KingdomSystem System, List<ResearchNode> Subjects)
		{
			List<string> options = new List<string>();
			for (int i = 0; i < Subjects.Count; i++)
			{
				string refusal;
				bool can = KingdomResearch.CanTakeUp(System, Subjects[i], out refusal);
				options.Add(Subjects[i].Named + (can ? "" : " {{K|[not yet]}}"));
			}
			int chosen = Popup.PickOption(Title: "What shall they work out?",
				Intro: "One thing at a time, and nothing else moves while they do it. Setting a new subject aside keeps whatever work already stands on it.",
				Options: options, AllowEscape: true);
			if (chosen < 0)
			{
				return;
			}
			string failure;
			if (!KingdomResearch.TakeUp(System, Subjects[chosen].Key, out failure))
			{
				Popup.Show(failure);
				return;
			}
			Popup.Show("{{G|The keepers of " + System.SeatName + " take up " + Subjects[chosen].Named + ".}} What comes of it comes of their own work, in their own time.");
		}

		// The teaching act (Addendum 22 B4). What crosses between two of the founder's cities is a
		// SEED and never a holding: the founder sets down what one city's keepers worked out, the
		// other city's keepers have the shape of it, and the walking is still theirs. Doors, never
		// rooms - Addendum 18's clause, applied to the road between two of your own cities exactly
		// as it applies to the road out of exile.
		private static void SetDownWhatWasLearned(KingdomSystem System, List<ResearchNode> Carried)
		{
			string away = AwayName(System);
			List<string> named = new List<string>();
			for (int i = 0; i < Carried.Count; i++)
			{
				if (KingdomResearch.Seed(System, Carried[i].Key, "the keepers of " + away))
				{
					named.Add(Carried[i].Named);
				}
			}
			if (named.Count == 0)
			{
				Popup.Show("There is nothing here they could not already have told you themselves.");
				return;
			}
			Popup.Show("{{G|You set it down for them: " + KingdomZoningRules.JoinAnd(named) + ".}} The keepers of "
				+ System.SeatName + " have the shape of it now. The rest of the walking is theirs.");
			KingdomChronicle.Record(System, "what the keepers of " + away + " knew was set down at " + System.SeatName);
		}

		private static string AwayName(KingdomSystem System)
		{
			return (System.Away != null && !string.IsNullOrEmpty(System.Away.SettlementName))
				? System.Away.SettlementName
				: "your other city";
		}

		private static string KeepersIntro(KingdomSystem System)
		{
			List<string> roster = Roster(System);
			int points = KingdomZoningRules.TechPoints(roster);
			TechLevel level = KingdomZoningRules.LevelForPoints(points);
			int wanted = KingdomZoningRules.PointsToNext(points);
			StringBuilder text = new StringBuilder();
			text.Append(System.SeatName).Append(" builds at the level of {{C|").Append(KingdomZoningRules.TechName(level)).Append("}}.");
			text.Append(wanted <= 0
				? "\n{{K|The keepers have learned everything this settlement can teach itself.}}"
				: ("\n{{K|" + wanted + " more toward " + KingdomZoningRules.TechName((TechLevel)((int)level + 1))
					+ ". A design taught is worth " + KingdomZoningRules.TechPointsPerDisk
					+ "; a machine certified fit for the grid is worth " + KingdomZoningRules.TechPointsPerCertification + ".}}"));
			AppendKind(text, roster, KingdomZoningRules.KindDisk, "\n\nTaught to the keepers: ");
			AppendKind(text, roster, KingdomZoningRules.KindMachine, "\nCertified fit for the grid: ");
			AppendKind(text, roster, KingdomZoningRules.KindOrigin, "\nTrades among the people: ");
			AppendKind(text, roster, KingdomZoningRules.KindNode, "\nWorked out here: ");
			AppendKind(text, roster, KingdomCeremonyRules.PatternKnowledgeKind, "\nHeld from a ceremony here: ");
			return text.ToString();
		}

		private static void AppendKind(StringBuilder Text, List<string> Roster, string Kind, string Label)
		{
			List<string> named = new List<string>();
			foreach (string key in Roster)
			{
				if (KingdomZoningRules.KindOf(key) == Kind)
				{
					string name = KingdomZoningRules.NameOf(key);
					if (name != null && !named.Contains(name))
					{
						named.Add(name);
					}
				}
			}
			if (named.Count > 0)
			{
				Text.Append(Label).Append(KingdomZoningRules.JoinAnd(named));
			}
		}

		// Only what the founder is actually carrying. A disk lying in a chest somewhere is not
		// something the keepers can be taught from, and reaching into containers the founder
		// merely owns would be the protection law's exact prohibition (STANDARDS 7).
		private static List<GameObject> CarriedDisks()
		{
			List<GameObject> disks = new List<GameObject>();
			Inventory inventory = The.Player?.Inventory;
			if (inventory == null)
			{
				return disks;
			}
			foreach (GameObject item in inventory.GetObjects())
			{
				DataDisk disk = item?.GetPart<DataDisk>();
				if (disk != null && disk.Data != null && !string.IsNullOrEmpty(DiskName(disk)))
				{
					disks.Add(item);
				}
			}
			return disks;
		}

		// The roster keys the founder is carrying right now, as a node's TaughtBy and SeededBy
		// lists would spell them. Never stored: this is what is in their hands this moment.
		private static List<string> CarriedKeys(List<GameObject> Disks)
		{
			List<string> keys = new List<string>();
			for (int i = 0; Disks != null && i < Disks.Count; i++)
			{
				string key = KingdomZoningRules.ComposeKey(KingdomZoningRules.KindDisk, DiskName(Disks[i].GetPart<DataDisk>()));
				if (key != null && !keys.Contains(key))
				{
					keys.Add(key);
				}
			}
			return keys;
		}

		/// <summary>
		/// The name a disk teaches under: an item modification's own display name, otherwise the
		/// blueprint the recipe builds. This is the string an author writes in a
		/// <c>Knowledge</c> attribute, so it has to be the one the founder reads on the screen.
		/// </summary>
		private static string DiskName(DataDisk Disk)
		{
			if (Disk == null || Disk.Data == null)
			{
				return null;
			}
			if (Disk.Data.Type == "Mod" && !string.IsNullOrEmpty(Disk.Data.DisplayName))
			{
				return Disk.Data.DisplayName;
			}
			return Disk.Data.Blueprint;
		}

		// The disk is not consumed. Vanilla's own "Learn" action destroys it because it writes
		// into the PLAYER's recipe list, which is a different ledger; here the founder is lending
		// the keepers something to copy, and taking a player's property to do it would be the
		// protection law broken for a convenience.
		private static void TeachFromDisk(KingdomSystem System, List<GameObject> Disks)
		{
			List<string> options = new List<string>();
			for (int i = 0; i < Disks.Count; i++)
			{
				string name = DiskName(Disks[i].GetPart<DataDisk>());
				bool known = KingdomZoningRules.Knows(Roster(System), KingdomZoningRules.ComposeKey(KingdomZoningRules.KindDisk, name));
				options.Add(name + (known ? " {{K|[already known here]}}" : ""));
			}
			int chosen = Popup.PickOption(Title: "Teach the keepers", Intro: "The disk is read and handed back. Nothing you carry is spent.", Options: options, AllowEscape: true);
			if (chosen < 0)
			{
				return;
			}
			string design = DiskName(Disks[chosen].GetPart<DataDisk>());
			if (!Learn(System, KingdomZoningRules.KindDisk, design))
			{
				Popup.Show("The keepers of " + System.SeatName + " already have that one written down.");
				return;
			}
			KingdomChronicle.Record(System, "the keepers of " + System.KingdomDisplayName + " were taught to build " + design);
			System.RecordDeed("taught the keepers of " + System.KingdomDisplayName + " to build " + design);
			Popup.Show("{{G|The keepers copy it out and hand the disk back.}} " + System.SeatName + " can raise " + XRL.Language.Grammar.A(design) + " when the ground and the stores allow.");
			// A roll changed, so a node somebody had already answered may now be answered here.
			KingdomResearch.ApplySources(System);
		}

		// The seated city's own rolls, with the one-time fold in front of them.
		//
		// THE FOLD IS A SHIM AND IS NAMED ONE. Before the knowledge siting the roster was a single
		// string on the game; a save written then carries it there and carries nothing on its
		// cities. This reads it into the seat once and retires the key, so the same save never
		// folds twice and a second city never inherits the first one's rolls by accident. It is not
		// a migration harness and it is not a policy: when the release-era harness lands
		// (Addendum 9) this is the first thing it should absorb.
		private static string Stored(KingdomSystem System)
		{
			if (System == null)
			{
				return "";
			}
			string legacy = The.Game?.GetStringGameState(RosterState, "") ?? "";
			if (legacy.Length > 0)
			{
				if (string.IsNullOrEmpty(System.KeepersRoster))
				{
					System.KeepersRoster = legacy;
					KingdomLog.Log("zoning: folded the old game-held roster into " + System.SeatName + " and retired the key");
				}
				The.Game?.SetStringGameState(RosterState, "");
			}
			return System.KeepersRoster ?? "";
		}

		private static void Store(KingdomSystem System, string Roster)
		{
			if (System == null)
			{
				return;
			}
			System.KeepersRoster = Roster ?? "";
		}
	}
}
