## BepInEx 插件开发经验
### 核心规则
我们再写.NET 3.5而不是.NET 6或者别的，写代码之前要注意

### Awake() 初始化顺序铁律
Log = Logger 和 Instance = this 必须是 Awake() 的前两行，
其他任何 Initialize() 调用必须在这两行之后。
**原因：** Initialize() 内部调用 AITalkPlugin.Log 时，若 Log 尚未赋值，会抛出 NullReferenceException。
注意：try-catch 包裹不能防止 catch 块本身的 NRE——若 catch 块内也调用 Log，而 Log 此时仍为 null，则 catch 块本身也炸。

### .NET 3.5 / Mono 语法限制
- 属性的 expression body（=> 简写）不可用：
  // 错误
  private static string BasePath => BepInEx.Paths.PluginPath;
  // 正确
  private static string BasePath
  {
      get { return BepInEx.Paths.PluginPath; }
  }
- 方法的 expression body 同理不可用：
  // 错误
  private string GetName() => _name;
  // 正确
  private string GetName() { return _name; }
  
- Lambda 表达式本身（LINQ、委托）正常使用，不受影响
- 空条件运算符 `?.Invoke()`（C# 6+）同样不兼容，需要展开：
  ```csharp
  // 错误
  onError?.Invoke("msg");
  // 正确
  if (onError != null) onError.Invoke("msg");
  ```
- 三元运算符 `a > 0 ? a : b` 和 `??` 空合并运算符是 .NET 2.0 / C# 2.0 就有的，不受影响

### Log 防护
所有 AITalkPlugin.Log 调用一律加 ?.：
```csharp
if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("...");
```
注意：在 Awake() 最开始两行（`Log = Logger; Instance = this;`）执行完毕之前，不要调用任何可能引用 Log 的方法（包括 Initialize()）。

### 调试技巧
无法使用断点，只能用日志逐行隔离。
遇到 NRE 时在每行之间插入 Log?.LogInfo("Step N")，
根据最后输出的 Step 定位崩溃位置。

### 常见陷阱
- static readonly 字段若未赋值默认为 null，Path.Combine(null, ...) 会 NRE
- AddComponent 之后立即调用 Initialize()，此时其他系统可能尚未就绪
- try/catch 包裹不能防止 catch 块本身的 NRE（Log 为 null 时）
- **BaseUnityPlugin 子类中某些字段声明会导致 TypeLoadException**
  - 现象：`TypeLoadException: Could not load type 'System.Runtime.CompilerServices.IteratorStateMachineAttribute' from assembly 'mscorlib'`
  - 原因：**不是协程的问题**，而是字段声明本身触发了 Mono 2.0 运行时对类型的额外检查
  - 触发条件不明确，经验证 `_lastAnimState = ""`、`_phase3StartTime = 0f` 等字段声明会触发
  - 解决思路：不要假设"只要不用协程就没问题"，逐步注释字段来找出触发者
  - 注意：这种 TypeLoadException 和 C# 6+ 语法错误是**两类完全不同的问题**，都需要分别排查
