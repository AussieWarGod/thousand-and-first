using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using HarmonyLib;
using Qud.UI;
using XRL;
using XRL.Core;

namespace ThousandAndFirst.Harness
{
	// Host pins old production AND old Harness. V1 uses three observers and no script; V2 also
	// overlays the narrow Autostart boundary. Neither recipe creates a game or loads a save.
	[HarmonyPatch(typeof(MainMenu), "Show", new Type[] { })]
	internal static class KingdomDowngradeProbe
	{
		private static int Consumed;
		internal const string ReaderScript = "upgrade-downgrade-check\n";
		private static string ScriptRoot, ScriptRequestHash, ScriptFault;
		private static MainMenu ScriptMenu;
		// No reference to the optional fourth overlay type: historical three-file recipes compile.
		internal static void AdmitScript(MainMenu menu, string root, string requestHash)
		{
			Require(ScriptFault == null && menu != null && (ScriptRoot == null || ScriptRoot == root
				&& ScriptRequestHash == requestHash && ReferenceEquals(ScriptMenu, menu)),
				"reader script admission changed");
			ScriptRoot = root; ScriptRequestHash = requestHash; ScriptMenu = menu;
		}
		internal static void RefuseScript(Exception error)
		{
			ScriptFault = ScriptFault ?? Bounded(error.GetType().Name + ": " + error.Message);
			try { MetricsManager.LogError("native-downgrade-reader REFUSED: script admission: " + ScriptFault); }
			catch { }
		}
		[HarmonyPostfix, HarmonyPriority(Priority.Last)]
		internal static void Postfix(MainMenu __instance)
		{
			try
			{
				string local = XRLCore.LocalPath;
				if (string.IsNullOrEmpty(local) || !KingdomDowngradeFiles.Present(
					Path.Combine(local, KingdomDowngradeRequest.FileName))) return;
				if (Interlocked.Exchange(ref Consumed, 1) != 0) return;
				Require(__instance != null, "main-menu instance missing");
				Require(ScriptRoot == null || ReferenceEquals(ScriptMenu, __instance), "reader main-menu instance changed");
				string root = Root(); Owner(root);
				string report = Observe(root, out KingdomDowngradeRequest request, out string requestHash);
				Owner(root);
				KingdomDowngradeFiles.WriteReport(root, report);
				VerifyAfterReport(request, requestHash, report);
				Owner(root);
				MetricsManager.LogInfo("native-downgrade-reader cases=1 passed=1 failed=0; main-menu=true; "
					+ "game-created=false; save-loaded=false; report-sha256=" + KingdomDowngradeFiles.HashText(report));
			}
			catch (Exception ex)
			{
				Interlocked.Exchange(ref Consumed, 1);
				try { MetricsManager.LogError("native-downgrade-reader REFUSED: " + Bounded(ex.Message)); }
				catch { }
			}
		}

		private static string Observe(string root, out KingdomDowngradeRequest request, out string requestHash)
		{
			using (var files = new KingdomDowngradeFiles())
			{
				Anchor(files, root);
				string wire = files.Read(Path.Combine(root, "Local", KingdomDowngradeRequest.FileName),
					KingdomDowngradeRequest.MaximumRequestBytes, out requestHash);
				Require(KingdomDowngradeRequest.TryParse(wire, out request) && request.Root == root,
					"downgrade request is malformed or belongs to another root");
				RequestAdmission(root, requestHash);
				KingdomDowngradeFiles.RequireAbsent(Path.Combine(root, KingdomDowngradeRequest.ReportName));
				string[] texts = Inputs(files, request);
				var accepted = new KingdomSealRecord[2]; int rejected = 0;
				var report = new StringBuilder("taf-downgrade-report-v1\n");
				report.Append("root=").Append(root).Append("\norigin=").Append(request.Origin)
					.Append("\nsource-pin=").Append(request.SourcePin).Append("\nold-pin=")
					.Append(KingdomDowngradeRequest.OldPin).Append("\nrequest-sha256=").Append(requestHash)
					.Append("\nsource-pin-authority=host-inventory\nold-runtime-authority=host-inventory\nentry=MainMenu.Show-postfix\n");
				for (int i = 0; i < 2; i++)
				{
					report.Append(request.At(i).Compose()).Append('\n');
					if (texts[i] == null) { report.Append("slot-result=absent\n"); continue; }
					Require(KingdomSealFormat.TryParse(texts[i], 4, 6, out int schema, out KingdomSealBody body,
						out KingdomSealFault frameFault, out string detail), "native frame rejected: " + frameFault + ": " + detail);
					Require(body.Text("kind") == "record" && body.Text("origin") == request.Origin
						&& (body.Text("status") == "living" || body.Text("status") == "terminal" || body.Text("status") == "retired"),
						"slot payload is not the exact source-origin record");
					bool parsed = KingdomSealRecord.TryParse(texts[i], out accepted[i], out KingdomSealFault fault, out detail);
					if (schema == 6 && body.Has("profile_schema") && body.KindOf("profile_schema") == KingdomSealKind.Number
						&& body.Number("profile_schema") == 2)
					{
						EmptyCampShape(body, report);
						Require(!parsed && accepted[i] == null && fault == KingdomSealFault.OutOfBounds
							&& detail == "'profile_schema' is 2, outside 0 to 1", "old reader did not reject schema2 at its exact bound");
						rejected++; report.Append("slot-result=OutOfBounds; ").Append(detail).Append('\n');
					}
					else
					{
						Require(parsed && accepted[i] != null && accepted[i].OriginGameId == request.Origin
							&& accepted[i].Status != KingdomSealStatus.Promoted && accepted[i].Compose() == texts[i],
							"non-schema2 sibling is not an exact accepted old stage");
						report.Append("slot-result=accepted-old-stage\n");
					}
				}
				Require(rejected > 0, "no actual schema2 input was rejected");
				Owner(root); files.Reprove(); Inventory(request);
				var store = new KingdomSealStore(Path.Combine(root, "Synced", "ThousandAndFirst"));
				KingdomSealRecord selected = store.ReadStage(request.Origin);
				bool any = accepted[0] != null || accepted[1] != null;
				Require(any == (selected != null), "native ReadStage absence differs from accepted input inventory");
				string selectedSlot = "absent";
				if (selected != null)
				{
					string selectedText = selected.Compose();
					Require(selected.OriginGameId == request.Origin, "native ReadStage returned another origin");
					selectedSlot = accepted[0] != null && selectedText == texts[0] ? "a"
						: accepted[1] != null && selectedText == texts[1] ? "b" : null;
					Require(selectedSlot != null, "native ReadStage returned an unobserved sibling");
				}
				files.Reprove(); Inventory(request); Owner(root);
				report.Append("read-stage=").Append(selectedSlot).Append("\nschema2-rejected=")
					.Append(rejected.ToString(CultureInfo.InvariantCulture))
					.Append("\ninput-bytes-unchanged=true\nno-escaped-parser-or-store-exception=true")
					.Append("\ngame-created=false\nsave-loaded=false\nstatus=observed\n");
				return report.ToString();
			}
		}
		private static void EmptyCampShape(KingdomSealBody body, StringBuilder report)
		{
			// Shape observation only. Source-game ownership and current validity belong to the host's
			// independently pinned current-reader observation; the old reader must still refuse.
			foreach (string key in new[] { "stage", "people", "technology_band", "revision", "written", "founded" })
				Require(body.Has(key) && body.KindOf(key) == KingdomSealKind.Number, "empty-camp numeric field missing: " + key);
			var bodies = body.TextList("canonical_body");
			Require(body.Text("status") == "living" && body.Number("stage") == 0 && body.Number("people") == 0
				&& bodies != null && bodies.Count == 1 && bodies[0] == "unresolved"
				&& body.Number("technology_band") >= 0 && body.Number("technology_band") <= 10
				&& body.Number("revision") > 0 && body.Number("founded") >= 0
				&& body.Number("written") >= body.Number("founded")
				&& KingdomDowngradeRequest.Hex(body.Text("source_profile_digest"), 64)
				&& KingdomDowngradeRequest.Hex(body.Text("profile_provenance_digest"), 64),
				"schema2 source is not a committed unresolved living empty-camp shape");
			report.Append("empty-camp-shape=true; status=living; stage=0; people=0; bodies=unresolved; technology=")
				.Append(body.Number("technology_band").ToString(CultureInfo.InvariantCulture))
				.Append("; source=").Append(body.Text("source_profile_digest"))
				.Append("; provenance=").Append(body.Text("profile_provenance_digest")).Append('\n');
		}

		private static void VerifyAfterReport(KingdomDowngradeRequest request, string requestHash, string report)
		{
			using (var files = new KingdomDowngradeFiles())
			{
				Anchor(files, request.Root);
				string wire = files.Read(Path.Combine(request.Root, "Local", KingdomDowngradeRequest.FileName),
					KingdomDowngradeRequest.MaximumRequestBytes, out string hash);
				Require(hash == requestHash && wire == request.Compose(), "request changed after report");
				RequestAdmission(request.Root, hash);
				string echo = files.Read(Path.Combine(request.Root, KingdomDowngradeRequest.ReportName),
					16384, out string reportHash);
				Require(echo == report && reportHash == KingdomDowngradeFiles.HashText(report), "report readback differs");
				Inputs(files, request); files.Reprove(); Inventory(request); Owner(request.Root);
			}
		}
		private static string[] Inputs(KingdomDowngradeFiles files, KingdomDowngradeRequest request)
		{
			Inventory(request); string[] texts = new string[2];
			for (int i = 0; i < 2; i++)
			{
				KingdomDowngradeRequest.Slot slot = request.At(i);
				string path = SlotPath(request, slot.Name);
				if (!slot.Present) { KingdomDowngradeFiles.RequireAbsent(path); continue; }
				texts[i] = files.Read(path, KingdomDowngradeRequest.MaximumSlotBytes, out string hash);
				Require(KingdomDowngradeFiles.ByteCount(texts[i]) == slot.Count && hash == slot.Sha256,
					"slot bytes differ from sealed source manifest");
			}
			return texts;
		}
		private static void Inventory(KingdomDowngradeRequest request)
		{
			int count = 0;
			foreach (string path in Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(SlotPath(request, 'a'))))
			{
				Require(++count <= 2, "unexpected stage inventory");
				bool expected = request.At(0).Present && path == SlotPath(request, 'a')
					|| request.At(1).Present && path == SlotPath(request, 'b');
				Require(expected, "unmanifested stage leaf");
			}
			Require(count == (request.At(0).Present ? 1 : 0) + (request.At(1).Present ? 1 : 0), "stage inventory incomplete");
		}
		private static string SlotPath(KingdomDowngradeRequest request, char slot) => Path.Combine(request.Root,
			"Synced", "ThousandAndFirst", "Stages", request.Origin + "." + slot + ".seal");
		private static void Anchor(KingdomDowngradeFiles files, string root)
		{
			Owner(root);
			Require(ScriptFault == null, "reader script boundary previously refused: " + ScriptFault);
			foreach (string suffix in new[] { "", "Local", "Save", "Synced", "Synced\\Saves",
				"Synced\\ThousandAndFirst", "Synced\\ThousandAndFirst\\Stages" })
				files.Anchor(suffix == "" ? root : Path.Combine(root, suffix));
			foreach (string path in Directory.EnumerateFileSystemEntries(Path.Combine(root, "Synced", "Saves")))
				throw new InvalidOperationException("old-reader profile contains a saved game: " + path);
			foreach (string leaf in new[] { "scenario-load.txt", "scenario-load-snapshot.txt", "upgrade-save-request.txt", "upgrade-stage-source.txt" })
				KingdomDowngradeFiles.RequireAbsent(Path.Combine(root, "Local", leaf));
			string script = Path.Combine(root, "Local", "scenario-script.txt");
			if (KingdomDowngradeFiles.Present(script))
			{
				Require(files.Read(script, 128, out _) == ReaderScript && ScriptRoot == root,
					"exact reader script lacks witnessed Autostart boundary");
			}
			else
			{
				Require(ScriptRoot == null, "admitted reader script disappeared");
				KingdomDowngradeFiles.RequireAbsent(Path.Combine(root, "Local", "upgrade-persona.txt"));
			}
		}
		private static void RequestAdmission(string root, string hash)
		{ Require(ScriptRoot == null || ScriptRoot == root && ScriptRequestHash == hash, "admitted reader request changed"); }
		private static string Root()
		{
			Require(Environment.OSVersion.Platform == PlatformID.Win32NT, "native Windows profile required");
			string local = XRLCore.LocalPath;
			Require(!string.IsNullOrEmpty(local), "native local profile unavailable");
			string root = Path.GetDirectoryName(local.TrimEnd('\\'));
			Require(KingdomDowngradeRequest.ValidRoot(root), "exact C:\\taf-scenario.<alnum> root required");
			return root;
		}
		private static void Owner(string root)
		{
			Require(The.Game == null && Path.GetFullPath(root) == root
				&& XRLCore.LocalPath.TrimEnd('\\') == Path.Combine(root, "Local")
				&& XRLCore.SavePath.TrimEnd('\\') == Path.Combine(root, "Save")
				&& XRLCore.SyncedPath.TrimEnd('\\') == Path.Combine(root, "Synced"),
				"main-menu game absence or exact profile paths changed");
		}
		private static string Bounded(string text)
		{
			text = (text ?? "unknown failure").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
			return text.Length <= 1000 ? text : text.Substring(0, 1000);
		}
		private static void Require(bool value, string failure) => KingdomDowngradeFiles.Require(value, failure);
	}
}
