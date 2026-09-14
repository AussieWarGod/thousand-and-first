using System;

namespace ThousandAndFirst.Harness
{
	/// <summary>Expected clear interior for this scenario's compiled Medium shared-home variants.</summary>
	internal static class KingdomPaidHousingRoomRules
	{
		internal static int ExpectedFloor(ArchitectureLayoutSnapshot snapshot)
		{
			if (snapshot == null || snapshot.LotSize != ArchitectureLotSize.Medium) return -1;
			int tables = 0, hearths = 0;
			foreach (var placement in snapshot.Placements)
			{
				string key = placement.StatefulAnchor;
				int identity = key == null ? -1 : key.LastIndexOf('@');
				string role = identity < 0 ? key : key.Substring(0, identity);
				if (role == "fixture:table") tables++;
				if (role == "fixture:hearth") hearths++;
			}
			if (tables > 1 || !(snapshot.BuildKey == "tentrow" ? tables == 0 && hearths == 0
				: snapshot.BuildKey == "hutyard" && hearths == 1)) return -1;
			// 24 interior cells minus three beds, two seats, storage and the main object.
			return 17 - hearths - tables;
		}
	}
}
