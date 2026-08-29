using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled shell for the five co-opted ceremonies
	/// (<see cref="KingdomCeremonyRules"/> owns the arithmetic and every hand-written line):
	/// the surveyor's plan staked ahead of a building and quoted when it rises, the raising
	/// ceremony that closes construction attended or not, the tastes and traits a settling
	/// notable carries, and the pattern-book a chartered caravan occasionally opens. Every entry
	/// point here is a single call for another system to make; none of them own a clock or a
	/// pass of their own.
	/// </summary>
	public static partial class KingdomCeremony
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionCeremony") != "No";

		/// <summary>String property carrying the surveyor's plan text from a staked marker
		/// through its scaffold to the moment the chronicle quotes it. Never present on a design
		/// raised without ever being staked (a direct commission) &mdash; absence is a normal
		/// state, not a fault.</summary>
		public const string SurveyorsPlanProperty = "KingdomSurveyorsPlan";

		// ==================================================================================
		// The surveyor's plan
		// ==================================================================================

		/// <summary>
		/// Writes the surveyor's plan onto a freshly staked marker: a lookable description
		/// framed as intention, and the same text stashed on a string property so it survives
		/// the marker's own destruction when the plan is realised. Call once, right after
		/// <c>r_KingdomPlanMarker.ApplyDesign</c>.
		/// </summary>
		/// <param name="Marker">The freshly created marker object.</param>
		/// <param name="Entry">The design staked.</param>
		/// <param name="SkinFlavor">The chosen skin's key, or null. Purely decorative &mdash;
		/// absence falls back to "plain stock" inside the template, never to missing text.</param>
		public static void StakePlan(GameObject Marker, KingdomRules.BuildEntry Entry, string SkinFlavor)
		{
			if (!Enabled || Marker == null || Entry == null)
			{
				return;
			}
			KingdomSystem.Guard("ceremony: stake plan", delegate
			{
				string text = KingdomCeremonyRules.SurveyorsPlanText(Entry.Category, Entry.Name, Entry.MinStage, SkinFlavor);
				Marker.SetStringProperty(SurveyorsPlanProperty, text);
				Marker.RequirePart<Description>().Short = text;
			});
		}

		/// <summary>
		/// Carries the staked plan's text from a marker onto the scaffold it becomes, the same
		/// way <c>KingdomDesign.StageSkin</c> carries the chosen skin. Call once, in
		/// <c>KingdomPlanMarker.Realize</c>, before the marker is destroyed.
		/// </summary>
		public static void TransferPlanQuote(GameObject Marker, GameObject Scaffold)
		{
			CarryPlanQuote(ReadPlanQuote(Marker), Scaffold);
		}

		/// <summary>
		/// The staked plan's text, off a marker or off the works that became one. Empty for
		/// anything raised without ever being staked, which is a normal state and not a fault.
		/// <para>
		/// Exists beside <see cref="TransferPlanQuote"/> for the plot path, which measures its rect
		/// out of the marker's own cell and so must take the marker down BEFORE the works that
		/// will carry the quote exists. Read first, carry after.
		/// </para>
		/// </summary>
		public static string ReadPlanQuote(GameObject From)
		{
			if (!Enabled || From == null)
			{
				return null;
			}
			return From.GetStringProperty(SurveyorsPlanProperty);
		}

		/// <summary>Writes a plan's text onto whatever will carry it to the raising. A blank text
		/// writes nothing, so a design nobody staked is left with no property rather than an empty
		/// one.</summary>
		public static void CarryPlanQuote(string Text, GameObject Onto)
		{
			if (!Enabled || Onto == null || string.IsNullOrEmpty(Text))
			{
				return;
			}
			KingdomSystem.Guard("ceremony: carry plan quote", delegate
			{
				Onto.SetStringProperty(SurveyorsPlanProperty, Text);
			});
		}
	}
}
