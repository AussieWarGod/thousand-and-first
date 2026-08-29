namespace ThousandAndFirst
{
	public static partial class KingdomBodyHistoryRules
	{
		internal static bool ValidCompletedLabOwner(string Value)
		{
			return PrefixedIdentifier(Value, LabOwnerPrefix);
		}

		internal static bool ValidEffectNonce(string Value)
		{
			if (Value == null || Value.Length != 32) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		internal static bool ValidWitnessFact(string Value)
		{
			return Text(Value);
		}

		private static bool NativePath(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > 256) return false;
			bool digit = false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (c >= '0' && c <= '9') { digit = true; continue; }
				if (c != '/' || !digit || i == Value.Length - 1) return false;
				digit = false;
			}
			return digit;
		}
	}
}
