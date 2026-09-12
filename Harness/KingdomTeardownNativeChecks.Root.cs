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
