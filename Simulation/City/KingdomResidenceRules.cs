using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal static class KingdomResidenceRules
	{
		internal const int MaxWireLength = 2048;
		internal const int MaxTags = 32;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static KingdomResidentRow ObserveHomeLoss(KingdomResidentRow row, long atTick)
		{
			if (row.RoofBrink.Stands || string.IsNullOrEmpty(row.Name)) return row;
			return row.WithBrink(BrinkKind.Roof, new KingdomBrinkWindow(true,
				Math.Max(0L, atTick), KingdomBrinkRules.Unwarned), null, 0);
		}

		internal static bool TryEncode(KingdomResidence Home, out string Wire)
		{
			Wire = null;
			if (!Text(Home.ZoneId, 256) || !Text(Home.PlotId, 512) || !Text(Home.BedId, 256)
				|| Home.Creed != null && !Text(Home.Creed, 128)
				|| (Home.ZoneId.Length == 0) != (Home.PlotId.Length == 0)
				|| Home.ZoneId.Length == 0 && Home.BedId.Length != 0
				|| !Tags(Home.Needs, out string needs) || !Tags(Home.Refuses, out string refuses)
				|| !Tags(Home.SelfTags, out string selfTags)) return false;
			string value = string.Join("\n", "taf-residence-v1", Encode(Home.ZoneId), Encode(Home.PlotId),
				Encode(Home.BedId), Home.Creed == null ? "-" : Encode(Home.Creed), needs, refuses, selfTags);
			if (value.Length > MaxWireLength) return false;
			Wire = value;
			return true;
		}

		internal static bool TryDecode(string Wire, out KingdomResidence Home)
		{
			Home = default;
			if (string.IsNullOrEmpty(Wire) || Wire.Length > MaxWireLength) return false;
			string[] fields = Wire.Split('\n');
			if (fields.Length != 8 || fields[0] != "taf-residence-v1") return false;
			try
			{
				string zone = Decode(fields[1]), plot = Decode(fields[2]), bed = Decode(fields[3]);
				string creed = fields[4] == "-" ? null : Decode(fields[4]);
				var home = new KingdomResidence(zone, plot, bed, creed,
					ReadTags(fields[5]), ReadTags(fields[6]), ReadTags(fields[7]));
				if (!TryEncode(home, out string canonical) || canonical != Wire) return false;
				Home = home;
				return true;
			}
			catch (FormatException) { return false; }
			catch (DecoderFallbackException) { return false; }
		}

		internal static bool SameHome(KingdomResidence Home, string Zone, string Plot)
			=> Home.HasHome && string.Equals(Home.ZoneId, Zone, StringComparison.Ordinal)
				&& string.Equals(Home.PlotId, Plot, StringComparison.Ordinal);

		internal static bool SameBed(KingdomResidence A, KingdomResidence B)
			=> !string.IsNullOrEmpty(A.BedId) && SameHome(A, B.ZoneId, B.PlotId)
				&& string.Equals(A.BedId, B.BedId, StringComparison.Ordinal);

		internal static bool ValidRows(KingdomResidentRow[] Rows)
		{
			if (Rows == null) return true;
			HashSet<string> beds = null;
			for (int i = 0; i < Rows.Length; i++)
			{
				if (string.IsNullOrEmpty(Rows[i].Residence)) continue;
				if (!TryDecode(Rows[i].Residence, out KingdomResidence home)) return false;
				if (KingdomResidentRules.OnTheRoll(Rows[i]) && !TryReserveBed(home, ref beds)) return false;
			}
			return true;
		}

		internal static bool TryReserveBed(KingdomResidence Home, ref HashSet<string> Beds)
		{
			if (Home.BedId.Length == 0) return true;
			if (Beds == null) Beds = new HashSet<string>(StringComparer.Ordinal);
			// Valid identity fields cannot contain line separators.
			return Beds.Add(Home.ZoneId + "\n" + Home.PlotId + "\n" + Home.BedId);
		}

		private static bool Tags(IReadOnlyList<string> Values, out string Encoded)
		{
			Encoded = null;
			if (Values == null || Values.Count > MaxTags) return false;
			var tags = new List<string>();
			for (int i = 0; i < Values.Count; i++)
			{
				if (!Text(Values[i], 128) || Values[i].Length == 0) return false;
				tags.Add(Values[i]);
			}
			tags.Sort(StringComparer.Ordinal);
			for (int i = 1; i < tags.Count; i++) if (tags[i - 1] == tags[i]) return false;
			for (int i = 0; i < tags.Count; i++) tags[i] = Encode(tags[i]);
			Encoded = string.Join(",", tags);
			return true;
		}

		private static IReadOnlyList<string> ReadTags(string Value)
		{
			if (Value.Length == 0) return Array.AsReadOnly(new string[0]);
			string[] tags = Value.Split(',');
			if (tags.Length > MaxTags) return null;
			for (int i = 0; i < tags.Length; i++) tags[i] = Decode(tags[i]);
			return Array.AsReadOnly(tags);
		}

		private static bool Text(string Value, int Limit)
		{
			if (Value == null || Value.Length > Limit) return false;
			foreach (char c in Value) if (char.IsControl(c) || char.IsSurrogate(c)) return false;
			return true;
		}

		private static string Encode(string Value) => Convert.ToBase64String(Utf8.GetBytes(Value));
		private static string Decode(string Value) => Utf8.GetString(Convert.FromBase64String(Value));
	}
}
