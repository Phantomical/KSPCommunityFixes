using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using static HarmonyLib.SymbolExtensions;

namespace KSPCommunityFixes
{
    public class AggressiveInlining : BasePatch
    {
        protected override Version VersionMin => new Version(1, 12, 5);

        protected override void ApplyPatches() {
            Debug.Log("Testing");
        }

        protected override void OnPatchApplied()
        {
            foreach (var method in TargetMethods())
            {
                Debug.Log($"Patching {method?.DeclaringType?.Name ?? "<null>"}.{method?.Name ?? "<null>"}");
                SetAggressiveInline(method);
            }
        }

        private IEnumerable<MethodBase> TargetMethods()
        {

            // Vector3
            foreach (var ctor in typeof(Vector3).GetConstructors())
                yield return ctor;

            yield return GetPropertyGetter((Vector3 v) => v.normalized);
            yield return GetPropertyGetter((Vector3 v) => v.magnitude);
            yield return GetPropertyGetter((Vector3 v) => v.sqrMagnitude);

            yield return GetMethodInfo(() => Vector3.Cross(default, default));
            yield return GetMethodInfo(() => Vector3.Dot(default, default));
            yield return GetMethodInfo(() => Vector3.Normalize(default));
            yield return GetMethodInfo(() => default(Vector3).Normalize());
            yield return GetMethodInfo(() => Vector3.Max(default, default));
            yield return GetMethodInfo(() => Vector3.Min(default, default));
            yield return GetMethodInfo(() => Vector3.SqrMagnitude(default));

            yield return AccessTools.Method(typeof(Vector3), "op_Addition");
            yield return AccessTools.Method(typeof(Vector3), "op_Division");
            yield return AccessTools.Method(typeof(Vector3), "op_Equality");
            yield return AccessTools.Method(typeof(Vector3), "op_Inequality");
            yield return AccessTools.Method(typeof(Vector3), "op_Multiply", new Type[]{ typeof(Vector3), typeof(float) });
            yield return AccessTools.Method(typeof(Vector3), "op_Multiply", new Type[]{ typeof(float), typeof(Vector3) });
            yield return AccessTools.Method(typeof(Vector3), "op_Subtraction");
            yield return AccessTools.Method(typeof(Vector3), "op_UnaryNegation");

            // Vector3d
            foreach (var ctor in typeof(Vector3d).GetConstructors())
                yield return ctor;

            yield return GetPropertyGetter((Vector3d v) => v.xzy);
            yield return GetPropertyGetter((Vector3d v) => v.magnitude);
            yield return GetPropertyGetter((Vector3d v) => v.normalized);
            yield return GetPropertyGetter((Vector3d v) => v.sqrMagnitude);

            yield return GetMethodInfo(() => Vector3d.Cross(default, default));
            yield return GetMethodInfo(() => Vector3d.Distance(default, default));
            yield return GetMethodInfo(() => Vector3d.Dot(default, default));
            yield return GetMethodInfo(() => Vector3d.Magnitude(default));
            yield return GetMethodInfo(() => Vector3d.Min(default, default));
            yield return GetMethodInfo(() => Vector3d.Max(default, default));
            yield return GetMethodInfo(() => Vector3d.Normalize(default));
            yield return GetMethodInfo(() => Vector3d.Scale(default, default));
            yield return GetMethodInfo(() => default(Vector3d).Normalize());
            yield return GetMethodInfo(() => default(Vector3d).Swizzle());
            yield return GetMethodInfo(() => default(Vector3d).Scale(default));
            yield return GetMethodInfo(() => default(Vector3d).Zero());

            yield return AccessTools.Method(typeof(Vector3d), "op_Addition", new Type[]{ typeof(Vector3d), typeof(Vector3d) });
            yield return AccessTools.Method(typeof(Vector3d), "op_Addition", new Type[]{ typeof(Vector3d), typeof(Vector3) });
            yield return AccessTools.Method(typeof(Vector3d), "op_Addition", new Type[]{ typeof(Vector3), typeof(Vector3d) });
            yield return AccessTools.Method(typeof(Vector3d), "op_Division");
            yield return AccessTools.Method(typeof(Vector3d), "op_Implicit", new Type[]{ typeof(Vector3) });
            yield return AccessTools.Method(typeof(Vector3d), "op_Implicit", new Type[]{ typeof(Vector3d) });
            yield return AccessTools.Method(typeof(Vector3d), "op_Equality");
            yield return AccessTools.Method(typeof(Vector3d), "op_Inequality");
            yield return AccessTools.Method(typeof(Vector3d), "op_Multiply", new Type[]{ typeof(Vector3d), typeof(double) });
            yield return AccessTools.Method(typeof(Vector3d), "op_Multiply", new Type[]{ typeof(double), typeof(Vector3d) });
            yield return AccessTools.Method(typeof(Vector3d), "op_Subtraction", new Type[]{ typeof(Vector3d), typeof(Vector3d) });
            yield return AccessTools.Method(typeof(Vector3d), "op_Subtraction", new Type[]{ typeof(Vector3d), typeof(Vector3) });
            yield return AccessTools.Method(typeof(Vector3d), "op_Subtraction", new Type[]{ typeof(Vector3), typeof(Vector3d) });
            yield return AccessTools.Method(typeof(Vector3d), "op_UnaryNegation");

            // List<T>
            yield return AccessTools.Method(typeof(List<>), nameof(List<int>.Add));
            yield return AccessTools.Method(typeof(List<>), nameof(List<int>.GetEnumerator));
            var list_getItem = typeof(List<>)
                .GetProperties()
                .Where(prop => prop.GetIndexParameters().Length != 0)
                .First();

            yield return list_getItem.GetGetMethod();
            yield return list_getItem.GetSetMethod();

            // List<T>.Enumerator
            foreach (var ctor in typeof(List<>.Enumerator).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                yield return ctor;
            yield return AccessTools.Method(typeof(List<>.Enumerator), nameof(List<int>.Enumerator.MoveNext));
        }

        private unsafe void SetAggressiveInline(MethodBase method)
        {
            if (method is DynamicMethod)
                return; // not supported

            var handle = method.MethodHandle;
            var ptr = (MonoMethod*)handle.Value;

            ptr->iflags |= (ushort)MethodImplOptions.AggressiveInlining;
        }

        // This is the first few members of the _MonoMethod struct as put within
        // the unity source:
        // https://github.com/Unity-Technologies/mono/blob/452383f5705f49c1f5633ecf20463ff311f14b1e/mono/metadata/class-internals.h#L72
        [StructLayout(LayoutKind.Sequential)]
        struct MonoMethod
        {
            public ushort flags;  // method flags
            public ushort iflags; // method implementation flags

            // .. the rest is not important
        }

        private static MethodInfo GetPropertyGetter(LambdaExpression lambda)
        {
            if (!(lambda.Body is MemberExpression member))
                throw new ArgumentException("Invalid expression. Expected a member access expression");

            if (!(member.Member is PropertyInfo property))
                throw new ArgumentException($"{member.Member.DeclaringType.Name}.{member.Member.Name} is not a property");

            var getter = property.GetGetMethod();
            if (getter is null)
                throw new ArgumentException($"{property.DeclaringType.Name}.{property.Name} does not have a getter");
            
            return getter;
        }

        private static MethodInfo GetPropertyGetter<T>(Expression<Action<T>> expr)
        {
            return GetPropertyGetter((LambdaExpression)expr);
        }

        private static MethodInfo GetPropertyGetter<T, R>(Expression<Func<T, R>> expr)
        {
            return GetPropertyGetter((LambdaExpression)expr);
        }

        private static MethodInfo GetPropertyGetter(Expression<Action> expr)
        {
            return GetPropertyGetter((LambdaExpression)expr);
        }

        private static MethodInfo GetPropertyGetter<R>(Expression<Func<R>> expr)
        {
            return GetPropertyGetter((LambdaExpression)expr);
        }

        private static MethodInfo GetPropertySetter(LambdaExpression lambda)
        {
            if (!(lambda.Body is MemberExpression member))
                throw new ArgumentException("Invalid expression. Expected a member access expression");

            if (!(member.Member is PropertyInfo property))
                throw new ArgumentException($"{member.Member.DeclaringType.Name}.{member.Member.Name} is not a property");

            var setter = property.GetSetMethod();
            if (setter is null)
                throw new ArgumentException($"{property.DeclaringType.Name}.{property.Name} does not have a setter");
            
            return setter;
        }

        private static MethodInfo GetPropertySetter<T>(Expression<Action<T>> expr)
        {
            return GetPropertySetter((LambdaExpression)expr);
        }

        private static MethodInfo GetPropertySetter(Expression<Action> expr)
        {
            return GetPropertySetter((LambdaExpression)expr);
        }

        private static MethodInfo GetPropertySetter<T, R>(Expression<Func<T, R>> expr)
        {
            return GetPropertySetter((LambdaExpression)expr);
        }

        private static MethodInfo GetPropertySetter<R>(Expression<Func<R>> expr)
        {
            return GetPropertySetter((LambdaExpression)expr);
        }
    }
}
