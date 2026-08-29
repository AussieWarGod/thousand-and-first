namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Exile may archive attributed ordinary transients, but must never erase a
		/// rowless, malformed, or construction-input binding whose parent graph is unavailable.</summary>
		private static bool AnyUnattributedTransientBinding(KingdomSystem system,
			KingdomJobTable jobs)
		{
			if (system?.Bindings == null || jobs == null
				|| !system.Bindings.TryRead(out KingdomBindingTable bindings, out _)) return true;
			for (int i = 0; i < bindings.Count; i++)
			{
				if (!bindings.TryAt(i, out KingdomBinding binding)) return true;
				if (binding.Kind != KingdomBindingKind.Transient) continue;
				if (binding.BindingKey <= 0 || string.IsNullOrEmpty(binding.ZoneId)
					|| string.IsNullOrEmpty(binding.ObjectId)) return true;
				bool attributed = false;
				for (int j = 0; j < jobs.Count; j++)
				{
					if (!jobs.TryAt(j, out KingdomJobRow row)) return true;
					if (row.JobId != binding.BindingKey
						&& row.DeliveryTripId != binding.BindingKey) continue;
					if (row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.ConstructionInput) return true;
					attributed = true;
				}
				if (!attributed) return true;
			}
			return false;
		}
	}
}
