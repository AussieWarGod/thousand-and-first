namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		// Existing small stakes still need their exact reader after a design raises its minimum.
		// New commissioning floors the requested size at the catalogue minimum, independently.
		private static bool ValidateRetainedBinding(LoadState State, RawBinding Raw,
			ArchitectureBindingDraft Draft)
		{
			string flag = Optional(Raw, "Retained");
			if (flag != null && flag != "yes" && flag != "no")
				return Fault(State, "binding " + Raw.Key, "Retained must be yes or no");
			foreach (ArchitectureTierDraft tier in Draft.Tiers)
			{
				if (!State.Buildings.TryGetValue(tier.BuildKey, out var building) || !building.HasPlot)
					return Fault(State, "binding " + Raw.Key, "retained-size proof needs a known plot building");
				bool below = Draft.Size < building.LotSize;
				if (below != (flag == "yes"))
					return Fault(State, "binding " + Raw.Key,
						"bindings below the current minimum must be explicitly Retained; current sizes cannot be Retained");
			}
			return true;
		}
	}
}
