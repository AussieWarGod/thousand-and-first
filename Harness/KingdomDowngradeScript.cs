using System;
using System.IO;
using HarmonyLib;
using Qud.UI;
using XRL;
using XRL.Core;

namespace ThousandAndFirst.Harness
{
	// V2-only fourth observer, compiled with the exact old Harness. This stops only its automatic
	// test-game selection; the actual MainMenu.Show and old parser/store observation still execute.
	[HarmonyPatch(typeof(KingdomScenarioTestGameEntry), "Autostart", new Type[] { typeof(MainMenu) })]
	internal static class KingdomDowngradeScript
	{
		private static bool Claimed;
		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static bool Prefix(MainMenu __0)
		{
			try
			{
				string local = XRLCore.LocalPath;
				if (!Claimed)
				{
					if (string.IsNullOrEmpty(local)) return true;
					bool request = KingdomDowngradeFiles.Present(Path.Combine(local, KingdomDowngradeRequest.FileName));
					if (!request && !ExactScriptWithoutRequest(local)) return true;
					Claimed = true; // Presence is claimed before parsing: malformed requests cannot fall through.
				}
				Observe(__0);
			}
			catch (Exception error)
			{
				Claimed = true;
				KingdomDowngradeProbe.RefuseScript(error);
			}
			return false;
		}

		private static bool ExactScriptWithoutRequest(string local)
		{
			string path = Path.Combine(local, "scenario-script.txt");
			if (!KingdomDowngradeFiles.Present(path)) return false;
			// Missing dedicated request cannot send the exact reader verb into ordinary autostart.
			// Other well-formed scripts are untouched. An unreadable script refuses automatic entry.
			using (var files = new KingdomDowngradeFiles())
			{
				files.Anchor(local.TrimEnd('\\'));
				string script = files.Read(path, 65536, out _); files.Reprove();
				return script == KingdomDowngradeProbe.ReaderScript;
			}
		}

		private static void Observe(MainMenu menu)
		{
			Require(menu != null && Environment.OSVersion.Platform == PlatformID.Win32NT,
				"reader needs actual Windows main-menu entry");
			string local = XRLCore.LocalPath;
			Require(!string.IsNullOrEmpty(local), "reader local profile absent");
			string root = Path.GetDirectoryName(local.TrimEnd('\\'));
			Require(KingdomDowngradeRequest.ValidRoot(root), "reader root is not exact C scenario custody");
			Owner(root);
			string requestHash;
			using (var files = new KingdomDowngradeFiles())
			{
				foreach (string suffix in new[] { "", "Local", "Save", "Synced", "Synced\\Saves",
					"Synced\\ThousandAndFirst", "Synced\\ThousandAndFirst\\Stages" })
					files.Anchor(suffix == "" ? root : Path.Combine(root, suffix));
				string wire = files.Read(Path.Combine(root, "Local", KingdomDowngradeRequest.FileName),
					KingdomDowngradeRequest.MaximumRequestBytes, out requestHash);
				Require(KingdomDowngradeRequest.TryParse(wire, out var request) && request.Root == root,
					"reader request is malformed or belongs to another root");
				Require(files.Read(Path.Combine(root, "Local", "scenario-script.txt"), 128, out _)
					== KingdomDowngradeProbe.ReaderScript, "reader script must be exactly upgrade-downgrade-check");
				foreach (string leaf in new[] { "scenario-load.txt", "scenario-load-snapshot.txt", "upgrade-save-request.txt", "upgrade-stage-source.txt" })
					KingdomDowngradeFiles.RequireAbsent(Path.Combine(root, "Local", leaf));
				foreach (string path in Directory.EnumerateFileSystemEntries(Path.Combine(root, "Synced", "Saves")))
					throw new InvalidOperationException("reader profile contains a saved game: " + path);
				files.Reprove(); Owner(root);
			}
			Owner(root); KingdomDowngradeProbe.AdmitScript(menu, root, requestHash);
		}
		private static void Owner(string root)
		{
			Require(The.Game == null && Path.GetFullPath(root) == root
				&& XRLCore.LocalPath != null && XRLCore.LocalPath.TrimEnd('\\') == Path.Combine(root, "Local")
				&& XRLCore.SavePath != null && XRLCore.SavePath.TrimEnd('\\') == Path.Combine(root, "Save")
				&& XRLCore.SyncedPath != null && XRLCore.SyncedPath.TrimEnd('\\') == Path.Combine(root, "Synced"),
				"reader game absence or exact profile paths changed");
		}
		private static void Require(bool value, string failure) => KingdomDowngradeFiles.Require(value, failure);
	}
}
