namespace ThousandAndFirst
{
	/// <summary>
	/// One staked plan as far as the scheduling arithmetic is concerned: what it would cost,
	/// whether it is exempt from the building cap the way a wall is, and where it sits in the
	/// queue. Nothing engine-coupled &mdash; <see cref="ThousandAndFirst.KingdomPlanMarker"/>
	/// (Growth/KingdomPlanMarker.cs) is the part that reads these off a real marker and carries
	/// out what this struct's owner decides.
	/// </summary>
	public readonly struct KingdomPendingPlan
	{
		/// <summary>Tick the plan was staked at. Earlier ticks win first place in the queue.</summary>
		public readonly long PlacedTick;

		/// <summary>
		/// Breaks a <see cref="PlacedTick"/> tie. Two plans staked back to back in the same
		/// charter session spend no game time between them, so the tick alone cannot always
		/// order them. Assigned from a monotonic counter at the moment each plan is staked;
		/// never recomputed here.
		/// </summary>
		public readonly long PlacedOrder;

		/// <summary>Drams the design costs, drawn once, in full, the moment it is realised.</summary>
		public readonly int CostDrams;

		/// <summary>
		/// True for a defensive design (a <c>KingdomRules.BuildEntry</c> with
		/// <c>Defence &gt; 0</c>), which never counts against the building cap &mdash; the same
		/// exemption <c>KingdomCommission.Commission</c> already grants a wall, so a plan and a
		/// founder-issued commission compete for the same one allowance rather than each getting
		/// their own.
		/// </summary>
		public readonly bool Defensive;

		public KingdomPendingPlan(long PlacedTick, long PlacedOrder, int CostDrams, bool Defensive)
		{
			this.PlacedTick = PlacedTick;
			this.PlacedOrder = PlacedOrder;
			this.CostDrams = CostDrams;
			this.Defensive = Defensive;
		}
	}

	/// <summary>
	/// Engine-free scheduling for staked plans: what order they wait in, whether one can be
	/// afforded right now, and how many a single settlement pass may realise. The engine-coupled
	/// half is <see cref="ThousandAndFirst.KingdomPlanMarker"/>, which reads real markers into
	/// <see cref="KingdomPendingPlan"/> values, calls <see cref="PlansToRealize"/>, and spends the
	/// water for real.
	/// </summary>
	public static class KingdomPlanRules
	{
		/// <summary>
		/// Plans one settlement pass may realise. Mirrors
		/// <c>KingdomCropRules.MaxCyclesPerVisit</c>'s reasoning exactly: a long absence should
		/// still catch up, but nothing here should let one visit clear a queue of arbitrary
		/// length in a single tick.
		/// </summary>
		public const int MaxPlansPerVisit = 3;

		/// <summary>
		/// Orders two plans oldest-first, tied plans by placement order.
		/// <para>
		/// Oldest-first was chosen over cheapest-first or a founder-set priority for one reason:
		/// it needs nothing else from the founder to stay fair. Cheapest-first would let an
		/// expensive design the founder staked first starve behind whatever smaller thing gets
		/// queued after it, indefinitely; founder-priority would need a UI of its own to set and
		/// read, and "not a management screen" is a pillar this mod holds everywhere else. First
		/// come, first served asks nothing of the founder and explains itself in one sentence.
		/// </para>
		/// </summary>
		public static int CompareOrder(KingdomPendingPlan A, KingdomPendingPlan B)
		{
			if (A.PlacedTick != B.PlacedTick)
			{
				return A.PlacedTick.CompareTo(B.PlacedTick);
			}
			return A.PlacedOrder.CompareTo(B.PlacedOrder);
		}

		/// <summary>
		/// Whether Plan can be realised against the stores and the cap exactly as they stand.
		/// Never partial: a plan that cannot cover its full cost this instant is left standing,
		/// exactly as it was, for as many passes as it takes. That is the whole of "waiting is
		/// not failing" &mdash; there is no partially-built, half-charged state to fall into.
		/// </summary>
		/// <param name="Plan">The plan under consideration.</param>
		/// <param name="StoredWater">Drams currently in the dedicated stores.</param>
		/// <param name="BuiltCount">Buildings already standing or scaffolded here, counted the
		/// same way <c>KingdomCommission.Commission</c> counts them for its own cap check.</param>
		/// <param name="CapForStage">The settlement's room for this stage, from
		/// <c>KingdomRules.MaxBuildingsForStage</c>.</param>
		public static bool CanAfford(KingdomPendingPlan Plan, int StoredWater, int BuiltCount, int CapForStage)
		{
			if (StoredWater < Plan.CostDrams)
			{
				return false;
			}
			return Plan.Defensive || BuiltCount < CapForStage;
		}

		/// <summary>
		/// Decides which of Plans a single settlement pass may realise, and in what order.
		/// <para>
		/// Walks the queue oldest-first (<see cref="CompareOrder"/>) and stops the instant one
		/// plan cannot be afforded &mdash; a later, cheaper plan never cuts in front of an older,
		/// costlier one just because the money happens to be there this pass, or the queue would
		/// stop being something the founder could predict. Everything already realised this pass
		/// is folded into the running water and cap totals before the next plan is judged, so a
		/// long absence can genuinely clear several plans in one visit when the stores allow it,
		/// bounded only by <see cref="MaxPlansPerVisit"/>.
		/// </para>
		/// <para>
		/// Returns indices into <paramref name="Plans"/>, in the order to realise them. An empty
		/// result is not a failure &mdash; it is the settlement correctly deciding it cannot yet
		/// afford anything, which is a permanent, ordinary state a plan may sit in forever
		/// without ever being resolved as an error and without ever being expired.
		/// </para>
		/// </summary>
		public static System.Collections.Generic.List<int> PlansToRealize(System.Collections.Generic.IReadOnlyList<KingdomPendingPlan> Plans, int StoredWater, int BuiltCount, int CapForStage)
		{
			System.Collections.Generic.List<int> result = new System.Collections.Generic.List<int>();
			if (Plans == null || Plans.Count == 0)
			{
				return result;
			}
			System.Collections.Generic.List<int> order = new System.Collections.Generic.List<int>(Plans.Count);
			for (int i = 0; i < Plans.Count; i++)
			{
				order.Add(i);
			}
			order.Sort(delegate(int a, int b)
			{
				return CompareOrder(Plans[a], Plans[b]);
			});
			int water = StoredWater;
			int built = BuiltCount;
			foreach (int index in order)
			{
				if (result.Count >= MaxPlansPerVisit)
				{
					break;
				}
				KingdomPendingPlan plan = Plans[index];
				if (!CanAfford(plan, water, built, CapForStage))
				{
					break;
				}
				result.Add(index);
				water -= plan.CostDrams;
				if (!plan.Defensive)
				{
					built++;
				}
			}
			return result;
		}
	}
}
