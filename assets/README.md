# 本地素材

游戏及米游社图标来自提供的图片素材，转换为最高 512×512 PNG 后嵌入 EXE，原图较小时保留原尺寸。大别野使用米游社图标。运行时不访问原始素材目录。

`fairy.ico` 由 `tools/CreateIcon.cs` 根据 Fairy 眼睛的圆形几何生成，含 16、32、48、64、128、256 像素尺寸，用于程序、窗口和托盘。参考项目声明见仓库根目录 `FAIRY-NOTICE.txt`。

界面与对话框以用户提供的绝区零 HDD 截图为视觉参考，用原生绘制实现深蓝背景、蓝色光晕、扫描线与右侧眼睛徽章，没有直接裁切参考截图。

`heybox.png` 来自用户本次提供的小黑盒标志图片，嵌入程序并用于平台选择、签到操作和小黑盒配置卡片。

`MiyouUI-Regular.ttf` 与 `MiyouUI-Bold.ttf` 由 Noto Sans SC 的 500/900 字重生成并重命名，中英文统一使用 Miyou UI 字体。字体嵌入 EXE，以进程私有方式加载，不向 Windows 安装字体。许可见 `NotoSansSC-OFL.txt`，来源声明见根目录 `THIRD-PARTY-NOTICES.txt`。`tools/prepare_ui_fonts.py` 可使用 `vendor/NotoSansSC-variable.ttf` 与 Python 的 fontTools 重新生成；普通构建直接使用现成字体，无需 Python。

