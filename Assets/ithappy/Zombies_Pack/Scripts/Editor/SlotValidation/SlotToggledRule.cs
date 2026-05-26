using System.Linq;
using CharacterCustomizationTool.Editor.Character;

namespace CharacterCustomizationTool.Editor.SlotValidation
{
    public class SlotToggledRule : ISlotValidationRules
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
            if (_slotExceptions.Contains(type) || !isToggled)
            {
                return;
            }

            character.Toggle(SlotType.Costume, false);
        }
    }
}