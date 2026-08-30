using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The founding scenario slice: prove the world holds no realm, then drive the ONE production
	/// first-city founding transaction ordinary play uses, at the operator's position in the
	/// born-clean test zone.
	/// <para>
	/// Same shape as <see cref="KingdomScenarioGallerySlice"/>: the production entry is called
	/// directly rather than through the debug wish, because the wish reports through Popup.Show
	/// and a sealed script suppresses popups; every refusal string here reaches the journal
	/// verbatim. The wish's popup is presentation, never part of the transaction.
	/// </para>
	/// <para>
	/// OBSERVATION NEVER MINTS STATE. Every read here fetches the kingdom system by presence
	/// (GetSystem, never RequireSystem); only the production transaction itself may mint it.
	/// </para>
	/// <para>
	/// MARKER LAW IS INHERITED, NOT DUPLICATED. This shard owns no marker code: the attended
	/// runner writes the shared per-game attempt marker before <see cref="TryFound"/> and advances
	/// it to committed on return, exactly as it does for the gallery staging, so a second realize
	/// refuses on <c>taf-scenario-transaction-committed</c> with no founding-specific state.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioFoundingStep
	{
		/// <summary>The founding production authority class, as the roster declares it.</summary>
		internal const string FoundingAuthority = "founding-transaction";

		/// <summary>Stable code carried beside the founding report tokens.</summary>
		internal const string CodeFounding = "taf-scenario-founding";

		/// <summary>A realm already stands: founding twice is a refusal, never a crash.</summary>
		internal const string CodeAlreadyFounded = "taf-scenario-founding-already-founded";

		/// <summary>Read-only: proves no realm stands, so the ground is claimable as a first city.</summary>
		internal static bool TryProveUnfounded(out string Detail, out string Failure)
		{
			Detail = null;
			Failure = null;
			KingdomSystem system = Observe();
			if (system != null && system.Founded)
				return Refuse("[" + CodeAlreadyFounded + "] a realm already stands ("
					+ (system.KingdomFactionName ?? "-")
					+ "); this world cannot found a first city", out Failure);
			Detail = "no founded realm; the ground is claimable as a first city";
			return true;
		}

		/// <summary>
		/// Read-only preconditions for the single production transaction, probed BEFORE the
		/// durable attempt marker exactly as the gallery slice probes its canvas: the production
		/// entry refuses these same states anyway, but its refusal would land after the marker and
		/// permanently spend the profile. A conservative pre-marker refusal is always lawful.
		/// </summary>
		internal static bool TryProvePreconditions(Zone Site, string Name, out string Failure)
		{
			Failure = null;
			if (Site == null) return Refuse("no loaded zone to found in", out Failure);
			if (The.ZoneManager == null || The.ZoneManager.ActiveZone != Site)
				return Refuse("direct founding inspects only the exact active ground; the "
					+ "operator must stand in the active zone", out Failure);
			string normalized;
			string nameFailure;
			if (!KingdomPresentationRules.TryNormalizeName(Name, out normalized, out nameFailure))
				return Refuse("the frozen city name is refused by the production name law: "
					+ KingdomScenarioRules.Bounded(nameFailure ?? "unnamed"), out Failure);
			string detail;
			return TryProveUnfounded(out detail, out Failure);
		}

		/// <summary>
		/// The single production transaction: the SAME direct first-founding entry the debug wish
		/// drives, unchanged. Runs between the caller's attempt marker and its commit. On success
		/// the report line carries stable tokens beside <see cref="CodeFounding"/>.
		/// </summary>
		internal static bool TryFound(Zone Site, string Name, out string Line, out string Failure)
		{
			Line = null;
			Failure = null;
			Faction faction = null;
			try
			{
				string refused;
				if (!KingdomFoundingTransaction.TryFoundFirstWithoutWater(Name, Site,
						out faction, out refused) || faction == null)
					return Refuse("the production founding refused: "
						+ KingdomScenarioRules.Bounded(string.IsNullOrEmpty(refused)
							? "unnamed" : refused), out Failure);
			}
			catch (Exception exception)
			{
				return Refuse("the production founding path threw: "
					+ KingdomScenarioRules.Bounded(exception.Message), out Failure);
			}
			KingdomSystem system = Observe();
			if (system == null || !system.Founded)
				return Refuse("the production founding returned a faction but no founded "
					+ "kingdom system stands", out Failure);
			Line = "[" + CodeFounding + "] faction=" + (system.KingdomFactionName ?? "-")
				+ " claimedzones="
				+ ClaimedZoneCount(system).ToString(CultureInfo.InvariantCulture)
				+ " stage=" + system.Stage;
			return true;
		}

		/// <summary>
		/// Measures the founding-transaction key set off the founded system, however it was
		/// founded. Recomputed from durable save state, like the architecture keys: the persisted
		/// system is the authority and nothing here reads a run-local cache.
		/// <para>
		/// The realm faction NAME is deliberately measured as presence, not value - see the key
		/// declaration in <see cref="KingdomScenarioAnchorRules"/> for why a GUID-minted identity
		/// key could never satisfy a differential.
		/// </para>
		/// </summary>
		internal static bool TryMeasure(out IDictionary<string, string> Captured,
			out string Failure)
		{
			Captured = null;
			Failure = null;
			KingdomSystem system = Observe();
			if (system == null || !system.Founded)
				return Refuse("no founded realm exists to measure; the founding key set is "
					+ "measurable only off a founded world", out Failure);
			bool registered = !string.IsNullOrEmpty(system.KingdomFactionName)
				&& Factions.GetIfExists(system.KingdomFactionName) != null;
			Captured = new SortedDictionary<string, string>(StringComparer.Ordinal)
			{
				{ "founding.founded", "true" },
				{ "founding.faction.present", registered ? "true" : "false" },
				{ "founding.claimedzones",
					ClaimedZoneCount(system).ToString(CultureInfo.InvariantCulture) },
				{ "founding.stage", system.Stage.ToString() }
			};
			return true;
		}

		private static int ClaimedZoneCount(KingdomSystem System)
		{
			return System.ClaimedZones == null ? 0 : System.ClaimedZones.Count;
		}

		/// <summary>The system as it stands, or null. Presence only; observation mints nothing.</summary>
		private static KingdomSystem Observe()
		{
			XRLGame game = The.Game;
			return game == null ? null : game.GetSystem<KingdomSystem>();
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
