using System;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		private void ValidateGroomingState(bool HasConfiguration,
			KingdomSuccessionConfiguration Configuration)
		{
			KingdomGroomingRecord grooming = default(KingdomGroomingRecord);
			bool hasGrooming = GroomingRecordWire.Length > 0;
			if (hasGrooming && !KingdomGroomingRecord.TryDecode(GroomingRecordWire,
				out grooming))
				throw new InvalidOperationException("The saved grooming record is invalid.");
			if ((hasGrooming && (!HasConfiguration
					|| Configuration.Choice != HeirChoice.Groomed
					|| !string.Equals(grooming.RealmId, Configuration.RealmId,
						StringComparison.Ordinal)
					|| grooming.ResidentId != Configuration.ChosenResidentId))
				|| (HasConfiguration && Configuration.Choice == HeirChoice.Groomed
					&& !hasGrooming))
				throw new InvalidOperationException(
					"The grooming record does not match its succession custom.");
		}
	}
}
