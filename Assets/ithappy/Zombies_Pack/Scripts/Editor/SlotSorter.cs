using System.Collections.Generic;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;

namespace CharacterCustomizationTool.Editor
{
    public static class SlotSorter
    {
        private static readonly List<SlotType> SlotTypesInOrder = new()
        {
            SlotType.Head,
            SlotType.Body,
            SlotType.Costume,
            SlotType.Eyelids,
            SlotType.Eyes,
            SlotType.Hat,
            SlotType.Hair,
            SlotType.Mask,
            SlotType.Outwear,
            SlotType.Overall,
            SlotType.Armor,
            SlotType.Apron,
            SlotType.Camera,
            SlotType.Gloves,
            SlotType.Pants,
            SlotType.Shorts,
            SlotType.Shoes,
            SlotType.Socks,
        };

        public static IEnumerable<SlotBase> Sort(IEnumerable<SlotBase> slots)
        {
            var sortedSlots = SlotTypesInOrder
                .Select(type => slots.FirstOrDefault(p => p.IsOfType(type)))
                .Where(part => part != null)
                .ToList();

            return sortedSlots;
        }
    }
}