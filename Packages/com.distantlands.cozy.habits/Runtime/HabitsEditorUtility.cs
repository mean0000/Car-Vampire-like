using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{
    public static class HabitsEditorUtility
    {
        static DistantLands.Cozy.Data.HabitsYearProfile cozyHabitsProfile;

        public static DistantLands.Cozy.Data.HabitsYearProfile habitsProfile
        {
            get
            {
                if (cozyHabitsProfile == null)
                    cozyHabitsProfile = CozyWeather.instance.GetModule<CozyHabits>().profile;


                return cozyHabitsProfile;
            }
            set
            {
                cozyHabitsProfile = value;
            }
        }
        
        static DistantLands.Cozy.CozyHabits cozyHabits;

        public static DistantLands.Cozy.CozyHabits habits
        {
            get
            {
                if (cozyHabits == null)
                    cozyHabits = CozyWeather.instance.GetModule<CozyHabits>();


                return cozyHabits;
            }
            set
            {
                cozyHabits = value;
            }
        }

        public static GUIStyle toolbarButtonIcon = new GUIStyle(GUI.skin.GetStyle("ToolbarButton"))
        {

            padding = new RectOffset(-5, -5, -5, -5),
            fixedWidth = 15,
            fixedHeight = 15

        };
        public static GUIStyle nextPreviousButtonStyle = new GUIStyle(GUI.skin.GetStyle("Button"))
        {

            padding = new RectOffset(-5, -5, -5, -5),
            fixedWidth = 30,
            fixedHeight = 30

        };

        public static string CapitalizeFirstLetter(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                char firstChar = char.ToUpper(str[0]);
                string capitalizedString = firstChar + str.Substring(1);
                return capitalizedString;
            }

            return str;
        }

    }
}