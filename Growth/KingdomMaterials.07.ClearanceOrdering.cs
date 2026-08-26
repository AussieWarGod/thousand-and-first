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

		// --- Clearance ------------------------------------------------------------------------

		/// <summary>What a rect would cost to clear and what it would yield, or why it will not
		/// be cleared at all.</summary>
		public struct ClearanceAssessment
		{
			/// <summary>False only when there was nothing to assess: no zone, no kingdom, or a
			/// rect outside the ground. Every other field is meaningless when this is false.</summary>
			public bool Valid;

			/// <summary>A founder-facing reason the rect will not be cleared, or null when it
			/// will. Names the object standing in the way, because "no" without a name is the one
			/// answer a building system is not allowed to give.</summary>
			public string Refusal;

			/// <summary>Cells in the rect.</summary>
			public int Cells;

			/// <summary>Cells holding something that has to come down.</summary>
			public int Standing;

			/// <summary>Total effort the clearing costs.</summary>
			public int Effort;

			/// <summary>What the clearing would put in the stockpiles.</summary>
			public KingdomMaterialTally Yield;
		}

		/// <summary>
		/// Reads a rect without changing anything: what stands in it, what clearing it costs, and
		/// what it yields. Safe to call for a confirmation popup.
		/// </summary>
		/// <param name="System">The kingdom; must be founded and must hold this ground.</param>
		/// <param name="Z">The ground.</param>
		/// <param name="X1">West edge, inclusive.</param>
		/// <param name="Y1">North edge, inclusive.</param>
		/// <param name="X2">East edge, inclusive.</param>
		/// <param name="Y2">South edge, inclusive.</param>
		public static ClearanceAssessment Assess(KingdomSystem System, Zone Z, int X1, int Y1, int X2, int Y2)
		{
			ClearanceAssessment assessment = default;
			assessment.Yield = new KingdomMaterialTally();
			if (System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return assessment;
			}
			if (X1 > X2 || Y1 > Y2 || X1 < 0 || Y1 < 0 || X2 >= Z.Width || Y2 >= Z.Height)
			{
				return assessment;
			}
			assessment.Valid = true;
			for (int y = Y1; y <= Y2; y++)
			{
				for (int x = X1; x <= X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						assessment.Valid = false;
						return assessment;
					}
					assessment.Cells++;
					KingdomStanding standing = KingdomStanding.Nothing;
					int hitpoints = 0;
					foreach (GameObject item in cell.GetObjects())
					{
						if (IsProtected(item, out var reason))
						{
							assessment.Refusal = reason;
							return assessment;
						}
						if (!TryClassify(item, out var kind))
						{
							continue;
						}
						if (kind > standing)
						{
							standing = kind;
							hitpoints = BaseHitpoints(item);
						}
					}
					assessment.Effort += KingdomMaterialRules.ClearanceEffort(standing, hitpoints);
					if (standing != KingdomStanding.Nothing)
					{
						assessment.Standing++;
						assessment.Yield.Add(KingdomMaterialRules.YieldMaterial(standing), KingdomMaterialRules.YieldUnits(standing));
					}
				}
			}
			assessment.Yield.Add(KingdomMaterial.Mud, KingdomMaterialRules.GroundMud(assessment.Cells));
			return assessment;
		}

		/// <summary>
		/// Stakes a clearance order over a rect. The order does nothing on its own: crew works it
		/// down on the settlement's ordinary passes, and only ever with hands the water detail and
		/// the works have not already claimed.
		/// </summary>
		/// <param name="System">The kingdom; must be founded and must hold this ground.</param>
		/// <param name="Z">The ground.</param>
		/// <param name="X1">West edge, inclusive.</param>
		/// <param name="Y1">North edge, inclusive.</param>
		/// <param name="X2">East edge, inclusive.</param>
		/// <param name="Y2">South edge, inclusive.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// staked and nothing is spent when it does.</param>
		/// <returns>True once the stake is standing.</returns>
		public static bool StakeClearance(KingdomSystem System, Zone Z, int X1, int Y1, int X2, int Y2, out string Failure)
		{
			Failure = null;
			if (!Enabled)
			{
				Failure = "The settlement is not growing.";
				return false;
			}
			ClearanceAssessment assessment = Assess(System, Z, X1, Y1, X2, Y2);
			if (!assessment.Valid)
			{
				Failure = "That ground is not the settlement's to clear.";
				return false;
			}
			if (assessment.Refusal != null)
			{
				Failure = assessment.Refusal;
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			List<GameObject> candidates = survey.Clearances;
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject item = candidates[i];
				r_KingdomClearance existing = item.GetPart<r_KingdomClearance>();
				if (existing != null && Overlaps(existing, X1, Y1, X2, Y2))
				{
					Failure = "That ground is already ordered cleared.";
					return false;
				}
			}
			Cell cell = StakeCell(Z, X1, Y1, X2, Y2);
			if (cell == null)
			{
				Failure = "There is nowhere to drive the stake.";
				return false;
			}
			GameObject stake = GameObject.Create(ClearanceStakeBlueprint);
			if (stake == null)
			{
				Failure = "The stake could not be driven.";
				return false;
			}
			r_KingdomClearance order = stake.GetPart<r_KingdomClearance>();
			if (order == null)
			{
				stake.Obliterate();
				Failure = "The stake could not be driven.";
				return false;
			}
			order.X1 = X1;
			order.Y1 = Y1;
			order.X2 = X2;
			order.Y2 = Y2;
			order.EffortTotal = assessment.Effort;
			order.EffortLeft = assessment.Effort;
			order.LastWorkedTick = The.Game.TimeTicks;
			stake.DisplayName = "ground ordered cleared";
			GameObject accepted = cell.AddObject(stake);
			KingdomSurvey.ObserveAddResultInActive(Z, stake, accepted);
			stake.MakeActive();
			if (stake.CurrentCell != cell)
			{
				bool removed = stake.Obliterate(null, Silent: true);
				if (removed || !GameObject.Validate(stake))
					KingdomSurvey.ObserveRemovedFromActive(Z, stake);
				Failure = "The stake could not be driven.";
				return false;
			}
			KingdomGovernanceScope.Commit("clear ground");
			string yield = assessment.Yield.Describe();
			KingdomChronicle.Record(System, KingdomPresentation.Rich(System.KingdomDisplayName) + " set its people to clearing " + assessment.Cells + " paces of ground");
			int days = KingdomMaterialRules.DaysForOneHand(assessment.Effort);
			MessageQueue.AddPlayerMessage("{{G|The ground is staked for clearing.}} " + assessment.Cells + " paces, "
				+ days + ((days == 1) ? " day" : " days") + " of work for a single pair of hands"
				+ ((yield == null) ? "" : (", and " + yield + " out of it")) + ".");
			KingdomLog.Log("materials: clearance staked " + X1 + "," + Y1 + " to " + X2 + "," + Y2 + " effort=" + assessment.Effort);
			return true;
		}

		/// <summary>
		/// Where the stake goes: the first open, passable cell inside the ordered rect, so the
		/// marker stands in the ground it names rather than on top of the tree it condemns.
		/// Falls back to the rect's north-west corner, which always exists once the rect has been
		/// bounds-checked.
		/// </summary>
		private static Cell StakeCell(Zone Z, int X1, int Y1, int X2, int Y2)
		{
			for (int y = Y1; y <= Y2; y++)
			{
				for (int x = X1; x <= X2; x++)
				{
					Cell candidate = Z.GetCell(x, y);
					if (candidate != null && candidate.IsEmpty() && candidate.IsPassable())
					{
						return candidate;
					}
				}
			}
			return Z.GetCell(X1, Y1);
		}
	}
}
