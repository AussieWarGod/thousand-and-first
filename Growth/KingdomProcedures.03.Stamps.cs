using System;
using System.Collections.Generic;
using System.Reflection;

using Qud.API;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

namespace ThousandAndFirst
{
	public static partial class KingdomProcedures
	{
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
	}
}
