using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomMergeRules
	{
		// --- The guardrail ------------------------------------------------------------------

		/// <summary>
		/// What a work that is already standing sees once the catalogue under it has been merged
		/// into.
		/// <para>
		/// The materialised half &mdash; every <see cref="MergeReach.Spent"/> and
		/// <see cref="MergeReach.Stamped"/> attribute &mdash; comes back exactly as the work was
		/// raised, whatever the merged draft now says, and comes back as a copy so no caller can
		/// reach the work through it. The read half follows the merge, which is how a later file's
		/// skin becomes available to re-dress a standing house and how a later file's chain link
		/// becomes something a standing hut can climb.
		/// </para>
		/// </summary>
		/// <param name="Work">The standing work. Null returns null.</param>
		/// <param name="Merged">The design of record after every file has been folded in. Null
		/// reads as "nothing changed".</param>
		public static MergeOffer Reconcile(StandingWork Work, BuildingDraft Merged)
		{
			if (Work == null)
			{
				return null;
			}
			BuildingDraft raised = Work.Raised;
			MergeOffer offer = new MergeOffer
			{
				Key = Work.Key,
				Raised = (raised == null) ? null : raised.Copy(),
				WearingSkinKey = Work.SkinKey
			};
			BuildingDraft reading = Merged ?? raised;
			if (reading != null)
			{
				offer.DisplayName = reading.Get(AttrDisplayName);
				offer.SuccessorKey = reading.Get(AttrUpgradesTo);
				if (reading.Skins != null)
				{
					for (int i = 0; i < reading.Skins.Count; i++)
					{
						if (reading.Skins[i] != null && !string.IsNullOrEmpty(reading.Skins[i].Key))
						{
							offer.SkinKeys.Add(reading.Skins[i].Key);
						}
					}
				}
			}
			if (string.IsNullOrEmpty(offer.DisplayName))
			{
				offer.DisplayName = Work.Key;
			}
			offer.WearingSkinWithdrawn = !string.IsNullOrEmpty(Work.SkinKey) && !offer.SkinKeys.Contains(Work.SkinKey);
			if (Merged != null && raised != null)
			{
				Diverge(raised, Merged, SpentAttributes, offer.Diverged);
				Diverge(raised, Merged, StampedAttributes, offer.Diverged);
			}
			return offer;
		}

		/// <summary>
		/// One line for the founder when a mod update changed a design under something they already
		/// built, or null when nothing that matters changed (STANDARDS 7b: say it once, and only
		/// when there is something to say).
		/// </summary>
		/// <param name="Name">What the founder calls the standing work.</param>
		public static string StandingLine(string Name, MergeOffer Offer)
		{
			if (Offer == null || Offer.Diverged.Count == 0)
			{
				return null;
			}
			string name = string.IsNullOrEmpty(Name) ? "the work" : Name;
			return "The plans for " + name + " have been redrawn, but the one that stands keeps the ground it was cut and the water it was raised with.";
		}

		private static void Diverge(BuildingDraft Raised, BuildingDraft Merged, string[] Attributes, List<string> Into)
		{
			for (int i = 0; i < Attributes.Length; i++)
			{
				string attribute = Attributes[i];
				if (!Same(Raised.Get(attribute), Merged.Get(attribute)) && !Into.Contains(attribute))
				{
					Into.Add(attribute);
				}
			}
		}

		// --- Small shared helpers -----------------------------------------------------------

		/// <summary>Whether two raw attribute values say the same thing. Absent and blank are the
		/// same thing, because every parser downstream reads blank as the default.</summary>
		private static bool Same(string A, string B)
		{
			string a = (A == null) ? "" : A.Trim();
			string b = (B == null) ? "" : B.Trim();
			return a == b;
		}

		private static bool Contains(string[] Set, string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return false;
			}
			for (int i = 0; i < Set.Length; i++)
			{
				if (string.Equals(Set[i], Value, System.StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		private static void Add(List<CatalogueFinding> Findings, CatalogueFinding Finding)
		{
			if (Findings != null && Finding != null)
			{
				Findings.Add(Finding);
			}
		}

		private static string Join(List<string> Names)
		{
			if (Names == null || Names.Count == 0)
			{
				return "nothing";
			}
			if (Names.Count == 1)
			{
				return Names[0];
			}
			string joined = "";
			for (int i = 0; i < Names.Count; i++)
			{
				if (i > 0)
				{
					joined += (i == Names.Count - 1) ? " and " : ", ";
				}
				joined += Names[i];
			}
			return joined;
		}
	}
}
