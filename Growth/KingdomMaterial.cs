namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement's material vocabulary, in two halves.
	/// <para>
	/// <b>Raw</b> &mdash; mud from turned ground, brush cut and retted into canvas and cord, timber
	/// from trees, stone from rock walls and boulders, marble from a seam, scrap from a ruin. Every
	/// one of them already stands in a Qud zone and can be carried away from it. Nothing here is
	/// minted; clearance, salvage, and trade are the only three doors raw material comes through.
	/// </para>
	/// <para>
	/// <b>Refined</b> &mdash; shaped timber off a sawyer's yard, shaped stone off a mason's yard,
	/// worked metal out of a smelter. These come through a fourth door and only that one: a staffed
	/// yard, standing on the settlement's own ground, working raw stock the settlement already
	/// earned (<see cref="KingdomMaterialRules.RawPerRefined"/>). No clearance yields them, no seam
	/// holds them, and no amount of waiting makes them: they are labour, which is the only thing in
	/// this economy that ever turns one good into a better one.
	/// </para>
	/// </summary>
	public enum KingdomMaterial
	{
		Mud = 0,
		Brush = 1,
		Timber = 2,
		Stone = 3,
		Marble = 4,
		Scrap = 5,
		ShapedTimber = 6,
		ShapedStone = 7,
		WorkedMetal = 8
	}
}
