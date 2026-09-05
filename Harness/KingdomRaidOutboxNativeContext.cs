using System;
using System.Collections.Generic;
using System.Text;
using ConsoleLib.Console;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomRaidOutboxNativeContext
	{
		internal const string RegistryKey = "r_TAF_ChronicleEventRegistry_v1";
		internal const string FaultKey = "r_TAF_ChronicleEventRegistryFault_v3";
		private readonly object Player;
		private readonly GameObject PlayerBody;
		private readonly MessageQueue Queue;
		private readonly List<string> Messages;
		private readonly object[] Dictionaries;
		private readonly HashSet<string> Cases = new HashSet<string>(StringComparer.Ordinal);
		private readonly List<string> ExpectedTail = new List<string>();
		private readonly StringBuilder Results = new StringBuilder();
		private string[] Prefix;
		private string OwnedRegistry, OwnedFault;
		private int PreviousMessage, LastMessage, LastTurnMessages;
		private bool Terse, Suppress, InCase, Poisoned;
		internal XRLGame Game { get; private set; }
		internal Zone Zone { get; private set; }
		internal int Passed { get; private set; }
		internal int Failed { get; private set; }
		internal int Count { get { return Cases.Count; } }

		internal KingdomRaidOutboxNativeContext(XRLGame Game, Zone Zone)
		{
			this.Game = Game;
			this.Zone = Zone;
			Player = Game?.Player;
			PlayerBody = Game?.Player?.Body;
			Queue = Game?.Player?.Messages;
			Messages = Queue?.Messages;
			Dictionaries = StateDictionaries(Game);
		}

		internal void Check(bool Condition, string Detail)
		{
			if (!Condition) throw new InvalidOperationException(Detail);
		}

		internal void ExpectedMessage(string Raw)
		{
			Check(InCase && SameScope() && ExactMessages(), "message expectation was not declared before its append");
			Check(Raw != null && ExpectedTail.Count < 16, "native message expectation is missing or over bound");
			string expected = Markup.Transform(ConsoleLib.Console.ColorUtility.CapitalizeExceptFormatting(Raw));
			Check(expected != null && expected != "!clear", "native fixture requires an appended message");
			ExpectedTail.Add(expected);
		}

		internal void ExpectFault(string ExactCode)
		{
			Check(InCase && SameScope() && ExactCode == "5:list-bound" && OwnedFault == null
				&& !HasAnyState(Game, FaultKey), "fault expectation lacks fresh exact fixture authority");
			OwnedFault = ExactCode;
		}

		internal void CaptureRegistry(string EventId, string Text)
		{
			Check(InCase && SameScope() && !OtherTypedState(RegistryKey), "registry custody changed");
			string raw, fingerprint, canonical;
			List<KingdomChronicleReceipt> rows;
			bool migrated;
			KingdomChronicleRegistryFault fault;
			Check(Game.StringGameState.TryGetValue(RegistryKey, out raw)
				&& KingdomChronicleReceiptRules.TryFingerprint(EventId, Text, false, null, out fingerprint)
				&& KingdomChronicleReceiptRules.TryParseRegistry(raw, out rows, out migrated, out fault)
				&& !migrated && fault == KingdomChronicleRegistryFault.None && rows.Count == 1
				&& rows[0].Compact && !rows[0].LegacyBlocked
				&& KingdomChronicleReceiptRules.IsTerminal(rows[0])
				&& rows[0].OfficialState == KingdomChronicleSinkDisposition.Delivered
				&& rows[0].OutsiderState == KingdomChronicleSinkDisposition.Delivered
				&& rows[0].JournalState == KingdomChronicleSinkDisposition.Skipped
				&& string.Equals(rows[0].EventId, EventId, StringComparison.Ordinal)
				&& string.Equals(rows[0].Fingerprint, fingerprint, StringComparison.Ordinal)
				&& KingdomChronicleReceiptRules.TryWriteRegistry(rows, out canonical, out fault)
				&& string.Equals(raw, canonical, StringComparison.Ordinal)
				&& (OwnedRegistry == null || string.Equals(OwnedRegistry, raw, StringComparison.Ordinal)),
				"native registry is not the one exact canonical terminal fixture receipt");
			OwnedRegistry = raw;
		}

		internal void Case(string Id, Action Body)
		{
			if (string.IsNullOrEmpty(Id) || Cases.Count >= KingdomRaidOutboxNativeProvider.ExpectedCases || !Cases.Add(Id))
				throw new InvalidOperationException("Native raid case identity is missing, repeated, or over bound.");
			string failure = Poisoned ? "preceding fixture left unproved state" : null;
			bool started = false;
			try
			{
				if (failure == null)
				{
					Snapshot();
					started = InCase = true;
					Body();
				}
			}
			catch (Exception error) { failure = Describe(error); }
			finally
			{
				InCase = false;
				if (started)
				{
					bool clean = false;
					try { clean = VerifyAndClean(); }
					catch (Exception error) { failure = Append(failure, "fixture cleanup threw " + Describe(error)); }
					if (!clean)
					{
						Poisoned = true;
						failure = Append(failure, "fixture custody or cleanup unproved; unknown state retained");
					}
				}
				else if (failure != null) Poisoned = true;
			}
			if (failure == null) Passed++;
			else Failed++;
			Results.Append('\n').Append(Id).Append(failure == null ? "=PASS" : "=FAIL ")
				.Append(failure == null ? "" : KingdomScenarioRules.Bounded(failure));
		}

		internal string Report()
		{
			return "native-raid-outbox cases=" + Count + " passed=" + Passed + " failed=" + Failed
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested; engine-raised-failure=untested"
				+ "; messages-retained=true; ui-listeners=not-reversed" + Results;
		}

		internal static bool HasAnyState(XRLGame Game, string Key)
		{
			return KingdomNativeRegressionContext.HasAnyState(Game, Key);
		}

		private void Snapshot()
		{
			Check(SameScope() && MessageQueue.Enabled && !HasAnyState(Game, RegistryKey)
				&& !HasAnyState(Game, FaultKey), "native fixture requires fresh unmodified game, queue, and Chronicle keys");
			Prefix = Messages.ToArray();
			PreviousMessage = Queue.PreviousMessage;
			LastMessage = Queue.LastMessage;
			LastTurnMessages = Queue.LastTurnMessages;
			Terse = Queue.Terse;
			Suppress = MessageQueue.Suppress;
			ExpectedTail.Clear();
			OwnedRegistry = OwnedFault = null;
		}

		private bool SameScope()
		{
			if (Game == null || Zone == null || Player == null || Queue == null || Messages == null
				|| !ReferenceEquals(The.Game, Game) || !ReferenceEquals(Game.Player, Player)
				|| PlayerBody == null || !ReferenceEquals(The.Player, PlayerBody)
				|| !ReferenceEquals(Game.Player.Messages, Queue) || !ReferenceEquals(Queue.Messages, Messages)
				|| !ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) || !ReferenceEquals(The.Player?.CurrentZone, Zone)
				|| Game.StringGameState == null || KingdomNativeRegressionContext.HasQuickstartState(Game)
				|| !KingdomScenarioDurableState.ProvesExactText(KingdomRaidOutboxNativeProvider.Receipt, "intent")
				|| (Game.GetSystem<KingdomSystem>()?.Founded ?? false)) return false;
			object[] current = StateDictionaries(Game);
			for (int i = 0; i < Dictionaries.Length; i++)
				if (!ReferenceEquals(Dictionaries[i], current[i])) return false;
			return true;
		}

		private bool ExactMessages()
		{
			if (Prefix == null || Messages.Count != Prefix.Length + ExpectedTail.Count
				|| Queue.PreviousMessage != PreviousMessage || Queue.LastMessage != LastMessage
				|| Queue.LastTurnMessages != LastTurnMessages || Queue.Terse != Terse
				|| MessageQueue.Suppress != Suppress) return false;
			for (int i = 0; i < Prefix.Length; i++)
				if (!string.Equals(Messages[i], Prefix[i], StringComparison.Ordinal)) return false;
			for (int i = 0; i < ExpectedTail.Count; i++)
				if (!string.Equals(Messages[Prefix.Length + i], ExpectedTail[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private bool OtherTypedState(string Key)
		{
			return (Game.IntGameState?.ContainsKey(Key) ?? false) || (Game.Int64GameState?.ContainsKey(Key) ?? false)
				|| (Game.BooleanGameState?.ContainsKey(Key) ?? false) || (Game.ObjectGameState?.ContainsKey(Key) ?? false);
		}

		private bool OwnedStateMatches(string Key, string Owned)
		{
			return Owned == null ? !HasAnyState(Game, Key)
				: KingdomScenarioDurableState.ProvesExactText(Key, Owned);
		}

		private bool RemoveOwned(string Key, string Owned)
		{
			if (!SameScope() || !OwnedStateMatches(Key, Owned)) return false;
			Dictionary<string, string> exact = (Dictionary<string, string>)Dictionaries[0];
			if (exact.ContainsKey(Key) && !exact.Remove(Key)) return false;
			return !HasAnyState(Game, Key);
		}

		private bool VerifyAndClean()
		{
			// Message listeners are real external effects. Retain their proven text; never rewind UI state.
			if (!SameScope() || !ExactMessages() || !OwnedStateMatches(RegistryKey, OwnedRegistry)
				|| !OwnedStateMatches(FaultKey, OwnedFault)) return false;
			return RemoveOwned(RegistryKey, OwnedRegistry) && RemoveOwned(FaultKey, OwnedFault)
				&& SameScope() && ExactMessages() && !HasAnyState(Game, RegistryKey) && !HasAnyState(Game, FaultKey);
		}

		private static object[] StateDictionaries(XRLGame Game)
		{
			return new object[] { Game?.StringGameState, Game?.IntGameState, Game?.Int64GameState,
				Game?.BooleanGameState, Game?.ObjectGameState };
		}

		private static string Describe(Exception Error)
		{
			string kind = Error.GetType().Name;
			try { return kind + ": " + KingdomScenarioRules.Bounded(Error.Message); }
			catch { return kind; }
		}

		private static string Append(string Failure, string Detail)
		{
			return string.IsNullOrEmpty(Failure) ? Detail : Failure + "; " + Detail;
		}
	}
}
