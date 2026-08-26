using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	/// <summary>Serialization-only projection. Runtime cleanup authority is valid only if this
	/// entire shape validates as one canonical state.</summary>
	internal sealed class KingdomInheritanceSavedShape
	{
		internal int PhaseValue;

		internal string LegacyText = "";

		internal string ReceiptText = "";

		internal string CommittedReceiptText = "";

		internal string TargetZoneId = "";

		internal string TargetTerrainBlueprint = "";

		internal int TargetTerrainRank = -1;

		internal string SecretId = "";

		internal string SiteName = "";

		internal int ApplyStatus = -1;

		internal int ApplyFault = -1;

		internal string ApplicationMarker = "";

		internal bool ReleasePending = false;

		internal bool OwnsSkipTerrainBuilders;

		internal bool OwnsNoBiomes;

		internal bool OwnsZoneName;

		internal bool RecoveryDisabled;

		internal bool RetryAuthorized = false;
	}

}
