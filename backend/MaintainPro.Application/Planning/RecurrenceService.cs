using MaintainPro.Application.Common;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Planning;

/// <summary>
/// Calendar-month intervals always retain the original anchor day and clamp it to the
/// target month's length. January 31 therefore returns to March 31 after February;
/// January 30 returns to March 30. A day interval never means a calendar month.
/// </summary>
public sealed class RecurrenceService
{
    public void Validate(MaintenanceFrequencyType type, int value)
    {
        if (!Enum.IsDefined(type)) throw new AppException(400, "FrequencyType is not valid.");
        if (type is MaintenanceFrequencyType.DAYS or MaintenanceFrequencyType.WEEKS or MaintenanceFrequencyType.MONTHS)
        {
            if (value < 1) throw new AppException(400, "Custom frequency values must be positive.");
        }
        else if (value != 1)
            throw new AppException(400, "Fixed frequencies require FrequencyValue to be 1.");
    }

    public DateOnly Next(DateOnly anchorDate, DateOnly afterDate, MaintenanceFrequencyType type, int value)
    {
        Validate(type, value);
        if (afterDate < anchorDate) return anchorDate;
        if (afterDate == DateOnly.MaxValue)
            throw new AppException(400, "The next occurrence exceeds the supported calendar range.");
        return OnOrAfter(anchorDate, afterDate.AddDays(1), type, value);
    }

    public DateOnly OnOrAfter(DateOnly anchorDate, DateOnly notBefore, MaintenanceFrequencyType type, int value)
    {
        Validate(type, value);
        if (notBefore <= anchorDate) return anchorDate;

        var dayInterval = type switch
        {
            MaintenanceFrequencyType.DAILY => 1L,
            MaintenanceFrequencyType.WEEKLY => 7L,
            MaintenanceFrequencyType.DAYS => value,
            MaintenanceFrequencyType.WEEKS => 7L * value,
            _ => 0L
        };
        if (dayInterval > 0)
        {
            var elapsed = (long)notBefore.DayNumber - anchorDate.DayNumber;
            var occurrenceIndex = (elapsed + dayInterval - 1) / dayInterval;
            var dayNumber = anchorDate.DayNumber + occurrenceIndex * dayInterval;
            if (dayNumber > DateOnly.MaxValue.DayNumber)
                throw new AppException(400, "The next occurrence exceeds the supported calendar range.");
            return DateOnly.FromDayNumber((int)dayNumber);
        }

        var monthInterval = type switch
        {
            MaintenanceFrequencyType.MONTHLY => 1L,
            MaintenanceFrequencyType.MONTHS => value,
            MaintenanceFrequencyType.QUARTERLY => 3L,
            MaintenanceFrequencyType.HALF_YEARLY => 6L,
            MaintenanceFrequencyType.YEARLY => 12L,
            _ => throw new AppException(400, "FrequencyType is not valid.")
        };
        var elapsedMonths = (notBefore.Year - anchorDate.Year) * 12L + notBefore.Month - anchorDate.Month;
        var index = elapsedMonths / monthInterval;
        var candidate = AtMonthOffset(anchorDate, index * monthInterval);
        return candidate >= notBefore ? candidate : AtMonthOffset(anchorDate, (index + 1) * monthInterval);
    }

    private static DateOnly AtMonthOffset(DateOnly anchorDate, long offset)
    {
        var absoluteMonth = (anchorDate.Year - 1) * 12L + anchorDate.Month - 1 + offset;
        if (absoluteMonth is < 0 or >= 9999L * 12)
            throw new AppException(400, "The next occurrence exceeds the supported calendar range.");
        var year = (int)(absoluteMonth / 12) + 1;
        var month = (int)(absoluteMonth % 12) + 1;
        return new DateOnly(year, month, Math.Min(anchorDate.Day, DateTime.DaysInMonth(year, month)));
    }
}
