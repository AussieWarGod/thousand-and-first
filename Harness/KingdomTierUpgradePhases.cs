using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The three observation phases of the ordinary tier upgrade. Each one only READS what the
	/// real settlement pass did during the preceding advance; none of them drives the upgrade.
	/// </summary>
	internal static partial class KingdomTierUpgradeChecks
	{
		/// <summary>Frozen quote for tent -&gt; tentrow, re-derived by production into
		/// <c>KingdomUpgrade.Assessment</c> and pinned here so a catalogue or district drift is a
		/// refusal rather than a silently absorbed number.</summary>
		internal const int QuoteDrams = 2;

		internal const long QuoteTicks = 900L;
		internal const int QuoteCrew = 1;
		internal const int QuoteBrush = 2;

		private sealed partial class Frame
		{
			/// <summary>First check, after leg one: the tent is the completed output of the real
			/// commission, a standing settlement work the improvement pass enumerates, and the
			/// real passes began nothing on it while its bill was short. The registry's quote is
			/// exactly the frozen one.</summary>
			private void StandingTent()
			{
				GameObject tent = Standing(FromKey);
				if (tent == null) RecordUnbuilt();
				Require(tent != null, "no completed settlement tent stands after the build wait");
				Require(tent.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
					&& tent.GetIntProperty(KingdomUpgrade.AdoptedProperty) != 1,
					"the standing tent is not unadopted settlement work");
				Require(tent.GetIntProperty(
					r_KingdomScaffold.PendingImprovementSuccessorProperty) != 1,
					"the standing tent still carries pending-improvement state");
				PredecessorId = tent.IDIfAssigned;
				Require(!string.IsNullOrEmpty(PredecessorId),
					"the standing tent has no assigned identity");
				KingdomConstructionJob commission;
				Require(KingdomConstruction.TryFind(TentJobId, out commission)
					&& commission != null
					&& commission.Phase == KingdomConstructionPhase.Complete
					&& commission.OutputId == PredecessorId
					&& tent.GetStringProperty(KingdomConstruction.ReceiptProperty) == TentJobId,
					"the standing tent is not the completed output of the real commission, or "
						+ "another job is bound to it");
				var held = tent.GetPart<r_KingdomImprovement>();
				Require(held == null || !held.Working,
					"an improvement began on the tent while its bill was short");
				string missing;
				Require(!KingdomMaterials.CanPayUpgrade(Zone, FromKey, out missing),
					"the upgrade bill became payable before the shortfall leg supplied it");
				Require(OnLocalSurvey(survey => Lists(survey.Built, tent)),
					"the standing tent is not in the survey the pass enumerates");
				KingdomUpgradeRules.UpgradeChain chain;
				Require(KingdomUpgrade.TryGetChain(FromKey, out chain) && chain != null
					&& chain.SuccessorKey == ToKey,
					"the catalogue declares no tent -> tentrow chain");
				KingdomMaterialTally bill = KingdomMaterials.UpgradeCostFor(FromKey);
				Require(bill != null && bill.Get(KingdomMaterial.Brush) == QuoteBrush,
					"the upgrade material bill is not the frozen canvas:2");
				for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				{
					KingdomMaterial material = (KingdomMaterial)i;
					Require(material == KingdomMaterial.Brush || bill.Get(material) == 0,
						"the upgrade bill asks for a material beyond canvas: " + material);
				}
				Evidence.Append("\nstanding tent=").Append(PredecessorId)
					.Append("; design=").Append(KingdomUpgrade.DesignKeyOf(tent))
					.Append("; commission=").Append(TentJobId)
					.Append("; successor-key=").Append(chain.SuccessorKey)
					.Append("; bill-brush=").Append(bill.Get(KingdomMaterial.Brush))
					.Append("; pass-announced=").Append(held == null ? "none"
						: ((KingdomUpgradeRules.UpgradeVerdict)held.AnnouncedReason).ToString());
			}

			/// <summary>Second check, after leg two: the settlement pass paid for and began the
			/// improvement. The physical debit is measured against the frozen quote.</summary>
			private void PaidAndBegun()
			{
				GameObject tent = Exact(PredecessorId);
				Require(tent != null, "the predecessor tent left its exact identity before work");
				ImprovementJobId = tent.GetStringProperty(KingdomConstruction.ReceiptProperty);
				bool bound = !string.IsNullOrEmpty(ImprovementJobId) && ImprovementJobId != TentJobId;
				if (!bound) RecordUnbegun(tent);
				Require(bound, "the settlement pass bound no improvement receipt to the tent");
				KingdomConstructionJob job;
				Require(KingdomConstruction.TryFind(ImprovementJobId, out job) && job != null
					&& job.Route == KingdomConstructionRoute.Improvement
					&& job.TargetKey == ToKey && job.SubjectId == PredecessorId
					&& KingdomConstruction.Owns(System, Zone, job),
					"the improvement job identity differs from the paid tier climb");
				bool scanned;
				string waited = BeginWaitNote(out scanned);
				RecordBound(job, scanned);
				Require(waited == null, "the funded begin waited before its scaffold stood: "
					+ KingdomScenarioRules.Bounded(waited));
				KingdomMaterialTally bill = new KingdomMaterialTally();
				bill.Add(KingdomMaterial.Brush, QuoteBrush);
				Require(KingdomQuickstartBuildClaims.CleanFirstPayment(job.Claims, QuoteDrams,
					new KingdomMaterialDebitCost(bill)),
					"the improvement claims differ from the frozen two-dram canvas:2 quote");
				Require(job.Phase == KingdomConstructionPhase.Working,
					"the paid improvement is not working: " + job.Phase);
				var improvement = tent.GetPart<r_KingdomImprovement>();
				Require(improvement != null && improvement.Working
					&& improvement.SuccessorKey == ToKey
					&& improvement.WorkCompleteTick == job.DueTick,
					"the predecessor carries no working improvement bound to its job");
				GameObject scaffold = improvement.Scaffold;
				Require(GameObject.Validate(scaffold)
					&& scaffold.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == ToKey,
					"the raised scaffold does not carry the successor build key");
				Evidence.Append("\nbegun job=").Append(ImprovementJobId)
					.Append("; drams=").Append(QuoteDrams)
					.Append("; brush=").Append(QuoteBrush)
					.Append("; due=").Append(job.DueTick)
					.Append("; scaffold=").Append(scaffold.IDIfAssigned);
				RecordBegunMarks(tent);
			}

			/// <summary>Why no completed tent stands after leg one, journaled before the refusal:
			/// the commission receipt's phase, projection, due tick and recorded failure, the
			/// works' stage, the crew inputs and the ledger tail. docs/DEVELOPMENT.md asks for the
			/// recorded construction reason before any longer wait: an unpriced window, a short
			/// gang and a blocked completion each look the same from the outside.</summary>
			private void RecordUnbuilt()
			{
				KingdomConstructionJob job;
				bool found = KingdomConstruction.TryFind(TentJobId, out job) && job != null;
				Evidence.Append("\nunbuilt tick=").Append(Game.TimeTicks)
					.Append("; job=").Append(TentJobId)
					.Append("; phase=").Append(found ? job.Phase.ToString() : "absent");
				if (found)
				{
					Evidence.Append("; physical=").Append(job.PhysicalPhase)
						.Append("; projection=").Append(job.Projection)
						.Append("; due=").Append(job.DueTick)
						.Append("; updated=").Append(job.UpdatedTick)
						.Append("; failure=").Append(KingdomScenarioRules.Bounded(job.Failure));
					GameObject works = string.IsNullOrEmpty(job.OutputId) ? null : Exact(job.OutputId);
					var plot = works?.GetPart<r_KingdomPlotWorks>();
					if (plot != null)
						Evidence.Append("; works-stage=").Append(plot.StageApplied)
							.Append("; works-start=").Append(plot.StartTick)
							.Append("; works-total=").Append(plot.TotalTicks);
				}
				Evidence.Append("; population=").Append(System.Population)
					.Append("; assigned=").Append(System.AssignedCrew).Append(LedgerTail());
			}

			/// <summary>Why the ready-looking begin never happened (docs/DEVELOPMENT.md readiness
			/// rule), journaled before the refusal: a fresh bound production assessment with its
			/// verdict and reason, the pass inputs it was asked with, the tent's last announced
			/// verdict and the ledger tail. Begin's zoning and contents waits return before any
			/// receipt is bound and reach only the in-memory ledger
			/// (<c>Growth/KingdomUpgrade.14.Begin.cs</c>), never Player.log, so this row is the one
			/// place they can surface. An outstanding-funding wait binds its receipt first, so the
			/// ledger scan journaled with <c>RecordBound</c> reads that one instead.</summary>
			private void RecordUnbegun(GameObject Tent)
			{
				string context;
				KingdomUpgrade.Assessment fresh = Assess(Tent, out context);
				Evidence.Append("\nunbegun verdict=").Append(fresh.Verdict)
					.Append("; reason=").Append(KingdomScenarioRules.Bounded(fresh.Reason))
					.Append("; ").Append(context).Append(LedgerTail());
			}

			/// <summary>The bound receipt as production left it, journaled before its payment and
			/// phase are required: phase, recorded failure, measured claims, publication ticks and
			/// revision, whether the scan for Begin's wait sentences saw every ledger note, and the
			/// ledger tail. A first-pass begin makes every funding and projection publication in
			/// the tick the job was created (<c>Growth/KingdomUpgrade.14.Begin.cs</c>).
			/// </summary>
			private void RecordBound(KingdomConstructionJob Job, bool Scanned)
			{
				KingdomConstructionClaims claims = Job.Claims;
				Evidence.Append("\nbound job=").Append(Job.Id)
					.Append("; phase=").Append(Job.Phase)
					.Append("; physical=").Append(Job.PhysicalPhase)
					.Append("; failure=").Append(KingdomScenarioRules.Bounded(Job.Failure))
					.Append("; created=").Append(Job.CreatedTick)
					.Append("; updated=").Append(Job.UpdatedTick)
					.Append("; revision=").Append(Job.Revision)
					.Append("; water=").Append(claims == null ? "absent"
						: claims.WaterSpent + "/" + claims.WaterRequested + " outstanding "
							+ claims.WaterOutstanding)
					.Append("; material-outstanding=").Append(claims == null ? "absent"
						: KingdomScenarioRules.Bounded(claims.MaterialOutstanding))
					.Append("; wait-scan=").Append(Scanned ? "every-note" : "ledger-at-cap")
					.Append(LedgerTail());
			}

			/// <summary>Final check, after leg three: the successor stands, its predecessor is
			/// provably gone, and the paid receipt is closed and settled. The receipt passes
			/// FinalRemoved when the predecessor's absence is committed
			/// (<c>Growth/KingdomUpgrade.25.HandoverRemoval.cs</c>) and the completion telling
			/// then settles it at EffectsSettled in the same recovery
			/// (<c>Growth/KingdomScaffold.CompletionAndLegacy.cs</c>, <c>TellCompletion</c>), the
			/// terminal state every natively-run improvement witness reads
			/// (<c>Harness/KingdomPaidHousingWitness.cs</c>). The predecessor's absence is proved
			/// by the typed physical lookup, not by a census of what happens to be alive.</summary>
			private void HandedOverAndRetired()
			{
				RecordAfterWait();
				GameObject successor = Standing(ToKey);
				Require(successor != null, "no completed tent-row stands after the handover wait");
				SuccessorId = successor.IDIfAssigned;
				Require(!string.IsNullOrEmpty(SuccessorId) && SuccessorId != PredecessorId,
					"the successor has no identity of its own");
				Require(successor.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
					&& successor.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == ToKey,
					"the successor lacks its built-state and build-key proof");
				Require(successor.GetIntProperty(
					r_KingdomScaffold.PendingImprovementSuccessorProperty) != 1
					&& KingdomUpgrade.IsFunctionallyBuilt(successor),
					"the successor never retired its pending-improvement marker");
				Require(successor.GetStringProperty(r_KingdomScaffold.RemovalProofProperty)
					== PredecessorId,
					"the successor carries no exact predecessor-removal proof");
				GameObject absent;
				Require(KingdomConstruction.FindExactId(Zone, PredecessorId, out absent)
					== KingdomPhysicalLookupState.Absent && absent == null,
					"the retired predecessor is still physically present");
				KingdomConstructionJob job;
				Require(KingdomConstruction.TryFind(ImprovementJobId, out job) && job != null,
					"the improvement receipt is absent");
				Require(job.Phase == KingdomConstructionPhase.Complete
					&& job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
					&& job.SubjectId == PredecessorId && job.OutputId == SuccessorId,
					"the improvement receipt did not close and settle on its exact endpoints: "
						+ job.Phase + "/" + job.PhysicalPhase);
				var improvement = successor.GetPart<r_KingdomImprovement>();
				Require(improvement == null || (!improvement.HandoverQuarantined
					&& string.IsNullOrEmpty(improvement.HandoverFailure)),
					"the successor carries a quarantined or failed handover receipt");
				Require(!string.IsNullOrEmpty(
					successor.GetStringProperty(r_KingdomScaffold.CompletionNameProperty)),
					"the successor carries no completion telling");
				Require(KingdomPlotRules.HeartRungOf(ToKey) == 0
					&& successor.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1,
					"the ordinary climb settled a founding-heart rung");
				RequireCarriedMarksAndCleanSeal(successor);
				Evidence.Append("\nsuccessor=").Append(SuccessorId)
					.Append("; predecessor=").Append(PredecessorId)
					.Append("; predecessor-lookup=Absent")
					.Append("; job-phase=").Append(job.Phase)
					.Append("; physical-phase=").Append(job.PhysicalPhase);
			}

			/// <summary>The one functionally built settlement work in this zone whose production
			/// design key is <paramref name="Key" />; refuses on ambiguity.</summary>
			internal GameObject Standing(string Key)
			{
				GameObject found = null;
				foreach (GameObject item in Zone.GetObjects())
				{
					if (!GameObject.Validate(item) || !KingdomUpgrade.IsFunctionallyBuilt(item)
						|| KingdomUpgrade.DesignKeyOf(item) != Key) continue;
					Require(found == null, "more than one standing '" + Key + "' stands here");
					found = item;
				}
				return found;
			}

			private static bool Lists(List<GameObject> Rows,
				GameObject Item)
			{
				for (int i = 0; i < Rows.Count; i++) if (ReferenceEquals(Rows[i], Item)) return true;
				return false;
			}

			private GameObject Exact(string Id)
			{
				GameObject found;
				return KingdomConstruction.FindExactId(Zone, Id, out found)
					== KingdomPhysicalLookupState.Exact ? found : null;
			}
		}
	}
}
