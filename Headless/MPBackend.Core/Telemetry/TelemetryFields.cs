using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using MissionPlanner;
using MissionPlanner.Attributes;

namespace MPBackend.Core.Telemetry
{
    public sealed record TelemetryField(string Name, string Type, string DisplayText, string Group);

    /// <summary>
    /// Reflection cache over <see cref="CurrentState"/>: the same ~1000 properties MP binds its HUD, quick view and
    /// status tab to. Getters are compiled once so snapshots are cheap.
    /// </summary>
    public static class TelemetryFields
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(TelemetryFields));

        private static readonly Dictionary<string, Func<CurrentState, object>> getters;

        static TelemetryFields()
        {
            var fields = new List<TelemetryField>();
            getters = new Dictionary<string, Func<CurrentState, object>>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in typeof(CurrentState).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0 || !IsSimple(prop.PropertyType))
                    continue;

                var display = prop.GetCustomAttribute<DisplayTextAttribute>()?.Text;
                var group = prop.GetCustomAttribute<GroupText>()?.DisplayName;
                fields.Add(new TelemetryField(prop.Name, TypeName(prop.PropertyType), display, group));
                getters[prop.Name] = Compile(prop);
            }

            All = fields.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static IReadOnlyList<TelemetryField> All { get; }

        public static bool Exists(string name) => getters.ContainsKey(name);

        /// <summary>
        /// Read the requested fields (all when null/empty). Enums are returned as their names.
        /// Unknown names are ignored.
        /// </summary>
        public static Dictionary<string, object> Snapshot(CurrentState cs, IEnumerable<string> names = null)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            var selected = names?.ToList();
            IEnumerable<KeyValuePair<string, Func<CurrentState, object>>> source =
                selected == null || selected.Count == 0
                    ? getters
                    : selected.Where(getters.ContainsKey).Select(n =>
                        new KeyValuePair<string, Func<CurrentState, object>>(n, getters[n]));

            foreach (var (name, getter) in source)
            {
                try
                {
                    var value = getter(cs);
                    result[name] = value is Enum e ? e.ToString() : value;
                }
                catch (Exception ex)
                {
                    log.Debug($"telemetry field {name}: {ex.Message}");
                }
            }

            return result;
        }

        private static Func<CurrentState, object> Compile(PropertyInfo prop)
        {
            var cs = Expression.Parameter(typeof(CurrentState), "cs");
            var body = Expression.Convert(Expression.Property(cs, prop), typeof(object));
            return Expression.Lambda<Func<CurrentState, object>>(body, cs).Compile();
        }

        private static bool IsSimple(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
                   type == typeof(DateTime) || type == typeof(TimeSpan);
        }

        private static string TypeName(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsEnum) return "enum";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(DateTime)) return "datetime";
            if (type == typeof(TimeSpan)) return "timespan";
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "number";
            return "integer";
        }
    }
}
