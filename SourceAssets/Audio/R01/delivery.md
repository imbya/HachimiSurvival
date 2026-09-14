# R01-S01：最小战斗音效

制作与提交日期：2026-09-14。执行者：ChatGPT（音频）。状态：**首版候选，待 review**。文件检查通过不等于人耳听审或 Unity 实测通过。

## 文件位置

- 本目录：六个 WAV 原件、`generate.py`、`requirements.txt`、`qa.json` 与本交付记录。
- `Review/`：单次、连续触发、混合试听 WAV，以及 `preview.html` 和时间轴。
- Unity：`HachimiSurvival/Assets/Audio/R01/SFX/` 放 `hiss.wav`、`enemy_warn.wav`、`interrupt.wav`、`player_hurt.wav`、`hit.wav`；`Voice/` 放 `laowu.wav`。

所有 WAV 沿用仓库现有 Git LFS 规则；没有修改 `.gitattributes`。Unity 与源文件目录引用相同音频数据，不是另一次制作。没有改场景、程序、项目设置或已有 `.meta`。

## 六个音效

统一为 **48 kHz、16-bit PCM、单声道 WAV**，仅一套音色，不含 BGM。

| 文件 | 时长 | 制作意图 |
| --- | ---: | --- |
| `hiss.wav` | 0.38 秒 | Q 哈气，喷气起音与气流嘶声 |
| `laowu.wav` | 0.32 秒 | 原创短喵呜拟声，开放元音收拢至“呜”，不复刻原录音 |
| `enemy_warn.wav` | 0.23 秒 | 两下略带不协和的中频预警脉冲 |
| `interrupt.wav` | 0.18 秒 | 短扣击与上行谐音确认 |
| `player_hurt.wav` | 0.18 秒 | 快速下坠拟声与柔和钝击 |
| `hit.wav` | 0.09 秒 | 低中频轻拍击，短尾音 |

## 制作与来源

工具：Python 3.13.5、NumPy 2.3.5、SciPy 1.17.0；标准库 `wave` 写入 PCM。音频模型：未调用。由 ChatGPT 编写并实际运行 [generate.py](generate.py)，输入仅为振荡器、伪随机噪声和参数曲线，没有外部录音、第三方声音采样或声音克隆。

提示词：未向音频生成模型提交提示词。制作 brief 为上表六项，遵循 [R01-S01](../../../plan/R01/audio/S01_Combat_Audio.md) 的时长、格式和短音节限制。`laowu.wav` 不是原梗猫叫录音，其猫梗识别度仍待听审。

合成采用 96 kHz 内部渲染、48 kHz 输出、噪声滤波、移动共振峰、音高与衰减曲线、去直流、首尾淡化、峰值约束和 16-bit TPDF 抖动；随机种子为 `26091401`。

工具使用条件参考：[Python](https://docs.python.org/3/license.html)、[NumPy](https://numpy.org/doc/stable/license.html)、[SciPy](https://scipy.org/faq/#what-are-scipy-s-licensing-terms)。这些链接说明工具许可，不是第三方录音授权或最终资产权属保证。本包不含外部录音；发布前由制作人更新素材使用记录。

## 已执行检查

[qa.json](qa.json) 记录本次复现的实际检查：六个原件的格式、时长、数字削波、首尾边界、直流偏移、首尾静音和至少 3 dB 峰值余量检查全部通过。峰值采用 8 倍过采样估计，不是认证真峰值仪器报告；K 加权能量代理不代表 LUFS 达标或已证实主观等响。

原生成脚本、六个音效和三个试听 WAV 均已与会话交付包核对一致。提交过程先验证生成结果，再上传并回读校验远端音频；Git LFS 媒体另用空缓存下载，确认还原的 WAV 与原件一致。临时传输分支已清理，传输工作流不并入主分支。

[复现与原件验证运行](https://github.com/imbya/HachimiSurvival/actions/runs/34857782166) 保留执行记录。仓库中的 `qa.json` 是生成器重新执行所得，不冒充额外的独立解码或人耳试听报告。

## 试听与待验收

将仓库拉取到本地、完成 Git LFS 下载后，打开 [Review/preview.html](Review/preview.html)。仓库版页面引用同目录 WAV，不嵌入重复音频数据；请保留目录结构，不单独复制 HTML。先从较低系统音量开始。

| 文件 | 内容 |
| --- | --- |
| [preview_single.wav](Review/preview_single.wav) | 7.20 秒；哈气→老吴→预警→打断→受伤→命中 |
| [preview_repeat.wav](Review/preview_repeat.wav) | 19.82 秒；分组连续触发，老吴约 0.769 秒一次，命中 60 ms 一次 |
| [preview_mix.wav](Review/preview_mix.wav) | 6.50 秒；离线混合模拟，不是游戏录屏 |

**未完成人耳单次／连续试听；未进行 Unity 导入、触发逻辑、混音或目标设备测试。** 本会话提供过单次串播放控件，文件也已实际渲染，但执行环境没有可靠的听觉回传。需确认老吴叫识别度、六声主观响度、重复疲劳，以及预警／受伤是否被其他声音遮蔽。

程序接入沿用任务要求：老吴每轮只播一次，普通命中每 60 ms 最多一声；梗人声静音不能同时关闭必要预警。`Review/preview_timeline.json` 的增益只是离线试听起点，不是已验证的游戏混音参数。

## 复现

在本目录运行：

```sh
python -m pip install -r requirements.txt
python generate.py --output ./rebuild
```

生成器默认拒绝覆盖已有 WAV，需显式指定 `--overwrite`；它不会下载音频、访问网络、修改 Unity 或操作 Git。
