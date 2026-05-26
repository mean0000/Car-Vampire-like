using CharacterCustomizationTool.Editor.Character;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class EyelidsStep : StepBase
    {
        public override GroupType GroupType => GroupType.Eyelids;

        public override StepResult Process(int count, GroupType[] groups, CustomizableCharacter character)
        {
            return new StepResult(Random.Range(0, count), true, RemoveSelf(groups));
        }
    }
}