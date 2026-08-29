namespace ThousandAndFirst
{
	/// <summary>
	/// Detached bridge between the save-roster verdict and engine work. It says which systems may
	/// be required only after the original registry has been counted. It owns no engine objects.
	/// </summary>
	public sealed class KingdomSaveSystemRosterRuntimePlan
	{
		public KingdomSaveSystemRosterDecision Decision { get; private set; }
		public int EnsureMask { get; private set; }

		public bool RecoveryRequired
		{
			get
			{
				return Decision == null || Decision.Disposition
					== KingdomSaveSystemRosterDisposition.RecoveryRequired
					|| Decision.Disposition == KingdomSaveSystemRosterDisposition.Refused;
			}
		}

		private KingdomSaveSystemRosterRuntimePlan(
			KingdomSaveSystemRosterDecision Decision, int EnsureMask)
		{
			this.Decision = Decision == null ? null : Decision.Clone();
			this.EnsureMask = EnsureMask;
		}

		public static KingdomSaveSystemRosterRuntimePlan Create(
			KingdomSaveSystemRosterContext Context, bool MarkerPresent, int MarkerRaw,
			KingdomSaveSystemRosterCounts Counts)
		{
			KingdomSaveSystemRosterDecision decision = KingdomSaveSystemRosterRules.Decide(
				Context, MarkerPresent, MarkerRaw, Counts);
			int ensure = 0;
			if (decision.Disposition == KingdomSaveSystemRosterDisposition.Bootstrap)
			{
				ensure = DecodedMask(decision.NextMarkerRaw,
					KingdomSaveSystemRosterRules.MandatoryMask);
			}
			else if (decision.Disposition == KingdomSaveSystemRosterDisposition.Refused
				|| decision.Disposition
					== KingdomSaveSystemRosterDisposition.RecoveryRequired)
			{
				ensure = MarkerPresent
					? DecodedMask(MarkerRaw, KingdomSaveSystemRosterRules.MandatoryMask)
					: KingdomSaveSystemRosterRules.MandatoryMask;
			}
			return new KingdomSaveSystemRosterRuntimePlan(decision, ensure);
		}

		/// <summary>Proves the registry after lawful shell creation. Exact means no omitted optional
		/// carrier and no extra one; multiplicity is still one or zero, never merely nonempty.</summary>
		public bool ExactAfterEnsure(KingdomSaveSystemRosterCounts Counts,
			out KingdomSaveSystemRosterSystem System, out int Expected, out int Actual,
			out string Failure)
		{
			System = KingdomSaveSystemRosterSystem.None;
			Expected = 0; Actual = 0; Failure = null;
			if (Counts == null)
			{
				Failure = "post-require save-system roster observation is absent";
				return false;
			}
			if (EnsureMask == 0) return true;
			for (int i = 1; i <= 5; i++)
			{
				KingdomSaveSystemRosterSystem current =
					(KingdomSaveSystemRosterSystem)i;
				int expected = (EnsureMask & (1 << (i - 1))) == 0 ? 0 : 1;
				int actual = Counts.Count(current);
				if (actual == expected) continue;
				System = current; Expected = expected; Actual = actual;
				Failure = "post-require save-system roster expected " + expected + " "
					+ current + " carrier but observed " + actual;
				return false;
			}
			return true;
		}

		public static bool Empty(KingdomSaveSystemRosterCounts Counts)
		{
			return Counts != null && Counts.Realm == 0 && Counts.Seal == 0
				&& Counts.CivicMemory == 0 && Counts.Succession == 0
				&& Counts.Inheritance == 0;
		}

		private static int DecodedMask(int Raw, int Fallback)
		{
			return KingdomSaveSystemRosterRules.TryDecode(Raw, out int _, out int mask,
				out KingdomSaveSystemRosterFault _, out KingdomSaveSystemRosterSystem _,
				out string _) ? mask : Fallback;
		}
	}
}
