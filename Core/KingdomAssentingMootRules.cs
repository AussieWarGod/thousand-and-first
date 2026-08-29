using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free bounds and public calculations for assenting-moot authority.</summary>
	public static partial class KingdomAssentingMootRules
	{
		public const int CurrentReceiptVersion = 1;
		public const int MaxAssents = 6;
		public const int MaxExemptions = 6;
		public const int StrengthPerAssent = 10;
		public const int MaxStrength = MaxAssents * StrengthPerAssent;
		public const int MaxIdentityChars = 1024;
		public const int MaxNameChars = 192;
		public const int MaxReasonChars = 384;
		public const int MaxFaultChars = 512;

		public static bool ActivationEligible(bool Founded, bool HasAssentNode,
			bool HasChavvahRite, bool HasSurfaceClaim, bool HorizontallyAdjacentMoonStair,
			bool RuntimeOwnerReady)
		{
			return Founded && HasAssentNode && HasChavvahRite && HasSurfaceClaim
				&& HorizontallyAdjacentMoonStair && RuntimeOwnerReady;
		}

		/// <summary>Each current voice adds ten; every durable exemption spends one voice.</summary>
		public static int StrengthFor(int ValidAssents, int GrantedExemptions)
		{
			int assents = Clamp(ValidAssents, 0, MaxAssents);
			int exemptions = Clamp(GrantedExemptions, 0, MaxExemptions);
			return Clamp((assents - exemptions) * StrengthPerAssent, 0, MaxStrength);
		}

		public static bool Contains(KingdomAssentingMootReceipt Receipt,
			KingdomAssentingMootRole Role, int ResidentId)
		{
			if (Receipt == null || ResidentId <= 0) return false;
			List<int> ids = Role == KingdomAssentingMootRole.Assent
				? Receipt.AssentResidentIds : Receipt.ExemptResidentIds;
			return ids != null && ids.BinarySearch(ResidentId) >= 0;
		}

		public static int RoleCount(KingdomAssentingMootReceipt Receipt,
			KingdomAssentingMootRole Role)
		{
			if (Receipt == null) return 0;
			List<int> ids = Role == KingdomAssentingMootRole.Assent
				? Receipt.AssentResidentIds : Receipt.ExemptResidentIds;
			return ids?.Count ?? 0;
		}

		private static int Clamp(int Value, int Minimum, int Maximum)
		{
			return Value < Minimum ? Minimum : Value > Maximum ? Maximum : Value;
		}
	}
}
