using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private static bool TryParseVerdict(string Parameter, out string Verdict,
			out string Screenshot, out string Note, out string Failure)
		{
			Verdict = null;
			Screenshot = null;
			Note = null;
			Failure = null;
			string[] parts = (Parameter ?? "").Split(new char[] { '|' }, 3);
			string verdict = parts.Length > 0 ? parts[0].Trim().ToLowerInvariant() : "";
			string screenshot = parts.Length > 1 ? parts[1].Trim() : "";
			string note = parts.Length > 2 ? parts[2].Trim() : null;
			if (verdict != "pass" && verdict != "fail")
				return Fail("Use pass or fail: kingdom:archverdict pass|SCREENSHOT|NOTE", out Failure);
			if (screenshot.Length < 1 || screenshot.Length > MaxScreenshotChars
				|| screenshot.IndexOf('\n') >= 0 || screenshot.IndexOf('\r') >= 0)
				return Fail("Name the captured screenshot in 1–" + MaxScreenshotChars
					+ " single-line characters.", out Failure);
			if (note != null && (note.Length > MaxNoteChars || note.IndexOf('\n') >= 0
				|| note.IndexOf('\r') >= 0))
				return Fail("Keep the verdict note to " + MaxNoteChars + " single-line characters.",
					out Failure);
			Verdict = verdict;
			Screenshot = screenshot;
			Note = string.IsNullOrEmpty(note) ? null : note;
			return true;
		}

		private static string ReceiptFor(GalleryCase Case, int Total, string SnapshotHash)
		{
			string payload = GallerySchema.ToString(CultureInfo.InvariantCulture) + "\n"
				+ ModVersion + "\n" + XRLGame.CoreVersion + "\n" + Case.Number.ToString(
					CultureInfo.InvariantCulture) + "/" + Total.ToString(CultureInfo.InvariantCulture)
				+ "\n" + Case.Key + "\n" + SnapshotHash;
			return "ag1-" + Hash(payload).Substring(0, 24);
		}

		private static HashSet<int> ConnectionCells(Zone Zone)
		{
			HashSet<int> result = new HashSet<int>();
			foreach (ZoneConnection connection in Zone.EnumerateConnections())
				AddConnection(result, Zone, connection);
			if (Zone.ZoneConnectionCache != null)
				for (int i = 0; i < Zone.ZoneConnectionCache.Count; i++)
					AddConnection(result, Zone, Zone.ZoneConnectionCache[i]);
			return result;
		}

		private static void AddConnection(HashSet<int> Into, Zone Zone, ZoneConnection Connection)
		{
			if (Connection != null && Connection.X >= 0 && Connection.X < Zone.Width
				&& Connection.Y >= 0 && Connection.Y < Zone.Height)
				Into.Add(Connection.Y * Zone.Width + Connection.X);
		}

		private static string Hash(string Value)
		{
			byte[] digest;
			using (SHA256 sha = SHA256.Create())
				digest = sha.ComputeHash(Encoding.UTF8.GetBytes(Value ?? ""));
			StringBuilder text = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2",
				CultureInfo.InvariantCulture));
			return text.ToString();
		}

		private static string Bounded(string Text, int Maximum)
		{
			if (string.IsNullOrEmpty(Text) || Text.Length <= Maximum) return Text;
			return Text.Substring(0, Maximum);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
