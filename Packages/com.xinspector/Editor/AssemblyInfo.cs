using System.Runtime.CompilerServices;

// 编辑器侧同样默认 internal。构建期管线（PropertyTreeBuilder、DrawerTypeRegistry）
// 的内部细节不对第三方开放，只对测试开放。
[assembly: InternalsVisibleTo("XInspector.Tests.Editor")]
