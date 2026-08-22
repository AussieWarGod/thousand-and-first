using System;
using System.Collections.Generic;
using System.Reflection;

using Qud.API;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the procedure system: the registry of authored records, the
	/// founder's ledger of which named procedures they have found, the read of a real body into the
	/// engine-free vocabulary the rules judge in, and the three write paths that actually change a
	/// founder.
	/// <para>
	/// <b>The whole doctrine, in one sentence: your sting is its sting.</b> What the founder gets is
	/// the source creature's own part with the source creature's own numbers in it, never a fresh
	/// instance built from a class name. Playable Slime grants by name and loses the field state;
	/// the precedent had to hand-patch one mutation's identity because of it, and this system makes
	/// that class of bug structurally impossible by never learning a creature's name at all.
	/// </para>
	/// <para>
	/// <b>How that survives the preservation chain, which is not how the precedent does it.</b>
	/// Trophic Absorption snapshots a live <c>PartsList</c> and calls <c>IPart.DeepCopy</c> in the
	/// same turn, because its source is still in memory. Ours is not: the creature is butchered, the
	/// raw part is obliterated into preserved parts at the vat-house, and the graft may happen a
	/// season and a reload later. So the field state is STAMPED onto the preserved item at butcher
	/// time (<c>KingdomProcedureRules.FormatStamp</c>) and the part is rebuilt from the stamp at
	/// graft time by instantiating the type and setting its fields from strings &mdash; which is
	/// what <c>GamePartBlueprint</c> itself does with every part in the game
	/// (<c>D/XRL/World/GamePartBlueprint.cs</c>), and which preserves the doctrine exactly while
	/// asking nothing of an object that no longer exists.
	/// </para>
	/// </summary>
	public static class KingdomProcedures
	{
		/// <summary>Whether the lab is switched on. Off, no record loads, nothing is discovered, and
		/// no building offers a verb.</summary>
		public static bool Enabled => XRL.UI.Options.GetOption("r_TAF_OptionLab") != "No";

		/// <summary>The journal id a named procedure's discovery bit lives under. A string in the
		/// save rather than an ordinal, so a mod adding procedures never renumbers ours.</summary>
		public const string NotePrefix = "taf:procedure:";

		/// <summary>The journal category a found procedure files under.</summary>
		public const string NoteCategory = "general";

		/// <summary>
		/// The property a preserved part carries its stamp under: what the creature was bearing,
		/// read BEFORE it was butchered.
		/// </summary>
		public const string StampProperty = "r_TAF_LabStamp";

		/// <summary>The property a preserved part carries the source's own display name under, so
		/// the slate can say what a thing came off without holding a reference to a dead creature.</summary>
		public const string SourceProperty = "r_TAF_LabSource";

		/// <summary>
		/// Every graft's manager key, so <c>Body.RemovePartsByManager</c> undoes one in a single
		/// call &mdash; the precedent's own reversal shape
		/// (<c>D/XRL/World/Parts/CyberneticsGraftedMirrorArm.cs:38</c>).
		/// </summary>
		public static string ManagerFor(string Key)
		{
			return "TAF::Lab::" + (Key ?? "");
		}

		private static List<LabProcedure> _procedures;

		private static readonly Dictionary<string, LabProcedure> ByKey = new Dictionary<string, LabProcedure>();

		private static bool NotesFiled;

		/// <summary>The whole registry, in the order the files declared it. Ties anywhere in this
		/// system break on key ascending, so the same founder on the same save reads the same slate
		/// in the same order.</summary>
		public static List<LabProcedure> All
		{
			get
			{
				EnsureLoaded();
				return _procedures;
			}
		}

		/// <summary>One record by key, or false. Keys are folded, like every other registry's.</summary>
		public static bool TryGet(string Key, out LabProcedure Procedure)
		{
			EnsureLoaded();
			Procedure = null;
			if (string.IsNullOrEmpty(Key))
			{
				return false;
			}
			return ByKey.TryGetValue(Key.Trim().ToLowerInvariant(), out Procedure);
		}

		/// <summary>Forgets the registry and everything cached about the world. Called by the
		/// registry loader and on a game load, so a reload never leaves a record or a filed journal
		/// note behind from another game.</summary>
		public static void Reload()
		{
			_procedures = null;
			ByKey.Clear();
			NotesFiled = false;
		}

		// ==================================================================================
		// The registry
		// ==================================================================================

		private static void EnsureLoaded()
		{
			if (_procedures != null)
			{
				return;
			}
			_procedures = new List<LabProcedure>();
			ByKey.Clear();
			Dictionary<string, Action<XmlDataHelper>> handlers = null;
			handlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdomprocedures",
					delegate(XmlDataHelper xml)
					{
						xml.HandleNodes(handlers);
					}
				},
				{ "procedure", HandleProcedure }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot("KingdomProcedures"))
			{
				item.HandleNodes(handlers);
			}
			foreach (string finding in KingdomProcedureRules.Validate(_procedures))
			{
				KingdomLog.Log("KingdomProcedures: " + finding);
			}
		}

		private static void HandleProcedure(XmlDataHelper xml)
		{
			// Every attribute is read unconditionally, for the reason the catalogue reads its own
			// that way: the engine records which attributes a pass asked for and warns about the
			// rest, so a pass that skips one on a fault makes the loader complain about the file.
			string key = xml.GetAttribute("Key");
			string displayName = xml.GetAttribute("DisplayName");
			string cls = xml.GetAttribute("Class");
			string grants = xml.GetAttribute("Grants");
			string slots = xml.GetAttribute("Slots");
			string slotCategories = xml.GetAttribute("SlotCategories");
			string source = xml.GetAttribute("Source");
			string attach = xml.GetAttribute("Attach");
			string minRung = xml.GetAttribute("MinRung");
			string cost = xml.GetAttribute("Cost");
			string bits = xml.GetAttribute("Bits");
			string staffDays = xml.GetAttribute("StaffDays");
			string preserved = xml.GetAttribute("Preserved");
			string creeds = xml.GetAttribute("Creeds");
			string knowledge = xml.GetAttribute("Knowledge");
			string magnitude = xml.GetAttribute("Magnitude");
			LabProcedure procedure;
			string error;
			if (!KingdomProcedureRules.TryParseProcedureAttributes(key, displayName, cls, grants, slots, slotCategories,
				source, attach, minRung, cost, bits, staffDays, preserved, creeds, knowledge, magnitude,
				out procedure, out error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomProcedures: " + error);
				SkipChildren(xml);
				return;
			}
			// HandleNodes stands in for DoneWithElement: it returns at once on a self-closing
			// <procedure/> and otherwise dispatches the disclosure lines, which a merging file
			// appends to exactly as it appends skins to a building.
			xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"discloses",
					delegate(XmlDataHelper child)
					{
						string text = child.GetAttribute("Text");
						if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
						{
							procedure.Discloses.Add(text.Trim());
						}
						child.DoneWithElement();
					}
				}
			});
			for (int i = 0; i < _procedures.Count; i++)
			{
				if (_procedures[i].Key == procedure.Key)
				{
					// In place, so the registry keeps first-declaration order: a mod that re-prices
					// a procedure does not move it to the bottom of the founder's slate.
					_procedures[i] = procedure;
					ByKey[procedure.Key] = procedure;
					return;
				}
			}
			_procedures.Add(procedure);
			ByKey[procedure.Key] = procedure;
		}

		private static void SkipChildren(XmlDataHelper xml)
		{
			xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>(), delegate(XmlDataHelper child)
			{
				child.DoneWithElement();
			});
		}

		// ==================================================================================
		// Discovery — the visibility law, enforced by the accessor and not by discipline
		// ==================================================================================

		/// <summary>The journal id one named procedure's discovery bit lives under.</summary>
		public static string NoteId(string Key)
		{
			return string.IsNullOrEmpty(Key) ? null : (NotePrefix + Key.Trim().ToLowerInvariant());
		}

		/// <summary>
		/// Files one unrevealed journal note per named procedure, once per game. Vanilla refuses an
		/// id it already holds, so this is idempotent whatever calls it.
		/// </summary>
		public static void FileNotes()
		{
			if (NotesFiled || !Enabled)
			{
				return;
			}
			EnsureLoaded();
			NotesFiled = true;
			for (int i = 0; i < _procedures.Count; i++)
			{
				LabProcedure procedure = _procedures[i];
				if (!procedure.IsNamed)
				{
					continue;
				}
				string id = NoteId(procedure.Key);
				if (id == null || JournalAPI.GetObservation(id) != null)
				{
					continue;
				}
				JournalAPI.AddObservation(
					"There is a thing that can be done to a body, and it is called " + procedure.Named + ".",
					id, NoteCategory, id, null, revealed: false, -1L);
			}
		}

		/// <summary>
		/// Whether this founder may see a procedure at all.
		/// <para>
		/// <b>Every surface asks this before it draws a row</b>, which is what makes "cannot have
		/// it" and "have never heard of it" the same absence of a row rather than two renderings. An
		/// ordinary record is always visible; a named one is invisible until it is found in the
		/// world (Addendum 14 at full strength, Addendum 20's hidden clause).
		/// </para>
		/// </summary>
		public static bool Discovered(LabProcedure Procedure)
		{
			if (Procedure == null || !Enabled)
			{
				return false;
			}
			if (!Procedure.IsNamed)
			{
				return true;
			}
			FileNotes();
			string id = NoteId(Procedure.Key);
			return id != null && JournalAPI.HasNote(id);
		}

		/// <summary>
		/// Tells the founder a named procedure exists, and where they heard it. Vanilla stamps the
		/// provenance on the entry itself, so the chronicle line writes itself.
		/// </summary>
		/// <returns>True when this call is what revealed it.</returns>
		public static bool Reveal(string Key, string LearnedFrom)
		{
			LabProcedure procedure;
			if (!Enabled || !TryGet(Key, out procedure) || !procedure.IsNamed || Discovered(procedure))
			{
				return false;
			}
			string id = NoteId(procedure.Key);
			if (id == null || !JournalAPI.TryRevealNote(id, LearnedFrom))
			{
				return false;
			}
			KingdomLog.Log("lab: found " + procedure.Key + ((LearnedFrom == null) ? "" : (" (" + LearnedFrom + ")")));
			return true;
		}

		// ==================================================================================
		// Reading a real body
		// ==================================================================================

		/// <summary>
		/// The founder's own anatomy, in the vocabulary the rules judge in.
		/// <para>
		/// <b>This is the rationing mechanism and there is no other one.</b> A procedure's
		/// <c>Slots</c> is checked against what this founder actually has, not against a table, so a
		/// True Kin, a robot player and a slime player each get a different legal set for free
		/// &mdash; derived, with no genotype list anywhere in this codebase (DIVERSITY &sect;3.4
		/// hard rules 2 and 3).
		/// </para>
		/// <para>
		/// Anatomy order is kept, because the founder reads their own body the way the game lists it
		/// and the slate must say it back the same way.
		/// </para>
		/// </summary>
		/// <param name="Who">The founder. Null or bodiless reads as an empty anatomy, which refuses
		/// everything by name rather than throwing.</param>
		/// <param name="Names">Filled with each slot's name as the founder would say it, index for
		/// index with the returned list. May be null.</param>
		public static List<LabSlot> Census(GameObject Who, List<string> Names = null)
		{
			List<LabSlot> anatomy = new List<LabSlot>();
			XRL.World.Parts.Body body = Who?.Body;
			if (body == null)
			{
				return anatomy;
			}
			List<BodyPart> parts = body.GetParts();
			if (parts == null)
			{
				return anatomy;
			}
			for (int i = 0; i < parts.Count; i++)
			{
				BodyPart part = parts[i];
				if (part == null || part.Abstract)
				{
					continue;
				}
				anatomy.Add(new LabSlot(part.Type, part.Category, part.Extrinsic,
					GameObject.Validate(part.DefaultBehavior), part.Manager));
				Names?.Add(part.GetOrdinalName());
			}
			return anatomy;
		}

		/// <summary>
		/// A record's <c>SlotCategories</c> as engine codes.
		/// <para>
		/// Resolved through <c>BodyPartCategory</c>'s own name table rather than a table of ours
		/// (<c>D/XRL/World/Anatomy/BodyPartCategory.cs:104-165</c>), which is why a modded category
		/// would work here the day the engine had one. A name the engine does not know resolves to
		/// zero and is DROPPED with a logged reason rather than silently admitting everything
		/// &mdash; hostile-input discipline, and the difference between a typo and an open door.
		/// </para>
		/// </summary>
		/// <returns>Empty when the record names none, which admits any live category.</returns>
		public static List<int> Categories(LabProcedure Procedure)
		{
			List<int> codes = new List<int>();
			List<string> names = KingdomProcedureRules.SlotCategoryNames(Procedure);
			for (int i = 0; i < names.Count; i++)
			{
				int code = BodyPartCategory.GetCodeIfExists(names[i]);
				if (code <= 0)
				{
					KingdomLog.Log("KingdomProcedures: procedure " + Procedure.Key + " names category \"" + names[i]
						+ "\", which the engine does not know. Dropped.");
					continue;
				}
				if (!codes.Contains(code))
				{
					codes.Add(code);
				}
			}
			return codes;
		}

		// ==================================================================================
		// The stamp — read the creature BEFORE it is butchered
		// ==================================================================================

		/// <summary>
		/// Reads a creature's parts and their field values into one stamp string.
		/// <para>
		/// Called with the creature still whole, because after butchering there is nothing useful to
		/// read &mdash; the precedent learned that the hard way and wrote it down
		/// (DIVERSITY &sect;3.0b, technique 1). Only PUBLIC INSTANCE fields are read, which is
		/// exactly the set <c>IPart.DeepCopy</c> itself carries
		/// (<c>D/XRL/World/IPart.cs:410-426</c>), so a stamp round-trips to the same part the
		/// precedent's own copy would have produced.
		/// </para>
		/// </summary>
		/// <param name="Creature">The thing about to be butchered.</param>
		/// <returns>The stamp, or the empty string when there was nothing worth stamping.</returns>
		public static string Stamp(GameObject Creature)
		{
			List<string> stamps = new List<string>();
			if (Creature?.PartsList == null)
			{
				return "";
			}
			for (int i = 0; i < Creature.PartsList.Count; i++)
			{
				IPart part = Creature.PartsList[i];
				if (part == null)
				{
					continue;
				}
				string name = part.GetType().Name;
				// The blocklist is applied at the STAMP as well as at the load, so a blocked class
				// is never even written down. A stamp is data in a save; a save that carries the
				// name of a thing we refuse to grant is a save that invites somebody to try.
				if (KingdomProcedureRules.Blocked(name))
				{
					continue;
				}
				string stamp = KingdomProcedureRules.FormatStamp(name, ReadFields(part));
				if (stamp != null)
				{
					stamps.Add(stamp);
				}
			}
			return KingdomProcedureRules.FormatStamps(stamps);
		}

		private static List<KeyValuePair<string, string>> ReadFields(IPart Part)
		{
			List<KeyValuePair<string, string>> fields = new List<KeyValuePair<string, string>>();
			FieldInfo[] declared = Part.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
			for (int i = 0; i < declared.Length; i++)
			{
				FieldInfo field = declared[i];
				if (field.IsLiteral || !IsPlain(field.FieldType))
				{
					continue;
				}
				object value = field.GetValue(Part);
				if (value == null)
				{
					continue;
				}
				fields.Add(new KeyValuePair<string, string>(field.Name, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)));
			}
			return fields;
		}

		/// <summary>
		/// Whether a field is of a kind a string can carry back whole.
		/// <para>
		/// Deliberately narrow. A field holding a <c>GameObject</c> or a collection cannot survive a
		/// stamp &mdash; <c>IPart.DeepCopy</c> itself only aliases such references and its own
		/// <c>MapInv</c> overload ignores its map (<c>D/XRL/World/IPart.cs:437-439</c>) &mdash; so
		/// they are left at the rebuilt part's own defaults rather than being half-carried. A part
		/// whose whole behaviour lives in such a field is a part this system should not be granting,
		/// and the audit is where that judgment belongs, not here.
		/// </para>
		/// </summary>
		private static bool IsPlain(Type Type)
		{
			return Type == typeof(string) || Type == typeof(int) || Type == typeof(long) || Type == typeof(bool)
				|| Type == typeof(float) || Type == typeof(double) || Type == typeof(short) || Type == typeof(byte)
				|| Type.IsEnum;
		}

		// ==================================================================================
		// The three write paths
		// ==================================================================================

		/// <summary>
		/// Performs one procedure on one founder.
		/// <para>
		/// Every graft carries <c>Manager = TAF::Lab::&lt;key&gt;</c> whether it is a limb or a
		/// part, so <see cref="Remove"/> undoes any of them in one call and nothing the lab does is
		/// permanent against the founder's will. That is the consent story, and it is also the
		/// escape hatch for the failure mode Playable Golem is remembered for: if a graft is what
		/// stranded you, it can come off (DIVERSITY &sect;3.0c, &sect;3.9 risk 4).
		/// </para>
		/// </summary>
		/// <param name="Who">The founder.</param>
		/// <param name="Procedure">The record.</param>
		/// <param name="SlotIndex">Which place, as an index into <see cref="Census"/>.</param>
		/// <param name="Stamp">The preserved part's stamp, from which the granted part is rebuilt.</param>
		/// <param name="Failure">Why not, when this answers false. Never a bare "that failed".</param>
		/// <returns>True when the founder actually changed. Never throws.</returns>
		public static bool Grant(GameObject Who, LabProcedure Procedure, int SlotIndex, string Stamp, out string Failure)
		{
			Failure = null;
			if (Who == null || Procedure == null)
			{
				Failure = "There is nobody on the table.";
				return false;
			}
			XRL.World.Parts.Body body = Who.Body;
			List<BodyPart> parts = body?.GetParts();
			if (parts == null)
			{
				Failure = KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoSlot, Procedure);
				return false;
			}
			// The census skips abstract parts, so the index the slate handed back is an index into
			// the FILTERED list and has to be walked back the same way it was built.
			BodyPart slot = null;
			int seen = 0;
			for (int i = 0; i < parts.Count; i++)
			{
				if (parts[i] == null || parts[i].Abstract)
				{
					continue;
				}
				if (seen++ == SlotIndex)
				{
					slot = parts[i];
					break;
				}
			}
			if (slot == null)
			{
				Failure = KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoSlot, Procedure);
				return false;
			}
			switch (Procedure.Source)
			{
			case LabSource.Limb:
				return GrantLimb(Who, Procedure, slot, out Failure);
			case LabSource.Mutation:
				return GrantMutation(Who, Procedure, Stamp, out Failure);
			default:
				return GrantPart(Who, Procedure, slot, Stamp, out Failure);
			}
		}

		private static bool GrantPart(GameObject Who, LabProcedure Procedure, BodyPart Slot, string Stamp, out string Failure)
		{
			Failure = null;
			GameObject bearer = Who;
			if (Procedure.Attach == LabAttach.Weapon)
			{
				// The audit's whole lesson, enforced at the commit rather than trusted at the
				// record: a part that only ever fires "WeaponHit" goes onto the thing a natural
				// attack is actually made with (Combat.cs:1186 fires it on the weapon; the weapon
				// for a natural attack is this limb's DefaultBehavior, BodyPart.cs:2874-2895).
				bearer = Slot.DefaultBehavior;
				if (!GameObject.Validate(bearer))
				{
					Failure = KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoWeapon, Procedure);
					return false;
				}
			}
			IPart built;
			if (!TryRebuild(Procedure.Grants, Stamp, out built))
			{
				Failure = "The hall could not make sense of what was kept. Nothing was done, and nothing was spent.";
				return false;
			}
			if (bearer.GetPart(Procedure.Grants) != null)
			{
				// One live instance per part type, always: duplicates double-fire events and escape
				// every GetPart-based toggle in the game (DIVERSITY §3.0b, technique 4).
				Failure = "You already carry that, and carrying it twice would only make it fire twice.";
				return false;
			}
			bearer.AddPart(built);
			// Recorded on the founder even when the part rode a claw, because the record is a record
			// of what was done to the FOUNDER and the claw is not the patient.
			Record(Who).Note(Procedure.Key, Slot.Type, (Procedure.Attach == LabAttach.Weapon));
			Who.WantToReequip();
			return true;
		}

		private static bool GrantLimb(GameObject Who, LabProcedure Procedure, BodyPart Slot, out string Failure)
		{
			Failure = null;
			List<string> wanted = KingdomProcedureRules.SlotTypes(Procedure);
			string type = (wanted.Count > 0) ? wanted[0] : Slot.Type;
			// The precedent's own call, positionally: Manager is the seventh argument and InsertAfter
			// the twenty-first (D/XRL/World/Parts/CyberneticsGraftedMirrorArm.cs:31). Named here,
			// because a bare wall of nulls is how that line became unreadable — and OrInsertBefore
			// is typed rather than omitted because two overloads differ only in it
			// (D/XRL/World/Anatomy/BodyPart.cs:3917 and :3927) and omitting it is ambiguous.
			BodyPart grown = Slot.AddPartAt(Base: type, Manager: ManagerFor(Procedure.Key),
				InsertAfter: Slot.Type, OrInsertBefore: (string)null);
			if (grown == null)
			{
				Failure = KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoSlot, Procedure);
				return false;
			}
			Record(Who).Note(Procedure.Key, type, OnWeapon: false);
			Who.WantToReequip();
			return true;
		}

		private static bool GrantMutation(GameObject Who, LabProcedure Procedure, string Stamp, out string Failure)
		{
			Failure = null;
			XRL.World.Parts.Mutations mutations = Who.RequirePart<XRL.World.Parts.Mutations>();
			if (mutations.HasMutation(Procedure.Grants))
			{
				Failure = "You already have that. The hall cannot give a body a thing it is already doing.";
				return false;
			}
			int level;
			int.TryParse(KingdomProcedureRules.StampedField(Stamp, Procedure.Grants, "Level"), out level);
			// NEVER the source's own level. The single most load-bearing balance number in the wave:
			// the mod this whole design learned from is remembered for granting mutations at the
			// source's strength, and its own author wrote down that it ruined the combat design.
			int granted = KingdomProcedureRules.GrantedMutationLevel(level);
			// Measured, never trusted: the engine answers -1 for a class it could not create
			// (D/XRL/World/Parts/Mutations.cs:444,459-462), so the state change is read back rather
			// than inferred from a return value whose name is not a contract (STANDARDS §1).
			mutations.AddMutation(Procedure.Grants, granted);
			if (!mutations.HasMutation(Procedure.Grants))
			{
				Failure = "Your body would not take it, and the hall will not force a thing that is refusing.";
				return false;
			}
			Record(Who).Note(Procedure.Key, "", OnWeapon: false);
			return true;
		}

		/// <summary>
		/// Rebuilds one part from a stamp: instantiate the type, then set its fields from strings
		/// &mdash; which is exactly what the engine does with every part in every blueprint it
		/// loads, and what the precedent's own repertoire says it is mirroring.
		/// </summary>
		private static bool TryRebuild(string ClassName, string Stamp, out IPart Part)
		{
			Part = null;
			if (string.IsNullOrEmpty(ClassName) || KingdomProcedureRules.Blocked(ClassName))
			{
				return false;
			}
			Type type = ModManager.ResolveType("XRL.World.Parts." + ClassName)
				?? ModManager.ResolveType("XRL.World.Parts.Mutation." + ClassName);
			if (type == null || !typeof(IPart).IsAssignableFrom(type))
			{
				return false;
			}
			IPart built = Activator.CreateInstance(type) as IPart;
			if (built == null)
			{
				return false;
			}
			FieldInfo[] declared = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
			for (int i = 0; i < declared.Length; i++)
			{
				string raw = KingdomProcedureRules.StampedField(Stamp, ClassName, declared[i].Name);
				if (raw == null || declared[i].IsLiteral || !IsPlain(declared[i].FieldType))
				{
					continue;
				}
				try
				{
					declared[i].SetValue(built, declared[i].FieldType.IsEnum
						? Enum.Parse(declared[i].FieldType, raw, ignoreCase: true)
						: Convert.ChangeType(raw, declared[i].FieldType, System.Globalization.CultureInfo.InvariantCulture));
				}
				catch (Exception e)
				{
					// One unreadable field costs its own field and nothing else: the part still
					// rebuilds, at that field's own default, and the log says which one went.
					KingdomLog.Log("KingdomProcedures: " + ClassName + "." + declared[i].Name + " would not read back (" + e.Message + ").");
				}
			}
			Part = built;
			return true;
		}

		/// <summary>
		/// Takes a graft off. Costs less than the graft, returns nothing, and is chronicled.
		/// <para>
		/// One call for a limb, because every limb we ever grew carries our manager
		/// (<c>Body.RemovePartsByManager</c>, <c>D/XRL/World/Parts/Body.cs:708-734</c>); an explicit
		/// walk for a part, because a part sits on the founder or on one of their claws and only the
		/// record knows which.
		/// </para>
		/// </summary>
		/// <returns>True when something actually came off.</returns>
		public static bool Remove(GameObject Who, string Key)
		{
			LabProcedure procedure;
			if (Who == null || !TryGet(Key, out procedure))
			{
				return false;
			}
			bool removed = false;
			if (procedure.Source == LabSource.Limb)
			{
				removed = Who.RemoveBodyPartsByManager(ManagerFor(procedure.Key), EvenIfDismembered: true) > 0;
			}
			else if (procedure.Source == LabSource.Mutation)
			{
				XRL.World.Parts.Mutations mutations = Who.GetPart<XRL.World.Parts.Mutations>();
				XRL.World.Parts.Mutation.BaseMutation held = mutations?.GetMutation(procedure.Grants);
				if (held != null)
				{
					mutations.RemoveMutation(held);
					removed = !mutations.HasMutation(procedure.Grants);
				}
			}
			else
			{
				removed = RemovePartFrom(Who, procedure.Grants);
				List<BodyPart> parts = Who.Body?.GetParts();
				for (int i = 0; parts != null && i < parts.Count && !removed; i++)
				{
					if (GameObject.Validate(parts[i].DefaultBehavior))
					{
						removed = RemovePartFrom(parts[i].DefaultBehavior, procedure.Grants);
					}
				}
			}
			if (removed)
			{
				Record(Who).Forget(procedure.Key);
				Who.WantToReequip();
			}
			return removed;
		}

		private static bool RemovePartFrom(GameObject Bearer, string ClassName)
		{
			return Bearer != null && Bearer.GetPart(ClassName) != null && Bearer.RemovePart(ClassName);
		}

		/// <summary>The founder's own record of what has been done to them, minted on first use.
		/// A part rather than game state, because Addendum 22 C11 rules the named procedures reset
		/// for an heir, and an heir is a different person carrying nothing of this.</summary>
		public static XRL.World.Parts.r_KingdomLabRecord Record(GameObject Who)
		{
			return Who.RequirePart<XRL.World.Parts.r_KingdomLabRecord>();
		}
	}

	/// <summary>
	/// Forgets the registry on every game load, for the reason the research registry states beside
	/// its own: the registry and the notes-filed flag are PROCESS statics, so a second game in the
	/// same session would otherwise believe its journal notes were already filed and quietly hide
	/// every named procedure from a founder who had found none of them.
	/// </summary>
	[HasCallAfterGameLoaded]
	public static class KingdomProcedureLoader
	{
		[CallAfterGameLoaded]
		public static void ForgetRegistry()
		{
			KingdomProcedures.Reload();
		}
	}
}

namespace XRL.World.Parts
{
	using System;
	using System.Collections.Generic;

	using ThousandAndFirst;

	/// <summary>
	/// What the lab has done to one founder, and what it may never do to them again.
	/// <para>
	/// <b>Named fields from version one, deliberately</b> (STANDARDS &sect;1). The precedent's own
	/// repertoire carries a hand-rolled magic header because it learned that positional reflection
	/// silently drops a part whose field layout moved between mod versions &mdash; and with it the
	/// player's entire collection. This mod's answer to the same lesson is the one the rest of the
	/// codebase already keeps: <c>WantFieldReflection</c> off and named fields on, which are
	/// self-describing, so an unknown name is skipped and a missing one keeps its default. Every
	/// schema at or below the current one is readable, and adding a field is free.
	/// </para>
	/// <para>
	/// Three parallel lists rather than a list of composites, for the reason every register in this
	/// mod is written that way: primitives round-trip through the engine's own writer without a
	/// custom composite reader, and a list that grows a fourth column costs a name and nothing else.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomLabRecord : IPart
	{
		/// <summary>Procedure keys performed, in the order they were performed.</summary>
		public List<string> Keys = new List<string>();

		/// <summary>The <c>BodyPart.Type</c> each one was performed at, index for index. Empty for a
		/// mutation, which is performed on the whole of a person.</summary>
		public List<string> Places = new List<string>();

		/// <summary>Whether each one rode a natural weapon rather than the founder themselves, index
		/// for index. What <c>KingdomProcedures.Remove</c> would otherwise have to guess.</summary>
		public List<bool> OnWeapon = new List<bool>();

		/// <summary>
		/// Named procedures this founder has had, ever, whether or not the graft is still on them.
		/// <para>
		/// Separate from <see cref="Keys"/> and it must stay separate: taking the Weeping Graft off
		/// does not un-weep it, and a founder who could have it re-done by removing it would have a
		/// once-ever procedure that was neither.
		/// </para>
		/// </summary>
		public string NamedLatch = "";

		/// <summary>Procedures this founder never wants offered again. The third answer of the
		/// three-way consent prompt, and it is permanent because that is what it promised.</summary>
		public List<string> Excluded = new List<string>();

		/// <summary>Whether the city has already spoken against the hall. Once is the whole of it.</summary>
		public bool SpokenAgainst;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		/// <summary>Records one procedure. Idempotent on the latch, so nothing anywhere has to
		/// remember whether it already asked.</summary>
		public void Note(string Key, string Place, bool OnWeapon)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			Normalize();
			Keys.Add(Key);
			Places.Add(Place ?? "");
			this.OnWeapon.Add(OnWeapon);
			LabProcedure procedure;
			if (KingdomProcedures.TryGet(Key, out procedure) && procedure.IsNamed)
			{
				NamedLatch = KingdomProcedureRules.Latch(NamedLatch, Key);
			}
		}

		/// <summary>Forgets a graft that came off. The named latch is untouched, on purpose.</summary>
		public void Forget(string Key)
		{
			Normalize();
			for (int i = Keys.Count - 1; i >= 0; i--)
			{
				if (!string.Equals(Keys[i], Key, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				Keys.RemoveAt(i);
				if (i < Places.Count)
				{
					Places.RemoveAt(i);
				}
				if (i < OnWeapon.Count)
				{
					OnWeapon.RemoveAt(i);
				}
				return;
			}
		}

		/// <summary>Whether a named procedure has already been performed on this founder, ever.</summary>
		public bool AlreadyHad(string Key)
		{
			return KingdomProcedureRules.Latched(NamedLatch, Key);
		}

		/// <summary>Whether the founder has asked never to be offered this again.</summary>
		public bool Refuses(string Key)
		{
			Normalize();
			for (int i = 0; i < Excluded.Count; i++)
			{
				if (string.Equals(Excluded[i], Key, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Never offer this again. Permanent, because that is what the third answer
		/// promised.</summary>
		public void Exclude(string Key)
		{
			Normalize();
			if (!string.IsNullOrEmpty(Key) && !Refuses(Key))
			{
				Excluded.Add(Key.Trim().ToLowerInvariant());
			}
		}

		/// <summary>What is grafted at one place, or null. What the slate's rows are drawn from.</summary>
		public string GraftedAt(string Place)
		{
			Normalize();
			for (int i = 0; i < Keys.Count && i < Places.Count; i++)
			{
				if (string.Equals(Places[i], Place, StringComparison.OrdinalIgnoreCase))
				{
					return Keys[i];
				}
			}
			return null;
		}

		/// <summary>Repairs a record read from a save written by an older build: null containers
		/// become empty ones, and lists that fell out of step are trimmed to their shortest, because
		/// a record that says a graft is at a place it cannot name is worse than one that says
		/// nothing.</summary>
		public void Normalize()
		{
			if (Keys == null)
			{
				Keys = new List<string>();
			}
			if (Places == null)
			{
				Places = new List<string>();
			}
			if (OnWeapon == null)
			{
				OnWeapon = new List<bool>();
			}
			if (Excluded == null)
			{
				Excluded = new List<string>();
			}
			if (NamedLatch == null)
			{
				NamedLatch = "";
			}
			while (Places.Count < Keys.Count)
			{
				Places.Add("");
			}
			while (OnWeapon.Count < Keys.Count)
			{
				OnWeapon.Add(false);
			}
		}

#if !TAF_TESTS
		/// <summary>
		/// Named fields, replacing the positional path outright.
		/// <para>
		/// <c>IComponent&lt;T&gt;.Write</c> reflects over fields IN DECLARATION ORDER by default
		/// (<c>D/XRL/World/IComponent.cs:4396-4425</c>), which is the trap the precedent's own
		/// repertoire wrote its warning about: a field-layout change between mod versions silently
		/// drops the part, and with it everything the founder had done to them. Named fields are
		/// self-describing &mdash; an unknown name is skipped, a missing one keeps its default
		/// &mdash; so every schema at or below this one reads, and adding a field costs a name.
		/// (<c>IPart</c> has no <c>WantFieldReflection</c> knob; only <c>IComposite</c> does. For a
		/// part, overriding these two IS the opt-out.)
		/// </para>
		/// </summary>
		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomLabRecord));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomLabRecord));
			Normalize();
		}
#endif
	}
}
