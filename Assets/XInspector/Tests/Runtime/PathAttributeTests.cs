using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 路径两特性（<c>[FilePath]</c> / <c>[FolderPath]</c>）的构造行为与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PathAttributeTests
    {
        #region 默认值

        /// <summary>两个特性的默认都是「工程相对、正斜杠、不存绝对路径、不校验存在」。</summary>
        [Test]
        public void 默认值()
        {
            var file = new FilePathAttribute();
            Assert.That(file.ParentFolder, Is.Null, "默认相对工程根。");
            Assert.That(file.Extensions, Is.Null, "默认不限扩展名。");
            Assert.That(file.AbsolutePath, Is.False);
            Assert.That(file.RequireExistingPath, Is.False);
            Assert.That(file.UseBackslashes, Is.False);

            var folder = new FolderPathAttribute();
            Assert.That(folder.ParentFolder, Is.Null);
            Assert.That(folder.AbsolutePath, Is.False);
            Assert.That(folder.RequireExistingPath, Is.False);
            Assert.That(folder.UseBackslashes, Is.False);
        }

        #endregion

        #region 具名参数

        /// <summary>参数走具名赋值（官方就是具名形态，没有位置重载）。</summary>
        [Test]
        public void 具名参数被保留()
        {
            var file = new FilePathAttribute
            {
                ParentFolder = "Assets/Resources",
                Extensions = "cs, unity",
                AbsolutePath = true,
                RequireExistingPath = true,
                UseBackslashes = true,
            };

            Assert.That(file.ParentFolder, Is.EqualTo("Assets/Resources"));
            Assert.That(file.Extensions, Is.EqualTo("cs, unity"));
            Assert.That(file.AbsolutePath, Is.True);
            Assert.That(file.RequireExistingPath, Is.True);
            Assert.That(file.UseBackslashes, Is.True);
        }

        #endregion

        #region 用法与边界

        /// <summary>两个都仅用于成员且不可重复。</summary>
        [Test]
        public void 仅成员且不可重复()
        {
            AssertMemberOnly(typeof(FilePathAttribute));
            AssertMemberOnly(typeof(FolderPathAttribute));
        }

        /// <summary>
        /// 不声明官方那个 <c>IncludeFileExtension</c>——默认值与语义都没核对到，
        /// 留一个「设了也不产生行为」的开关正是本包最想避免的静默现象。
        /// </summary>
        /// <remarks>这条守卫的用意：哪天有人「顺手补上」，先被这里问一次。</remarks>
        [Test]
        public void 不声明未核实的IncludeFileExtension()
        {
            Assert.That(
                typeof(FilePathAttribute).GetProperty("IncludeFileExtension"),
                Is.Null,
                "该字段的默认值与确切语义未从官网核对到，故意不做——见类注释。");
        }

        #endregion

        #region Private Helpers

        /// <summary>断言「仅用于成员、不可重复、不作用于类」。</summary>
        /// <param name="type">特性类型。</param>
        private static void AssertMemberOnly(Type type)
        {
            var usage = type.GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, type.Name);
            Assert.That(usage.AllowMultiple, Is.False, type.Name);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True, type.Name);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False, type.Name);
        }

        #endregion
    }
}
