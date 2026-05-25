using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DistantLands.Cozy.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    public class CozyHabits : CozyDateOverride
    {

        [SerializeField]
        private bool selection;
        [SerializeField]
        private bool monthView;
        [SerializeField]
        private bool weekView;
        [SerializeField]
        private bool dayView;

        public HabitsYearProfile profile;

        [System.Serializable]
        public class Day
        {


            public Weekday weekday;
            [ModifiedDate]
            public ModifiedDate date;
            public CozyHabitProfile[] events;

        }

        [Weekday(WeekdayAttribute.TitleStyle.fullDayName, 30, true, false)]
        public Day currentDay = new Day() { date = new ModifiedDate(0, 0, 0) };
        [Weekday(WeekdayAttribute.TitleStyle.weekdayInitial, 24, false, true)]
        public Day[] currentWeek;
        public Day[] currentMonth;

        [System.Serializable]
        public struct ModifiedDate
        {
            public int day;
            public int month;
            public int year;
            public MeridiemTime time;

            public string GetName()
            {
                return $"{day + 1}/{month + 1}/{year}";
            }

            public ModifiedDate(int _day, int _month, int _year)
            {
                day = _day;
                month = _month;
                year = _year;
                time = new MeridiemTime(0, 0);
            }
            public ModifiedDate(int _day, int _month, int _year, MeridiemTime _meridiemTime)
            {
                day = _day;
                month = _month;
                year = _year;
                time = _meridiemTime;
            }

            public static bool operator >(ModifiedDate a, ModifiedDate b)
            {
                if (a.year > b.year)
                    return true;
                if (a.month > b.month && a.year == b.year)
                    return true;
                if (a.day > b.day && a.month == b.month && a.year == b.year)
                    return true;
                // if (a.day == b.day && a.month == b.month && a.year == b.year && a.time)
                //     return true;

                return false;

            }
            public static bool operator <(ModifiedDate a, ModifiedDate b)
            {
                if (a.year < b.year)
                    return true;
                if (a.month < b.month && a.year == b.year)
                    return true;
                if (a.day < b.day && a.month == b.month && a.year == b.year)
                    return true;

                return false;

            }
            public static ModifiedDate operator -(ModifiedDate a, int b)
            {

                if (CozyWeather.instance.GetModule<CozyHabits>().profile)
                {
                    HabitsYearProfile profile = CozyWeather.instance.GetModule<CozyHabits>().profile;

                    a.day -= b;

                    while (a.day < 0)
                    {
                        a.month--;
                        if (a.month < 0)
                        {
                            a.month = profile.months.Count - 1;
                            a.year--;
                        }
                        a.day += profile.months[a.month].daysInMonth;
                    }

                    return a;
                }
                else
                    return new ModifiedDate(a.day - b, a.month, a.year);
            }
            public static ModifiedDate operator +(ModifiedDate a, int b)
            {

                if (CozyWeather.instance.GetModule<CozyHabits>().profile)
                {
                    HabitsYearProfile profile = CozyWeather.instance.GetModule<CozyHabits>().profile;



                    a.day += b;

                    while (a.day >= profile.months[a.month].daysInMonth)
                    {
                        a.day -= profile.months[a.month].daysInMonth;
                        a.month++;
                        if (a.month >= profile.months.Count)
                        {
                            a.month = 0;
                            a.year++;
                        }
                    }
                    return a;
                }
                else
                    return new ModifiedDate(a.day + b, a.month, a.year);
            }
            public static bool operator <=(ModifiedDate a, ModifiedDate b)
            {
                return (a.day == b.day && a.month == b.month && a.year == b.year) || a < b;
            }
            public static bool operator >=(ModifiedDate a, ModifiedDate b)
            {
                return (a.day == b.day && a.month == b.month && a.year == b.year) || a > b;
            }

        }

        public enum Weekday { sunday, monday, tuesday, wednesday, thursday, friday, saturday }

        private int yearLength;
        public int simpleDate;

        public float dayPercent;

        public enum Calendar { red = 1, orange = 2, yellow = 4, green = 8, lightBlue = 16, blue = 32, purple = 64, pink = 128, white = 256, grey = 512 }

        public override void InitializeModule()
        {
            base.InitializeModule();
            if (weatherSphere.timeModule)
                weatherSphere.timeModule.overrideDate = this;

#if UNITY_EDITOR
            if (!EditorPrefs.HasKey("CZY_HabitsCalendar"))
                EditorPrefs.SetInt("CZY_HabitsCalendar", 1023);
            if (!EditorPrefs.HasKey("CZY_HabitsShowTime"))
                EditorPrefs.SetBool("CZY_HabitsShowTime", true);
#endif

        }

        void OnStart()
        {

            SetupVariables();

        }

        public static Color CalendarColor(Calendar calendar)
        {

            switch (calendar)
            {
                case Calendar.red:
                    return new Color(232f / 255, 95f / 255, 92f / 255);
                case Calendar.orange:
                    return new Color(240f / 255, 135f / 255, 0);
                case Calendar.yellow:
                    return new Color(255f / 255, 200f / 255, 87f / 255);
                case Calendar.green:
                    return new Color(123f / 255, 200f / 255, 132f / 255);
                case Calendar.blue:
                    return new Color(51f / 255, 75f / 255, 200f / 255);
                case Calendar.purple:
                    return new Color(140f / 255, 78f / 255, 180f / 255);
                case Calendar.grey:
                    return new Color(0.5f, 0.5f, 0.5f);
                case Calendar.white:
                    return new Color(1, 1, 1);
                case Calendar.lightBlue:
                    return new Color(96f / 255, 180f / 255, 212f / 255);
                case Calendar.pink:
                    return new Color(220f / 255, 177f / 255, 203f / 255);



            }


            return Color.white;
        }

        public static string GetWeekdayNameFromInt(int id)
        {

            switch (id)
            {
                case 0:
                    return "S";
                case 1:
                    return "M";
                case 2:
                    return "T";
                case 3:
                    return "W";
                case 4:
                    return "T";
                case 5:
                    return "F";
                case 6:
                    return "S";
                default:
                    return "";
            }

        }

        public void SetupVariables()
        {
            FormatDate();
            yearLength = profile.GetYearLength();
            currentDay.events = ObserveDailySchedule(currentDay);
            GetCurrentWeek();
            GetCurrentMonth();
        }

        public CozyHabitProfile[] ObserveDailySchedule(Day day)
        {

            List<CozyHabitProfile> habits = new List<CozyHabitProfile>();

            foreach (CozyHabitProfile habit in profile.events)
            {

                if (habit == null)
                    continue;

                ModifiedDate startDay = !(habit.repeatStyle == CozyHabitProfile.RepeatStyle.never) ? new ModifiedDate(habit.startDate.day, habit.startDate.month, 0) : habit.startDate;
                ModifiedDate endDay = !(habit.repeatStyle == CozyHabitProfile.RepeatStyle.never) ? new ModifiedDate(habit.endDate.day, habit.endDate.month, 0) : habit.endDate;
                ModifiedDate currentDay = !(habit.repeatStyle == CozyHabitProfile.RepeatStyle.never) ? new ModifiedDate(day.date.day, day.date.month, 0) : day.date;

                if (startDay <= currentDay && endDay >= currentDay || !habit.dateRange)
                    switch (habit.repeatStyle)
                    {
                        case CozyHabitProfile.RepeatStyle.never:
                            if (habit.dateRange)
                            {
                                if (habit.startDate.day <= day.date.day && habit.endDate.day >= day.date.day && habit.startDate.month <= day.date.month && habit.endDate.month >= day.date.month)
                                {
                                    habits.Add(habit);
                                }
                            }
                            else
                            {
                                if (habit.startDate.day == day.date.day && habit.startDate.month == day.date.month && habit.startDate.year == day.date.year)
                                {
                                    habits.Add(habit);
                                }
                            }
                            break;
                        case CozyHabitProfile.RepeatStyle.annually:
                            if (habit.dateRange)
                            {
                                if (startDay.day == currentDay.day && startDay.month == currentDay.month)
                                    habits.Add(habit);

                            }
                            else if (startDay.day <= currentDay.day && endDay.day >= currentDay.day && startDay.month <= currentDay.month && endDay.month >= currentDay.month)
                            {
                                habits.Add(habit);
                            }
                            break;
                        case CozyHabitProfile.RepeatStyle.monthly:
                            if (habit.dateRange ? startDay.day <= currentDay.day && endDay.day >= currentDay.day : startDay.day == currentDay.day)
                            {
                                habits.Add(habit);
                            }
                            break;
                        case CozyHabitProfile.RepeatStyle.everyOtherDay:
                            if ((ConvertToSimpleDate(currentDay, true) - ConvertToSimpleDate(startDay, true)) % 2 == 0)
                            {
                                habits.Add(habit);
                            }
                            break;
                        case CozyHabitProfile.RepeatStyle.daily:
                            habits.Add(habit);
                            break;
                        case CozyHabitProfile.RepeatStyle.weekdays:
                            if (day.weekday == Weekday.monday || day.weekday == Weekday.tuesday || day.weekday == Weekday.wednesday || day.weekday == Weekday.thursday || day.weekday == Weekday.friday)
                            {
                                habits.Add(habit);
                            }
                            break;
                        case CozyHabitProfile.RepeatStyle.weekends:
                            if (day.weekday == Weekday.saturday || day.weekday == Weekday.sunday)
                            {
                                habits.Add(habit);
                            }
                            break;
                        case CozyHabitProfile.RepeatStyle.weekly:
                            if (habit.weekday == day.weekday)
                            {
                                habits.Add(habit);
                            }
                            break;
                    }
            }

            habits = habits.OrderBy(habitTime => (float)habitTime.startTime).ToList<CozyHabitProfile>();
            return habits.ToArray();

        }

        public void GetCurrentWeek()
        {
            List<Day> days = new List<Day>();
            // int startDay = ConvertToSimpleDate(currentDay.date, true);
            ModifiedDate startDate = currentDay.date - (int)currentDay.weekday;

            for (int i = 0; i < 7; i++)
            {

                Day day = new Day
                {
                    date = startDate + i,
                    weekday = (Weekday)i
                };
                day.events = ObserveDailySchedule(day);

                days.Add(day);

            }

            currentWeek = days.ToArray();

        }

        public void GetCurrentMonth()
        {

            if (profile == null)
                return;

            List<Day> days = new List<Day>();
            ModifiedDate startDate = currentDay.date - currentDay.date.day;
            int simpleStartDate = ConvertToSimpleDate(startDate, true);

            for (int i = 0; i < profile.months[startDate.month].daysInMonth; i++)
            {

                Day day = new Day
                {
                    date = startDate + i,
                    weekday = GetWeekday(simpleStartDate + i)
                };
                day.events = ObserveDailySchedule(day);

                days.Add(day);

            }

            currentMonth = days.ToArray();

        }

        void Update()
        {
            if (profile == null)
                return;

            if (yearLength == 0)
                SetupVariables();


            if (!Application.isPlaying)
            {
                FormatDate();
                currentDay.events = ObserveDailySchedule(currentDay);
                GetCurrentWeek();
                GetCurrentMonth();

            }



            yearPercentage = (float)simpleDate / yearLength;

            ManageEvents();

        }

        public int ConvertToSimpleDate(ModifiedDate date, bool includeYear)
        {
            int simpleDate = 0;

            if (includeYear)
                simpleDate += date.year * yearLength;

            for (int i = 0; i < date.month; i++)
            {
                simpleDate += profile.months[i].daysInMonth;
            }

            return simpleDate + date.day;
        }

        public void ManageEvents()
        {

            // List<CozyHabitProfile> habitsDuringThisFrame = currentDay.events.Where(h => MeridiemTime.MeridiemTimeToDayPercent(h.startTime) <= weatherSphere.dayPercentage && MeridiemTime.MeridiemTimeToDayPercent(h.endTime) >= dayPercent).ToList();

            if (weatherSphere.dayPercentage < dayPercent && weatherSphere.dayPercentage < 0.05f)
                dayPercent = 0;

            foreach (CozyHabitProfile habit in currentDay.events)
            {

                bool check = habit.overnight ? (habit.startTime <= weatherSphere.dayPercentage) ||
                (habit.endTime >= dayPercent) :
                 habit.startTime <= weatherSphere.dayPercentage && habit.endTime >= dayPercent;

                if (check)
                {

                    if (habit.runHabitContinuously)
                    {
                        if (weatherSphere.weatherModule)
                        {
                            if (!habit.cancelIfWeatherIsPlaying.Contains(weatherSphere.weatherModule.ecosystem.currentWeather))
                                habit.RaiseOnUpdate();
                        }
                        else
                            habit.RaiseOnUpdate();
                    }
                    if (habit.runHabitOnStart && !habit.isEventRunning)
                    {
                        if (weatherSphere.weatherModule)
                        {
                            if (!habit.cancelIfWeatherIsPlaying.Contains(weatherSphere.weatherModule.ecosystem.currentWeather))
                                habit.RaiseOnStart();
                        }
                        else
                            habit.RaiseOnStart();
                    }

                    habit.isEventRunning = true;

                }
                else
                {
                    if (habit.runHabitOnEnd && habit.isEventRunning)
                    {
                        habit.RaiseOnEnd();

                    }

                    habit.isEventRunning = false;
                }
            }

            dayPercent = weatherSphere.dayPercentage;
        }

        void FormatDate()
        {

            simpleDate = ConvertToSimpleDate(currentDay.date, false);

            while (currentDay.date.day < 0)
            {
                currentDay.date.month--;
                if (currentDay.date.month < 0)
                {
                    currentDay.date.month = profile.months.Count - 1;
                    currentDay.date.year--;
                }
                currentDay.date.day += profile.months[currentDay.date.month].daysInMonth;
            }
            while (currentDay.date.day >= profile.months[currentDay.date.month].daysInMonth)
            {
                currentDay.date.day -= profile.months[currentDay.date.month].daysInMonth;
                currentDay.date.month++;
                if (currentDay.date.month >= profile.months.Count)
                {
                    currentDay.date.month = 0;
                    currentDay.date.year++;
                }
            }

            currentDay.weekday = GetWeekday(ConvertToSimpleDate(currentDay.date, true));


        }

        public Weekday GetWeekday(int day)
        {
            return (Weekday)ClampWithLoop((int)(day - Mathf.Floor(day / 7) * 7) + (int)profile.startDay, 0, 6);
        }

        public override float GetCurrentYearPercentage()
        {
            return yearPercentage;
        }

        public override float GetCurrentYearPercentage(float inTime)
        {
            return (float)(simpleDate + Mathf.Round(inTime)) / yearLength;
        }

        public override float DayAndTime()
        {
            return (float)(simpleDate + weatherSphere.timeModule.currentTime);
        }

        public override void ChangeDay(int days)
        {
            foreach (CozyHabitProfile habit in currentDay.events)
            {
                if (habit.allDay)
                {
                    habit.RaiseOnEnd();
                }
            }

            currentDay.date.day += days;
            FormatDate();
            currentDay.events = ObserveDailySchedule(currentDay);
            weatherSphere.events.RaiseOnDayChange();
            if (weatherSphere.timeModule)
            {
                weatherSphere.timeModule.currentDay = simpleDate;
                weatherSphere.timeModule.currentYear = currentDay.date.year;
            }
            if (weatherSphere.timeModule.transit)
                weatherSphere.timeModule.transit.GetModifiedDayPercent();

            foreach (CozyHabitProfile habit in currentDay.events)
            {
                if (habit.allDay)
                {
                    habit.RaiseOnStart();
                }
            }
        }

        public static int ClampWithLoop(int value, int minValue, int maxValue)
        {
            int range = maxValue - minValue + 1;

            while (value < minValue)
                value += range;

            while (value > maxValue)
                value -= range;

            return value;
        }

        public override int DaysPerYear()
        {
            return yearLength;
        }

    }
}