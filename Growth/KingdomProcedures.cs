using System;
using System.Collections.Generic;
using System.Reflection;

using Qud.API;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

namespace ThousandAndFirst
{
	internal readonly struct KingdomLabOwnershipSnapshot
	{
		public readonly string ProcedureKey;
		public readonly string JobId;
		public readonly string PatientId;
		public readonly int BodyPartId;
		public readonly string BearerId;
		public readonly string Grants;
		public readonly int Source;
		public readonly int Attach;
		public readonly string Manager;
		public readonly string Detail;
		public readonly string Fingerprint;
		public readonly int PartOrdinal;
		public readonly string EffectNonce;

		public KingdomLabOwnershipSnapshot(string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, string BearerId)
		{
			this.ProcedureKey = ProcedureKey ?? "";
			this.JobId = JobId ?? "";
			this.PatientId = PatientId ?? "";
			this.BodyPartId = BodyPartId;
			this.BearerId = BearerId ?? "";
			Grants = "";
			Source = -1;
			Attach = -1;
			Manager = "";
			Detail = "";
			Fingerprint = "";
			PartOrdinal = -1;
			EffectNonce = "";
		}

		public KingdomLabOwnershipSnapshot(string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, string BearerId, string Grants, int Source, int Attach,
			string Manager, string Detail, string Fingerprint, int PartOrdinal,
			string EffectNonce = "")
		{
			this.ProcedureKey = ProcedureKey ?? "";
			this.JobId = JobId ?? "";
			this.PatientId = PatientId ?? "";
			this.BodyPartId = BodyPartId;
			this.BearerId = BearerId ?? "";
			this.Grants = Grants ?? "";
			this.Source = Source;
			this.Attach = Attach;
			this.Manager = Manager ?? "";
			this.Detail = Detail ?? "";
			this.Fingerprint = Fingerprint ?? "";
			this.PartOrdinal = PartOrdinal;
			this.EffectNonce = EffectNonce ?? "";
		}
	}

	internal sealed class KingdomLabGrantAttempt
	{
		public KingdomLabOwnedTargetState State = KingdomLabOwnedTargetState.Uncertain;
		public IPart ExactPart;
		public BodyPart ExactBodyPart;
		public int BodyPartId;
		public int PartOrdinal = -1;
		public string BearerId = "";
		public string Failure = "";
	}

	internal sealed class KingdomLabOwnedTarget
	{
		public GameObject Bearer;
		public XRL.World.Parts.r_KingdomLabEffectLedger Ledger;
		public IPart ExactPart;
		public BodyPart ExactBodyPart;
	}

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

		/// <summary>Per-procedure ownership marker. Removal requires this marker and an exact record
		/// identity, so a native same-class effect is never selected by a class scan.</summary>
		public static string OwnerProperty(string Key)
		{
			return "r_TAF_LabOwner::" + (Key ?? "").Trim().ToLowerInvariant();
		}

		public static string OwnerNonceProperty(string Key)
		{
			return "r_TAF_LabOwnerNonce::" + (Key ?? "").Trim().ToLowerInvariant();
		}

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
						KingdomXmlSchema.HandleRoot(xml, handlers, "KingdomProcedures");
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

		/// <summary>Live and detached anatomy are one identity domain.</summary>
		internal static List<BodyPart> AllBodyParts(GameObject Who)
		{
			List<BodyPart> result = new List<BodyPart>();
			List<BodyPart> live = Who?.Body?.GetParts();
			for (int i = 0; live != null && i < live.Count; i++)
			{
				if (live[i] != null && !ContainsReference(result, live[i])) result.Add(live[i]);
			}
			List<XRL.World.Parts.Body.DismemberedPart> detached = Who?.Body?.DismemberedParts;
			for (int i = 0; detached != null && i < detached.Count; i++)
			{
				BodyPart part = detached[i]?.Part;
				if (part != null && !ContainsReference(result, part)) result.Add(part);
			}
			return result;
		}

		internal static BodyPart ExactBodyPart(GameObject Who, int BodyPartId)
		{
			return (BodyPartId > 0) ? Who?.Body?.GetPartByID(BodyPartId, EvenIfDismembered: true) : null;
		}

		/// <summary>Exact identity in the live body tree. Detached anatomy is deliberately excluded.</summary>
		internal static BodyPart ExactLiveBodyPart(GameObject Who, int BodyPartId)
		{
			if (BodyPartId <= 0 || Who?.Body == null) return null;
			BodyPart candidate = Who.Body.GetPartByID(BodyPartId, EvenIfDismembered: false);
			return BodyOwnsLivePart(Who, candidate) ? candidate : null;
		}

		internal static bool BodyOwnsLivePart(GameObject Who, BodyPart Candidate)
		{
			return Who?.Body != null && Candidate != null
				&& ReferenceEquals(Candidate.ParentBody, Who.Body)
				&& ContainsReference(Who.Body.GetParts(), Candidate);
		}

		internal static bool BodyOwnsPart(GameObject Who, BodyPart Candidate)
		{
			if (Who?.Body == null || Candidate == null || !ReferenceEquals(Candidate.ParentBody, Who.Body))
			{
				return false;
			}
			return ContainsReference(AllBodyParts(Who), Candidate);
		}

		private static bool ContainsReference(IList<BodyPart> Parts, BodyPart Candidate)
		{
			for (int i = 0; Parts != null && i < Parts.Count; i++)
			{
				if (ReferenceEquals(Parts[i], Candidate)) return true;
			}
			return false;
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
			return GrantAt(Who, Procedure, slot.ID,
				(Procedure.Attach == LabAttach.Weapon && GameObject.Validate(slot.DefaultBehavior))
					? slot.DefaultBehavior.ID : Who.ID,
				Stamp, Guid.NewGuid().ToString("N"), out Failure);
		}

		/// <summary>Terminal grant against the exact slot and bearer selected at commission.</summary>
		public static bool GrantAt(GameObject Who, LabProcedure Procedure, int BodyPartId,
			string BearerId, string Stamp, string JobId, out string Failure)
		{
			string detail = ExecutionDetail(Procedure, Stamp);
			string manager = ManagerFor(Procedure?.Key);
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, Procedure?.Key, Procedure?.Grants,
				(int)(Procedure?.Source ?? LabSource.Part),
				(int)(Procedure?.Attach ?? LabAttach.Body), manager, detail);
			KingdomLabGrantAttempt attempt = GrantAtExact(Who, Procedure, BodyPartId, BearerId,
				Stamp, JobId, manager, detail, fingerprint);
			Failure = attempt.Failure;
			return attempt.State == KingdomLabOwnedTargetState.Present;
		}

		internal static KingdomLabGrantAttempt GrantAtExact(GameObject Who, LabProcedure Procedure,
			int BodyPartId, string BearerId, string Stamp, string JobId, string Manager,
			string Detail, string Fingerprint)
		{
			KingdomLabGrantAttempt attempt = new KingdomLabGrantAttempt { BearerId = BearerId ?? "" };
			if (Who == null || Procedure == null || Who.Body == null)
			{
				attempt.Failure = "There is nobody on the table.";
				return attempt;
			}
			if (!KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
				Procedure.Key, Procedure.Grants, (int)Procedure.Source, (int)Procedure.Attach,
				Manager, Fingerprint, Detail))
			{
				attempt.Failure = "The paid job's immutable effect contract is not valid.";
				return attempt;
			}
			BodyPart slot = ExactLiveBodyPart(Who, BodyPartId);
			if (slot == null || slot.Abstract || !BodyOwnsLivePart(Who, slot))
			{
				attempt.Failure = KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoSlot, Procedure);
				return attempt;
			}
			GameObject expected = (Procedure.Attach == LabAttach.Weapon) ? slot.DefaultBehavior : Who;
			if (!GameObject.Validate(expected) || !string.Equals(expected.ID, BearerId,
				StringComparison.Ordinal) || (Procedure.Attach == LabAttach.Weapon
					&& !ReferenceEquals(slot.DefaultBehavior, expected)))
			{
				attempt.Failure = "The selected body part no longer bears the exact thing the paid contract recorded.";
				return attempt;
			}
			if (HasProcedureClass(Who, Procedure))
			{
				attempt.Failure = "That procedure already exists on live or detached anatomy. The hall will not create a second instance.";
				return attempt;
			}
			switch (Procedure.Source)
			{
			case LabSource.Limb:
				return GrantLimb(Who, Procedure, slot, JobId, Manager, Detail, Fingerprint);
			case LabSource.Mutation:
				return GrantMutation(Who, Procedure, slot, Stamp, JobId, Manager, Detail,
					Fingerprint);
			default:
				return GrantPart(Who, Procedure, slot, expected, Stamp, JobId, Manager, Detail,
					Fingerprint);
			}
		}

		private static KingdomLabGrantAttempt GrantPart(GameObject Who, LabProcedure Procedure,
			BodyPart Slot, GameObject Bearer, string Stamp, string JobId, string Manager,
			string Detail, string Fingerprint)
		{
			KingdomLabGrantAttempt attempt = new KingdomLabGrantAttempt { BearerId = Bearer.ID };
			IPart built;
			if (!TryRebuild(Procedure.Grants, Stamp, out built))
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = "The hall could not make sense of what was kept. No body effect was made.";
				return attempt;
			}
			if (Bearer.GetPart(Procedure.Grants) != null)
			{
				attempt.Failure = "You already carry that, and carrying it twice would only make it fire twice.";
				return attempt;
			}
			XRL.World.Parts.r_KingdomLabEffectLedger ledger;
			if (!PrepareOwnershipIntent(Bearer, Who, Procedure, Slot.ID, JobId, Manager, Detail,
				Fingerprint, built, Bearer.PartsList?.Count ?? 0, out ledger, out attempt.Failure))
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				return attempt;
			}
			Exception error = null;
			try
			{
				Bearer.AddPart(built);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			int ordinal = ReferencePartOrdinal(Bearer, built);
			if (ordinal >= 0 && ReferenceEquals(built.ParentObject, Bearer)
				&& CountPartClass(Bearer, Procedure.Grants) == 1)
			{
				PublishOwnership(Who, Bearer, Procedure, Slot.Type, Slot.ID, JobId, Manager,
					Detail, Fingerprint, built, ordinal, ledger, attempt);
				if (error != null) attempt.Failure = "The engine callback threw after the exact effect was attached; ownership was recovered: " + error.Message;
				return attempt;
			}
			bool absent = TryRollbackExactPart(Bearer, built);
			if (absent)
			{
				ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
				ClearOwnerIfExact(Bearer, Procedure.Key, JobId);
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = (error == null) ? "The exact effect did not attach."
					: "The attachment callback threw; the exact attempted part was rolled back.";
			}
			else
			{
				ledger.Quarantine(Procedure.Key, JobId);
				attempt.Failure = "The exact attempted part changed topology during attachment. Its intent is quarantined; no same-class part will be adopted.";
			}
			return attempt;
		}

		private static KingdomLabGrantAttempt GrantLimb(GameObject Who, LabProcedure Procedure,
			BodyPart Slot, string JobId, string Manager, string Detail, string Fingerprint)
		{
			KingdomLabGrantAttempt attempt = new KingdomLabGrantAttempt { BearerId = Who.ID };
			string type = string.IsNullOrEmpty(Detail) ? Slot.Type : Detail;
			BodyPart grown = new BodyPart(type, 0, Slot.ParentBody, Manager: Manager);
			int grownId = grown.ID;
			XRL.World.Parts.r_KingdomLabEffectLedger ledger;
			if (!PrepareOwnershipIntent(Who, Who, Procedure, grownId, JobId, Manager, Detail,
				Fingerprint, null, -1, out ledger, out attempt.Failure))
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				return attempt;
			}
			Exception error = null;
			try
			{
				Slot.AddPart(grown, Slot.Type, DoUpdate: false);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			if (BodyOwnsLivePart(Who, grown)
				&& ReferenceEquals(ExactLiveBodyPart(Who, grownId), grown))
			{
				PublishOwnership(Who, Who, Procedure, type, grownId, JobId, Manager, Detail,
					Fingerprint, null, -1, ledger, attempt);
				attempt.ExactBodyPart = grown;
				attempt.BodyPartId = grownId;
				try
				{
					Who.Body.UpdateBodyParts();
					Who.Body.RecalculateTypeArmor(type);
					Who.WantToReequip();
				}
				catch (Exception ex)
				{
					error = error ?? ex;
				}
				if (error != null) attempt.Failure = "The body update callback threw after the exact limb and ownership receipt were durable: " + error.Message;
				return attempt;
			}
			bool absent = TryRollbackExactBodyPart(Who, grown);
			if (absent)
			{
				ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
				ClearOwnerIfExact(Who, Procedure.Key, JobId);
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = (error == null) ? "The exact limb did not enter the patient's body."
					: "The limb insertion threw; the exact partial limb was rolled back.";
			}
			else
			{
				ledger.Quarantine(Procedure.Key, JobId);
				attempt.State = KingdomLabOwnedTargetState.Uncertain;
				attempt.Failure = "The limb insertion left uncertain exact topology. Its prepublished intent is quarantined; no same-type limb will be adopted.";
			}
			return attempt;
		}

		private static bool TryRollbackExactBodyPart(GameObject Who, BodyPart Part)
		{
			if (!BodyOwnsPart(Who, Part) && Part?.ParentPart == null) return true;
			try { Who?.Body?.RemovePart(Part); }
			catch { }
			return !BodyOwnsPart(Who, Part) && Part?.ParentPart == null;
		}

		private static KingdomLabGrantAttempt GrantMutation(GameObject Who, LabProcedure Procedure,
			BodyPart Slot, string Stamp, string JobId, string Manager, string Detail,
			string Fingerprint)
		{
			KingdomLabGrantAttempt attempt = new KingdomLabGrantAttempt { BearerId = Who.ID };
			XRL.World.Parts.Mutations mutations = Who.RequirePart<XRL.World.Parts.Mutations>();
			if (Who.GetPart(Procedure.Grants) is XRL.World.Parts.Mutation.BaseMutation)
			{
				attempt.Failure = "You already have that, whether native or modifier-backed. The hall will not replace it.";
				return attempt;
			}
			int level;
			int.TryParse(KingdomProcedureRules.StampedField(Stamp, Procedure.Grants, "Level"), out level);
			// NEVER the source's own level. The single most load-bearing balance number in the wave:
			// the mod this whole design learned from is remembered for granting mutations at the
			// source's strength, and its own author wrote down that it ruined the combat design.
			int granted = KingdomProcedureRules.GrantedMutationLevel(level);
			XRL.World.Parts.Mutation.BaseMutation exact =
				XRL.World.Parts.Mutation.BaseMutation.Create(Procedure.Grants);
			if (exact == null)
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = "The frozen mutation class could not be constructed.";
				return attempt;
			}
			XRL.World.Parts.r_KingdomLabEffectLedger ledger;
			if (!PrepareOwnershipIntent(Who, Who, Procedure, Slot.ID, JobId, Manager, Detail,
				Fingerprint, exact, Who.PartsList?.Count ?? 0, out ledger, out attempt.Failure))
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				return attempt;
			}
			Exception error = null;
			try
			{
				mutations.AddMutation(exact, granted);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			int ordinal = ReferencePartOrdinal(Who, exact);
			bool listed = MutationListed(mutations, exact);
			if (ordinal >= 0 && ReferenceEquals(exact.ParentObject, Who) && listed)
			{
				PublishOwnership(Who, Who, Procedure, "", Slot.ID, JobId, Manager, Detail,
					Fingerprint, exact, ordinal, ledger, attempt);
				if (error != null) attempt.Failure = "The mutation callback threw after the exact listed mutation and ownership receipt were durable: " + error.Message;
				return attempt;
			}
			bool absent = !listed && TryRollbackExactPart(Who, exact);
			if (absent)
			{
				ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
				ClearOwnerIfExact(Who, Procedure.Key, JobId);
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = "Mutation publication stopped before MutationList accepted the exact instance; the partial part was rolled back.";
			}
			else
			{
				ledger.Quarantine(Procedure.Key, JobId);
				attempt.Failure = "Mutation publication left an uncertain exact instance. It is quarantined; no class replacement will be adopted.";
			}
			return attempt;
		}

		internal static string ContractDetail(LabProcedure Procedure)
		{
			if (Procedure?.Source != LabSource.Limb) return "";
			List<string> wanted = KingdomProcedureRules.SlotTypes(Procedure);
			return (wanted.Count > 0) ? wanted[0] : "";
		}

		internal static string ExecutionDetail(LabProcedure Procedure, string Stamp)
		{
			string catalog = ContractDetail(Procedure);
			if (Procedure?.Source == LabSource.Limb) return catalog;
			return "stamp:" + KingdomLabRules.ExecutionStampFingerprint(Stamp);
		}

		internal static bool CatalogMatchesExecutionDetail(LabProcedure Procedure, string Detail)
		{
			if (Procedure == null || Detail == null) return false;
			return Procedure.Source == LabSource.Limb
				? string.Equals(Detail, ContractDetail(Procedure), StringComparison.Ordinal)
				: Detail.StartsWith("stamp:", StringComparison.Ordinal)
					&& Detail.Length == "stamp:".Length + 16;
		}

		private static bool PrepareOwnershipIntent(GameObject Bearer, GameObject Who,
			LabProcedure Procedure, int BodyPartId, string JobId, string Manager, string Detail,
			string Fingerprint, IPart RuntimePart, int PartOrdinal,
			out XRL.World.Parts.r_KingdomLabEffectLedger Ledger, out string Failure)
		{
			Ledger = null;
			Failure = null;
			try
			{
				Ledger = Bearer.RequirePart<XRL.World.Parts.r_KingdomLabEffectLedger>();
				if (Ledger == null || CountPartClass(Bearer, nameof(XRL.World.Parts.r_KingdomLabEffectLedger)) != 1)
				{
					Failure = "The bearer has an ambiguous ownership ledger.";
					return false;
				}
				int prior = Ledger.IndexOf(Procedure.Key, JobId);
				Ledger.TrackIntent(Procedure.Key, JobId, Who.ID, BodyPartId,
					(int)Procedure.Source, (int)Procedure.Attach, Procedure.Grants, Manager,
					Detail, Fingerprint, PartOrdinal, RuntimePart);
				int ledgerAt = Ledger.IndexOf(Procedure.Key, JobId);
				string nonce = Ledger.NonceAt(ledgerAt);
				string priorOwner = Bearer.GetStringProperty(OwnerProperty(Procedure.Key));
				string priorNonce = Bearer.GetStringProperty(OwnerNonceProperty(Procedure.Key));
				if ((!string.IsNullOrEmpty(priorOwner)
						&& !string.Equals(priorOwner, JobId, StringComparison.Ordinal))
					|| (!string.IsNullOrEmpty(priorNonce)
						&& !string.Equals(priorNonce, nonce, StringComparison.Ordinal)))
				{
					Failure = "A foreign ownership marker already occupies this procedure key.";
					if (prior < 0) Ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
					return false;
				}
				Bearer.SetStringProperty(OwnerProperty(Procedure.Key), JobId ?? "");
				Bearer.SetStringProperty(OwnerNonceProperty(Procedure.Key), nonce);
				if (Ledger.IndexOf(Procedure.Key, JobId) < 0
					|| !string.Equals(Bearer.GetStringProperty(OwnerProperty(Procedure.Key)),
						JobId, StringComparison.Ordinal)
					|| !string.Equals(Bearer.GetStringProperty(
						OwnerNonceProperty(Procedure.Key)),
						Ledger.NonceAt(Ledger.IndexOf(Procedure.Key, JobId)), StringComparison.Ordinal))
				{
					Failure = "The exact ownership intent could not be published before body mutation.";
					Ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
					ClearOwnerIfExact(Bearer, Procedure.Key, JobId);
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "Ownership intent publication threw before body mutation: " + ex.Message;
				try { Ledger?.Forget(Procedure.Key, JobId, CleanupPatient: false); } catch { }
				ClearOwnerIfExact(Bearer, Procedure.Key, JobId);
				return false;
			}
		}

		private static void PublishOwnership(GameObject Who, GameObject Bearer,
			LabProcedure Procedure, string Place, int BodyPartId, string JobId, string Manager,
			string Detail, string Fingerprint, IPart RuntimePart, int PartOrdinal,
			XRL.World.Parts.r_KingdomLabEffectLedger Ledger, KingdomLabGrantAttempt Attempt)
		{
			Attempt.State = KingdomLabOwnedTargetState.Present;
			Attempt.ExactPart = RuntimePart;
			Attempt.BodyPartId = BodyPartId;
			Attempt.PartOrdinal = PartOrdinal;
			Attempt.BearerId = Bearer.ID;
			try
			{
				Ledger.CommitBinding(Procedure.Key, JobId, PartOrdinal, RuntimePart);
				Bearer.SetStringProperty(OwnerProperty(Procedure.Key), JobId ?? "");
				int ledgerAt = Ledger.IndexOf(Procedure.Key, JobId);
				Bearer.SetStringProperty(OwnerNonceProperty(Procedure.Key), Ledger.NonceAt(ledgerAt));
				Record(Who).Note(Procedure.Key, Place,
					Procedure.Attach == LabAttach.Weapon, BodyPartId, Bearer.ID, JobId,
					Procedure.Named, Procedure.Grants, (int)Procedure.Source,
					(int)Procedure.Attach, Manager, Detail, Fingerprint, PartOrdinal,
					Ledger.NonceAt(ledgerAt));
			}
			catch (Exception ex)
			{
				Attempt.Failure = "The exact effect is present; post-effect ownership publication needs repair: " + ex.Message;
			}
		}

		private static void ClearOwnerIfExact(GameObject Bearer, string Key, string JobId)
		{
			try
			{
				if (GameObject.Validate(Bearer) && string.Equals(Bearer.GetStringProperty(
					OwnerProperty(Key)), JobId, StringComparison.Ordinal))
				{
					Bearer.RemoveStringProperty(OwnerProperty(Key));
					Bearer.RemoveStringProperty(OwnerNonceProperty(Key));
				}
			}
			catch { }
		}

		internal static int ReferencePartOrdinal(GameObject Bearer, IPart Part)
		{
			for (int i = 0; Bearer?.PartsList != null && i < Bearer.PartsList.Count; i++)
			{
				if (ReferenceEquals(Bearer.PartsList[i], Part)) return i;
			}
			return -1;
		}

		private static int CountPartClass(GameObject Bearer, string ClassName)
		{
			int count = 0;
			for (int i = 0; Bearer?.PartsList != null && i < Bearer.PartsList.Count; i++)
			{
				if (string.Equals(Bearer.PartsList[i]?.Name, ClassName,
					StringComparison.Ordinal)) count++;
			}
			return count;
		}

		private static bool TryRollbackExactPart(GameObject Bearer, IPart Part)
		{
			if (ReferencePartOrdinal(Bearer, Part) < 0
				&& (Part?.ParentObject == null || ReferenceEquals(Part.ParentObject, Bearer)))
			{
				return true;
			}
			try
			{
				Bearer.RemovePart(Part);
			}
			catch { }
			return ReferencePartOrdinal(Bearer, Part) < 0
				&& (Part?.ParentObject == null || ReferenceEquals(Part.ParentObject, Bearer));
		}

		internal static bool MutationListed(XRL.World.Parts.Mutations Mutations,
			XRL.World.Parts.Mutation.BaseMutation Mutation)
		{
			for (int i = 0; Mutations?.MutationList != null && i < Mutations.MutationList.Count; i++)
			{
				if (ReferenceEquals(Mutations.MutationList[i], Mutation)) return true;
			}
			return false;
		}

		/// <summary>Actual global class presence across founder and every natural-weapon bearer.</summary>
		public static bool HasProcedureClass(GameObject Who, LabProcedure Procedure)
		{
			if (Who == null || Procedure == null)
			{
				return false;
			}
			if (Procedure.Source == LabSource.Mutation)
			{
				// Modifier-backed mutations live as BaseMutation parts but are deliberately absent
				// from Mutations.MutationList. AddMutation removes such a part before adding its own;
				// checking the live part is therefore the non-destructive global collision test.
				return Who.GetPart(Procedure.Grants) is XRL.World.Parts.Mutation.BaseMutation;
			}
			if (Procedure.Source == LabSource.Limb)
			{
				List<BodyPart> held = AllBodyParts(Who);
				for (int i = 0; held != null && i < held.Count; i++)
				{
					if (string.Equals(held[i]?.Manager, ManagerFor(Procedure.Key), StringComparison.Ordinal))
					{
						return true;
					}
				}
				return false;
			}
			if (Who.GetPart(Procedure.Grants) != null)
			{
				return true;
			}
			List<BodyPart> parts = AllBodyParts(Who);
			List<GameObject> seen = new List<GameObject>();
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				GameObject bearer = parts[i]?.DefaultBehavior;
				if (GameObject.Validate(bearer) && !seen.Contains(bearer))
				{
					seen.Add(bearer);
					if (bearer.GetPart(Procedure.Grants) != null)
					{
						return true;
					}
				}
			}
			return false;
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

		/// <summary>Freezes the exact ownership identity before a removal receipt can spend water.
		/// A pre-ledger record is deliberately not upgraded by guessing.</summary>
		internal static KingdomLabOwnedTargetState SnapshotOwned(GameObject Who, string Key,
			out KingdomLabOwnershipSnapshot Snapshot)
		{
			Snapshot = default(KingdomLabOwnershipSnapshot);
			if (Who == null || string.IsNullOrEmpty(Key))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			XRL.World.Parts.r_KingdomLabRecord record = Record(Who);
			record.Normalize();
			int at = record.IndexOf(Key);
			if (at < 0)
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (!record.ContractAt(at, out Snapshot, Who.ID))
			{
				// Legacy type/manager/ordinal rows remain visible to the slate, but are
				// read-only quarantine. They cannot mint mutation authority by inference.
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (Snapshot.Source == (int)LabSource.Limb && !EnsureLimbLedger(Who, Snapshot))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			KingdomLabOwnedTarget target;
			return ClassifyOwned(Who, Snapshot, out target);
		}

		private static bool TryMigrateLegacyLimb(GameObject Who, LabProcedure Procedure,
			XRL.World.Parts.r_KingdomLabRecord Record, int At,
			out KingdomLabOwnershipSnapshot Snapshot)
		{
			Snapshot = default(KingdomLabOwnershipSnapshot);
			if (Who == null || Procedure?.Source != LabSource.Limb || Record.RegistryQuarantined
				|| At < 0 || At >= Record.Keys.Count || !string.IsNullOrEmpty(Record.Fingerprints[At])
				|| At >= Record.EffectNonces.Count || Record.EffectNonces[At].Length != 32)
			{
				return false;
			}
			string manager = ManagerFor(Procedure.Key);
			BodyPart exact = null;
			List<BodyPart> all = AllBodyParts(Who);
			for (int i = 0; i < all.Count; i++)
			{
				if (!string.Equals(all[i]?.Manager, manager, StringComparison.Ordinal)) continue;
				if (exact != null) return false;
				exact = all[i];
			}
			if (exact == null || !BodyOwnsPart(Who, exact)
				|| (Record.BodyPartIds[At] > 0 && Record.BodyPartIds[At] != exact.ID)) return false;
			string detail = ContractDetail(Procedure);
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, Procedure.Key, Procedure.Grants,
				(int)Procedure.Source, (int)Procedure.Attach, manager, detail);
			string job = string.IsNullOrEmpty(Record.JobIds[At])
				? Guid.NewGuid().ToString("N") : Record.JobIds[At];
			if (!Record.UpgradeLegacyLimbAt(At, exact.ID, Who.ID, job, Procedure.Named,
				Procedure.Grants, (int)Procedure.Attach, manager, detail, fingerprint)) return false;
			if (!Record.ContractAt(At, out Snapshot, Who.ID)) return false;
			return EnsureLimbLedger(Who, Snapshot);
		}

		private static bool EnsureLimbLedger(GameObject Who, KingdomLabOwnershipSnapshot Snapshot)
		{
			if (Who == null || Snapshot.Source != (int)LabSource.Limb
				|| !string.Equals(Who.ID, Snapshot.PatientId, StringComparison.Ordinal)
				|| !string.Equals(Who.ID, Snapshot.BearerId, StringComparison.Ordinal)) return false;
			BodyPart limb = ExactBodyPart(Who, Snapshot.BodyPartId);
			if (limb == null || !BodyOwnsPart(Who, limb)
				|| !string.Equals(limb.Manager, Snapshot.Manager, StringComparison.Ordinal)) return false;
			try
			{
				XRL.World.Parts.r_KingdomLabEffectLedger ledger =
					Who.RequirePart<XRL.World.Parts.r_KingdomLabEffectLedger>();
				int at = ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId);
				if (at < 0)
				{
					ledger.TrackIntent(Snapshot.ProcedureKey, Snapshot.JobId, Who.ID,
						Snapshot.BodyPartId, Snapshot.Source, Snapshot.Attach, Snapshot.Grants,
						Snapshot.Manager, Snapshot.Detail, Snapshot.Fingerprint, -1, null,
						Snapshot.EffectNonce);
				}
				else if (!ledger.EntryMatches(at, Snapshot.ProcedureKey, Snapshot.JobId, Who.ID,
					Snapshot.BodyPartId, Snapshot.Source, Snapshot.Attach, Snapshot.Grants,
					Snapshot.Manager, Snapshot.Detail, Snapshot.Fingerprint, -1))
				{
					if (!ledger.UpgradeLegacyLimb(Snapshot.ProcedureKey, Snapshot.JobId, Who.ID,
						Snapshot.BodyPartId, Snapshot.Attach, Snapshot.Grants, Snapshot.Manager,
						Snapshot.Detail, Snapshot.Fingerprint)) return false;
				}
				string marker = Who.GetStringProperty(OwnerProperty(Snapshot.ProcedureKey));
				if (!string.IsNullOrEmpty(marker) && !string.Equals(marker, Snapshot.JobId,
					StringComparison.Ordinal)) return false;
				Who.SetStringProperty(OwnerProperty(Snapshot.ProcedureKey), Snapshot.JobId);
				Who.SetStringProperty(OwnerNonceProperty(Snapshot.ProcedureKey),
					Snapshot.EffectNonce);
				ledger.CommitBinding(Snapshot.ProcedureKey, Snapshot.JobId, -1, null);
				return true;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: exact legacy limb migration stopped (" + ex.Message + ")");
				return false;
			}
		}

		/// <summary>Finds a current commission in the bearer ledger without inventing a legacy
		/// identity from a same-class effect.</summary>
		internal static KingdomLabOwnedTargetState SnapshotTracked(GameObject Who,
			LabProcedure Procedure, string JobId, string BearerId,
			out KingdomLabOwnershipSnapshot Snapshot)
		{
			Snapshot = default(KingdomLabOwnershipSnapshot);
			if (Who == null || Procedure == null || string.IsNullOrEmpty(JobId)
				|| string.IsNullOrEmpty(BearerId))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			GameObject bearer = string.Equals(BearerId, Who.ID, StringComparison.Ordinal)
				? Who : GameObject.FindByID(BearerId);
			XRL.World.Parts.r_KingdomLabEffectLedger ledger =
				bearer?.GetPart<XRL.World.Parts.r_KingdomLabEffectLedger>();
			int at = ledger?.IndexOf(Procedure.Key, JobId) ?? -1;
			if (at < 0 || !string.Equals(ledger.PatientIds[at], Who.ID,
				StringComparison.Ordinal) || ledger.LedgerQuarantined
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					ledger.ProcedureKeys[at], ledger.ClassNames[at], ledger.Sources[at],
					ledger.Attaches[at], ledger.Managers[at], ledger.Fingerprints[at],
					ledger.Details[at]))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			Snapshot = new KingdomLabOwnershipSnapshot(Procedure.Key, JobId, Who.ID,
				ledger.BodyPartIds[at], BearerId, ledger.ClassNames[at], ledger.Sources[at],
				ledger.Attaches[at], ledger.Managers[at], ledger.Details[at],
				ledger.Fingerprints[at], ledger.PartOrdinals[at], ledger.NonceAt(at));
			KingdomLabOwnedTarget target;
			return ClassifyOwned(Who, Snapshot, out target);
		}

		/// <summary>Reads one tracked target. Missing physical state proves absence; a same-class
		/// replacement without the original tracker is foreign and therefore uncertain.</summary>
		internal static KingdomLabOwnedTargetState ClassifyOwned(GameObject Who,
			LabProcedure Procedure, KingdomLabOwnershipSnapshot Snapshot,
			out KingdomLabOwnedTarget Target)
		{
			if (Procedure == null || !string.Equals(Procedure.Key, Snapshot.ProcedureKey,
				StringComparison.OrdinalIgnoreCase))
			{
				Target = null;
				return KingdomLabOwnedTargetState.Uncertain;
			}
			return ClassifyOwned(Who, Snapshot, out Target);
		}

		internal static KingdomLabOwnedTargetState ClassifyOwned(GameObject Who,
			KingdomLabOwnershipSnapshot Snapshot, out KingdomLabOwnedTarget Target)
		{
			Target = null;
			if (Who == null
				|| !string.Equals(Who.ID, Snapshot.PatientId, StringComparison.Ordinal)
				|| string.IsNullOrEmpty(Snapshot.JobId) || Snapshot.BodyPartId <= 0
				|| string.IsNullOrEmpty(Snapshot.BearerId) || Snapshot.EffectNonce.Length != 32
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					Snapshot.ProcedureKey, Snapshot.Grants, Snapshot.Source, Snapshot.Attach,
					Snapshot.Manager, Snapshot.Fingerprint, Snapshot.Detail))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			GameObject bearer;
			if (!ResolveExactBearer(Who, Snapshot, out bearer))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			XRL.World.Parts.r_KingdomLabEffectLedger ledger =
				bearer.GetPart<XRL.World.Parts.r_KingdomLabEffectLedger>();
			int entry = ledger?.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) ?? -1;
			if (entry < 0)
			{
				return UntrackedPhysicalState(Who, bearer, Snapshot);
			}
			if (ledger.LedgerQuarantined || ledger.BindingStates[entry] == 2
				|| !string.Equals(ledger.NonceAt(entry), Snapshot.EffectNonce,
					StringComparison.Ordinal)
				|| !string.Equals(bearer.GetStringProperty(
					OwnerNonceProperty(Snapshot.ProcedureKey)), Snapshot.EffectNonce,
					StringComparison.Ordinal)
				|| !ledger.EntryMatches(entry, Snapshot.ProcedureKey, Snapshot.JobId, Who.ID,
					Snapshot.BodyPartId, Snapshot.Source, Snapshot.Attach, Snapshot.Grants,
					Snapshot.Manager, Snapshot.Detail, Snapshot.Fingerprint,
					Snapshot.PartOrdinal))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			Target = new KingdomLabOwnedTarget { Bearer = bearer, Ledger = ledger };
			if (ledger.BindingStates[entry] == 4)
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (Snapshot.Source == (int)LabSource.Limb)
			{
				BodyPart limb = ExactBodyPart(Who, Snapshot.BodyPartId);
				if (ledger.BindingStates[entry] == 3)
				{
					if (limb == null) return KingdomLabOwnedTargetState.Absent;
					if (!BodyOwnsPart(Who, limb) || !string.Equals(limb.Manager,
						Snapshot.Manager, StringComparison.Ordinal))
					{
						return KingdomLabOwnedTargetState.Uncertain;
					}
					Target.ExactBodyPart = limb;
					return KingdomLabOwnedTargetState.Present;
				}
				if (limb == null)
				{
					return KingdomLabOwnedTargetState.Absent;
				}
				if (!BodyOwnsPart(Who, limb) || !string.Equals(limb.Manager,
					Snapshot.Manager, StringComparison.Ordinal))
				{
					return KingdomLabOwnedTargetState.Uncertain;
				}
				Target.ExactBodyPart = limb;
				return KingdomLabOwnedTargetState.Present;
			}
			if (ledger.BindingStates[entry] == 3)
			{
				IPart tombstonePart;
				KingdomLabOwnedTargetState tombstone = ledger.ClassifyTombstone(entry,
					out tombstonePart);
				Target.ExactPart = tombstonePart;
				return tombstone;
			}
			IPart exact = ledger.ResolvePart(entry);
			if (Snapshot.Source == (int)LabSource.Mutation)
			{
				XRL.World.Parts.Mutations mutations = Who.GetPart<XRL.World.Parts.Mutations>();
				XRL.World.Parts.Mutation.BaseMutation owned =
					exact as XRL.World.Parts.Mutation.BaseMutation;
				if (owned != null && MutationListed(mutations, owned))
				{
					Target.ExactPart = owned;
					return KingdomLabOwnedTargetState.Present;
				}
				// RemoveMutation deliberately leaves modifier-backed mutation parts. The exact
				// runtime instance plus absence from MutationList proves only our contribution gone.
				if (owned != null)
				{
					Target.ExactPart = owned;
					return KingdomLabOwnedTargetState.Absent;
				}
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (exact != null)
			{
				Target.ExactPart = exact;
				return KingdomLabOwnedTargetState.Present;
			}
			return KingdomLabOwnedTargetState.Uncertain;
		}

		private static KingdomLabOwnedTargetState UntrackedPhysicalState(GameObject Who,
			GameObject Bearer, KingdomLabOwnershipSnapshot Snapshot)
		{
			if (Snapshot.Source == (int)LabSource.Limb)
			{
				return ExactBodyPart(Who, Snapshot.BodyPartId) == null
					? KingdomLabOwnedTargetState.Absent : KingdomLabOwnedTargetState.Uncertain;
			}
			return Bearer.GetPart(Snapshot.Grants) == null
				? KingdomLabOwnedTargetState.Absent : KingdomLabOwnedTargetState.Uncertain;
		}

		private static bool ResolveExactBearer(GameObject Who,
			KingdomLabOwnershipSnapshot Snapshot, out GameObject Bearer)
		{
			Bearer = null;
			if (Snapshot.Source != (int)LabSource.Part
				|| Snapshot.Attach == (int)LabAttach.Body)
			{
				if (!string.Equals(Snapshot.BearerId, Who.ID, StringComparison.Ordinal)) return false;
				Bearer = Who;
				return true;
			}
			if (Snapshot.Attach != (int)LabAttach.Weapon) return false;
			BodyPart slot = ExactBodyPart(Who, Snapshot.BodyPartId);
			GameObject exact = slot?.DefaultBehavior;
			if (slot == null || !BodyOwnsPart(Who, slot) || !GameObject.Validate(exact)
				|| !ReferenceEquals(slot.DefaultBehavior, exact)
				|| !string.Equals(exact.ID, Snapshot.BearerId, StringComparison.Ordinal)) return false;
			Bearer = exact;
			return true;
		}

		/// <summary>Calls the engine only with the exact tracked instance or exact body-part ID.</summary>
		internal static KingdomLabOwnedTargetState RemoveExact(GameObject Who,
			LabProcedure Procedure, KingdomLabOwnershipSnapshot Snapshot)
		{
			KingdomLabOwnedTarget target;
			KingdomLabOwnedTargetState before = ClassifyOwned(Who, Snapshot, out target);
			if (before != KingdomLabOwnedTargetState.Present || target == null)
			{
				return before;
			}
			int tracked = target.Ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId);
			if (target.Ledger.BindingStateAt(tracked) == 3
				&& !target.Ledger.RearmPresent(tracked, target.ExactPart))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (!target.Ledger.BeginRemoval(Snapshot.ProcedureKey, Snapshot.JobId))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			try
			{
				if (Snapshot.Source == (int)LabSource.Limb)
				{
					Who.Body.RemovePartByID(Snapshot.BodyPartId);
				}
				else if (Snapshot.Source == (int)LabSource.Mutation)
				{
					Who.GetPart<XRL.World.Parts.Mutations>()?.RemoveMutation(
						target.ExactPart as XRL.World.Parts.Mutation.BaseMutation);
				}
				else
				{
					target.Bearer.RemovePart(target.ExactPart);
				}
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: exact removal callback threw (" + ex.Message + ")");
			}
			return SettleRemovalIntent(Who, Snapshot, target);
		}

		private static KingdomLabOwnedTargetState SettleRemovalIntent(GameObject Who,
			KingdomLabOwnershipSnapshot Snapshot, KingdomLabOwnedTarget Target)
		{
			int entry = Target.Ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId);
			if (entry < 0) return KingdomLabOwnedTargetState.Uncertain;
			if (Target.Ledger.BindingStateAt(entry) == 3)
			{
				KingdomLabOwnedTarget ignored;
				return ClassifyOwned(Who, Snapshot, out ignored);
			}
			if (Target.Ledger.BindingStateAt(entry) != 4)
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (Snapshot.Source == (int)LabSource.Limb)
			{
				BodyPart limb = ExactBodyPart(Who, Snapshot.BodyPartId);
				if (limb == null)
				{
					Target.Ledger.MarkRemoved(Snapshot.ProcedureKey, Snapshot.JobId);
					return KingdomLabOwnedTargetState.Absent;
				}
				if (BodyOwnsPart(Who, limb) && string.Equals(limb.Manager,
					Snapshot.Manager, StringComparison.Ordinal))
				{
					Target.Ledger.CancelRemoval(Snapshot.ProcedureKey, Snapshot.JobId);
					return KingdomLabOwnedTargetState.Present;
				}
				Target.Ledger.Quarantine(Snapshot.ProcedureKey, Snapshot.JobId);
				return KingdomLabOwnedTargetState.Uncertain;
			}
			IPart exact = Target.ExactPart;
			int ordinal = ReferencePartOrdinal(Target.Bearer, exact);
			if (exact == null || exact.ParentObject == null || ordinal < 0)
			{
				Target.Ledger.MarkRemoved(Snapshot.ProcedureKey, Snapshot.JobId);
				return KingdomLabOwnedTargetState.Absent;
			}
			if (!ReferenceEquals(exact.ParentObject, Target.Bearer)
				|| ordinal != Snapshot.PartOrdinal
				|| !string.Equals(exact.Name, Snapshot.Grants, StringComparison.Ordinal))
			{
				Target.Ledger.Quarantine(Snapshot.ProcedureKey, Snapshot.JobId);
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (Snapshot.Source == (int)LabSource.Mutation
				&& !MutationListed(Who.GetPart<XRL.World.Parts.Mutations>(),
					exact as XRL.World.Parts.Mutation.BaseMutation))
			{
				Target.Ledger.MarkRemoved(Snapshot.ProcedureKey, Snapshot.JobId);
				return KingdomLabOwnedTargetState.Absent;
			}
			Target.Ledger.CancelRemoval(Snapshot.ProcedureKey, Snapshot.JobId);
			return KingdomLabOwnedTargetState.Present;
		}

		internal static bool CleanupOwned(GameObject Who, LabProcedure Procedure,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			KingdomLabOwnedTarget ignored;
			if (ClassifyOwned(Who, Snapshot, out ignored) != KingdomLabOwnedTargetState.Absent)
				return false;
			GameObject bearer;
			if (!ResolveExactBearer(Who, Snapshot, out bearer)) return false;
			XRL.World.Parts.r_KingdomLabEffectLedger ledger =
				bearer?.GetPart<XRL.World.Parts.r_KingdomLabEffectLedger>();
			int entry = ledger?.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) ?? -1;
			if (entry < 0) return false;
			ledger.MarkRemoved(Snapshot.ProcedureKey, Snapshot.JobId);
			IPart tombstonePart;
			if (ledger.ClassifyTombstone(entry, out tombstonePart)
				!= KingdomLabOwnedTargetState.Absent) return false;
			string marker = bearer.GetStringProperty(OwnerProperty(Snapshot.ProcedureKey));
			string nonceMarker = bearer.GetStringProperty(
				OwnerNonceProperty(Snapshot.ProcedureKey));
			if (!string.IsNullOrEmpty(marker)
				&& !string.Equals(marker, Snapshot.JobId, StringComparison.Ordinal)) return false;
			if (!string.IsNullOrEmpty(nonceMarker)
				&& !string.Equals(nonceMarker, Snapshot.EffectNonce,
					StringComparison.Ordinal)) return false;
			if (string.Equals(marker, Snapshot.JobId, StringComparison.Ordinal))
			{
				try { bearer.RemoveStringProperty(OwnerProperty(Snapshot.ProcedureKey)); }
				catch { return false; }
				if (!string.IsNullOrEmpty(bearer.GetStringProperty(
					OwnerProperty(Snapshot.ProcedureKey)))) return false;
			}
			if (string.Equals(nonceMarker, Snapshot.EffectNonce, StringComparison.Ordinal))
			{
				try { bearer.RemoveStringProperty(OwnerNonceProperty(Snapshot.ProcedureKey)); }
				catch { return false; }
			}
			XRL.World.Parts.r_KingdomLabRecord record =
				Who?.GetPart<XRL.World.Parts.r_KingdomLabRecord>();
			record?.ForgetOwned(Snapshot.ProcedureKey, Snapshot.JobId);
			return ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) == entry
				&& ledger.BindingStateAt(entry) == 3
				&& !string.Equals(bearer.GetStringProperty(OwnerProperty(Snapshot.ProcedureKey)),
					Snapshot.JobId, StringComparison.Ordinal)
				&& !string.Equals(bearer.GetStringProperty(
					OwnerNonceProperty(Snapshot.ProcedureKey)), Snapshot.EffectNonce,
					StringComparison.Ordinal)
				&& !RecordContains(record, Snapshot.ProcedureKey, Snapshot.JobId);
		}

		internal static bool PurgeOwnedTombstone(GameObject Who,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			GameObject bearer;
			if (!ResolveExactBearer(Who, Snapshot, out bearer)) return false;
			XRL.World.Parts.r_KingdomLabEffectLedger ledger =
				bearer.GetPart<XRL.World.Parts.r_KingdomLabEffectLedger>();
			int entry = ledger?.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) ?? -1;
			if (entry < 0) return true;
			IPart exact;
			if (ledger.BindingStateAt(entry) != 3
				|| ledger.ClassifyTombstone(entry, out exact)
					!= KingdomLabOwnedTargetState.Absent) return false;
			ledger.Forget(Snapshot.ProcedureKey, Snapshot.JobId, CleanupPatient: false);
			return ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) < 0;
		}

		private static bool RecordContains(XRL.World.Parts.r_KingdomLabRecord Record,
			string Key, string JobId)
		{
			Record?.Normalize();
			for (int i = 0; Record != null && i < Record.Keys.Count; i++)
			{
				if (string.Equals(Record.Keys[i], Key, StringComparison.OrdinalIgnoreCase)
					&& string.Equals(Record.JobIds[i], JobId, StringComparison.Ordinal)) return true;
			}
			return false;
		}

		/// <summary>Compatibility entrypoint, now using the exact ownership protocol.</summary>
		public static bool Remove(GameObject Who, string Key)
		{
			KingdomLabOwnershipSnapshot snapshot;
			if (SnapshotOwned(Who, Key, out snapshot) != KingdomLabOwnedTargetState.Present)
			{
				return false;
			}
			LabProcedure procedure;
			if (!TryGet(Key, out procedure)
				|| RemoveExact(Who, procedure, snapshot) != KingdomLabOwnedTargetState.Absent)
				return false;
			try { Who.WantToReequip(); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: compatibility removal reequip threw (" + ex.Message + ")");
			}
			KingdomLabOwnedTarget ignored;
			return ClassifyOwned(Who, snapshot, out ignored) == KingdomLabOwnedTargetState.Absent
				&& CleanupOwned(Who, procedure, snapshot)
				&& PurgeOwnedTombstone(Who, snapshot);
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
	/// Bearer-side proof of which exact live effect one lab commission owns. Primitive named fields
	/// survive saves; runtime references are rebuilt only while this proof remains present. A
	/// PartRemovedEvent erases the proof before a same-class replacement can inherit it.
	/// </summary>
	[Serializable]
	public class r_KingdomLabEffectLedger : IPart
	{
		public List<string> ProcedureKeys = new List<string>();
		public List<string> JobIds = new List<string>();
		public List<string> PatientIds = new List<string>();
		public List<int> BodyPartIds = new List<int>();
		public List<int> Sources = new List<int>();
		public List<string> ClassNames = new List<string>();
		public List<int> Attaches = new List<int>();
		public List<string> Managers = new List<string>();
		public List<string> Details = new List<string>();
		public List<string> Fingerprints = new List<string>();
		public List<int> PartOrdinals = new List<int>();
		public List<int> BindingStates = new List<int>();
		public List<string> EffectNonces = new List<string>();
		public bool LedgerQuarantined;

		[NonSerialized]
		private List<IPart> RuntimeParts;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
		{
			r_KingdomLabEffectLedger copy = (r_KingdomLabEffectLedger)base.DeepCopy(Parent, MapInv);
			copy.ProcedureKeys = new List<string>(ProcedureKeys ?? new List<string>());
			copy.JobIds = new List<string>(JobIds ?? new List<string>());
			copy.PatientIds = new List<string>(PatientIds ?? new List<string>());
			copy.BodyPartIds = new List<int>(BodyPartIds ?? new List<int>());
			copy.Sources = new List<int>(Sources ?? new List<int>());
			copy.ClassNames = new List<string>(ClassNames ?? new List<string>());
			copy.Attaches = new List<int>(Attaches ?? new List<int>());
			copy.Managers = new List<string>(Managers ?? new List<string>());
			copy.Details = new List<string>(Details ?? new List<string>());
			copy.Fingerprints = new List<string>(Fingerprints ?? new List<string>());
			copy.PartOrdinals = new List<int>(PartOrdinals ?? new List<int>());
			copy.BindingStates = new List<int>(BindingStates ?? new List<int>());
			copy.EffectNonces = new List<string>(EffectNonces ?? new List<string>());
			copy.RuntimeParts = null;
			return copy;
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			Normalize();
			for (int i = 0; i < EffectNonces.Count; i++)
			{
				EffectNonces[i] = Guid.NewGuid().ToString("N");
				BindingStates[i] = 2;
			}
			LedgerQuarantined = true;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == PooledEvent<PartRemovedEvent>.ID;
		}

		public override bool HandleEvent(PartRemovedEvent E)
		{
			Normalize();
			for (int i = ProcedureKeys.Count - 1; i >= 0; i--)
			{
				IPart runtime = RuntimeParts[i];
				if (ReferenceEquals(runtime, E.Part))
				{
					if (BindingStates[i] == 4 || BindingStates[i] == 3)
					{
						BindingStates[i] = 3;
					}
					else
					{
						ForgetAt(i, CleanupPatient: true);
					}
				}
			}
			return base.HandleEvent(E);
		}

		public override void ObjectLoaded()
		{
			base.ObjectLoaded();
			Normalize();
			for (int i = 0; i < ProcedureKeys.Count; i++)
			{
				RebindAt(i);
			}
		}

		public void Track(string ProcedureKey, string JobId, string PatientId, int BodyPartId,
			int Source, string ClassName, IPart RuntimePart)
		{
			string manager = KingdomProcedures.ManagerFor(ProcedureKey);
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, ProcedureKey, ClassName, Source,
				(int)LabAttach.Body, manager, "");
			TrackIntent(ProcedureKey, JobId, PatientId, BodyPartId, Source,
				(int)LabAttach.Body, ClassName, manager, "", fingerprint,
				KingdomProcedures.ReferencePartOrdinal(ParentObject, RuntimePart), RuntimePart);
			CommitBinding(ProcedureKey, JobId,
				KingdomProcedures.ReferencePartOrdinal(ParentObject, RuntimePart), RuntimePart);
		}

		public void TrackIntent(string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, int Source, int Attach, string ClassName, string Manager,
			string Detail, string Fingerprint, int PartOrdinal, IPart RuntimePart,
			string EffectNonce = "")
		{
			Normalize();
			string nonce = EffectNonce ?? "";
			if (LedgerQuarantined)
			{
				LedgerQuarantined = true;
				throw new InvalidOperationException("lab effect ledger is quarantined");
			}
			if (!KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
				ProcedureKey, ClassName, Source, Attach, Manager, Fingerprint, Detail))
			{
				throw new InvalidOperationException("invalid lab effect contract");
			}
			int existing = IndexOf(ProcedureKey, JobId);
			if (existing >= 0)
			{
				if (string.IsNullOrEmpty(nonce)) nonce = EffectNonces[existing];
				if (!EntryMatches(existing, ProcedureKey, JobId, PatientId, BodyPartId,
					Source, Attach, ClassName, Manager, Detail, Fingerprint, PartOrdinal,
					IgnoreOrdinal: true)
					|| nonce.Length != 32
					|| !string.Equals(EffectNonces[existing], nonce, StringComparison.Ordinal))
				{
					throw new InvalidOperationException("lab effect identity collision");
				}
				RuntimeParts[existing] = RuntimePart;
				PartOrdinals[existing] = PartOrdinal;
				BindingStates[existing] = 0;
				return;
			}
			if (string.IsNullOrEmpty(nonce)) nonce = Guid.NewGuid().ToString("N");
			if (nonce.Length != 32)
				throw new InvalidOperationException("invalid lab effect nonce");
			if (ProcedureKeys.Count >= KingdomLabRules.MaxEffectRows)
			{
				LedgerQuarantined = true;
				throw new InvalidOperationException("lab effect ledger is full");
			}
			ProcedureKeys.Add(ProcedureKey ?? "");
			JobIds.Add(JobId ?? "");
			PatientIds.Add(PatientId ?? "");
			BodyPartIds.Add(BodyPartId);
			Sources.Add(Source);
			ClassNames.Add(ClassName ?? "");
			Attaches.Add(Attach);
			Managers.Add(Manager ?? "");
			Details.Add(Detail ?? "");
			Fingerprints.Add(Fingerprint ?? "");
			PartOrdinals.Add(PartOrdinal);
			BindingStates.Add(0);
			EffectNonces.Add(nonce);
			RuntimeParts.Add(RuntimePart);
		}

		public string NonceAt(int At)
		{
			Normalize();
			return At >= 0 && At < EffectNonces.Count ? EffectNonces[At] : "";
		}

		public void CommitBinding(string ProcedureKey, string JobId, int PartOrdinal,
			IPart RuntimePart)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at < 0) throw new InvalidOperationException("lab effect intent is absent");
			if (Sources[at] == (int)LabSource.Limb)
			{
				BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartIds[at]);
				if (limb == null || !KingdomProcedures.BodyOwnsPart(ParentObject, limb)
					|| !string.Equals(limb.Manager, Managers[at], StringComparison.Ordinal))
				{
					BindingStates[at] = 2;
					throw new InvalidOperationException("exact limb binding is not present");
				}
			}
			else
			{
				if (RuntimePart == null || !ReferenceEquals(RuntimePart.ParentObject, ParentObject)
					|| KingdomProcedures.ReferencePartOrdinal(ParentObject, RuntimePart) != PartOrdinal)
				{
					BindingStates[at] = 2;
					throw new InvalidOperationException("exact part binding is not present at its ordinal");
				}
			}
			PartOrdinals[at] = PartOrdinal;
			RuntimeParts[at] = RuntimePart;
			BindingStates[at] = 1;
		}

		public void Quarantine(string ProcedureKey, string JobId)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at >= 0) BindingStates[at] = 2;
		}

		public bool BeginRemoval(string ProcedureKey, string JobId)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at < 0 || BindingStates[at] == 2 || BindingStates[at] == 3) return false;
			BindingStates[at] = 4;
			return true;
		}

		public void MarkRemoved(string ProcedureKey, string JobId)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at >= 0 && BindingStates[at] != 2) BindingStates[at] = 3;
		}

		public void CancelRemoval(string ProcedureKey, string JobId)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at >= 0 && BindingStates[at] == 4) BindingStates[at] = 1;
		}

		public int BindingStateAt(int At)
		{
			Normalize();
			return At < 0 || At >= BindingStates.Count ? 2 : BindingStates[At];
		}

		internal bool RearmPresent(int At, IPart Exact)
		{
			Normalize();
			if (At < 0 || At >= ProcedureKeys.Count || BindingStates[At] != 3) return false;
			if (Sources[At] == (int)LabSource.Limb)
			{
				BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartIds[At]);
				if (limb == null || !KingdomProcedures.BodyOwnsPart(ParentObject, limb)
					|| !string.Equals(limb.Manager, Managers[At], StringComparison.Ordinal))
					return false;
			}
			else
			{
				int ordinal = KingdomProcedures.ReferencePartOrdinal(ParentObject, Exact);
				if (Exact == null || !ReferenceEquals(Exact.ParentObject, ParentObject)
					|| ordinal != PartOrdinals[At]
					|| !string.Equals(Exact.Name, ClassNames[At], StringComparison.Ordinal)
					|| (Sources[At] == (int)LabSource.Mutation
						&& !KingdomProcedures.MutationListed(ParentObject.GetPart<Mutations>(),
							Exact as XRL.World.Parts.Mutation.BaseMutation))) return false;
				RuntimeParts[At] = Exact;
			}
			BindingStates[At] = 1;
			return true;
		}

		internal KingdomLabOwnedTargetState ClassifyTombstone(int At, out IPart Exact)
		{
			Exact = null;
			Normalize();
			if (At < 0 || At >= ProcedureKeys.Count || BindingStates[At] != 3)
				return KingdomLabOwnedTargetState.Uncertain;
			IPart runtime = RuntimeParts[At];
			if (runtime == null || runtime.ParentObject == null)
			{
				int frozenOrdinal = PartOrdinals[At];
				IPart candidate = frozenOrdinal >= 0 && ParentObject?.PartsList != null
					&& frozenOrdinal < ParentObject.PartsList.Count
					? ParentObject.PartsList[frozenOrdinal] : null;
				if (candidate == null || !string.Equals(candidate.Name, ClassNames[At],
					StringComparison.Ordinal)) return KingdomLabOwnedTargetState.Absent;
				if (Sources[At] == (int)LabSource.Mutation
					&& !KingdomProcedures.MutationListed(ParentObject.GetPart<Mutations>(),
						candidate as XRL.World.Parts.Mutation.BaseMutation))
				{
					return KingdomLabOwnedTargetState.Absent;
				}
				return KingdomLabOwnedTargetState.Uncertain;
			}
			int ordinal = KingdomProcedures.ReferencePartOrdinal(ParentObject, runtime);
			if (!ReferenceEquals(runtime.ParentObject, ParentObject) || ordinal < 0
				|| ordinal != PartOrdinals[At]
				|| !string.Equals(runtime.Name, ClassNames[At], StringComparison.Ordinal))
				return KingdomLabOwnedTargetState.Uncertain;
			if (Sources[At] == (int)LabSource.Mutation
				&& !KingdomProcedures.MutationListed(ParentObject.GetPart<Mutations>(),
					runtime as XRL.World.Parts.Mutation.BaseMutation))
				return KingdomLabOwnedTargetState.Absent;
			Exact = runtime;
			return KingdomLabOwnedTargetState.Present;
		}

		public bool UpgradeLegacyLimb(string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, int Attach, string ClassName, string Manager, string Detail,
			string Fingerprint)
		{
			Normalize();
			if (LedgerQuarantined || string.IsNullOrEmpty(JobId)
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					ProcedureKey, ClassName, (int)LabSource.Limb, Attach, Manager,
					Fingerprint, Detail)) return false;
			int at = IndexOf(ProcedureKey, JobId);
			if (at < 0 || !string.Equals(PatientIds[at], PatientId, StringComparison.Ordinal)
				|| Sources[at] != (int)LabSource.Limb || BodyPartIds[at] != BodyPartId
				|| (!string.IsNullOrEmpty(ClassNames[at])
					&& !string.Equals(ClassNames[at], ClassName, StringComparison.Ordinal)))
			{
				return false;
			}
			BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartId);
			if (limb == null || !KingdomProcedures.BodyOwnsPart(ParentObject, limb)
				|| !string.Equals(limb.Manager, Manager, StringComparison.Ordinal)
				|| !string.Equals(ParentObject?.GetStringProperty(
					KingdomProcedures.OwnerProperty(ProcedureKey)), JobId, StringComparison.Ordinal))
			{
				return false;
			}
			ClassNames[at] = ClassName;
			Attaches[at] = Attach;
			Managers[at] = Manager;
			Details[at] = Detail;
			Fingerprints[at] = Fingerprint;
			PartOrdinals[at] = -1;
			BindingStates[at] = 1;
			return true;
		}

		public int IndexOf(string ProcedureKey, string JobId)
		{
			Normalize();
			for (int i = 0; i < ProcedureKeys.Count; i++)
			{
				if (string.Equals(ProcedureKeys[i], ProcedureKey,
					StringComparison.OrdinalIgnoreCase)
					&& string.Equals(JobIds[i], JobId, StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		public bool EntryMatches(int At, string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, int Source, int Attach, string ClassName, string Manager,
			string Detail, string Fingerprint, int PartOrdinal, bool IgnoreOrdinal = false)
		{
			Normalize();
			return At >= 0 && At < ProcedureKeys.Count
				&& string.Equals(ProcedureKeys[At], ProcedureKey,
					StringComparison.OrdinalIgnoreCase)
				&& string.Equals(JobIds[At], JobId, StringComparison.Ordinal)
				&& string.Equals(PatientIds[At], PatientId, StringComparison.Ordinal)
				&& BodyPartIds[At] == BodyPartId && Sources[At] == Source && Attaches[At] == Attach
				&& string.Equals(ClassNames[At], ClassName, StringComparison.Ordinal)
				&& string.Equals(Managers[At], Manager, StringComparison.Ordinal)
				&& string.Equals(Details[At], Detail, StringComparison.Ordinal)
				&& string.Equals(Fingerprints[At], Fingerprint, StringComparison.Ordinal)
				&& (IgnoreOrdinal || PartOrdinals[At] == PartOrdinal);
		}

		public bool EntryMatches(int At, string ProcedureKey, string JobId, string PatientId,
			int BodyPartId, int Source, string ClassName)
		{
			Normalize();
			return At >= 0 && At < ProcedureKeys.Count
				&& string.Equals(ProcedureKeys[At], ProcedureKey, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(JobIds[At], JobId, StringComparison.Ordinal)
				&& string.Equals(PatientIds[At], PatientId, StringComparison.Ordinal)
				&& BodyPartIds[At] == BodyPartId && Sources[At] == Source
				&& string.Equals(ClassNames[At], ClassName, StringComparison.Ordinal);
		}

		public IPart ResolvePart(int At)
		{
			Normalize();
			if (LedgerQuarantined || At < 0 || At >= ProcedureKeys.Count
				|| Sources[At] == (int)LabSource.Limb || BindingStates[At] == 2
				|| BindingStates[At] == 3 || BindingStates[At] == 4)
			{
				return null;
			}
			IPart runtime = RuntimeParts[At];
			if (runtime != null && ReferenceEquals(runtime.ParentObject, ParentObject)
				&& KingdomProcedures.ReferencePartOrdinal(ParentObject, runtime) == PartOrdinals[At]
				&& string.Equals(runtime.Name, ClassNames[At], StringComparison.Ordinal))
			{
				return runtime;
			}
			RuntimeParts[At] = null;
			return RebindAt(At);
		}

		private IPart RebindAt(int At)
		{
			if (LedgerQuarantined || At < 0 || At >= ProcedureKeys.Count
				|| BindingStates[At] == 2
				|| !string.Equals(ParentObject?.GetStringProperty(
					KingdomProcedures.OwnerProperty(ProcedureKeys[At])), JobIds[At],
					StringComparison.Ordinal)
				|| !string.Equals(ParentObject?.GetStringProperty(
					KingdomProcedures.OwnerNonceProperty(ProcedureKeys[At])), EffectNonces[At],
					StringComparison.Ordinal))
			{
				return null;
			}
			if (BindingStates[At] == 3) return null;
			if (BindingStates[At] == 4)
			{
				BindingStates[At] = 2;
				return null;
			}
			if (Sources[At] == (int)LabSource.Limb)
			{
				BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartIds[At]);
				if (limb != null && KingdomProcedures.BodyOwnsPart(ParentObject, limb)
					&& string.Equals(limb.Manager, Managers[At], StringComparison.Ordinal)
					&& KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
						ProcedureKeys[At], ClassNames[At], Sources[At], Attaches[At], Managers[At],
						Fingerprints[At], Details[At]))
				{
					BindingStates[At] = 1;
					return null;
				}
				BindingStates[At] = 2;
				return null;
			}
			int ordinal = PartOrdinals[At];
			if (ordinal < 0 || ParentObject?.PartsList == null || ordinal >= ParentObject.PartsList.Count)
			{
				BindingStates[At] = 2;
				return null;
			}
			IPart candidate = ParentObject.PartsList[ordinal];
			if (candidate == null || !ReferenceEquals(candidate.ParentObject, ParentObject)
				|| !string.Equals(candidate.Name, ClassNames[At], StringComparison.Ordinal)
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					ProcedureKeys[At], ClassNames[At], Sources[At], Attaches[At], Managers[At],
					Fingerprints[At], Details[At]))
			{
				BindingStates[At] = 2;
				return null;
			}
			if (Sources[At] == (int)LabSource.Mutation
				&& !KingdomProcedures.MutationListed(
					ParentObject.GetPart<XRL.World.Parts.Mutations>(),
					candidate as XRL.World.Parts.Mutation.BaseMutation))
			{
				BindingStates[At] = 2;
				return null;
			}
			RuntimeParts[At] = candidate;
			BindingStates[At] = 1;
			return candidate;
		}

		public void Forget(string ProcedureKey, string JobId, bool CleanupPatient)
		{
			int at = IndexOf(ProcedureKey, JobId);
			if (at >= 0)
			{
				ForgetAt(at, CleanupPatient);
			}
		}

		private void ForgetAt(int At, bool CleanupPatient)
		{
			string key = ProcedureKeys[At];
			string job = JobIds[At];
			string nonce = EffectNonces[At];
			string patientId = PatientIds[At];
			ProcedureKeys.RemoveAt(At);
			JobIds.RemoveAt(At);
			PatientIds.RemoveAt(At);
			BodyPartIds.RemoveAt(At);
			Sources.RemoveAt(At);
			ClassNames.RemoveAt(At);
			Attaches.RemoveAt(At);
			Managers.RemoveAt(At);
			Details.RemoveAt(At);
			Fingerprints.RemoveAt(At);
			PartOrdinals.RemoveAt(At);
			BindingStates.RemoveAt(At);
			EffectNonces.RemoveAt(At);
			RuntimeParts.RemoveAt(At);
			try
			{
				if (string.Equals(ParentObject.GetStringProperty(
					KingdomProcedures.OwnerProperty(key)), job, StringComparison.Ordinal))
				{
					ParentObject.RemoveStringProperty(KingdomProcedures.OwnerProperty(key));
				}
				if (string.Equals(ParentObject.GetStringProperty(
					KingdomProcedures.OwnerNonceProperty(key)), nonce, StringComparison.Ordinal))
				{
					ParentObject.RemoveStringProperty(KingdomProcedures.OwnerNonceProperty(key));
				}
				if (CleanupPatient)
				{
					GameObject patient = GameObject.FindByID(patientId);
					patient?.GetPart<r_KingdomLabRecord>()?.ForgetOwned(key, job);
				}
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: effect-ledger cleanup threw (" + ex.Message + ")");
			}
		}

		public void Normalize()
		{
			ProcedureKeys = ProcedureKeys ?? new List<string>();
			JobIds = JobIds ?? new List<string>();
			PatientIds = PatientIds ?? new List<string>();
			BodyPartIds = BodyPartIds ?? new List<int>();
			Sources = Sources ?? new List<int>();
			ClassNames = ClassNames ?? new List<string>();
			Attaches = Attaches ?? new List<int>();
			Managers = Managers ?? new List<string>();
			Details = Details ?? new List<string>();
			Fingerprints = Fingerprints ?? new List<string>();
			PartOrdinals = PartOrdinals ?? new List<int>();
			BindingStates = BindingStates ?? new List<int>();
			EffectNonces = EffectNonces ?? new List<string>();
			int original = ProcedureKeys.Count;
			int count = original;
			count = Math.Min(count, JobIds.Count);
			count = Math.Min(count, PatientIds.Count);
			count = Math.Min(count, BodyPartIds.Count);
			count = Math.Min(count, Sources.Count);
			count = Math.Min(count, ClassNames.Count);
			if (count != original || JobIds.Count != original || PatientIds.Count != original
				|| BodyPartIds.Count != original || Sources.Count != original
				|| ClassNames.Count != original)
			{
				LedgerQuarantined = true;
			}
			if (Attaches.Count != count || Managers.Count != count || Details.Count != count
				|| Fingerprints.Count != count || PartOrdinals.Count != count
				|| BindingStates.Count != count)
			{
				// Pre-contract ledgers cannot prove which authored effect they own. Keep the
				// rows as individually quarantined; a unique manager-owned legacy limb may be
				// upgraded later without making a class-only inference.
			}
			if (count > KingdomLabRules.MaxEffectRows)
			{
				LedgerQuarantined = true;
				count = KingdomLabRules.MaxEffectRows;
			}
			Trim(ProcedureKeys, count);
			Trim(JobIds, count);
			Trim(PatientIds, count);
			Trim(BodyPartIds, count);
			Trim(Sources, count);
			Trim(ClassNames, count);
			Pad(Attaches, count, -1);
			Pad(Managers, count, "");
			Pad(Details, count, "");
			Pad(Fingerprints, count, "");
			Pad(PartOrdinals, count, -1);
			Pad(BindingStates, count, 2);
			Pad(EffectNonces, count, "");
			for (int i = 0; i < count; i++)
			{
				if (BindingStates[i] < 0 || BindingStates[i] > 4
					|| EffectNonces[i].Length != 32
					|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
						ProcedureKeys[i], ClassNames[i], Sources[i], Attaches[i], Managers[i],
						Fingerprints[i], Details[i]))
				{
					BindingStates[i] = 2;
				}
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(ProcedureKeys[i], ProcedureKeys[j], StringComparison.OrdinalIgnoreCase)
						&& string.Equals(JobIds[i], JobIds[j], StringComparison.Ordinal))
					{
						BindingStates[i] = BindingStates[j] = 2;
						LedgerQuarantined = true;
					}
				}
			}
			RuntimeParts = RuntimeParts ?? new List<IPart>();
			Trim(RuntimeParts, count);
			while (RuntimeParts.Count < count)
			{
				RuntimeParts.Add(null);
			}
		}

		private static void Pad<T>(List<T> Values, int Count, T Value)
		{
			Trim(Values, Count);
			while (Values.Count < Count) Values.Add(Value);
		}

		private static void Trim<T>(List<T> Values, int Count)
		{
			if (Values.Count > Count)
			{
				Values.RemoveRange(Count, Values.Count - Count);
			}
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Normalize();
			for (int i = 0; i < ProcedureKeys.Count; i++)
			{
				if (BindingStates[i] == 2 || BindingStates[i] == 3) continue;
				if (BindingStates[i] == 4)
				{
					BindingStates[i] = 2;
					continue;
				}
				if (Sources[i] == (int)LabSource.Limb)
				{
					BodyPart limb = KingdomProcedures.ExactBodyPart(ParentObject, BodyPartIds[i]);
					if (limb == null || !KingdomProcedures.BodyOwnsPart(ParentObject, limb)
						|| !string.Equals(limb.Manager, Managers[i], StringComparison.Ordinal))
					{
						BindingStates[i] = 2;
					}
					continue;
				}
				IPart exact = RuntimeParts[i];
				int ordinal = KingdomProcedures.ReferencePartOrdinal(ParentObject, exact);
				if (exact == null || ordinal < 0 || ordinal != PartOrdinals[i]
					|| !ReferenceEquals(exact.ParentObject, ParentObject)
					|| !string.Equals(exact.Name, ClassNames[i], StringComparison.Ordinal)
					|| (Sources[i] == (int)LabSource.Mutation
						&& !KingdomProcedures.MutationListed(
							ParentObject?.GetPart<XRL.World.Parts.Mutations>(),
							exact as XRL.World.Parts.Mutation.BaseMutation)))
				{
					BindingStates[i] = 2;
				}
			}
			Writer.WriteNamedFields(this, typeof(r_KingdomLabEffectLedger));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomLabEffectLedger));
			RuntimeParts = null;
			Normalize();
		}
	}

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

		/// <summary>Stable selected body-part identity, index for index.</summary>
		public List<int> BodyPartIds = new List<int>();

		/// <summary>Stable exact effect bearer identity, index for index.</summary>
		public List<string> BearerIds = new List<string>();

		/// <summary>Commission identity written into the ownership marker.</summary>
		public List<string> JobIds = new List<string>();

		/// <summary>Frozen execution contract. DisplayNames is presentation only; the remaining
		/// columns authorize exact recovery and removal.</summary>
		public List<string> DisplayNames = new List<string>();
		public List<string> Grants = new List<string>();
		public List<int> Sources = new List<int>();
		public List<int> Attaches = new List<int>();
		public List<string> Managers = new List<string>();
		public List<string> Details = new List<string>();
		public List<string> Fingerprints = new List<string>();
		public List<int> PartOrdinals = new List<int>();
		public List<string> EffectNonces = new List<string>();
		public bool RegistryQuarantined;
		public string RegistryFault = "";

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

		public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
		{
			r_KingdomLabRecord copy = (r_KingdomLabRecord)base.DeepCopy(Parent, MapInv);
			copy.Keys = new List<string>(Keys ?? new List<string>());
			copy.Places = new List<string>(Places ?? new List<string>());
			copy.OnWeapon = new List<bool>(OnWeapon ?? new List<bool>());
			copy.BodyPartIds = new List<int>(BodyPartIds ?? new List<int>());
			copy.BearerIds = new List<string>(BearerIds ?? new List<string>());
			copy.JobIds = new List<string>(JobIds ?? new List<string>());
			copy.DisplayNames = new List<string>(DisplayNames ?? new List<string>());
			copy.Grants = new List<string>(Grants ?? new List<string>());
			copy.Sources = new List<int>(Sources ?? new List<int>());
			copy.Attaches = new List<int>(Attaches ?? new List<int>());
			copy.Managers = new List<string>(Managers ?? new List<string>());
			copy.Details = new List<string>(Details ?? new List<string>());
			copy.Fingerprints = new List<string>(Fingerprints ?? new List<string>());
			copy.PartOrdinals = new List<int>(PartOrdinals ?? new List<int>());
			copy.EffectNonces = new List<string>(EffectNonces ?? new List<string>());
			copy.Excluded = new List<string>(Excluded ?? new List<string>());
			return copy;
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			for (int i = 0; i < EffectNonces.Count; i++)
				EffectNonces[i] = Guid.NewGuid().ToString("N");
			RegistryQuarantined = true;
			RegistryFault = "Copied patient receipt has fresh nonces and no procedure authority.";
		}

		/// <summary>Records one procedure. Idempotent on the latch, so nothing anywhere has to
		/// remember whether it already asked.</summary>
		public void Note(string Key, string Place, bool OnWeapon)
		{
			NoteLegacy(Key, Place, OnWeapon, 0, "", "");
		}

		public void Note(string Key, string Place, bool OnWeapon, int BodyPartId,
			string BearerId, string JobId)
		{
			NoteLegacy(Key, Place, OnWeapon, BodyPartId, BearerId, JobId);
		}

		private void NoteLegacy(string Key, string Place, bool OnWeapon, int BodyPartId,
			string BearerId, string JobId)
		{
			if (string.IsNullOrEmpty(Key)) return;
			Normalize();
			if (Keys.Count >= KingdomLabRules.MaxEffectRows)
			{
				RegistryQuarantined = true;
				RegistryFault = "The patient ownership receipt registry is full.";
				return;
			}
			Keys.Add(Key);
			Places.Add(Place ?? "");
			this.OnWeapon.Add(OnWeapon);
			BodyPartIds.Add(BodyPartId);
			BearerIds.Add(BearerId ?? "");
			JobIds.Add(JobId ?? "");
			DisplayNames.Add("");
			Grants.Add("");
			Sources.Add(-1);
			Attaches.Add(-1);
			Managers.Add("");
			Details.Add("");
			Fingerprints.Add("");
			PartOrdinals.Add(-1);
			EffectNonces.Add("");
			LabProcedure procedure;
			if (KingdomProcedures.TryGet(Key, out procedure) && procedure.IsNamed)
			{
				NamedLatch = KingdomProcedureRules.Latch(NamedLatch, Key);
			}
		}

		public void Note(string Key, string Place, bool OnWeapon, int BodyPartId,
			string BearerId, string JobId, string DisplayName, string Grants, int Source,
			int Attach, string Manager, string Detail, string Fingerprint, int PartOrdinal,
			string EffectNonce = "")
		{
			Normalize();
			if (RegistryQuarantined || string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(JobId)
				|| BodyPartId <= 0 || string.IsNullOrEmpty(BearerId)
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					Key, Grants, Source, Attach, Manager, Fingerprint, Detail)
				|| string.IsNullOrEmpty(EffectNonce) || EffectNonce.Length != 32)
			{
				throw new InvalidOperationException("invalid or quarantined patient ownership receipt");
			}
			for (int i = 0; i < Keys.Count; i++)
			{
				if (!string.Equals(Keys[i], Key, StringComparison.OrdinalIgnoreCase)
					|| !string.Equals(JobIds[i], JobId, StringComparison.Ordinal)) continue;
				if (BodyPartIds[i] == BodyPartId
					&& string.Equals(BearerIds[i], BearerId, StringComparison.Ordinal)
					&& string.Equals(this.Grants[i], Grants, StringComparison.Ordinal)
					&& Sources[i] == Source && Attaches[i] == Attach
					&& string.Equals(Managers[i], Manager, StringComparison.Ordinal)
					&& string.Equals(Details[i], Detail, StringComparison.Ordinal)
					&& string.Equals(Fingerprints[i], Fingerprint, StringComparison.Ordinal)
					&& PartOrdinals[i] == PartOrdinal
					&& string.Equals(EffectNonces[i], EffectNonce, StringComparison.Ordinal))
				{
					return;
				}
				RegistryQuarantined = true;
				RegistryFault = "An ownership receipt reused a job ID with different physical identity.";
				throw new InvalidOperationException(RegistryFault);
			}
			if (Keys.Count >= KingdomLabRules.MaxEffectRows)
			{
				RegistryQuarantined = true;
				RegistryFault = "The patient ownership receipt registry is full.";
				throw new InvalidOperationException(RegistryFault);
			}
			Keys.Add(Key);
			Places.Add(Place ?? "");
			this.OnWeapon.Add(OnWeapon);
			BodyPartIds.Add(BodyPartId);
			BearerIds.Add(BearerId ?? "");
			JobIds.Add(JobId ?? "");
			DisplayNames.Add(DisplayName ?? Key);
			this.Grants.Add(Grants ?? "");
			Sources.Add(Source);
			Attaches.Add(Attach);
			Managers.Add(Manager ?? "");
			Details.Add(Detail ?? "");
			Fingerprints.Add(Fingerprint ?? "");
			PartOrdinals.Add(PartOrdinal);
			EffectNonces.Add(EffectNonce);
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
				RemoveAt(i);
				return;
			}
		}

		/// <summary>Forgets only the record minted by one exact commission.</summary>
		public void ForgetOwned(string Key, string JobId)
		{
			Normalize();
			for (int i = Keys.Count - 1; i >= 0; i--)
			{
				if (string.Equals(Keys[i], Key, StringComparison.OrdinalIgnoreCase)
					&& i < JobIds.Count
					&& string.Equals(JobIds[i], JobId, StringComparison.Ordinal))
				{
					RemoveAt(i);
					return;
				}
			}
		}

		private void RemoveAt(int At)
		{
			Keys.RemoveAt(At);
			Places.RemoveAt(At);
			OnWeapon.RemoveAt(At);
			BodyPartIds.RemoveAt(At);
			BearerIds.RemoveAt(At);
			JobIds.RemoveAt(At);
			DisplayNames.RemoveAt(At);
			Grants.RemoveAt(At);
			Sources.RemoveAt(At);
			Attaches.RemoveAt(At);
			Managers.RemoveAt(At);
			Details.RemoveAt(At);
			Fingerprints.RemoveAt(At);
			PartOrdinals.RemoveAt(At);
			EffectNonces.RemoveAt(At);
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
				if (BodyPartIds[i] <= 0
					&& string.Equals(Places[i], Place, StringComparison.OrdinalIgnoreCase))
				{
					return Keys[i];
				}
			}
			return null;
		}

		/// <summary>Exact identity lookup for current records; type is only a legacy fallback.</summary>
		public string GraftedAt(int BodyPartId, string LegacyPlace)
		{
			Normalize();
			for (int i = 0; i < Keys.Count; i++)
			{
				if (BodyPartId > 0 && i < BodyPartIds.Count && BodyPartIds[i] == BodyPartId)
				{
					return Keys[i];
				}
			}
			return GraftedAt(LegacyPlace);
		}

		internal bool ContractAt(int At, out KingdomLabOwnershipSnapshot Snapshot, string PatientId)
		{
			Normalize();
			Snapshot = default(KingdomLabOwnershipSnapshot);
			if (RegistryQuarantined || At < 0 || At >= Keys.Count || BodyPartIds[At] <= 0
				|| string.IsNullOrEmpty(BearerIds[At]) || string.IsNullOrEmpty(JobIds[At])
				|| EffectNonces[At].Length != 32
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					Keys[At], Grants[At], Sources[At], Attaches[At], Managers[At],
					Fingerprints[At], Details[At]))
			{
				return false;
			}
			Snapshot = new KingdomLabOwnershipSnapshot(Keys[At], JobIds[At], PatientId,
				BodyPartIds[At], BearerIds[At], Grants[At], Sources[At], Attaches[At],
				Managers[At], Details[At], Fingerprints[At], PartOrdinals[At],
				EffectNonces[At]);
			return true;
		}

		internal bool UpgradeLegacyLimbAt(int At, int BodyPartId, string BearerId,
			string JobId, string DisplayName, string Grants, int Attach, string Manager,
			string Detail, string Fingerprint)
		{
			Normalize();
			if (RegistryQuarantined || At < 0 || At >= Keys.Count || BodyPartId <= 0
				|| string.IsNullOrEmpty(BearerId) || string.IsNullOrEmpty(JobId)
				|| !string.IsNullOrEmpty(Fingerprints[At])
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					Keys[At], Grants, (int)LabSource.Limb, Attach, Manager,
					Fingerprint, Detail)) return false;
			BodyPartIds[At] = BodyPartId;
			BearerIds[At] = BearerId;
			JobIds[At] = JobId;
			DisplayNames[At] = DisplayName ?? Keys[At];
			this.Grants[At] = Grants;
			Sources[At] = (int)LabSource.Limb;
			Attaches[At] = Attach;
			Managers[At] = Manager;
			Details[At] = Detail;
			Fingerprints[At] = Fingerprint;
			PartOrdinals[At] = -1;
			return true;
		}

		public int IndexOf(string Key)
		{
			Normalize();
			for (int i = 0; i < Keys.Count; i++)
			{
				if (string.Equals(Keys[i], Key, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>Repairs a record read from a save written by an older build: null containers
		/// become empty ones, and lists that fell out of step are trimmed to their shortest, because
		/// a record that says a graft is at a place it cannot name is worse than one that says
		/// nothing.</summary>
		public void Normalize()
		{
			Keys = Keys ?? new List<string>();
			Places = Places ?? new List<string>();
			OnWeapon = OnWeapon ?? new List<bool>();
			Excluded = Excluded ?? new List<string>();
			BodyPartIds = BodyPartIds ?? new List<int>();
			BearerIds = BearerIds ?? new List<string>();
			JobIds = JobIds ?? new List<string>();
			DisplayNames = DisplayNames ?? new List<string>();
			Grants = Grants ?? new List<string>();
			Sources = Sources ?? new List<int>();
			Attaches = Attaches ?? new List<int>();
			Managers = Managers ?? new List<string>();
			Details = Details ?? new List<string>();
			Fingerprints = Fingerprints ?? new List<string>();
			PartOrdinals = PartOrdinals ?? new List<int>();
			EffectNonces = EffectNonces ?? new List<string>();
			NamedLatch = NamedLatch ?? "";
			RegistryFault = RegistryFault ?? "";

			if (Keys.Count > KingdomLabRules.MaxEffectRows)
			{
				RegistryQuarantined = true;
				RegistryFault = "Patient ownership receipt registry exceeded its bound.";
				Keys.RemoveRange(KingdomLabRules.MaxEffectRows,
					Keys.Count - KingdomLabRules.MaxEffectRows);
			}
			int count = Keys.Count;
			bool anyContract = DisplayNames.Count > 0 || Grants.Count > 0 || Sources.Count > 0
				|| Attaches.Count > 0 || Managers.Count > 0 || Details.Count > 0
				|| Fingerprints.Count > 0 || PartOrdinals.Count > 0 || EffectNonces.Count > 0;
			if (anyContract && (DisplayNames.Count != count || Grants.Count != count
				|| Sources.Count != count || Attaches.Count != count || Managers.Count != count
				|| Details.Count != count || Fingerprints.Count != count
				|| PartOrdinals.Count != count || EffectNonces.Count != count))
			{
				RegistryQuarantined = true;
				RegistryFault = "Patient ownership receipt columns disagree.";
			}
			Pad(Places, count, "");
			Pad(OnWeapon, count, false);
			Pad(BodyPartIds, count, 0);
			Pad(BearerIds, count, "");
			Pad(JobIds, count, "");
			Pad(DisplayNames, count, "");
			Pad(Grants, count, "");
			Pad(Sources, count, -1);
			Pad(Attaches, count, -1);
			Pad(Managers, count, "");
			Pad(Details, count, "");
			Pad(Fingerprints, count, "");
			Pad(PartOrdinals, count, -1);
			Pad(EffectNonces, count, "");
			for (int i = 0; i < count; i++)
			{
				bool claimsContract = !string.IsNullOrEmpty(Fingerprints[i])
					|| !string.IsNullOrEmpty(Grants[i]) || Sources[i] >= 0 || Attaches[i] >= 0;
				if (claimsContract && (!KingdomLabRules.ValidEffectContract(
					KingdomLabRules.EffectContractVersion, Keys[i], Grants[i], Sources[i],
					Attaches[i], Managers[i], Fingerprints[i], Details[i])
					|| EffectNonces[i].Length != 32))
				{
					RegistryQuarantined = true;
					RegistryFault = "A patient ownership receipt has an invalid effect contract.";
				}
				if (string.IsNullOrEmpty(JobIds[i])) continue;
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(Keys[i], Keys[j], StringComparison.OrdinalIgnoreCase)
						&& string.Equals(JobIds[i], JobIds[j], StringComparison.Ordinal))
					{
						RegistryQuarantined = true;
						RegistryFault = "Patient ownership receipts duplicate one job identity.";
					}
				}
			}
			if (Excluded.Count > 256)
			{
				Excluded.RemoveRange(256, Excluded.Count - 256);
			}
		}

		private static void Pad<T>(List<T> Values, int Count, T Value)
		{
			if (Values.Count > Count) Values.RemoveRange(Count, Values.Count - Count);
			while (Values.Count < Count) Values.Add(Value);
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
