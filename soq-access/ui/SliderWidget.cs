using System;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    internal sealed class SliderWidget : Widget
    {
        private readonly string _label;
        private readonly Func<string> _getValueText;
        private readonly Func<int> _getValue;
        private readonly Func<int> _getMinimumValue;
        private readonly Func<int> _getMaximumValue;
        private readonly Func<int> _getStep;
        private readonly Func<int, bool> _setValue;
        private readonly Func<bool> _isEnabled;
        private readonly Func<bool> _isVisible;

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
            : base(id)
        {
            _label = label ?? string.Empty;
            _getValueText = getValueText;
            _getValue = getValue;
            _getMinimumValue = getMinimumValue;
            _getMaximumValue = getMaximumValue;
            _getStep = getStep;
            _setValue = setValue;
            _isEnabled = isEnabled;
            _isVisible = isVisible;
        }

        public override bool IsVisible
        {
            get { return _isVisible == null || _isVisible(); }
        }

        public override string GetLabel()
        {
            return _label;
        }

        public override string GetRole()
        {
            return "slider";
        }

        public override string GetStatus()
        {
            return IsEnabled() ? string.Empty : "disabled";
        }

        public override string GetFocusMessage()
        {
            string valueText = GetValueText();
            if (string.IsNullOrWhiteSpace(valueText))
            {
                return _label + " slider";
            }

            return _label + " slider. " + valueText + ".";
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
                changed = SetValue(GetValue() - GetStep());
            }
            else if (action.Key == AccessibilityActions.SliderIncrease.Key)
            {
                changed = SetValue(GetValue() + GetStep());
            }
            else if (action.Key == AccessibilityActions.SliderMinimum.Key)
            {
                changed = SetValue(GetMinimumValue());
            }
            else if (action.Key == AccessibilityActions.SliderMaximum.Key)
            {
                changed = SetValue(GetMaximumValue());
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
    }
}
