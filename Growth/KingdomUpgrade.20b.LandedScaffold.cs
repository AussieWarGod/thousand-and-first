using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		/// <summary>
		/// The landed scaffold's identity, read from the successor's durable removal intent
		/// (published before the scaffold's Destroy in <c>Growth/KingdomScaffold.Durable.cs</c>),
		/// never from the volatile reference that <c>r_KingdomImprovement.PollHandover</c> drops
		/// with <c>GameObject.Validate(ref Scaffold)</c>. The decision is the engine-free
		/// <see cref="KingdomUpgradeRules.LandedScaffoldIdentity"/>; this only reads the engine.
		/// Read-only: nothing is stamped (#283).
		/// </summary>
		private static bool TryLandedScaffoldId(r_KingdomImprovement Intent, GameObject Successor,
			out string ScaffoldId)
		{
			ScaffoldId = null;
			if (Intent == null || !GameObject.Validate(Successor)) return false;
			GameObject reference = Intent.Scaffold;
			bool live = GameObject.Validate(reference);
			string id = Successor.GetStringProperty(r_KingdomScaffold.ScaffoldRemovalIntentIdProperty);
			bool exact = !live && r_KingdomScaffold.HasExactScaffoldRemovalIntent(Successor, id);
			bool absent = exact && KingdomConstruction.FindGlobalLiveId(id, out _)
				== KingdomPhysicalLookupState.Absent;
			ScaffoldId = KingdomUpgradeRules.LandedScaffoldIdentity(live, reference != null,
				reference == null ? null : reference.IDIfAssigned, exact, id, absent);
			return ScaffoldId != null;
		}
	}
}
