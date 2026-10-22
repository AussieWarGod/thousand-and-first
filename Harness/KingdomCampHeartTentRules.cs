namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free bill arithmetic for the paid camp tent (#282). Since 561bffd3 the tent is a
	/// Medium lot billed <c>canvas:12,timber:1</c> (canvas reads as brush,
	/// Growth/KingdomMaterialRules.cs), and since 7403458b its own improvement into the tent row
	/// asks for <c>canvas:2</c> alone. The native fixture feeds these rules the catalogue it is
	/// running (<c>KingdomMaterials.CostFor</c> / <c>UpgradeCostFor("tent")</c>); the DevTests
	/// feed them the same row read from RuntimeData/KingdomBuildings.xml. Neither side holds a
	/// bill literal.
	/// </summary>
	internal static class KingdomCampHeartTentRules
	{
		/// <summary>Brush a paid-tent script mints beside the rung-2 bill: the tent's own brush
		/// plus one fewer than the tent's canvas-only upgrade asks for. Once the tent is paid,
		/// production's own MaterialsInHand gate (Growth/KingdomUpgradeRules.Assessment.cs) keeps
		/// that automatic upgrade unpayable until the fixture holds the completed tent. At
		/// 499357a8 the same automatic tent-row improvement spent materials meant for the heart.
		/// </summary>
		internal static int PaidTentBrush(int TentBrush, int UpgradeBrush)
		{
			return TentBrush + UpgradeBrush - 1;
		}

		/// <summary>Sentinel brush the paid tent leaves in the camp store.</summary>
		internal static int SavedBrush(int TentBrush, int UpgradeBrush)
		{
			return PaidTentBrush(TentBrush, UpgradeBrush) - TentBrush;
		}

		/// <summary>Whether the arithmetic holds for a store of <paramref name="Capacity"/>
		/// declared units that already carries <paramref name="Fill"/> units of the rung-2 bill:
		/// the tent asks for brush, at least one sentinel unit survives it, the survivors cannot
		/// pay the tent's own upgrade, and the setup fill stays inside the declared capacity.
		/// </summary>
		internal static bool Lawful(int TentBrush, int UpgradeBrush, int Fill, int Capacity)
		{
			int saved = SavedBrush(TentBrush, UpgradeBrush);
			return TentBrush > 0 && UpgradeBrush > 0 && Fill >= 0 && saved >= 1
				&& saved < UpgradeBrush && Fill + PaidTentBrush(TentBrush, UpgradeBrush) <= Capacity;
		}
	}
}
