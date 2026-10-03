using System.Runtime.CompilerServices;

// XInspector 默认 internal：公开面只保留特性与少数入口类型，其余一律 internal。
// 编辑器侧与测试侧经 InternalsVisibleTo 访问内部实现——这样「哪些是给第三方看的」
// 由可见性而不是文档约定来保证。
[assembly: InternalsVisibleTo("Venusir.Xinspector.Editor")]
[assembly: InternalsVisibleTo("Venusir.Xinspector.Tests")]
[assembly: InternalsVisibleTo("Venusir.Xinspector.Editor.Tests")]
