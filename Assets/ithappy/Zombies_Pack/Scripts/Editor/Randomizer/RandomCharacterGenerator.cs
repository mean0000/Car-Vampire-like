using System;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Randomizer.Steps;
using CharacterCustomizationTool.Editor.Randomizer.Steps.Impl;

namespace CharacterCustomizationTool.Editor.Randomizer
{
    public class RandomCharacterGenerator
    {
        private readonly IRandomizerStep[] _randomizerSteps =
        {
            new BodyStep(),
            new HeadStep(),
            new EyelidsStep(),
            new EyesStep(),
            new CostumeStep(),
            new HatStep(),
            new HairStep(),
            new OverallStep(),
            new OutwearStep(),
            new PantsStep(),
            new ShoesStep(),
            new GlovesStep(),
        };

        public void Randomize(CustomizableCharacter character)
        {
            character.ToDefault();

            var groups = Enum.GetValues(typeof(GroupType)).Cast<GroupType>().ToArray();

            foreach (var step in _randomizerSteps)
            {
                var variantsCount = character.GetVariantsCountInGroup(step.GroupType);

                var stepResult = step.Process(variantsCount, groups, character);

                groups = stepResult.AvailableGroups;
                character.PickGroup(step.GroupType, stepResult.Index, stepResult.IsActive);
            }
        }
    }
}