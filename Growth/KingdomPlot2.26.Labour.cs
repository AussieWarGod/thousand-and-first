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
		public static void Advance(r_KingdomPlotWorks Works, KingdomSystem System, long TimeTick)
		{
			if (Works == null || Works.DesignKey == null)
			{
				return;
			}
			KingdomPlotRules.PlotStage target;
			GameObject parent = Works.ParentObject;
			int schema = parent == null ? 0 : parent.GetIntProperty(PlotWorkSchemaProperty);
			if (schema == 0)
			{
				// Compatibility only. New Stake always writes schema 2.
				target = KingdomPlotRules.StageAt(TimeTick - Works.StartTick, Works.TotalTicks);
			}
			else if (schema != PlotWorkSchema
				|| !TryAdvancePlotLabour(Works, System, TimeTick, out target))
			{
				if (schema != PlotWorkSchema)
				{
					SayPlotWorkFault(System, parent, "The plot work has an unknown labour receipt and cannot advance safely.");
				}
				return;
			}
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
			KingdomSystem System, long TimeTick, out KingdomPlotRules.PlotStage Target)
		{
			Target = (KingdomPlotRules.PlotStage)Works.StageApplied;
			GameObject parent = Works.ParentObject;
			if (parent == null
				|| !TryGetPlotWorkLong(parent, PlotWorkRequiredProperty, out long required)
				|| !TryGetPlotWorkLong(parent, PlotWorkRemainingProperty, out long remaining)
				|| !TryGetPlotWorkLong(parent, PlotWorkLastTickProperty, out long last)
				|| required < 1L || remaining < 0L || remaining > required || last < 0L)
			{
				SayPlotWorkFault(System, parent,
					"The plot work's labour receipt is incomplete or contradictory; it has been left unchanged.");
				return false;
			}

			long completed = required - remaining;
			Target = KingdomPlotRules.StageAt(completed, required);
			if (remaining == 0L || TimeTick <= last)
			{
				return true;
			}

			// Spend the interval before sampling work. No hands means no progress, but the same
			// empty interval can never be claimed after hands arrive. Craft-district infrastructure
			// is already frozen into required duration; authored layout infrastructure joins the
			// second percentage when the architecture receipt supplies it.
			int effectiveness = KingdomConstructionPresence.EffectivenessOf(parent, System,
				out int freeHands, out bool selected);
			if (selected) SayPlotWorkShortfall(System, parent, Works.DisplayName, freeHands);
			ArchitectureLabourProgress progress = KingdomArchitectureRules.AdvanceLabour(
				last, TimeTick, remaining, effectiveness, 100);
			SetPlotWorkLong(parent, PlotWorkLastTickProperty, progress.NextTick);
			remaining = progress.RemainingTicks;
			SetPlotWorkLong(parent, PlotWorkRemainingProperty, remaining);
			if (progress.Complete)
				SetPlotWorkLong(parent, PlotWorkCompletedTickProperty, progress.CompletionTick);
			Target = KingdomPlotRules.StageAt(required - remaining, required);
			return true;
		}

		private static void SayPlotWorkShortfall(KingdomSystem System, GameObject Works,
			string DisplayName, int FreeHands)
		{
			if (System == null || !System.Founded || Works == null) return;
			string line = KingdomRules.RaisingShortfallLine(DisplayName ?? "structure", FreeHands);
			if (line == null)
			{
				Works.SetIntProperty(PlotWorkShortfallSaidProperty, 0);
				return;
			}
			if (Works.GetIntProperty(PlotWorkShortfallSaidProperty) == 1) return;
			Works.SetIntProperty(PlotWorkShortfallSaidProperty, 1);
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
				currentAuthored = KingdomArchitectureRules.IsCurrentSnapshotEncoding(
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
			return true;
		}

	}
}
