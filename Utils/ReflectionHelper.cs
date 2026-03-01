using System;
using System.Collections.Generic;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge;

public static class ReflectionHelper
{
    private static readonly Dictionary<(Type, string), FieldInfo> _fieldCache = [];

    private static FieldInfo GetFieldInfo(Type type, string fieldName)
    {
        var key = (type, fieldName);
        if (_fieldCache.TryGetValue(key, out var field))
        {
            return field;
        }

        field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (field is not null)
        {
            _fieldCache[key] = field;
        }
        return field;
    }

    public static T GetPrivateField<T>(object instance, string fieldName) where T : class
    {
        if (instance is null) return null;

        var field = GetFieldInfo(instance.GetType(), fieldName);

        if (field is null)
        {
            Logger.LogWarning($"[ReflectionHelper] Field '{fieldName}' not found on type '{instance.GetType().Name}'");
            return null;
        }
        return field.GetValue(instance) as T;
    }

    public static void SetPrivateField(object instance, string fieldName, object value)
    {
        if (instance is null) return;

        var field = GetFieldInfo(instance.GetType(), fieldName);

        if (field is not null)
        {
            field.SetValue(instance, value);
        }
        else
        {
            Logger.LogWarning($"[ReflectionHelper] Field '{fieldName}' not found on type '{instance.GetType().Name}'");
        }
    }
}
