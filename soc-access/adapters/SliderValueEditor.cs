using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The little box a slider draws over its value, and the game's own way of opening it.
    ///
    /// Every page the menu factory builds draws the same <c>UISlider</c>, so this is written once and
    /// every adapter's slider answers with it.
    /// </summary>
    public static class SliderValueEditor
    {
        private static readonly FieldInfo EditButtonField = AccessTools.Field(typeof(UISlider), "_editValueButton");

        /// <summary>What the game calls the popup its own value box opens ("Provide a number",
        /// <c>UISlider.HandleTextClicked</c>), or empty where this slider draws no such box.</summary>
        public static string Label(IUISlider slider)
        {
            return EditButtonOf(slider) == null
                ? string.Empty
                : GameText.Get("Common/ProvideNumber", string.Empty);
        }

        /// <summary>
        /// Open the game's own "Provide a number" popup for this slider.
        ///
        /// The native path is the delegate the slider itself installed: <c>UISlider.OnEnable</c> adds
        /// <c>HandleTextClicked</c> to the value box's <c>OnClickedInside</c>, which
        /// <c>UITransform.Update</c> raises from a real mouse press landing inside the box - NOT from
        /// <c>OnPointerClick</c>, so a synthesized pointer click reaches the button's empty
        /// <c>OnClicked</c> and nothing happens. Running the installed delegate is that same handler
        /// minus the mouse; the handler's own guard on the slider being interactable, and the popup it
        /// raises, are the game's.
        /// </summary>
        public static bool Open(IUISlider slider)
        {
            UIButton button = EditButtonOf(slider);
            Action<Vector2> clicked = button != null ? button.OnClickedInside : null;
            if (clicked == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            clicked(Vector2.zero);
            return true;
        }

        private static UIButton EditButtonOf(IUISlider slider)
        {
            UISlider concrete = slider as UISlider;
            return concrete != null && EditButtonField != null
                ? EditButtonField.GetValue(concrete) as UIButton
                : null;
        }
    }
}
