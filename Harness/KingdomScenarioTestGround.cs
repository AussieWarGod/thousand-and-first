using System;
using System.Collections.Generic;

using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.World;

using ThousandAndFirst;
using ThousandAndFirst.Harness;

namespace XRL.World.ZoneBuilders
{
	/// <summary>
	/// Dev-only born-clean test ground: the scenario's starting zone is generated normally and then
	/// stripped, so a persona starts on flat bare passable ground instead of on whatever worldgen
	/// happened to paint there.
	/// <para>
	/// WHY. Worldgen places villages, ruins, and lairs in wilderness parasangs by seed, so no
	/// hardcoded start parasang can be trusted: the previous default, <c>JoppaWorld.11.21</c>,
	/// turned out live to contain a village - arrival announcement, held-object clearance refusals,
	/// ruins in sight. Hunting a terrain that never hosts one is the wrong fix, because "never" is
	/// not a property any terrain has. Declaring the ground is.
	/// </para>
	/// <para>
	/// SEAM. Registered by <see cref="ThousandAndFirst.Harness.KingdomScenarioTestGroundModule" /> as
	/// a POST-builder for exactly one zone id, so it runs at priority 5000 - after the terrain,
	/// village, ruin, and lair builders have all had their say - and touches no other zone in the
	/// world. Registration happens on the <c>BootStartingLocation</c> boot event, which
	/// <c>QudGameBootModule</c> fires well before <c>GlobalLocation.ResolveCell</c> generates that
	/// zone.
	/// </para>
	/// <para>
	/// WHAT IT KEEPS. The one-cell border, so the zone's own travel connections to its neighbours
	/// survive; stairs, which are the zone's connection to the strata below; and anything the
	/// settlement's own clearance law already reads as bare - floors, paint objects, and the engine
	/// bookkeeping widgets that have no physical presence. Everything else in the interior goes,
	/// creatures and liquid pools included. The predicate is
	/// <c>KingdomPlots.ReadObject</c> - the SAME law production founding applies - so this builder
	/// invents no new notion of what counts as ground.
	/// </para>
	/// <para>
	/// DETERMINISTIC AND DEV-ONLY. It removes; it never places or rolls. Under the sealed seed the
	/// generated zone is the same zone every time, so what this strips is the same every time. The
	/// file lives in <c>Harness/</c>, which the shipped manifest does not select and
	/// <c>Tools/stage.sh</c> excludes, so an ordinary build never compiles it.
	/// </para>
	/// </summary>
	public sealed class KingdomScenarioTestGroundBuilder
	{
		/// <summary>Journal verb column for the row this builder writes when it fires.</summary>
		internal const string BuiltRow = "TESTGROUND-BUILT";

		public bool BuildZone(Zone Z)
		{
			if (Z == null) return false;
			int removed = 0;
			int keptStairs = 0;
			int keptBare = 0;
			// The border ring is left alone: zone-edge cells carry the travel connections to the
			// neighbouring parasangs, and a test ground nobody can walk out of is not a test ground.
			for (int y = 1; y < Z.Height - 1; y++)
				for (int x = 1; x < Z.Width - 1; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null) continue;
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (item == null) continue;
						if (item.HasPart("StairsUp") || item.HasPart("StairsDown"))
						{
							keptStairs++;
							continue;
						}
						// Creatures are checked first: the production clearance law deliberately
						// skips them (in play they walk off), but a zone being BUILT has no player
						// to walk away from, and "no creatures beyond what wanders in later" is the
						// whole point of a born-clean ground.
						if (!item.IsCreature
							&& KingdomPlots.ReadObject(item) == KingdomPlotRules.GroundKind.Bare)
						{
							keptBare++;
							continue;
						}
						bool gone = false;
						try { gone = item.Obliterate(null, Silent: true); }
						catch (Exception) { }
						if (gone || !GameObject.Validate(item)) removed++;
					}
				}
			KingdomScenarioJournal.Append(BuiltRow, true, Describe(Z, removed, keptStairs, keptBare));
			return true;
		}

		/// <summary>
		/// Says what the ground actually is now, not what the builder tried to do. Anything that
		/// survived is counted by the same two predicates the staging canvas refuses on, so a
		/// persona that later fails to stage can be read against this row rather than guessed at.
		/// </summary>
		private static string Describe(Zone Z, int Removed, int KeptStairs, int KeptBare)
		{
			int impassable = 0;
			int liquid = 0;
			for (int y = 1; y < Z.Height - 1; y++)
				for (int x = 1; x < Z.Width - 1; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null) continue;
					if (!cell.IsPassable()) impassable++;
					if (cell.HasOpenLiquidVolume()) liquid++;
				}
			return (Z.ZoneID ?? "(unkeyed)") + ": cleared " + Removed + " object(s) from the "
				+ (Z.Width - 2) + "x" + (Z.Height - 2) + " interior; kept " + KeptStairs
				+ " stair(s) and " + KeptBare + " bare/widget object(s), plus the border ring for "
				+ "travel connections. Interior now has " + impassable + " impassable cell(s) and "
				+ liquid + " open-liquid cell(s).";
		}
	}
}

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Binds <see cref="XRL.World.ZoneBuilders.KingdomScenarioTestGroundBuilder" /> to the zone the
	/// scenario actually starts in. Carries no window, no data, and no build-code contribution.
	/// <para>
	/// SEAM. <c>QudGameBootModule.bootGame</c> resolves the starting location by firing
	/// <c>BootStartingLocation</c> through every enabled module in order, chaining the
	/// <c>GlobalLocation</c> element from one to the next, and only later calls
	/// <c>GlobalLocation.ResolveCell</c>, which is what generates the zone. This module is appended
	/// after every base-game module, so by the time it sees that element
	/// <c>QudChooseStartingLocationModule</c> has already filled it in - and there is still a whole
	/// boot sequence between here and generation. It reads the element and never alters it.
	/// </para>
	/// <para>
	/// Binding to the RESOLVED location rather than to the harness's own declared one is deliberate:
	/// <c>Tools/prepare-scenario.sh</c>'s <c>TAF_SCENARIO_START</c> rewrites that declaration inside
	/// the profile, and an operator walking chargen by hand may pick something else entirely.
	/// Whatever the flow settled on is what gets cleaned.
	/// </para>
	/// <para>
	/// Inert for every mode but the scenario mode, and inert without a sealed script: an attended
	/// profile is one an operator is driving by hand, and silently rewriting the world under them is
	/// exactly the surprise this harness must not spring.
	/// </para>
	/// </summary>
	public sealed class KingdomScenarioTestGroundModule : AbstractEmbarkBuilderModule
	{
		/// <summary>
		/// The builder class name. <c>ZoneBuilderBlueprint.Create</c> resolves it as
		/// <c>"XRL.World.ZoneBuilders." + Class</c> through <c>ModManager.ResolveType</c>, which
		/// searches mod assemblies, so the type above must keep that namespace and this name.
		/// </summary>
		internal const string BuilderClass = "KingdomScenarioTestGroundBuilder";

		/// <summary>Carries no data and contributes nothing to build codes.</summary>
		public override bool IncludeInBuildCodes()
		{
			return false;
		}

		/// <summary>No window, no selection, so no advance may ever be blocked on this module.</summary>
		public override string DataErrors()
		{
			return null;
		}

		/// <summary>Active only under the scenario mode. Never touches any other mode's flow.</summary>
		public override bool shouldBeEnabled()
		{
			QudGamemodeModule modes = builder == null
				? null
				: builder.GetModule<QudGamemodeModule>();
			if (modes == null) return false;
			return string.Equals(modes.GetMode(), KingdomScenarioFastEmbarkModule.ModeId,
				StringComparison.Ordinal);
		}

		public override object handleBootEvent(string id, XRLGame game, EmbarkInfo info,
			object element = null)
		{
			if (string.Equals(id, QudGameBootModule.BOOTEVENT_BOOTSTARTINGLOCATION,
				StringComparison.Ordinal))
				KingdomSystem.Guard("scenario test-ground binding",
					delegate { Bind(element as GlobalLocation); });
			return base.handleBootEvent(id, game, info, element);
		}

		/// <summary>
		/// Registers the post-builder, or does nothing at all. Fail-closed and silent by design: the
		/// scenario still runs on natural ground if this cannot bind, and the absence of a
		/// <c>TESTGROUND-BUILT</c> journal row is how a reader learns that it did not.
		/// </summary>
		private static void Bind(GlobalLocation Where)
		{
			if (Where == null || !KingdomScenarioScript.Present()) return;
			string zone = Where.ZoneID;
			if (string.IsNullOrEmpty(zone)) return;
			ZoneManager manager = The.ZoneManager;
			if (manager == null) return;
			manager.AddZonePostBuilder(zone, BuilderClass);
		}
	}
}
