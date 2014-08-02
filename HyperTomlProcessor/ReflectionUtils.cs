using System;
using System.Collections;
using System.Collections.Generic;

namespace HyperTomlProcessor
{
    internal static class ReflectionUtils
    {
        internal static Type GetCollectionType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return i.GetGenericArguments()[0];
            }
            return typeof(object);
        }

        internal static bool TryGetDictionaryType(Type type, out Type keyType, out Type valueType)
        {
            if (type == typeof(IDictionary) || type == typeof(object))
            {
                keyType = valueType = typeof(object);
                return true;
            }
            var iDic = false;
            Type iDicGeneric = null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                iDicGeneric = type;
            }
            else
            {
                foreach (var i in type.GetInterfaces())
                {
                    if (!iDic && i == typeof(IDictionary))
                    {
                        iDic = true;
                        if (iDicGeneric != null) break;
                    }
                    else if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    {
                        iDicGeneric = i;
                        break;
                    }
                }
            }
            if (iDicGeneric != null)
            {
                var genericTypes = iDicGeneric.GetGenericArguments();
                keyType = genericTypes[0];
                valueType = genericTypes[1];
                return true;
            }
            if (iDic)
            {
                keyType = valueType = typeof(object);
                return true;
            }
            keyType = valueType = null;
            return false;
        }
    }
}
