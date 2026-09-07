using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ThousandAndFirst.WorkshopSteam
{
	/// <summary>Synthetic local Windows fixtures only: no SDK, upload, or release acceptance.</summary>
	public static partial class UploadPackageTests
	{
		private static int passed, failed;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool CreateHardLinkW(string link, string target, IntPtr security);
		public static int Run()
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
			{ Console.WriteLine("UNSUPPORTED: UploadPackage tests require actual Windows file leases"); return 2; }
			passed = failed = 0;
			Case("exact private package and lease disposal", Exact);
			Case("exact alpha lane", Alpha);
			Case("expected hash and canonical target", ExpectedIdentity);
			Case("plan schema identity lane and version", PlanIdentity);
			Case("duplicate JSON properties at every boundary", DuplicateProperties);
			Case("closed sorted exact inventory", Inventory);
			Case("metadata must match plan, not manifest prose", Metadata);
			Case("receipt complete and exact", Receipts);
			Case("POSIX drive and Windows path safety", Paths);
			Case("file and plan byte bounds", Bounds);
			Case("tag and change-note validation", TextBounds);
			Case("post-acquisition drift stays refused", Drift);
			Case("hard-linked input refuses and releases handles", HardLinks);
			Case("junction/reparse child and content ancestor refuse before traversal", Reparse);
			Case("culture-independent item and path validation", Culture);
			Case("held empty package directory denies actual native write access", EmptyDirectoryWriter);
			Case("held empty directory blocks rename or rejects same-name replacement", EmptyDirectoryReplacement);
			Console.WriteLine("UploadPackage synthetic Windows fixtures: passed=" + passed + " failed=" + failed);
			return failed == 0 ? 0 : 1;
		}
		private static void Exact()
		{
			using (Fixture f = new Fixture())
			{
				UploadPackage package = f.Open();
				try
				{
					Check(package.Revalidate() && package.PlanSHA == f.Hash && package.ReceiptSHA == Hash(f.Receipt));
					Check(package.Request.Item == 12345 && package.Request.ManifestId == "r_ThousandAndFirst"
						&& package.Request.Version == "0.3.7" && package.Request.ContentPath == f.Content
						&& package.Request.PreviewPath == Path.Combine(f.Content, "preview.png") && package.Request.SteamVisibility == 2
						&& package.Request.Tags.Count == 2 && package.Request.ChangeNote == "Synthetic fixture");
					foreach (string path in new[] { f.PlanPath, f.Receipt, f.Payload })
					{ Throws<IOException>(() => File.WriteAllText(path, "changed")); Throws<IOException>(() => File.Move(path, path + ".moved")); }
					Throws<IOException>(() => Directory.Move(f.Content, f.Content + ".moved"));
					Check(package.Revalidate());
				}
				finally { package.Dispose(); }
				Check(!package.Revalidate()); package.Dispose(); f.ProveWritable();
			}
		}
		private static void Alpha()
		{
			using (Fixture f = new Fixture(true)) using (UploadPackage p = f.Open())
				Check(p.Request.Item == 3794797472UL && p.Request.SteamVisibility == 0 && p.Revalidate());
		}
		private static void ExpectedIdentity()
		{
			using (Fixture f = new Fixture())
			{
				foreach (string hash in new[] { null, "", new string('0', 64), f.Hash.ToUpperInvariant() })
					Refuses(() => UploadPackage.Open(f.PlanPath, hash, "12345", "Synthetic fixture"));
				foreach (string item in new[] { null, "0", "+12345", "012345", "12345 ", "18446744073709551616", "54321" })
					Refuses(() => UploadPackage.Open(f.PlanPath, f.Hash, item, "Synthetic fixture"));
				f.ProveWritable();
			}
		}
		private static void PlanIdentity()
		{
			foreach (string field in new[] { "schema", "mode", "manifestId", "qudVisibility" })
				BadPlan(p => p[field] = "foreign");
			BadPlan(p => p["planOnly"] = false); BadPlan(p => p["appId"] = 333641);
			BadPlan(p => p["steamVisibility"] = 0); BadPlan(p => p["unexpected"] = true); BadPlan(p => p.Remove("files"));
			foreach (string version in new[] { "0.3", "0.3.1.0", "0.03.1", "0.4.0", "0.3.-1" }) BadPlan(p => p["version"] = version);
			BadPlan(p => { p["targetItem"] = "3794797472"; p["mode"] = "test"; }, "3794797472");
			BadPlan(p => { p["mode"] = "alpha"; p["qudVisibility"] = "2"; p["steamVisibility"] = 0; });
		}
		private static void DuplicateProperties()
		{
			using (Fixture f = new Fixture())
			{
				f.WritePlan(f.Plan.ToJsonString().Insert(1, "\"schema\":\"taf-workshop-upload-plan-v1\",")); f.Refuse();
			}
			using (Fixture f = new Fixture())
			{
				f.WritePlan(f.Plan.ToJsonString().Replace("\"path\":", "\"path\":\"duplicate\",\"path\":")); f.Refuse();
			}
			foreach (string file in new[] { "manifest.json", "workshop.json" })
				using (Fixture f = new Fixture())
				{
					string path = Path.Combine(f.Content, file), raw = File.ReadAllText(path);
					File.WriteAllText(path, raw.Insert(1, "\"unrelated\":{\"x\":1,\"x\":2},"), Utf8); f.Rebuild(); f.Refuse();
				}
		}
		private static void Inventory()
		{
			BadPlan(p => p["files"][0]["size"] = 1); BadPlan(p => p["files"][0]["sha256"] = new string('0', 64));
			BadPlan(p => { JsonArray rows = p["files"].AsArray(); JsonNode first = rows[0].DeepClone(); rows.RemoveAt(0); rows.Add(first); });
			BadPlan(p => p["files"].AsArray().RemoveAt(0));
			foreach (int kind in new[] { 0, 1, 2 }) using (Fixture f = new Fixture())
			{
				if (kind == 0) File.WriteAllText(Path.Combine(f.Content, "unlisted"), "extra");
				if (kind == 1) File.Delete(f.Payload);
				if (kind == 2) File.AppendAllText(f.Payload, "modified");
				f.Refuse();
			}
			using (Fixture f = new Fixture())
			{
				File.WriteAllText(Path.Combine(f.Content, "é.txt"), "first"); File.WriteAllText(Path.Combine(f.Content, "e\u0301.txt"), "second");
				f.Rebuild(); f.Refuse();
			}
		}
		private static void Metadata()
		{
			foreach (string field in new[] { "id", "version", "PreviewImage" }) using (Fixture f = new Fixture())
			{ f.EditMetadata("manifest.json", field, JsonValue.Create("foreign")); f.Refuse(); }
			foreach (string field in new[] { "Title", "Description", "Tags", "Visibility", "ImagePath" }) using (Fixture f = new Fixture())
			{ f.EditMetadata("workshop.json", field, JsonValue.Create("foreign")); f.Refuse(); }
			using (Fixture f = new Fixture()) { f.EditMetadata("workshop.json", "WorkshopId", JsonValue.Create(12346)); f.Refuse(); }
			using (Fixture f = new Fixture()) using (UploadPackage p = f.Open()) Check(p.Request.Description == "Workshop rich description");
		}
		private static void Receipts()
		{
			foreach (int kind in new[] { 0, 1, 2, 3 }) using (Fixture f = new Fixture())
			{
				string raw = File.ReadAllText(f.Receipt), first = raw.Substring(0, raw.IndexOf('\n') + 1);
				f.ChangeReceipt(kind == 0 ? raw.TrimEnd('\n') : kind == 1 ? raw + first : kind == 2 ? raw.Substring(first.Length) : raw.Replace("  ./", "  ../"));
				f.Refuse();
			}
		}
		private static void Paths()
		{
			foreach (string path in new[] { "../escape", "CON.txt", "COM¹.txt", "a/../b", "a\\b", "/absolute", "trailing.", "space ", "a:b", "a\u200Bb" })
				BadPlan(p => p["files"][0]["path"] = path);
			foreach (string path in new[] { "/tmp/content", "//server/share", "C:\\content", "/mnt/c/a/../b", "/mnt/c/CON" })
				BadPlan(p => p["contentPath"] = path);
			BadPlan(p => p["previewPath"] = "/mnt/c/elsewhere/preview.png");
			using (Fixture f = new Fixture()) { f.Plan["receiptPath"] = Fixture.Posix(f.Payload); f.WritePlan(); f.Refuse(); }
		}
		private static void Bounds()
		{
			BadPlan(p => p["files"][0]["size"] = -1); BadPlan(p => p["files"][0]["size"] = 512L * 1024 * 1024 + 1);
			BadPlan(p =>
			{
				JsonArray rows = new JsonArray();
				for (int i = 0; i <= 10000; i++) rows.Add(new JsonObject { ["path"] = "f" + i.ToString("D5", CultureInfo.InvariantCulture), ["size"] = 0, ["sha256"] = new string('0', 64) });
				p["files"] = rows;
			});
			using (Fixture f = new Fixture()) { f.WritePlan(new string(' ', 4 * 1024 * 1024 + 1)); f.Refuse(); }
		}
		private static void TextBounds()
		{
			BadPlan(p => p["tags"] = JsonSerializer.SerializeToNode(new[] { "Building", "Building" }));
			BadPlan(p => p["tags"] = JsonSerializer.SerializeToNode(new[] { "a,b" })); BadPlan(p => p["title"] = "bad\nline");
			BadPlan(p => p["description"] = new string('x', 8000));
			using (Fixture f = new Fixture()) foreach (string note in new[] { null, "", " padded ", "bad\0note", "\ud800", new string('é', 4001) })
				Refuses(() => UploadPackage.Open(f.PlanPath, f.Hash, "12345", note));
		}
		private static void Drift()
		{
			foreach (bool directory in new[] { false, true }) using (Fixture f = new Fixture())
			{
				using (UploadPackage p = f.Open())
				{
					string extra = Path.Combine(f.Content, "unlisted");
					if (directory) Directory.CreateDirectory(extra); else File.WriteAllText(extra, "extra");
					Check(!p.Revalidate()); if (!directory) File.Delete(extra); Check(!p.Revalidate());
				}
				f.ProveWritable();
			}
		}
		private static void HardLinks()
		{
			foreach (int which in new[] { 0, 1, 2 }) using (Fixture f = new Fixture())
			{
				string target = which == 0 ? f.PlanPath : which == 1 ? f.Receipt : f.Payload;
				string original = Hash(target); Check(CreateHardLinkW(Path.Combine(f.Root, "owned-link"), target, IntPtr.Zero));
				f.Refuse(); Check(Hash(target) == original); f.ProveWritable();
			}
		}
		private static void Reparse()
		{
			List<Exception> failures = new List<Exception>();
			foreach (bool ancestor in new[] { false, true }) try { JunctionCase(ancestor); } catch (Exception error) { failures.Add(error); }
			if (failures.Count > 0) throw new AggregateException("junction fixture failures", failures);
		}
		private static void JunctionCase(bool ancestor)
		{
			Fixture f = new Fixture(); List<Exception> failures = new List<Exception>(); bool removable = true;
			string link = Path.Combine(ancestor ? f.Root : f.Content, ancestor ? "owned-directory-link" : "owned-link");
			string target = ancestor ? f.Content : Path.Combine(f.Root, "target");
			try
			{
				Directory.CreateDirectory(target);
				if (!ancestor) File.WriteAllText(Path.Combine(target, "must-not-traverse.txt"), "owned sentinel");
				Junction(f, link, target);
				if (ancestor) { f.Plan["contentPath"] = Fixture.Posix(link); f.Plan["previewPath"] = Fixture.Posix(Path.Combine(link, "preview.png")); f.WritePlan(); }
				RefusesReason(f.Open, ancestor ? "wrong input type" : "linked entry");
				Console.WriteLine("junction body: exact expected refusal; ancestor=" + ancestor);
			}
			catch (Exception error) { Console.WriteLine("junction body failure: " + error); failures.Add(error); }
			finally
			{
				try { DeleteOwnedJunction(link, target); } catch (Exception error) { removable = false; Console.WriteLine("junction cleanup failure: " + error); failures.Add(error); }
				if (removable) try { f.Dispose(); } catch (Exception error) { Console.WriteLine("fixture cleanup failure: " + error); failures.Add(error); }
				else Console.WriteLine("fixture retained: " + f.Root);
			}
			if (failures.Count > 0) throw new AggregateException("junction case ancestor=" + ancestor, failures);
		}
		private static void DeleteOwnedJunction(string link, string target)
		{
			FileAttributes attributes;
			try { attributes = File.GetAttributes(link); } catch (FileNotFoundException) { return; } catch (DirectoryNotFoundException) { return; }
			string actual = new DirectoryInfo(link).LinkTarget;
			Check((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == (FileAttributes.Directory | FileAttributes.ReparsePoint)
				&& (string.Equals(actual, target, StringComparison.OrdinalIgnoreCase) || string.Equals(actual, @"\??\" + target, StringComparison.OrdinalIgnoreCase)
					|| string.Equals(actual, @"\\?\" + target, StringComparison.OrdinalIgnoreCase)));
			Directory.Delete(link, false); Check(!Directory.Exists(link) && !File.Exists(link)); Console.WriteLine("owned junction removed nonrecursively: " + link);
		}
		private static void Junction(Fixture fixture, string link, string target)
		{
			foreach (string path in new[] { link, target })
				Check(path.StartsWith(fixture.Root + "\\", StringComparison.Ordinal) && path == Path.GetFullPath(path)
					&& path.IndexOfAny("\"%!&|<>^\r\n".ToCharArray()) < 0);
			Check(!Directory.Exists(link) && !File.Exists(link) && Directory.Exists(target));
			using (Process child = new Process { StartInfo = new ProcessStartInfo
			{
				FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"), Arguments = "/d /c mklink /J \"" + link + "\" \"" + target + "\"",
				WorkingDirectory = fixture.Root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
			} })
			{
				List<Exception> failures = new List<Exception>(); bool started = false; Task<string> output = null, errorOutput = null;
				try
				{
					Check(child.Start()); started = true; output = ReadOutput(child.StandardOutput); errorOutput = ReadOutput(child.StandardError);
					if (!child.WaitForExit(10000)) throw new TimeoutException("owned junction helper timed out");
					if (child.ExitCode != 0) throw new IOException("mklink exit=" + child.ExitCode);
				}
				catch (Exception error) { failures.Add(error); }
				finally
				{
					try { if (started && !child.HasExited) { child.Kill(); if (!child.WaitForExit(5000)) throw new IOException("owned junction helper did not stop"); } } catch (Exception error) { failures.Add(error); }
					try
					{
						Console.WriteLine("mklink exit=" + (started && child.HasExited ? child.ExitCode.ToString(CultureInfo.InvariantCulture) : "unavailable"));
						Check(output != null && errorOutput != null && Task.WaitAll(new Task[] { output, errorOutput }, 2000));
						Console.WriteLine("mklink stdout: " + output.Result); Console.WriteLine("mklink stderr: " + errorOutput.Result);
					}
					catch (Exception error) { failures.Add(error); }
				}
				if (failures.Count > 0) throw new AggregateException("owned mklink failed: " + link, failures);
			}
			Check((File.GetAttributes(link) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == (FileAttributes.Directory | FileAttributes.ReparsePoint));
		}
		private static async Task<string> ReadOutput(StreamReader reader)
		{
			char[] buffer = new char[1024]; StringBuilder text = new StringBuilder(); int read; bool truncated = false;
			while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
			{ int keep = Math.Min(read, 8192 - text.Length); text.Append(buffer, 0, keep); truncated |= keep != read; }
			return text.ToString() + (truncated ? " [truncated at 8192 characters]" : "");
		}
		private static void RefusesReason(Func<UploadPackage> action, string reason)
		{
			try { using (UploadPackage unexpected = action()) throw new InvalidOperationException("junction was accepted"); }
			catch (InvalidDataException error) { if (error.Message != reason) throw new InvalidOperationException("expected refusal: " + reason + "; actual: " + error.Message, error); }
		}
		private static void Culture()
		{
			CultureInfo before = CultureInfo.CurrentCulture;
			try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); using (Fixture f = new Fixture()) using (UploadPackage p = UploadPackage.Open(Fixture.Posix(f.PlanPath), f.Hash, f.Item, "Synthetic fixture")) Check(p.Revalidate()); }
			finally { CultureInfo.CurrentCulture = before; }
		}
		private static void BadPlan(Action<JsonObject> mutate, string item = "12345")
		{ using (Fixture f = new Fixture()) { mutate(f.Plan); f.WritePlan(); Refuses(() => UploadPackage.Open(f.PlanPath, f.Hash, item, "Synthetic fixture")); f.ProveWritable(); } }
		private static void Refuses(Func<UploadPackage> action)
		{ UploadPackage unexpected = null; bool refused = false; try { unexpected = action(); } catch (Exception) { refused = true; } finally { if (unexpected != null) unexpected.Dispose(); } Check(refused); }
		private static void Throws<T>(Action action) where T : Exception
		{ bool thrown = false; try { action(); } catch (T) { thrown = true; } Check(thrown); }
		private static void Check(bool value) { if (!value) throw new InvalidOperationException("fixture assertion failed"); }
		private static void Case(string name, Action body)
		{ try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); } }
		private static string Hash(string path)
		{ using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
		internal sealed class Fixture : IDisposable
		{
			internal readonly string Root, Content, Receipt, PlanPath, Payload, Item;
			internal JsonObject Plan;
			internal string Hash;
			internal Fixture(bool alpha = false, string version = "0.3.7", string item = null)
			{
				Root = Path.Combine(Path.GetTempPath(), "taf-upload-package-test." + Guid.NewGuid().ToString("N"));
				Content = Path.Combine(Root, "content"); Receipt = Path.Combine(Root, "receipt.sha256"); PlanPath = Path.Combine(Root, "plan.json");
				Payload = Path.Combine(Content, "Core", "Payload.cs"); Item = item ?? (alpha ? "3794797472" : "12345"); Directory.CreateDirectory(Path.GetDirectoryName(Payload));
				File.WriteAllText(Payload, "// synthetic package fixture\n", Utf8); File.WriteAllBytes(Path.Combine(Content, "preview.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII="));
				File.WriteAllText(Path.Combine(Content, "manifest.json"), JsonSerializer.Serialize(new { id = "r_ThousandAndFirst", version = version, PreviewImage = "preview.png", description = "Different manifest prose" }), Utf8);
				File.WriteAllText(Path.Combine(Content, "workshop.json"), JsonSerializer.Serialize(new { WorkshopId = ulong.Parse(Item, CultureInfo.InvariantCulture), Title = "Synthetic Workshop", Description = "Workshop rich description", Tags = "Building,Lore", Visibility = alpha ? "2" : "0", ImagePath = "preview.png" }), Utf8);
				Plan = JsonSerializer.SerializeToNode(new { schema = "taf-workshop-upload-plan-v1", planOnly = true, appId = 333640, targetItem = Item, mode = alpha ? "alpha" : "test", manifestId = "r_ThousandAndFirst", version = version, title = "Synthetic Workshop", description = "Workshop rich description", tags = new[] { "Building", "Lore" }, qudVisibility = alpha ? "2" : "0", steamVisibility = alpha ? 0 : 2, contentPath = Posix(Content), previewPath = Posix(Path.Combine(Content, "preview.png")), receiptPath = Posix(Receipt) }).AsObject(); Rebuild();
			}
			internal void Rebuild()
			{
				string[] paths = Directory.GetFiles(Content, "*", SearchOption.AllDirectories); Array.Sort(paths, StringComparer.Ordinal);
				JsonArray rows = new JsonArray(); StringBuilder receipt = new StringBuilder();
				foreach (string path in paths)
				{
					string name = path.Substring(Content.Length + 1).Replace('\\', '/'), sha = UploadPackageTests.Hash(path);
					rows.Add(new JsonObject { ["path"] = name, ["sha256"] = sha, ["size"] = new FileInfo(path).Length }); receipt.Append(sha).Append("  ./").Append(name).Append('\n');
				}
				Plan["files"] = rows; Plan["manifestSHA"] = UploadPackageTests.Hash(Path.Combine(Content, "manifest.json")); Plan["workshopSHA"] = UploadPackageTests.Hash(Path.Combine(Content, "workshop.json")); ChangeReceipt(receipt.ToString());
			}
			internal void EditMetadata(string file, string field, JsonNode value)
			{ string path = Path.Combine(Content, file); JsonObject json = JsonNode.Parse(File.ReadAllText(path)).AsObject(); json[field] = value; File.WriteAllText(path, json.ToJsonString(), Utf8); Rebuild(); }
			internal void ChangeReceipt(string value) { File.WriteAllText(Receipt, value, Utf8); Plan["receiptSHA"] = UploadPackageTests.Hash(Receipt); WritePlan(); }
			internal void WritePlan() { WritePlan(Plan.ToJsonString()); }
			internal void WritePlan(string raw) { File.WriteAllText(PlanPath, raw, Utf8); Hash = UploadPackageTests.Hash(PlanPath); }
			internal UploadPackage Open() { return UploadPackage.Open(PlanPath, Hash, Item, "Synthetic fixture"); }
			internal void Refuse() { Refuses(Open); }
			internal void ProveWritable() { foreach (string path in new[] { PlanPath, Receipt, Payload }) if (File.Exists(path)) using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Check(stream.CanWrite); }
			internal static string Posix(string path) { Check(path.Length > 3 && path[1] == ':'); return "/mnt/" + char.ToLowerInvariant(path[0]) + "/" + path.Substring(3).Replace('\\', '/'); }
			public void Dispose() { Directory.Delete(Root, true); }
		}
	}
}
