using System.Linq;
using CharacterCustomizationTool.Editor.Character;

namespace CharacterCustomizationTool.Editor.SlotValidation
{
    public class OverallToggleRule : ISlotValidationRules
    {
        private readonly SlotType[] _incompatibleSlots =
        {
            SlotType.Outwear,
            SlotType.Apron,
            SlotType.Armor,
            SlotType.Pants,
        };

        public void Validate(CustomizableCharacter character, SlotType type, bool isToggled)
        {
            if (!isToggled)
            {
                return;
            }

            if (type == SlotType.Overall)
            {
                foreach (var incompatibleSlot in _incompatibleSlots)
                {
                    character.Toggle(incompatibleSlot, false);
                }

                return;
            }

            if (_incompatibleSlots.Contains(type))
            {
                character.Toggle(SlotType.Overall, false);
            }
        }
    }
}