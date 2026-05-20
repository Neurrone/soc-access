using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class MoveTroopPopupAdapter
    {
        private static readonly FieldInfo CurrentStateField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_currentState");
        private static readonly FieldInfo HeaderTextField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_headerText");
        private static readonly FieldInfo AmountTextField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_amountText");
        private static readonly FieldInfo SliderField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_slider");
        private static readonly FieldInfo NonPortraitContainerField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_nonPortraitContainer");
        private static readonly FieldInfo PortraitContainerField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_portraitContainer");
        private static readonly FieldInfo DraggableContainerField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_draggableContainer");
        private static readonly FieldInfo LeftContainerField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_leftContainer");
        private static readonly FieldInfo RightContainerField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_rightContainer");
        private static readonly FieldInfo MoveAllButtonLeftField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_moveAllButtonLeft");
        private static readonly FieldInfo MoveAllButtonRightField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_moveAllButtonRight");
        private static readonly FieldInfo SplitHalfButtonField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_splitHalfButton");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_localization");
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
        private readonly ILocalizationHandler _localization;

        public MoveTroopPopupAdapter(TroopHUDEntryMovable movable)
        {
            _movable = movable;
            _localization = GetField<ILocalizationHandler>(LocalizationField);
        }

        public object SourceKey
        {
            get { return _movable; }
        }

        public string Title
        {
            get { return GetText(GetField<IUITextMesh>(HeaderTextField)); }
        }

        public string MoveAllLeftLabel
        {
            get { return ModText.Get(ModStrings.Screens.MoveAllLeft); }
        }

        public string MoveAllRightLabel
        {
            get { return ModText.Get(ModStrings.Screens.MoveAllRight); }
        }

        public string SplitEqualLabel
        {
            get { return GetFirstTooltipLine(SplitEqualTooltip); }
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
            if (!IsMovableActive())
            {
                return false;
            }

            if (GetStateName() == "Deciding")
            {
                return true;
            }

            return IsDialogContainerActive()
                && GetSlider() != null
                && !IsGameObjectActive(GetField<Component>(DraggableContainerField));
        }

        public bool MoveAllLeft()
        {
            return Invoke(HandleMoveAllLeftMethod);
        }

        public bool IsMoveAllLeftEnabled()
        {
            UIButton button = GetField<UIButton>(MoveAllButtonLeftField);
            return IsButtonEnabled(button);
        }

        public Tooltip MoveAllLeftTooltip
        {
            get { return Tooltip.ForComponent(GetField<UIButton>(MoveAllButtonLeftField), _localization); }
        }

        public bool SplitEqual()
        {
            return Invoke(HandleSplitMethod);
        }

        public bool IsSplitEqualEnabled()
        {
            return IsButtonEnabled(GetField<UIButton>(SplitHalfButtonField));
        }

        public Tooltip SplitEqualTooltip
        {
            get { return Tooltip.ForComponent(GetField<UIButton>(SplitHalfButtonField), _localization); }
        }

        public bool MoveAllRight()
        {
            return Invoke(HandleMoveAllRightMethod);
        }

        public bool IsMoveAllRightEnabled()
        {
            return IsButtonEnabled(GetField<UIButton>(MoveAllButtonRightField));
        }

        public Tooltip MoveAllRightTooltip
        {
            get { return Tooltip.ForComponent(GetField<UIButton>(MoveAllButtonRightField), _localization); }
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

        public bool CanConfirm()
        {
            return IsMovableActive()
                && GetSlider() != null
                && HandleSliderPointerUpMethod != null;
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

        public bool CanCancel()
        {
            return IsMovableActive()
                && AnimateTroopsUIsToDestinationsMethod != null;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public string GetDistributionText()
        {
            string left = GetText(GetField<IUITextMesh>(LeftPortraitAmountField));
            string right = GetText(GetField<IUITextMesh>(RightPortraitAmountField));
            return "Left: " + (string.IsNullOrWhiteSpace(left) ? "0" : left)
                + ", right: " + (string.IsNullOrWhiteSpace(right) ? "0" : right);
        }

        public int GetSliderValue()
        {
            UISlider slider = GetSlider();
            return slider != null ? Mathf.RoundToInt(slider.SliderValue) : 0;
        }

        public int GetSliderMinimum()
        {
            UISlider slider = GetSlider();
            return slider != null ? Mathf.RoundToInt(slider.SliderMinLimit) : 0;
        }

        public int GetSliderMaximum()
        {
            UISlider slider = GetSlider();
            return slider != null ? Mathf.RoundToInt(slider.SliderMaxLimit) : 0;
        }

        public int GetSliderStep()
        {
            return 1;
        }

        public bool SetSliderValue(int value)
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

        public bool IsSliderEnabled()
        {
            UISlider slider = GetSlider();
            return slider != null && slider.Interactable;
        }

        private UISlider GetSlider()
        {
            return GetField<UISlider>(SliderField);
        }

        private bool IsMovableActive()
        {
            return _movable != null
                && ((Component)_movable).gameObject != null
                && ((Component)_movable).gameObject.activeInHierarchy;
        }

        private bool IsDialogContainerActive()
        {
            return IsGameObjectActive(GetField<GameObject>(NonPortraitContainerField))
                || IsGameObjectActive(GetField<GameObject>(PortraitContainerField))
                || IsGameObjectActive(GetField<Component>(LeftContainerField))
                || IsGameObjectActive(GetField<Component>(RightContainerField));
        }

        private static bool IsGameObjectActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsGameObjectActive(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
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

        private static string GetFirstTooltipLine(Tooltip tooltip)
        {
            IReadOnlyList<string> lines = tooltip != null ? tooltip.TextLines : null;
            if (lines == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string line = SpeechTextSanitizer.Normalize(lines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return string.Empty;
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable;
        }
    }
}
