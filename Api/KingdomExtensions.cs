using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	/// <summary>
	/// The registry: who is extending the city, and under what terms.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;6.6. Discovery is the engine's own idiom &mdash;
	/// <c>ModManager.GetInstancesWithAttribute&lt;T&gt;(typeof(A))</c>
	/// (<c>D/XRL/ModManager.cs:1185-1196</c>), the same call <c>WorldFactory</c> makes for world
	/// builders (<c>D/XRL/World/WorldFactory.cs:108</c>) &mdash; so a third-party mod needs no hard
	/// reference beyond the contract namespace and no line of ours.
	/// </para>
	/// <para>
	/// <b>The five invariants, enforced here rather than trusted.</b> Draws go through
	/// <see cref="KingdomExtensionDraws"/> and the kernel (clause 1). Every call crosses
	/// <c>KingdomExecutor.Submit</c>, so a frozen reading goes in, a frozen result comes out, and a
	/// source that throws or runs long stalls itself and nothing else (clauses 2 and 3). Telling
	/// goes through the ledger, the chronicle and <c>KingdomWord</c> under &sect;4.2's shared
	/// budget (clause 4). Everything an extension returns is clamped by
	/// <see cref="KingdomApiRules"/> before it reaches a surface (clause 5).
	/// </para>
	/// <para>
	/// <b>Refused loudly.</b> Version drift, a missing contract or a nameless owner is a refusal by
	/// mod name, in the log and in the message queue, with the version it wanted and the version we
	/// are. Never silently skipped, because a player attributes missing behaviour to us.
	/// </para>
	/// </summary>
	[HasModSensitiveStaticCache]
	public static partial class KingdomExtensions
	{
		/// <summary>
		/// One admitted extension and the mod that owns it. The immutable manifest ID is captured
		/// once at registration; display titles are presentation and may rename or collide.
		/// </summary>
		internal sealed class Binding
		{
			internal readonly string ModName;

			internal readonly string AssemblyName;

			internal readonly string TypeName;

			internal readonly IKingdomExtension Extension;

			internal Binding(string modName, string assemblyName, string typeName,
				IKingdomExtension extension)
			{
				ModName = modName;
				AssemblyName = assemblyName;
				TypeName = typeName;
				Extension = extension;
			}
		}

		/// <summary>
		/// Reset when the mod list changes, exactly as the engine resets its own
		/// (<c>ModManager.ResetModSensitiveStaticCaches</c>, <c>D/XRL/ModManager.cs:340-355</c>).
		/// A stale registry after a mod is disabled would go on running code the player removed.
		/// <para>
		/// Reset to <b>null</b> and not to an empty list: the reset writes
		/// <c>CreateEmptyInstance ? Activator.CreateInstance(fieldType) : null</c>
		/// (<c>D/XRL/ModManager.cs:351-352</c>), and an empty list is indistinguishable from "built
		/// and found nothing" &mdash; which would leave the registry permanently empty after any
		/// mod-list change instead of rebuilding it.
		/// </para>
		/// </summary>
		[ModSensitiveStaticCache]
		private static List<Binding> Bound;

		[ModSensitiveStaticCache]
		private static List<string> Refused;

		/// <summary>Runtime fault notices already shown this mod-list generation. Logging happens on
		/// every fault; the screen names each owner/lane once so a broken identity source cannot
		/// turn a daily reconciliation into message spam.</summary>
		[ModSensitiveStaticCache]
		private static HashSet<string> AnnouncedFaults;

		private const int MaxRuntimeFaultAnnouncements = 64;

		/// <summary>What <see cref="Enabled"/> read when the registry was built. The option is a
		/// checkbox the player can flip mid-session, and a registry that never noticed would go on
		/// running third-party code after the player switched it off.</summary>
		private static bool BuiltEnabled;

		/// <summary>Whether the behaviour lane is open at all. Off means the data lane still works
		/// &mdash; XML merge-by-key never depended on this &mdash; and no third-party C# runs.</summary>
		public static bool Enabled
		{
			get { return XRL.UI.Options.GetOption("r_TAF_OptionExtensions", "Yes") != "No"; }
		}

		/// <summary>The published API version, restated where a modder will look for it.</summary>
		public static int Version
		{
			get { return KingdomApiRules.Version; }
		}

		/// <summary>
		/// How many extensions are admitted, and what they are called. Reads the registry, building
		/// it on first ask.
		/// </summary>
		/// <returns>Mod names, one per admitted extension, in registration order. Empty when the
		/// lane is off or nothing is installed.</returns>
		public static List<string> Admitted()
		{
			List<string> names = new List<string>();
			foreach (Binding binding in Registry())
			{
				names.Add(binding.ModName);
			}
			return names;
		}

		/// <summary>
		/// Whether any admitted extension wants to be asked what happened.
		/// <para>
		/// Asked before the settlement pass reads the book at all: projecting a reading costs three
		/// array allocations, and a city with no extensions installed &mdash; which is nearly every
		/// city &mdash; must not pay them once a pass for nobody.
		/// </para>
		/// </summary>
		internal static bool AnyHappeningSource()
		{
			foreach (Binding binding in Registry())
			{
				if (binding.Extension is IKingdomHappeningSource)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Refusal lines from the last registration, for the founder's own report and for
		/// a bug report. Empty when nothing was refused.</summary>
		public static List<string> Refusals()
		{
			Registry();
			return new List<string>(Refused ?? new List<string>());
		}

		// ==================================================================================
		// Registration
		// ==================================================================================

		private static List<Binding> Registry()
		{
			bool enabled = Enabled;
			if (Bound != null && BuiltEnabled == enabled)
			{
				return Bound;
			}
			List<Binding> bound = new List<Binding>();
			List<string> refused = new List<string>();
			if (enabled)
			{
				KingdomSystem.Guard("kingdom extension registration", delegate
				{
					Collect(bound, refused);
					RefuseNamespaceCollisions(bound, refused);
				});
			}
			// Sorted, and not left in scan order. ModManager walks ActiveTypes, whose order is the
			// player's mod list -- so two installs with the same mods in a different order would
			// otherwise run the same extensions in a different sequence, and any draw either made
			// would land on a different ordinal. Determinism is not a property we get to leave to
			// a load order.
			bound.Sort(delegate(Binding a, Binding b)
			{
				int mod = string.CompareOrdinal(a.ModName, b.ModName);
				if (mod != 0) return mod;
				int assembly = string.CompareOrdinal(a.AssemblyName, b.AssemblyName);
				return (assembly != 0) ? assembly : string.CompareOrdinal(a.TypeName, b.TypeName);
			});
			refused.Sort(StringComparer.Ordinal);
			Bound = bound;
			Refused = refused;
			BuiltEnabled = enabled;
			Announce(refused);
			return Bound;
		}

		/// <summary>
		/// Finds every marked type and admits the ones that qualify.
		/// <para>
		/// <b>The scan is the engine's, the construction is ours</b>, and the split is deliberate.
		/// <c>ModManager.GetInstancesWithAttribute</c> (<c>D/XRL/ModManager.cs:1185-1196</c>) does
		/// both in one call, but its <c>Activator.CreateInstance</c> runs unguarded over every
		/// marked type &mdash; so one third-party class with no parameterless constructor would
		/// throw out of the middle of the loop and take every other mod's extension down with it.
		/// That is exactly the failure &sect;6.6 clause 3 forbids. The cached attribute scan is
		/// still the engine's own; only the per-type construction moved inside a guard.
		/// </para>
		/// </summary>
		private static void Collect(List<Binding> bound, List<string> refused)
		{
			foreach (Type type in ModManager.GetTypesWithAttribute(typeof(KingdomExtensionAttribute)))
			{
				if (type == null)
				{
					continue;
				}
				string owner = OwnerOf(type);
				if (!typeof(IKingdomExtension).IsAssignableFrom(type))
				{
					refused.Add(KingdomApiRules.RefusalLine(KingdomExtensionVerdict.RefusedNoContract, owner, 0));
					continue;
				}
				IKingdomExtension extension = null;
				int declared = 0;
				bool asked = false;
				// Third-party code, running before it has been admitted: the constructor and the
				// version getter are both asked inside the guard, so either throwing is a refusal
				// of THAT extension and not a crash of the registry.
				KingdomSystem.Guard("kingdom extension " + (type.FullName ?? type.Name), delegate
				{
					extension = Activator.CreateInstance(type) as IKingdomExtension;
					if (extension != null)
					{
						declared = extension.ApiVersion;
						asked = true;
					}
				});
				bool behaviour = extension is IResourceKind || extension is IJobKind
					|| extension is ICarrierKind || extension is INetworkKind
					|| extension is IWorkBehaviour;
				bool identity = extension is IKingdomIdentitySource;
				bool contract = extension is IKingdomAskSource || extension is IKingdomHappeningSource
					|| identity || behaviour;
				int required = behaviour ? KingdomApiRules.BehaviourVersion : (identity ? 2 : 1);
				KingdomExtensionVerdict verdict = (asked && extension != null)
					? KingdomApiRules.Judge(owner, declared, contract, required)
					: KingdomExtensionVerdict.RefusedThrew;
				if (verdict != KingdomExtensionVerdict.Accepted)
				{
					refused.Add(KingdomApiRules.RefusalLine(verdict, owner, declared, required));
					continue;
				}
				bound.Add(new Binding(owner, AssemblyNameOf(type),
					type.FullName ?? type.Name, extension));
			}
		}

		private static string OwnerOf(Type type)
		{
			if (type == null)
			{
				return "";
			}
			ModInfo mod = (type.Assembly == null) ? null : ModManager.GetMod(type.Assembly);
			if (mod != null)
			{
				return mod.ID ?? "";
			}
			string assembly = AssemblyNameOf(type);
			return string.IsNullOrEmpty(assembly) ? "" : assembly;
		}

		private static string AssemblyNameOf(Type type)
		{
			return type == null || type.Assembly == null ? "" : type.Assembly.GetName().Name ?? "";
		}

		/// <summary>Refuses every owner in a lossy canonical-namespace collision. First-wins would
		/// make mod load order transfer durable rows, identity keys, and draw streams across mods.</summary>
		private static void RefuseNamespaceCollisions(List<Binding> bound, List<string> refused)
		{
			Dictionary<string, string> firstByNamespace =
				new Dictionary<string, string>(StringComparer.Ordinal);
			HashSet<string> collidedOwners = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < bound.Count; i++)
			{
				string owner = bound[i].ModName;
				string ownerNamespace = KingdomApiRules.Kind(owner);
				string first;
				if (firstByNamespace.TryGetValue(ownerNamespace, out first)
					&& !string.Equals(first, owner, StringComparison.Ordinal))
				{
					collidedOwners.Add(first);
					collidedOwners.Add(owner);
				}
				else
				{
					firstByNamespace[ownerNamespace] = owner;
				}
			}
			if (collidedOwners.Count == 0) return;
			bound.RemoveAll(delegate(Binding binding)
			{
				return collidedOwners.Contains(binding.ModName);
			});
			foreach (string owner in collidedOwners)
				refused.Add(KingdomApiRules.RefusalLine(
					KingdomExtensionVerdict.RefusedNamespaceCollision, owner, 0));
		}

		private static void Announce(List<string> refused)
		{
			for (int i = 0; i < refused.Count; i++)
			{
				MetricsManager.LogError("ThousandAndFirst API: " + refused[i]);
				KingdomLog.Log("extension refused: " + refused[i]);
				if (The.Game != null)
				{
					MessageQueue.AddPlayerMessage("{{R|" + refused[i] + "}}");
				}
			}
		}

		// ==================================================================================
		// The published lanes
		// ==================================================================================

		/// <summary>
		/// Every extension-taught ask, clamped and attributed.
		/// <para>
		/// Preconditions: <paramref name="Reading"/> is the frozen reading the board is being built
		/// from. Side effects: none beyond a log line per faulted source. Failure mode: a source
		/// that throws or runs past its lane's budget contributes nothing, is logged by mod name,
		/// and does not stop the rest.
		/// </para>
		/// </summary>
		internal static List<KingdomAsk> Asks(KingdomSystem System, KingdomCityReading Reading, List<string> Stalled)
		{
			List<KingdomAsk> asks = new List<KingdomAsk>();
			if (System == null || Reading == null)
			{
				return asks;
			}
			foreach (Binding binding in Registry())
			{
				IKingdomAskSource source = binding.Extension as IKingdomAskSource;
				if (source == null)
				{
					continue;
				}
				asks.AddRange(Run(System, Reading, source, binding.ModName, false, Stalled));
			}
			return asks;
		}

		/// <summary>
		/// One ask source across the seam, clamped and attributed. The city's own source goes
		/// through this call too, so a gap in the published contract is a gap in our own board
		/// first (&sect;6.6's reason for opening at W5 rather than W1).
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Reading">The frozen reading.</param>
		/// <param name="Source">The source. Null yields nothing.</param>
		/// <param name="Owner">Who to attribute a fault to, and whose slug prefixes the kinds.</param>
		/// <param name="Own">True for the city's own source: its kinds are already the board's own
		/// vocabulary, so they are not prefixed, and it answers to the board's cap rather than to
		/// the per-extension one.</param>
		/// <param name="Stalled">Collects the owner of a source that faulted, so the board can say
		/// so out loud. STANDARDS &sect;7b: a source that contributed nothing because it broke is
		/// applicable-but-blocked, and a log line is not somewhere the founder will see it.</param>
		internal static List<KingdomAsk> Run(KingdomSystem System, KingdomCityReading Reading, IKingdomAskSource Source, string Owner, bool Own = false, List<string> Stalled = null)
		{
			List<KingdomAsk> asks = new List<KingdomAsk>();
			if (System == null || Reading == null || Source == null)
			{
				return asks;
			}
			bool own = Own;
			AskJob job = new AskJob(Source, new KingdomExtensionDraws(
				System.SimulationSeed, Reading.SettlementId, Owner), Owner, own);
			KingdomComputeResult<KingdomAsk[]> result = KingdomCity.Seam.Submit(Reading, job);
			if (!result.Published)
			{
				Fault(Owner, "asks", result.Status.ToString());
				if (Stalled != null && !Stalled.Contains(Owner))
				{
					Stalled.Add(Owner);
				}
				return asks;
			}
			Keep(result.Value, own ? KingdomAskRules.MaxAsks : KingdomApiRules.MaxAsksPerSource, own ? null : Owner, Reading, asks);
			return asks;
		}

		/// <summary>
		/// Every extension-taught happening since the city last asked, recorded to the chronicle
		/// and pushed only while the pass's shared telling budget has a line to spare.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Reading">The frozen reading.</param>
		/// <param name="Label">The city's name, for the word surface.</param>
		/// <param name="Here">Whether the founder is standing in this city.</param>
		/// <param name="CursorWire">Bounded per-source last-ask receipts.</param>
		/// <param name="PublishCursor">Publishes one prepared receipt before its source runs.</param>
		/// <param name="LegacySinceTick">Retired city-wide receipt. Used only to seed an absent
		/// per-source wire after upgrade; never authorizes a current per-source window.</param>
		/// <param name="NowTick">The pass's own clock, and the ceiling a notice may be dated at.
		/// Passed in rather than read off the reading: the book's processed-through tick can lag
		/// the pass by the part of a day it has not integrated yet, and a source that honestly
		/// dated a notice "now" would have it silently dropped as the future.</param>
		/// <param name="Spare">Told lines still unspent on this pass.</param>
		/// <returns>Lines actually pushed. Recording is unbudgeted; only the push is.</returns>
		internal static int Happenings(KingdomSystem System, KingdomCityReading Reading, string Label,
			bool Here, string CursorWire, Action<string> PublishCursor, long LegacySinceTick,
			long NowTick, int Spare)
		{
			int pushed = 0;
			if (System == null || Reading == null || PublishCursor == null)
			{
				return 0;
			}
			List<Binding> sources = new List<Binding>();
			List<string> sourceKeys = new List<string>();
			foreach (Binding binding in Registry())
			{
				IKingdomHappeningSource source = binding.Extension as IKingdomHappeningSource;
				if (source == null)
				{
					continue;
				}
				if (sources.Count >= KingdomHappeningCursorRules.MaxSources)
				{
					Fault(binding.ModName, "happenings", "SourceCap");
					continue;
				}
				string sourceKey;
				if (!KingdomHappeningCursorRules.TrySourceKey(binding.ModName,
					binding.AssemblyName, binding.TypeName, out sourceKey))
				{
					Fault(binding.ModName, "happenings", "SourceIdentity");
					continue;
				}
				sources.Add(binding);
				sourceKeys.Add(sourceKey);
			}
			string cursor = CursorWire ?? "";
			if (cursor.Length == 0 && LegacySinceTick > 0L)
			{
				if (LegacySinceTick > NowTick || !KingdomHappeningCursorRules.TrySeedLegacy(
					sourceKeys, LegacySinceTick, out cursor))
				{
					Fault("The Thousand and First", "happening cursors", "LegacySeedRefused");
					return 0;
				}
				PublishCursor(cursor);
			}
			if (!KingdomHappeningCursorRules.TryRetain(cursor, sourceKeys, out cursor))
			{
				Fault("The Thousand and First", "happening cursors", "MalformedWire");
				return 0;
			}
			if (!string.Equals(cursor, CursorWire ?? "", StringComparison.Ordinal))
				PublishCursor(cursor);
			for (int i = 0; i < sources.Count; i++)
			{
				Binding binding = sources[i];
				IKingdomHappeningSource source = (IKingdomHappeningSource)binding.Extension;
				long sinceTick;
				string prepared;
				if (!KingdomHappeningCursorRules.TryAdvance(cursor, sourceKeys[i], NowTick,
					out sinceTick, out prepared))
				{
					Fault(binding.ModName, "happenings", "CursorRefused");
					continue;
				}
				// Advance before third-party code. A throw therefore loses this window on the same
				// documented terms as a timeout; it cannot replay already-recorded notices after load.
				PublishCursor(prepared);
				cursor = prepared;
				HappeningJob job = new HappeningJob(source, sinceTick,
					new KingdomExtensionDraws(System.SimulationSeed, Reading.SettlementId,
						binding.ModName), binding.ModName);
				KingdomComputeResult<KingdomNotice[]> result = KingdomCity.Seam.Submit(Reading, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "happenings", result.Status.ToString());
					continue;
				}
				pushed += Record(System, binding, result.Value, Label, Here, NowTick, sinceTick,
					Spare - pushed);
			}
			return pushed;
		}

		/// <summary>
		/// Extra live roster keys every admitted identity source gives one frozen identity. Each
		/// source crosses the executor independently. Faulted sources contribute nothing; valid keys
		/// are bounded, attributed to their owner, folded, and de-duplicated.
		/// </summary>
		/// <param name="Reading">The frozen identity. No engine object crosses the seam.</param>
		/// <param name="Stalled">Optional distinct mod names whose source faulted or overran.</param>
		/// <returns>Fresh canonical keys in deterministic registry/source order.</returns>
		internal static List<string> IdentityKeys(KingdomIdentityReading Reading, List<string> Stalled = null)
		{
			List<string> keys = new List<string>();
			foreach (Binding binding in Registry())
			{
				IKingdomIdentitySource source = binding.Extension as IKingdomIdentitySource;
				if (source == null)
				{
					continue;
				}
				IdentityKeysJob job = new IdentityKeysJob(source, binding.ModName);
				KingdomComputeResult<string[]> result = KingdomCity.Seam.Submit(Reading, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "identity keys", result.Status.ToString());
					Stall(Stalled, binding.ModName);
					continue;
				}
				KeepIdentityKeys(result.Value, binding.ModName, keys);
			}
			return keys;
		}

		/// <summary>
		/// Composed extension affinity for one frozen identity and existing work kind. Each source
		/// crosses the executor independently; a fault is the neutral 100. Bounded source deltas are
		/// summed before one final clamp, so mixed opinions are independent of registry order.
		/// </summary>
		internal static int IdentityAffinity(KingdomIdentityReading Reading, string WorkKind,
			List<string> Stalled = null)
		{
			long affinityDelta = 0L;
			KingdomIdentityWorkReading request = new KingdomIdentityWorkReading(Reading, WorkKind);
			foreach (Binding binding in Registry())
			{
				IKingdomIdentitySource source = binding.Extension as IKingdomIdentitySource;
				if (source == null)
				{
					continue;
				}
				IdentityAffinityJob job = new IdentityAffinityJob(source, binding.ModName);
				KingdomComputeResult<int> result = KingdomCity.Seam.Submit(request, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "identity affinity", result.Status.ToString());
					Stall(Stalled, binding.ModName);
					continue;
				}
				affinityDelta += KingdomApiRules.IdentityAffinity(result.Value) - 100L;
			}
			return KingdomApiRules.IdentityAffinityFromDelta(affinityDelta);
		}

		private static void Stall(List<string> stalled, string owner)
		{
			if (stalled != null && !stalled.Contains(owner))
			{
				stalled.Add(owner);
			}
		}

		private static void KeepIdentityKeys(string[] source, string owner, List<string> into)
		{
			int kept = 0;
			for (int i = 0; source != null && i < source.Length
				&& i < KingdomApiRules.MaxIdentityKeyCandidatesPerSource
				&& kept < KingdomApiRules.MaxIdentityKeysPerSource; i++)
			{
				string key = KingdomApiRules.IdentityKey(owner, source[i]);
				if (key == null || into.Contains(key))
				{
					continue;
				}
				into.Add(key);
				kept++;
			}
		}

		private static int Record(KingdomSystem system, Binding binding, KingdomNotice[] notices, string label, bool here, long nowTick, long sinceTick, int spare)
		{
			int pushed = 0;
			int kept = 0;
			for (int i = 0; notices != null && i < notices.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall
				&& kept < KingdomApiRules.MaxNoticesPerSource; i++)
			{
				KingdomNotice notice = notices[i];
				string telling = KingdomApiRules.Trim(notice.Telling);
				string kind = KingdomApiRules.Kind(notice.Kind);
				if (string.IsNullOrEmpty(telling) || string.IsNullOrEmpty(kind))
				{
					continue;
				}
				// The city does not report the future, and it does not re-report what it already
				// told: a notice outside the window this lane was asked about is dropped rather
				// than filed with a wrong date.
				if (notice.Tick > nowTick || notice.Tick <= sinceTick)
				{
					continue;
				}
				kept++;
				KingdomChronicle.Record(system, telling);
				string spoken = KingdomApiRules.Trim(notice.Notice);
				if (pushed < spare && !string.IsNullOrEmpty(spoken))
				{
					KingdomWord.Ambient(system, label, here, spoken);
					pushed++;
				}
				KingdomLog.Log("extension happening: " + binding.ModName + " kind=" + kind + " tick=" + notice.Tick);
			}
			return pushed;
		}

		private static void Keep(KingdomAsk[] source, int limit, string modName, KingdomCityReading reading, List<KingdomAsk> into)
		{
			string prefix = string.IsNullOrEmpty(modName) ? "" : (KingdomApiRules.Slug(modName) + ":");
			int kept = 0;
			for (int i = 0; source != null && i < source.Length
				&& i < KingdomApiRules.MaxBehaviourCandidatesPerCall && kept < limit; i++)
			{
				KingdomAsk ask = source[i];
				string kind = KingdomApiRules.Kind(ask.Kind);
				string title = KingdomApiRules.Trim(ask.Title);
				if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(title))
				{
					continue;
				}
				kept++;
				into.Add(new KingdomAsk(
					prefix + kind,
					title,
					KingdomApiRules.Trim(ask.Want),
					Held(reading, ask.ZoneId),
					// Clamped DOWN, never up: an undefined weight is not a claim of urgency, and
					// clamping garbage to Grave would make it the loudest line on the board.
					(ask.Weight > KingdomAskWeight.Grave) ? KingdomAskWeight.Passing : ask.Weight));
			}
		}

		/// <summary>The ask's ground when the city actually holds it, and null otherwise. A board
		/// that named ground the city does not hold would send the founder somewhere that is not
		/// theirs, and the name would be fetched from the world to do it.</summary>
		private static string Held(KingdomCityReading reading, string zoneId)
		{
			if (reading == null || string.IsNullOrEmpty(zoneId))
			{
				return null;
			}
			for (int i = 0; i < reading.ZoneCount; i++)
			{
				KingdomZoneReading zone;
				if (reading.TryZone(i, out zone) && zone.ZoneId == zoneId)
				{
					return zoneId;
				}
			}
			return null;
		}

		private static void Fault(string owner, string lane, string status)
		{
			string line = owner + " stalled its own " + lane + " (" + status + "). The city is unaffected.";
			MetricsManager.LogError("ThousandAndFirst API: " + line);
			KingdomLog.Log("extension fault: " + owner + " lane=" + lane + " status=" + status);
			if (The.Game == null) return;
			if (AnnouncedFaults == null)
			{
				AnnouncedFaults = new HashSet<string>(StringComparer.Ordinal);
			}
			string key = (owner ?? "") + "|" + (lane ?? "");
			if (AnnouncedFaults.Count < MaxRuntimeFaultAnnouncements && AnnouncedFaults.Add(key))
			{
				MessageQueue.AddPlayerMessage("{{r|" + owner + " stalled its own " + lane
					+ ". The city is unaffected; the log names the fault.}}");
			}
		}

		// ==================================================================================
		// The jobs. Every one crosses the seam, so each inherits budget, timeout and error isolation
		// from the same contract our own computations do (§2.5).
		// ==================================================================================

		private sealed class AskJob : IKingdomComputation<KingdomCityReading, KingdomAsk[]>
		{
			private readonly IKingdomAskSource source;

			private readonly KingdomExtensionDraws draws;

			private readonly string label;

			internal AskJob(IKingdomAskSource source, KingdomExtensionDraws draws,
				string modName, bool own)
			{
				this.source = source;
				this.draws = draws;
				// The receipt distinguishes the city's own asks from an extension's, because
				// §6.5's whole point is that a regression has an owner.
				label = (own ? "asks:" : "ext:asks:") + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomCityReading input, out KingdomAsk[] output, out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Ask(input, draws);
				counters = new KingdomComputeCounters(0, output == null ? 0L : output.Length,
					draws.ReportedDraws, 0, 0L);
				fault = KingdomCityFault.None;
				return true;
			}
		}

		private sealed class HappeningJob : IKingdomComputation<KingdomCityReading, KingdomNotice[]>
		{
			private readonly IKingdomHappeningSource source;

			private readonly long sinceTick;

			private readonly KingdomExtensionDraws draws;

			private readonly string label;

			internal HappeningJob(IKingdomHappeningSource source, long sinceTick,
				KingdomExtensionDraws draws, string modName)
			{
				this.source = source;
				this.sinceTick = sinceTick;
				this.draws = draws;
				label = "ext:happenings:" + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomCityReading input, out KingdomNotice[] output, out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Happen(input, sinceTick, draws);
				counters = new KingdomComputeCounters(0, output == null ? 0L : output.Length,
					draws.ReportedDraws, 0, 0L);
				fault = KingdomCityFault.None;
				return true;
			}
		}

		private sealed class IdentityKeysJob : IKingdomComputation<KingdomIdentityReading, string[]>
		{
			private readonly IKingdomIdentitySource source;

			private readonly string label;

			internal IdentityKeysJob(IKingdomIdentitySource source, string modName)
			{
				this.source = source;
				label = "ext:identity-keys:" + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomIdentityReading input, out string[] output,
				out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Keys(input);
				counters = KingdomComputeCounters.None;
				fault = KingdomCityFault.None;
				return true;
			}
		}

		private sealed class IdentityAffinityJob
			: IKingdomComputation<KingdomIdentityWorkReading, int>
		{
			private readonly IKingdomIdentitySource source;

			private readonly string label;

			internal IdentityAffinityJob(IKingdomIdentitySource source, string modName)
			{
				this.source = source;
				label = "ext:identity-affinity:" + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomIdentityWorkReading input, out int output,
				out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Affinity(input.Identity, input.WorkKind);
				counters = KingdomComputeCounters.None;
				fault = KingdomCityFault.None;
				return true;
			}
		}
	}
}
