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
			/// <summary>Phase 2: the production-built tent is a standing settlement work the
			/// improvement pass can see, and the registry's quote is exactly the frozen one.
			/// </summary>
			private void StandingTent()
			{
				GameObject tent = Standing(FromKey);
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
				KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
				Require(survey != null, "the settlement could not be surveyed");
				bool listed = false;
				for (int i = 0; i < survey.Built.Count; i++)
					if (ReferenceEquals(survey.Built[i], tent)) listed = true;
				Require(listed, "the standing tent is not in the survey the pass enumerates");
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
					.Append("; successor-key=").Append(chain.SuccessorKey)
					.Append("; bill-brush=").Append(bill.Get(KingdomMaterial.Brush));
			}

			/// <summary>Phase 4: the settlement pass paid for and began the improvement. The
			/// physical debit is measured against the frozen quote, not merely reported.</summary>
			private void PaidAndBegun()
			{
				GameObject tent = Exact(PredecessorId);
				Require(tent != null, "the predecessor tent left its exact identity before work");
				ImprovementJobId = tent.GetStringProperty(KingdomConstruction.ReceiptProperty);
				Require(!string.IsNullOrEmpty(ImprovementJobId),
					"the settlement pass bound no construction receipt to the tent");
				KingdomConstructionJob job;
				Require(KingdomConstruction.TryFind(ImprovementJobId, out job) && job != null
					&& job.Route == KingdomConstructionRoute.Improvement
					&& job.TargetKey == ToKey && job.SubjectId == PredecessorId
					&& KingdomConstruction.Owns(System, Zone, job),
					"the improvement job identity differs from the paid tier climb");
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
			}

			/// <summary>Phase 5: the successor stands, its predecessor is provably gone, and the
			/// paid receipt is closed. The predecessor's absence is proved by the typed physical
			/// lookup, not by a census of what happens to be alive.</summary>
			private void HandedOverAndRetired()
			{
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
				Require(KingdomConstruction.TryFind(ImprovementJobId, out job) && job != null
					&& job.Phase == KingdomConstructionPhase.Complete
					&& job.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved
					&& job.OutputId == SuccessorId,
					"the improvement receipt did not close on its exact successor");
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

			private GameObject Exact(string Id)
			{
				GameObject found;
				return KingdomConstruction.FindExactId(Zone, Id, out found)
					== KingdomPhysicalLookupState.Exact ? found : null;
			}
		}
	}
}
