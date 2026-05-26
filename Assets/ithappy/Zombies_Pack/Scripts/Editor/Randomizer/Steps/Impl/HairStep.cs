namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class HairStep : SlotStepBase, IRandomizerStep
    {
        public override GroupType GroupType => GroupType.Hair;

        protected override float Probability => .5f;

        protected override GroupType[] IncompatibleGroups => new[]
        {
            GroupType.Hat,
            GroupType.Costume,
        };
    }
}