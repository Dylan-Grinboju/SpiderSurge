using System;
using System.Collections.Generic;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public static class ReflectionHelper
    {
        private static readonly Dictionary<(Type, string), FieldInfo> _fieldCache = new Dictionary<(Type, string), FieldInfo>();

        private static FieldInfo GetFieldInfo(Type type, string fieldName)
        {
            var key = (type, fieldName);
            if (_fieldCache.TryGetValue(key, out var field))
            {
                return field;
            }

            field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                _fieldCache[key] = field;
            }
            return field;
        }

        public static T GetPrivateField<T>(object instance, string fieldName) where T : class
        {
            if (instance == null) return null;

            var field = GetFieldInfo(instance.GetType(), fieldName);

            if (field == null)
            {
                Logger.LogWarning($"[ReflectionHelper] Field '{fieldName}' not found on type '{instance.GetType().Name}'");
                return null;
            }
            return field.GetValue(instance) as T;
        }

        public static void SetPrivateField(object instance, string fieldName, object value)
        {
            if (instance == null) return;

            var field = GetFieldInfo(instance.GetType(), fieldName);

            if (field != null)
            {
                field.SetValue(instance, value);
            }
            else
            {
                Logger.LogWarning($"[ReflectionHelper] Field '{fieldName}' not found on type '{instance.GetType().Name}'");
            }
        }
    }
}
