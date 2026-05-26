namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class PantsStep : SlotStepBase, IRandomizerStep
    {
        public override GroupType GroupType => GroupType.Pants;

        protected override float Probability => 1f;

        protected override GroupType[] IncompatibleGroups => new[]
        {
            GroupType.Costume,
            GroupType.Overall,
        };
    }
}