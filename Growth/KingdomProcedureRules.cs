using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Where a granted part actually has to sit for its events to reach anybody.
	/// <para>
	/// <b>This is the audit's whole lesson, and it is a fact about vanilla, not a preference.</b>
	/// One melee hit fires <c>"AttackerHit"</c> on the ATTACKER
	/// (<c>D/XRL/World/Parts/Combat.cs:1146-1154</c> &mdash; <c>Attacker.FireEvent(obj5)</c> at
	/// <c>:1154</c>) and <c>"WeaponHit"</c> on the WEAPON
	/// (<c>:1178-1186</c> &mdash; <c>Weapon.FireEvent(obj7)</c> at <c>:1186</c>). A part whose
	/// <c>Register</c> asks only for the weapon event is <b>inert</b> if it is copied onto a
	/// player's torso: nothing will ever fire it there. No record in this registry ships without
	/// stating which of the two it is, and the grant verb puts it where it said.
	/// </para>
	/// <para>
	/// The weapon a natural attack carries is the limb's own <c>DefaultBehavior</c> object:
	/// <c>BodyPart.GetFirstValidWeapon</c> returns it (<c>D/XRL/World/Anatomy/BodyPart.cs:2874-2895</c>),
	/// <c>Combat.cs:729-756</c> hands it through as the <c>Weapon</c> argument, and
	/// <c>Combat.cs:1636-1639</c> returns early on a null weapon &mdash; so there is no unarmed
	/// branch anywhere: <b>every</b> melee attack has a weapon object, and for a natural attack that
	/// object is the limb's default behaviour. <c>Combat.cs:1648</c> names the case outright, in
	/// vanilla's own words, by accepting a weapon the attacker <c>IsADefaultBehavior</c> of.
	/// </para>
	/// </summary>
	public enum LabAttach : byte
	{
		/// <summary>Copied onto the founder themselves. Correct for anything registering an
		/// <c>Attacker*</c> event, and for every part that answers a pooled event on its
		/// bearer.</summary>
		Body = 0,

		/// <summary>Copied onto the natural weapon standing at the granted slot. The only honest
		/// home for a part that asks solely for <c>"WeaponHit"</c> or <c>"WeaponDealDamage"</c>.
		/// Refused, by name, at a slot that bears no natural weapon.</summary>
		Weapon = 1
	}

	/// <summary>What the founder must bring, and by which of vanilla's three write paths it lands
	/// (DIVERSITY-AND-TECH-TREES &sect;3.4's source table).</summary>
	public enum LabSource : byte
	{
		/// <summary>A preserved part from a creature that carried the named <c>IPart</c>. Granted
		/// with <c>IPart.DeepCopy</c> (<c>D/XRL/World/IPart.cs:401-435</c>), so the source
		/// instance's own field values are the numbers the founder gets.</summary>
		Part = 0,

		/// <summary>A preserved severed limb, carrying <c>DismemberedProperties</c>
		/// (<c>D/XRL/World/Parts/Body.cs:2557</c>). Granted with <c>BodyPart.AddPartAt</c>.</summary>
		Limb = 1,

		/// <summary>A preserved gland or organ from a mutation-bearing creature. Granted with
		/// <c>Mutations.AddMutation</c>, and never at the source's own level
		/// (<see cref="KingdomProcedureRules.GrantedMutationLevel"/>).</summary>
		Mutation = 2
	}

	/// <summary>
	/// The class ladder, mapped onto the risk split the precedent whitelist already keeps
	/// (DIVERSITY &sect;3.4). The numbers are the rung vocabulary the founder reads, so they are
	/// stable and are never renumbered (STANDARDS &sect;9).
	/// </summary>
	public enum LabClass : byte
	{
		/// <summary>Attack riders. The hall's ordinary work.</summary>
		Rider = 1,

		/// <summary>Defences and utility. The hall's ordinary work with teeth.</summary>
		Defence = 2,

		/// <summary>A new limb at a named slot, with whatever it brings. The theatre's work.</summary>
		Limb = 3,

		/// <summary>One of the four, once ever, found in the world and never listed before it is
		/// (DIVERSITY &sect;3.7; Addendum 14 at full strength, Addendum 20's hidden clause).</summary>
		Named = 4
	}

	/// <summary>
	/// What one attempt to commission a procedure came to. Every refusal names a thing the founder
	/// could go and do; none of them says "that failed" (STANDARDS 7b).
	/// <para>
	/// Appended to, never renumbered: these are a published vocabulary the moment a third party's
	/// record can provoke one.
	/// </para>
	/// </summary>
	public enum LabVerdict : byte
	{
		/// <summary>The hall will do it.</summary>
		Allowed = 0,

		/// <summary>The founder's anatomy carries no part of the type this record wants. The
		/// rationing mechanism, and the reason the lab cannot become a shopping list.</summary>
		RefusedNoSlot = 1,

		/// <summary>There is such a part, and something is already grafted to it.</summary>
		RefusedSlotTaken = 2,

		/// <summary>There is such a part and it is not of a kind this procedure will open &mdash;
		/// the <c>SlotCategories</c> gate, and the whole of how a True Kin, a robot and a slime get
		/// different legal sets with no genotype list anywhere.</summary>
		RefusedCategory = 3,

		/// <summary>The hall is not built high enough for this class of work.</summary>
		RefusedRung = 4,

		/// <summary>A weapon-attach record at a slot that bears no natural weapon. Nothing to ride,
		/// so nothing is grafted &mdash; the audit's lesson enforced at the commit.</summary>
		RefusedNoWeapon = 5,

		/// <summary>The vat-house is keeping nothing that answers this record.</summary>
		RefusedUnkept = 6,

		/// <summary>A named procedure this founder has already had. Once, ever.</summary>
		RefusedOnceEver = 7,

		/// <summary>A named procedure nobody has found yet. Never named in the refusal, because
		/// saying its name is the thing the visibility law forbids.</summary>
		RefusedUndiscovered = 8,

		/// <summary>What is kept is of the class, and is not of this record's own band. The
		/// QUESTION-BACKLOG QB-10 seam: two records over one class, priced apart by what the
		/// source itself carries.</summary>
		RefusedMagnitude = 9
	}

	/// <summary>
	/// One place on the founder's body, as the rules half sees it. Read off the real anatomy by
	/// <c>KingdomProcedures.Census</c> and never constructed anywhere else, so every judgment below
	/// is a pure function of what the founder actually is.
	/// </summary>
	public readonly struct LabSlot
	{
		/// <summary>The vanilla <c>BodyPart.Type</c> &mdash; "Arm", "Face", "Fungal Outcrop". An
		/// OPEN vocabulary: <c>B/Bodies.xml</c> declares 157 of them and
		/// <c>CyberneticsGraftedMirrorArm</c> mints "Thrown Weapon" at runtime
		/// (<c>D/XRL/World/Parts/CyberneticsGraftedMirrorArm.cs:31</c>), so nothing here validates
		/// against a closed list.</summary>
		public readonly string Type;

		/// <summary>The <c>BodyPartCategory</c> code, 1 to 23
		/// (<c>D/XRL/World/Anatomy/BodyPartCategory.cs:8-52</c>). Zero for a part whose category
		/// could not be read, which every <c>SlotCategories</c> gate then refuses rather than
		/// guesses at.</summary>
		public readonly int Category;

		/// <summary>Vanilla's own disqualifier: an extrinsic part is worn scaffolding, not the
		/// body. <c>BodyPart.CanReceiveCyberneticImplant</c> refuses on exactly this and on
		/// category (<c>D/XRL/World/Anatomy/BodyPart.cs:7072-7083</c>).</summary>
		public readonly bool Extrinsic;

		/// <summary>Whether this limb carries a <c>DefaultBehavior</c> object &mdash; the thing a
		/// natural attack is actually made with, and the only lawful home for a
		/// <see cref="LabAttach.Weapon"/> record.</summary>
		public readonly bool Bears;

		/// <summary>The key of the procedure already grafted here, or null. One graft to a place:
		/// the ceiling is the founder's body, not their patience.</summary>
		public readonly string Grafted;

		public LabSlot(string Type, int Category, bool Extrinsic, bool Bears, string Grafted)
		{
			this.Type = Type ?? "";
			this.Category = Category;
			this.Extrinsic = Extrinsic;
			this.Bears = Bears;
			this.Grafted = string.IsNullOrEmpty(Grafted) ? null : Grafted;
		}
	}

	/// <summary>
	/// One authored procedure. A catalogue record in the same idiom as a building, shipped in
	/// <c>KingdomProcedures.xml</c>, mergeable by key, and extended by anybody who ships a file
	/// with the matching root (STANDARDS &sect;6).
	/// </summary>
	public sealed class LabProcedure
	{
		/// <summary>Registry identity. Merge-by-key, folded, like every other data lane.</summary>
		public string Key;

		/// <summary>What the slate calls it. Falls back to <see cref="Key"/>.</summary>
		public string DisplayName;

		public LabClass Class = LabClass.Rider;

		/// <summary>
		/// The <c>IPart</c> class this procedure grants &mdash; <b>never a creature, and never "a
		/// creature's power"</b> (DIVERSITY &sect;3.4 hard rule 1). This is what makes the registry
		/// a contract: a modded creature carrying <c>PoisonOnHit</c> is a lawful source for the
		/// envenomed sting the day that mod ships, with no entry of ours.
		/// </summary>
		public string Grants;

		/// <summary><c>BodyPart.Type</c> names, comma separated &mdash; exactly
		/// <c>CyberneticsBaseItem.Slots</c>'s own shape
		/// (<c>D/XRL/World/Parts/CyberneticsBaseItem.cs:14,155-157</c>). Checked against the
		/// founder's OWN anatomy, never against a table.</summary>
		public string Slots;

		/// <summary><c>BodyPartCategory</c> names, comma separated. Empty admits any live
		/// category, so a record that says nothing about kind is a record about every kind.</summary>
		public string SlotCategories;

		public LabSource Source = LabSource.Part;

		public LabAttach Attach = LabAttach.Body;

		/// <summary>The rung of hall this class of work wants. 0 the slab, 1 the vat-house, 2 the
		/// grafting hall, 3 the chimeric theatre.</summary>
		public int MinRung = 2;

		/// <summary>Drams the commission draws from the city's dedicated stores.</summary>
		public int Cost;

		/// <summary>Bits, in vanilla's own bit-string vocabulary.</summary>
		public string Bits;

		/// <summary>Days of the hall's real labour. Never a timer: a hall with no hands works no
		/// days at all (Addendum 8 clause 2).</summary>
		public int StaffDays = 1;

		/// <summary>Preserved parts consumed. One creature, one limb.</summary>
		public int Preserved = 1;

		/// <summary>Standing this costs, in the <c>-Faction</c> removal idiom the QoL vocabulary
		/// already speaks. Spent through the existing <c>AdjustStanding</c> path.</summary>
		public string Creeds;

		/// <summary>Roster tokens the city must hold, in <c>KingdomZoningRules.Knows</c>'s own
		/// grammar. The lab's own gates ride the shipped knowledge lane and mint nothing.</summary>
		public string Knowledge;

		/// <summary>
		/// The band of the source part's own field this record will take, as
		/// <c>Field:Low-High</c>. Null takes anything.
		/// <para>
		/// <b>The QB-10 mechanism, and it names a FIELD rather than a creature</b>, so hard rule 1
		/// survives intact: <c>ReflectDamage</c> ships as two records over one class because a
		/// quartz baboon carries <c>ReflectPercentage="5"</c> and a mirror bug carries
		/// <c>"100"</c>, and under "your sting is its sting" those are not the same product at the
		/// same price. Nothing is clamped &mdash; the founder still gets exactly what they brought
		/// home; the band only decides which slate the thing they brought home appears on.
		/// </para>
		/// </summary>
		public string Magnitude;

		/// <summary>Lines the slate prints under the name, before anything is committed. Authored,
		/// because the one documented complaint about the vanilla picker is consequence-legibility
		/// (DIVERSITY &sect;3.0d), and because a procedure with a cost to the founder's own city
		/// must say so in words (STANDARDS 7b).</summary>
		public List<string> Discloses = new List<string>();

		/// <summary>What the slate calls it, with the key as the fallback so a half-authored record
		/// still reads as something.</summary>
		public string Named => string.IsNullOrEmpty(DisplayName) ? Key : DisplayName;

		/// <summary>Whether this is one of the four. Never listed until found, once ever per
		/// founder, and reset for an heir (Addendum 22 C11).</summary>
		public bool IsNamed => Class == LabClass.Named;
	}

	/// <summary>
	/// The lab's arithmetic and its judgments, engine-free and total: what a body will take, where a
	/// granted part has to sit for anything to reach it, what a carcass keeps, what a mutation is
	/// worth once the hall is done capping it, and every sentence a refusal is told with.
	/// <para>
	/// <b>Nothing here reads a clock and nothing here rolls a die</b> &mdash; with exactly one
	/// exception, which is <see cref="ChooseChimericSlot"/>, is confessedly a gamble, is priced as
	/// one, and draws through the settlement kernel so that the same confession on the same save
	/// always comes to the same thing (DIVERSITY &sect;3.4 hard rule 4; &sect;3.7).
	/// </para>
	/// <para>
	/// The engine-coupled half is <c>KingdomProcedures</c> and <c>KingdomLab</c>, in the same
	/// folder, exactly as <c>KingdomMirrorGateRules</c> sits beside <c>KingdomMirrorGate</c>.
	/// </para>
	/// </summary>
	public static class KingdomProcedureRules
	{
		// --- The rung ladder (DIVERSITY §3.3) --------------------------------------------------

		/// <summary>The butcher's slab. Not the lab: the prerequisite.</summary>
		public const int RungSlab = 0;

		/// <summary>The vat-house. Nothing is grafted here; things are kept.</summary>
		public const int RungVat = 1;

		/// <summary>The grafting hall. Class I and Class II.</summary>
		public const int RungHall = 2;

		/// <summary>The chimeric theatre. Class III, and the four named.</summary>
		public const int RungTheatre = 3;

		/// <summary>
		/// The rung a class of work wants when a record does not say. Not a default so much as the
		/// ladder itself: riders and defences are hall work, limbs and named procedures are theatre
		/// work, and a record that disagrees with its own class is saying something deliberate.
		/// </summary>
		public static int RungForClass(LabClass Class)
		{
			return (Class == LabClass.Limb || Class == LabClass.Named) ? RungTheatre : RungHall;
		}

		// --- The stamp grammar --------------------------------------------------------------
		//
		// A preserved part carries, on the item itself, what was read off the creature BEFORE it
		// was butchered: the class names it bore and the field values those classes held. That is
		// the whole of the snapshot idiom (DIVERSITY §3.4's read path), and it is a string, so it
		// is written and read here where it can be tabled.

		/// <summary>Between one stamped class and the next.</summary>
		public const char ClassSeparator = ';';

		/// <summary>Between a class name and its field blob.</summary>
		public const char BlobSeparator = '@';

		/// <summary>Between one field and the next inside a blob.</summary>
		public const char FieldSeparator = ',';

		/// <summary>Between a field's name and its value.</summary>
		public const char ValueSeparator = '=';

		/// <summary>
		/// Whether a name or value can be stamped whole.
		/// <para>
		/// Refused rather than escaped, which is the posture the realm's own register keeps
		/// (<c>KingdomMirrorGateRules.Storable</c>): a value that cannot be stored whole is a value
		/// the stamp would give back wrong, and giving back a wrong number is worse than admitting
		/// there is no number.
		/// </para>
		/// </summary>
		public static bool Stampable(string Text)
		{
			return !string.IsNullOrEmpty(Text)
				&& Text.IndexOf(ClassSeparator) < 0
				&& Text.IndexOf(BlobSeparator) < 0
				&& Text.IndexOf(FieldSeparator) < 0
				&& Text.IndexOf(ValueSeparator) < 0;
		}

		/// <summary>
		/// One class's stamp: its name, and the fields it was carrying. Field order is the caller's
		/// (the engine half hands them in reflection order, which is stable for a type), so the
		/// same creature always stamps the same string.
		/// </summary>
		/// <param name="ClassName">The <c>IPart</c> class name. Refused if unstampable.</param>
		/// <param name="Fields">Name to value. Null or empty stamps the class alone, which is what
		/// a field-less part honestly is.</param>
		/// <returns>The stamp, or null when the class name could not be written down.</returns>
		public static string FormatStamp(string ClassName, IList<KeyValuePair<string, string>> Fields)
		{
			if (!Stampable(ClassName))
			{
				return null;
			}
			System.Text.StringBuilder text = new System.Text.StringBuilder(ClassName);
			if (Fields == null || Fields.Count == 0)
			{
				return text.ToString();
			}
			bool any = false;
			for (int i = 0; i < Fields.Count; i++)
			{
				if (!Stampable(Fields[i].Key) || !Stampable(Fields[i].Value))
				{
					// One unwritable field costs its own field and nothing else: the class still
					// stamps, so a part with one exotic field is still a lawful source.
					continue;
				}
				text.Append(any ? FieldSeparator : BlobSeparator);
				text.Append(Fields[i].Key).Append(ValueSeparator).Append(Fields[i].Value);
				any = true;
			}
			return text.ToString();
		}

		/// <summary>Joins the stamps a whole carcass carried into the one string the preserved item
		/// holds.</summary>
		public static string FormatStamps(IList<string> Stamps)
		{
			if (Stamps == null || Stamps.Count == 0)
			{
				return "";
			}
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			for (int i = 0; i < Stamps.Count; i++)
			{
				if (string.IsNullOrEmpty(Stamps[i]))
				{
					continue;
				}
				if (text.Length > 0)
				{
					text.Append(ClassSeparator);
				}
				text.Append(Stamps[i]);
			}
			return text.ToString();
		}

		/// <summary>The class names a stamp carries, in stamp order. Never null.</summary>
		public static List<string> StampedClasses(string Stamp)
		{
			List<string> names = new List<string>();
			if (string.IsNullOrEmpty(Stamp))
			{
				return names;
			}
			string[] entries = Stamp.Split(ClassSeparator);
			for (int i = 0; i < entries.Length; i++)
			{
				string entry = entries[i];
				if (entry.Length == 0)
				{
					continue;
				}
				int at = entry.IndexOf(BlobSeparator);
				string name = (at < 0) ? entry : entry.Substring(0, at);
				if (name.Length > 0 && !names.Contains(name))
				{
					names.Add(name);
				}
			}
			return names;
		}

		/// <summary>Whether a preserved part is a lawful source for a record: it was stamped with
		/// the class the record grants.</summary>
		public static bool StampCarries(string Stamp, string ClassName)
		{
			if (string.IsNullOrEmpty(ClassName))
			{
				return false;
			}
			return StampedClasses(Stamp).Contains(ClassName.Trim());
		}

		/// <summary>
		/// One stamped field's value, or null.
		/// </summary>
		/// <param name="Stamp">The whole stamp, all classes.</param>
		/// <param name="ClassName">Which class's blob to read.</param>
		/// <param name="Field">Which field.</param>
		public static string StampedField(string Stamp, string ClassName, string Field)
		{
			if (string.IsNullOrEmpty(Stamp) || string.IsNullOrEmpty(ClassName) || string.IsNullOrEmpty(Field))
			{
				return null;
			}
			string wanted = ClassName.Trim();
			string field = Field.Trim();
			string[] entries = Stamp.Split(ClassSeparator);
			for (int i = 0; i < entries.Length; i++)
			{
				int at = entries[i].IndexOf(BlobSeparator);
				if (at < 0 || entries[i].Substring(0, at) != wanted)
				{
					continue;
				}
				string[] pairs = entries[i].Substring(at + 1).Split(FieldSeparator);
				for (int p = 0; p < pairs.Length; p++)
				{
					int eq = pairs[p].IndexOf(ValueSeparator);
					if (eq > 0 && pairs[p].Substring(0, eq) == field)
					{
						return pairs[p].Substring(eq + 1);
					}
				}
				return null;
			}
			return null;
		}

		// --- The knowledge gate ----------------------------------------------------------------

		/// <summary>
		/// Whether a city's keepers hold everything a record asks for.
		/// <para>
		/// <b>The shipped roster grammar and nothing of ours.</b> ALL tokens are required and each
		/// may carry alternatives, exactly as a <c>&lt;building&gt;</c>'s <c>Knowledge</c> does
		/// (<c>KingdomZoningRules.Knows</c>) &mdash; which is what lets a procedure gate on a
		/// research node, a shared rite, a taught disk or a certified machine with one attribute,
		/// and lets a third party's procedure gate on a third party's research with no C# at all.
		/// </para>
		/// </summary>
		/// <param name="Roster">The city's rolls. Null reads as a city that knows nothing.</param>
		/// <param name="Knowledge">The record's declaration. Null or empty asks nothing.</param>
		public static bool KnowledgeMet(ICollection<string> Roster, string Knowledge)
		{
			foreach (string token in KingdomZoningRules.Tokens(Knowledge))
			{
				if (!KingdomZoningRules.Knows(Roster, token))
				{
					return false;
				}
			}
			return true;
		}

		// --- The magnitude band (QUESTION-BACKLOG QB-10) --------------------------------------

		/// <summary>
		/// Reads a <c>Magnitude</c> attribute: <c>Field:Low-High</c>, both ends inclusive.
		/// </summary>
		/// <returns>False when the attribute is present and unreadable, which is a typo wearing a
		/// rule's clothes and is refused at load. An absent attribute reads as true with a null
		/// field, which is the ordinary state of every record that takes any source.</returns>
		public static bool TryParseMagnitude(string Source, out string Field, out int Low, out int High, out string Error)
		{
			Field = null;
			Low = 0;
			High = 0;
			Error = null;
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return true;
			}
			string text = Source.Trim();
			int colon = text.IndexOf(':');
			int dash = text.IndexOf('-', (colon < 0) ? 0 : colon + 1);
			if (colon <= 0 || dash <= colon + 1)
			{
				Error = "\"" + Source + "\" is not Field:Low-High.";
				return false;
			}
			string field = text.Substring(0, colon).Trim();
			int low;
			int high;
			if (field.Length == 0
				|| !int.TryParse(text.Substring(colon + 1, dash - colon - 1).Trim(), out low)
				|| !int.TryParse(text.Substring(dash + 1).Trim(), out high))
			{
				Error = "\"" + Source + "\" carries no readable band.";
				return false;
			}
			if (low > high)
			{
				Error = "\"" + Source + "\" runs backwards.";
				return false;
			}
			Field = field;
			Low = low;
			High = high;
			return true;
		}

		/// <summary>
		/// Whether a stamped source falls in a record's band. A record with no band takes anything;
		/// a record WITH a band refuses a source whose field could not be read, because admitting a
		/// number nobody could read is exactly how a rung-2 price buys a rung-3 product.
		/// </summary>
		public static bool MagnitudeAdmits(LabProcedure Procedure, string Stamp)
		{
			if (Procedure == null)
			{
				return false;
			}
			string field;
			int low;
			int high;
			string error;
			if (!TryParseMagnitude(Procedure.Magnitude, out field, out low, out high, out error) || field == null)
			{
				return true;
			}
			int value;
			return int.TryParse(StampedField(Stamp, Procedure.Grants, field), out value) && value >= low && value <= high;
		}

		// --- The slot judgment (DIVERSITY §3.4 hard rules 2 and 3) -----------------------------

		/// <summary>The slot types a record names, folded and trimmed. Empty means the record names
		/// none, which no judgment below will ever match &mdash; deliberately, because a record that
		/// forgot to say where it goes must not go everywhere.</summary>
		public static List<string> SlotTypes(LabProcedure Procedure)
		{
			return Split((Procedure == null) ? null : Procedure.Slots);
		}

		/// <summary>
		/// The category names a record names, in the case the file wrote them. Empty admits any live
		/// category.
		/// <para>
		/// Trimmed and NOT folded, unlike <see cref="SlotTypes"/>, and the asymmetry is forced by
		/// vanilla: <c>BodyPartCategory.GetCode</c> switches on exact strings and answers zero for
		/// anything it does not recognise (<c>D/XRL/World/Anatomy/BodyPartCategory.cs:104-160</c>).
		/// Slot types are compared against a founder's own anatomy by us, so we fold both sides;
		/// category names are handed to the engine, so they go as written.
		/// </para>
		/// </summary>
		public static List<string> SlotCategoryNames(LabProcedure Procedure)
		{
			return SplitTrimmed((Procedure == null) ? null : Procedure.SlotCategories);
		}

		/// <summary>
		/// Whether one place on the founder's body could take one procedure, and if not, why.
		/// <para>
		/// The order is the order the founder would want to hear it in: the wrong place first
		/// (nothing can answer that), then the wrong kind of place, then the place already spoken
		/// for, then the place with nothing on it to ride. Each is a different sentence and each
		/// names a different thing to go and do.
		/// </para>
		/// </summary>
		/// <param name="Procedure">The record.</param>
		/// <param name="Slot">One place, read off the real anatomy.</param>
		/// <param name="Categories">The category CODES this record admits, resolved by the engine
		/// half through <c>BodyPartCategory</c>'s own name table
		/// (<c>D/XRL/World/Anatomy/BodyPartCategory.cs:104,163</c>). Null or empty admits any.</param>
		public static LabVerdict JudgeSlot(LabProcedure Procedure, LabSlot Slot, IList<int> Categories)
		{
			if (Procedure == null)
			{
				return LabVerdict.RefusedNoSlot;
			}
			List<string> wanted = SlotTypes(Procedure);
			if (wanted.Count == 0 || !wanted.Contains(Fold(Slot.Type)))
			{
				return LabVerdict.RefusedNoSlot;
			}
			// Vanilla's own disqualifier, and it leads because it is the one that is true about the
			// place rather than about the record: worn scaffolding is not a body, whatever the
			// record wants (BodyPart.CanReceiveCyberneticImplant, D/…/BodyPart.cs:7074-7077).
			if (Slot.Extrinsic)
			{
				return LabVerdict.RefusedCategory;
			}
			if (Categories != null && Categories.Count > 0 && !Categories.Contains(Slot.Category))
			{
				return LabVerdict.RefusedCategory;
			}
			if (Slot.Grafted != null)
			{
				return LabVerdict.RefusedSlotTaken;
			}
			if (Procedure.Attach == LabAttach.Weapon && !Slot.Bears)
			{
				return LabVerdict.RefusedNoWeapon;
			}
			return LabVerdict.Allowed;
		}

		/// <summary>
		/// The places on this body that would take this procedure, in anatomy order.
		/// <para>
		/// Anatomy order rather than sorted, because the founder reads their own body top to bottom
		/// and the slate must list it the way they would say it.
		/// </para>
		/// </summary>
		public static List<int> LegalSlots(LabProcedure Procedure, IList<LabSlot> Anatomy, IList<int> Categories)
		{
			List<int> legal = new List<int>();
			if (Procedure == null || Anatomy == null)
			{
				return legal;
			}
			for (int i = 0; i < Anatomy.Count; i++)
			{
				if (JudgeSlot(Procedure, Anatomy[i], Categories) == LabVerdict.Allowed)
				{
					legal.Add(i);
				}
			}
			return legal;
		}

		/// <summary>
		/// The kindest true refusal for a procedure with no legal slot at all. Walking the body
		/// once and keeping the most specific answer, so a founder with a taken arm hears "already
		/// spoken for" rather than "there is nowhere on you", which would be a lie.
		/// </summary>
		public static LabVerdict BestRefusal(LabProcedure Procedure, IList<LabSlot> Anatomy, IList<int> Categories)
		{
			LabVerdict best = LabVerdict.RefusedNoSlot;
			if (Procedure == null || Anatomy == null)
			{
				return best;
			}
			for (int i = 0; i < Anatomy.Count; i++)
			{
				LabVerdict verdict = JudgeSlot(Procedure, Anatomy[i], Categories);
				if (verdict == LabVerdict.Allowed)
				{
					return LabVerdict.Allowed;
				}
				// Ranked by how near the founder is to having it: a slot bearing no weapon is one
				// natural weapon away; a taken slot is one removal away; a wrong-kind slot is a
				// body away; no slot at all is the furthest of the four.
				if (Rank(verdict) > Rank(best))
				{
					best = verdict;
				}
			}
			return best;
		}

		private static int Rank(LabVerdict Verdict)
		{
			switch (Verdict)
			{
			case LabVerdict.RefusedNoWeapon:
				return 3;
			case LabVerdict.RefusedSlotTaken:
				return 2;
			case LabVerdict.RefusedCategory:
				return 1;
			default:
				return 0;
			}
		}

		/// <summary>
		/// The whole verdict on one commission, anatomy and hall and vat-house and history
		/// together. What the slate calls before it offers a row, and what the commit calls again
		/// before it takes a dram &mdash; because the founder may have been away and the answer may
		/// have changed.
		/// </summary>
		/// <param name="Procedure">The record.</param>
		/// <param name="Anatomy">The founder's own body.</param>
		/// <param name="Categories">Resolved category codes; null admits any.</param>
		/// <param name="Rung">The highest rung of lab standing in this city.</param>
		/// <param name="Kept">Preserved parts in the vat-house that are lawful sources.</param>
		/// <param name="Discovered">Whether a named procedure has been found in the world. Ignored
		/// for every other class.</param>
		/// <param name="AlreadyDone">Whether a named procedure has already been performed on this
		/// founder.</param>
		public static LabVerdict Judge(LabProcedure Procedure, IList<LabSlot> Anatomy, IList<int> Categories,
			int Rung, int Kept, bool Discovered, bool AlreadyDone)
		{
			if (Procedure == null)
			{
				return LabVerdict.RefusedNoSlot;
			}
			// Discovery is asked first and answered in silence, because every other refusal names
			// the procedure and this is the one that may not (Addendum 14, Addendum 20's hidden
			// clause). A named procedure nobody has found has no row at all.
			if (Procedure.IsNamed && !Discovered)
			{
				return LabVerdict.RefusedUndiscovered;
			}
			if (Procedure.IsNamed && AlreadyDone)
			{
				return LabVerdict.RefusedOnceEver;
			}
			if (Rung < Procedure.MinRung)
			{
				return LabVerdict.RefusedRung;
			}
			if (Kept < Procedure.Preserved)
			{
				return LabVerdict.RefusedUnkept;
			}
			return BestRefusal(Procedure, Anatomy, Categories);
		}

		// --- The preservation chain (DIVERSITY §3.5) ------------------------------------------

		/// <summary>
		/// Preserved parts one raw part binds into, on vanilla's own arithmetic and nothing else.
		/// <para>
		/// <b>The design note this corrects, and it is worth stating plainly.</b> &sect;3.5 records
		/// the figure as "<c>Result x Number x Count</c>", which reads as a product of three. It is
		/// not. <c>Campfire.PerformPreserve</c> (<c>D/XRL/World/Parts/Campfire.cs:512</c>) seeds a
		/// count of one (<c>:543</c>), OVERWRITES it with <c>PreparedCookingIngredient.charges</c>
		/// if that part is present (<c>:544-547</c>), overwrites it AGAIN with
		/// <c>PreservableItem.Number</c> if THAT part is present (<c>:548-551</c> &mdash; so
		/// <c>Number</c> wins outright rather than multiplying), and only then multiplies by the
		/// stack (<c>:552</c> <c>num3 *= go.Count</c>). <c>Result</c> is the BLUEPRINT handed over
		/// (<c>:554-557</c>), not a factor. The vat-house issues exactly this, because inventing our
		/// own multiplier would be inventing a second economy on top of one that already works.
		/// </para>
		/// <para>
		/// Vanilla's shipped calibration is the sanity check: bear meat gives 5, a dawnglider tail
		/// 10, a psychal gland 5 &mdash; so a carcass yielding three to eight preserved parts is
		/// vanilla-shaped, and a Class III limb consuming a whole creature's yield reads correctly
		/// as one creature, one limb.
		/// </para>
		/// </summary>
		/// <param name="Number">The source's <c>PreservableItem.Number</c>. Zero or less reads as
		/// one, which is what a part carrying no number honestly is.</param>
		/// <param name="Count">The stack size going in.</param>
		public static int PreservedYield(int Number, int Count)
		{
			if (Count <= 0)
			{
				return 0;
			}
			long yield = (long)((Number > 0) ? Number : 1) * Count;
			return (yield > int.MaxValue) ? int.MaxValue : (int)yield;
		}

		/// <summary>
		/// What the vat-house's own labour turns one raw part into over a stretch of days.
		/// <para>
		/// <b>There is no rot anywhere in this and there never will be.</b> Vanilla has none &mdash;
		/// <c>PreservableItem</c> is two fields and no behaviour at all
		/// (<c>D/XRL/World/Parts/PreservableItem.cs:8,10</c>) &mdash; and a decay timer would be a
		/// rate that ran on time alone, which Addendum 8 clause 2 forbids outright. What gates the
		/// chain is LABOUR: a staffed work, real hands, real world-days. A vat-house with nobody in
		/// it keeps what it holds forever and preserves nothing new, which is the honest shape.
		/// </para>
		/// </summary>
		/// <param name="ElapsedTicks">Ticks since the vat last settled.</param>
		/// <param name="CrewEffectiveness">Hands and capability together, 0 to 100.</param>
		/// <param name="WearEffectiveness">What the building's condition leaves of it, 0 to 100.</param>
		/// <returns>Labour ticks actually worked. Zero when any term is zero, by arithmetic rather
		/// than by a special case.</returns>
		public static int VatWorked(long ElapsedTicks, int CrewEffectiveness, int WearEffectiveness)
		{
			if (ElapsedTicks <= 0L || CrewEffectiveness <= 0 || WearEffectiveness <= 0)
			{
				return 0;
			}
			long rate = (long)Clamp(CrewEffectiveness, 0, 100) * Clamp(WearEffectiveness, 0, 100) / 100L;
			long worked = KingdomRules.LabouredTicks(ElapsedTicks, (int)rate);
			return (worked > int.MaxValue) ? int.MaxValue : (int)worked;
		}

		/// <summary>Days of the vat's labour one raw part wants before it is kept. One, and it is
		/// deliberately the smallest number that is still a day: the vat-house is a gate, not a
		/// tax, and it has to be worth building for the trade good alone.</summary>
		public const int PreserveDays = 1;

		/// <summary>A procedure's authored staff-days, in ticks. Staff-days at the settlement's own
		/// day, exactly as the research bench counts its effort.</summary>
		public static int StaffDayTicks(int StaffDays)
		{
			if (StaffDays <= 0)
			{
				return (int)KingdomRules.TicksPerDay;
			}
			long ticks = (long)StaffDays * KingdomRules.TicksPerDay;
			return (ticks > int.MaxValue) ? int.MaxValue : (int)ticks;
		}

		// --- The mutation cap (DIVERSITY §3.4 source table; §3.9 risk 3) -----------------------

		/// <summary>The floor a granted mutation lands at.</summary>
		public const int MinMutationLevel = 1;

		/// <summary>
		/// The ceiling a granted mutation lands at, and it is <b>never the source's own level</b>.
		/// The single most load-bearing balance number in the wave: Playable Slime's own to-do file
		/// records that a free repeatable absorb verb <i>"makes all fighting styles ...
		/// unnecessary"</i>, and granting a level-10 creature's mutation at level 10 is that failure
		/// exactly (DIVERSITY &sect;3.0a, &sect;3.9 risk 3).
		/// </summary>
		public const int MaxMutationLevel = 3;

		/// <summary>What a granted mutation is actually worth. Clamped to
		/// <see cref="MinMutationLevel"/>..<see cref="MaxMutationLevel"/> whatever the source
		/// carried, and a source with no level at all still grants a real mutation rather than
		/// nothing.</summary>
		public static int GrantedMutationLevel(int SourceLevel)
		{
			return Clamp(SourceLevel, MinMutationLevel, MaxMutationLevel);
		}

		// --- Once, ever (DIVERSITY §3.7; Addendum 22 C11) --------------------------------------

		/// <summary>Between named procedures in the founder's own record of what has been done to
		/// them. The same shape the realm's arch register keeps, and for the same reason: it is one
		/// string that has to come back out saying exactly what went in.</summary>
		public const char LatchSeparator = '|';

		/// <summary>Whether this founder has already had a named procedure. Folded, because a key
		/// is a key whatever case the file wrote it in.</summary>
		public static bool Latched(string Latch, string Key)
		{
			if (string.IsNullOrEmpty(Latch) || string.IsNullOrEmpty(Key))
			{
				return false;
			}
			string wanted = Fold(Key);
			string[] done = Latch.Split(LatchSeparator);
			for (int i = 0; i < done.Length; i++)
			{
				if (Fold(done[i]) == wanted)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The founder's record with one more named procedure in it. Copy-on-write and idempotent:
		/// latching a thing twice is the same string, so nothing anywhere has to remember whether it
		/// already asked.
		/// </summary>
		/// <returns>The latch afterwards, or the latch unchanged when the key is unwritable or
		/// already held.</returns>
		public static string Latch(string Latch, string Key)
		{
			string folded = Fold(Key);
			if (folded == null || folded.IndexOf(LatchSeparator) >= 0 || Latched(Latch, folded))
			{
				return Latch ?? "";
			}
			return string.IsNullOrEmpty(Latch) ? folded : (Latch + LatchSeparator + folded);
		}

		// --- The one sanctioned draw (DIVERSITY §3.4 hard rule 4's own exception; §3.7) --------

		private const int ConfessionRulesVersion = 1;

		private static readonly KernelSeed128 ConfessionSeed = default(KernelSeed128);

		private const string ConfessionEventStreamId = "taf:lab:confession:v1";

		private const uint ConfessionEventKind = 1u;

		private const uint ConfessionDrawIndex = 0u;

		/// <summary>
		/// Which limb the Chimeric Confession comes back with.
		/// <para>
		/// <b>The only die this system rolls, and it is disclosed before it is thrown.</b> Every
		/// other procedure is what the slate said, because a thing that cost a season and a fortune
		/// may not roll dice (DIVERSITY &sect;3.1's rejection of golem randomness). This one is the
		/// exception the doctrine names by hand: the confession is confessedly a gamble and is
		/// priced as one, and the slate says so in the founder's own language before they commit.
		/// </para>
		/// <para>
		/// Drawn through the settlement kernel rather than <c>Stat.Random</c>, so the same
		/// confession on the same save is the same limb after a reload &mdash; a gamble the founder
		/// takes once, not a gamble the save file re-takes every time it is opened.
		/// </para>
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id
		/// (<c>KingdomChronicle.SettlementId</c>).</param>
		/// <param name="Ordinal">The tick the confession was commissioned at.</param>
		/// <param name="CandidateCount">How many limbs the game's own chimera weighting offered.</param>
		/// <returns>An index into the candidates, or -1 when there was nothing to choose from.
		/// Falls back to the first candidate if the kernel refuses, which is a limb rather than a
		/// crash.</returns>
		public static int ChooseChimericSlot(string SettlementId, ulong Ordinal, int CandidateCount)
		{
			if (CandidateCount <= 0)
			{
				return -1;
			}
			if (CandidateCount == 1)
			{
				return 0;
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(ConfessionRulesVersion, SettlementId, ConfessionEventStreamId,
				ConfessionEventKind, Ordinal, out key, out fault))
			{
				return 0;
			}
			ulong value;
			if (!CounterRandom.TryDrawBelow(ConfessionSeed, key, ConfessionDrawIndex, (ulong)CandidateCount, out value, out fault))
			{
				return 0;
			}
			return (int)value;
		}

		// --- Parsing (STANDARDS §6) ------------------------------------------------------------

		/// <summary>
		/// Reads one <c>&lt;procedure&gt;</c> element into a record.
		/// <para>
		/// A procedure is REFUSED whole on a fault, the way a research node is and unlike a building
		/// gate, and for the same reason: a gate restricts a design that exists either way, while a
		/// procedure whose slots or class cannot be read is a thing that would open a founder's body
		/// on a guess.
		/// </para>
		/// <para>
		/// The one fault that is a rule rather than a typo is the blocklist (Addendum 22 D1): a
		/// record granting a self-replication part, <c>Invisibility</c>, <c>WallWalker</c>,
		/// <c>Metamorphosis</c> or <c>OldElectricalGeneration</c> is refused by file and key rather
		/// than left as a convention somebody can forget. Boundary powers arrive as named
		/// procedures, one ruling each, or they do not arrive.
		/// </para>
		/// </summary>
		/// <param name="Error">Null on success; one sentence naming the key and the fault otherwise.</param>
		public static bool TryParseProcedureAttributes(string Key, string DisplayName, string Class, string Grants,
			string Slots, string SlotCategories, string Source, string Attach, string MinRung,
			string Cost, string Bits, string StaffDays, string Preserved, string Creeds, string Knowledge,
			string Magnitude, out LabProcedure Procedure, out string Error)
		{
			Procedure = null;
			string key = Fold(Key);
			if (key == null)
			{
				Error = "a <procedure> element carries no Key.";
				return false;
			}
			string grants = Trimmed(Grants);
			if (grants == null)
			{
				Error = "procedure " + key + ": Grants names no part class. A procedure grants a CLASS, never a creature.";
				return false;
			}
			if (Blocked(grants))
			{
				Error = "procedure " + key + ": Grants \"" + grants
					+ "\", which the blocklist holds (Addendum 22 D1). Boundary powers arrive as named procedures, one ruling each, or not at all.";
				return false;
			}
			LabClass cls;
			if (!TryParseClass(Class, out cls))
			{
				Error = "procedure " + key + ": Class \"" + Class + "\" is not I, II, III or IV.";
				return false;
			}
			LabSource source = LabSource.Part;
			if (!string.IsNullOrEmpty(Source) && !TryParseSource(Source, out source))
			{
				Error = "procedure " + key + ": Source \"" + Source + "\" is not part, limb or mutation.";
				return false;
			}
			LabAttach attach;
			if (!TryParseAttach(Attach, out attach))
			{
				Error = "procedure " + key + ": Attach \"" + Attach
					+ "\" is not body or weapon. A rider that only ever fires on a weapon is inert on a torso, so every record must say which it is.";
				return false;
			}
			string slots = Trimmed(Slots);
			if (slots == null)
			{
				Error = "procedure " + key + ": Slots names nowhere on a body to put it.";
				return false;
			}
			int rung = RungForClass(cls);
			if (!string.IsNullOrEmpty(MinRung) && (!int.TryParse(MinRung.Trim(), out rung) || rung < RungSlab || rung > RungTheatre))
			{
				Error = "procedure " + key + ": MinRung \"" + MinRung + "\" is not one of " + RungSlab + " to " + RungTheatre + ".";
				return false;
			}
			int cost = 0;
			if (!string.IsNullOrEmpty(Cost) && (!int.TryParse(Cost.Trim(), out cost) || cost < 0))
			{
				Error = "procedure " + key + ": Cost \"" + Cost + "\" is not a count of drams.";
				return false;
			}
			int staffDays = 1;
			if (!string.IsNullOrEmpty(StaffDays) && (!int.TryParse(StaffDays.Trim(), out staffDays) || staffDays < 1))
			{
				Error = "procedure " + key + ": StaffDays \"" + StaffDays + "\" is not a count of days of work.";
				return false;
			}
			int preserved = 1;
			if (!string.IsNullOrEmpty(Preserved) && (!int.TryParse(Preserved.Trim(), out preserved) || preserved < 0))
			{
				Error = "procedure " + key + ": Preserved \"" + Preserved + "\" is not a count of kept parts.";
				return false;
			}
			string magnitudeField;
			int low;
			int high;
			string bandError;
			if (!TryParseMagnitude(Magnitude, out magnitudeField, out low, out high, out bandError))
			{
				Error = "procedure " + key + ": Magnitude " + bandError;
				return false;
			}
			Procedure = new LabProcedure
			{
				Key = key,
				DisplayName = Trimmed(DisplayName),
				Class = cls,
				Grants = grants,
				Slots = slots,
				SlotCategories = Trimmed(SlotCategories),
				Source = source,
				Attach = attach,
				MinRung = rung,
				Cost = cost,
				Bits = Trimmed(Bits),
				StaffDays = staffDays,
				Preserved = preserved,
				Creeds = Trimmed(Creeds),
				Knowledge = Trimmed(Knowledge),
				Magnitude = Trimmed(Magnitude)
			};
			Error = null;
			return true;
		}

		/// <summary>The class ladder in the vocabulary the design doc writes it in, and in the
		/// numbers a hand-written file might use instead.</summary>
		public static bool TryParseClass(string Source, out LabClass Class)
		{
			Class = LabClass.Rider;
			switch (Fold(Source))
			{
			case "i":
			case "1":
			case "rider":
				Class = LabClass.Rider;
				return true;
			case "ii":
			case "2":
			case "defence":
			case "defense":
				Class = LabClass.Defence;
				return true;
			case "iii":
			case "3":
			case "limb":
				Class = LabClass.Limb;
				return true;
			case "iv":
			case "4":
			case "named":
				Class = LabClass.Named;
				return true;
			default:
				return false;
			}
		}

		public static bool TryParseSource(string Source, out LabSource Kind)
		{
			Kind = LabSource.Part;
			switch (Fold(Source))
			{
			case "part":
				Kind = LabSource.Part;
				return true;
			case "limb":
				Kind = LabSource.Limb;
				return true;
			case "mutation":
				Kind = LabSource.Mutation;
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// The attach bit. Absent reads as <see cref="LabAttach.Body"/>, which is the safe default
		/// only because every record this mod ships states it outright and the validator says so
		/// about every record that does not.
		/// </summary>
		public static bool TryParseAttach(string Source, out LabAttach Attach)
		{
			Attach = LabAttach.Body;
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return true;
			}
			switch (Fold(Source))
			{
			case "body":
			case "bearer":
				Attach = LabAttach.Body;
				return true;
			case "weapon":
			case "natural":
				Attach = LabAttach.Weapon;
				return true;
			default:
				return false;
			}
		}

		// --- The blocklist (Addendum 22 D1) ----------------------------------------------------

		/// <summary>
		/// Part and mutation classes no derived record may ever grant, whatever any file says.
		/// <para>
		/// The golem quest's own list (<c>D/XRL/World/Quests/GolemQuest/GolemAtzmusSelection.cs:21</c>)
		/// plus the precedent whitelist's <c>[Spicy]</c> block, whose own header calls it
		/// experimental and save-breaking, plus every self-replication class the census turned up.
		/// It is enforced at LOAD rather than at commission, so a third party's file that names one
		/// fails loudly on the day it ships rather than quietly on the day somebody clicks.
		/// </para>
		/// </summary>
		public static readonly string[] Blocklist = new string[19]
		{
			"Invisibility", "WallWalker", "Metamorphosis", "OldElectricalGeneration",
			"Reconstitution", "SplitOnDeath", "Cloneling", "Mimic", "MimicProperties",
			"Engulfing", "EngulfingDamage", "FugueOnStep", "StunningForceOnJump", "Twinner", "Triner",
			"Spawner", "Breeder", "CloneOnHit", "FabricateFromSelf"
		};

		/// <summary>Whether a class is on the blocklist. Case-insensitive, because a file that
		/// spells it in lower case is naming the same class.</summary>
		public static bool Blocked(string ClassName)
		{
			if (string.IsNullOrEmpty(ClassName))
			{
				return false;
			}
			string wanted = Fold(ClassName);
			for (int i = 0; i < Blocklist.Length; i++)
			{
				if (Fold(Blocklist[i]) == wanted)
				{
					return true;
				}
			}
			return false;
		}

		// --- Registry validation (STANDARDS §6, §9) --------------------------------------------

		/// <summary>
		/// What is wrong with a merged registry, said once at load. Nothing is unregistered: a
		/// record that is wrong about itself stays in the registry and is offered, which is the only
		/// shape a check on third-party content can honestly take. The checks are the ones no single
		/// record can see.
		/// </summary>
		/// <returns>One sentence per finding, in registry order; never null.</returns>
		public static List<string> Validate(IList<LabProcedure> Procedures)
		{
			List<string> findings = new List<string>();
			if (Procedures == null)
			{
				return findings;
			}
			for (int i = 0; i < Procedures.Count; i++)
			{
				LabProcedure procedure = Procedures[i];
				if (procedure == null || procedure.Key == null)
				{
					continue;
				}
				// Class IV is deliberately exempt. A named procedure's gate is AUTHORED, one ruling
				// each (DIVERSITY §3.7), and the four do not all sit at the same height: the Lantern
				// Rib is hall work at rung 2 because it does not change what a founder IS, only what
				// they are carrying and where. Flagging that would be flagging the design.
				if (procedure.Class != LabClass.Named && procedure.MinRung < RungForClass(procedure.Class))
				{
					findings.Add("procedure " + procedure.Key + " is Class " + Roman(procedure.Class) + " and sits at rung "
						+ procedure.MinRung + ", below the rung that class of work is done at.");
				}
				if (procedure.Source == LabSource.Limb && procedure.Class != LabClass.Limb && !procedure.IsNamed)
				{
					findings.Add("procedure " + procedure.Key + " takes a severed limb and is not Class III. A limb is grafted at the theatre or not at all.");
				}
				if (procedure.Attach == LabAttach.Weapon && procedure.Source != LabSource.Part)
				{
					findings.Add("procedure " + procedure.Key + " attaches to a natural weapon and does not grant a part. Only a part rides a weapon.");
				}
				if (procedure.Magnitude != null && procedure.Source != LabSource.Part)
				{
					findings.Add("procedure " + procedure.Key + " names a Magnitude band and does not grant a part. There is no field on a limb to band.");
				}
				for (int j = i + 1; j < Procedures.Count; j++)
				{
					if (Procedures[j] == null || Procedures[j].Key == null || Procedures[j].Grants != procedure.Grants)
					{
						continue;
					}
					// Two records over one class is the QB-10 shape and is lawful, but only when
					// something tells them apart: without bands on both, the cheaper one is simply
					// the better buy and the dearer one is a record nobody will ever pick.
					if (procedure.Magnitude == null || Procedures[j].Magnitude == null)
					{
						findings.Add("procedures " + procedure.Key + " and " + Procedures[j].Key + " both grant " + procedure.Grants
							+ " and at least one names no Magnitude band, so nothing tells the two apart at the slate.");
					}
				}
			}
			return findings;
		}

		/// <summary>The class as the design doc and the slate both write it.</summary>
		public static string Roman(LabClass Class)
		{
			switch (Class)
			{
			case LabClass.Rider:
				return "I";
			case LabClass.Defence:
				return "II";
			case LabClass.Limb:
				return "III";
			default:
				return "IV";
			}
		}

		// --- The words (STANDARDS 7b: every refusal names the fix) -----------------------------

		/// <summary>
		/// Why the hall will not do a thing, in the founder's own language.
		/// <para>
		/// Empty for <see cref="LabVerdict.Allowed"/>, and empty for
		/// <see cref="LabVerdict.RefusedUndiscovered"/> &mdash; the second deliberately, because
		/// telling a founder that something they have never heard of is refused would be telling
		/// them it exists, which is the one thing the visibility law forbids.
		/// </para>
		/// </summary>
		/// <param name="Verdict">What <see cref="Judge"/> answered.</param>
		/// <param name="Procedure">The record, for the things a refusal may name.</param>
		public static string RefusalLine(LabVerdict Verdict, LabProcedure Procedure)
		{
			string named = (Procedure == null) ? "it" : Procedure.Named;
			switch (Verdict)
			{
			case LabVerdict.RefusedNoSlot:
				return "There is nowhere on you to put " + named + ". A body is a finite thing, and yours has no "
					+ FirstSlot(Procedure) + ".";
			case LabVerdict.RefusedSlotTaken:
				return "Every place " + named + " could go is already spoken for. Have something taken off, and the hall can put this on.";
			case LabVerdict.RefusedCategory:
				return "You are not made of the kind of thing " + named + " is grafted to. The hall can open a body; it cannot change what a body is.";
			case LabVerdict.RefusedRung:
				return "The hall here is not built high enough for " + named + ". That is "
					+ RungName(Procedure == null ? RungTheatre : Procedure.MinRung) + " work.";
			case LabVerdict.RefusedNoWeapon:
				return "There is nothing on you there for " + named
					+ " to ride. It lives in a claw or a sting, not in the flesh behind one, so it wants a limb that already bites.";
			case LabVerdict.RefusedUnkept:
				return "The hall will not open a body for a thing that was not kept. The vat-house has no "
					+ ((Procedure == null) ? "source" : SourceWord(Procedure)) + " for " + named + ".";
			case LabVerdict.RefusedOnceEver:
				return "That was done to you once. It is not the kind of thing that is done twice.";
			case LabVerdict.RefusedMagnitude:
				return "What the vat-house is keeping is of the right kind and the wrong measure for " + named + ".";
			default:
				return "";
			}
		}

		/// <summary>The rung a founder would name it by.</summary>
		public static string RungName(int Rung)
		{
			switch (Rung)
			{
			case RungSlab:
				return "the slab's";
			case RungVat:
				return "the vat-house's";
			case RungHall:
				return "the grafting hall's";
			default:
				return "the chimeric theatre's";
			}
		}

		/// <summary>What the vat-house would be keeping, said as a founder would say it.</summary>
		public static string SourceWord(LabProcedure Procedure)
		{
			if (Procedure == null)
			{
				return "source";
			}
			switch (Procedure.Source)
			{
			case LabSource.Limb:
				return "kept limb";
			case LabSource.Mutation:
				return "kept gland";
			default:
				return "kept part";
			}
		}

		private static string FirstSlot(LabProcedure Procedure)
		{
			List<string> slots = SlotTypes(Procedure);
			return (slots.Count == 0) ? "such place" : slots[0];
		}

		// --- Small shared helpers ----------------------------------------------------------------

		/// <summary>A comma list, folded and trimmed, empties dropped. Never null.</summary>
		public static List<string> Split(string Source)
		{
			List<string> parts = new List<string>();
			if (string.IsNullOrEmpty(Source))
			{
				return parts;
			}
			string[] raw = Source.Split(',');
			for (int i = 0; i < raw.Length; i++)
			{
				string one = Fold(raw[i]);
				if (one != null && !parts.Contains(one))
				{
					parts.Add(one);
				}
			}
			return parts;
		}

		/// <summary>A comma list, trimmed only, empties dropped. Never null. See
		/// <see cref="SlotCategoryNames"/> for why the un-folded variant exists.</summary>
		public static List<string> SplitTrimmed(string Source)
		{
			List<string> parts = new List<string>();
			if (string.IsNullOrEmpty(Source))
			{
				return parts;
			}
			string[] raw = Source.Split(',');
			for (int i = 0; i < raw.Length; i++)
			{
				string one = Trimmed(raw[i]);
				if (one != null && !parts.Contains(one))
				{
					parts.Add(one);
				}
			}
			return parts;
		}

		private static int Clamp(int Value, int Low, int High)
		{
			if (Value < Low)
			{
				return Low;
			}
			return (Value > High) ? High : Value;
		}

		private static string Fold(string Value)
		{
			if (Value == null)
			{
				return null;
			}
			string folded = Value.Trim().ToLowerInvariant();
			return (folded.Length == 0) ? null : folded;
		}

		private static string Trimmed(string Value)
		{
			if (Value == null)
			{
				return null;
			}
			string trimmed = Value.Trim();
			return (trimmed.Length == 0) ? null : trimmed;
		}
	}
}
