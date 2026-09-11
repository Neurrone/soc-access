using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class MoveTroopPopupAdapter : IPresent
    {
        private static readonly FieldInfo CurrentStateField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_currentState");
        private static readonly FieldInfo HeaderTextField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_headerText");
        private static readonly FieldInfo AmountTextField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_amountText");
        private static readonly FieldInfo SliderField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_slider");
        private static readonly FieldInfo NonPortraitContainerField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_nonPortraitContainer");
        private static readonly FieldInfo MoveAllButtonLeftField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_moveAllButtonLeft");
        private static readonly FieldInfo MoveAllButtonRightField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_moveAllButtonRight");
        private static readonly FieldInfo SplitHalfButtonField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_splitHalfButton");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_localization");
        private static readonly FieldInfo LeftPortraitAmountField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_leftPortraitAmount");
        private static readonly FieldInfo RightPortraitAmountField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_rightPortraitAmount");
        private static readonly MethodInfo HandleMoveAllLeftMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleMoveAllLeft");
        private static readonly MethodInfo HandleMoveAllRightMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleMoveAllRight");
        private static readonly MethodInfo HandleSplitMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleSplit");
        private static readonly MethodInfo HandleSliderChangedMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleSliderChanged");
        private static readonly MethodInfo HandleSliderPointerUpMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "HandleSliderPointerUp");

        private readonly TroopHUDEntryMovable _movable;
        private readonly ILocalizationHandler _localization;

        public MoveTroopPopupAdapter(TroopHUDEntryMovable movable)
        {
            _movable = movable;
            _localization = Reflect.Get<ILocalizationHandler>(_movable, LocalizationField);
        }

        public object SourceKey
        {
            get { return _movable; }
        }

        public string Title
        {
            get { return GetText(Reflect.Get<IUITextMesh>(_movable, HeaderTextField)); }
        }

        /// <summary>The split button's own name, read from the game's localization rather than off the
        /// button: the tooltip the game writes there has the key that presses it appended
        /// ("Split Equal(Space)"), which is a key name rather than the button's name.</summary>
        public string SplitEqualLabel
        {
            get { return GameText.Get(_localization, "Common/MoveTroops/SplitHalf", string.Empty); }
        }

        /// <summary>The line the popup draws under its header: the game's own caption and the number
        /// beside it, which the game draws as two texts in one container.</summary>
        public IReadOnlyList<string> MaxTroopSizeTexts
        {
            get
            {
                List<string> texts = new List<string>(2);
                IUITextMesh amount = Reflect.Get<IUITextMesh>(_movable, AmountTextField);
                Component component = amount as Component;
                Transform container = component != null ? component.transform.parent : null;
                if (container == null)
                {
                    Add(texts, GetText(amount));
                    return texts;
                }

                if (_maxSizeTexts == null || !ReferenceEquals(_maxSizeContainer, container))
                {
                    _maxSizeContainer = container;
                    _maxSizeTexts = container.GetComponentsInChildren<UITextMesh>(true);
                }

                for (int i = 0; i < _maxSizeTexts.Length; i++)
                {
                    Add(texts, GetText(_maxSizeTexts[i]));
                }

                return texts;
            }
        }

        // The two texts of the caption line, and the image the hotkey list hangs on: which components
        // they are is fixed while the popup is up, and finding the image read a full tooltip capture
        // per child of the panel on every build. What they say is still read live.
        private Transform _maxSizeContainer;
        private UITextMesh[] _maxSizeTexts;
        private UIImage _hotkeysImage;
        private bool _hotkeysProbed;

        /// <summary>The icon the popup draws in its corner, whose tooltip is the game's own list of the
        /// hotkeys this popup answers to. It is the one image in the panel with a tooltip of its own;
        /// the buttons beside it are buttons.</summary>
        public Tooltip HotkeysTooltip
        {
            get
            {
                UIImage image = GetHotkeysImage();
                return image == null ? null : Tooltip.ForComponent(image, _localization);
            }
        }

        private UIImage GetHotkeysImage()
        {
            if (_hotkeysProbed)
            {
                return _hotkeysImage;
            }

            GameObject container = Reflect.Get<GameObject>(_movable, NonPortraitContainerField);
            Transform root = container != null ? container.transform : null;
            if (root == null)
            {
                return null;
            }

            _hotkeysProbed = true;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                UIImage image = child.GetComponent<UIImage>();
                if (image == null || child.GetComponent<UIButton>() != null)
                {
                    continue;
                }

                Tooltip tooltip = Tooltip.ForComponent(image, _localization);
                if (tooltip != null && tooltip.TextLines != null && tooltip.TextLines.Count > 0)
                {
                    _hotkeysImage = image;
                    return image;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the game is asking how many troops to move. Only the DECIDING state counts: the
        /// drag ghost is one object that is also the drag itself, and an idle one left active - after a
        /// swap, or between a hot reload and the next frame - draws no popup and must not read as one.
        /// </summary>
        public bool IsPresent()
        {
            return IsMovableActive()
                && GetStateName() == "Deciding"
                && GetSlider() != null;
        }

        /// <summary>The number the popup draws for the maximum troop size - what the caption above is
        /// about, and the component the line's node is drawn by.</summary>
        public Component MaxTroopSizeText
        {
            get { return Reflect.Get<IUITextMesh>(_movable, AmountTextField) as Component; }
        }

        public Component SliderComponent
        {
            get { return GetSlider(); }
        }

        public Component MoveAllLeftButton
        {
            get { return Reflect.Get<UIButton>(_movable, MoveAllButtonLeftField); }
        }

        public Component SplitEqualButton
        {
            get { return Reflect.Get<UIButton>(_movable, SplitHalfButtonField); }
        }

        public Component MoveAllRightButton
        {
            get { return Reflect.Get<UIButton>(_movable, MoveAllButtonRightField); }
        }

        public bool MoveAllLeft()
        {
            return Invoke(HandleMoveAllLeftMethod);
        }

        public bool IsMoveAllLeftEnabled()
        {
            UIButton button = Reflect.Get<UIButton>(_movable, MoveAllButtonLeftField);
            return IsButtonEnabled(button);
        }

        public Tooltip MoveAllLeftTooltip
        {
            get { return Tooltip.ForComponent(Reflect.Get<UIButton>(_movable, MoveAllButtonLeftField), _localization); }
        }

        public bool SplitEqual()
        {
            return Invoke(HandleSplitMethod);
        }

        public bool IsSplitEqualEnabled()
        {
            return IsButtonEnabled(Reflect.Get<UIButton>(_movable, SplitHalfButtonField));
        }

        public Tooltip SplitEqualTooltip
        {
            get { return Tooltip.ForComponent(Reflect.Get<UIButton>(_movable, SplitHalfButtonField), _localization); }
        }

        public bool MoveAllRight()
        {
            return Invoke(HandleMoveAllRightMethod);
        }

        public bool IsMoveAllRightEnabled()
        {
            return IsButtonEnabled(Reflect.Get<UIButton>(_movable, MoveAllButtonRightField));
        }

        public Tooltip MoveAllRightTooltip
        {
            get { return Tooltip.ForComponent(Reflect.Get<UIButton>(_movable, MoveAllButtonRightField), _localization); }
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

        public string LeftAmount
        {
            get { return GetText(Reflect.Get<IUITextMesh>(_movable, LeftPortraitAmountField)); }
        }

        public string RightAmount
        {
            get { return GetText(Reflect.Get<IUITextMesh>(_movable, RightPortraitAmountField)); }
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
            return Reflect.Get<UISlider>(_movable, SliderField);
        }

        private bool IsMovableActive()
        {
            return _movable != null
                && ((Component)_movable).gameObject != null
                && ((Component)_movable).gameObject.activeInHierarchy;
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

        private static string GetText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static void Add(List<string> texts, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                texts.Add(text);
            }
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable;
        }
    }
}
