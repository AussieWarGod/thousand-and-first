using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{
		private static void QuarantineStrike(KingdomConstructionJob Job, string Failure)
		{
			if (Job == null) return;
			KingdomConstructionJob current = Job;
			if (!KingdomConstruction.IsCurrent(current))
				KingdomConstruction.TryFind(Job.Id, out current);
			if (current == null || KingdomConstructionRules.IsTerminal(current.Phase)) return;
			KingdomConstruction.UpdatePhysical(ref current, KingdomPhysicalPhase.Quarantined,
				current.PhysicalIndex, current.PhysicalAmount, current.PhysicalSpilled,
				current.PhysicalItemId, current.PhysicalDestinationId, current.PhysicalReceipt,
				Failure);
			KingdomConstruction.Quarantine(ref current, Failure);
			KingdomLog.Log("materials: strike quarantined " + current.Id + ": " + Failure);
		}

		private static void WorkClearance(KingdomSystem System, Zone Z, GameObject StakeObject, r_KingdomClearance Order, int Hands, long TimeTicks)
		{
			if (Order.LastWorkedTick <= 0)
			{
				Order.LastWorkedTick = TimeTicks;
				return;
			}
			int days = KingdomRules.ElapsedDays(TimeTicks - Order.LastWorkedTick);
			if (days <= 0)
			{
				return;
			}
			if (Hands <= 0)
			{
				if (!Order.NoHandsAnnounced)
				{
					Order.NoHandsAnnounced = true;
					System.Ledger.Note("{{r|Ground stands staked for clearing at " + KingdomPresentation.Rich(System.SeatName) + ", and there is nobody free to swing at it. Stand a settler down off the water or a work.}}");
				}
				Order.LastWorkedTick = KingdomRules.AdvanceCheckpoint(Order.LastWorkedTick, TimeTicks);
				return;
			}
			Order.NoHandsAnnounced = false;
			Order.LastWorkedTick = KingdomRules.AdvanceCheckpoint(Order.LastWorkedTick, TimeTicks);
			if (Order.EffortLeft > 0)
			{
				Order.EffortLeft -= KingdomMaterialRules.EffortWorked(Hands, days);
				if (Order.EffortLeft > 0)
				{
					return;
				}
			}
			// Re-read the ground rather than trusting what was assessed at staking: a tree may
			// have burned down since, and something the settlement will not touch may have been
			// set down in the rect. The yield is whatever actually comes out, and an obstruction
			// stops the finish rather than being cleared around.
			ClearanceAssessment assessment = Assess(System, Z, Order.X1, Order.Y1, Order.X2, Order.Y2);
			if (!assessment.Valid || assessment.Refusal != null)
			{
				if (!Order.BlockedAnnounced)
				{
					Order.BlockedAnnounced = true;
					System.Ledger.Note("{{r|" + (assessment.Refusal ?? "The staked ground cannot be read.") + " The clearing waits.}}");
				}
				return;
			}
			KingdomMaterialTally yield = new KingdomMaterialTally();
			int removed = 0;
			bool vetoed = false;
			string vetoedName = null;
			for (int y = Order.Y1; y <= Order.Y2; y++)
			{
				for (int x = Order.X1; x <= Order.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					List<GameObject> standing = new List<GameObject>(cell.GetObjects());
					for (int i = 0; i < standing.Count; i++)
					{
						GameObject item = standing[i];
						if (!GameObject.Validate(item))
						{
							continue;
						}
						if (IsProtected(item, out string reason)
							|| !TryClassify(item, out var kind))
						{
							if (reason != null)
							{
								vetoed = true;
								vetoedName = item.ShortDisplayNameStripped;
							}
							continue;
						}
						if (IsProtected(item, out reason)
							|| !KingdomOrdinaryCustody.TryProveEmpty(item, out _))
						{
							vetoed = true;
							vetoedName = item.ShortDisplayNameStripped;
							continue;
						}
						bool gone = false;
						try { gone = item.Obliterate(null, Silent: true); }
						catch { }
						if (gone || !GameObject.Validate(item))
						{
							KingdomSurvey.ObserveRemovedFromActive(Z, item);
							yield.Add(KingdomMaterialRules.YieldMaterial(kind),
								KingdomMaterialRules.YieldUnits(kind));
							removed++;
						}
						else
						{
							vetoed = true;
							vetoedName = item.ShortDisplayNameStripped;
						}
					}
				}
			}
			Cell stakeCell = StakeObject.CurrentCell;
			MaterialStock stock = Stock(Z);
			int spilled = stock.PutAll(yield, stakeCell);
			if (vetoed)
			{
				if (!Order.BlockedAnnounced)
				{
					Order.BlockedAnnounced = true;
					System.Ledger.Note("{{r|The " + (vetoedName ?? "ground")
						+ " refused the clearing callback. " + removed
						+ (removed == 1 ? " removable thing was" : " removable things were")
						+ " honestly returned; the stake waits on the remainder.}}");
				}
				return;
			}
			int groundPhase = StakeObject.GetIntProperty(ClearanceGroundPhaseProperty);
			if (groundPhase == 1)
			{
				Order.BlockedAnnounced = true;
				System.Ledger.Note("{{r|The clearance ground-yield callback was interrupted. The stake is held for inspection rather than issuing mud twice.}}");
				return;
			}
			if (groundPhase != 0 && groundPhase != 2)
			{
				Order.BlockedAnnounced = true;
				System.Ledger.Note("{{r|The clearance ground-yield receipt is malformed. The stake is held for inspection.}}");
				return;
			}
			if (groundPhase == 0)
			{
				int mud = KingdomMaterialRules.GroundMud(assessment.Cells);
				StakeObject.SetIntProperty(ClearanceGroundPhaseProperty, 1);
				yield.Add(KingdomMaterial.Mud, mud);
				try { spilled += stock.Put(KingdomMaterial.Mud, mud, stakeCell); }
				catch
				{
					Order.BlockedAnnounced = true;
					System.Ledger.Note("{{r|The clearance ground-yield callback threw. The stake is held for inspection rather than issuing mud twice.}}");
					return;
				}
				StakeObject.SetIntProperty(ClearanceGroundPhaseProperty, 2);
			}
			if (KingdomPurpose.HasProtectedCargoEvidence(StakeObject)
				|| !KingdomOrdinaryCustody.TryProveEmpty(StakeObject, out _))
			{
				Order.BlockedAnnounced = true;
				System.Ledger.Note("{{r|The clearance stake now holds another object. Empty it before removal; no ground yield will be issued again.}}");
				return;
			}
			bool stakeRemoved;
			try { stakeRemoved = StakeObject.Obliterate(null, Silent: true); }
			finally { KingdomSurvey.ObserveCurrentTopologyInActive(Z, StakeObject); }
			if (stakeRemoved || !GameObject.Validate(StakeObject))
				KingdomSurvey.ObserveRemovedFromActive(Z, StakeObject);
			if (!stakeRemoved || GameObject.Validate(StakeObject))
			{
				Order.BlockedAnnounced = true;
				System.Ledger.Note("{{r|The cleared ground remains marked because its stake refused removal. No ground yield will be issued again.}}");
				return;
			}
			string carried = yield.Describe();
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			KingdomChronicle.Record(System, assessment.Cells + " paces of ground were cleared at " + realm + ", and " + ((carried == null) ? "nothing came of it but turned earth" : ("the settlement was the richer by " + carried)));
			System.RecordDeed("the ground cleared at " + realm);
			MessageQueue.AddPlayerMessage("{{G|The ground is clear.}} " + removed + ((removed == 1) ? " thing came down" : " things came down")
				+ ((carried == null) ? "." : (", and " + carried + " went to the stockpiles."))
				+ ((spilled > 0) ? " Some of it lies on the ground for want of a stockpile." : ""));
			KingdomLog.Log("materials: clearance done cells=" + assessment.Cells + " removed=" + removed + " yield=" + (carried ?? "none") + " spilled=" + spilled);
		}
	}
}
