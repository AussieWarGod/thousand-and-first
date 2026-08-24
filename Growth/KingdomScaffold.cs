using System;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently.
namespace XRL.World.Parts
{
	/// <summary>
	/// A commissioned building part-way up: the frame that stands on the ground between the
	/// order and the raising.
	/// <para>
	/// <b>It rises on labour, not on the calendar</b> (BUILDING-CATALOGUE-BRIEF.md Addendum 8
	/// clause 2, and the author's ruling that a scaffold nobody works on does not rise). The
	/// duration a design authors in <c>BuildTicks</c> is what a properly-crewed settlement takes
	/// &mdash; <see cref="KingdomRules.RaisingHandsWanted"/> free pairs of hands &mdash; and it
	/// is banked once into <see cref="RemainingTicks"/> the first time this frame is looked at.
	/// After that, every stretch of elapsed time buys labour ticks at the pace the settlement's
	/// spare hands can actually manage (<see cref="KingdomRules.RaisingEffectiveness"/>), and a
	/// settlement with nobody free raises nothing at all, however long the founder is away.
	/// </para>
	/// <para>
	/// Idle time is SPENT, never banked: <see cref="LastWorkedTick"/> advances whether or not
	/// anyone stood here, exactly as an unstaffed yard's day budget does. A settlement that
	/// emptied out and refilled does not get the empty months back as a burst of building. And
	/// because a shortfall is a thing the founder can act on, it says so once and unsays itself
	/// the moment the crew is whole again (STANDARDS 7b).
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomScaffold : IPart
	{
		public string TargetBlueprint;

		public string TargetDisplayName;

		/// <summary>
		/// Stamped by every commissioning path as the tick this would be finished at if it were
		/// fully crewed from the moment it was ordered, and read once at that value to bank the
		/// authored labour into <see cref="RemainingTicks"/>. Restamped, when the work actually
		/// runs out, to the tick it ACTUALLY ran out at &mdash; which is what the raising
		/// ceremony needs to know whether the founder was standing there for it
		/// (<c>KingdomCeremonyRules.IsAttended</c>). A frame that finished halfway through an
		/// absence is told in the homecoming, exactly as before; one that finishes under the
		/// founder's eye gathers the crew.
		/// </summary>
		public long CompleteTick;

		/// <summary>Labour ticks left to raise this. Zero before the frame has been looked at
		/// once; derived then from <see cref="CompleteTick"/>, so no commissioning path has to
		/// know that raising takes hands.</summary>
		public long RemainingTicks;

		/// <summary>Tick labour was last charged against this frame, or 0 before the first
		/// look.</summary>
		public long LastWorkedTick;

		/// <summary>Whether the founder has already been told this raising is short-handed, so
		/// the reason is given once per stall rather than every turn (STANDARDS 7b).</summary>
		public bool ShortfallSaid;

		public int StaffNeeded;

		public bool ThresholdManning;

		public const string RemovalProofProperty = "KingdomConstructionPredecessorRemoved";
		/// <summary>Named-object property holding exact retry identity for receiptless legacy
		/// scaffolds. This must not become a reflected part field: shipped saves serialize the
		/// public fields of this part positionally through <c>IComponent.Write</c>.</summary>
		public const string LegacySuccessorIdProperty = "KingdomConstructionLegacySuccessorId";
		public const string TellingProperty = "KingdomConstructionTold";
		public const string CompletionNameProperty = "KingdomConstructionCompletionName";
		public const string CompletionTickProperty = "KingdomConstructionCompletionTick";
		public const string CompletionPlanProperty = "KingdomConstructionCompletionPlan";
		public const string FinalPendingProperty = "KingdomConstructionFinalPending";

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			// Receipt-bearing work advances only from KingdomConstruction.OnSettlementPass.
			// Receiptless scaffolds from old saves retain their legacy turn-tick path.
			if (TargetBlueprint != null && string.IsNullOrEmpty(
				ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty)))
			{
				if (AdvanceLabour(TimeTick)) CompleteLegacy();
			}
			base.TurnTick(TimeTick, Amount);
		}

		/// <summary>
		/// Charges the labour the settlement's spare hands did on this frame since it was last
		/// looked at, and finishes it when the work runs out.
		/// <para>
		/// A turn tick only fires in an active zone, so an absence arrives here as one long
		/// stretch resolved at the moment of awareness &mdash; the lazy catch-up the whole mod
		/// keeps. The crew is sampled once, now, because now is the only honest reading there
		/// is: nobody recorded who was standing in an unwatched city.
		/// </para>
		/// </summary>
		private bool AdvanceLabour(long TimeTick)
		{
			if (RemainingTicks <= 0 && LastWorkedTick <= 0)
			{
				long authored = CompleteTick - TimeTick;
				RemainingTicks = (authored > 0) ? authored : 1L;
				LastWorkedTick = TimeTick;
				return false;
			}
			if (RemainingTicks <= 0) return true;
			long previous = LastWorkedTick;
			long elapsed = TimeTick - previous;
			if (elapsed <= 0)
			{
				return false;
			}
			LastWorkedTick = TimeTick;
			int effectiveness = EffectivenessOf(out var freeHands, out var system);
			Say(system, freeHands);
			long worked = KingdomRules.LabouredTicks(elapsed, effectiveness);
			if (worked <= 0 || RemainingTicks <= 0)
			{
				return false;
			}
			if (worked < RemainingTicks)
			{
				RemainingTicks -= worked;
				return false;
			}
			// The work ran out somewhere inside this stretch, and WHERE matters: it decides
			// whether this was a raising the founder attended or one the homecoming reports.
			// Ticks needed at the pace just measured, rounded up, laid back down from the last
			// stamp.
			long spent = (effectiveness >= 100) ? RemainingTicks : (RemainingTicks * 100L + effectiveness - 1L) / effectiveness;
			long finished = previous + spent;
			CompleteTick = (finished > TimeTick || finished < previous) ? TimeTick : finished;
			RemainingTicks = 0;
			return true;
		}

		/// <summary>Advances one exact receipt-bearing scaffold from the semantic pass.</summary>
		public void AdvanceDurable(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job, long TimeTick)
		{
			if (!ExactPredecessor(System, Z, Job)) return;
			bool ready = RemainingTicks <= 0 && LastWorkedTick > 0;
			if (!ready && Job.Phase == KingdomConstructionPhase.Working)
			{
				ready = AdvanceLabour(TimeTick);
			}
			if (!ready && Job.Phase != KingdomConstructionPhase.ProjectionPending) return;
			ContinueDurable(System, Z, Job);
		}

		/// <summary>Retries a finished scaffold without charging another labour interval.</summary>
		public void RetryDurable(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (RemainingTicks > 0 || LastWorkedTick <= 0 || !ExactPredecessor(System, Z, Job)) return;
			ContinueDurable(System, Z, Job);
		}

		private void ContinueDurable(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			KingdomConstructionJob current = Job;
			Cell cell = ParentObject.CurrentCell;
			string blueprint = TargetBlueprint;
			string predecessorId = ParentObject.ID;
			if (!ExactPredecessor(System, Z, current) || cell == null || cell.ParentZone != Z
				|| string.IsNullOrEmpty(blueprint)) return;

			GameObject successor;
			int successorCount = FindExactSuccessors(Z, current, blueprint, ParentObject, out successor);
			if (successorCount > 1)
			{
				KingdomConstruction.Quarantine(ref current,
					"More than one exact successor carries the scaffold receipt.");
				return;
			}
			if (current.Phase == KingdomConstructionPhase.Working
				|| current.Phase == KingdomConstructionPhase.Outstanding)
			{
				if (!KingdomConstruction.BeginProjection(ref current, out _)) return;
			}
			else if (current.Phase != KingdomConstructionPhase.ProjectionPending)
			{
				return;
			}
			int finalPending = ParentObject.GetIntProperty(FinalPendingProperty);
			if (finalPending != 0 && finalPending != 1)
			{
				KingdomConstruction.Quarantine(ref current,
					"The scaffold final-projection flag is not an exact boolean.");
				return;
			}
			ParentObject.SetIntProperty(FinalPendingProperty, 1);
			if (ParentObject.GetIntProperty(FinalPendingProperty) != 1)
			{
				KingdomConstruction.Quarantine(ref current,
					"The scaffold did not retain its final-projection marker.");
				return;
			}
			if (!ExactPredecessor(System, Z, current) || ParentObject.CurrentCell != cell
				|| TargetBlueprint != blueprint || !KingdomConstruction.IsCurrent(current))
			{
				KingdomConstruction.Quarantine(ref current,
					"The scaffold changed across its durable projection boundary.");
				return;
			}

			if (successor == null)
			{
				// A reloaded pending row with no successor is ambiguous. Only the live attempt that
				// just wrote Pending, or a writer-proved Outstanding retry, may create one.
				if (Job.Phase == KingdomConstructionPhase.ProjectionPending)
				{
					KingdomConstruction.Quarantine(ref current,
						"The interrupted final projection has no safely identifiable successor.");
					return;
				}
				try
				{
					successor = GameObject.Create(blueprint);
				}
				catch (Exception ex)
				{
					KingdomConstruction.Quarantine(ref current,
						"The final blueprint threw before creating a successor: " + ex.Message);
					return;
				}
				if (!GameObject.Validate(successor))
				{
					KingdomConstruction.FinishProjection(ref current, false, false,
						"The final blueprint could not create its exact successor.");
					return;
				}
				if (successor.Blueprint != blueprint)
				{
					QuarantineOrRetryAfterAdd(ref current, successor,
						"The final blueprint created an unexpected successor.");
					return;
				}
				if (!KingdomConstruction.UpdateFinalOutput(ref current,
					predecessorId, successor.ID))
				{
						QuarantineOrRetryAfterAdd(ref current, successor,
							"The final successor identity could not be published before AddObject.");
						return;
				}
				try
				{
					PrepareSuccessor(successor, current);
				}
				catch (Exception ex)
				{
					QuarantineOrRetryAfterAdd(ref current, successor,
						"The final successor threw while it was staged: " + ex.Message);
					return;
				}
				try
				{
					cell.AddObject(successor);
					successor.MakeActive();
				}
				catch (Exception ex)
				{
					QuarantineOrRetryAfterAdd(ref current, successor,
						"The final successor threw while entering its cell: " + ex.Message);
					return;
				}
				if (!IsExactSuccessor(successor, Z, cell, current, blueprint))
				{
					QuarantineOrRetryAfterAdd(ref current, successor,
						"The final successor could not be observed exactly after AddObject.");
					return;
				}
			}

			// AddObject and MakeActive are callbacks. Re-read both endpoints before removal.
			if (!ExactPredecessor(System, Z, current) || ParentObject.CurrentCell != cell
				|| TargetBlueprint != blueprint
				|| !IsExactSuccessor(successor, Z, cell, current, blueprint)
				|| !KingdomConstruction.IsCurrent(current))
			{
				KingdomConstruction.Quarantine(ref current,
					"A construction endpoint changed before predecessor removal.");
				return;
			}
			bool removed;
			try
			{
				removed = ParentObject.Destroy(null, Silent: true);
			}
			catch (Exception ex)
			{
				KingdomConstruction.Quarantine(ref current,
					"The scaffold threw during predecessor removal: " + ex.Message);
				return;
			}
			if (!removed || GameObject.Validate(ParentObject))
			{
				if (GameObject.Validate(ParentObject) && ParentObject.CurrentCell == cell
					&& ParentObject.CurrentZone == Z && TargetBlueprint == blueprint)
				{
					KingdomConstruction.FinishProjection(ref current, false, false,
						"The exact successor stands, but scaffold removal was vetoed.");
				}
				else
				{
					KingdomConstruction.Quarantine(ref current,
						"Scaffold removal moved or partially changed the predecessor.");
				}
				return;
			}
			KingdomPhysicalLookupState predecessorState = KingdomConstruction.FindExactId(
				Z, predecessorId, out _);
			if (!KingdomConstruction.Owns(System, Z, current)
				|| predecessorState != KingdomPhysicalLookupState.Absent
				|| TargetBlueprint != blueprint
				|| !IsExactSuccessor(successor, Z, cell, current, blueprint))
			{
				KingdomConstruction.Quarantine(ref current,
					"The successor changed during predecessor removal.");
				return;
			}
			if (!KingdomConstruction.IsCurrent(current)) return;
			successor.SetStringProperty(RemovalProofProperty, predecessorId);
			if (successor.GetStringProperty(RemovalProofProperty) != predecessorId)
			{
				KingdomConstruction.Quarantine(ref current,
					"The successor did not retain predecessor-removal proof.");
				return;
			}

			if (current.Route == KingdomConstructionRoute.Improvement)
			{
				KingdomConstruction.FinishProjection(ref current, true, true);
				return;
			}
			if (KingdomConstruction.Complete(ref current))
			{
				TellCompletion(System, successor, current);
			}
		}

		private void PrepareSuccessor(GameObject Successor, KingdomConstructionJob Job)
		{
			string displayName = TargetDisplayName ?? "structure";
			KingdomConstruction.Bind(Successor, Job);
			KingdomDesign.ApplyRenderOverrides(Successor,
				ParentObject.GetStringProperty(KingdomDesign.StagedColorStringProperty),
				ParentObject.GetStringProperty(KingdomDesign.StagedDetailColorProperty),
				ParentObject.GetStringProperty(KingdomDesign.StagedRenderStringProperty),
				ParentObject.GetStringProperty(KingdomDesign.StagedTileProperty));
			if (Successor.GetPart<LiquidVolume>() != null)
			{
				Successor.SetIntProperty("KingdomStores", 1);
			}
			else if (TargetBlueprint == LarderBlueprint)
			{
				Successor.SetIntProperty("KingdomLarder", 1);
			}
			Successor.SetIntProperty("KingdomBuilt", 1);
			Successor.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Job.TargetKey);
			int defence = ParentObject.GetIntProperty("KingdomDefencePending");
			if (defence > 0) Successor.SetIntProperty("KingdomDefence", defence);
			if (StaffNeeded > 0)
			{
				Successor.SetIntProperty("KingdomStaffNeeded", StaffNeeded);
				if (ThresholdManning) Successor.SetIntProperty("KingdomThresholdManning", 1);
				if (Successor.GetPart<Capacitor>() != null)
					Successor.SetIntProperty("KingdomHandCranked", 1);
			}
			Successor.SetStringProperty(CompletionNameProperty, displayName);
			Successor.SetStringProperty(CompletionTickProperty,
				CompleteTick.ToString(CultureInfo.InvariantCulture));
			string quote = ParentObject.GetStringProperty(KingdomCeremony.SurveyorsPlanProperty);
			if (!string.IsNullOrEmpty(quote)) Successor.SetStringProperty(CompletionPlanProperty, quote);
		}

		private static void QuarantineOrRetryAfterAdd(ref KingdomConstructionJob Job,
			GameObject Successor, string Failure)
		{
			bool removed = false;
			try
			{
				removed = !GameObject.Validate(Successor)
					|| (Successor.Obliterate(null, Silent: true) && !GameObject.Validate(Successor));
			}
			catch
			{
				removed = false;
			}
			if (removed)
			{
				KingdomConstruction.Quarantine(ref Job,
					Failure + " The frozen successor identity was retired and cannot be replaced.");
			}
			else
				KingdomConstruction.Quarantine(ref Job, Failure);
		}

		private bool ExactPredecessor(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			Cell expected = Z == null || Job == null ? null : Z.GetCell(Job.X, Job.Y);
			if (!KingdomConstruction.Owns(System, Z, Job) || !GameObject.Validate(ParentObject)
				|| expected == null || ParentObject.CurrentZone != Z
				|| ParentObject.CurrentCell != expected
				|| !KingdomConstruction.IsCurrent(Job)
				|| !KingdomConstruction.HasReceipt(ParentObject, Job)
				|| ParentObject.GetPart<r_KingdomScaffold>() != this
				|| ParentObject.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Job.TargetKey)
				return false;
			if (Job.Route != KingdomConstructionRoute.Improvement)
			{
				return (Job.Route == KingdomConstructionRoute.CommissionScaffold
					|| Job.Route == KingdomConstructionRoute.PlanScaffold)
					&& ParentObject.ID == Job.SubjectId;
			}
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out work);
			r_KingdomImprovement intent = GameObject.Validate(work)
				? work.GetPart<r_KingdomImprovement>() : null;
			return workState == KingdomPhysicalLookupState.Exact
				&& intent != null && work.CurrentZone == Z && work.CurrentCell == expected
				&& KingdomConstruction.HasReceipt(work, Job)
				&& work.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
				&& (string.IsNullOrEmpty(Job.Payload)
					|| work.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == Job.Payload)
				&& intent.Working && intent.Scaffold == ParentObject
				&& intent.SuccessorKey == Job.TargetKey
				&& intent.SuccessorBlueprint == TargetBlueprint;
		}

		public static int FindExactSuccessors(Zone Z, KingdomConstructionJob Job,
			string Blueprint, GameObject Predecessor, out GameObject Successor)
		{
			Successor = null;
			if (Z == null || Job == null || string.IsNullOrEmpty(Blueprint)) return 0;
			Cell cell = Z.GetCell(Job.X, Job.Y);
			if (cell == null) return 0;
			int count = 0;
			bool conflict = false;
			foreach (GameObject item in cell.GetObjects())
			{
				if (item == Predecessor || !IsMarkedSuccessor(item, Z, cell, Job, Blueprint)) continue;
				if (item.ID != Job.OutputId)
				{
					conflict = true;
					continue;
				}
				if (Successor == null) Successor = item;
				count++;
			}
			if (conflict || count > 1) return 2;
			if (count == 1)
			{
				GameObject global;
				if (KingdomConstruction.FindExactId(Z, Job.OutputId, out global)
					!= KingdomPhysicalLookupState.Exact
					|| !ReferenceEquals(global, Successor)) return 2;
			}
			return count;
		}

		public static bool IsExactSuccessor(GameObject Successor, Zone Z, Cell Cell,
			KingdomConstructionJob Job, string Blueprint)
		{
			return IsMarkedSuccessor(Successor, Z, Cell, Job, Blueprint)
				&& !string.IsNullOrEmpty(Job.OutputId) && Successor.ID == Job.OutputId;
		}

		private static bool IsMarkedSuccessor(GameObject Successor, Zone Z, Cell Cell,
			KingdomConstructionJob Job, string Blueprint)
		{
			return Z != null && Job != null && Cell != null
				&& Cell == Z.GetCell(Job.X, Job.Y)
				&& GameObject.Validate(Successor) && Successor.CurrentZone == Z
				&& Successor.CurrentCell == Cell && Successor.Blueprint == Blueprint
				&& Successor.GetIntProperty("KingdomBuilt") == 1
				&& Successor.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == Job.TargetKey
				&& KingdomConstruction.HasReceipt(Successor, Job);
		}

		public static bool HasRemovalProof(GameObject Successor, string PredecessorId)
		{
			return GameObject.Validate(Successor) && !string.IsNullOrEmpty(PredecessorId)
				&& Successor.GetStringProperty(RemovalProofProperty) == PredecessorId;
		}

		public static bool TellCompletion(KingdomSystem System, GameObject Successor,
			KingdomConstructionJob Job)
		{
			Zone zone = GameObject.Validate(Successor) ? Successor.CurrentZone : null;
			Cell cell = Successor?.CurrentCell;
			KingdomRules.BuildEntry entry;
			if (!GameObject.Validate(Successor) || Job == null || zone == null || cell == null
				|| Job.Phase != KingdomConstructionPhase.Complete
				|| !KingdomConstruction.Owns(System, zone, Job)
				|| cell != zone.GetCell(Job.X, Job.Y)
				|| Successor.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
				|| Successor.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Job.TargetKey
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out entry)
				|| Successor.Blueprint != entry.Blueprint
				|| !KingdomConstruction.HasReceipt(Successor, Job)
				|| !HasRemovalProof(Successor, Job.SubjectId)
				|| !KingdomConstruction.IsCurrent(Job)) return false;
			int told = Successor.GetIntProperty(TellingProperty);
			if (told != 0 && told != 1)
			{
				KingdomConstructionJob corrupt = Job;
				KingdomConstruction.Quarantine(ref corrupt,
					"The construction telling flag is not an exact boolean.");
				return false;
			}
			if (told == 1
				&& Job.Outbox != null && KingdomConstructionRules.OutboxSettled(Job.Outbox))
			{
				KingdomConstructionJob closed = Job;
				return closed.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
					|| KingdomConstruction.UpdatePhysical(ref closed,
						KingdomPhysicalPhase.EffectsSettled, closed.PhysicalIndex,
						closed.PhysicalAmount, closed.PhysicalSpilled, closed.PhysicalItemId,
						closed.PhysicalDestinationId, closed.PhysicalReceipt);
			}
			string displayName = Successor.GetStringProperty(CompletionNameProperty)
				?? Successor.ShortDisplayName ?? "structure";
			long tick;
			if (!long.TryParse(Successor.GetStringProperty(CompletionTickProperty),
				NumberStyles.Integer, CultureInfo.InvariantCulture, out tick)) tick = Job.DueTick;
			KingdomConstructionJob telling = Job;
			if (telling.PhysicalPhase != KingdomPhysicalPhase.EffectsPending
				&& !KingdomConstruction.UpdatePhysical(ref telling,
					KingdomPhysicalPhase.EffectsPending, telling.PhysicalIndex,
					telling.PhysicalAmount, telling.PhysicalSpilled, telling.PhysicalItemId,
					telling.PhysicalDestinationId, telling.PhysicalReceipt)) return false;
			if (!KingdomCeremony.EnsureBuildingRaised(System, cell, displayName, tick,
				Successor.GetStringProperty(CompletionPlanProperty), ref telling)) return false;
			GameObject exactSuccessor;
			if (!KingdomConstruction.Owns(System, zone, telling)
				|| !KingdomConstruction.IsCurrent(telling)
				|| KingdomConstruction.FindExactId(zone, telling.OutputId, out exactSuccessor)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactSuccessor, Successor)
				|| !IsExactSuccessor(Successor, zone, cell, telling, entry.Blueprint)
				|| !HasRemovalProof(Successor, telling.SubjectId)) return false;
			Successor.SetIntProperty(TellingProperty, 1);
			if (Successor.GetIntProperty(TellingProperty) != 1) return false;
			if (!KingdomConstruction.UpdatePhysical(ref telling,
				KingdomPhysicalPhase.EffectsSettled, telling.PhysicalIndex,
				telling.PhysicalAmount, telling.PhysicalSpilled, telling.PhysicalItemId,
				telling.PhysicalDestinationId, telling.PhysicalReceipt)) return false;
			KingdomLog.Log("scaffold complete: " + displayName + " (" + Successor.Blueprint + ")");
			return true;
		}

		/// <summary>
		/// How fast this frame is rising right now, 0 to 100.
		/// <para>
		/// Founding is the one exemption: a frame raised before there is a settlement is raised
		/// by the founder's own hands, and there is no roster to read. Everything after that is
		/// read off the settlement &mdash; whoever the water detail and the works left over.
		/// </para>
		/// </summary>
		private int EffectivenessOf(out int FreeHands, out KingdomSystem System)
		{
			FreeHands = 0;
			System = The.Game.RequireSystem<KingdomSystem>();
			if (System == null || !System.Founded)
			{
				return 100;
			}
			FreeHands = KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew);
			return KingdomRules.RaisingEffectiveness(FreeHands);
		}

		/// <summary>Names a short-handed raising once, and unsays it the moment the crew is
		/// whole (STANDARDS 7b).</summary>
		private void Say(KingdomSystem System, int FreeHands)
		{
			if (System == null || !System.Founded)
			{
				return;
			}
			string line = KingdomRules.RaisingShortfallLine(TargetDisplayName ?? "structure", FreeHands);
			if (line == null)
			{
				ShortfallSaid = false;
				return;
			}
			if (ShortfallSaid)
			{
				return;
			}
			ShortfallSaid = true;
			System.Ledger.Note("{{r|" + line + "}}");
		}

		/// <summary>
		/// The one blueprint the settlement dedicates to its own food stores on completion.
		/// Named here rather than inferred, so a future container-bearing building does not
		/// quietly become a pantry.
		/// </summary>
		public const string LarderBlueprint = "r_KingdomLarder";

		private void CompleteLegacy()
		{
			Cell cell = ParentObject.CurrentCell;
			string blueprint = TargetBlueprint;
			string displayName = TargetDisplayName ?? "structure";
			int defence = ParentObject.GetIntProperty("KingdomDefencePending");
			// Read before the scaffold is taken down, not after: what the founder chose when they
			// commissioned this rides on the scaffold object, and the scaffold is about to stop
			// being a thing to read from.
			string skinColorString = ParentObject.GetStringProperty(KingdomDesign.StagedColorStringProperty);
			string skinDetailColor = ParentObject.GetStringProperty(KingdomDesign.StagedDetailColorProperty);
			string skinRenderString = ParentObject.GetStringProperty(KingdomDesign.StagedRenderStringProperty);
			string skinTile = ParentObject.GetStringProperty(KingdomDesign.StagedTileProperty);
			// Which registry entry ordered this, so a work never has to be recognised by reading
			// its blueprint back against a catalog two designs may share.
			string buildKey = ParentObject.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			string planQuote = ParentObject.GetStringProperty(KingdomCeremony.SurveyorsPlanProperty);
			if (cell == null)
			{
				return;
			}
			GameObject gameObject = null;
			string legacySuccessorId = ParentObject.GetStringProperty(LegacySuccessorIdProperty);
			if (!string.IsNullOrEmpty(legacySuccessorId))
			{
				foreach (GameObject item in cell.GetObjects())
				{
					if (GameObject.Validate(item) && item.ID == legacySuccessorId
						&& item.Blueprint == blueprint)
					{
						gameObject = item;
						break;
					}
				}
				// A known successor moved or changed. Freeze; never guess by creating a duplicate.
				if (gameObject == null) return;
			}
			if (gameObject == null)
			{
				gameObject = GameObject.Create(blueprint);
				if (gameObject == null) return;
				ParentObject.SetStringProperty(LegacySuccessorIdProperty, gameObject.ID);
				cell.AddObject(gameObject);
			}
			if (gameObject.CurrentCell != cell || gameObject.Blueprint != blueprint)
			{
				return;
			}
			KingdomDesign.ApplyRenderOverrides(gameObject, skinColorString, skinDetailColor, skinRenderString, skinTile);
			if (gameObject.GetPart<XRL.World.Parts.LiquidVolume>() != null)
			{
				gameObject.SetIntProperty("KingdomStores", 1);
			}
			else if (blueprint == LarderBlueprint)
			{
				// A civic larder the settlement paid for is the settlement's, the same way a
				// commissioned cask rack is. Keyed on the blueprint rather than "has an
				// Inventory and no LiquidVolume", because the charging post carries a
				// Container/Inventory pair too and is not a pantry.
				gameObject.SetIntProperty("KingdomLarder", 1);
			}
			gameObject.SetIntProperty("KingdomBuilt", 1);
			if (!string.IsNullOrEmpty(buildKey))
			{
				gameObject.SetStringProperty(KingdomUpgrade.BuildKeyProperty, buildKey);
			}
			if (defence > 0)
			{
				gameObject.SetIntProperty("KingdomDefence", defence);
			}
			if (StaffNeeded > 0)
			{
				gameObject.SetIntProperty("KingdomStaffNeeded", StaffNeeded);
				if (ThresholdManning)
				{
					gameObject.SetIntProperty("KingdomThresholdManning", 1);
				}
				if (gameObject.GetPart<XRL.World.Parts.Capacitor>() != null)
				{
					gameObject.SetIntProperty("KingdomHandCranked", 1);
				}
			}
			gameObject.MakeActive();
			if (gameObject.CurrentCell != cell || gameObject.Blueprint != blueprint
				|| gameObject.GetIntProperty("KingdomBuilt") != 1) return;
			bool removed = ParentObject.Destroy(null, Silent: true);
			if (!removed || GameObject.Validate(ParentObject)) return;
			TargetBlueprint = null;
			KingdomLog.Log("scaffold complete: " + displayName + " (" + blueprint + ") at " + cell.X + "," + cell.Y);
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				KingdomCeremony.OnBuildingRaised(system, cell, displayName, CompleteTick, planQuote);
			}
			else
			{
				MessageQueue.AddPlayerMessage("{{G|The " + displayName + " is complete.}}");
			}
		}
	}
}
