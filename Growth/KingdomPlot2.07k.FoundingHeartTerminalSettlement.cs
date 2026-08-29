using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static bool SettleFoundingHeartPredecessor(Zone Z, FoundingHeartContext Context,
			GameObject Final, ref KingdomFoundingHeartTerminalPlan Terminal, bool FreshAttempt)
		{
			if (!FreshAttempt)
			{
				if (r_KingdomScaffold.HasRemovalProof(Final, Terminal.PredecessorId)
					&& ExactFoundingHeartRetiredAuthority(Z, Terminal.PredecessorId, out _))
					return AdvanceFoundingHeartTerminal(Z, Context, Final, ref Terminal,
						KingdomFoundingHeartTerminalPhase.RemovalAttempting,
						KingdomFoundingHeartTerminalPhase.Removed);
				return QuarantineFoundingHeartTerminal(Z, Final,
					"Founding-heart removal reloaded without a durable callback-success proof.");
			}
			KingdomPhysicalLookupState before = FindGlobalFoundingHeartId(Terminal.PredecessorId,
				out GameObject predecessor, out bool graveyard);
			if (before != KingdomPhysicalLookupState.Exact || graveyard
				|| !TryReadFoundingHeartWorkAuthority(Z, predecessor, out _))
				return QuarantineFoundingHeartTerminal(Z, Final,
					"Founding-heart removal did not begin from its exact live predecessor.");
			bool returned = false;
			bool removed = false;
			try { removed = predecessor.Destroy(null, Silent: true); returned = true; }
			catch { }
			finally { KingdomSurvey.ObserveCurrentTopologyInActive(Z, predecessor); }
			if (!ExactFoundingHeartFinalObjectGameState(Context.Plan, Final, true)
				|| !KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(returned, removed,
				GameObject.Validate(predecessor), KingdomConstruction.FindExactId(Z,
					Terminal.PredecessorId, out _) == KingdomPhysicalLookupState.Absent,
				ExactGraveyardTombstone(Terminal.PredecessorId, predecessor)))
				return QuarantineFoundingHeartTerminal(Z, Final,
					"Founding-heart removal callback did not produce its exact tombstone.");
			KingdomSurvey.ObserveRemovedFromActive(Z, predecessor);
			Final.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, Terminal.PredecessorId);
			if (!r_KingdomScaffold.HasRemovalProof(Final, Terminal.PredecessorId)
				|| !ExactFoundingHeartRetiredAuthority(Z, Terminal.PredecessorId, out _)
				|| !ExactFoundingHeartFinalTruth(Final, Context.Stake)) return false;
			return AdvanceFoundingHeartTerminal(Z, Context, Final, ref Terminal,
				KingdomFoundingHeartTerminalPhase.RemovalAttempting,
				KingdomFoundingHeartTerminalPhase.Removed);
		}

		private static bool SettleFoundingHeartEffects(KingdomSystem System, Zone Z,
			FoundingHeartContext Context, GameObject Final,
			ref KingdomFoundingHeartTerminalPlan Terminal)
		{
			string buildKey = Terminal.BuildKey;
			string rung = KingdomPlotRules.HeartRungOf(buildKey).ToString(
				global::System.Globalization.CultureInfo.InvariantCulture);
			try { Z.SetZoneProperty(HeartRungProperty, rung); }
			catch
			{
				if (Z.GetZoneProperty(HeartRungProperty, null) != rung) return false;
			}
			if (Z.GetZoneProperty(HeartRungProperty, null) != rung) return false;
			if (!DriveFoundingHeartSink(Z, Context, Final, ref Terminal, false,
				() => KingdomCeremony.OnBuildingRaised(System, Final.CurrentCell,
					Final.GetStringProperty(r_KingdomScaffold.CompletionNameProperty),
					ReadFoundingHeartCompletionTick(Final),
						Final.GetStringProperty(r_KingdomScaffold.CompletionPlanProperty)))) return false;
			if (!DriveFoundingHeartSink(Z, Context, Final, ref Terminal, true,
					() => KingdomCeremonyHeart.OnRungRaised(System, Z, buildKey, true))) return false;
			return AdvanceFoundingHeartTerminal(Z, Context, Final, ref Terminal,
				KingdomFoundingHeartTerminalPhase.EffectsAttempting,
				KingdomFoundingHeartTerminalPhase.EffectsSettled);
		}

		private static long ReadFoundingHeartCompletionTick(GameObject Final)
		{
			return long.TryParse(Final?.GetStringProperty(
				r_KingdomScaffold.CompletionTickProperty),
				global::System.Globalization.NumberStyles.Integer,
				global::System.Globalization.CultureInfo.InvariantCulture, out long tick) ? tick : 0L;
		}

		private static bool DriveFoundingHeartSink(Zone Z, FoundingHeartContext Context,
			GameObject Final, ref KingdomFoundingHeartTerminalPlan Terminal, bool Heart,
			System.Action Callback)
		{
			KingdomFoundingHeartSinkDisposition state = Heart ? Terminal.Heart : Terminal.Raising;
			if (!ExactFoundingHeartFinalObjectGameState(Context.Plan, Final, true))
				return QuarantineFoundingHeartTerminal(Z, Final,
					"Founding-heart effects lost canonical final custody.");
			if (state == KingdomFoundingHeartSinkDisposition.Settled
				|| state == KingdomFoundingHeartSinkDisposition.Lost) return true;
			if (state == KingdomFoundingHeartSinkDisposition.Attempting)
				return AdvanceFoundingHeartSink(Z, Context, Final, ref Terminal, Heart,
					KingdomFoundingHeartSinkDisposition.Attempting,
					KingdomFoundingHeartSinkDisposition.Lost);
			if (!AdvanceFoundingHeartSink(Z, Context, Final, ref Terminal, Heart,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting)) return false;
			bool callbackReturned = false;
			try { Callback(); callbackReturned = true; }
			catch { }
			if (!ExactFoundingHeartFinalObjectGameState(Context.Plan, Final, true))
				return QuarantineFoundingHeartTerminal(Z, Final,
					"Founding-heart callback changed canonical final custody.");
			if (!callbackReturned) return false;
			return AdvanceFoundingHeartSink(Z, Context, Final, ref Terminal, Heart,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Settled);
		}

		private static bool AdvanceFoundingHeartSink(Zone Z, FoundingHeartContext Context,
			GameObject Final, ref KingdomFoundingHeartTerminalPlan Terminal, bool Heart,
			KingdomFoundingHeartSinkDisposition Expected,
			KingdomFoundingHeartSinkDisposition Next)
		{
			string prior = KingdomFoundingHeartTerminalRules.Encode(Terminal);
			var changed = Terminal.Copy();
			if (!ExactFoundingHeartFinalObjectGameState(Context.Plan, Final, true)
				|| !KingdomFoundingHeartTerminalRules.TryAdvanceSink(changed, Heart, Expected, Next)
				|| !PublishFoundingHeartTerminal(Z, Final, Context, changed, prior)) return false;
			Terminal = changed;
			return true;
		}
	}
}
