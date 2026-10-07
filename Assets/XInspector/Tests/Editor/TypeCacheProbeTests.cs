using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 类型选择器的候选枚举：<c>TypeCache.GetTypesDerivedFrom</c> 的四条**形状测量**。
    /// <para>
    /// 本仓此前只用过它的泛型形态（<c>EditorTypeScanner.CollectInstantiable</c>），
    /// 而类型选择器的基类是**运行期变量**。<c>GetTypesDerivedFrom</c> 对接口、抽象类、
    /// <c>object</c>、开放泛型分别给什么——本仓一个字都没测过，故先量再写判据。
    /// </para>
    /// <para>
    /// 夹具类型全部定义在本文件里；**不断言框架类型在不在**（那会随工程而变），
    /// 也**不钉数量与字符串格式**——只钉「拿得到 / 拿不到」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class TypeCacheProbeTests
    {
        #region 探针

        /// <summary>接口的派生里有什么：实现类？接口自己？派生接口？抽象实现类？</summary>
        [Test]
        public void 接口的派生里有什么()
        {
            var types = new List<Type>(TypeCache.GetTypesDerivedFrom(typeof(ITypeCacheProbeContract)));
            Log("接口", types);

            Assert.That(types, Has.Member(typeof(TypeCacheProbeImplementation)), "直接实现类在。");
            Assert.That(
                types,
                Has.Member(typeof(TypeCacheProbeConcrete)),
                "经抽象类间接实现的也在。");
            Assert.That(
                types,
                Has.Member(typeof(TypeCacheProbeAbstract)),
                "抽象实现类在（接口的派生不筛抽象）。");
            Assert.That(
                types,
                Has.No.Member(typeof(ITypeCacheProbeContract)),
                "接口自己不在——GetTypesDerivedFrom 的语义本就写着「派生」。");
            Assert.That(
                types,
                Has.Member(typeof(ITypeCacheProbeDerivedContract)),
                "派生接口在（实测）。");
        }

        /// <summary>抽象类与 <c>object</c> 的派生里有什么：含自身？含接口？结构体枚举？</summary>
        [Test]
        public void 抽象类与object的派生里有什么()
        {
            var abstractTypes = new List<Type>(TypeCache.GetTypesDerivedFrom(typeof(TypeCacheProbeAbstract)));
            Log("抽象类", abstractTypes);

            Assert.That(abstractTypes, Has.Member(typeof(TypeCacheProbeConcrete)), "具体派生类在。");
            Assert.That(
                abstractTypes,
                Has.No.Member(typeof(TypeCacheProbeAbstract)),
                "基类自己不在。");

            var objectTypes = new List<Type>(TypeCache.GetTypesDerivedFrom(typeof(object)));
            Log("object", objectTypes);

            Assert.That(
                objectTypes,
                Has.Member(typeof(TypeCacheProbeAbstract)),
                "普通类在 object 的派生里。");
            Assert.That(
                objectTypes,
                Has.No.Member(typeof(ITypeCacheProbeContract)),
                "实测：**接口不在 object 的派生里**——不设约束时接口不在候选里，"
                + "要接口请把 BaseType 写成那个接口（这条要进 README）。");
            Assert.That(
                objectTypes,
                Has.Member(typeof(TypeCacheProbeStruct)),
                "实测：结构体在 object 的派生里（它们是值类型，但仍是「派生自 object」）。");
        }

        /// <summary>开放泛型的派生：返回派生实现，还是抛异常？</summary>
        [Test]
        public void 开放泛型的派生给什么()
        {
            List<Type> types = null;
            Exception error = null;

            try
            {
                types = new List<Type>(TypeCache.GetTypesDerivedFrom(typeof(ITypeCacheProbeGeneric<>)));
            }
            catch (Exception exception)
            {
                error = exception;
            }

            TestContext.WriteLine(
                error == null
                    ? $"[探针] 开放泛型：未抛异常；{Describe(types)}"
                    : $"[探针] 开放泛型：**抛了** {error.GetType().Name}——{error.Message}");

            Assert.That(error, Is.Null, "候选枚举要据此决定要不要 try/catch（抛了就得兜）。");
            Assert.That(
                types,
                Has.Member(typeof(TypeCacheProbeGenericImplementation)),
                "闭合实现（实现了 ITypeCacheProbeGeneric<int>）在开放泛型的派生里（实测）。");
        }

        #endregion

        #region 工具

        /// <summary>把一格测量写进日志。</summary>
        /// <param name="capture">这一格在测什么。</param>
        /// <param name="types">拿到的类型。</param>
        private static void Log(string capture, List<Type> types)
        {
            TestContext.WriteLine($"[探针] {capture}：{Describe(types)}");
        }

        /// <summary>把一列类型压成一行短描述（数量 + 夹具相关的几个）。</summary>
        /// <param name="types">类型列表；可以为 <c>null</c>。</param>
        /// <returns>描述文本。</returns>
        private static string Describe(List<Type> types)
        {
            if (types == null)
            {
                return "null";
            }

            var names = new List<string>();
            foreach (var type in types)
            {
                if (type.FullName != null && type.FullName.Contains("TypeCacheProbe"))
                {
                    names.Add(type.Name);
                }
            }

            return $"共 {types.Count} 个；夹具相关的有 {names.Count} 个：{string.Join(", ", names)}";
        }

        #endregion
    }

    #region Fixtures

    /// <summary>测量用的接口。</summary>
    internal interface ITypeCacheProbeContract
    {
    }

    /// <summary>派生接口——测「接口的派生里含不含派生接口」。</summary>
    internal interface ITypeCacheProbeDerivedContract : ITypeCacheProbeContract
    {
    }

    /// <summary>直接实现。</summary>
    internal sealed class TypeCacheProbeImplementation : ITypeCacheProbeContract
    {
    }

    /// <summary>抽象实现类——既测「接口的派生含不含抽象类」，也当下面那条抽象类测量的基。</summary>
    internal abstract class TypeCacheProbeAbstract : ITypeCacheProbeContract
    {
    }

    /// <summary>经抽象类**间接**实现接口的具体类。</summary>
    internal sealed class TypeCacheProbeConcrete : TypeCacheProbeAbstract
    {
    }

    /// <summary>结构体——测 <c>object</c> 的派生里含不含值类型。</summary>
    internal struct TypeCacheProbeStruct
    {
    }

    /// <summary>开放泛型接口。</summary>
    internal interface ITypeCacheProbeGeneric<T>
    {
    }

    /// <summary>闭合实现——它实现的是 <c>ITypeCacheProbeGeneric&lt;int&gt;</c>。</summary>
    internal sealed class TypeCacheProbeGenericImplementation : ITypeCacheProbeGeneric<int>
    {
    }

    #endregion
}
