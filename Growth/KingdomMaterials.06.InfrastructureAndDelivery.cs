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
		/// <summary>
		/// Whether the settlement's own infrastructure will carry a design of this size, and why
		/// not when it will not. The engine-coupled half of Addendum 7's gate: this reads what
		/// stands on the ground and hands the verdict to <c>KingdomMaterialRules.AllowsBuild</c>,
		/// which owns the law and the wording.
		/// </summary>
		/// <param name="Z">Ground the commission would be issued on.</param>
		/// <param name="Key">The design's registry key.</param>
		/// <param name="Failure">A founder-facing reason when this returns false, naming the yard.
		/// </param>
		public static bool AllowsInfrastructure(Zone Z, string Key, out string Failure)
		{
			Failure = null;
			if (!KingdomPlots.TryGetSpec(Key, out var spec) || spec == null || !KingdomMaterialRules.RequiresYard(spec.Size))
			{
				return true;
			}
			KingdomMaterialTally cost = CostFor(Key);
			if (KingdomMaterialRules.YardsFor(spec.Size, cost).Count == 0)
			{
				return true;
			}
			string name = KingdomData.TryGetBuilding(Key, out var entry) ? entry.Name : null;
			return KingdomMaterialRules.AllowsBuild(spec.Size, cost, YardsStanding(Z), name, out Failure);
		}

		/// <summary>
		/// What every yard on this ground is doing: standing, staffed this pass, and headed by a
		/// notable. One walk of the zone, and a yard is whatever a design declared itself to be
		/// with <c>Refines</c> &mdash; a third party's sawmill counts exactly like ours.
		/// </summary>
		public static List<KingdomMaterialRules.KingdomYardStanding> YardsStanding(Zone Z)
		{
			List<KingdomMaterialRules.KingdomYardStanding> yards = new List<KingdomMaterialRules.KingdomYardStanding>();
			if (Z == null)
			{
				return yards;
			}
			bool[] standing = new bool[KingdomMaterialRules.YardCount];
			bool[] staffed = new bool[KingdomMaterialRules.YardCount];
			bool[] headed = new bool[KingdomMaterialRules.YardCount];
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			List<GameObject> candidates = survey.Built;
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject item = candidates[i];
				if (!KingdomUpgrade.IsFunctionallyBuilt(item))
				{
					continue;
				}
				if (!TryRefineryOf(item.GetStringProperty(KingdomUpgrade.BuildKeyProperty), out var yard))
				{
					continue;
				}
				int index = (int)yard;
				standing[index] = true;
				// KingdomStaffed is set by the staffing pass earlier in this same visit
				// (KingdomGrowth.AssignWork), so this reads the crew that is actually in the yard
				// today rather than the crew the design asked for.
				staffed[index] |= item.GetIntProperty("KingdomStaffed") == 1;
				headed[index] |= IsHeaded(item);
			}
			for (int i = 0; i < KingdomMaterialRules.YardCount; i++)
			{
				if (standing[i])
				{
					yards.Add(new KingdomMaterialRules.KingdomYardStanding((KingdomYard)i, standing[i], staffed[i], headed[i]));
				}
			}
			return yards;
		}

		/// <summary>
		/// The office layer's answer to "does a named notable head this work?", installed by
		/// whoever owns the office seats (Addendum 6). Left null here on purpose: this file must
		/// not decide who holds an office, and a mod that ships without the office layer must not
		/// find its grand works refused by a question nobody is answering.
		/// <para>
		/// Null therefore reads as "not enforced" rather than "not headed", the same compatibility
		/// rule an absent attribute follows everywhere else in this economy. Once the probe is
		/// installed, an unheaded yard refuses a grand work by name.
		/// </para>
		/// </summary>
		public static System.Func<GameObject, bool> HeadedProbe;

		/// <summary>Whether a work is headed, or true when no office layer has installed a probe.
		/// </summary>
		public static bool IsHeaded(GameObject Work)
		{
			return HeadedProbe == null || (Work != null && HeadedProbe(Work));
		}

		/// <summary>
		/// Legacy immediate entry for a design's composite stockpile cost. New construction must
		/// call <see cref="ReservePayment"/>, persist its job, then inspect the receipt result. This
		/// wrapper remains for compatibility and returns true only for an exact commit; a dynamic
		/// terminal veto is logged with its outstanding claim rather than reported as "nothing taken."
		/// </summary>
		/// <returns>True only when the exact composite receipt committed.</returns>
		public static bool Pay(Zone Z, string Key)
		{
			KingdomMaterialDebit debit = ReservePayment(Z, Key);
			KingdomMaterialDebitResult result = debit.Commit();
			if (result.Partial)
			{
				KingdomLog.Log("materials: legacy payment on " + Key + " ended " + result.Outcome
					+ "; outstanding=" + result.Outstanding.ToClaimString());
			}
			else if (result.Exact)
			{
				KingdomLog.Log("materials: exact composite payment on " + Key);
			}
			return result.Exact;
		}

		/// <summary>
		/// Whether the dedicated stockpiles on this ground cover an IMPROVEMENT's material cost,
		/// without spending any of it. The absorption law asks this before a work is judged ready,
		/// so an improvement short of material is refused by name rather than begun and abandoned
		/// at the moment <see cref="ReserveUpgradePayment"/> would have taken the cost.
		/// </summary>
		/// <param name="Z">Ground the work stands on.</param>
		/// <param name="PredecessorKey">Registry key of the standing design whose transition is
		/// being paid.</param>
		/// <param name="Missing">What the stockpiles are short of, or null when they cover it.
		/// </param>
		public static bool CanPayUpgrade(Zone Z, string PredecessorKey, out string Missing)
		{
			Missing = null;
			KingdomMaterialTally cost = UpgradeCostFor(PredecessorKey);
			if (cost.IsEmpty())
			{
				return true;
			}
			MaterialStock stock = Stock(Z);
			if (KingdomMaterialRules.Covers(stock.Tally, cost))
			{
				return true;
			}
			Missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
			return false;
		}

		/// <summary>
		/// Puts a charter's carried material into this ground's stockpiles, dropping whatever
		/// will not fit at the founder's feet rather than keeping it on the cart. Mirrors what
		/// <c>KingdomTrade</c> already does with water it cannot store.
		/// </summary>
		/// <returns>Units that ended up on the ground rather than in a stockpile.</returns>
		public static int Deliver(KingdomSystem System, Zone Z, KingdomMaterialTally Carried)
		{
			if (Carried == null || Carried.IsEmpty() || Z == null)
			{
				return 0;
			}
			MaterialStock stock = Stock(Z);
			Cell founderCell = The.Player?.CurrentCell;
			Cell fallback = (founderCell != null && founderCell.ParentZone == Z) ? founderCell : Z.GetCell(Z.Width / 2, Z.Height / 2);
			int spilled = stock.PutAll(Carried, fallback);
			if (spilled > 0 && System != null)
			{
				System.Ledger.Note("{{r|" + spilled + " loads came under charter with no stockpile to go in, and were set down on the ground.}}");
			}
			return spilled;
		}
	}
}
