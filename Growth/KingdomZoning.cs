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
		/// Game-state key the keepers' roster is stored under. A flat, self-describing
		/// string-to-string entry on the game rather than a new serialized field on
		/// <c>KingdomSystem</c>: it cannot break positional field reflection, an older save
		/// simply has no entry and reads as an empty roster, and a newer save read by an older
		/// build carries a key that build ignores. Realm-wide on purpose &mdash; what the keepers
		/// learned travels with the founder's own people to the founder's other city.
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
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes(Key, Districts, MinZones, Knowledge, MinTech, out string error);
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
		/// Every knowledge key the settlement holds: the stored roster of designs taught and
		/// machines certified, plus one <c>origin:</c> key for each people living here right now.
		/// Origins are read live off <c>KingdomSystem.OriginCounts</c> rather than stored,
		/// because a trade the settlement holds only because somebody from that country lives
		/// here should leave with them.
		/// </summary>
		/// <param name="System">The realm. Null yields an empty roster.</param>
		public static List<string> Roster(KingdomSystem System)
		{
			List<string> roster = KingdomZoningRules.DecodeRoster(Stored());
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
		/// Adds one design to the keepers' stored knowledge. Idempotent: teaching the same design
		/// twice changes nothing and reports false, so nothing can be farmed by repetition.
		/// Announces a rise in the settlement's craft when one happens, once, where the founder
		/// is standing.
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
			List<string> stored = KingdomZoningRules.DecodeRoster(Stored());
			if (stored.Contains(key))
			{
				return false;
			}
			TechLevel before = KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(stored));
			stored.Add(key);
			Store(KingdomZoningRules.EncodeRoster(stored));
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
		/// the grid later returns the machine to the founder, not the knowledge to nobody. Safe
		/// to call for a machine already recorded.
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
				Learn(System, KingdomZoningRules.KindMachine, Machine.Blueprint);
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
		/// The settlement's verdict on raising one design on one piece of ground, with the module
		/// switch and every null case already folded in.
		/// </summary>
		/// <param name="System">The realm. Null permits &mdash; there is nothing to gate for.</param>
		/// <param name="ZoneID">Zone the work would stand in; its district is looked up in
		/// <c>KingdomSystem.ZoneDistricts</c>. Null or unclaimed ground reads as undistricted.</param>
		/// <param name="Entry">The design. Null permits.</param>
		public static ZoningJudgement Judge(KingdomSystem System, string ZoneID, KingdomRules.BuildEntry Entry)
		{
			return JudgeAt(System, DistrictOf(System, ZoneID), Entry);
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
					if (!KingdomRules.StyleAllows(entry.Styles, System.Style) || System.Stage < entry.MinStage)
					{
						continue;
					}
					if (JudgeAt(System, current, entry).Permitted && !JudgeAt(System, District, entry).Permitted && !lost.Contains(entry.Name))
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
				while (true)
				{
					List<GameObject> disks = CarriedDisks();
					List<string> options = new List<string>();
					List<char> hotkeys = new List<char>();
					options.Add((disks.Count > 0)
						? "{{W|Teach the keepers a design from a data disk}}"
						: "{{K|You carry no data disk to teach from}}");
					hotkeys.Add('t');
					options.Add("Close");
					hotkeys.Add('z');
					int chosen = Popup.PickOption(Title: "What the keepers of " + System.SeatName + " know", Intro: KeepersIntro(System), Options: options, Hotkeys: hotkeys, AllowEscape: true);
					if (chosen != 0 || disks.Count == 0)
					{
						return;
					}
					TeachFromDisk(System, disks);
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

		private static ZoningJudgement JudgeAt(KingdomSystem System, string District, KingdomRules.BuildEntry Entry)
		{
			if (!Enabled || System == null || !System.Founded || Entry == null)
			{
				return ZoningJudgement.Allowed;
			}
			int claimed = (System.ClaimedZones != null) ? System.ClaimedZones.Count : 0;
			return KingdomZoningRules.Judge(GateFor(Entry.Key), District, Entry.Category, claimed, Roster(System));
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
			case ZoningVerdict.RefusedDistrict:
			{
				string here = DistrictOf(System, ZoneID);
				string standing = string.IsNullOrEmpty(here)
					? "This ground carries no district"
					: ("This ground is the {{C|" + KingdomRules.DistrictName(here) + "}}");
				return standing + ", and " + XRL.Language.Grammar.A(name) + " is raised in {{C|" + Judgement.Detail
					+ "}}. Name this ground from the Charter, or walk to ground that already carries it.";
			}
			default:
				return XRL.Language.Grammar.A(name) + " cannot be raised here.";
			}
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
		}

		private static string Stored()
		{
			return The.Game?.GetStringGameState(RosterState, "") ?? "";
		}

		private static void Store(string Roster)
		{
			The.Game?.SetStringGameState(RosterState, Roster ?? "");
		}
	}
}
