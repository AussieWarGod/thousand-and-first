using System.Collections.Generic;
using System.Text;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomTeardownNativeChecks
	{
		/// <summary>Run 43: the plot's CURRENT root, re-resolved every Check through the
		/// engine-free KingdomTeardownRootResolution over the live job row (Job.OutputId names
		/// the final building once production re-roots the paid output), the survey's Built set
		/// (a built object carrying this job's construction receipt, for a compacted row), and
		/// only then the works root captured at commission time.</summary>
		private sealed partial class Case
		{
			/// <summary>The id the last Check read, and which source named it; journaled as
			/// final=/root-source=. The struck object's id is kept for the removal proof.</summary>
			private string CurrentId, CurrentSource, StruckId;

			private GameObject ResolveCurrentRoot()
			{
				KingdomConstructionJob row = null;
				bool found = !string.IsNullOrEmpty(JobId)
					&& KingdomConstruction.TryFind(JobId, out row) && row != null;
				bool complete = found && row.Phase == KingdomConstructionPhase.Complete;
				string receiptHolder = null;
				if (!found)
				{
					KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
					for (int i = 0; i < survey.Built.Count; i++)
					{
						GameObject built = survey.Built[i];
						if (GameObject.Validate(built) && built.GetStringProperty(
							KingdomConstruction.ReceiptProperty) == JobId)
						{ receiptHolder = built.IDIfAssigned; break; }
					}
				}
				CurrentId = KingdomTeardownRootResolution.Choose(WorksId, found, complete,
					found ? row.OutputId : null, receiptHolder, out CurrentSource);
				return Zone.FindObjectByID(CurrentId);
			}

			/// <summary>Run 45: may the strike be ordered on this built root now? Reads the
			/// building's real construction receipt and its row, asks the production supersede
			/// predicate, and journals receipt=/receipt-source=/receipt-phase=/supersedable=.
			/// WaitClosure is a wait, never a refusal: the checkpoint budget bounds it.</summary>
			private KingdomTeardownStrikeReadiness.Verdict StrikeReadiness(GameObject Built,
				StringBuilder Evidence)
			{
				string receipt = Built.GetStringProperty(KingdomConstruction.ReceiptProperty);
				bool present = !string.IsNullOrEmpty(receipt);
				KingdomConstructionJob row = null;
				bool found = present && KingdomConstruction.TryFind(receipt, out row) && row != null;
				bool terminal = found && KingdomConstructionRules.IsTerminal(row.Phase);
				bool supersedable = terminal
					&& KingdomConstruction.CanSupersedeTerminalReceipt(System, Zone, Built, row);
				KingdomTeardownStrikeReadiness.Verdict verdict = KingdomTeardownStrikeReadiness.Judge(
					true, present, found, terminal, supersedable);
				Evidence.Append("; case=").Append(Name)
					.Append(" receipt=").Append(present ? receipt : "none")
					.Append(" receipt-source=").Append(KingdomTeardownStrikeReadiness.ReceiptSource(present, found))
					.Append(" receipt-phase=").Append(found ? row.Phase.ToString() : "no-row")
					.Append(" supersedable=").Append(supersedable)
					.Append(" strike-readiness=").Append(verdict)
					.Append(" synthetic-bill=stock-only");
				return verdict;
			}

			/// <summary>What the resolved root says about itself: read from THAT object, never
			/// echoed from the job row. Larder-gate: the Frame starts the larder only once the
			/// fire case reaches Phase 2 (struck), so an unstruck fire says so here.</summary>
			private string RootClause(GameObject Root)
			{
				return new StringBuilder()
					.Append(" final=").Append(string.IsNullOrEmpty(CurrentId) ? "unassigned" : CurrentId)
					.Append(" root-source=").Append(CurrentSource ?? "unread")
					.Append(" blueprint=").Append(Root == null ? "absent" : Root.Blueprint)
					.Append(" design-key=").Append(Root == null ? "absent"
						: (KingdomUpgrade.DesignKeyOf(Root) ?? "none"))
					.Append(" kingdom-built=").Append(Root == null ? -1 : Root.GetIntProperty("KingdomBuilt"))
					.Append(" larder-gate=").Append(Name).Append("-phase:").Append(Phase)
					.Append("/needs:2")
					.ToString();
			}
		}
	}
}
