# v1.3.0 验证结果

日期：2026-10-26（Asia/Shanghai）
游戏：0.8.0.3 / Steam Build 26197073，DPS Mod 1.1.2

## 独立副本 Unity 自检

[OneWaySpeaker TEST] PASS icon 13114022 dt[device_thumbnail_71]
[OneWaySpeaker TEST] PASS renamed 13114022
[OneWaySpeaker TEST] PASS description 13114022
[OneWaySpeaker TEST] PASS icon 13114023 dt[device_thumbnail_71]
[OneWaySpeaker TEST] PASS renamed 13114023
[OneWaySpeaker TEST] PASS description 13114023
[OneWaySpeaker TEST] PASS icon 13114024 dt[device_thumbnail_71]
[OneWaySpeaker TEST] PASS renamed 13114024
[OneWaySpeaker TEST] PASS description 13114024
[OneWaySpeaker TEST] PASS unrelated names unchanged
[OneWaySpeaker TEST] PASS description appended on new line
[OneWaySpeaker TEST] PASS bell database variants 3
[OneWaySpeaker TEST] PASS pixel texture
[OneWaySpeaker TEST] PASS inventory icon preserves tall proportions with centered transparent padding
[OneWaySpeaker TEST] PASS original bell prefab
[OneWaySpeaker TEST] PASS bell control panel prefab
[OneWaySpeaker TEST] PASS battle panel replaces both bell glyphs
[OneWaySpeaker TEST] PASS battle glyph preserves aspect and separate light/dark states
[OneWaySpeaker TEST] PASS battle background, attack button and native layout unchanged
[OneWaySpeaker TEST] PASS battle icon uses coarse 48px grid and three amber colors
[OneWaySpeaker TEST] PASS world renderer replacement
[OneWaySpeaker TEST] PASS preview camera layer preserved
[OneWaySpeaker TEST] PASS speaker and waves behind weapon slot device_slot_wheel
[OneWaySpeaker TEST] PASS speaker and waves behind weapon slot slot_top
[OneWaySpeaker TEST] PASS speaker and waves behind weapon slot slot_back
[OneWaySpeaker TEST] PASS speaker and waves behind weapon slot slot_back_top
[OneWaySpeaker TEST] PASS speaker and waves behind weapon slot slot_front
[OneWaySpeaker TEST] PASS speaker and waves behind weapon slot slot_front_top
[OneWaySpeaker TEST] PASS preview camera fits tall speaker without top clipping
[OneWaySpeaker TEST] PASS perspective preview keeps car large while fitting speakers
[OneWaySpeaker TEST] PASS speaker lowered 12 percent
[OneWaySpeaker TEST] PASS preview clone has one speaker at original size
[OneWaySpeaker TEST] PASS MP3 decode and loop playback
[OneWaySpeaker TEST] PASS repeat attack keeps music position
[OneWaySpeaker TEST] PASS fade distance matches HUD metres (world units x2)
[OneWaySpeaker TEST] PASS song stays at configured volume before final 20m
[OneWaySpeaker TEST] PASS song respects muted BGM settings
[OneWaySpeaker TEST] PASS 20m begins fade at full volume
[OneWaySpeaker TEST] PASS 10m song volume is half
[OneWaySpeaker TEST] PASS 5m song continues smooth fade
[OneWaySpeaker TEST] PASS song is silent at 0m
[OneWaySpeaker TEST] PASS repeat attack or reversing cannot restart finished fade
[OneWaySpeaker TEST] PASS music enables pixel sound waves
[OneWaySpeaker TEST] PASS sound waves animate during playback
[OneWaySpeaker TEST] PASS native BGM suppressed while preserving mute flags
[OneWaySpeaker TEST] PASS rest arrival stops song and begins native BGM from silence
[OneWaySpeaker TEST] PASS native BGM restores smoothly over 1.5 seconds
[OneWaySpeaker TEST] PASS recovery respects native rest volume adjustment
[OneWaySpeaker TEST] PASS native audio Update does not compound recovery gain
[OneWaySpeaker TEST] PASS recovery preserves pre-existing mute state
[OneWaySpeaker TEST] PASS recovery reaches current native level without restart or overshoot
[OneWaySpeaker TEST] PASS rest screen cannot restart music
[OneWaySpeaker TEST] PASS sound waves stop at rest screen
[OneWaySpeaker TEST] PASS next departure first attack starts new song
[OneWaySpeaker TEST] PASS run end stops music
[OneWaySpeaker TEST] PASS rest without a speaker song leaves native BGM untouched
[OneWaySpeaker TEST] ALL PASSED

## 实现与边界

- HUD 实际米数为 scene_remain_progress ×2，最后20m对应内部剩余10。
- 音箱音乐使用平滑距离包络：20m全音量、10m半音量、0m静音。
- 休整后使用非缩放时间，1.5秒恢复原版当前背景音乐输出。
- 原版 AudioSystem.Update 每帧先计算原生音量，再执行 Mod 增益，避免音量被覆盖或重复叠乘。
- 保留原版休整轨道音量变化、配置音量及原有静音状态。
- World.dll 296处、Commons.dll 4处原 DPS 调用保留。
- 更新不修改用户存档、DPS 历史或设置；更新后做哈希核对。
- 未实跑完整路线，渐变、界面事件及回归行为通过隔离自检与补丁结构校验验证。
