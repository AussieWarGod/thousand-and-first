using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static bool HasFoundingHeartTerminalEvidence(KingdomFoundingHeartPlan Plan, Zone Z)
		{
			string key = FoundingHeartFinalRootKey(Plan);
			return !string.IsNullOrEmpty(Z?.GetZoneProperty(FoundingHeartTerminalProperty, null))
				|| The.Game?.ObjectGameState.ContainsKey(key) == true
				|| FindGlobalFoundingHeartId(FoundingHeartFinalId(Plan), out _, out _)
					!= KingdomPhysicalLookupState.Absent;
		}

		private static bool RecoverSealedFoundingHeart(KingdomSystem System, Zone Z,
			FoundingHeartContext Context)
		{
			if (System == null || Z == null || !ExactFoundingHeartSeal(Z, Context?.Plan))
				return HeartRefused("sealed: context or seal");
			if (!HasFoundingHeartTerminalEvidence(Context.Plan, Z))
			{
				// The same two clauses, each named. A sealed heart whose works slot cannot be
				// found exactly is the shape an authored improvement leaves behind when it
				// replaces the root, and the log could not tell that from a lost authority.
				if (FindGlobalFoundingHeartId(KingdomFoundingHeartRules.SlotId(Context.Plan,
						KingdomFoundingHeartRules.WorksSlot), out GameObject works,
						out bool graveyard) != KingdomPhysicalLookupState.Exact || graveyard)
					// The one shape this is not: a sealed heart whose root climbed a rung by the
					// improvement route, which replaces the object and cannot carry the reserved
					// identity forward. Proved on its own terms, or refused as before.
					return HasClimbedFoundingHeartRoot(System, Z, Context)
						|| HeartRefused("sealed: works slot lookup");
				return TryReadFoundingHeartWorkAuthority(Z, works, out _)
					|| HeartRefused("sealed: work authority");
			}
			return DriveFoundingHeartTerminal(System, Z, Context, null, null, 0L, null, false);
		}

		private static bool FinishFoundingHeart(r_KingdomPlotWorks Works, KingdomSystem System,
			Zone Z, FoundingHeartContext Context, string DisplayName, long CompleteTick,
			string PlanQuote, bool Yielding)
		{
			GameObject predecessor = Works?.ParentObject;
			if (!TryReadFoundingHeartWorkAuthority(Z, predecessor, out FoundingHeartContext exact)
				|| exact.Plan.TransactionId != Context?.Plan.TransactionId) return false;
			return DriveFoundingHeartTerminal(System, Z, exact, predecessor, DisplayName,
				CompleteTick, PlanQuote, Yielding);
		}

		private static bool DriveFoundingHeartTerminal(KingdomSystem System, Zone Z,
			FoundingHeartContext Context, GameObject Predecessor, string DisplayName,
			long CompleteTick, string PlanQuote, bool Yielding)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			if (System == null || Z == null || !ExactFoundingHeartSeal(Z, plan)) return false;
			KingdomFoundingHeartTerminalPlan terminal;
			GameObject final;
			bool freshRemovalAttempt = false;
			if (!HasFoundingHeartTerminalEvidence(plan, Z))
			{
				if (!BeginFoundingHeartTerminal(Z, Context, Predecessor, DisplayName,
					CompleteTick, PlanQuote, Yielding, out terminal, out final)) return false;
			}
			else if (!TryReadFoundingHeartTerminal(Z, Context, out terminal, out final)) return false;
			if (!RepairFoundingHeartFinalIntent(Z, final, Context, terminal)) return false;
			if (!ExactPreparedFoundingHeartFinal(final, Z, Context, terminal)) return false;

			if (terminal.Phase == KingdomFoundingHeartTerminalPhase.OutputPrepared)
			{
				Cell cell = Z.GetCell(terminal.X, terminal.Y);
				bool callbackReturned = false;
				GameObject accepted = null;
				if (final.CurrentCell == null && final.InInventory == null)
				{
					try { accepted = cell?.AddObject(final, NoStack: true); callbackReturned = true; }
					catch { }
					finally { KingdomSurvey.ObserveAddResultInActive(Z, final, accepted); }
				}
				bool exactEndpoint = ExactSettledFoundingHeartFinal(final, Z, Context, terminal);
				bool exactRoot = ExactFoundingHeartFinalObjectGameState(plan, final, true);
				if (!KingdomFoundingHeartTerminalRules.ExactAddCut(callbackReturned,
					object.ReferenceEquals(accepted, final), exactEndpoint, exactRoot)) return false;
				if (!AdvanceFoundingHeartTerminal(Z, Context, final, ref terminal,
					KingdomFoundingHeartTerminalPhase.OutputPrepared,
					KingdomFoundingHeartTerminalPhase.OutputSettled)) return false;
			}
			if (terminal.Phase == KingdomFoundingHeartTerminalPhase.OutputSettled)
			{
				if (!AdvanceFoundingHeartTerminal(Z, Context, final, ref terminal,
					KingdomFoundingHeartTerminalPhase.OutputSettled,
					KingdomFoundingHeartTerminalPhase.RemovalAttempting)) return false;
				freshRemovalAttempt = true;
			}
			if (terminal.Phase == KingdomFoundingHeartTerminalPhase.RemovalAttempting
				&& !SettleFoundingHeartPredecessor(Z, Context, final, ref terminal,
					freshRemovalAttempt)) return false;
			if (terminal.Phase == KingdomFoundingHeartTerminalPhase.Removed)
			{
				if (!r_KingdomScaffold.HasRemovalProof(final, terminal.PredecessorId)
					|| !AdvanceFoundingHeartTerminal(Z, Context, final, ref terminal,
						KingdomFoundingHeartTerminalPhase.Removed,
						KingdomFoundingHeartTerminalPhase.EffectsAttempting)) return false;
			}
			if (terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsAttempting
				&& !SettleFoundingHeartEffects(System, Z, Context, final, ref terminal)) return false;
			if (terminal.Phase != KingdomFoundingHeartTerminalPhase.EffectsSettled
				|| !ExactSettledFoundingHeartFinal(final, Z, Context, terminal)
				|| !ExactFoundingHeartRetiredAuthority(Z, terminal.PredecessorId, out _)) return false;
			return RetireFoundingHeartFinalRoot(plan, final)
				&& ExactFoundingHeartFinalObjectGameState(plan, final, false);
		}

		private static bool RepairFoundingHeartFinalIntent(Zone Z, GameObject Final,
			FoundingHeartContext Context, KingdomFoundingHeartTerminalPlan Terminal)
		{
			if (Terminal.Phase == KingdomFoundingHeartTerminalPhase.RemovalAttempting)
				return !r_KingdomScaffold.HasRemovalProof(Final, Terminal.PredecessorId)
					|| ExactFoundingHeartRetiredAuthority(Z, Terminal.PredecessorId, out _);
			if (Terminal.Phase >= KingdomFoundingHeartTerminalPhase.Removed)
				return ExactFoundingHeartRetiredAuthority(Z, Terminal.PredecessorId, out _);
			KingdomPhysicalLookupState state = FindGlobalFoundingHeartId(Terminal.PredecessorId,
				out GameObject predecessor, out bool graveyard);
			if (state == KingdomPhysicalLookupState.Absent) return true;
			if (state != KingdomPhysicalLookupState.Exact || graveyard
				|| !FoundingHeartIdentity(predecessor, Context.Plan,
					KingdomFoundingHeartRules.WorksSlot)) return false;
			if (FoundingHeartPropertyAbsent(predecessor, FinalOutputIdProperty))
				predecessor.SetStringProperty(FinalOutputIdProperty, Terminal.FinalId);
			return ExactFoundingHeartString(predecessor, FinalOutputIdProperty, Terminal.FinalId);
		}

		private static bool BeginFoundingHeartTerminal(Zone Z, FoundingHeartContext Context,
			GameObject Predecessor, string DisplayName, long CompleteTick, string PlanQuote,
			bool Yielding, out KingdomFoundingHeartTerminalPlan Terminal, out GameObject Final)
		{
			Terminal = null;
			Final = null;
			KingdomFoundingHeartPlan plan = Context?.Plan;
			string finalId = FoundingHeartFinalId(plan);
			Cell cell = Z?.GetCell(Context.Architecture.MainWorldX, Context.Architecture.MainWorldY);
			if (!TryReadFoundingHeartWorkAuthority(Z, Predecessor, out _)
				|| cell == null || FindGlobalFoundingHeartId(finalId, out _, out _)
					!= KingdomPhysicalLookupState.Absent
				|| The.Game?.ObjectGameState.ContainsKey(FoundingHeartFinalRootKey(plan)) == true)
				return HeartRefused("terminal: predecessor or root preflight");
			FoundingHeartAllocationFence fence = new FoundingHeartAllocationFence(Z, Context);
			if (!fence.Current) return HeartRefused("terminal: allocation authority");
			try { Final = fence.Create(Context.Stake.Blueprint); }
			catch { return HeartRefused("terminal: final factory threw"); }
			if (!UnplacedFoundingHeartOutput(Final, Context.Stake.Blueprint) || !fence.Current)
				return HeartRefused("terminal: final factory or custody");
			bool published = false;
			try
			{
				Final.IDIfAssigned = finalId;
				if (!KingdomArchitectureStamper.TryCopyFrozenOwner(Predecessor, Final, out string copyFailure))
					return HeartRefused("terminal: layout copy: " + copyFailure);
				if (!KingdomPurpose.CopyCommit(Predecessor, Final)) return HeartRefused("terminal: purpose copy");
				KingdomPlotRules.PlotRect foot = new KingdomPlotRules.PlotRect(Context.Stake.FootprintX1,
					Context.Stake.FootprintY1, Context.Stake.FootprintX2, Context.Stake.FootprintY2);
				PrepareFinalBuilding(Final, Context.Entry, null, plan.PlotId, Context.Rect, foot,
					(KingdomPlotRules.RoofState)Context.Stake.Roof, null, null, null, null,
					DisplayName ?? Context.Stake.DisplayName, CompleteTick, PlanQuote, true,
					Yielding, Context.Stake.Defence, Context.Stake.Staff,
					Context.Stake.ThresholdManning);
				if (!KingdomFoundingHeartTerminalRules.TryCreate(plan.TransactionId,
					KingdomFoundingHeartRules.CompletionSeal(plan), plan.ZoneId,
					KingdomFoundingHeartRules.SlotId(plan, KingdomFoundingHeartRules.WorksSlot),
					finalId, Context.Stake.Blueprint, Context.Stake.BuildKey, plan.PlotId,
					cell.X, cell.Y, out Terminal)) return HeartRefused("terminal: authority plan");
				string encoded = KingdomFoundingHeartTerminalRules.Encode(Terminal);
				Final.SetStringProperty(FoundingHeartTerminalProperty, encoded);
				if (!fence.Current) return HeartRefused("terminal: prepared allocation authority");
				if (!ExactPreparedFoundingHeartFinal(Final, Z, Context, Terminal))
					return HeartRefused("terminal: prepared final shape");
				if (!RootFoundingHeartFinal(plan, Final)) return HeartRefused("terminal: saved root");
				published = true;
				if (!PublishFoundingHeartTerminal(Z, Final, Context, Terminal, null))
					return HeartRefused("terminal: mirrored authority");
				Predecessor.SetStringProperty(FinalOutputIdProperty, finalId);
				return Predecessor.GetStringProperty(FinalOutputIdProperty) == finalId;
			}
			finally
			{
				if (!published && fence.Current && UnplacedFoundingHeartOutput(Final, Context.Stake.Blueprint)
					&& ExactFoundingHeartFinalObjectGameState(plan, Final, false)
					&& Terminal != null && ExactPreparedFoundingHeartFinal(Final, Z, Context, Terminal)) RemoveCreatedWorks(Final, Z);
			}
		}

		private static bool ExactPreparedFoundingHeartFinal(GameObject Final, Zone Z,
			FoundingHeartContext Context, KingdomFoundingHeartTerminalPlan Terminal)
		{
			if (!GameObject.Validate(Final) || !FoundingHeartTerminalBinding(Context, Terminal)
				|| Final.IDIfAssigned != Terminal.FinalId || Final.Blueprint != Terminal.Blueprint
				|| Final.GetStringProperty(FoundingHeartTerminalProperty)
					!= KingdomFoundingHeartTerminalRules.Encode(Terminal)
				|| !ExactFoundingHeartFinalShape(Final, Context.Stake)
				|| Final.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Terminal.BuildKey
				|| Final.GetStringProperty(PlotIdProperty) != Terminal.PlotId
					|| !TryReadStampedRect(Final, out KingdomPlotRules.PlotRect rect)
					|| !SameRect(rect, Context.Rect)
					|| !KingdomPlotRules.ValidZoneRect(rect, Z.Width, Z.Height)
					|| !ExactFoundingHeartInt(Final, FootX1Property, Context.Stake.FootprintX1)
					|| !ExactFoundingHeartInt(Final, FootY1Property, Context.Stake.FootprintY1)
					|| !ExactFoundingHeartInt(Final, FootX2Property, Context.Stake.FootprintX2)
					|| !ExactFoundingHeartInt(Final, FootY2Property, Context.Stake.FootprintY2)
					|| !ExactFoundingHeartInt(Final, PlotRoofProperty, Context.Stake.Roof)
					|| !KingdomArchitectureRuntime.TryRead(Final, out KingdomArchitectureIntent intent, out _)
				|| !SameIntent(intent, Context.Architecture)
				|| !KingdomArchitectureStamper.TryReadOwner(Final, out _, out _, out string lot, out _)
				|| lot != Context.Plan.PlotId) return false;
			return Final.CurrentCell == null
				? Final.CurrentZone == null && Final.InInventory == null
				: Final.CurrentCell == Z.GetCell(Terminal.X, Terminal.Y)
					&& Final.CurrentZone == Z && Final.InInventory == null;
		}

		private static bool ExactSettledFoundingHeartFinal(GameObject Final, Zone Z,
			FoundingHeartContext Context, KingdomFoundingHeartTerminalPlan Terminal)
		{
			KingdomPlotRules.PlotRect foot = new KingdomPlotRules.PlotRect(Context.Stake.FootprintX1,
				Context.Stake.FootprintY1, Context.Stake.FootprintX2, Context.Stake.FootprintY2);
			return ExactPreparedFoundingHeartFinal(Final, Z, Context, Terminal)
				&& ExactFinalBuilding(Final, Z, Z.GetCell(Terminal.X, Terminal.Y), Context.Entry,
					null, Terminal.PlotId, Context.Rect, foot,
					(KingdomPlotRules.RoofState)Context.Stake.Roof, Context.Architecture, false, null)
				&& ExactFoundingHeartFinalTruth(Final, Context.Stake);
		}

		private static bool AdvanceFoundingHeartTerminal(Zone Z, FoundingHeartContext Context,
			GameObject Final, ref KingdomFoundingHeartTerminalPlan Terminal,
			KingdomFoundingHeartTerminalPhase Expected, KingdomFoundingHeartTerminalPhase Next)
		{
			string prior = KingdomFoundingHeartTerminalRules.Encode(Terminal);
			var changed = Terminal.Copy();
			if (!ExactFoundingHeartFinalObjectGameState(Context.Plan, Final, true)
				|| !KingdomFoundingHeartTerminalRules.TryAdvancePhase(changed, Expected, Next)
				|| !PublishFoundingHeartTerminal(Z, Final, Context, changed, prior)) return false;
			Terminal = changed;
			return true;
		}
	}
}
