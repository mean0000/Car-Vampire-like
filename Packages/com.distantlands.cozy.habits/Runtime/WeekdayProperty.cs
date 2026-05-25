using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using DistantLands.Cozy.Data;

namespace DistantLands.Cozy
{
    [System.Serializable]
    public class WeekdayAttribute : PropertyAttribute
    {

        public enum TitleStyle { weekdayInitial, weekday, fullDayName, day }
        public TitleStyle titleStyle;
        public bool labelTime;
        public bool highlightCurrentDay;

        public int linesCount;

        public WeekdayAttribute(TitleStyle title, int lines, bool labelTimes, bool highlightDay)
        {
            titleStyle = title;
            linesCount = lines;
            labelTime = labelTimes;
            highlightCurrentDay = highlightDay;

        }
    }

    [System.Serializable]
    public class WeekdayEventAttribute : PropertyAttribute { }

    [System.Serializable]
    public class ModifiedDateAttribute : PropertyAttribute { }

    [System.Serializable]
    public class EnumFlagsAttribute : PropertyAttribute { }


}