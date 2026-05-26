using System.Linq;
using CharacterCustomizationTool.Editor.Character;

namespace CharacterCustomizationTool.Editor.SlotValidation
{
    public class FullBodyToggledRule : ISlotValidationRules
    {
        private readonly SlotType[] _slotExceptions =
        {
            SlotType.Costume,
            SlotType.Body,
            SlotType.Head,
            SlotType.Eyes,
            SlotType.Eyelids,
        };

        public void Validate(CustomizableCharacter character, SlotType type, bool isToggled)
        {
            if (type != SlotType.Costume || !isToggled)
            {
                return;
            }

            foreach (var slot in character.Slots.Where(s => !_slotExceptions.Contains(s.Type)))
            {
                slot.Toggle(false);
            }
        }
    }
}