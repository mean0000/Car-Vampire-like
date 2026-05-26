namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class HatStep : SlotStepBase, IRandomizerStep
    {
        public override GroupType GroupType => GroupType.Hat;

        protected override float Probability => .33f;

        protected override GroupType[] IncompatibleGroups => new[]
        {
            GroupType.Hair,
            GroupType.Costume,
        };
    }
}