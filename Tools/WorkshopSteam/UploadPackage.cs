using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using ThousandAndFirst.Tools;

namespace ThousandAndFirst.WorkshopSteam
{
	/// <summary>Read-only byte leases and fresh inventory checks, not release authorization.
	/// The caller must revalidate immediately before submission and after completion.</summary>
	public sealed partial class UploadPackage : IDisposable
	{
		private const long Maximum = 512L * 1024 * 1024;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
		private readonly List<IDisposable> leases = new List<IDisposable>();
		private readonly Dictionary<string, SafeFileHandle> directories = new Dictionary<string, SafeFileHandle>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
		private HashSet<string> treeDirectories;
		private Entry planFile, receiptFile;
		private string content;
		private bool disposed, refused;
		public UploadRequest Request { get; private set; }
		public string PlanSHA { get; private set; }
		public string ReceiptSHA { get; private set; }
		private UploadPackage() { }

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern SafeFileHandle CreateFileW(string path, uint access, uint share,
			IntPtr security, uint creation, uint flags, IntPtr template);

		public static UploadPackage Open(string planPath, string expectedPlanSHA, string expectedItem, string changeNote)
		{
			Require(Environment.OSVersion.Platform == PlatformID.Win32NT, "Windows file leases required");
			UploadPackage package = new UploadPackage();
			try { package.Load(planPath, expectedPlanSHA, expectedItem, changeNote); return package; }
			catch (Exception error)
			{
				try { package.Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
				throw;
			}
		}

		private void Load(string path, string hash, string expectedItem, string note)
		{
			Require(HashShape(hash), "invalid expected plan hash");
			ulong item = Item(expectedItem);
			planFile = LeaseFile(Native(path, false), 4 * 1024 * 1024, hash);
			PlanSHA = hash;
			using (JsonDocument document = Json(planFile))
			{
				JsonElement p = document.RootElement;
				Fields(p, "schema planOnly appId targetItem mode manifestId version title description tags qudVisibility steamVisibility contentPath previewPath receiptPath receiptSHA manifestSHA workshopSHA files");
				Require(S(p, "schema") == "taf-workshop-upload-plan-v1" && p.GetProperty("planOnly").ValueKind == JsonValueKind.True
					&& p.GetProperty("appId").GetInt32() == 333640 && S(p, "targetItem") == expectedItem, "plan identity mismatch");
				string mode = S(p, "mode"), version = S(p, "version"), visibility = S(p, "qudVisibility");
				Require((mode == "test" && item != 3794797472UL && visibility == "0" && p.GetProperty("steamVisibility").GetInt32() == 2)
					|| (mode == "alpha" && item == 3794797472UL && visibility == "2" && p.GetProperty("steamVisibility").GetInt32() == 0), "wrong Workshop lane");
				Version parsed;
				Require(S(p, "manifestId") == "r_ThousandAndFirst" && Version.TryParse(version, out parsed)
					&& parsed.Major == 0 && parsed.Minor == 3 && parsed.Build >= 0 && parsed.Revision == -1 && parsed.ToString() == version, "invalid manifest version or identity");
				content = Native(S(p, "contentPath"), true);
				string preview = Native(S(p, "previewPath"), true), receipt = Native(S(p, "receiptPath"), true);
				Require(preview == Path.Combine(content, "preview.png") && !Inside(receipt, content) && receipt != content, "unsafe preview or receipt path");
				ReceiptSHA = S(p, "receiptSHA"); receiptFile = LeaseFile(receipt, 1024 * 1024, ReceiptSHA);
				string title = S(p, "title"), description = S(p, "description");
				Require(Text(title, 128, false) && Text(description, 7999, true) && Text(note, 8000, true)
					&& note == note.Trim(), "invalid upload prose");
				List<string> tags = new List<string>(); HashSet<string> uniqueTags = new HashSet<string>(StringComparer.Ordinal);
				foreach (JsonElement tag in p.GetProperty("tags").EnumerateArray())
				{
					string value = tag.GetString();
					Require(tags.Count < 64 && Text(value, 255, false) && value.IndexOf(',') < 0 && uniqueTags.Add(value), "invalid tags"); tags.Add(value);
				}
				Require(tags.Count > 0 && Utf8.GetByteCount(string.Join(",", tags)) < 1025, "tags exceed bound");
				long total = 0; string previous = null;
				foreach (JsonElement row in p.GetProperty("files").EnumerateArray())
				{
					Fields(row, "path sha256 size"); string name = Name(S(row, "path"));
					long size = row.GetProperty("size").GetInt64(); string sha = S(row, "sha256");
					Require(entries.Count < 10000 && size >= 0 && size <= Maximum - total && HashShape(sha)
						&& (previous == null || CompareUtf8(previous, name) < 0), "invalid file inventory");
					entries.Add(name, new Entry { Path = Path.Combine(content, name.Replace('/', '\\')), Size = size, Hash = sha });
					total += size; previous = name;
				}
				Require(entries.ContainsKey("manifest.json") && entries.ContainsKey("workshop.json") && entries.ContainsKey("preview.png"), "missing package metadata");
				Require(entries["manifest.json"].Hash == S(p, "manifestSHA") && entries["workshop.json"].Hash == S(p, "workshopSHA"), "metadata hash mismatch");
				Scan(true); VerifyReceipt(); Metadata(p, item, version, visibility, title, description, tags);
				Request = new UploadRequest(item, S(p, "manifestId"), version, title, description, content, preview,
					tags.ToArray(), p.GetProperty("steamVisibility").GetInt32(), note);
			}
			Require(Revalidate(), "package changed during acquisition");
		}

		public bool Revalidate()
		{
			if (disposed || refused) return false;
			try { Verify(planFile); Verify(receiptFile); Scan(false); return true; }
			catch (Exception) { refused = true; return false; }
		}

		private void Scan(bool acquire)
		{
			HashSet<string> files = new HashSet<string>(StringComparer.Ordinal), dirs = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> folded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Walk(content, "", 0, acquire, files, dirs, folded);
			Require(files.Count == entries.Count && (acquire || treeDirectories.SetEquals(dirs)), "package tree changed");
			if (acquire) treeDirectories = dirs;
		}
		private void Walk(string directory, string prefix, int depth, bool acquire, HashSet<string> files, HashSet<string> dirs, HashSet<string> folded)
		{
			Require(depth <= 64 && dirs.Count < 10000 && dirs.Add(prefix), "directory bound exceeded"); Anchor(directory);
			foreach (string path in Directory.EnumerateFileSystemEntries(directory))
			{
				string relative = Name(prefix + Path.GetFileName(path));
				Require(folded.Add(relative.Normalize(NormalizationForm.FormC)), "aliased package entry");
				FileAttributes attributes = File.GetAttributes(path); Require((attributes & FileAttributes.ReparsePoint) == 0, "linked entry");
				if ((attributes & FileAttributes.Directory) != 0) Walk(path, relative + "/", depth + 1, acquire, files, dirs, folded);
				else
				{
					Entry row; Require(files.Count < 10000 && files.Add(relative) && entries.TryGetValue(relative, out row), "unlisted package file");
					row = entries[relative];
					if (acquire) { Entry leased = LeaseFile(path, row.Size, row.Hash); Require(leased.Size == row.Size, "file size mismatch"); row.Stream = leased.Stream; }
					Verify(row);
				}
			}
		}
		private void Anchor(string directory)
		{
			string parent = Path.GetDirectoryName(directory);
			if (!string.IsNullOrEmpty(parent) && !directories.ContainsKey(parent)) Anchor(parent);
			if (!directories.ContainsKey(directory)) directories.Add(directory, Handle(directory, true));
			Require((File.GetAttributes(directory) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == FileAttributes.Directory, "directory authority changed");
		}
		private SafeFileHandle Handle(string path, bool directory)
		{
			SafeFileHandle handle = CreateFileW(path, directory ? 0x80u : 0x80000000u, 1, IntPtr.Zero, 3,
				0x00200000u | (directory ? 0x02000000u : 0), IntPtr.Zero);
			leases.Add(handle); Require(!handle.IsInvalid, "cannot lease input");
			FileAttributes attributes = File.GetAttributes(path);
			Require((attributes & FileAttributes.ReparsePoint) == 0 && ((attributes & FileAttributes.Directory) != 0) == directory, "wrong input type");
			return handle;
		}
		private Entry LeaseFile(string path, long bound, string sha)
		{
			Require(HashShape(sha), "invalid recorded hash"); Anchor(Path.GetDirectoryName(path));
			FileStream stream = new FileStream(Handle(path, false), FileAccess.Read); leases.Add(stream);
			Require(stream.Length >= 0 && stream.Length <= bound, "input byte bound exceeded");
			Entry entry = new Entry { Path = path, Stream = stream, Size = stream.Length, Hash = sha }; Verify(entry); return entry;
		}
		private static void Verify(Entry entry)
		{
			Require(entry != null && entry.Stream != null && entry.Stream.Length == entry.Size
				&& (File.GetAttributes(entry.Path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0
				&& ScenarioFileTrust.GetLinkCount(entry.Stream.SafeFileHandle) == 1, "file authority changed");
			entry.Stream.Position = 0;
			using (SHA256 sha = SHA256.Create()) Require(Hex(sha.ComputeHash(entry.Stream)) == entry.Hash && entry.Stream.Position == entry.Size, "file bytes changed");
		}
		private void VerifyReceipt()
		{
			string text = Utf8.GetString(Bytes(receiptFile)); Require(text.EndsWith("\n", StringComparison.Ordinal), "incomplete receipt");
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (string line in text.Substring(0, text.Length - 1).Split('\n'))
			{
				Require(line.Length > 66 && HashShape(line.Substring(0, 64)) && line[64] == ' ' && (line[65] == ' ' || line[65] == '*'), "malformed receipt");
				string name = line.Substring(66); if (name.StartsWith("./", StringComparison.Ordinal)) name = name.Substring(2);
				Entry entry; Require(seen.Add(Name(name)) && entries.TryGetValue(name, out entry) && entry.Hash == line.Substring(0, 64), "receipt inventory mismatch");
			}
			Require(seen.Count == entries.Count, "receipt is not closed");
		}
		private void Metadata(JsonElement plan, ulong item, string version, string visibility, string title, string description, List<string> tags)
		{
			using (JsonDocument manifest = Json(entries["manifest.json"]))
			using (JsonDocument workshop = Json(entries["workshop.json"]))
			{
				JsonElement m = manifest.RootElement, w = workshop.RootElement;
				Fields(w, "WorkshopId Title Description Tags Visibility ImagePath");
				Require(S(m, "id") == S(plan, "manifestId") && S(m, "version") == version && S(m, "PreviewImage") == "preview.png"
					&& w.GetProperty("WorkshopId").GetUInt64() == item && S(w, "Visibility") == visibility
					&& S(w, "Title") == title && S(w, "Description") == description && S(w, "Tags") == string.Join(",", tags)
					&& S(w, "ImagePath") == "preview.png", "package metadata contradicts plan");
			}
		}
		private static JsonDocument Json(Entry entry)
		{
			JsonDocument document = JsonDocument.Parse(Utf8.GetString(Bytes(entry)), new JsonDocumentOptions { MaxDepth = 64 });
			try { Unique(document.RootElement); return document; } catch { document.Dispose(); throw; }
		}
		private static void Unique(JsonElement value)
		{
			if (value.ValueKind == JsonValueKind.Object)
			{
				HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
				foreach (JsonProperty property in value.EnumerateObject()) { Require(names.Add(property.Name), "duplicate JSON property"); Unique(property.Value); }
			}
			else if (value.ValueKind == JsonValueKind.Array) foreach (JsonElement child in value.EnumerateArray()) Unique(child);
		}
		private static byte[] Bytes(Entry entry)
		{
			Require(entry.Size <= 4 * 1024 * 1024, "metadata exceeds bound"); entry.Stream.Position = 0;
			byte[] bytes = new byte[(int)entry.Size]; int offset = 0, read;
			while (offset < bytes.Length && (read = entry.Stream.Read(bytes, offset, bytes.Length - offset)) > 0) offset += read;
			Require(offset == bytes.Length, "truncated input"); return bytes;
		}
		private static string Native(string value, bool posixOnly)
		{
			Require(value != null, "missing path");
			Match match = Regex.Match(value, @"\A/mnt/([a-zA-Z])/(.+)\z");
			if (match.Success) value = char.ToUpperInvariant(match.Groups[1].Value[0]) + ":\\" + Name(match.Groups[2].Value).Replace('/', '\\');
			else Require(!posixOnly, "content paths require a mounted drive");
			Require(Regex.IsMatch(value, @"\A[A-Za-z]:\\") && value.Length > 3 && value == Path.GetFullPath(value), "noncanonical native path");
			Name(value.Substring(3).Replace('\\', '/')); return value;
		}
		private static string Name(string value)
		{
			Require(Text(value, 32767, false) && value.IndexOf('\\') < 0, "invalid path text");
			for (int i = 0; i < value.Length; i++)
			{
				UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(value, i);
				Require(category != UnicodeCategory.Format && category != UnicodeCategory.OtherNotAssigned && category != UnicodeCategory.PrivateUse
					&& category != UnicodeCategory.LineSeparator && category != UnicodeCategory.ParagraphSeparator
					&& (category != UnicodeCategory.SpaceSeparator || value[i] == ' '), "nonprintable path text");
			}
			foreach (string part in value.Split('/'))
			{
				Require(part.Length > 0 && part != "." && part != ".." && !part.EndsWith(".", StringComparison.Ordinal)
					&& !part.EndsWith(" ", StringComparison.Ordinal) && part.IndexOfAny("<>:\"|?*".ToCharArray()) < 0, "unsafe path component");
				string stem = part.Split('.')[0].TrimEnd(' ');
				Require(!Regex.IsMatch(stem, @"\A(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|(?:COM|LPT)[1-9¹²³])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "device path refused");
			}
			return value;
		}
		private static bool Text(string value, int maxBytes, bool lines)
		{
			if (string.IsNullOrEmpty(value)) return false;
			for (int i = 0; i < value.Length; i++) if (char.IsControl(value[i]) && !(lines && "\r\n\t".IndexOf(value[i]) >= 0)) return false;
			try { return Utf8.GetByteCount(value) <= maxBytes; } catch (EncoderFallbackException) { return false; }
		}
		private static ulong Item(string value)
		{
			ulong item; Require(ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out item)
				&& item > 0 && item.ToString(CultureInfo.InvariantCulture) == value, "invalid item identity"); return item;
		}
		private static int CompareUtf8(string a, string b)
		{
			byte[] left = Utf8.GetBytes(a), right = Utf8.GetBytes(b);
			for (int i = 0; i < Math.Min(left.Length, right.Length); i++) if (left[i] != right[i]) return left[i].CompareTo(right[i]);
			return left.Length.CompareTo(right.Length);
		}
		private static bool Inside(string path, string root) => path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
		private static bool HashShape(string value) => value != null && Regex.IsMatch(value, @"\A[0-9a-f]{64}\z");
		private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
		private static string S(JsonElement value, string name) => value.GetProperty(name).GetString();
		private static void Fields(JsonElement value, string names)
		{
			HashSet<string> expected = new HashSet<string>(names.Split(' '), StringComparer.Ordinal);
			foreach (JsonProperty property in value.EnumerateObject()) Require(expected.Remove(property.Name), "unknown JSON field");
			Require(expected.Count == 0, "missing JSON field");
		}
		private static void Require(bool value, string detail) { if (!value) throw new InvalidDataException(detail); }
		private sealed class Entry { internal string Path, Hash; internal long Size; internal FileStream Stream; }
		public void Dispose()
		{
			if (disposed) return; disposed = true; Exception failure = null;
			for (int i = leases.Count - 1; i >= 0; i--) try { leases[i].Dispose(); } catch (Exception error) { failure = error; }
			leases.Clear(); if (failure != null) throw new IOException("input lease disposal failed", failure);
		}
	}
}
