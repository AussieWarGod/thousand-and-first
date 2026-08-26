using System;
using System.Collections.Generic;
using System.Reflection;

using Qud.API;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the procedure system: the registry of authored records, the
	/// founder's ledger of which named procedures they have found, the read of a real body into the
	/// engine-free vocabulary the rules judge in, and the three write paths that actually change a
	/// founder.
	/// <para>
	/// <b>The whole doctrine, in one sentence: your sting is its sting.</b> What the founder gets is
	/// the source creature's own part with the source creature's own numbers in it, never a fresh
	/// instance built from a class name. Playable Slime grants by name and loses the field state;
	/// the precedent had to hand-patch one mutation's identity because of it, and this system makes
	/// that class of bug structurally impossible by never learning a creature's name at all.
	/// </para>
	/// <para>
	/// <b>How that survives the preservation chain, which is not how the precedent does it.</b>
	/// Trophic Absorption snapshots a live <c>PartsList</c> and calls <c>IPart.DeepCopy</c> in the
	/// same turn, because its source is still in memory. Ours is not: the creature is butchered, the
	/// raw part is obliterated into preserved parts at the vat-house, and the graft may happen a
	/// season and a reload later. So the field state is STAMPED onto the preserved item at butcher
	/// time (<c>KingdomProcedureRules.FormatStamp</c>) and the part is rebuilt from the stamp at
	/// graft time by instantiating the type and setting its fields from strings &mdash; which is
	/// what <c>GamePartBlueprint</c> itself does with every part in the game
	/// (<c>D/XRL/World/GamePartBlueprint.cs</c>), and which preserves the doctrine exactly while
	/// asking nothing of an object that no longer exists.
	/// </para>
	/// </summary>
	public static partial class KingdomProcedures
	{
		/// <summary>Whether the lab is switched on. Off, no record loads, nothing is discovered, and
		/// no building offers a verb.</summary>
		public static bool Enabled => XRL.UI.Options.GetOption("r_TAF_OptionLab") != "No";

		/// <summary>The journal id a named procedure's discovery bit lives under. A string in the
		/// save rather than an ordinal, so a mod adding procedures never renumbers ours.</summary>
		public const string NotePrefix = "taf:procedure:";

		/// <summary>The journal category a found procedure files under.</summary>
		public const string NoteCategory = "general";

		/// <summary>
		/// The property a preserved part carries its stamp under: what the creature was bearing,
		/// read BEFORE it was butchered.
		/// </summary>
		public const string StampProperty = "r_TAF_LabStamp";

		/// <summary>The property a preserved part carries the source's own display name under, so
		/// the slate can say what a thing came off without holding a reference to a dead creature.</summary>
		public const string SourceProperty = "r_TAF_LabSource";

		/// <summary>Per-procedure ownership marker. Removal requires this marker and an exact record
		/// identity, so a native same-class effect is never selected by a class scan.</summary>
		public static string OwnerProperty(string Key)
		{
			return "r_TAF_LabOwner::" + (Key ?? "").Trim().ToLowerInvariant();
		}

		public static string OwnerNonceProperty(string Key)
		{
			return "r_TAF_LabOwnerNonce::" + (Key ?? "").Trim().ToLowerInvariant();
		}

		/// <summary>
		/// Every graft's manager key, so <c>Body.RemovePartsByManager</c> undoes one in a single
		/// call &mdash; the precedent's own reversal shape
		/// (<c>D/XRL/World/Parts/CyberneticsGraftedMirrorArm.cs:38</c>).
		/// </summary>
		public static string ManagerFor(string Key)
		{
			return "TAF::Lab::" + (Key ?? "");
		}

		private static List<LabProcedure> _procedures;

		private static readonly Dictionary<string, LabProcedure> ByKey = new Dictionary<string, LabProcedure>();

		private static bool NotesFiled;

		/// <summary>The whole registry, in the order the files declared it. Ties anywhere in this
		/// system break on key ascending, so the same founder on the same save reads the same slate
		/// in the same order.</summary>
		public static List<LabProcedure> All
		{
			get
			{
				EnsureLoaded();
				return _procedures;
			}
		}

		/// <summary>One record by key, or false. Keys are folded, like every other registry's.</summary>
		public static bool TryGet(string Key, out LabProcedure Procedure)
		{
			EnsureLoaded();
			Procedure = null;
			if (string.IsNullOrEmpty(Key))
			{
				return false;
			}
			return ByKey.TryGetValue(Key.Trim().ToLowerInvariant(), out Procedure);
		}

		/// <summary>Forgets the registry and everything cached about the world. Called by the
		/// registry loader and on a game load, so a reload never leaves a record or a filed journal
		/// note behind from another game.</summary>
		public static void Reload()
		{
			_procedures = null;
			ByKey.Clear();
			NotesFiled = false;
		}

		// ==================================================================================
		// The registry
		// ==================================================================================

		private static void EnsureLoaded()
		{
			if (_procedures != null)
			{
				return;
			}
			_procedures = new List<LabProcedure>();
			ByKey.Clear();
			Dictionary<string, Action<XmlDataHelper>> handlers = null;
			handlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdomprocedures",
					delegate(XmlDataHelper xml)
					{
						KingdomXmlSchema.HandleRoot(xml, handlers, "KingdomProcedures");
					}
				},
				{ "procedure", HandleProcedure }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot("KingdomProcedures"))
			{
				item.HandleNodes(handlers);
			}
			foreach (string finding in KingdomProcedureRules.Validate(_procedures))
			{
				KingdomLog.Log("KingdomProcedures: " + finding);
			}
		}

		private static void HandleProcedure(XmlDataHelper xml)
		{
			// Every attribute is read unconditionally, for the reason the catalogue reads its own
			// that way: the engine records which attributes a pass asked for and warns about the
			// rest, so a pass that skips one on a fault makes the loader complain about the file.
			string key = xml.GetAttribute("Key");
			string displayName = xml.GetAttribute("DisplayName");
			string cls = xml.GetAttribute("Class");
			string grants = xml.GetAttribute("Grants");
			string slots = xml.GetAttribute("Slots");
			string slotCategories = xml.GetAttribute("SlotCategories");
			string source = xml.GetAttribute("Source");
			string attach = xml.GetAttribute("Attach");
			string minRung = xml.GetAttribute("MinRung");
			string cost = xml.GetAttribute("Cost");
			string bits = xml.GetAttribute("Bits");
			string staffDays = xml.GetAttribute("StaffDays");
			string preserved = xml.GetAttribute("Preserved");
			string creeds = xml.GetAttribute("Creeds");
			string knowledge = xml.GetAttribute("Knowledge");
			string magnitude = xml.GetAttribute("Magnitude");
			LabProcedure procedure;
			string error;
			if (!KingdomProcedureRules.TryParseProcedureAttributes(key, displayName, cls, grants, slots, slotCategories,
				source, attach, minRung, cost, bits, staffDays, preserved, creeds, knowledge, magnitude,
				out procedure, out error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomProcedures: " + error);
				SkipChildren(xml);
				return;
			}
			// HandleNodes stands in for DoneWithElement: it returns at once on a self-closing
			// <procedure/> and otherwise dispatches the disclosure lines, which a merging file
			// appends to exactly as it appends skins to a building.
			xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"discloses",
					delegate(XmlDataHelper child)
					{
						string text = child.GetAttribute("Text");
						if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
						{
							procedure.Discloses.Add(text.Trim());
						}
						child.DoneWithElement();
					}
				}
			});
			for (int i = 0; i < _procedures.Count; i++)
			{
				if (_procedures[i].Key == procedure.Key)
				{
					// In place, so the registry keeps first-declaration order: a mod that re-prices
					// a procedure does not move it to the bottom of the founder's slate.
					_procedures[i] = procedure;
					ByKey[procedure.Key] = procedure;
					return;
				}
			}
			_procedures.Add(procedure);
			ByKey[procedure.Key] = procedure;
		}

		private static void SkipChildren(XmlDataHelper xml)
		{
			xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>(), delegate(XmlDataHelper child)
			{
				child.DoneWithElement();
			});
		}

		// ==================================================================================
		// Discovery — the visibility law, enforced by the accessor and not by discipline
		// ==================================================================================

		/// <summary>The journal id one named procedure's discovery bit lives under.</summary>
		public static string NoteId(string Key)
		{
			return string.IsNullOrEmpty(Key) ? null : (NotePrefix + Key.Trim().ToLowerInvariant());
		}

		/// <summary>
		/// Files one unrevealed journal note per named procedure, once per game. Vanilla refuses an
		/// id it already holds, so this is idempotent whatever calls it.
		/// </summary>
		public static void FileNotes()
		{
			if (NotesFiled || !Enabled)
			{
				return;
			}
			EnsureLoaded();
			NotesFiled = true;
			for (int i = 0; i < _procedures.Count; i++)
			{
				LabProcedure procedure = _procedures[i];
				if (!procedure.IsNamed)
				{
					continue;
				}
				string id = NoteId(procedure.Key);
				if (id == null || JournalAPI.GetObservation(id) != null)
				{
					continue;
				}
				JournalAPI.AddObservation(
					"There is a thing that can be done to a body, and it is called " + procedure.Named + ".",
					id, NoteCategory, id, null, revealed: false, -1L);
			}
		}
	}
}
