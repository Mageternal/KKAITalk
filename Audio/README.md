# KKAITalk 音频系统

## 目录结构

```
BepInEx\plugins\KKAITalk\
├── saves\                    # 存档文件夹（已存在）
├── audio\                     # 音频文件夹（自动创建）
│   ├── timbre\                # 音色参考音频
│   │   └── xxx.wav          # 参考音频文件
│   └── respite\              # 喘息音频
│       └── xxx\              # 按角色名的文件夹
│           ├── xxx_01.wav   # 喘息音频文件
│           ├── xxx_02.wav
│           └── xxx_03.wav
├── audio_config.ini          # 配置文件（自动创建）
└── KKAITalk.dll             # 插件

## 配置步骤

### 1. TTS 服务器

确保 Qwen3-TTS 服务已启动：
```bash
cd D:\AI\Qwen3-TTS-GGUF
python tts_server.py --model model-base
```

### 2. 配置角色音色

编辑 `audio_config.ini`：

```ini
[timbre_角色名]
ref_audio=角色名.wav          ; 参考音频文件名
ref_text=参考音频对应的文本   ; 必须是准确的文本
language=chinese             ; 语言
temperature=0.6               ; 采样温度
sub_temperature=0.6           ; 子采样温度
instruct=温柔撒娇地说         ; 情感指令（可选）
```

### 3. 配置喘息音频

**方式一**：在配置文件中指定：
```ini
[respite_角色名]
audio_files=01.wav|02.wav|03.wav
```

**方式二**：在 `audio\respite\角色名\` 文件夹下直接放入音频文件

## API 调用

```csharp
// 初始化（插件自动完成）
_audioManager.Initialize();

// 设置当前角色
_audioManager.SetCurrentCharacter("角色名");

// 播放发言（TTS合成）
_audioManager.Speak("要说的内容", () => {
    Debug.Log("发言完成");
});

// 播放喘息
_audioManager.PlayRespite();

// 停止所有音频
_audioManager.StopAll();

// 检查TTS服务器状态
_audioManager.CheckServerStatus((online) => {
    Debug.Log($"TTS服务器: {(online ? "在线" : "离线")}");
});
```

## 注意事项

1. **参考音频的文本必须准确** - 这是 TTS 克隆音色的关键
2. **喘息音频建议 1-3 秒** - 太长会影响节奏
3. **H场景发言配合** - 需要根据游戏状态协调喘息和发言
4. **TTS 首次合成较慢** - 后续使用缓存会快很多

## 情感指令示例

- 温柔撒娇地说
- 冷静地说
- 害羞地说
- 兴奋地说
- 疲惫地说
- 痛苦地说
