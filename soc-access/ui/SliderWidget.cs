using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    public sealed class SliderWidget : Widget
    {
        private readonly Func<string> _getLabel;
        private readonly Func<string> _getValueText;
        private readonly Func<int> _getValue;
        private readonly Func<int> _getMinimumValue;
        private readonly Func<int> _getMaximumValue;
        private readonly Func<int> _getStep;
        private readonly Func<int, bool> _setValue;
        private readonly Func<float> _getFloatValue;
        private readonly Func<float> _getMinimumFloatValue;
        private readonly Func<float> _getMaximumFloatValue;
        private readonly Func<float> _getFloatStep;
        private readonly Func<float, bool> _setFloatValue;
        private readonly Func<bool> _isEnabled;
        private readonly Func<bool> _isVisible;
        private readonly Func<Tooltip> _getTooltip;
        private readonly bool _usesFloatValues;

        public SliderWidget(
            string id,
            string label,
            Func<string> getValueText,
            Func<int> getValue,
            Func<int> getMinimumValue,
            Func<int> getMaximumValue,
            Func<int> getStep,
            Func<int, bool> setValue,
            Func<bool> isEnabled)
            : this(id, label, getValueText, getValue, getMinimumValue, getMaximumValue, getStep, setValue, isEnabled, null)
        {
        }

        public SliderWidget(
            string id,
            string label,
            Func<string> getValueText,
            Func<int> getValue,
            Func<int> getMinimumValue,
            Func<int> getMaximumValue,
            Func<int> getStep,
            Func<int, bool> setValue,
            Func<bool> isEnabled,
            Func<bool> isVisible = null)
            : this(id, () => label, getValueText, getValue, getMinimumValue, getMaximumValue, getStep, setValue, isEnabled, isVisible, null)
        {
        }

        public SliderWidget(
            string id,
            Func<string> getLabel,
            Func<string> getValueText,
            Func<int> getValue,
            Func<int> getMinimumValue,
            Func<int> getMaximumValue,
            Func<int> getStep,
            Func<int, bool> setValue,
            Func<bool> isEnabled,
            Func<bool> isVisible = null,
            Func<Tooltip> getTooltip = null)
            : base(id)
        {
            _getLabel = getLabel;
            _getValueText = getValueText;
            _getValue = getValue;
            _getMinimumValue = getMinimumValue;
            _getMaximumValue = getMaximumValue;
            _getStep = getStep;
            _setValue = setValue;
            _isEnabled = isEnabled;
            _isVisible = isVisible;
            _getTooltip = getTooltip;
        }

        public SliderWidget(
            string id,
            string label,
            Func<string> getValueText,
            Func<float> getValue,
            Func<float> getMinimumValue,
            Func<float> getMaximumValue,
            Func<float> getStep,
            Func<float, bool> setValue,
            Func<bool> isEnabled,
            Func<bool> isVisible = null)
            : this(id, () => label, getValueText, getValue, getMinimumValue, getMaximumValue, getStep, setValue, isEnabled, isVisible, null)
        {
        }

        public SliderWidget(
            string id,
            Func<string> getLabel,
            Func<string> getValueText,
            Func<float> getValue,
            Func<float> getMinimumValue,
            Func<float> getMaximumValue,
            Func<float> getStep,
            Func<float, bool> setValue,
            Func<bool> isEnabled,
            Func<bool> isVisible = null,
            Func<Tooltip> getTooltip = null)
            : base(id)
        {
            _getLabel = getLabel;
            _getValueText = getValueText;
            _getFloatValue = getValue;
            _getMinimumFloatValue = getMinimumValue;
            _getMaximumFloatValue = getMaximumValue;
            _getFloatStep = getStep;
            _setFloatValue = setValue;
            _isEnabled = isEnabled;
            _isVisible = isVisible;
            _getTooltip = getTooltip;
            _usesFloatValues = true;
        }

        public override bool IsVisible
        {
            get { return _isVisible == null || _isVisible(); }
        }

        public override string GetLabel()
        {
            return GetLabelText();
        }

        public override string GetRole()
        {
            return ModText.Get(ModStrings.UI.RoleSlider);
        }

        public override string GetStatus()
        {
            string valueText = GetValueText();
            string disabledText = IsEnabled() ? string.Empty : ModText.Get(ModStrings.UI.StatusDisabled);
            if (string.IsNullOrWhiteSpace(valueText))
            {
                return disabledText;
            }

            if (string.IsNullOrWhiteSpace(disabledText))
            {
                return valueText;
            }

            return valueText + " " + disabledText;
        }

        public override Tooltip GetTooltip()
        {
            return _getTooltip != null ? _getTooltip() : null;
        }

        public override bool ClaimsAction(string actionKey)
        {
            return IsEnabled()
                && (actionKey == AccessibilityActions.SliderDecrease.Key
                    || actionKey == AccessibilityActions.SliderIncrease.Key
                    || actionKey == AccessibilityActions.SliderMinimum.Key
                    || actionKey == AccessibilityActions.SliderMaximum.Key);
        }

        public override bool HandleAction(InputAction action)
        {
            if (!IsEnabled() || action == null)
            {
                return false;
            }

            bool changed = false;
            if (action.Key == AccessibilityActions.SliderDecrease.Key)
            {
                changed = _usesFloatValues
                    ? SetFloatValue(GetFloatValue() - GetFloatStep())
                    : SetValue(GetValue() - GetStep());
            }
            else if (action.Key == AccessibilityActions.SliderIncrease.Key)
            {
                changed = _usesFloatValues
                    ? SetFloatValue(GetFloatValue() + GetFloatStep())
                    : SetValue(GetValue() + GetStep());
            }
            else if (action.Key == AccessibilityActions.SliderMinimum.Key)
            {
                changed = _usesFloatValues
                    ? SetFloatValue(GetMinimumFloatValue())
                    : SetValue(GetMinimumValue());
            }
            else if (action.Key == AccessibilityActions.SliderMaximum.Key)
            {
                changed = _usesFloatValues
                    ? SetFloatValue(GetMaximumFloatValue())
                    : SetValue(GetMaximumValue());
            }

            if (changed)
            {
                SpeakValue();
            }

            return changed;
        }

        private bool IsEnabled()
        {
            return _isEnabled == null || _isEnabled();
        }

        private string GetValueText()
        {
            return _getValueText != null ? (_getValueText() ?? string.Empty) : string.Empty;
        }

        private string GetLabelText()
        {
            return _getLabel != null ? (_getLabel() ?? string.Empty) : string.Empty;
        }

        private int GetValue()
        {
            return _getValue != null ? _getValue() : GetMinimumValue();
        }

        private int GetMinimumValue()
        {
            return _getMinimumValue != null ? _getMinimumValue() : 0;
        }

        private int GetMaximumValue()
        {
            int minimumValue = GetMinimumValue();
            int maximumValue = _getMaximumValue != null ? _getMaximumValue() : minimumValue;
            return maximumValue < minimumValue ? minimumValue : maximumValue;
        }

        private int GetStep()
        {
            int step = _getStep != null ? _getStep() : 1;
            return step <= 0 ? 1 : step;
        }

        private bool SetValue(int value)
        {
            if (_setValue == null)
            {
                return false;
            }

            int clampedValue = Clamp(value, GetMinimumValue(), GetMaximumValue());
            if (clampedValue == GetValue())
            {
                return false;
            }

            return _setValue(clampedValue);
        }

        private float GetFloatValue()
        {
            return _getFloatValue != null ? _getFloatValue() : GetMinimumFloatValue();
        }

        private float GetMinimumFloatValue()
        {
            return _getMinimumFloatValue != null ? _getMinimumFloatValue() : 0f;
        }

        private float GetMaximumFloatValue()
        {
            float minimumValue = GetMinimumFloatValue();
            float maximumValue = _getMaximumFloatValue != null ? _getMaximumFloatValue() : minimumValue;
            return maximumValue < minimumValue ? minimumValue : maximumValue;
        }

        private float GetFloatStep()
        {
            float step = _getFloatStep != null ? _getFloatStep() : 0f;
            if (step > 0f)
            {
                return step;
            }

            float range = GetMaximumFloatValue() - GetMinimumFloatValue();
            return range > 0f ? range / 100f : 1f;
        }

        private bool SetFloatValue(float value)
        {
            if (_setFloatValue == null)
            {
                return false;
            }

            float clampedValue = Clamp(value, GetMinimumFloatValue(), GetMaximumFloatValue());
            if (Math.Abs(clampedValue - GetFloatValue()) < 0.0001f)
            {
                return false;
            }

            return _setFloatValue(clampedValue);
        }

        private void SpeakValue()
        {
            string valueText = GetValueText();
            if (!string.IsNullOrWhiteSpace(valueText))
            {
                SpeechPipeline.Output(new SpeechRequest(valueText, interrupt: false));
            }
        }

        private static int Clamp(int value, int minimumValue, int maximumValue)
        {
            if (value < minimumValue)
            {
                return minimumValue;
            }

            return value > maximumValue ? maximumValue : value;
        }

        private static float Clamp(float value, float minimumValue, float maximumValue)
        {
            if (value < minimumValue)
            {
                return minimumValue;
            }

            return value > maximumValue ? maximumValue : value;
        }
    }
}
