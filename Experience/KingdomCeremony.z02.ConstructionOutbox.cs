using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomCeremony
	{

		public static bool EnsureRoadPaved(KingdomSystem System, int Cells,
			KingdomMaterial Material, ref KingdomConstructionJob Job)
		{
			if (System == null || Cells <= 0) return false;
			return EnsureRouteOutbox(System, "paved",
				KingdomRoadRules.PavedRecord(Cells, Material, KingdomPresentation.Rich(System.KingdomDisplayName)), null,
				KingdomRoadRules.PavedLine(Cells, Material, KingdomPresentation.Rich(System.SeatName)),
				"the paving of the ways at " + KingdomPresentation.Rich(System.SeatName), ref Job);
		}

		public static bool EnsureRoadPavedFromReceipt(KingdomSystem System,
			ref KingdomConstructionJob Job)
		{
			if (Job == null || Job.Route != KingdomConstructionRoute.RoadPaving) return false;
			List<KingdomConstructionCell> cells;
			KingdomMaterialDebitCost cost;
			if (!KingdomConstructionRules.TryDecodeCells(Job.Payload, out cells)
				|| Job.Claims == null || !KingdomMaterialDebitCost.TryParseClaim(
					Job.Claims.MaterialRequested, out cost)) return false;
			int found = 0;
			KingdomMaterial material = (KingdomMaterial)(-1);
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial candidate = (KingdomMaterial)i;
				if (cost.Materials.Get(candidate) <= 0) continue;
				found++;
				material = candidate;
			}
			if (found != 1 || !cost.Bits.IsEmpty() || !cost.Exotics.IsEmpty()) return false;
			return EnsureRoadPaved(System, cells.Count, material, ref Job);
		}

		public static bool EnsureTerminalClosed(KingdomSystem System,
			ref KingdomConstructionJob Job)
		{
			if (System == null || Job == null || !KingdomConstructionRules.IsTerminal(Job.Phase)
				|| Job.Phase == KingdomConstructionPhase.Complete) return false;
			if (Job.Outbox != null) return KingdomConstructionRules.OutboxSettled(Job.Outbox);
			KingdomConstructionOutbox box = new KingdomConstructionOutbox
			{
				EventId = "construction:" + Job.Id + ":closed", Mode = 1,
				ChronicleState = KingdomConstructionSinkDisposition.Skipped,
				LedgerState = KingdomConstructionSinkDisposition.Skipped,
				MessageState = KingdomConstructionSinkDisposition.Skipped,
				DeedState = KingdomConstructionSinkDisposition.Skipped
			};
			return KingdomConstruction.UpdateOutbox(ref Job, box);
		}

		/// <summary>Wear-owned caller freezes optional leak closure before removing its wear part.</summary>
		public static bool EnsureWearRepaired(KingdomSystem System, string WorkName,
			string LeakStoppedLine, ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(WorkName)) return false;
			string line = KingdomWearRules.RepairCompleteLine(WorkName);
			string held = string.IsNullOrEmpty(LeakStoppedLine) ? null
				: "{{G|" + XRL.Language.Grammar.InitCap(LeakStoppedLine) + "}}";
			string message = "{{G|" + line + "}}";
			if (held != null) message += "\n" + held;
			return EnsureRouteOutbox(System, "mended", line, held, message, null, ref Job);
		}

		/// <summary>Freeze Wear telling before its part-removal callback; do not dispatch yet.</summary>
		public static bool PrepareWearRepaired(KingdomSystem System, string WorkName,
			string LeakStoppedLine, ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(WorkName)) return false;
			string line = KingdomWearRules.RepairCompleteLine(WorkName);
			string held = string.IsNullOrEmpty(LeakStoppedLine) ? null
				: "{{G|" + XRL.Language.Grammar.InitCap(LeakStoppedLine) + "}}";
			string message = "{{G|" + line + "}}" + (held == null ? "" : "\n" + held);
			return PublishRouteOutbox(System, "mended", line, held, message, null, ref Job);
		}

		public static bool EnsureSocketRedressed(KingdomSystem System, string DisplayName,
			string SkinKey, ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(DisplayName)
				|| string.IsNullOrEmpty(SkinKey)) return false;
			return EnsureRouteOutbox(System, "redressed",
				"the " + DisplayName + " at " + KingdomPresentation.Rich(System.KingdomDisplayName)
					+ " was given a new coat, dressed as " + SkinKey,
				null, "{{G|The " + DisplayName + " is re-dressed.}}", null, ref Job);
		}

		public static bool PrepareSocketRedressed(KingdomSystem System, string DisplayName,
			string SkinKey, ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(DisplayName)
				|| string.IsNullOrEmpty(SkinKey)) return false;
			return PublishRouteOutbox(System, "redressed",
				"the " + DisplayName + " at " + KingdomPresentation.Rich(System.KingdomDisplayName)
					+ " was given a new coat, dressed as " + SkinKey,
				null, "{{G|The " + DisplayName + " is re-dressed.}}", null, ref Job);
		}

		public static bool EnsureSocketStaked(KingdomSystem System, string DisplayName,
			ref KingdomConstructionJob Job)
		{
			if (System == null || string.IsNullOrEmpty(DisplayName)) return false;
			return PublishRouteOutbox(System, "socket-staked",
				"the cleared ground at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " was staked again for "
					+ XRL.Language.Grammar.A(DisplayName), null,
				"{{G|The cleared plot is staked for " + XRL.Language.Grammar.A(DisplayName) + ".}}",
				null, ref Job) && Dispatch(System, ref Job);
		}

		private static bool EnsureRouteOutbox(KingdomSystem System, string Suffix,
			string Chronicle, string Ledger, string Message, string Deed,
			ref KingdomConstructionJob Job)
		{
			if (System == null || !System.Founded || Job == null
				|| Job.Phase != KingdomConstructionPhase.Complete
				|| string.IsNullOrEmpty(Suffix) || string.IsNullOrEmpty(Chronicle)) return false;
			return PublishRouteOutbox(System, Suffix, Chronicle, Ledger, Message, Deed,
				ref Job) && Dispatch(System, ref Job);
		}

		private static bool PublishRouteOutbox(KingdomSystem System, string Suffix,
			string Chronicle, string Ledger, string Message, string Deed,
			ref KingdomConstructionJob Job)
		{
			if (System == null || !System.Founded || Job == null
				|| string.IsNullOrEmpty(Suffix) || string.IsNullOrEmpty(Chronicle)) return false;
			string eventId = "construction:" + Job.Id + ":" + Suffix;
			if (Job.Outbox != null && Job.Outbox.EventId != eventId)
			{
				if (!KingdomConstructionRules.OutboxSettled(Job.Outbox)
					|| !KingdomConstruction.UpdateOutbox(ref Job, null)) return false;
			}
			if (Job.Outbox == null)
			{
				KingdomConstructionOutbox box = new KingdomConstructionOutbox
				{
					EventId = eventId, Mode = 1,
					Chronicle = Chronicle,
					ChronicleState = KingdomConstructionSinkDisposition.Pending,
					Ledger = Ledger,
					LedgerState = Ledger == null ? KingdomConstructionSinkDisposition.Skipped
						: KingdomConstructionSinkDisposition.Pending,
					Message = Message,
					MessageState = Message == null ? KingdomConstructionSinkDisposition.Skipped
						: KingdomConstructionSinkDisposition.Pending,
					Deed = Deed,
					DeedState = Deed == null ? KingdomConstructionSinkDisposition.Skipped
						: KingdomConstructionSinkDisposition.Pending
				};
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
			}
			return Job.Outbox.EventId == eventId;
		}

		private static bool Dispatch(KingdomSystem System, ref KingdomConstructionJob Job)
		{
			if (System == null || Job == null || Job.Outbox == null) return false;
			KingdomConstructionOutbox box = Job.Outbox.Copy();

			// Uninspectable sinks: an interrupted Attempting state is explicit loss, never retry.
			if (box.DeedState == KingdomConstructionSinkDisposition.Attempting)
			{
				box.DeedState = KingdomConstructionSinkDisposition.Lost;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
			}
			if (box.DeedState == KingdomConstructionSinkDisposition.Pending)
			{
				box.DeedState = KingdomConstructionSinkDisposition.Attempting;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				try
				{
					System.RecordDeed(box.Deed);
					box.DeedState = KingdomConstructionSinkDisposition.Delivered;
					if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				}
				catch { return false; }
			}

			// RecordOnce owns exact inspection and may be called again after an interrupted attempt.
			if (box.ChronicleState == KingdomConstructionSinkDisposition.Pending
				|| box.ChronicleState == KingdomConstructionSinkDisposition.Attempting)
			{
				box.ChronicleState = KingdomConstructionSinkDisposition.Attempting;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				try
				{
					if (!KingdomChronicle.RecordOnce(System, box.EventId + ":chronicle",
						box.Chronicle, Job.Route == KingdomConstructionRoute.WearRepair)) return false;
					box.ChronicleState = KingdomConstructionSinkDisposition.Delivered;
					if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				}
				catch { return false; }
			}

			if (box.LedgerState == KingdomConstructionSinkDisposition.Pending)
			{
				try
				{
					if (System.Ledger == null || System.Ledger.Notes == null) return false;
					if (!KingdomConstructionRules.TryFreezeLedger(System.Ledger.Notes, box.Ledger,
						out box.LedgerBeforeCount, out box.LedgerBeforeHash,
						out box.LedgerAfterCount, out box.LedgerAfterHash))
					{
						box.LedgerState = KingdomConstructionSinkDisposition.Lost;
						if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
					}
					else
					{
						box.LedgerState = KingdomConstructionSinkDisposition.Attempting;
						if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
					}
				}
				catch { return false; }
			}
			if (box.LedgerState == KingdomConstructionSinkDisposition.Attempting)
			{
				try
				{
					if (System.Ledger == null || System.Ledger.Notes == null) return false;
					KingdomConstructionCasAction action = KingdomConstructionRules.LedgerCasAction(
						System.Ledger.Notes, box.LedgerBeforeCount, box.LedgerBeforeHash,
						box.LedgerAfterCount, box.LedgerAfterHash);
					if (action == KingdomConstructionCasAction.Quarantine)
					{
						box.LedgerState = KingdomConstructionSinkDisposition.Lost;
						return KingdomConstruction.UpdateOutbox(ref Job, box);
					}
					if (action == KingdomConstructionCasAction.Apply)
					{
						System.Ledger.Note(box.Ledger);
						action = KingdomConstructionRules.LedgerCasAction(System.Ledger.Notes,
							box.LedgerBeforeCount, box.LedgerBeforeHash,
							box.LedgerAfterCount, box.LedgerAfterHash);
						if (action != KingdomConstructionCasAction.Confirm) return false;
					}
					box.LedgerState = KingdomConstructionSinkDisposition.Delivered;
					if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				}
				catch { return false; }
			}

			if (box.MessageState == KingdomConstructionSinkDisposition.Attempting)
			{
				box.MessageState = KingdomConstructionSinkDisposition.Lost;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
			}
			if (box.MessageState == KingdomConstructionSinkDisposition.Pending)
			{
				box.MessageState = KingdomConstructionSinkDisposition.Attempting;
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				try
				{
					MessageQueue.AddPlayerMessage(box.Message);
					box.MessageState = KingdomConstructionSinkDisposition.Delivered;
					if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
				}
				catch { return false; }
			}
			return KingdomConstructionRules.OutboxSettled(Job.Outbox);
		}
	}
}
