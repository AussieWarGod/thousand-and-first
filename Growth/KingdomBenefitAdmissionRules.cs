namespace ThousandAndFirst
{
	/// <summary>Engine-free bounded-admission law for one active-zone provider snapshot.</summary>
	public static class KingdomBenefitAdmissionRules
	{
		public static int ExplicitRows(int ObservedParts, bool ObjectOverflow)
		{
			if (ObservedParts <= 0) return 0;
			return ObjectOverflow ? 1 : ObservedParts;
		}

		public static int Remaining(int Admitted)
		{
			if (Admitted < 0) return 0;
			if (Admitted == 0) return KingdomBenefitEmbodimentRules.MaxProvidersPerZone;
			return Admitted >= KingdomBenefitEmbodimentRules.MaxProvidersPerZone ? 0
				: KingdomBenefitEmbodimentRules.MaxProvidersPerZone - Admitted;
		}

		/// <summary>Declarative parts on one object are atomic. A partial object would make which
		/// extension part ran depend on mutable part order.</summary>
		public static bool WholeObjectFits(int Admitted, int Offered)
		{
			return Admitted >= 0
				&& Admitted <= KingdomBenefitEmbodimentRules.MaxProvidersPerZone
				&& Offered >= 0 && Offered <= Remaining(Admitted);
		}

		/// <summary>Admits a whole stable anchor group or leaves accounting untouched so the
		/// caller can quarantine that group and continue with later fitting evidence.</summary>
		public static bool TryAdmitWholeGroup(ref int Admitted, int Offered)
		{
			if (!WholeObjectFits(Admitted, Offered)) return false;
			Admitted += Offered; return true;
		}

		/// <summary>Known native declarations are already canonical, so their stable prefix may
		/// fill the remaining rows without evaluating anything past the cap.</summary>
		public static int NativePrefix(int Admitted, int Offered)
		{
			if (Offered <= 0) return 0;
			int remaining = Remaining(Admitted);
			return Offered < remaining ? Offered : remaining;
		}
	}
}
