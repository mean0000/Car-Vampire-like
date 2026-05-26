using CharacterCustomizationTool.Editor.Character;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class HeadStep : StepBase
    {
        public override GroupType GroupType => GroupType.Head;

        public override StepResult Process(int count, GroupType[] groups, CustomizableCharacter character)
        {
            var startIndex = (int)(Mathf.Floor(character.GetSlotBy(SlotType.Body).SelectedIndex / 3f) * 3);
            var endIndex = startIndex + 3;

            return new StepResult(Random.Range(startIndex, endIndex), true, RemoveSelf(groups));
        }
    }
}