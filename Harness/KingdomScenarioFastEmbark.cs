using System;
using System.Collections.Generic;

using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.UI;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Dev-only fast embark for the scenario mode. Registered from <c>Harness/EmbarkModules.xml</c>,
	/// which only ever loads inside a throwaway scenario profile, and inert for every other mode.
	/// <para>
	/// SEAMS. <c>QudGamemodeModule.SelectMode</c> calls <c>setData</c> before its own
	/// <c>builder.advance()</c>, and <c>EmbarkBuilder.NotifyModuleChanges</c> hands that change to
	/// every other module through <see cref="handleModuleDataChange" />. That is the earliest point
	/// at which the chosen mode is knowable, so it is where this module pre-fills the flow. The mode
	/// descriptor's <c>stringGameStates</c> are parsed out of the profile's own EmbarkModules.xml at
	/// load time, so the sealed request - and the seed frozen into it by
	/// <c>Tools/prepare-scenario.sh</c> - is readable here, long before <c>bootGame</c> applies those
	/// same states to the game.
	/// </para>
	/// <para>
	/// SEED AUTHORITY. This module assigns <c>EmbarkInfo.GameSeed</c> exactly as the native
	/// <c>Options.EnableSeed</c> popup does (verbatim assignment, no normalization - see
	/// <c>QudCustomizeCharacterModuleWindow.SelectMenuOption</c>). It is written LAST, after the
	/// pregen build code has been loaded, because a build code deserializes module data and this
	/// module refuses to depend on the order in which it does so. The gate is untouched:
	/// <c>KingdomScenarioRealizer.TryProveSeed</c> still reads the engine's own
	/// <c>OriginalWorldSeed</c> and <c>GetWorldSeed()</c> after generation and refuses a world that
	/// does not match.
	/// </para>
	/// <para>
	/// FAIL-CLOSED. Any missing or malformed input - no request state, an unparseable request, a
	/// request with no frozen seed, an absent pregen or starting location, a missing stock module -
	/// surfaces one named popup and changes NOTHING. Ordinary character creation is left intact and
	/// the operator can still walk it by hand. Nothing here runs for any mode but the scenario mode.
	/// </para>
	/// </summary>
	public sealed class KingdomScenarioFastEmbarkModule : AbstractEmbarkBuilderModule
	{
		/// <summary>The one mode this module acts on. Must match Harness/EmbarkModules.xml.</summary>
		internal const string ModeId = "TAFScenario";

		/// <summary>
		/// The fixed build. A pregen is a whole preset - genotype, subtype, mutations, attributes -
		/// carried as one build code, so applying it removes every remaining chargen window's work in
		/// a single step. "Praetorian Prime" is the base game's own most-survivable preset and a True
		/// Kin, so no mutation roll perturbs a pinned-seed run. The name is resolved by lookup and
		/// refused if absent, never assumed.
		/// </summary>
		internal const string PregenName = "Praetorian Prime";

		/// <summary>The chartype a pregen build lives under; gates QudPregenModule.shouldBeEnabled.</summary>
		internal const string PregenChartype = "Pregen";

		/// <summary>
		/// Fixed start. The location window is the last in the flow and errors without a selection,
		/// so leaving it unset would replace one piece of friction with another.
		/// <para>
		/// The harness declares its own location in <c>Harness/EmbarkModules.xml</c> - open salt
		/// dunes west of Joppa, whose whole 3x3 parasang region is one uniform terrain - rather than
		/// reusing the recommended start. The scenario script's first verb prepares ground, and
		/// Joppa's zone is a built village whose cells the production clearance law lawfully refuses;
		/// a run that embarked there would spend its one non-retryable profile learning that. Like
		/// Joppa, the dev location grants no items, skills, or reputation. Its parasang is a
		/// prepare-time knob: <c>Tools/prepare-scenario.sh</c>'s <c>TAF_SCENARIO_START</c> rewrites
		/// the declared location inside the throwaway profile before the profile is sealed.
		/// </para>
		/// </summary>
		internal const string StartingLocationId = "TAFTestGround";

		/// <summary>Last window in the stock flow. The walk stops here; see <see cref="Advance" />.</summary>
		internal const string FinalWindowViewId = "Chargen/ChooseStartingLocation";

		/// <summary>
		/// Hard bound on the auto-advance walk. The stock flow is thirteen windows; a cap an order of
		/// magnitude above that ends a walk that stops making progress for a reason nobody predicted,
		/// rather than spinning inside a UI callback.
		/// </summary>
		internal const int MaxAdvances = 32;

		/// <summary>
		/// Re-entrancy latch. Every <c>setData</c> below re-enters <see cref="handleModuleDataChange" />
		/// through <c>NotifyModuleChanges</c>, so this is raised BEFORE the first one. It also makes
		/// the pre-fill once-per-builder: a second mode selection must not re-apply it.
		/// </summary>
		private bool Applied;

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
			QudGamemodeModule modes = GamemodeModule();
			if (modes == null) return false;
			return string.Equals(modes.GetMode(), ModeId, StringComparison.Ordinal);
		}

		/// <summary>
		/// Sorts the dev tile FIRST in the mode carousel.
		/// <para>
		/// <c>QudGamemodeModuleWindow.GetSelections</c> enumerates <c>GameModes.Values</c> directly and
		/// the engine offers no ordering attribute, so carousel order IS dictionary insertion order,
		/// which is base-game-then-mods. That put the dev tile last and off-screen. Rebuilding the
		/// dictionary with this mode first is the only seam available and is dev-profile-only: an
		/// ordinary build never loads the file that registers this module. Every other mode keeps its
		/// relative order and no descriptor is altered.
		/// </para>
		/// <para>
		/// Runs in <c>Init</c>, which <c>EmbarkBuilder.InitModules</c> calls for every module before
		/// any window is assembled or shown - and, because a mod module is appended after the base
		/// ones, after <c>QudGamemodeModule.Init</c> has added its own Quickstart tile.
		/// </para>
		/// </summary>
		public override void Init()
		{
			base.Init();
			QudGamemodeModule modes = GamemodeModule();
			if (modes == null || modes.GameModes == null) return;
			QudGamemodeModule.GameModeDescriptor scenario;
			if (!modes.GameModes.TryGetValue(ModeId, out scenario) || scenario == null) return;
			Dictionary<string, QudGamemodeModule.GameModeDescriptor> ordered =
				new Dictionary<string, QudGamemodeModule.GameModeDescriptor>(StringComparer.Ordinal);
			ordered.Add(ModeId, scenario);
			foreach (KeyValuePair<string, QudGamemodeModule.GameModeDescriptor> row in modes.GameModes)
				if (!string.Equals(row.Key, ModeId, StringComparison.Ordinal))
					ordered.Add(row.Key, row.Value);
			modes.GameModes = ordered;
		}

		/// <summary>
		/// The pre-fill trigger. Fires once, only for a change that selects the scenario mode.
		/// </summary>
		public override void handleModuleDataChange(AbstractEmbarkBuilderModule module,
			AbstractEmbarkBuilderModuleData oldValues, AbstractEmbarkBuilderModuleData newValues)
		{
			if (Applied) return;
			if (!(module is QudGamemodeModule)) return;
			QudGamemodeModuleData chosen = newValues as QudGamemodeModuleData;
			if (chosen == null || !string.Equals(chosen.Mode, ModeId, StringComparison.Ordinal)) return;
			// Raised before any setData, because each one re-enters this method.
			Applied = true;
			string failure;
			if (TryApply((QudGamemodeModule)module, out failure)) return;
			Popup.ShowAsync("The {{W|[Dev] TAF scenario}} mode could not pre-fill character creation: "
				+ KingdomScenarioRules.Bounded(failure ?? "unknown fault")
				+ ".\n\nOrdinary character creation is untouched. Walk it by hand, and set the world "
				+ "seed frozen by Tools/prepare-scenario.sh yourself before the world is generated - "
				+ "the new-game gate refuses any other world.");
		}

		/// <summary>
		/// Resolves EVERYTHING before mutating anything, then applies in one ordered pass. A refusal
		/// discovered halfway through would leave chargen half-filled, which is worse than the
		/// friction this removes.
		/// </summary>
		private bool TryApply(QudGamemodeModule Modes, out string Failure)
		{
			Failure = null;
			if (builder == null) return Refuse("there is no embark builder to fill", out Failure);
			string seed;
			if (!TryFrozenSeed(Modes, out seed, out Failure)) return false;
			QudChartypeModule chartype = builder.GetModule<QudChartypeModule>();
			QudPregenModule pregens = builder.GetModule<QudPregenModule>();
			QudChooseStartingLocationModule locations =
				builder.GetModule<QudChooseStartingLocationModule>();
			if (chartype == null || pregens == null || locations == null || pregens.pregens == null
				|| locations.startingLocations == null)
				return Refuse("the stock character-creation modules are not all present", out Failure);
			QudPregenModule.QudPregenData pregen;
			if (!pregens.pregens.TryGetValue(PregenName, out pregen) || pregen == null
				|| string.IsNullOrEmpty(pregen.Code))
				return Refuse("this build carries no pregen named '" + PregenName
					+ "' with a build code", out Failure);
			// The harness declares this location itself, so its absence means the overlay's
			// EmbarkModules.xml did not reach the location module - not that the base game changed.
			if (!locations.startingLocations.ContainsKey(StartingLocationId))
				return Refuse("the harness starting location '" + StartingLocationId
					+ "' is not registered; the profile's Harness/EmbarkModules.xml did not load",
					out Failure);
			// Chartype first: QudPregenModule.shouldBeEnabled reads it, so the pregen module is not
			// enabled until this lands.
			chartype.setData(new QudChartypeModuleData(PregenChartype));
			pregens.setData(new QudPregenModuleData(PregenName));
			// Exactly what QudPregenModule.SelectPregen does, minus its advanceToSummary: the walk
			// below is this module's, so the two never fight over where the flow stops.
			builder.InitModulesFromCode(pregen.Code);
			// LAST of the pre-fill. Nothing after this point writes GameSeed, so nothing can
			// silently replace the sealed seed with the random one QudGameBootModule.Init handed
			// out - the walk below shows windows, and no window's BeforeShow assigns it.
			builder.info.GameSeed = seed;
			Advance();
			// AFTER the walk, deliberately. Every window the walk passes is shown, and
			// QudChooseStartingLocationModuleWindow.BeforeShow REPLACES the module's data with a
			// fresh one pinned to "Joppa". A location set before the walk is therefore discarded;
			// only an assignment made once the walk has stopped ON that window survives into boot.
			locations.setData(new QudChooseStartingLocationModuleData(StartingLocationId));
			return true;
		}

		/// <summary>
		/// Reads the sealed request off the mode descriptor and returns the seed it froze.
		/// <para>
		/// The descriptor's states come from the profile's own EmbarkModules.xml, which
		/// <c>Tools/prepare-scenario.sh</c> rewrites and then seals; the launcher re-proves the
		/// overlay still carries exactly the sealed request before the game starts. Parsing goes
		/// through the harness's own total, bounded request parser rather than a local split, so a
		/// malformed request is refused here by the same rule the gate applies later.
		/// </para>
		/// </summary>
		private static bool TryFrozenSeed(QudGamemodeModule Modes, out string Seed, out string Failure)
		{
			Seed = null;
			Failure = null;
			if (Modes == null || Modes.GameModes == null)
				return Refuse("the game-mode module declares no modes", out Failure);
			QudGamemodeModule.GameModeDescriptor descriptor;
			if (!Modes.GameModes.TryGetValue(ModeId, out descriptor) || descriptor == null
				|| descriptor.stringGameStates == null)
				return Refuse("the scenario mode declares no string game states", out Failure);
			string request;
			if (!descriptor.stringGameStates.TryGetValue(
				KingdomScenarioNewGameGate.RequestState, out request))
				return Refuse("the scenario mode declares no '"
					+ KingdomScenarioNewGameGate.RequestState + "' request", out Failure);
			string key;
			IDictionary<string, string> selection;
			string seed;
			if (!KingdomScenarioRequest.TryParse(request, out key, out selection, out seed,
				out Failure)) return false;
			if (seed == null)
				return Refuse("the request '" + KingdomScenarioRules.Bounded(request)
					+ "' froze no seed; prepare a fresh profile with Tools/prepare-scenario.sh",
					out Failure);
			Seed = seed;
			return true;
		}

		/// <summary>
		/// Walks the remaining windows to the end of the stock flow.
		/// <para>
		/// <c>advance(force: true)</c> skips <c>checkStateAsync</c> entirely, so the walk raises no
		/// error or warning popup and runs synchronously; every window it passes is SHOWN, exactly as
		/// the engine's own Quickstart path does, so each one still gets the <c>BeforeShow</c> that
		/// initializes its data. The walk stops AT the last window rather than past it: the
		/// <c>builder.advance()</c> that <c>SelectMode</c> runs immediately after this returns
		/// supplies the final step into world generation. If that step ever stops firing, the
		/// operator lands on a fully pre-filled starting-location window and one confirm finishes it.
		/// </para>
		/// <para>
		/// Three independent stops: the flow already finished, the flow reached the last window, or
		/// an advance made no progress. The iteration cap is the backstop for none of them holding.
		/// </para>
		/// </summary>
		private void Advance()
		{
			for (int step = 0; step < MaxAdvances; step++)
			{
				if (EmbarkBuilder.finishedEvent != null
					&& EmbarkBuilder.finishedEvent.Task.IsCompleted) return;
				EmbarkBuilderModuleWindowDescriptor before = builder.activeWindow;
				if (before != null
					&& string.Equals(before.viewID, FinalWindowViewId, StringComparison.Ordinal))
					return;
				builder.advance(true);
				if (builder.activeWindow == before) return;
			}
		}

		/// <summary>The gamemode module, or null when the builder is not assembled yet.</summary>
		private QudGamemodeModule GamemodeModule()
		{
			return builder == null ? null : builder.GetModule<QudGamemodeModule>();
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
