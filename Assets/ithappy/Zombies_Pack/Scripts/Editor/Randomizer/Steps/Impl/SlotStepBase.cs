using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public abstract class SlotStepBase : StepBase, IRandomizerStep
    {
        protected abstract GroupType[] IncompatibleGroups { get; }

        public override StepResult Process(int count, GroupType[] groups, CustomizableCharacter character)
        {
            var cannotProcess = !groups.Contains(GroupType);
            groups = RemoveSelf(groups);

            if (cannotProcess || Random.value > Probability)
            {
                return new StepResult(0, false, groups);
            }

            var newGroups = groups.Where(g => !IncompatibleGroups.Contains(g)).ToArray();

            return new StepResult(Random.Range(0, count), true, newGroups);
        }
    }
}