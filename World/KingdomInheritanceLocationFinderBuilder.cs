using ThousandAndFirst;
using XRL.World;

namespace XRL.World.ZoneBuilders
{
	/// <summary>
	/// Success-aware replacement for AddLocationFinder. ZoneBuilderCollection copies its member
	/// count before applying, so removing persistent builders during the site builder cannot stop a
	/// later generic finder in the same attempt. This builder always returns true and creates the
	/// widget only after the exact application marker and map note are both proved.
	/// </summary>
	public sealed class KingdomInheritanceLocationFinderBuilder
	{
		public string LegacyId;

		public string TargetGameId;

		public string TargetZoneId;

		public int ReconstructionVersion;

		public bool BuildZone(Zone Z)
		{
			try
			{
				KingdomInheritanceState state = KingdomInheritanceState.Instance;
				if (state != null)
				{
					string failure;
					state.TryInstallLocationFinder(Z, LegacyId, TargetGameId, TargetZoneId,
						ReconstructionVersion, out failure);
				}
			}
			catch (System.Exception ex)
			{
				try
				{
					KingdomInheritanceState.Instance?.RecordDiscoveryFailure(
						"the success-aware finder threw: " + ex.Message);
				}
				catch (System.Exception)
				{
				}
			}
			return true;
		}
	}
}
