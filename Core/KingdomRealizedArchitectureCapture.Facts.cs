using System;
using System.Collections.Generic;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomRealizedArchitectureCapture
	{
		private const int MaxLiquidComponents = 16;

		/// <summary>
		/// One measured object row. Every fact is read off the object itself or off its blueprint,
		/// never off the cell it stands in.
		/// </summary>
		private static bool TryFact(GameObject Item, int X, int Y, bool Owner,
			out KingdomRealizedObjectFact Fact, out string Failure)
		{
			Fact = null;
			string liquid;
			if (!TryLiquid(Item, out liquid, out Failure)) return false;
			Render render = Item.GetPart<Render>();
			// LIVE physics, not the blueprint's declaration. A component whose Physics part was
			// stripped, or whose Solid flag flipped after staging, no longer does what the design
			// says it does, and a blueprint-only read would digest it as the intact output.
			Physics physics = Item.GetPart<Physics>();
			GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(
				Item.Blueprint);
			bool door = Item.IsDoor();
			Fact = new KingdomRealizedObjectFact
			{
				X = X,
				Y = Y,
				Blueprint = Item.Blueprint,
				Slot = Item.GetStringProperty(KingdomArchitectureStamper.ComponentSlotProperty),
				Layer = Item.GetIntProperty(KingdomArchitectureStamper.ComponentLayerProperty),
				// Absent stays absent: the stamper removes this key when a placement declares no
				// stateful anchor, and an explicitly stored empty one is a different, refused shape.
				Anchor = Item.HasStringProperty(KingdomArchitectureStamper.ComponentAnchorProperty)
					? Item.GetStringProperty(KingdomArchitectureStamper.ComponentAnchorProperty)
					: null,
				// Proved against the owner's frozen receipt above; the lot-bearing token itself is
				// deliberately not carried into the comparison.
				AuthorityProved = true,
				Existing = Item.GetIntProperty(
					KingdomArchitectureStamper.ComponentExistingProperty) == 1,
				Owner = Owner,
				PhysicsPresent = physics != null,
				Solid = physics != null && physics.Solid,
				// The stamper's own pass audit names this part and field; kept beside the live fact
				// so a component that drifted from its declaration is visible as a difference.
				BlueprintSolid = blueprint != null && blueprint.HasPart("Physics")
					&& blueprint.GetPartParameter("Physics", "Solid", false),
				Door = door,
				Liquid = liquid,
				Tile = render == null ? null : render.Tile,
				RenderString = render == null ? null : render.RenderString,
				ColorString = render == null ? null : render.ColorString,
				DetailColor = render == null ? null : render.DetailColor,
				TileColor = render == null ? null : render.TileColor,
				RenderLayer = render == null ? 0 : render.RenderLayer,
				PathState = Item.GetIntProperty(KingdomRoads.PathStateProperty)
			};
			return true;
		}

		/// <summary>
		/// This object's OWN liquid, never the ground's. An architecture piece that holds liquid is a
		/// functional difference; a puddle beside it is somebody else's.
		/// <para>
		/// The subgrammar is length-prefixed for the same reason the outer grammar is: a component
		/// liquid key is a live string, and joining live strings with separators reintroduces exactly
		/// the collision the outer framing removes.
		/// </para>
		/// </summary>
		private static bool TryLiquid(GameObject Item, out string Canonical, out string Failure)
		{
			Canonical = null;
			Failure = null;
			LiquidVolume volume = Item.GetPart<LiquidVolume>();
			if (volume == null) return true;
			List<string> rows = new List<string>();
			if (volume.ComponentLiquids != null)
				foreach (KeyValuePair<string, int> component in volume.ComponentLiquids)
				{
					if (rows.Count >= MaxLiquidComponents)
						return Fail("a lot component declares more liquid components than a lawful "
							+ "architecture piece may carry", out Failure);
					string row = KingdomRealizedCaptureRules.Pair(component.Key, component.Value);
					if (row == null)
						return Fail("a lot component declares an unencodable liquid component key",
							out Failure);
					rows.Add(row);
				}
			rows.Sort(StringComparer.Ordinal);
			Canonical = KingdomRealizedCaptureRules.Liquid(volume.Volume, volume.MaxVolume,
				volume.Flags, rows);
			if (Canonical == null)
				return Fail("a lot component's liquid could not be canonically encoded", out Failure);
			return true;
		}
	}
}
