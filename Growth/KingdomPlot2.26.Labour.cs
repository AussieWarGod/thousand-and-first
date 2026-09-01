using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		/// <summary>Canonical prior-interval witness; named property keeps shipped part ABI fixed.</summary>
		public const string PlotWorkWindowProperty = "r_TAF_PlotWorkWindow";
		private const int PlotWorkLabourSaid = 1;
		private const int PlotWorkInfrastructureSaid = 2;

		/// <summary>Compatibility entry. Current work fails closed until settlement infrastructure
		/// authority calls the full overload; schema-zero plots retain their frozen calendar.</summary>
		public static void Advance(r_KingdomPlotWorks Works, KingdomSystem System, long TimeTick)
		{
			Advance(Works, System, TimeTick,
				KingdomPlotLabourWindowRules.InfrastructureUnavailable,
				"The plot's construction infrastructure was not witnessed on its owner ground.");
		}

		public static void Advance(r_KingdomPlotWorks Works, KingdomSystem System, long TimeTick,
			int InfrastructurePercent, string InfrastructureFailure)
		{
			if (Works == null || Works.DesignKey == null)
			{
				return;
			}
			KingdomPlotRules.PlotStage target;
			if (!TryAdvancePlotLabour(Works, System, TimeTick, InfrastructurePercent,
				InfrastructureFailure, out target)) return;
			if ((int)target <= Works.StageApplied)
			{
				return;
			}
			KingdomSystem.Guard("plot raising", delegate
			{
				while (Works.StageApplied < (int)target && Works.DesignKey != null)
				{
					KingdomPlotRules.PlotStage next = (KingdomPlotRules.PlotStage)(Works.StageApplied + 1);
					if (!Apply(Works, next))
					{
						// The stage could not land -- a design a third-party mod withdrew between
						// staking and finishing, or a zone torn down under us. The plot stays
						// exactly where it is and tries again, which is the same "waiting is not
						// failing" contract a staked plan already holds.
						break;
					}
					Works.StageApplied = (int)next;
				}
			});
		}

		private static bool TryAdvancePlotLabour(r_KingdomPlotWorks Works,
			KingdomSystem System, long TimeTick, int InfrastructurePercent,
			string InfrastructureFailure, out KingdomPlotRules.PlotStage Target,
			bool PricePriorWitness = true, bool CaptureCurrentWitness = true)
		{
			Target = (KingdomPlotRules.PlotStage)Works.StageApplied;
			GameObject parent = Works.ParentObject;
			KingdomPlotLabourReceipt receipt = new KingdomPlotLabourReceipt
			{
				Schema = parent == null ? KingdomPlotLabourRules.LegacySchema
					: parent.GetIntProperty(PlotWorkSchemaProperty),
				LegacyStartTick = Works.StartTick,
				LegacyTotalTicks = Works.TotalTicks
			};
			receipt.HasRequiredTicks = TryGetPlotWorkLong(parent,
				PlotWorkRequiredProperty, out receipt.RequiredTicks);
			receipt.HasRemainingTicks = TryGetPlotWorkLong(parent,
				PlotWorkRemainingProperty, out receipt.RemainingTicks);
			receipt.HasLastTick = TryGetPlotWorkLong(parent,
				PlotWorkLastTickProperty, out receipt.LastTick);
			KingdomPlotLabourStep step = KingdomPlotLabourRules.Assess(receipt, TimeTick);
			if (step.Verdict == KingdomPlotLabourVerdict.Invalid)
			{
				SayPlotWorkFault(System, parent, step.Failure);
				return false;
			}
			Target = KingdomPlotRules.StageAt(step.CompletedTicks, step.RequiredTicks);
			if (step.Verdict != KingdomPlotLabourVerdict.Attended || step.Complete) return true;
			if (TimeTick < receipt.LastTick) return true;
			if (TimeTick == receipt.LastTick)
				return CaptureCurrentWitness
					? TryCapturePlotLabourWindow(parent, System, TimeTick,
						InfrastructurePercent, InfrastructureFailure, Works.DisplayName)
					: TryCaptureZeroPlotLabourWindow(parent, System, TimeTick, InfrastructureFailure);

			// Prior loaded facts price the elapsed interval. Current loaded facts are captured only
			// for the next one, so a settler seated on this wake cannot work the preceding absence.
			KingdomPlotLabourWindow prior = null;
			bool witnessed = PricePriorWitness && KingdomPlotLabourWindowRules.TryForInterval(
				parent.GetStringProperty(PlotWorkWindowProperty), receipt.LastTick, out prior);
			step = KingdomPlotLabourRules.Advance(receipt, TimeTick,
				witnessed ? prior.LabourPercent : 0,
				witnessed ? prior.InfrastructurePercent : 0);
			if (!step.WriteReceipt) return false;
			SetPlotWorkLong(parent, PlotWorkLastTickProperty, step.NextTick);
			SetPlotWorkLong(parent, PlotWorkRemainingProperty, step.RemainingTicks);
			if (step.Complete)
			{
				SetPlotWorkLong(parent, PlotWorkCompletedTickProperty, step.CompletionTick);
				parent.RemoveStringProperty(PlotWorkWindowProperty);
			}
			Target = KingdomPlotRules.StageAt(step.CompletedTicks, step.RequiredTicks);
			if (step.Complete) return true;
			return CaptureCurrentWitness
				? TryCapturePlotLabourWindow(parent, System, TimeTick,
					InfrastructurePercent, InfrastructureFailure, Works.DisplayName)
				: TryCaptureZeroPlotLabourWindow(parent, System, TimeTick, InfrastructureFailure);
		}

		/// <summary>Burns an unauthorized interval at zero without applying any physical stage.</summary>
		internal static void ConsumePlotLabourAtZero(r_KingdomPlotWorks Works,
			KingdomSystem System, long TimeTick, string Failure)
		{
			KingdomPlotRules.PlotStage ignored;
			TryAdvancePlotLabour(Works, System, TimeTick,
				KingdomPlotLabourWindowRules.InfrastructureUnavailable, Failure,
				out ignored, false, false);
		}

		private static void SayPlotWorkShortfall(KingdomSystem System, GameObject Works,
			string DisplayName, int FreeHands)
		{
			if (System == null || !System.Founded || Works == null) return;
			string line = KingdomRules.RaisingShortfallLine(DisplayName ?? "structure", FreeHands);
			if (line == null)
			{
				Works.SetIntProperty(PlotWorkShortfallSaidProperty,
					Works.GetIntProperty(PlotWorkShortfallSaidProperty) & ~PlotWorkLabourSaid);
				return;
			}
			int said = Works.GetIntProperty(PlotWorkShortfallSaidProperty);
			if ((said & PlotWorkLabourSaid) != 0) return;
			Works.SetIntProperty(PlotWorkShortfallSaidProperty, said | PlotWorkLabourSaid);
			System.Ledger.Note("{{r|" + line + "}}");
		}

		private static void SayPlotWorkFault(KingdomSystem System, GameObject Works, string Failure)
		{
			if (Works != null && Works.GetIntProperty(PlotWorkFaultSaidProperty) == 1) return;
			if (Works != null) Works.SetIntProperty(PlotWorkFaultSaidProperty, 1);
			KingdomLog.Log("plot labour: " + Failure);
			if (System != null && System.Founded) System.Ledger.Note("{{r|" + Failure + "}}");
		}

		private static void SetPlotWorkLong(GameObject Object, string Property, long Value)
		{
			Object?.SetStringProperty(Property, Value.ToString(
				global::System.Globalization.CultureInfo.InvariantCulture));
		}

		private static bool TryGetPlotWorkLong(GameObject Object, string Property, out long Value)
		{
			Value = 0L;
			return Object != null && long.TryParse(Object.GetStringProperty(Property),
				global::System.Globalization.NumberStyles.Integer,
				global::System.Globalization.CultureInfo.InvariantCulture, out Value);
		}

		private static bool Apply(r_KingdomPlotWorks Works, KingdomPlotRules.PlotStage Stage)
		{
			GameObject parent = Works.ParentObject;
			Zone zone = parent?.CurrentZone;
			if (zone == null)
			{
				return false;
			}
			bool foundingHeart = FoundingHeartWorkIdentityEvidence(parent);
			int priorStage = Works.StageApplied;
			if (foundingHeart
				&& !TryReadFoundingHeartWorkAuthority(zone, parent, out _)) return false;
			KingdomPlotRules.PlotRect plot = Works.Rect();
			KingdomPlotRules.PlotRect footprint = TryReadFootprint(parent, out var stamped) ? stamped : plot;
			KingdomPlotRules.RoofState roof = RoofOf(parent);
			KingdomArchitectureIntent authored = null;
			bool currentAuthored = false;
			if (HasArchitectureReceiptEvidence(parent))
			{
				if (!KingdomArchitectureRuntime.TryRead(parent, out authored,
					out string receiptFailure))
				{
					KingdomLog.Log("architecture: plot stage refused: " + receiptFailure);
					return false;
				}
				currentAuthored = KingdomArchitectureRules.IsManagedSnapshotEncoding(
					authored.EncodedSnapshot);
				if (currentAuthored && (!KingdomArchitectureStamper.TryReadOwner(parent,
					out _, out _, out string lot, out receiptFailure)
					|| lot != parent.GetStringProperty(PlotIdProperty)))
				{
					KingdomLog.Log("architecture: plot layout receipt refused: " + receiptFailure);
					return false;
				}
			}
			switch (Stage)
			{
				case KingdomPlotRules.PlotStage.Cleared:
					HashSet<int> managed = null;
					if (currentAuthored && !KingdomArchitectureStamper.TryManagedCells(authored,
						zone, out managed, out string managedFailure))
					{
						KingdomLog.Log("architecture: authored clearance refused: " + managedFailure);
						return false;
					}
					if (!ClearGround(Works, zone, plot, footprint, roof, managed)) return false;
					if (currentAuthored && !KingdomArchitectureStamper.TryStageLayer(parent,
						zone, ArchitectureLayer.Ground, out string groundFailure))
					{
						KingdomLog.Log("architecture: ground layer refused: " + groundFailure);
						return false;
					}
					break;
				case KingdomPlotRules.PlotStage.Frame:
					if (currentAuthored)
					{
						if (!KingdomArchitectureStamper.TryStageLayer(parent, zone,
							ArchitectureLayer.Structure, out string structureFailure))
						{
							KingdomLog.Log("architecture: structure layer refused: "
								+ structureFailure);
							return false;
						}
					}
					else RaiseFrame(Works, zone, footprint, roof);
					break;
				case KingdomPlotRules.PlotStage.Walls:
					if (currentAuthored)
					{
						if (!KingdomArchitectureStamper.TryStageLayer(parent, zone,
							ArchitectureLayer.Object, out string objectFailure))
						{
							KingdomLog.Log("architecture: object layer refused: " + objectFailure);
							return false;
						}
					}
					else RaiseWalls(Works, zone, footprint, roof);
					break;
				case KingdomPlotRules.PlotStage.Done:
					if (currentAuthored && !KingdomArchitectureStamper.TryVerifyComplete(
						parent, zone, out string completeFailure))
					{
						KingdomLog.Log("architecture: incomplete authored plot refused: "
							+ completeFailure);
						return false;
					}
					return Finish(Works, zone, plot, footprint, roof);
			}
			string line = KingdomPlotRules.StageLine(Stage, Works.DisplayName ?? "work");
			if (line != null && parent.IsValid() && zone.IsActive())
			{
				MessageQueue.AddPlayerMessage("{{W|" + line + "}}");
			}
			return !foundingHeart || Works.StageApplied == priorStage
				&& TryReadFoundingHeartWorkAuthority(zone, parent, out _);
		}

	}
}
