using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.Wish;

namespace ThousandAndFirst.Harness
{
	[HasWishCommand]
	public static class KingdomUpgradeSource
	{
		private static bool Attempted, Armed, Saving, Completed;
		private static XRLGame Game;
		private static KingdomSystem System;
		private static KingdomInheritanceState Inheritance;
		private static KingdomPolityRealmTransition Transition;
		private static KingdomUpgradeState Before;
		private static string Root, Request, Case, GameId, Directory, Fault;
		private static int InheritanceWrites, TransitionWrites, LegacyWrites;
		private static int PrimaryPaths, InfoPaths;

		[WishCommand("kingdom:upgrade-arm", null)]
		public static void Arm(string parameter)
		{
			try
			{
				Check(!Attempted && string.IsNullOrEmpty(parameter), "upgrade witness cannot rearm or accept arguments");
				Attempted = true;
				KingdomUpgradeState.Engine();
				Check(Thread.CurrentThread == XRLCore.CoreThread, "upgrade arm is not on the actual core thread");
				Root = KingdomUpgradeFiles.Root();
				Request = KingdomUpgradeFiles.Read(Path.Combine(Root, "Local", KingdomUpgradeSnapshotCodec.RequestFile), 160);
				Check(KingdomUpgradeSnapshotCodec.TryRequest(Request, out Case), "upgrade source request is malformed");
				foreach (string name in new[] { KingdomUpgradeSnapshotCodec.SnapshotFile, KingdomUpgradeSnapshotCodec.ReceiptFile,
					KingdomUpgradeSnapshotCodec.FailureFile }) KingdomUpgradeFiles.Vacant(Path.Combine(Root, name));
				Game = The.Game;
				KingdomUpgradeState initial = new KingdomUpgradeState(Game, Case);
				System = initial.System; Inheritance = initial.Inheritance; Transition = initial.Transition;
				GameId = Game.GameID; Directory = KingdomUpgradeFiles.SaveDirectory(Root, GameId);
				Check(!CapabilityManager.HasCapability(CapabilityManager.CapabilityType.RequiresStorageExpansion),
					"asynchronous storage-expansion callbacks are outside this save witness");
				Owner(); Armed = true;
				Popup.Show("Upgrade witness armed for the next actual Primary save. Save normally, then quit. "
					+ "No save or world state was created by this wish.");
				Owner(); initial.Exact();
			}
			catch (Exception error)
			{
				Fail(error);
				try { Popup.Show("Upgrade witness refused; evidence retained: " + Fault); } catch { }
			}
		}

		internal static KingdomUpgradeState Begin(XRLGame game, string name, bool copyCache, bool copyPrimary)
		{
			if (!Armed || name != "Primary") return null;
			try
			{
				Armed = false;
				Check(!Saving && !Completed && Fault == null && ReferenceEquals(game, Game), "upgrade save invocation is not the armed owner");
				Owner();
				Check(Thread.CurrentThread == XRLCore.CoreThread && !copyCache && !copyPrimary
					&& Game.Running && !Game.Transient && !Game.DontSaveThisIsAReplay
					&& (Game.SaveTask == null || Game.SaveTask.IsCompleted), "upgrade source did not enter an ordinary primary save");
				Before = new KingdomUpgradeState(Game, Case); Saving = true; return Before;
			}
			catch (Exception error) { Fail(error); return null; }
		}

		internal static void Wrote(object value)
		{
			if (!Saving) return;
			try
			{
				if (ReferenceEquals(value, Inheritance)) InheritanceWrites++;
				if (ReferenceEquals(value, Transition)) TransitionWrites++;
				if (Transition != null && ReferenceEquals(value, Transition.Legacy)) LegacyWrites++;
				Check(InheritanceWrites <= 16 && TransitionWrites <= 16 && LegacyWrites <= 16, "upgrade save repeated observed writers excessively");
				Before.Exact();
			}
			catch (Exception error) { Fail(error); }
		}

		internal static void CacheResolved(XRLGame game, string name, string path)
		{
			if (!Saving || !ReferenceEquals(game, Game)) return;
			try
			{
				Check(path != null && Game._CacheDirectory != null
					&& KingdomUpgradeFiles.Same(Path.GetFullPath(Game._CacheDirectory), Directory), "actual save cache root drifted");
				string expected = name == null ? Directory : Path.Combine(Directory, name);
				string full = Path.GetFullPath(path);
				Check(KingdomUpgradeFiles.Same(full, expected) && (KingdomUpgradeFiles.Same(full, Directory)
					|| full.StartsWith(Directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)),
					"actual save resolved a path outside its selected owned cache");
				if (name == "Primary.sav.gz") PrimaryPaths++;
				if (name == "Primary.json") InfoPaths++;
				Check(PrimaryPaths <= 16 && InfoPaths <= 16, "actual save repeatedly resolved primary paths");
			}
			catch (Exception error) { Fail(error); }
		}

		internal static void End(XRLGame game, KingdomUpgradeState state, Task result)
		{
			if (state == null) return;
			try
			{
				Check(ReferenceEquals(game, Game) && ReferenceEquals(state, Before) && Saving && Fault == null,
					"upgrade actual save reported failure: " + Fault);
				Check(result == null || result.IsCompleted && !result.IsFaulted && !result.IsCanceled,
					"upgrade save did not finish synchronously on the pinned PC path");
				Owner(); state.Exact(); state.Stage();
				Check(PrimaryPaths > 0 && InfoPaths > 0
					&& (Inheritance == null ? InheritanceWrites == 0 : InheritanceWrites > 0)
					&& (Transition == null ? TransitionWrites == 0 : TransitionWrites > 0)
					&& (Transition?.Legacy == null ? LegacyWrites == 0 : LegacyWrites > 0),
					"actual save did not serialize each selected original authority");
				Check(KingdomUpgradeSnapshotCodec.TryEncode(state.Snapshot, out string wire), "upgrade snapshot cannot encode");
				string primary = Path.Combine(Directory, "Primary.sav.gz"), info = Path.Combine(Directory, "Primary.json");
				using (FileStream file = KingdomUpgradeFiles.Open(primary, KingdomUpgradeFiles.MaxSaveBytes))
					Check(file.ReadByte() == 31 && file.ReadByte() == 139, "upgrade primary is not a real gzip save");
				string primaryHash = KingdomUpgradeFiles.HashFile(primary, KingdomUpgradeFiles.MaxSaveBytes);
				string infoHash = KingdomUpgradeFiles.HashFile(info, 1048576);
				// The engine can hold Cache.db writable. Only the stopped-process host binds its hash.
				string receipt = KingdomUpgradeSnapshotCodec.Receipt(state.Snapshot, primaryHash, infoHash, KingdomUpgradeFiles.HashText(wire));
				Owner(); state.Exact(); state.Stage();
				KingdomUpgradeFiles.New(Path.Combine(Root, KingdomUpgradeSnapshotCodec.SnapshotFile), wire);
				Check(KingdomUpgradeFiles.Read(Path.Combine(Root, KingdomUpgradeSnapshotCodec.SnapshotFile),
					KingdomUpgradeSnapshotCodec.MaxWireChars) == wire, "upgrade snapshot readback differs");
				Check(KingdomUpgradeFiles.HashFile(primary, KingdomUpgradeFiles.MaxSaveBytes) == primaryHash
					&& KingdomUpgradeFiles.HashFile(info, 1048576) == infoHash, "saved primary or metadata changed while witnessed");
				Owner(); state.Exact();
				KingdomUpgradeFiles.New(Path.Combine(Root, KingdomUpgradeSnapshotCodec.ReceiptFile), receipt);
				Check(KingdomUpgradeFiles.Read(Path.Combine(Root, KingdomUpgradeSnapshotCodec.ReceiptFile), 1024) == receipt,
					"upgrade save receipt readback differs");
				Completed = true;
			}
			catch (Exception error) { Fail(error); }
			finally { Saving = false; }
		}

		internal static void Error(XRLGame game, Exception error)
		{
			if (ReferenceEquals(game, Game) && (Saving || Completed)) Fail(error ?? new InvalidOperationException("actual SaveGameError"));
		}

		private static void Owner()
		{
			Check(Fault == null && Root == KingdomUpgradeFiles.Root() && ReferenceEquals(The.Game, Game)
				&& Game.GameID == GameId && ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
				&& ReferenceEquals(KingdomUpgradeState.ReadInheritance(Game), Inheritance)
				&& ReferenceEquals(System?.PolityTransition, Transition)
				&& Game._CacheDirectory != null && KingdomUpgradeFiles.Same(Path.GetFullPath(Game._CacheDirectory), Directory)
				&& KingdomUpgradeFiles.SaveDirectory(Root, GameId) == Directory
				&& KingdomUpgradeFiles.Read(Path.Combine(Root, "Local", KingdomUpgradeSnapshotCodec.RequestFile), 160) == Request,
				"upgrade source owner, selected cache or request changed");
		}

		private static void Fail(Exception error)
		{
			Armed = false;
			if (Fault != null) return;
			Fault = error.GetType().Name + ": " + error.Message;
			if (Fault.Length > 1000) Fault = Fault.Substring(0, 1000);
			try
			{
				if (Root != null && Root == KingdomUpgradeFiles.Root())
					KingdomUpgradeFiles.New(Path.Combine(Root, KingdomUpgradeSnapshotCodec.FailureFile),
						"taf-upgrade-save-failure-v1\n" + Fault.Replace('\r', ' ').Replace('\n', ' ') + "\n");
			}
			catch { /* The host requires success evidence and refuses missing/partial receipts. */ }
		}

		private static void Check(bool condition, string failure) { KingdomUpgradeFiles.Check(condition, failure); }
	}
}
