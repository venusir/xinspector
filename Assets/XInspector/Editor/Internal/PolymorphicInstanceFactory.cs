using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace XInspector.Editor
{
    /// <summary>
    /// 按 <c>Type</c> 造一个实例——多态选择器换类型时用（值是必须当场存在的：
    /// <c>managedReferenceValue</c> 要的是一份活实例，不是类型名）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>四档 <see cref="NonDefaultConstructorPreference"/> 的差别只在「没有公开无参构造」那一格。</b>
    /// 值类型与有无参构造的类型四档一视同仁（<c>Activator.CreateInstance</c>）；
    /// 没有无参构造时才按档：`ConstructIdeal` 挑「最直接的构造」、`Exclude` 直接失败
    /// （候选里本就不该有它）、`LogWarning` 不构造只失败（调用方告警）、
    /// `PreferUninitialized` 用 <c>FormatterServices.GetUninitializedObject</c>（不跑构造）。
    /// </para>
    /// <para>
    /// <b>失败一律给中文原因、不许静默。</b> 构造体自己抛的异常会被 <c>Activator</c>/<c>Invoke</c>
    /// 包成 <c>TargetInvocationException</c>，原因取它的 <c>InnerException</c> 才说人话。
    /// </para>
    /// <para>
    /// <c>FormatterServices</c> 在 Unity 6000.4 的 .NET Standard 2.1 参考程序集里存在、
    /// 不带过时告警，**不需要 pragma**。哪天 Unity 把它标成过时，用一句
    /// <c>#pragma warning disable SYSLIB0050</c> 顶住即可，别改成自研 IL。
    /// </para>
    /// </remarks>
    internal static class PolymorphicInstanceFactory
    {
        /// <summary>
        /// 按偏好造一个实例。
        /// </summary>
        /// <param name="type">要造的类型。</param>
        /// <param name="preference">非默认构造的处置档。</param>
        /// <param name="instance">造出的实例；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因（中文，可直接拼进告警）；成功时为 <c>null</c>。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        public static bool TryCreate(
            Type type, NonDefaultConstructorPreference preference, out object instance, out string reason)
        {
            instance = null;
            reason = null;

            if (type == null)
            {
                reason = "类型为空";
                return false;
            }

            if (type.IsAbstract || type.IsInterface)
            {
                reason = $"{ReflectedAccessor.DescribeType(type)} 是抽象类或接口，造不出实例";
                return false;
            }

            if (type.IsGenericTypeDefinition || type.ContainsGenericParameters)
            {
                reason = $"{ReflectedAccessor.DescribeType(type)} 是开放泛型，造不出实例";
                return false;
            }

            if (type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null)
            {
                return TryActivate(type, out instance, out reason);
            }

            switch (preference)
            {
                case NonDefaultConstructorPreference.Exclude:
                    reason =
                        $"{ReflectedAccessor.DescribeType(type)} 没有公开无参构造——" +
                        "Exclude 档本不该把它列进候选";
                    return false;

                case NonDefaultConstructorPreference.LogWarning:
                    reason =
                        $"{ReflectedAccessor.DescribeType(type)} 没有公开无参构造——" +
                        "按 LogWarning 档**不构造**（只留这条告警）";
                    return false;

                case NonDefaultConstructorPreference.PreferUninitialized:
                    return TryUninitialized(type, out instance, out reason);

                default:
                    return TryConstructIdeal(type, out instance, out reason);
            }
        }

        /// <summary>有（或不需要）无参构造的那一格：<c>Activator.CreateInstance</c>。</summary>
        /// <param name="type">类型。</param>
        /// <param name="instance">实例。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryActivate(Type type, out object instance, out string reason)
        {
            try
            {
                instance = Activator.CreateInstance(type);
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                instance = null;
                reason = DescribeFailure(type, exception);
                return false;
            }
        }

        /// <summary>
        /// <c>ConstructIdeal</c> 那一档：挑**参数最少**的公开实例构造，参数逐个填默认值。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <param name="instance">实例。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// 参数填值用 <see cref="ButtonParameters.DefaultFor"/>（C# 的 <c>default</c>）——
        /// 与按钮参数区同一个问题的同一份答案。同数量构造按<b>元数据令牌</b>取小者定序：
        /// <c>GetConstructors</c> 的顺序没有契约，不定序会让「挑哪一个」随运行时漂。
        /// </remarks>
        private static bool TryConstructIdeal(Type type, out object instance, out string reason)
        {
            instance = null;
            reason = null;

            var constructors = type.GetConstructors();
            ConstructorInfo best = null;
            var bestCount = int.MaxValue;

            for (var i = 0; i < constructors.Length; i++)
            {
                var candidate = constructors[i];
                var count = candidate.GetParameters().Length;

                if (best == null || count < bestCount ||
                    (count == bestCount && candidate.MetadataToken < best.MetadataToken))
                {
                    best = candidate;
                    bestCount = count;
                }
            }

            if (best == null)
            {
                reason = $"{ReflectedAccessor.DescribeType(type)} 没有公开的实例构造";
                return false;
            }

            var parameters = best.GetParameters();
            var arguments = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                arguments[i] = ButtonParameters.DefaultFor(parameters[i].ParameterType);
            }

            try
            {
                instance = best.Invoke(arguments);
                return true;
            }
            catch (Exception exception)
            {
                reason = DescribeFailure(type, exception);
                return false;
            }
        }

        /// <summary>
        /// <c>PreferUninitialized</c> 那一档：<c>FormatterServices.GetUninitializedObject</c>——**不跑构造**。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <param name="instance">实例。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryUninitialized(Type type, out object instance, out string reason)
        {
            try
            {
                instance = FormatterServices.GetUninitializedObject(type);
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                instance = null;
                reason = DescribeFailure(type, exception);
                return false;
            }
        }

        /// <summary>把构造失败转成一句中文原因（拆掉反射的包装异常）。</summary>
        /// <param name="type">类型。</param>
        /// <param name="exception">异常。</param>
        /// <returns>原因文本。</returns>
        private static string DescribeFailure(Type type, Exception exception)
        {
            var inner = exception is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : exception;

            return $"构造 {ReflectedAccessor.DescribeType(type)} 失败：{inner.Message}";
        }
    }
}
