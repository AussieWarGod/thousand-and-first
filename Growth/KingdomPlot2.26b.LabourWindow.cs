using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		/// <summary>
		/// The one stage refusal a founder can act on: a living body standing on a layout slot of
		/// a raising whose labour is already paid. Said once per job and slot (house law: an
		/// applicable-but-blocked path announces once), and forgotten the moment the ground layer
		/// stops refusing for an occupant, so a later block is said again.
		/// </summary>
		/// <param name="Slot">The refused slot key, or null when the block is not in force.</param>
		/// <param name="Line">The sentence to say for that key.</param>
		private static void SayPlotWorkOccupied(KingdomSystem System, GameObject Works,
			string Slot, string Line)
		{
			if (Works == null) return;
			if (Slot == null)
			{
				Works.SetIntProperty(PlotWorkOccupantAnnouncedProperty, 0);
				Works.SetStringProperty(PlotWorkOccupantSlotProperty, null, RemoveIfNull: true);
				return;
			}
			if (!KingdomPlotRules.ShouldAnnounceOccupiedSlot(
				Works.GetIntProperty(PlotWorkOccupantAnnouncedProperty),
				Works.GetStringProperty(PlotWorkOccupantSlotProperty), Slot)) return;
			Works.SetIntProperty(PlotWorkOccupantAnnouncedProperty, 1);
			Works.SetStringProperty(PlotWorkOccupantSlotProperty, Slot);
			if (System != null && System.Founded && Line != null)
			{
				System.Ledger.Note("{{r|" + Line + "}}");
			}
		}

		/// <summary>
		/// The crew stood its own people off a paid raising's ground. Said once per job: the
		/// founder is told their settlers were moved, because nothing moves without being named.
		/// </summary>
		private static void SayPlotWorkCleared(KingdomSystem System, GameObject Works,
			string Name, int Moved, bool Raised, string Fault)
		{
			if (Works == null || Moved <= 0
				|| Works.GetIntProperty(PlotWorkClearedAnnouncedProperty) == 1) return;
			Works.SetIntProperty(PlotWorkClearedAnnouncedProperty, 1);
			if (System != null && System.Founded)
			{
				System.Ledger.Note("{{W|" + KingdomPlotRules.ClearedOccupiedSlots(
					Name ?? "work", Moved, Raised, Fault) + "}}");
			}
		}

		/// <summary>Freezes only facts witnessed now; they may price the following interval.</summary>
		private static bool TryCapturePlotLabourWindow(GameObject Root, KingdomSystem System,
			long TimeTick, int InfrastructurePercent, string InfrastructureFailure,
			string DisplayName)
		{
			int effectiveness = KingdomConstructionPresence.EffectivenessOf(Root, System,
				out int freeHands, out bool selected);
			if (Root.GetIntProperty(KingdomConstructionPresence.SchemaProperty)
				!= KingdomConstructionPresenceRules.Schema)
			{
				effectiveness = 0;
				freeHands = 0;
				selected = false;
			}
			int infrastructure = InfrastructurePercent
				== KingdomPlotLabourWindowRules.InfrastructureReady
				? KingdomPlotLabourWindowRules.InfrastructureReady
				: KingdomPlotLabourWindowRules.InfrastructureUnavailable;
			KingdomPlotLabourWindow current = new KingdomPlotLabourWindow
			{
				Tick = TimeTick,
				LabourPercent = effectiveness,
				InfrastructurePercent = infrastructure,
				Hands = freeHands,
				Selected = selected
			};
			if (!KingdomPlotLabourWindowRules.TryEncode(current, out string encoded))
			{
				current.LabourPercent = 0;
				freeHands = current.Hands = 0;
				selected = current.Selected = false;
				if (!KingdomPlotLabourWindowRules.TryEncode(current, out encoded)) return false;
				SayPlotWorkFault(System, Root,
					"The plot's loaded construction-crew witness was out of bounds; no crew was inferred.");
			}
			Root.SetStringProperty(PlotWorkWindowProperty, encoded);
			if (Root.GetStringProperty(PlotWorkWindowProperty) != encoded)
			{
				SayPlotWorkFault(System, Root,
					"The plot could not retain its loaded construction witness; no later work was inferred.");
				return false;
			}
			SayPlotInfrastructure(System, Root,
				infrastructure == KingdomPlotLabourWindowRules.InfrastructureReady
					? null : InfrastructureFailure);
			if (selected) SayPlotWorkShortfall(System, Root, DisplayName, freeHands);
			return true;
		}

		/// <summary>Unauthorized facts can seed only a canonical zero following interval.</summary>
		private static bool TryCaptureZeroPlotLabourWindow(GameObject Root, KingdomSystem System,
			long TimeTick, string Failure)
		{
			KingdomPlotLabourWindow zero = new KingdomPlotLabourWindow
			{
				Tick = TimeTick,
				LabourPercent = 0,
				InfrastructurePercent = KingdomPlotLabourWindowRules.InfrastructureUnavailable,
				Hands = 0,
				Selected = false
			};
			if (!KingdomPlotLabourWindowRules.TryEncode(zero, out string encoded)) return false;
			Root.SetStringProperty(PlotWorkWindowProperty, encoded);
			if (Root.GetStringProperty(PlotWorkWindowProperty) != encoded)
			{
				Root.RemoveStringProperty(PlotWorkWindowProperty);
				SayPlotWorkFault(System, Root,
					"The plot could not retain its zero labour witness; no later work was inferred.");
				return false;
			}
			SayPlotInfrastructure(System, Root, Failure);
			return true;
		}

		private static void SayPlotInfrastructure(KingdomSystem System, GameObject Works,
			string Failure)
		{
			if (Works == null) return;
			if (string.IsNullOrEmpty(Failure))
			{
				Works.SetIntProperty(PlotWorkShortfallSaidProperty,
					Works.GetIntProperty(PlotWorkShortfallSaidProperty)
						& ~PlotWorkInfrastructureSaid);
				return;
			}
			int said = Works.GetIntProperty(PlotWorkShortfallSaidProperty);
			if ((said & PlotWorkInfrastructureSaid) != 0) return;
			Works.SetIntProperty(PlotWorkShortfallSaidProperty,
				said | PlotWorkInfrastructureSaid);
			KingdomLog.Log("plot infrastructure: " + Failure);
			if (System != null && System.Founded) System.Ledger.Note("{{r|" + Failure + "}}");
		}
	}
}
