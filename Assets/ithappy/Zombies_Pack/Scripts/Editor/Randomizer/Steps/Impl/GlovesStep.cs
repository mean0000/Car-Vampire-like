namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class GlovesStep : SlotStepBase, IRandomizerStep
    {
        public override GroupType GroupType => GroupType.Gloves;

        protected override float Probability => .2f;

        protected override GroupType[] IncompatibleGroups => new[]
        {
            GroupType.Costume,
        };
    }
}