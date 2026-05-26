namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class OutwearStep : SlotStepBase, IRandomizerStep
    {
        public override GroupType GroupType => GroupType.Outwear;

        protected override float Probability => 1f;

        protected override GroupType[] IncompatibleGroups => new[]
        {
            GroupType.Costume,
            GroupType.Overall,
        };
    }
}