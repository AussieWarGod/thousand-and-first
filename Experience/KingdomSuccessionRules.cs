using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Everything Kingdom Mode can work out without the engine: how long the word takes to reach
	/// the seat, which settler the realm's own law raises, what the realm thinks of them on the day
	/// they take the charter, which half of the founder's journal dies with the founder, and when a
	/// line has simply run out.
	/// <para>
	/// <b>The mode is not a difficulty setting; it is a claim about who the run belongs to.</b>
	/// Classic says the run is the character's and ends with them. Roleplay says the death was a
	/// mistake and rewinds it. Kingdom Mode says the death was real and the kingdom was never the
	/// character's to begin with &mdash; so the person is gone, permanently and witnessed, and the
	/// realm goes on to raise somebody else. Addendum 21 and its extension; Addendum 22 C1-C13.
	/// </para>
	/// <para>
	/// <b>The honesty rule (Addendum 21, binding) is what every number here serves.</b> Becoming a
	/// citizen is as if a new game began as that citizen: their body, their attributes, their
	/// knowledge, and their standing &mdash; never the founder's. Which is why the accession regard
	/// below is derived from the heir's own life and floored well short of trust, and why the
	/// forget table exempts exactly two kinds and no others.
	/// </para>
	/// <para>
	/// No <c>XRL</c> usings, by the same law every other <c>*Rules</c> file in this mod keeps
	/// (STANDARDS &sect;2). The engine half is <c>KingdomSuccession</c>.
	/// </para>
	/// </summary>
	public static partial class KingdomSuccessionRules
	{
		internal const int MaxDeathTokenChars = 512;
		/// <summary>The mourning rite is not an ordinary four-person happening. Every living
		/// resident whose exact bound body already stands in the rite zone is evidence, up to the
		/// city's legal population envelope.</summary>
		internal const int MaxRiteAttendees = KingdomRules.MaxPopulation;

		/// <summary>Worst-case canonical base64 for sixty admitted rows at the per-field bounds
		/// enforced by <see cref="ValidRiteAttendee"/>. This state normally exists for one
		/// synchronous callback, but injected/cold-load checkpoints must retain it whole.</summary>
		internal const int MaxRiteManifestChars = 1024 * 1024;

		/// <summary>Only forward, adjacent rite checkpoints are legal. Repeating the current
		/// checkpoint is the exact-once retry; skipping physical evidence is never legal.</summary>
		public static bool MayAdvanceRite(MourningRiteStage Current, MourningRiteStage Next)
		{
			if (!Enum.IsDefined(typeof(MourningRiteStage), Current)
				|| !Enum.IsDefined(typeof(MourningRiteStage), Next))
			{
				return false;
			}
			return Next == Current || (int)Next == (int)Current + 1;
		}

		/// <summary>Check-before-mint for the in-run founder shrine.</summary>
		public static FounderShrinePlacementVerdict JudgeFounderShrinePlacement(
			bool HasReceipt, bool ExactObjectMatches, bool CellPassable, int CellObjectCount)
		{
			if (HasReceipt)
			{
				return ExactObjectMatches ? FounderShrinePlacementVerdict.AdoptExact
					: FounderShrinePlacementVerdict.Refuse;
			}
			return CellPassable && CellObjectCount == 0
				? FounderShrinePlacementVerdict.Create : FounderShrinePlacementVerdict.Refuse;
		}

		/// <summary>Canonical, bounded receipt for the real bodies assigned to the procession.
		/// Text fields are base64 so names, object ids and zone ids cannot alter row topology.</summary>
		public static string EncodeRiteManifest(KingdomRiteAttendee[] Attendees)
		{
			if (Attendees == null || Attendees.Length == 0
				|| Attendees.Length > MaxRiteAttendees)
			{
				return "";
			}
			StringBuilder encoded = new StringBuilder();
			HashSet<int> residentIds = new HashSet<int>();
			HashSet<string> objectIds = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Attendees.Length; i++)
			{
				KingdomRiteAttendee row = Attendees[i];
				if (!ValidRiteAttendee(row) || !residentIds.Add(row.ResidentId)
					|| !objectIds.Add(row.ObjectId))
				{
					return "";
				}
				if (i > 0) encoded.Append('\n');
				encoded.Append("v1|").Append(row.ResidentId.ToString(CultureInfo.InvariantCulture))
					.Append('|').Append(ToBase64(row.ObjectId))
					.Append('|').Append(ToBase64(row.Name))
					.Append('|').Append(ToBase64(row.ZoneId))
					.Append('|').Append(row.OriginalX.ToString(CultureInfo.InvariantCulture))
					.Append('|').Append(row.OriginalY.ToString(CultureInfo.InvariantCulture))
					.Append('|').Append(ToBase64(row.Post))
					.Append('|').Append(ToBase64(row.Home))
					.Append('|').Append(row.RiteX.ToString(CultureInfo.InvariantCulture))
					.Append('|').Append(row.RiteY.ToString(CultureInfo.InvariantCulture));
				if (encoded.Length > MaxRiteManifestChars) return "";
			}
			return encoded.ToString();
		}

		public static bool TryDecodeRiteManifest(string Manifest,
			out KingdomRiteAttendee[] Attendees)
		{
			Attendees = Array.Empty<KingdomRiteAttendee>();
			if (string.IsNullOrEmpty(Manifest) || Manifest.Length > MaxRiteManifestChars)
			{
				return false;
			}
			string[] lines = Manifest.Split('\n');
			if (lines.Length == 0 || lines.Length > MaxRiteAttendees) return false;
			KingdomRiteAttendee[] rows = new KingdomRiteAttendee[lines.Length];
			HashSet<int> residentIds = new HashSet<int>();
			HashSet<string> objectIds = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < lines.Length; i++)
			{
				string[] p = lines[i].Split('|');
				int id, ox, oy, rx, ry;
				string objectId, name, zone, post, home;
				if (p.Length != 11 || p[0] != "v1"
					|| !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out id)
					|| !TryFromBase64(p[2], out objectId)
					|| !TryFromBase64(p[3], out name)
					|| !TryFromBase64(p[4], out zone)
					|| !int.TryParse(p[5], NumberStyles.None, CultureInfo.InvariantCulture, out ox)
					|| !int.TryParse(p[6], NumberStyles.None, CultureInfo.InvariantCulture, out oy)
					|| !TryFromBase64(p[7], out post)
					|| !TryFromBase64(p[8], out home)
					|| !int.TryParse(p[9], NumberStyles.None, CultureInfo.InvariantCulture, out rx)
					|| !int.TryParse(p[10], NumberStyles.None, CultureInfo.InvariantCulture, out ry))
				{
					return false;
				}
				rows[i] = new KingdomRiteAttendee(id, objectId, name, zone, ox, oy,
					post, home, rx, ry);
				if (!ValidRiteAttendee(rows[i]) || !residentIds.Add(id)
					|| !objectIds.Add(objectId)) return false;
			}
			if (!string.Equals(EncodeRiteManifest(rows), Manifest, StringComparison.Ordinal))
			{
				return false;
			}
			Attendees = rows;
			return true;
		}

		private static bool ValidRiteAttendee(KingdomRiteAttendee Row)
		{
			return Row.ResidentId > 0 && !string.IsNullOrEmpty(Row.ObjectId)
				&& !string.IsNullOrEmpty(Row.Name) && !string.IsNullOrEmpty(Row.ZoneId)
				&& Row.OriginalX >= 0 && Row.OriginalX <= 4096
				&& Row.OriginalY >= 0 && Row.OriginalY <= 4096
				&& Row.RiteX >= 0 && Row.RiteX <= 4096
				&& Row.RiteY >= 0 && Row.RiteY <= 4096
				&& Row.ObjectId.Length <= 512 && Row.Name.Length <= 512
				&& Row.ZoneId.Length <= 1024 && Row.Post.Length <= 1024
				&& Row.Home.Length <= 1024;
		}

		private static string ToBase64(string Text)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? ""));
		}

		private static bool TryFromBase64(string Encoded, out string Text)
		{
			Text = "";
			try
			{
				byte[] bytes = Convert.FromBase64String(Encoded ?? "");
				Text = new UTF8Encoding(false, true).GetString(bytes);
				return Convert.ToBase64String(bytes) == Encoded;
			}
			catch
			{
				return false;
			}
		}
	}
}
