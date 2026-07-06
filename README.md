# Dynamite Timer

一个用于 **Casualties Unknown** 的 BepInEx/Harmony Mod。点燃炸药后，它会在炸药附近显示剩余爆炸倒计时，并标出炸药所在位置。

## 功能

- 在世界坐标中跟随炸药显示倒计时。
- 支持多个炸药同时倒计时。
- 显示炸药所在位置，例如 `右手 4.1s`、`左手 3.2s`、`口腔 2.5s`。
- 根据剩余时间改变文字颜色，方便快速判断危险程度。
- 对倒计时位置做平滑处理，减少角色移动和手臂晃动带来的文字抖动。
- 多个倒计时文本接近时会自动错开，降低重叠。
- 文本带黑色描边，提升复杂背景下的可读性。

## 安装

1. 编译项目，生成：

   ```text
   bin\Debug\DynamiteTimer.dll
   ```

2. 将 `DynamiteTimer.dll` 复制到游戏的 BepInEx 插件目录：

   ```text
   BepInEx\plugins\
   ```

3. 启动游戏。

4. 在 BepInEx 日志中看到以下信息即代表插件加载成功：

   ```text
   Dynamite Timer Loaded
   ```

## 构建

在项目目录运行：

```powershell
dotnet build .\DynamiteTimer.csproj
```

构建成功后，DLL 输出到：

```text
bin\Debug\DynamiteTimer.dll
```

## 修改显示语言

倒计时里的位置文字，例如：

```text
右手 4.1s
左手 3.2s
口腔 2.5s
```

主要在 `SlotLabelResolver.cs` 顶部修改：

```csharp
private const string GroundLabel = "地面";
private const string RightHandLabel = "右手";
private const string LeftHandLabel = "左手";
private const string MouthLabel = "口腔";
private const string UpperBackLabel = "上背部";
private const string MiddleBackLabel = "中背部";
private const string LowerBackLabel = "下背部";
private const string SlotLabel = "槽";
```

如果想改成英文，可以改成类似：

```csharp
private const string RightHandLabel = "Right hand";
private const string LeftHandLabel = "Left hand";
private const string MouthLabel = "Mouth";
```

时间后面的 `s` 在 `WorldTimerRenderer.cs` 的 `GetLabelText` 方法里拼接。如果要显示成大写 `S`、中文 `秒`，或其他格式，可以修改那里返回字符串的部分。

## 实现说明

游戏中炸药的原始逻辑位于 `CustomItemBehaviour.DynamiteExplode()`。点燃后，游戏会通过类似下面的调用延迟爆炸：

```csharp
component11.Invoke("DynamiteExplode", 5f);
```

本 Mod 使用两层逻辑保证倒计时能正常显示：

- Patch `Body.UseItem(Item item)` 和 `Body.UseItemInHand()`，在真正使用炸药时先用默认 5 秒启动倒计时，保证显示不丢。
- Patch 原版炸药 useAction 里的 `Invoke("DynamiteExplode", fuseSeconds)` 调用点。如果其他 Mod 只修改这个 `fuseSeconds`，倒计时会用实际传入 `Invoke` 的时间覆盖默认 5 秒。

倒计时组件会挂到被点燃的炸药对象上：

```csharp
DynamiteTimer
```

并使用实际引线时间设置结束时间：

```csharp
EndTime = Time.time + fuseSeconds;
```

如果其他 Mod 将原来的 `5f` 改成 `8f`、`2.5f` 或其他值，并且仍保留原版炸药 useAction 的 `Invoke("DynamiteExplode", fuseSeconds)` 调用，本 Mod 显示的时间会跟随实际引线时间。

## 槽位显示

当前槽位映射：

| Slot | 显示文本 |
| --- | --- |
| 0 | 右手 |
| 1 | 左手 |
| 2 | 口腔 |
| 3 | 上背部 |
| 4 | 中背部 |
| 5 | 下背部 |

手部显示为物理左右手，而不是“主手/副手”。这是因为游戏切换主手后，主手概念会变化，但玩家视角中物品仍在对应的左/右手位置，显示左右手更不容易误解。

## 调试日志

点燃炸药时会输出槽位信息，便于确认游戏版本变化后槽位映射是否仍正确。

示例：

```text
Dynamite timer started from UseItemInHand with fuse 5s
Dynamite slot from UseItemInHand: label=右手, slot=0, ...
```

如果后续游戏更新改变了槽位顺序，可以优先检查 `SlotLabelResolver.cs`。

## 已知行为

- 普通攻击不会触发炸药倒计时；只有真正使用炸药时才启动计时。
- 炸药倒计时文本使用 Unity IMGUI 绘制。
- 如果游戏字体不支持中文，中文标签可能显示为方块；可以按“修改显示语言”一节将标签改成英文。

## 主要文件

- `DynamiteTimerPlugin.cs`：BepInEx 插件入口。
- `BodyUseItemPatch.cs`：Harmony patch，启动炸药倒计时并尽量读取实际 `Invoke` 延迟。
- `DynamiteTimer.cs`：挂在每个炸药对象上的倒计时组件。
- `WorldTimerRenderer.cs`：世界坐标转屏幕坐标并绘制倒计时文本。
- `SlotLabelResolver.cs`：解析炸药所在槽位并生成显示标签。

## 技术环境

- 游戏：Casualties Unknown
- Unity：Mono
- Mod 框架：BepInEx
- Patch 框架：Harmony
- 项目类型：Class Library (.NET Framework)
- 目标框架：.NET Framework 4.7.2
