using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomChronicle
	{
		/// <summary>Captures the exact realm-scoped replay registry before exile clears it.
		/// Parsing is required here so malformed evidence is preserved in place, not moved into an
		/// archive that a later return would treat as authority.</summary>
		internal static bool TryCaptureRealmRegistry(out string Registry, out string Fault,
			out string Failure)
		{
			Registry = null;
			Fault = null;
			Failure = null;
			if (The.Game == null)
			{
				Failure = "chronicle game state is unavailable";
				return false;
			}
			try
			{
				Registry = The.Game.GetStringGameState(EventRegistryState, "") ?? "";
				Fault = The.Game.GetStringGameState(EventRegistryFaultState, "") ?? "";
			}
			catch
			{
				Failure = "chronicle registry could not be read";
				return false;
			}
			List<KingdomChronicleReceipt> rows;
			bool migrated;
			KingdomChronicleRegistryFault parseFault = KingdomChronicleRegistryFault.None;
			if (Fault.Length > 160 ||
				!KingdomChronicleReceiptRules.TryParseRegistry(Registry, out rows,
					out migrated, out parseFault) || migrated)
			{
				Failure = "chronicle registry is malformed or noncanonical (" + parseFault + ")";
				return false;
			}
			return true;
		}

		/// <summary>Exact before/after CAS for exile. Either value may already be empty after a
		/// save cut; any third value is unrelated realm evidence and refuses the transition.</summary>
		internal static bool TryClearRealmRegistry(string ExpectedRegistry, string ExpectedFault,
			out string Failure)
		{
			return TryMoveRealmRegistry(ExpectedRegistry ?? "", ExpectedFault ?? "", "", "",
				out Failure);
		}

		/// <summary>Exact inverse CAS for return. A new realm's receipt graph is never overwritten;
		/// return is allowed only into the genuinely empty unfounded interval.</summary>
		internal static bool TryRestoreRealmRegistry(string ArchivedRegistry, string ArchivedFault,
			out string Failure)
		{
			return TryMoveRealmRegistry("", "", ArchivedRegistry ?? "", ArchivedFault ?? "",
				out Failure);
		}

		private static bool TryMoveRealmRegistry(string BeforeRegistry, string BeforeFault,
			string AfterRegistry, string AfterFault, out string Failure)
		{
			Failure = null;
			if (The.Game == null)
			{
				Failure = "chronicle game state is unavailable";
				return false;
			}
			try
			{
				string currentRegistry = The.Game.GetStringGameState(EventRegistryState, "") ?? "";
				string currentFault = The.Game.GetStringGameState(EventRegistryFaultState, "") ?? "";
				if ((currentRegistry != BeforeRegistry && currentRegistry != AfterRegistry) ||
					(currentFault != BeforeFault && currentFault != AfterFault))
				{
					Failure = "chronicle registry carries a third realm value";
					return false;
				}
				if (currentRegistry == BeforeRegistry)
					The.Game.SetStringGameState(EventRegistryState, AfterRegistry);
				currentRegistry = The.Game.GetStringGameState(EventRegistryState, "") ?? "";
				if (currentRegistry != AfterRegistry)
				{
					Failure = "chronicle registry CAS did not settle";
					return false;
				}
				currentFault = The.Game.GetStringGameState(EventRegistryFaultState, "") ?? "";
				if (currentFault == BeforeFault)
					The.Game.SetStringGameState(EventRegistryFaultState, AfterFault);
				if ((The.Game.GetStringGameState(EventRegistryFaultState, "") ?? "") == AfterFault)
					return true;
			}
			catch { }
			Failure = "chronicle fault-register CAS did not settle";
			return false;
		}

		private static long Now()
		{
			return Math.Max(0L, The.Game == null ? 0L : The.Game.TimeTicks);
		}

		private static void ReportFault(KingdomChronicleRegistryFault Fault,
			string Context, bool PlayerVisible)
		{
			string code = ((int)Fault).ToString() + ":" + (Context ?? "unknown");
			if (code.Length > 160) code = code.Substring(0, 160);
			bool first = true;
			try
			{
				if (The.Game != null)
				{
					first = !string.Equals(The.Game.GetStringGameState(EventRegistryFaultState, ""),
						code, StringComparison.Ordinal);
					if (first) The.Game.SetStringGameState(EventRegistryFaultState, code);
				}
			}
			catch { }
			try { KingdomLog.Log("chronicle v3 refused " + code); }
			catch { }
			if (!PlayerVisible || !first) return;
			try
			{
				string line = Context == "capacity"
					? "{{r|The kingdom chronicle registry is full. This telling was refused; no replay receipt was discarded.}}"
					: "{{r|A kingdom chronicle receipt could not be proved. This telling was settled as lost or refused; no receipt was discarded.}}";
				XRL.Messages.MessageQueue.AddPlayerMessage(line);
			}
			catch { }
		}

		/// <summary>Returns the current city's persisted immutable id. Missing or mismatched
		/// provenance returns null; names are prose and never a draw or replay subject.</summary>
		internal static string SettlementId(KingdomSystem System)
		{
			return System?.CurrentSettlementId;
		}	}
}
