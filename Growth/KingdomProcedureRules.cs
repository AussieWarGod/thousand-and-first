using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
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
	public static partial class KingdomProcedureRules
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

	}
}
