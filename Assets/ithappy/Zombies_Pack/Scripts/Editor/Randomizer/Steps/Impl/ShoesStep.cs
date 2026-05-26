namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class ShoesStep : SlotStepBase, IRandomizerStep
    {
        public override GroupType GroupType => GroupType.Shoes;

        protected override float Probability => .5f;

        protected override GroupType[] IncompatibleGroups => new[]
        {
            GroupType.Costume,
        };
    }
}