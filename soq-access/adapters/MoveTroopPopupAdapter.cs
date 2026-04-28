using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class MoveTroopPopupAdapter
    {
        private static readonly FieldInfo CurrentStateField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_currentState");
        private static readonly FieldInfo HeaderTextField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_headerText");
        private static readonly FieldInfo AmountTextField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_amountText");
        private static readonly FieldInfo SliderField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_slider");
        private static readonly FieldInfo MoveAllButtonLeftField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_moveAllButtonLeft");
        private static readonly FieldInfo MoveAllButtonRightField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_moveAllButtonRight");
        private static readonly FieldInfo SplitHalfButtonField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_splitHalfButton");
        private static readonly FieldInfo LeftPortraitAmountField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_leftPortraitAmount");
        private static readonly FieldInfo RightPortraitAmountField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_rightPortraitAmount");
        private static readonly FieldInfo DecideTargetTroopEntryField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_decideTargetTroopEntry");
        private static readonly MethodInfo HandleMoveAllLeftMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleMoveAllLeft");
        private static readonly MethodInfo HandleMoveAllRightMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleMoveAllRight");
        private static readonly MethodInfo HandleSplitMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleSplit");
        private static readonly MethodInfo HandleSliderChangedMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleSliderChanged");
        private static readonly MethodInfo HandleSliderPointerUpMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleSliderPointerUp");
        private static readonly MethodInfo AnimateTroopsUIsToDestinationsMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "AnimateTroopsUIsToDestinations");

        private readonly TroopHUDEntryMovable _movable;

        public MoveTroopPopupAdapter(TroopHUDEntryMovable movable)
        {
            _movable = movable;
        }

        public object SourceKey
        {
            get { return _movable; }
        }

        public string Title
        {
            get { return GetText(GetField<IUITextMesh>(HeaderTextField)); }
        }

        public string MaxTroopSize
        {
            get
            {
                string amount = GetText(GetField<IUITextMesh>(AmountTextField));
                return string.IsNullOrWhiteSpace(amount) ? string.Empty : "Max troop size " + amount;
            }
        }

        public bool IsPresent()
        {
            return _movable != null
                && ((Component)_movable).gameObject.activeInHierarchy
                && GetStateName() == "Deciding";
        }

        public ButtonWidget BuildMoveAllLeftButton()
        {
            UIButton button = GetField<UIButton>(MoveAllButtonLeftField);
            return new ButtonWidget(
                "move-troop-move-all-left",
                "Move all left",
                () => Invoke(HandleMoveAllLeftMethod),
                () => NativeSelectionUtility.SelectAndShowTooltip(button),
                () => IsButtonEnabled(button));
        }

        public ButtonWidget BuildSplitButton()
        {
            UIButton button = GetField<UIButton>(SplitHalfButtonField);
            return new ButtonWidget(
                "move-troop-split-equal",
                "Split equally",
                () => Invoke(HandleSplitMethod),
                () => NativeSelectionUtility.SelectAndShowTooltip(button),
                () => IsButtonEnabled(button));
        }

        public ButtonWidget BuildMoveAllRightButton()
        {
            UIButton button = GetField<UIButton>(MoveAllButtonRightField);
            return new ButtonWidget(
                "move-troop-move-all-right",
                "Move all right",
                () => Invoke(HandleMoveAllRightMethod),
                () => NativeSelectionUtility.SelectAndShowTooltip(button),
                () => IsButtonEnabled(button));
        }

        public SliderWidget BuildDistributionSlider()
        {
            return new SliderWidget(
                "move-troop-distribution",
                "troop distribution",
                GetDistributionText,
                GetSliderValue,
                GetSliderMinimum,
                GetSliderMaximum,
                () => 1,
                SetSliderValue,
                IsSliderEnabled);
        }

        public bool Confirm()
        {
            UISlider slider = GetSlider();
            if (slider == null || HandleSliderPointerUpMethod == null)
            {
                return false;
            }

            HandleSliderPointerUpMethod.Invoke(_movable, new object[] { slider });
            return true;
        }

        public bool Cancel()
        {
            if (AnimateTroopsUIsToDestinationsMethod == null)
            {
                return false;
            }

            AnimateTroopsUIsToDestinationsMethod.Invoke(_movable, null);
            DecideTargetTroopEntryField?.SetValue(_movable, null);
            return true;
        }

        public void HideNativeTooltip()
        {
            NativeSelectionUtility.HideTooltip();
        }

        private string GetDistributionText()
        {
            string left = GetText(GetField<IUITextMesh>(LeftPortraitAmountField));
            string right = GetText(GetField<IUITextMesh>(RightPortraitAmountField));
            return "Left: " + (string.IsNullOrWhiteSpace(left) ? "0" : left)
                + ", right: " + (string.IsNullOrWhiteSpace(right) ? "0" : right);
        }

        private int GetSliderValue()
        {
            UISlider slider = GetSlider();
            return slider != null ? Mathf.RoundToInt(slider.SliderValue) : 0;
        }

        private int GetSliderMinimum()
        {
            UISlider slider = GetSlider();
            return slider != null ? Mathf.RoundToInt(slider.SliderMinLimit) : 0;
        }

        private int GetSliderMaximum()
        {
            UISlider slider = GetSlider();
            return slider != null ? Mathf.RoundToInt(slider.SliderMaxLimit) : 0;
        }

        private bool SetSliderValue(int value)
        {
            UISlider slider = GetSlider();
            if (slider == null || HandleSliderChangedMethod == null)
            {
                return false;
            }

            int clamped = Mathf.Clamp(value, GetSliderMinimum(), GetSliderMaximum());
            if (Mathf.RoundToInt(slider.SliderValue) == clamped)
            {
                return false;
            }

            slider.SliderValue = clamped;
            HandleSliderChangedMethod.Invoke(_movable, new object[] { slider });
            return true;
        }

        private bool IsSliderEnabled()
        {
            UISlider slider = GetSlider();
            return slider != null && slider.Interactable;
        }

        private UISlider GetSlider()
        {
            return GetField<UISlider>(SliderField);
        }

        private bool Invoke(MethodInfo method)
        {
            if (method == null)
            {
                return false;
            }

            method.Invoke(_movable, null);
            return true;
        }

        private string GetStateName()
        {
            object value = CurrentStateField != null ? CurrentStateField.GetValue(_movable) : null;
            return value != null ? value.ToString() : string.Empty;
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return _movable != null && field != null ? field.GetValue(_movable) as T : null;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable;
        }
    }
}
