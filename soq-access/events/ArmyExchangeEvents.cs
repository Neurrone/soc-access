namespace SongsOfConquestAccess.Events
{
    internal sealed class ArmyExchangeInvalidDestinationEvent : IAccessibilityEvent
    {
        public ArmyExchangeInvalidDestinationEvent(string sourceSlotId, string targetSlotId)
        {
            SourceSlotId = sourceSlotId ?? string.Empty;
            TargetSlotId = targetSlotId ?? string.Empty;
        }

        public string Kind
        {
            get { return AccessibilityEvents.ArmyExchange.InvalidDestination; }
        }

        public bool Interrupt
        {
            get { return true; }
        }

        public string SourceSlotId { get; private set; }

        public string TargetSlotId { get; private set; }

        public string GetSpeechText()
        {
            return "Cannot drop there.";
        }
    }
}
